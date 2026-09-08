using System;
using System.Collections.Generic;
using System.Linq;

namespace SeaOfUncertainty.Core
{
    public sealed class AiDecision
    {
        public ActionKind Action;
        public HexCoord Hex;
        public MoveMode MoveMode;
        public SearchMode SearchMode;
        public Salvo Salvo;
        public string TargetId;
        public PatrolPosture PatrolPosture;
        public SupportKind SupportKind;
        public bool DeclareSynchronizedStrike;
        public string SynchronizedStrikeId;
        public List<string> ParticipantIds = new List<string>();
        public bool UsePriorPlanning;
        public string DeconflictedFormationId;
        public string Rationale;

        public string ModeName => DeclareSynchronizedStrike ? "Synchronized Declaration" : !string.IsNullOrEmpty(SynchronizedStrikeId) ? "Synchronized Resolution" : Action == ActionKind.Move ? MoveMode.ToString() : Action == ActionKind.Search ? SearchMode.ToString() : Action == ActionKind.Strike ? Salvo.ToString() : Action == ActionKind.Patrol ? PatrolPosture.ToString() : Action == ActionKind.Support ? SupportKind.ToString() : "Default";
    }

    public static class PrototypeAiCommander
    {
        public static AiDecision Choose(PrototypeGame game)
        {
            if (game?.Active == null) return null;
            FormationState actor = game.Active;
            SynchronizedStrikeState readySynchronized = game.ReadySynchronizedStrikeFor(actor);
            if (readySynchronized != null)
                return new AiDecision { Action = ActionKind.Strike, SynchronizedStrikeId = readySynchronized.Id, TargetId = readySynchronized.ContactTargetId, Hex = readySynchronized.Aim, Salvo = readySynchronized.Participants.First().Salvo, Rationale = "Resolve the reserved synchronized event at its scheduled Strike Time." };
            List<ContactState> contacts = game.Contacts
                .Where(contact => contact.Owner == actor.Side && !contact.IsLost)
                .OrderByDescending(contact => contact.Location)
                .ThenByDescending(contact => contact.Identity)
                .ThenBy(contact => contact.Age)
                .ThenBy(contact => contact.TargetId, StringComparer.Ordinal)
                .ToList();

            if (game.Sides[actor.Side].CommandSlots == 0)
            {
                AiDecision standingMission = ChooseStandingMission(game, actor, contacts);
                if (standingMission != null) return standingMission;
            }

            ContactState strikeContact = contacts
                .Where(contact => SelectSalvo(actor, contact).HasValue)
                .OrderByDescending(contact => contact.Identity)
                .ThenByDescending(contact => contact.Location)
                .ThenBy(contact => contact.Age)
                .ThenBy(contact => HexCoord.Distance(actor.Position, contact.LastKnownPosition))
                .FirstOrDefault();
            if (strikeContact != null && strikeContact.Location == LocationQuality.High && strikeContact.Identity == IdentityQuality.Identified &&
                !game.SynchronizedStrikes.Any(item => item.Side == actor.Side) && game.Sides[actor.Side].CommandSlots > 0)
            {
                List<FormationState> synchronized = game.EligibleSynchronizedStrikeParticipants(actor.Side, strikeContact, strikeContact.LastKnownPosition, Salvo.Standard)
                    .Where(item => item == actor || item.ReadyTime <= actor.ReadyTime + 2).OrderBy(item => item == actor ? 0 : 1).ThenByDescending(item => item.EffectiveStrike).ThenBy(item => item.Id).Take(3).ToList();
                if (synchronized.Contains(actor) && synchronized.Count >= 2)
                {
                    IReadOnlyList<CommandResponseDefinition> hand = game.ResponseHand(actor.Side);
                    return new AiDecision
                    {
                        Action = ActionKind.Strike, DeclareSynchronizedStrike = true, TargetId = strikeContact.TargetId, Hex = strikeContact.LastKnownPosition, Salvo = Salvo.Standard,
                        ParticipantIds = synchronized.Select(item => item.Id).ToList(), UsePriorPlanning = hand.Any(card => card.Id == "C-02"),
                        DeconflictedFormationId = hand.Any(card => card.Id == "C-10") ? synchronized.Last().Id : null,
                        Rationale = "Reserve nearby available formations against a high-location identified Contact using only owned Contact information."
                    };
                }
            }
            if (strikeContact != null)
            {
                Salvo selectedSalvo = SelectSalvo(actor, strikeContact).Value;
                return new AiDecision { Action = ActionKind.Strike, TargetId = strikeContact.TargetId, Hex = strikeContact.LastKnownPosition, Salvo = selectedSalvo, Rationale = selectedSalvo == Salvo.Heavy ? "Commit the expendable Heavy capability to a high-quality identified Contact inside extended range." : "Engage the strongest usable owned Contact area already inside weapon range." };
            }

            if (game.HasLogisticsAccess(actor) && game.NeedsReplenishment(actor))
                return new AiDecision { Action = ActionKind.Replenish, Rationale = "Use current logistics access to restore Endurance, weapons, damage, and one capability loss." };

            if (actor.Friction || actor.Disruption)
                return new AiDecision { Action = ActionKind.Recover, Rationale = "Clear recoverable Entropy before another complex action." };

            FormationState supportRecipient = game.Formations.Where(candidate => candidate.Side == actor.Side && candidate != actor && !candidate.IsDestroyed && HexCoord.Distance(actor.Position, candidate.Position) <= Rules.SupportRange)
                .OrderByDescending(candidate => candidate.EffectiveStrike).ThenBy(candidate => candidate.Id, StringComparer.Ordinal).FirstOrDefault();
            if (!actor.SupportActive && !actor.SupportBlockedUntilRecover && supportRecipient != null && actor.Kind == FormationKind.AirGroup && !actor.HasEffect("X-08"))
                return new AiDecision { Action = ActionKind.Support, TargetId = supportRecipient.Id, SupportKind = SupportKind.Strike, Rationale = "Assign available air Support to the strongest nearby friendly striking formation." };

            ContactState searchContact = contacts.FirstOrDefault(contact => HexCoord.Distance(actor.Position, contact.LastKnownPosition) <= game.SearchRangeFor(actor, actor.Kind == FormationKind.Submarine ? SearchMode.Passive : SearchMode.Active));
            if (searchContact != null && (actor.Kind == FormationKind.Submarine || actor.EffectiveSearch >= 3 || searchContact.Age > 0 || searchContact.Location < LocationQuality.High))
            {
                bool needsFocused = actor.Kind != FormationKind.Submarine && game.Sides[actor.Side].CommandSlots > 0 && (searchContact.Age >= 2 || searchContact.Location == LocationQuality.Low);
                SearchMode mode = actor.Kind == FormationKind.Submarine ? SearchMode.Passive : needsFocused ? SearchMode.Focused : SearchMode.Active;
                return new AiDecision { Action = ActionKind.Search, Hex = searchContact.LastKnownPosition, SearchMode = mode, Rationale = needsFocused ? "Use available Command to improve a stale or low-quality owned Contact." : "Refresh the best available Contact without consulting hidden enemy state." };
            }

            AiDecision movement = ChooseObjectiveMove(game, actor);
            if (movement != null) return movement;

            if (!actor.PatrolActive)
                return new AiDecision { Action = ActionKind.Patrol, Hex = actor.Position, PatrolPosture = PatrolPosture.Balanced, Rationale = "Screen the objective area with a persistent interception." };

            SearchMode fallbackMode = actor.Kind == FormationKind.Submarine ? SearchMode.Passive : SearchMode.Active;
            HexCoord searchCenter = HexCoord.Distance(actor.Position, game.Area.Objective) <= game.SearchRangeFor(actor, fallbackMode) ? game.Area.Objective : actor.Position;
            return new AiDecision { Action = ActionKind.Search, Hex = searchCenter, SearchMode = fallbackMode, Rationale = "Search the operational objective when no stronger Contact or movement opportunity exists." };
        }

