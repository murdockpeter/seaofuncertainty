using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace SeaOfUncertainty.Core
{
    public enum MultiplayerSessionPhase { Lobby, InMatch, Suspended, Completed }

    [Serializable]
    public sealed class MultiplayerPlayerSeat
    {
        public string PlayerId;
        public string DisplayName;
        public Side Side;
        public bool IsHost;
        public bool IsReady;
        public bool IsConnected;
        public string CredentialDigest;
    }

    [Serializable]
    public sealed class MultiplayerLobby
    {
        public int Version = 1;
        public string SessionId;
        public string JoinCode;
        public string Name;
        public string ScenarioId;
        public int Seed;
        public bool IsPrivate = true;
        public int MaxPlayers = 2;
        public int Revision;
        public MultiplayerSessionPhase Phase;
        public List<MultiplayerPlayerSeat> Players = new List<MultiplayerPlayerSeat>();
    }

    public sealed class MultiplayerAccess
    {
        public bool Success;
        public string Code;
        public string Message;
        public string PlayerId;
        public string ReconnectCredential;
        public MultiplayerLobby Lobby;
        public ReconnectPackage Recovery;
    }

    [Serializable]
    public sealed class MultiplayerRecoveryCheckpoint
    {
        public int Version = 1;
        public string Payload;
        public string Signature;
    }

    [Serializable]
    internal sealed class MultiplayerRecoveryPayload
    {
        public int Version = 1;
        public MultiplayerLobby Lobby;
        public AuthoritativeReplayDocument Replay;
    }

    public enum MultiplayerWireKind { Command, Receipt, Reconnect, ReconnectResult, Ping, Pong, Error, Join, JoinResult, Ready, ReadyResult, StartResult }

    [Serializable]
    public sealed class MultiplayerWirePacket
    {
        public int Version = 1;
        public MultiplayerWireKind Kind;
        public string PlayerId;
        public string Credential;
        public long ClientTimestamp;
        public int LastAcknowledgedSequence = -1;
        public string KnownViewDigest;
        public string Code;
        public string Message;
        public string CommandJson;
        public string ReceiptJson;
        public string ReconnectJson;
    }

    /// <summary>Authority-side application protocol. Its response must be sent only to the requesting peer.</summary>
    public sealed class AuthoritativeWireProtocol
    {
        private readonly MultiplayerMatchLifecycle lifecycle;
        public AuthoritativeWireProtocol(MultiplayerMatchLifecycle lifecycle)
        {
            this.lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        }

        public string Handle(string json)
        {
            MultiplayerWirePacket request;
            try { request = JsonUtility.FromJson<MultiplayerWirePacket>(json); }
            catch (Exception) { return Error("MALFORMED_PACKET", "Packet is not valid JSON."); }
            if (request == null || request.Version != 1) return Error("BAD_WIRE_VERSION", "Unsupported multiplayer wire version.");
            if (request.Kind == MultiplayerWireKind.Ping)
                return JsonUtility.ToJson(new MultiplayerWirePacket { Kind = MultiplayerWireKind.Pong, ClientTimestamp = request.ClientTimestamp }, false);
            if (request.Kind == MultiplayerWireKind.Command)
            {
                ClientCommandEnvelope envelope;
                try { envelope = JsonUtility.FromJson<ClientCommandEnvelope>(request.CommandJson); }
                catch (Exception) { return Error("MALFORMED_COMMAND", "Command payload is not valid JSON."); }
                CommandReceipt receipt = lifecycle.Submit(request.PlayerId, request.Credential, envelope);
                if (receipt == null) return Error("UNAUTHORIZED", "Command sender is not an authenticated connected seat.");
                return JsonUtility.ToJson(new MultiplayerWirePacket { Kind = MultiplayerWireKind.Receipt, ReceiptJson = JsonUtility.ToJson(receipt, false) }, false);
            }
            if (request.Kind == MultiplayerWireKind.Reconnect)
            {
                MultiplayerAccess result = lifecycle.Reconnect(request.PlayerId, request.Credential, request.LastAcknowledgedSequence, request.KnownViewDigest);
                if (!result.Success) return Error(result.Code, result.Message);
                return JsonUtility.ToJson(new MultiplayerWirePacket { Kind = MultiplayerWireKind.ReconnectResult, ReconnectJson = result.Recovery == null ? string.Empty : JsonUtility.ToJson(result.Recovery, false), Code = result.Code, Message = result.Message }, false);
            }
            return Error("UNSUPPORTED_PACKET", "Packet kind is not accepted by the authority.");
        }

        private static string Error(string code, string message)
            => JsonUtility.ToJson(new MultiplayerWirePacket { Kind = MultiplayerWireKind.Error, Code = code, Message = message }, false);
    }

    /// <summary>
    /// Transport-independent lifecycle for a two-player authoritative match. Production session
    /// services mirror this state; reconnect credentials and full replay checkpoints remain private.
    /// </summary>
    public sealed class MultiplayerMatchLifecycle
    {
        private const string JoinAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        private readonly Func<int, string> secretSource;
        private MultiplayerLobby lobby;
        private AuthoritativeMatchServer authority;

        public MultiplayerLobby Lobby => Clone(lobby);
        public AuthoritativeMatchServer Authority => authority;

        public MultiplayerMatchLifecycle(Func<int, string> secretSource = null)
        {
            this.secretSource = secretSource ?? RandomSecret;
        }

        public MultiplayerAccess Create(string name, string scenarioId, int seed, string hostName, bool isPrivate = true)
        {
            if (lobby != null) return Fail("SESSION_EXISTS", "This lifecycle already owns a session.");
            ScenarioDefinition scenario = ScenarioCatalog.Find(scenarioId);
            if (scenario == null) return Fail("SCENARIO_NOT_FOUND", "The selected scenario does not exist.");
            string credential = secretSource(32);
            var host = new MultiplayerPlayerSeat
            {
                PlayerId = "player-" + secretSource(12).ToLowerInvariant(), DisplayName = CleanName(hostName), Side = Side.Blue,
                IsHost = true, IsConnected = true, CredentialDigest = Digest(credential)
            };
            lobby = new MultiplayerLobby
            {
                SessionId = "match-" + secretSource(16).ToLowerInvariant(), JoinCode = secretSource(6).ToUpperInvariant(),
                Name = string.IsNullOrWhiteSpace(name) ? "Sea of Uncertainty Match" : name.Trim(), ScenarioId = scenario.Id,
                Seed = seed, IsPrivate = isPrivate, Phase = MultiplayerSessionPhase.Lobby, Revision = 1,
                Players = new List<MultiplayerPlayerSeat> { host }
            };
            return Success(host, credential, "CREATED", "Lobby created. Share the invite code with the opposing commander.");
        }

        public MultiplayerAccess JoinByCode(string joinCode, string displayName)
        {
            if (lobby == null || !string.Equals(NormalizeCode(joinCode), lobby.JoinCode, StringComparison.Ordinal)) return Fail("INVITE_NOT_FOUND", "The invite code is invalid or expired.");
            if (lobby.Phase != MultiplayerSessionPhase.Lobby) return Fail("MATCH_STARTED", "This match is no longer accepting new players.");
            if (lobby.Players.Count >= lobby.MaxPlayers) return Fail("LOBBY_FULL", "This lobby already has both commanders.");
            string credential = secretSource(32);
            var guest = new MultiplayerPlayerSeat
            {
                PlayerId = "player-" + secretSource(12).ToLowerInvariant(), DisplayName = CleanName(displayName), Side = Side.Red,
                IsConnected = true, CredentialDigest = Digest(credential)
            };
            lobby.Players.Add(guest);
            lobby.Revision++;
            return Success(guest, credential, "JOINED", "Joined lobby as Red command.");
        }

        public string InvitationUri()
            => lobby == null ? string.Empty : "seaofuncertainty://join/" + lobby.JoinCode;

        public MultiplayerAccess SetReady(string playerId, string credential, bool ready)
        {
            MultiplayerPlayerSeat player = Authenticate(playerId, credential);
            if (player == null) return Fail("UNAUTHORIZED", "Player identity or reconnect credential is invalid.");
            if (lobby.Phase != MultiplayerSessionPhase.Lobby) return Fail("SETUP_LOCKED", "Match setup is locked after play begins.");
            player.IsReady = ready;
            lobby.Revision++;
            return Success(player, null, "READY_UPDATED", ready ? "Commander ready." : "Commander no longer ready.");
        }

        public MultiplayerAccess Start(string playerId, string credential)
        {
            MultiplayerPlayerSeat host = Authenticate(playerId, credential);
            if (host == null || !host.IsHost) return Fail("HOST_REQUIRED", "Only the authenticated host can start the match.");
            if (lobby.Phase != MultiplayerSessionPhase.Lobby) return Fail("BAD_PHASE", "The session is not waiting in the lobby.");
            if (lobby.Players.Count != lobby.MaxPlayers || lobby.Players.Any(player => !player.IsReady || !player.IsConnected))
                return Fail("PLAYERS_NOT_READY", "Both connected commanders must be present and ready.");
            authority = new AuthoritativeMatchServer(lobby.SessionId, lobby.Seed, ScenarioCatalog.Find(lobby.ScenarioId));
            lobby.Phase = MultiplayerSessionPhase.InMatch;
            lobby.Revision++;
            return Success(host, null, "STARTED", "Authoritative match started.");
        }

        public CommandReceipt Submit(string playerId, string credential, ClientCommandEnvelope envelope)
        {
            MultiplayerPlayerSeat player = Authenticate(playerId, credential);
            if (player == null || !player.IsConnected || authority == null || envelope == null) return null;
            if (player.Side != envelope.Side || envelope.Command == null || player.Side != envelope.Command.Side) return new CommandReceipt
            {
                MatchId = lobby.SessionId, Side = player.Side, Sequence = envelope.Sequence, Code = "SIDE_MISMATCH",
                Outcome = "Authenticated seat does not own the submitted command side.", RequestResync = true,
                Snapshot = authority.SnapshotFor(player.Side), ViewDigest = authority.ViewDigest(player.Side)
            };
            return authority.Submit(envelope);
        }

        public MultiplayerAccess Disconnect(string playerId, string credential)
        {
            MultiplayerPlayerSeat player = Authenticate(playerId, credential);
            if (player == null) return Fail("UNAUTHORIZED", "Player identity or reconnect credential is invalid.");
            player.IsConnected = false;
            if (lobby.Phase == MultiplayerSessionPhase.InMatch) lobby.Phase = MultiplayerSessionPhase.Suspended;
            lobby.Revision++;
            return Success(player, null, "DISCONNECTED", "Seat reserved for authenticated reconnection.");
        }

        public MultiplayerAccess Reconnect(string playerId, string credential, int lastAcknowledgedSequence, string knownViewDigest)
        {
            MultiplayerPlayerSeat player = Authenticate(playerId, credential);
            if (player == null) return Fail("UNAUTHORIZED", "Player identity or reconnect credential is invalid.");
            player.IsConnected = true;
            if (lobby.Phase == MultiplayerSessionPhase.Suspended && lobby.Players.All(item => item.IsConnected)) lobby.Phase = MultiplayerSessionPhase.InMatch;
            lobby.Revision++;
            MultiplayerAccess result = Success(player, null, "RECONNECTED", "Seat and side restored.");
            if (authority != null) result.Recovery = authority.Reconnect(player.Side, lastAcknowledgedSequence, knownViewDigest);
            return result;
        }

        public MultiplayerRecoveryCheckpoint CreateCheckpoint(string playerId, string credential, string signingKey)
        {
            MultiplayerPlayerSeat host = Authenticate(playerId, credential);
            if (host == null || !host.IsHost || authority == null || string.IsNullOrEmpty(signingKey)) return null;
            var payload = new MultiplayerRecoveryPayload { Lobby = Clone(lobby, true), Replay = authority.Replay };
            string json = JsonUtility.ToJson(payload, false);
            return new MultiplayerRecoveryCheckpoint { Payload = json, Signature = Sign(json, signingKey) };
        }

        public static MultiplayerMatchLifecycle RestoreCheckpoint(MultiplayerRecoveryCheckpoint checkpoint, string signingKey, Func<int, string> secretSource = null)
        {
            if (checkpoint == null || checkpoint.Version != 1 || string.IsNullOrEmpty(signingKey) || string.IsNullOrEmpty(checkpoint.Payload) ||
                !FixedEquals(checkpoint.Signature, Sign(checkpoint.Payload, signingKey))) return null;
            MultiplayerRecoveryPayload payload = JsonUtility.FromJson<MultiplayerRecoveryPayload>(checkpoint.Payload);
            if (payload?.Lobby == null || payload.Replay == null || payload.Lobby.Version != 1 || payload.Replay.Version != 1) return null;
            var restored = new MultiplayerMatchLifecycle(secretSource) { lobby = payload.Lobby };
            restored.authority = new AuthoritativeMatchServer(payload.Lobby.SessionId, payload.Replay);
            restored.lobby.Phase = MultiplayerSessionPhase.Suspended;
            foreach (MultiplayerPlayerSeat player in restored.lobby.Players) player.IsConnected = false;
            restored.lobby.Revision++;
            return restored;
        }

        private MultiplayerPlayerSeat Authenticate(string playerId, string credential)
        {
            if (lobby == null || string.IsNullOrEmpty(playerId) || string.IsNullOrEmpty(credential)) return null;
            MultiplayerPlayerSeat player = lobby.Players.FirstOrDefault(item => string.Equals(item.PlayerId, playerId, StringComparison.Ordinal));
            return player != null && FixedEquals(player.CredentialDigest, Digest(credential)) ? player : null;
        }

        private MultiplayerAccess Success(MultiplayerPlayerSeat player, string credential, string code, string message)
            => new MultiplayerAccess { Success = true, Code = code, Message = message, PlayerId = player.PlayerId, ReconnectCredential = credential, Lobby = Clone(lobby) };

        private MultiplayerAccess Fail(string code, string message)
            => new MultiplayerAccess { Code = code, Message = message, Lobby = Clone(lobby) };

        private static MultiplayerLobby Clone(MultiplayerLobby source, bool includeCredentialDigests = false)
        {
            if (source == null) return null;
            MultiplayerLobby copy = JsonUtility.FromJson<MultiplayerLobby>(JsonUtility.ToJson(source, false));
            if (!includeCredentialDigests)
                foreach (MultiplayerPlayerSeat player in copy.Players) player.CredentialDigest = string.Empty;
            return copy;
        }

        private static string CleanName(string value)
            => string.IsNullOrWhiteSpace(value) ? "Commander" : value.Trim().Substring(0, Math.Min(32, value.Trim().Length));

        private static string NormalizeCode(string value)
            => new string((value ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

        private static string RandomSecret(int length)
        {
            byte[] bytes = new byte[Math.Max(1, length)];
            using (RandomNumberGenerator random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
            var result = new StringBuilder(length);
            for (int index = 0; index < length; index++) result.Append(JoinAlphabet[bytes[index] % JoinAlphabet.Length]);
            return result.ToString();
        }

        private static string Digest(string value)
        {
            using (SHA256 sha = SHA256.Create()) return Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty)));
        }

        private static string Sign(string payload, string signingKey)
        {
            if (string.IsNullOrEmpty(signingKey)) return string.Empty;
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingKey))) return Hex(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload ?? string.Empty)));
        }

        private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();

        private static bool FixedEquals(string left, string right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            int difference = 0;
            for (int index = 0; index < left.Length; index++) difference |= left[index] ^ right[index];
            return difference == 0;
        }
    }
}
