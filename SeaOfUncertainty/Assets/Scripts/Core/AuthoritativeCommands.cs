using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace SeaOfUncertainty.Core
{
    public enum AuthoritativeCommandKind
    {
        Action,
        AssignMission,
        PlayResponse,
        RespondToEntropy,
        PushThrough,
        RestoreCommand,
        DeclareSynchronizedStrike,
        ResolveSynchronizedStrike,
        AbortSynchronizedStrike,
        RetaskSynchronizedStrike
    }

    [Serializable]
    public sealed class AuthoritativeCommand
    {
        public int Version = 1;
        public int Sequence;
        public AuthoritativeCommandKind Kind;
        public Side Side;
        public string ActorId;
        public ActionKind Action;
        public string TargetId;
        public string CardId;
        public string EventId;
        public int Q;
        public int R;
        public MoveMode MoveMode;
        public SearchMode SearchMode;
        public SearchPriority SearchPriority;
        public Salvo Salvo;
        public Reaction Reaction;
        public bool HasEvadeDestination;
        public int EvadeQ;
        public int EvadeR;
        public PatrolPosture PatrolPosture;
        public SupportKind SupportKind;
        public EntropySource EntropySource;
        public MissionObjectiveKind MissionObjective;
        public MissionPosture MissionPosture;
        public MissionTrigger MissionTrigger;
        public bool ContinueBlind;
        public bool UsePriorPlanning;
        public string DeconflictedFormationId;
        public List<string> ParticipantIds = new List<string>();

        public HexCoord Hex => new HexCoord(Q, R);
        public HexCoord? EvadeDestination => HasEvadeDestination ? new HexCoord(EvadeQ, EvadeR) : (HexCoord?)null;

        public static AuthoritativeCommand FromAiDecision(PrototypeGame game, AiDecision decision)
        {
            if (game == null || game.Active == null || decision == null) return null;
            FormationState actor = game.Active;
            var command = new AuthoritativeCommand
            {
                Kind = decision.DeclareSynchronizedStrike ? AuthoritativeCommandKind.DeclareSynchronizedStrike :
                    !string.IsNullOrEmpty(decision.SynchronizedStrikeId) ? AuthoritativeCommandKind.ResolveSynchronizedStrike : AuthoritativeCommandKind.Action,
                Side = actor.Side, ActorId = actor.Id, Action = decision.Action, TargetId = decision.TargetId,
                EventId = decision.SynchronizedStrikeId, Q = decision.Hex.Q, R = decision.Hex.R,
                MoveMode = decision.MoveMode, SearchMode = decision.SearchMode, Salvo = decision.Salvo,
                PatrolPosture = decision.PatrolPosture, SupportKind = decision.SupportKind,
                UsePriorPlanning = decision.UsePriorPlanning, DeconflictedFormationId = decision.DeconflictedFormationId,
                ParticipantIds = decision.ParticipantIds == null ? new List<string>() : new List<string>(decision.ParticipantIds)
            };
            FormationState target = game.Find(decision.TargetId);
            if (decision.Action == ActionKind.Strike && target != null && !target.IsDestroyed && target.Position.Equals(decision.Hex))
            {
                command.Reaction = PrototypeAiCommander.ChooseReaction(game, actor, target);
                HexCoord? evade = command.Reaction == Reaction.Evade ? PrototypeAiCommander.ChooseEvadeDestination(game, actor, target) : null;
                if (evade.HasValue) { command.HasEvadeDestination = true; command.EvadeQ = evade.Value.Q; command.EvadeR = evade.Value.R; }
            }
            return command;
        }
    }

    [Serializable]
    public sealed class AuthoritativeEvent
    {
        public int Version = 1;
        public int Sequence;
        public int OperationalTimeBefore;
        public int OperationalTimeAfter;
        public bool Accepted;
        public string StateBefore;
        public string StateAfter;
        public string Outcome;
    }

    [Serializable]
    public sealed class AuthoritativeReplayDocument
    {
        public int Version = 1;
        public string ScenarioId;
        public int Seed;
        public List<AuthoritativeCommand> Commands = new List<AuthoritativeCommand>();
        public List<AuthoritativeEvent> Events = new List<AuthoritativeEvent>();
    }

    public sealed class AuthoritativeReplayResult
    {
        public bool Success;
        public int CommandsApplied;
        public int FailedSequence = -1;
        public string Message;
        public PrototypeGame Game;
    }

    public static class AuthoritativeCommandProcessor
    {
        public static bool Execute(PrototypeGame game, AuthoritativeCommand command, out string message)
        {
            message = "Command or game is missing.";
            if (game == null || command == null) return false;
            if (command.Version != 1) { message = $"Unsupported command version {command.Version}."; return false; }
            FormationState actor = game.Find(command.ActorId);
            if (actor == null) { message = $"Formation {command.ActorId} does not exist."; return false; }
            if (actor.Side != command.Side) { message = "Command side does not own the acting Formation."; return false; }
            ContactState contact = game.Contacts.FirstOrDefault(item => item.Owner == command.Side && !item.IsLost && item.TargetId == command.TargetId);
            HexCoord? evade = command.EvadeDestination;

            switch (command.Kind)
            {
                case AuthoritativeCommandKind.Action:
                    if (actor != game.Active) { message = "Command actor is not the active Formation."; return false; }
                    switch (command.Action)
                    {
                        case ActionKind.Move: return game.Move(actor, command.Hex, command.MoveMode, out message);
                        case ActionKind.Search: return game.SearchArea(actor, command.Hex, command.SearchMode, command.SearchPriority, out message);
                        case ActionKind.Strike:
                            if (contact == null) { message = "Command target is not a usable owned Contact."; return false; }
                            return game.StrikeContact(actor, contact, command.Hex, command.Salvo, command.Reaction, evade, out _, out message);
                        case ActionKind.Patrol: return game.Patrol(actor, command.Hex, command.PatrolPosture, game.Find(command.TargetId), out message);
                        case ActionKind.Support: return game.Support(actor, game.Find(command.TargetId), command.SupportKind, out message);
                        case ActionKind.Recover: return string.IsNullOrEmpty(command.CardId) ? game.Recover(actor, out message) : game.Recover(actor, command.EntropySource, command.CardId, out message);
                        case ActionKind.Replenish: return game.Replenish(actor, command.CardId, out message);
                        default: return game.Hold(actor, out message);
                    }
                case AuthoritativeCommandKind.AssignMission:
                    return game.AssignStandingMission(actor, command.Action, command.MissionObjective, command.TargetId, command.Hex, command.MissionPosture, command.MissionTrigger, out message);
                case AuthoritativeCommandKind.PlayResponse:
                    return game.PlayCommandResponse(command.Side, command.CardId, actor, contact, command.Hex, command.Action, out message);
                case AuthoritativeCommandKind.RespondToEntropy:
                    return game.RespondToEntropy(actor, command.CardId, out message);
                case AuthoritativeCommandKind.PushThrough:
                    return game.PushThrough(actor, out message);
                case AuthoritativeCommandKind.RestoreCommand:
                    return game.RestoreCommand(actor, out message);
                case AuthoritativeCommandKind.DeclareSynchronizedStrike:
                    return game.DeclareSynchronizedStrike(actor, command.ParticipantIds.Select(game.Find), contact, command.Hex, command.Salvo,
                        command.UsePriorPlanning, command.DeconflictedFormationId, out _, out message);
                case AuthoritativeCommandKind.ResolveSynchronizedStrike:
                    return game.ResolveSynchronizedStrike(game.SynchronizedStrikes.FirstOrDefault(item => item.Id == command.EventId), command.Reaction,
                        evade, command.ContinueBlind, out _, out message);
                case AuthoritativeCommandKind.AbortSynchronizedStrike:
                    return game.AbortSynchronizedStrike(game.SynchronizedStrikes.FirstOrDefault(item => item.Id == command.EventId), out message);
                case AuthoritativeCommandKind.RetaskSynchronizedStrike:
                    return game.RetaskSynchronizedStrike(game.SynchronizedStrikes.FirstOrDefault(item => item.Id == command.EventId), contact, command.Hex, out message);
                default:
                    message = $"Unsupported command kind {command.Kind}.";
                    return false;
            }
        }
    }

    public static class AuthoritativeReplay
    {
        public static AuthoritativeEvent Record(AuthoritativeReplayDocument document, PrototypeGame game, AuthoritativeCommand command)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (game == null) throw new ArgumentNullException(nameof(game));
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (string.IsNullOrEmpty(document.ScenarioId)) { document.ScenarioId = game.Scenario.Id; document.Seed = game.Seed; }
            command.Sequence = document.Commands.Count;
            string before = StateDigest(game);
            int timeBefore = game.Time;
            bool accepted = AuthoritativeCommandProcessor.Execute(game, command, out string outcome);
            var resolved = new AuthoritativeEvent
            {
                Sequence = command.Sequence, OperationalTimeBefore = timeBefore, OperationalTimeAfter = game.Time,
                Accepted = accepted, StateBefore = before, StateAfter = StateDigest(game), Outcome = outcome ?? string.Empty
            };
            document.Commands.Add(command);
            document.Events.Add(resolved);
            return resolved;
        }

        public static AuthoritativeReplayResult Replay(AuthoritativeReplayDocument document)
        {
            var result = new AuthoritativeReplayResult { Message = "Replay document is missing." };
            if (document == null) return result;
            if (document.Version != 1) { result.Message = $"Unsupported replay version {document.Version}."; return result; }
            ScenarioDefinition scenario = ScenarioCatalog.Find(document.ScenarioId);
            if (scenario == null) { result.Message = $"Scenario {document.ScenarioId} was not found."; return result; }
            var game = new PrototypeGame(document.Seed, scenario);
            result.Game = game;
            for (int index = 0; index < document.Commands.Count; index++)
            {
                AuthoritativeCommand command = document.Commands[index];
                AuthoritativeEvent expected = document.Events != null && index < document.Events.Count ? document.Events[index] : null;
                string before = StateDigest(game);
                if (expected != null && expected.StateBefore != before) return Fail(result, command.Sequence, $"State diverged before command {command.Sequence}.");
                bool accepted = AuthoritativeCommandProcessor.Execute(game, command, out string outcome);
                string after = StateDigest(game);
                if (expected != null && (expected.Accepted != accepted || expected.StateAfter != after || expected.OperationalTimeAfter != game.Time))
                    return Fail(result, command.Sequence, $"State diverged after command {command.Sequence}: {outcome}");
                result.CommandsApplied++;
            }
            result.Success = true;
            result.Message = $"Replayed {result.CommandsApplied} commands without divergence; final state {StateDigest(game)}.";
            return result;
        }

        public static string ToJson(AuthoritativeReplayDocument document, bool pretty = true) => JsonUtility.ToJson(document, pretty);
        public static AuthoritativeReplayDocument FromJson(string json) => JsonUtility.FromJson<AuthoritativeReplayDocument>(json);

        public static string StateDigest(PrototypeGame game)
        {
            string canonical = JsonUtility.ToJson(game.CaptureState(), false);
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static AuthoritativeReplayResult Fail(AuthoritativeReplayResult result, int sequence, string message)
        {
            result.FailedSequence = sequence;
            result.Message = message;
            return result;
        }
    }
}