        public static Reaction ChooseReaction(PrototypeGame game, FormationState attacker, FormationState defender)
        {
            IReadOnlyList<Reaction> legal = game.AvailableReactions(attacker, defender);
            if (legal.Contains(Reaction.None)) return Reaction.None;
            if (defender.OrderlyWithdrawalReady && legal.Contains(Reaction.Evade)) return Reaction.Evade;
            if ((defender.Damage >= DamageState.Heavy || defender.Endurance == Endurance.Critical) && legal.Contains(Reaction.Evade)) return Reaction.Evade;
            if (legal.Contains(Reaction.Counterattack) && defender.EffectiveStrike >= attacker.EffectiveDefense) return Reaction.Counterattack;
            return legal.Contains(Reaction.Defend) ? Reaction.Defend : legal[0];
        }

        public static HexCoord? ChooseEvadeDestination(PrototypeGame game, FormationState attacker, FormationState defender)
        {
            int allowance = defender.OrderlyWithdrawalReady ? 2 : 1;
            IReadOnlyList<HexCoord> legal = game.LegalEvadeDestinations(attacker, defender, allowance);
            return legal.Count > 0 ? legal[0] : (HexCoord?)null;
        }

        public static bool Execute(PrototypeGame game, AiDecision decision, out string message)
            => Execute(game, decision, null, null, out message);

