using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace SeaOfUncertainty.Core
{
    [Serializable]
    public sealed class SideContactView
    {
        public string ContactRef;
        public HexCoord LastKnownPosition;
        public LocationQuality Location;
        public IdentityQuality Identity;
        public ContactDomain Domain;
        public int Age;
        public int MovementUncertainty;
        public bool IsFalse;
        public bool IsLost;
        public bool HasContradictoryPosition;
        public HexCoord ContradictoryPosition;
    }

    [Serializable]
    public sealed class SideStateView
    {
        public Side Side;
        public CommandArchitecture Architecture;
        public int CommandSlots;
        public int CommandStrain;
        public List<CommandSlotState> SlotStates = new List<CommandSlotState>();
        public List<string> ResponseHand = new List<string>();
    }

    [Serializable]
    public sealed class SideSnapshot
    {
        public int Version = 1;
        public string MatchId;
        public string ScenarioId;
        public Side Viewer;
        public int OperationalTime;
        public Side ActiveSide;
        public string ActiveFormationId;
        public string RuntimeWeather;
        public int RuntimeWeatherSeverity;
        public SideStateView Command;
        public List<FormationState> Formations = new List<FormationState>();
        public List<SideContactView> Contacts = new List<SideContactView>();
        public List<string> VisibleLog = new List<string>();
    }

    [Serializable]
    public sealed class ClientCommandEnvelope
    {
        public int Version = 1;
        public string MatchId;
        public Side Side;
        public int Sequence;
        public string ExpectedViewDigest;
        public AuthoritativeCommand Command;
    }

    [Serializable]
    public sealed class CommandReceipt
    {
        public int Version = 1;
        public string MatchId;
        public Side Side;
        public int Sequence;
        public int NextSequence;
        public bool Accepted;
        public bool Duplicate;
        public bool RequestResync;
        public string Code;
        public string Outcome;
        public string ViewDigest;
        public string DiagnosticId;
        public SideSnapshot Snapshot;
    }

    public sealed class ServerDisputeDiagnostic
    {
        public string Id;
        public Side Side;
        public int Sequence;
        public string Code;
        public string ClientViewDigest;
        public string AuthoritativeViewDigest;
        public string AuthorityDigest;
    }

    [Serializable]
    public sealed class ReconnectPackage
    {
        public int Version = 1;
        public string MatchId;
        public Side Side;
        public int NextSequence;
        public bool SnapshotRequired;
        public string ViewDigest;
        public SideSnapshot Snapshot;
    }

    public sealed class AuthoritativeMatchServer
    {
        private readonly Dictionary<Side, int> nextSequence = new Dictionary<Side, int>();
        private readonly Dictionary<Side, Dictionary<int, CommandReceipt>> receipts = new Dictionary<Side, Dictionary<int, CommandReceipt>>();
        private readonly Dictionary<Side, Dictionary<string, string>> targetByContactRef = new Dictionary<Side, Dictionary<string, string>>();
        private readonly Dictionary<Side, Dictionary<ContactState, string>> contactRefByContact = new Dictionary<Side, Dictionary<ContactState, string>>();
        private readonly AuthoritativeReplayDocument replay = new AuthoritativeReplayDocument();
        private readonly List<ServerDisputeDiagnostic> diagnostics = new List<ServerDisputeDiagnostic>();

        public string MatchId { get; }
        public PrototypeGame Game { get; }
        public AuthoritativeReplayDocument Replay => replay;
        public IReadOnlyList<ServerDisputeDiagnostic> Diagnostics => diagnostics;

        public AuthoritativeMatchServer(string matchId, int seed, ScenarioDefinition scenario = null)
        {
            MatchId = string.IsNullOrWhiteSpace(matchId) ? throw new ArgumentException("A stable match ID is required.", nameof(matchId)) : matchId;
            Game = new PrototypeGame(seed, scenario);
            replay.MatchHeader(Game);
            InitializeSideState();
        }

        public AuthoritativeMatchServer(string matchId, AuthoritativeReplayDocument recoveryReplay)
        {
            MatchId = string.IsNullOrWhiteSpace(matchId) ? throw new ArgumentException("A stable match ID is required.", nameof(matchId)) : matchId;
            if (recoveryReplay == null) throw new ArgumentNullException(nameof(recoveryReplay));
            AuthoritativeReplayResult restored = AuthoritativeReplay.Replay(recoveryReplay);
            if (!restored.Success) throw new InvalidOperationException("Authority replay recovery failed: " + restored.Message);
            Game = restored.Game;
            AuthoritativeReplayDocument copy = AuthoritativeReplay.FromJson(AuthoritativeReplay.ToJson(recoveryReplay, false));
            replay.Version = copy.Version;
            replay.ScenarioId = copy.ScenarioId;
            replay.Seed = copy.Seed;
            replay.Commands = copy.Commands ?? new List<AuthoritativeCommand>();
            replay.Events = copy.Events ?? new List<AuthoritativeEvent>();
            InitializeSideState();
            foreach (AuthoritativeCommand command in replay.Commands)
                nextSequence[command.Side]++;
        }

        private void InitializeSideState()
        {
            foreach (Side side in Enum.GetValues(typeof(Side)))
            {
                nextSequence[side] = 0;
                receipts[side] = new Dictionary<int, CommandReceipt>();
                targetByContactRef[side] = new Dictionary<string, string>(StringComparer.Ordinal);
                contactRefByContact[side] = new Dictionary<ContactState, string>();
            }
        }

        public string ContactReference(Side side, ContactState contact)
        {
            if (contact == null || contact.Owner != side || !Game.Contacts.Contains(contact)) return string.Empty;
            EnsureContactReferences(side);
            return contactRefByContact[side].TryGetValue(contact, out string value) ? value : string.Empty;
        }

        public SideSnapshot SnapshotFor(Side side)
        {
            EnsureContactReferences(side);
            SideState state = Game.Sides[side];
            return new SideSnapshot
            {
                MatchId = MatchId,
                ScenarioId = Game.Scenario.Id,
                Viewer = side,
                OperationalTime = Game.Time,
                ActiveSide = Game.Active?.Side ?? side,
                ActiveFormationId = Game.Active != null && Game.Active.Side == side ? Game.Active.Id : string.Empty,
                RuntimeWeather = Game.RuntimeWeather,
                RuntimeWeatherSeverity = Game.RuntimeWeatherSeverity,
                Command = new SideStateView
                {
                    Side = side, Architecture = state.Architecture, CommandSlots = state.CommandSlots, CommandStrain = state.CommandStrain,
                    SlotStates = CloneList(state.SlotStates), ResponseHand = Game.ResponseHand(side).Select(card => card.Id).ToList()
                },
                Formations = CloneList(Game.Formations.Where(item => item.Side == side).ToList()),
                Contacts = Game.Contacts.Where(item => item.Owner == side).Select(item => new SideContactView
                {
                    ContactRef = ContactReference(side, item), LastKnownPosition = item.LastKnownPosition, Location = item.Location,
                    Identity = item.Identity, Domain = item.Domain, Age = item.Age, MovementUncertainty = item.MovementUncertainty,
                    IsFalse = item.IsFalse, IsLost = item.IsLost, HasContradictoryPosition = item.HasContradictoryPosition,
                    ContradictoryPosition = item.ContradictoryPosition
                }).ToList(),
                VisibleLog = Game.VisibleLog(side).ToList()
            };
        }

        public string ViewDigest(Side side) => Digest(JsonUtility.ToJson(SnapshotFor(side), false));

        public CommandReceipt Submit(ClientCommandEnvelope envelope)
        {
            if (envelope == null) return Error(Side.Blue, -1, "MISSING_ENVELOPE", "Command envelope is missing.", true);
            Side side = envelope.Side;
            if (envelope.Version != 1 || envelope.Command == null) return Error(side, envelope.Sequence, "BAD_VERSION", "Unsupported envelope or missing command.", true);
            if (!string.Equals(envelope.MatchId, MatchId, StringComparison.Ordinal)) return Error(side, envelope.Sequence, "WRONG_MATCH", "Envelope belongs to another match.", true);
            if (envelope.Sequence < nextSequence[side])
            {
                if (receipts[side].TryGetValue(envelope.Sequence, out CommandReceipt prior)) return CloneReceipt(prior, true);
                CommandReceipt recoveredDuplicate = Error(side, envelope.Sequence, "DUPLICATE_RECOVERED", "Command predates the recovered authority checkpoint and was not applied again.", false);
                recoveredDuplicate.Duplicate = true;
                return recoveredDuplicate;
            }
            if (envelope.Sequence != nextSequence[side]) return Error(side, envelope.Sequence, "OUT_OF_ORDER", $"Expected command sequence {nextSequence[side]}.", true, envelope.ExpectedViewDigest);
            string currentView = ViewDigest(side);
            if (!string.Equals(envelope.ExpectedViewDigest, currentView, StringComparison.Ordinal))
                return Error(side, envelope.Sequence, "STALE_VIEW", "Client view does not match the authoritative side view.", true, envelope.ExpectedViewDigest);
            if (envelope.Command.Side != side) return Error(side, envelope.Sequence, "SIDE_MISMATCH", "Envelope and command sides differ.", true);

            AuthoritativeCommand translated = TranslateCommand(side, envelope.Command);
            AuthoritativeEvent resolved = AuthoritativeReplay.Record(replay, Game, translated);
            nextSequence[side]++;
            var receipt = new CommandReceipt
            {
                MatchId = MatchId, Side = side, Sequence = envelope.Sequence, NextSequence = nextSequence[side],
                Accepted = resolved.Accepted, Code = resolved.Accepted ? "ACCEPTED" : "RULE_REJECTED", Outcome = resolved.Outcome,
                Snapshot = SnapshotFor(side)
            };
            receipt.ViewDigest = Digest(JsonUtility.ToJson(receipt.Snapshot, false));
            receipts[side][envelope.Sequence] = receipt;
            return receipt;
        }

        public ReconnectPackage Reconnect(Side side, int lastAcknowledgedSequence, string knownViewDigest)
        {
            SideSnapshot snapshot = SnapshotFor(side);
            string digest = Digest(JsonUtility.ToJson(snapshot, false));
            bool current = lastAcknowledgedSequence == nextSequence[side] - 1 && string.Equals(knownViewDigest, digest, StringComparison.Ordinal);
            return new ReconnectPackage
            {
                MatchId = MatchId, Side = side, NextSequence = nextSequence[side], SnapshotRequired = !current,
                ViewDigest = digest, Snapshot = current ? null : snapshot
            };
        }

        private AuthoritativeCommand TranslateCommand(Side side, AuthoritativeCommand wire)
        {
            AuthoritativeCommand copy = JsonUtility.FromJson<AuthoritativeCommand>(JsonUtility.ToJson(wire));
            if (!string.IsNullOrEmpty(copy.TargetId) && targetByContactRef[side].TryGetValue(copy.TargetId, out string targetId)) copy.TargetId = targetId;
            copy.Sequence = replay.Commands.Count;
            return copy;
        }

        private void EnsureContactReferences(Side side)
        {
            foreach (ContactState contact in Game.Contacts.Where(item => item.Owner == side))
            {
                if (contactRefByContact[side].ContainsKey(contact)) continue;
                string reference = $"TRACK-{side.ToString().Substring(0, 1).ToUpperInvariant()}-{contactRefByContact[side].Count + 1:000}";
                contactRefByContact[side][contact] = reference;
                targetByContactRef[side][reference] = contact.TargetId ?? string.Empty;
            }
        }

        private CommandReceipt Error(Side side, int sequence, string code, string outcome, bool resync, string clientViewDigest = "")
        {
            SideSnapshot snapshot = SnapshotFor(side);
            string viewDigest = Digest(JsonUtility.ToJson(snapshot, false));
            string diagnosticId = $"DIAG-{MatchId}-{side}-{sequence}-{diagnostics.Count + 1:000}";
            diagnostics.Add(new ServerDisputeDiagnostic
            {
                Id = diagnosticId, Side = side, Sequence = sequence, Code = code, ClientViewDigest = clientViewDigest ?? string.Empty,
                AuthoritativeViewDigest = viewDigest, AuthorityDigest = AuthoritativeReplay.StateDigest(Game)
            });
            return new CommandReceipt
            {
                MatchId = MatchId, Side = side, Sequence = sequence, NextSequence = nextSequence[side], Code = code,
                Outcome = outcome, RequestResync = resync, Snapshot = snapshot, ViewDigest = viewDigest, DiagnosticId = diagnosticId
            };
        }

        private static CommandReceipt CloneReceipt(CommandReceipt receipt, bool duplicate)
        {
            CommandReceipt clone = JsonUtility.FromJson<CommandReceipt>(JsonUtility.ToJson(receipt));
            clone.Duplicate = duplicate;
            clone.Code = "DUPLICATE";
            return clone;
        }

        private static List<T> CloneList<T>(List<T> source)
        {
            var wrapper = new SerializableList<T> { Items = source ?? new List<T>() };
            return JsonUtility.FromJson<SerializableList<T>>(JsonUtility.ToJson(wrapper)).Items;
        }

        private static string Digest(string value)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty))).Replace("-", string.Empty).ToLowerInvariant();
        }

        [Serializable]
        private sealed class SerializableList<T> { public List<T> Items = new List<T>(); }
    }

    public static class ReplayDocumentExtensions
    {
        public static void MatchHeader(this AuthoritativeReplayDocument replay, PrototypeGame game)
        {
            replay.ScenarioId = game.Scenario.Id;
            replay.Seed = game.Seed;
        }
    }

    [Serializable]
    public sealed class LoopbackFaultProfile
    {
        public int LatencyTicks = 1;
        public int DropEvery;
        public int DuplicateEvery;
        public bool ReverseReadyBatch;
    }

    public sealed class LoopbackMultiplayerTransport
    {
        private sealed class Pending
        {
            public int DeliverAt;
            public int Order;
            public ClientCommandEnvelope Envelope;
        }

        private readonly AuthoritativeMatchServer server;
        private readonly LoopbackFaultProfile faults;
        private readonly List<Pending> pending = new List<Pending>();
        private int sent;
        private int order;
        public int Tick { get; private set; }

        public LoopbackMultiplayerTransport(AuthoritativeMatchServer server, LoopbackFaultProfile faults = null)
        {
            this.server = server ?? throw new ArgumentNullException(nameof(server));
            this.faults = faults ?? new LoopbackFaultProfile();
        }

        public void Send(ClientCommandEnvelope envelope)
        {
            sent++;
            if (faults.DropEvery > 0 && sent % faults.DropEvery == 0) return;
            Queue(envelope);
            if (faults.DuplicateEvery > 0 && sent % faults.DuplicateEvery == 0) Queue(envelope);
        }

        public IReadOnlyList<CommandReceipt> Advance(int ticks = 1)
        {
            Tick += Math.Max(0, ticks);
            List<Pending> ready = pending.Where(item => item.DeliverAt <= Tick).OrderBy(item => item.Order).ToList();
            if (faults.ReverseReadyBatch) ready.Reverse();
            foreach (Pending item in ready) pending.Remove(item);
            return ready.Select(item => server.Submit(item.Envelope)).ToList();
        }

        public int PendingCount => pending.Count;
        private void Queue(ClientCommandEnvelope envelope) => pending.Add(new Pending { DeliverAt = Tick + Math.Max(0, faults.LatencyTicks), Order = order++, Envelope = envelope });
    }
}
