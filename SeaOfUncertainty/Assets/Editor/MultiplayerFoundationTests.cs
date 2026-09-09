using System;
using System.Collections.Generic;
using System.Linq;
using SeaOfUncertainty.Core;
using SeaOfUncertainty.Prototype;
using UnityEditor;
using UnityEngine;

namespace SeaOfUncertainty.Editor
{
    public static class MultiplayerFoundationTests
    {
        [MenuItem("Sea of Uncertainty/Tests/Run Multiplayer Foundation Suite")]
        public static void Run()
        {
            Assert(!OnlineMultiplayerCoordinator.IsDeveloperPreviewEnabled(Array.Empty<string>(), false, false), "Online preview remains hidden in a non-development release");
            Assert(OnlineMultiplayerCoordinator.IsDeveloperPreviewEnabled(new[] { "game.exe", "-enableOnlinePreview" }, false, false), "Explicit launch flag enables the online validation surface");
            AssertSessionLifecycleAndInterruptedRecovery();
            var server = new AuthoritativeMatchServer("PHASE1-SECURITY", 424242, ScenarioCatalog.Find("meridian-veil"));
            AssertPrivacy(server, Side.Blue);
            AssertPrivacy(server, Side.Red);

            Side actingSide = server.Game.Active.Side;
            ClientCommandEnvelope hold = Envelope(server, actingSide, 0, new AuthoritativeCommand
            {
                Kind = AuthoritativeCommandKind.Action, Side = actingSide, ActorId = server.Game.Active.Id, Action = ActionKind.Hold
            });
            var loopback = new LoopbackMultiplayerTransport(server, new LoopbackFaultProfile { LatencyTicks = 2, DuplicateEvery = 1 });
            loopback.Send(hold);
            Assert(loopback.PendingCount == 2 && loopback.Advance().Count == 0, "Loopback applies configured latency before delivery");
            IReadOnlyList<CommandReceipt> delivered = loopback.Advance();
            Assert(delivered.Count == 2 && delivered.Count(item => item.Accepted) == 2 && delivered.Any(item => item.Duplicate), "Duplicate delivery is idempotently acknowledged without applying state twice");
            Assert(server.Replay.Commands.Count == 1, "Duplicate transport packets create only one authoritative command event");

            CommandReceipt acceptedHold = delivered.First(item => !item.Duplicate);
            ReconnectPackage currentReconnect = server.Reconnect(actingSide, 0, acceptedHold.ViewDigest);
            Assert(!currentReconnect.SnapshotRequired && currentReconnect.Snapshot == null && currentReconnect.NextSequence == 1, "Current client reconnects without a redundant snapshot");
            ReconnectPackage staleReconnect = server.Reconnect(actingSide, -1, "stale");
            Assert(staleReconnect.SnapshotRequired && staleReconnect.Snapshot != null, "Stale client receives a side-scoped recovery snapshot");

            ClientCommandEnvelope outOfOrder = Envelope(server, actingSide, 3, new AuthoritativeCommand { Kind = AuthoritativeCommandKind.Action, Side = actingSide, ActorId = server.Game.Active.Id, Action = ActionKind.Hold });
            CommandReceipt orderRejected = server.Submit(outOfOrder);
            Assert(!orderRejected.Accepted && orderRejected.RequestResync && orderRejected.Code == "OUT_OF_ORDER" && !string.IsNullOrEmpty(orderRejected.DiagnosticId) && server.Diagnostics.Any(item => item.Id == orderRejected.DiagnosticId && !string.IsNullOrEmpty(item.AuthorityDigest)), "Out-of-order packet returns an opaque diagnostic ID while full-state evidence remains server-side");
            ClientCommandEnvelope stale = Envelope(server, actingSide, 1, outOfOrder.Command);
            stale.ExpectedViewDigest = "wrong-view";
            CommandReceipt staleRejected = server.Submit(stale);
            Assert(!staleRejected.Accepted && staleRejected.Code == "STALE_VIEW" && server.Replay.Commands.Count == 1, "Stale-view precondition prevents mutation");

            Side strikeSide = server.Game.Active.Side;
            ContactState strikeContact = server.Game.Contacts.First(item => item.Owner == strikeSide && !item.IsLost);
            FormationState hiddenTarget = server.Game.Find(strikeContact.TargetId);
            strikeContact.LastKnownPosition = server.Game.Active.Position;
            strikeContact.Location = LocationQuality.High;
            string opaqueRef = server.ContactReference(strikeSide, strikeContact);
            int strikeSequence = strikeSide == actingSide ? 1 : 0;
            AuthoritativeCommand wireStrike = new AuthoritativeCommand
            {
                Kind = AuthoritativeCommandKind.Action, Side = strikeSide, ActorId = server.Game.Active.Id, Action = ActionKind.Strike,
                TargetId = opaqueRef, Q = strikeContact.LastKnownPosition.Q, R = strikeContact.LastKnownPosition.R, Salvo = Salvo.Light, Reaction = Reaction.None
            };
            CommandReceipt strikeReceipt = server.Submit(Envelope(server, strikeSide, strikeSequence, wireStrike));
            Assert(strikeReceipt.Accepted && wireStrike.TargetId == opaqueRef && server.Replay.Commands.Last().TargetId == strikeContact.TargetId, "Authority translates an opaque Contact reference without mutating the wire command");
            string strikeView = JsonUtility.ToJson(strikeReceipt.Snapshot);
            Assert(!strikeView.Contains(strikeContact.TargetId) && (hiddenTarget == null || !strikeView.Contains(hiddenTarget.Name)), "Resolved receipt still excludes hidden target IDs and names");
            AssertPrivacy(server, strikeSide);

            var faultServer = new AuthoritativeMatchServer("PHASE1-FAULTS", 9911, ScenarioCatalog.Find("meridian-veil"));
            Side faultSide = faultServer.Game.Active.Side;
            string startingDigest = faultServer.ViewDigest(faultSide);
            var faults = new LoopbackMultiplayerTransport(faultServer, new LoopbackFaultProfile { LatencyTicks = 2, DropEvery = 3, DuplicateEvery = 2, ReverseReadyBatch = true });
            faults.Send(new ClientCommandEnvelope { MatchId = faultServer.MatchId, Side = faultSide, Sequence = 0, ExpectedViewDigest = startingDigest, Command = new AuthoritativeCommand { Kind = AuthoritativeCommandKind.Action, Side = faultSide, ActorId = faultServer.Game.Active.Id, Action = ActionKind.Hold } });
            faults.Send(new ClientCommandEnvelope { MatchId = faultServer.MatchId, Side = faultSide, Sequence = 1, ExpectedViewDigest = startingDigest, Command = new AuthoritativeCommand { Kind = AuthoritativeCommandKind.Action, Side = faultSide, ActorId = faultServer.Game.Active.Id, Action = ActionKind.Hold } });
            faults.Send(new ClientCommandEnvelope { MatchId = faultServer.MatchId, Side = faultSide, Sequence = 2, ExpectedViewDigest = startingDigest, Command = new AuthoritativeCommand { Kind = AuthoritativeCommandKind.Action, Side = faultSide, ActorId = faultServer.Game.Active.Id, Action = ActionKind.Hold } });
            Assert(faults.PendingCount == 3 && faults.Advance().Count == 0, "Loss simulation drops the configured packet while retaining delayed originals and duplicates");
            IReadOnlyList<CommandReceipt> faultReceipts = faults.Advance();
            Assert(faultReceipts.Count == 3 && faultReceipts.Any(item => item.Code == "OUT_OF_ORDER") && faultReceipts.Any(item => item.Code == "ACCEPTED"), "Reordered batch is rejected deterministically until the expected packet arrives");
            Assert(faultServer.Reconnect(faultSide, -1, string.Empty).SnapshotRequired, "Faulted client can recover from a fresh authoritative snapshot");

            Debug.Log("Sea of Uncertainty multiplayer tests passed: lobby, invitations, setup, signed interrupted recovery, authority, opaque Contacts, privacy, latency, loss, duplicates, ordering, reconnect, and diagnostics.");
        }