        public static bool Execute(PrototypeGame game, AiDecision decision, Reaction? selectedReaction, HexCoord? evadeDestination, out string message)
        {
            message = "AI has no legal decision.";
            if (game?.Active == null || decision == null) return false;
            FormationState actor = game.Active;
            if (decision.DeclareSynchronizedStrike)
            {
                ContactState declarationContact = game.Contacts.FirstOrDefault(item => item.Owner == actor.Side && !item.IsLost && item.TargetId == decision.TargetId);
                return game.DeclareSynchronizedStrike(actor, decision.ParticipantIds.Select(game.Find), declarationContact, decision.Hex, decision.Salvo, decision.UsePriorPlanning, decision.DeconflictedFormationId, out _, out message);
            }
            if (!string.IsNullOrEmpty(decision.SynchronizedStrikeId))
            {
                SynchronizedStrikeState synchronized = game.SynchronizedStrikes.FirstOrDefault(item => item.Id == decision.SynchronizedStrikeId);
                if (synchronized == null) { message = "The synchronized event is no longer active."; return false; }
                ContactState synchronizedContact = game.Contacts.FirstOrDefault(item => item.Owner == actor.Side && item.TargetId == synchronized.ContactTargetId);
                FormationState synchronizedTarget = synchronizedContact == null || synchronizedContact.IsFalse ? null : game.Find(synchronizedContact.TargetId);
                bool hit = synchronizedTarget != null && !synchronizedTarget.IsDestroyed && synchronizedTarget.Position.Equals(synchronized.Aim);
                Reaction synchronizedReaction = hit ? selectedReaction ?? ChooseReaction(game, actor, synchronizedTarget) : Reaction.None;
                HexCoord? synchronizedEvade = hit && synchronizedReaction == Reaction.Evade ? evadeDestination ?? ChooseEvadeDestination(game, actor, synchronizedTarget) : null;
                return game.ResolveSynchronizedStrike(synchronized, synchronizedReaction, synchronizedEvade, synchronizedContact == null || synchronizedContact.IsLost, out _, out message);
            }
            switch (decision.Action)
            {
                case ActionKind.Move: return game.Move(actor, decision.Hex, decision.MoveMode, out message);
                case ActionKind.Search: return game.SearchArea(actor, decision.Hex, decision.SearchMode, out message);
                case ActionKind.Strike:
                    ContactState contact = game.Contacts.FirstOrDefault(item => item.Owner == actor.Side && !item.IsLost && item.TargetId == decision.TargetId);
                    if (contact == null) { message = "The selected Contact is no longer usable."; return false; }
                    FormationState target = game.Find(decision.TargetId);
                    bool hit = target != null && !target.IsDestroyed && target.Position.Equals(decision.Hex);
                    Reaction reaction = hit ? selectedReaction ?? ChooseReaction(game, actor, target) : Reaction.None;
                    HexCoord? destination = hit && reaction == Reaction.Evade ? evadeDestination ?? ChooseEvadeDestination(game, actor, target) : null;
                    return game.StrikeContact(actor, contact, decision.Hex, decision.Salvo, reaction, destination, out _, out message);
                case ActionKind.Recover: return game.Recover(actor, out message);
                case ActionKind.Patrol: return game.Patrol(actor, decision.Hex, decision.PatrolPosture, null, out message);
                case ActionKind.Support: return game.Support(actor, game.Find(decision.TargetId), decision.SupportKind, out message);
                case ActionKind.Replenish: return game.Replenish(actor, null, out message);
                default: return game.Hold(actor, out message);
            }
        }