        private static void AssertSessionLifecycleAndInterruptedRecovery()
        {
            int secret = 0;
            Func<int, string> deterministicSecret = length =>
            {
                secret++;
                string source = ("ABCDEFGHJKLMNPQRSTUVWXYZ23456789" + secret.ToString("D8"));
                return new string(Enumerable.Range(0, length).Select(index => source[(index + secret) % source.Length]).ToArray());
            };
            var lifecycle = new MultiplayerMatchLifecycle(deterministicSecret);
            MultiplayerAccess host = lifecycle.Create("Meridian duel", "meridian-veil", 7719, "Blue Commander");
            Assert(host.Success && host.Lobby.Players.Count == 1 && host.Lobby.Players[0].Side == Side.Blue, "Host creates the Blue seat and a two-player lobby");
            Assert(host.Lobby.Players.All(player => string.IsNullOrEmpty(player.CredentialDigest)), "Public lobby views omit reconnect credential digests");
            Assert(lifecycle.InvitationUri().EndsWith(host.Lobby.JoinCode, StringComparison.Ordinal), "Invitation deep link carries only the join code");
            Assert(!lifecycle.JoinByCode("WRONG!", "Intruder").Success, "Invalid invitation is rejected");
            MultiplayerAccess guest = lifecycle.JoinByCode(" " + host.Lobby.JoinCode.ToLowerInvariant() + " ", "Red Commander");
            Assert(guest.Success && guest.Lobby.Players.Count == 2 && guest.Lobby.Players.Last().Side == Side.Red, "Invite code normalization joins the Red seat");
            Assert(!lifecycle.JoinByCode(host.Lobby.JoinCode, "Third Commander").Success, "A third commander cannot enter the full lobby");
            Assert(!lifecycle.SetReady(host.PlayerId, "wrong-credential", true).Success, "Ready state requires the reconnect credential");
            Assert(lifecycle.SetReady(host.PlayerId, host.ReconnectCredential, true).Success && lifecycle.SetReady(guest.PlayerId, guest.ReconnectCredential, true).Success, "Both authenticated commanders can ready");
            Assert(!lifecycle.Start(guest.PlayerId, guest.ReconnectCredential).Success, "Only the host can lock setup and start");
            Assert(lifecycle.Start(host.PlayerId, host.ReconnectCredential).Success && lifecycle.Authority != null, "Ready host starts the authoritative match");

            Side acting = lifecycle.Authority.Game.Active.Side;
            MultiplayerAccess actingAccess = acting == Side.Blue ? host : guest;
            var command = new ClientCommandEnvelope
            {
                MatchId = lifecycle.Authority.MatchId, Side = acting, Sequence = 0, ExpectedViewDigest = lifecycle.Authority.ViewDigest(acting),
                Command = new AuthoritativeCommand { Kind = AuthoritativeCommandKind.Action, Side = acting, ActorId = lifecycle.Authority.Game.Active.Id, Action = ActionKind.Hold }
            };
            CommandReceipt receipt = lifecycle.Submit(actingAccess.PlayerId, actingAccess.ReconnectCredential, command);
            Assert(receipt != null && receipt.Accepted && lifecycle.Authority.Replay.Commands.Count == 1, "Authenticated seat submits one authoritative command");
            MultiplayerAccess wrongSide = acting == Side.Blue ? guest : host;
            Assert(lifecycle.Submit(wrongSide.PlayerId, wrongSide.ReconnectCredential, command)?.Code == "SIDE_MISMATCH", "A valid player cannot submit the opponent's command envelope");

            var wire = new AuthoritativeWireProtocol(lifecycle);
            string unauthorizedJson = wire.Handle(JsonUtility.ToJson(new MultiplayerWirePacket
            {
                Kind = MultiplayerWireKind.Command, PlayerId = wrongSide.PlayerId, Credential = "wrong", CommandJson = JsonUtility.ToJson(command)
            }));
            MultiplayerWirePacket unauthorized = JsonUtility.FromJson<MultiplayerWirePacket>(unauthorizedJson);
            Assert(unauthorized.Kind == MultiplayerWireKind.Error && unauthorized.Code == "UNAUTHORIZED" && !unauthorizedJson.Contains("\"Snapshot\""), "Wire protocol returns no snapshot to an unauthenticated peer");
            string pingJson = wire.Handle(JsonUtility.ToJson(new MultiplayerWirePacket { Kind = MultiplayerWireKind.Ping, ClientTimestamp = 7719 }));
            MultiplayerWirePacket pong = JsonUtility.FromJson<MultiplayerWirePacket>(pingJson);
            Assert(pong.Kind == MultiplayerWireKind.Pong && pong.ClientTimestamp == 7719, "Wire protocol provides a latency probe without game state");

            Assert(lifecycle.Disconnect(guest.PlayerId, guest.ReconnectCredential).Success && lifecycle.Lobby.Phase == MultiplayerSessionPhase.Suspended, "Disconnect suspends an in-progress match while reserving the seat");
            const string signingKey = "test-only-authority-checkpoint-key";
            MultiplayerRecoveryCheckpoint checkpoint = lifecycle.CreateCheckpoint(host.PlayerId, host.ReconnectCredential, signingKey);
            Assert(checkpoint != null && !checkpoint.Payload.Contains(host.ReconnectCredential) && !checkpoint.Payload.Contains(guest.ReconnectCredential), "Authority checkpoint never stores raw reconnect credentials");
            var tampered = new MultiplayerRecoveryCheckpoint { Payload = checkpoint.Payload + " ", Signature = checkpoint.Signature };
            Assert(MultiplayerMatchLifecycle.RestoreCheckpoint(tampered, signingKey) == null, "Tampered interrupted-match checkpoint is rejected");
            MultiplayerMatchLifecycle restored = MultiplayerMatchLifecycle.RestoreCheckpoint(checkpoint, signingKey, deterministicSecret);
            Assert(restored != null && restored.Lobby.Phase == MultiplayerSessionPhase.Suspended && restored.Lobby.Players.All(player => !player.IsConnected), "Signed checkpoint restores into a disconnected suspended state");
            Assert(restored.Authority.Replay.Commands.Count == 1 && AuthoritativeReplay.StateDigest(restored.Authority.Game) == AuthoritativeReplay.StateDigest(lifecycle.Authority.Game), "Interrupted authority restores the deterministic command history and state");
            MultiplayerAccess recoveredGuest = restored.Reconnect(guest.PlayerId, guest.ReconnectCredential, -1, string.Empty);
            Assert(recoveredGuest.Success && recoveredGuest.Recovery?.Snapshot != null && recoveredGuest.Recovery.Snapshot.Formations.All(item => item.Side == Side.Red), "Returning player receives only the reserved side's recovery snapshot");
            MultiplayerAccess recoveredHost = restored.Reconnect(host.PlayerId, host.ReconnectCredential, -1, string.Empty);
            Assert(recoveredHost.Success && restored.Lobby.Phase == MultiplayerSessionPhase.InMatch, "Match resumes only after both reserved seats reconnect");
            CommandReceipt recoveredDuplicate = restored.Submit(actingAccess.PlayerId, actingAccess.ReconnectCredential, command);
            Assert(recoveredDuplicate != null && recoveredDuplicate.Duplicate && recoveredDuplicate.Code == "DUPLICATE_RECOVERED" && restored.Authority.Replay.Commands.Count == 1, "Pre-checkpoint retransmission cannot reapply a command");
        }

        private static ClientCommandEnvelope Envelope(AuthoritativeMatchServer server, Side side, int sequence, AuthoritativeCommand command)
            => new ClientCommandEnvelope { MatchId = server.MatchId, Side = side, Sequence = sequence, ExpectedViewDigest = server.ViewDigest(side), Command = command };

        private static void AssertPrivacy(AuthoritativeMatchServer server, Side viewer)
        {
            Side enemy = viewer == Side.Blue ? Side.Red : Side.Blue;
            string json = JsonUtility.ToJson(server.SnapshotFor(viewer));
            Assert(server.SnapshotFor(viewer).Formations.All(item => item.Side == viewer), "Side snapshot contains only owned formations");
            Assert(server.SnapshotFor(viewer).Contacts.All(item => item.ContactRef.StartsWith("TRACK-", StringComparison.Ordinal)), "Contacts use opaque stable track references");
            foreach (FormationState formation in server.Game.Formations.Where(item => item.Side == enemy))
                Assert(!json.Contains(formation.Id) && !json.Contains(formation.Name), $"{viewer} snapshot excludes hidden {enemy} formation identity {formation.Id}");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Multiplayer foundation test failed: " + message);
        }
    }
}