        public static bool TryPlayUsefulResponse(PrototypeGame game, out string message)
        {
            message = string.Empty;
            if (game?.Active == null) return false;
            FormationState actor = game.Active;
            HashSet<string> hand = new HashSet<string>(game.ResponseHand(actor.Side).Select(card => card.Id));
            ContactState contact = game.Contacts.Where(item => item.Owner == actor.Side && !item.IsLost).OrderByDescending(item => item.Age).ThenBy(item => item.Location).ThenBy(item => item.TargetId).FirstOrDefault();
            if (hand.Contains("C-01") && actor.Friction) return game.PlayCommandResponse(actor.Side, "C-01", actor, null, null, out message);
            if (hand.Contains("C-19") && actor.Endurance != Endurance.Ready) return game.PlayCommandResponse(actor.Side, "C-19", actor, null, null, out message);
            if (hand.Contains("C-06") && actor.Destruction) return game.PlayCommandResponse(actor.Side, "C-06", actor, null, null, out message);
            if (hand.Contains("C-12") && contact != null && contact.Age > 0) return game.PlayCommandResponse(actor.Side, "C-12", null, contact, null, out message);
            if (hand.Contains("C-23") && contact != null && contact.Location < LocationQuality.High) return game.PlayCommandResponse(actor.Side, "C-23", null, contact, null, out message);
            if (hand.Contains("C-05") && actor.EffectiveSearch >= actor.EffectiveStrike) return game.PlayCommandResponse(actor.Side, "C-05", actor, null, null, out message);
            if (hand.Contains("C-16") && actor.Damage >= DamageState.Heavy) return game.PlayCommandResponse(actor.Side, "C-16", actor, null, null, out message);
            if (hand.Contains("C-18") && actor.Damage >= DamageState.Heavy && !actor.OrderlyWithdrawalReady) return game.PlayCommandResponse(actor.Side, "C-18", actor, null, null, out message);
            if (hand.Contains("C-14") && HexCoord.Distance(actor.Position, game.Area.Objective) > 1) return game.PlayCommandResponse(actor.Side, "C-14", actor, null, null, out message);
            return false;
        }

        public static bool TryPrepareReactionResponse(PrototypeGame game, FormationState defender, out string message)
        {
            message = string.Empty;
            if (game == null || defender == null) return false;
            HashSet<string> hand = new HashSet<string>(game.ResponseHand(defender.Side).Select(card => card.Id));
            if (hand.Contains("C-16") && defender.ReactionDefenseBonus == 0)
                return game.PlayCommandResponse(defender.Side, "C-16", defender, null, null, out message);
            if (hand.Contains("C-18") && defender.Damage >= DamageState.Heavy && !defender.OrderlyWithdrawalReady)
                return game.PlayCommandResponse(defender.Side, "C-18", defender, null, null, out message);
            return false;
        }

        private static AiDecision ChooseObjectiveMove(PrototypeGame game, FormationState actor)
        {
            int currentDistance = HexCoord.Distance(actor.Position, game.Area.Objective);
            if (currentDistance <= 1) return null;
            MoveMode mode = MoveMode.Normal;
            List<HexCoord> candidates = game.LegalMoveDestinations(actor, mode).ToList();
            HexCoord destination = candidates
                .OrderBy(candidate => HexCoord.Distance(candidate, game.Area.Objective))
                .ThenBy(candidate => game.Area.TerrainAt(candidate) == OperationalTerrain.Littoral ? 1 : 0)
                .ThenBy(candidate => candidate.Q)
                .ThenBy(candidate => candidate.R)
                .FirstOrDefault();
            if (candidates.Count == 0 || HexCoord.Distance(destination, game.Area.Objective) >= currentDistance) return null;
            return new AiDecision { Action = ActionKind.Move, Hex = destination, MoveMode = mode, Rationale = "Improve position toward the public operational objective." };
        }

        private static AiDecision ChooseStandingMission(PrototypeGame game, FormationState actor, List<ContactState> contacts)
        {
            switch (actor.Mission)
            {
                case ActionKind.Move: return ChooseObjectiveMove(game, actor);
                case ActionKind.Search:
                    SearchMode searchMode = actor.Kind == FormationKind.Submarine ? SearchMode.Passive : SearchMode.Active;
                    HexCoord center = HexCoord.Distance(actor.Position, actor.MissionObjectiveHex) <= game.SearchRangeFor(actor, searchMode) ? actor.MissionObjectiveHex : actor.Position;
                    return new AiDecision { Action = ActionKind.Search, Hex = center, SearchMode = searchMode, Rationale = "No Command Attention is free; continue the assigned Standing Mission." };
                case ActionKind.Strike:
                    ContactState target = contacts.FirstOrDefault(contact => SelectSalvo(actor, contact).HasValue);
                    return target == null ? null : new AiDecision { Action = ActionKind.Strike, TargetId = target.TargetId, Hex = target.LastKnownPosition, Salvo = SelectSalvo(actor, target).Value, Rationale = "No Command Attention is free; execute the assigned Strike Mission." };
                case ActionKind.Patrol: return new AiDecision { Action = ActionKind.Patrol, Hex = actor.Position, PatrolPosture = PatrolPosture.Balanced, Rationale = "No Command Attention is free; establish the assigned Screen." };
                case ActionKind.Support:
                    FormationState recipient = game.Formations.Where(candidate => candidate.Side == actor.Side && candidate != actor && !candidate.IsDestroyed && HexCoord.Distance(actor.Position, candidate.Position) <= Rules.SupportRange).OrderBy(candidate => candidate.Id).FirstOrDefault();
                    return recipient == null ? null : new AiDecision { Action = ActionKind.Support, TargetId = recipient.Id, SupportKind = SupportKind.Strike, Rationale = "No Command Attention is free; execute the assigned Support Mission." };
                case ActionKind.Recover: return actor.Friction || actor.Disruption ? new AiDecision { Action = ActionKind.Recover, Rationale = "No Command Attention is free; execute the assigned Recover Mission." } : null;
                case ActionKind.Replenish: return game.HasLogisticsAccess(actor) && game.NeedsReplenishment(actor) ? new AiDecision { Action = ActionKind.Replenish, Rationale = "No Command Attention is free; execute the assigned Replenishment Mission." } : null;
                case ActionKind.Hold: return new AiDecision { Action = ActionKind.Hold, Rationale = "No Command Attention is free; maintain the assigned Hold Mission." };
                default: return null;
            }
        }

        private static Salvo? SelectSalvo(FormationState actor, ContactState contact)
        {
            if (contact == null) return null;
            int distance = HexCoord.Distance(actor.Position, contact.LastKnownPosition);
            bool heavyAvailable = contact.Identity == IdentityQuality.Identified && contact.Location == LocationQuality.High && actor.CanHeavySalvo;
            if (heavyAvailable && distance <= Rules.StrikeRange(actor.Kind, Salvo.Heavy)) return Salvo.Heavy;
            if (distance <= Rules.StrikeRange(actor.Kind, Salvo.Standard)) return Salvo.Standard;
            if (distance <= Rules.StrikeRange(actor.Kind, Salvo.Light)) return Salvo.Light;
            return null;
        }
    }
}
