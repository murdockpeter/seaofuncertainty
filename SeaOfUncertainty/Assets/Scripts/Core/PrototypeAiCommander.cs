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
        public string Rationale;

        public string ModeName => Action == ActionKind.Move ? MoveMode.ToString() : Action == ActionKind.Search ? SearchMode.ToString() : Action == ActionKind.Strike ? Salvo.ToString() : "Default";
    }

    public static class PrototypeAiCommander
    {
        public static AiDecision Choose(PrototypeGame game)
        {
            if (game?.Active == null) return null;
            FormationState actor = game.Active;
            List<ContactState> contacts = game.Contacts
                .Where(contact => contact.Owner == actor.Side && !contact.IsLost)
                .OrderByDescending(contact => contact.Location)
                .ThenByDescending(contact => contact.Identity)
                .ThenBy(contact => contact.Age)
                .ThenBy(contact => contact.TargetId, StringComparer.Ordinal)
                .ToList();

            ContactState strikeContact = contacts
                .Where(contact => game.Find(contact.TargetId) != null && SelectSalvo(actor, contact).HasValue)
                .OrderByDescending(contact => contact.Identity)
                .ThenByDescending(contact => contact.Location)
                .ThenBy(contact => contact.Age)
                .ThenBy(contact => HexCoord.Distance(actor.Position, contact.LastKnownPosition))
                .FirstOrDefault();
            if (strikeContact != null)
            {
                Salvo selectedSalvo = SelectSalvo(actor, strikeContact).Value;
                return new AiDecision { Action = ActionKind.Strike, TargetId = strikeContact.TargetId, Salvo = selectedSalvo, Rationale = selectedSalvo == Salvo.Heavy ? "Commit the expendable Heavy capability to a high-quality identified Contact inside extended range." : "Engage the strongest usable owned Contact already inside weapon range." };
            }

            if (actor.Friction || actor.Disruption)
                return new AiDecision { Action = ActionKind.Recover, Rationale = "Clear recoverable Entropy before another complex action." };

            ContactState searchContact = contacts.FirstOrDefault(contact => HexCoord.Distance(actor.Position, contact.LastKnownPosition) <= Rules.SearchRange(actor.Kind == FormationKind.Submarine ? SearchMode.Passive : SearchMode.Active));
            if (searchContact != null && (actor.Kind == FormationKind.Submarine || actor.EffectiveSearch >= 3 || searchContact.Age > 0 || searchContact.Location < LocationQuality.High))
            {
                bool needsFocused = actor.Kind != FormationKind.Submarine && game.Sides[actor.Side].CommandSlots > 0 && (searchContact.Age >= 2 || searchContact.Location == LocationQuality.Low);
                SearchMode mode = actor.Kind == FormationKind.Submarine ? SearchMode.Passive : needsFocused ? SearchMode.Focused : SearchMode.Active;
                return new AiDecision { Action = ActionKind.Search, Hex = searchContact.LastKnownPosition, SearchMode = mode, Rationale = needsFocused ? "Use available Command to improve a stale or low-quality owned Contact." : "Refresh the best available Contact without consulting hidden enemy state." };
            }

            AiDecision movement = ChooseObjectiveMove(game, actor);
            if (movement != null) return movement;

            SearchMode fallbackMode = actor.Kind == FormationKind.Submarine ? SearchMode.Passive : SearchMode.Active;
            HexCoord searchCenter = HexCoord.Distance(actor.Position, game.Area.Objective) <= Rules.SearchRange(fallbackMode) ? game.Area.Objective : actor.Position;
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
            switch (decision.Action)
            {
                case ActionKind.Move: return game.Move(actor, decision.Hex, decision.MoveMode, out message);
                case ActionKind.Search: return game.SearchArea(actor, decision.Hex, decision.SearchMode, out message);
                case ActionKind.Strike:
                    FormationState target = game.Find(decision.TargetId);
                    if (target == null) { message = "The selected Contact no longer supports a target."; return false; }
                    Reaction reaction = selectedReaction ?? ChooseReaction(game, actor, target);
                    HexCoord? destination = reaction == Reaction.Evade ? evadeDestination ?? ChooseEvadeDestination(game, actor, target) : null;
                    return game.Strike(actor, target, decision.Salvo, reaction, destination, out _, out message);
                case ActionKind.Recover: return game.Recover(actor, out message);
                default: return game.Hold(actor, out message);
            }
        }

        private static AiDecision ChooseObjectiveMove(PrototypeGame game, FormationState actor)
        {
            int currentDistance = HexCoord.Distance(actor.Position, game.Area.Objective);
            if (currentDistance <= 1) return null;
            MoveMode mode = MoveMode.Normal;
            int allowance = Rules.MoveAllowance(actor, mode);
            var candidates = new List<HexCoord>();
            for (int q = 0; q < game.Area.Width; q++)
            {
                for (int r = 0; r < game.Area.Height; r++)
                {
                    var candidate = new HexCoord(q, r);
                    int movement = HexCoord.Distance(actor.Position, candidate);
                    if (movement < 1 || movement > allowance) continue;
                    OperationalTerrain terrain = game.Area.TerrainAt(candidate);
                    if (terrain == OperationalTerrain.Land && actor.Kind != FormationKind.AirGroup) continue;
                    if (game.Area.RestrictedAreas != null && game.Area.RestrictedAreas.Any(region => region.Hexes.Contains(candidate))) continue;
                    candidates.Add(candidate);
                }
            }
            HexCoord destination = candidates
                .OrderBy(candidate => HexCoord.Distance(candidate, game.Area.Objective))
                .ThenBy(candidate => game.Area.TerrainAt(candidate) == OperationalTerrain.Littoral ? 1 : 0)
                .ThenBy(candidate => candidate.Q)
                .ThenBy(candidate => candidate.R)
                .FirstOrDefault();
            if (candidates.Count == 0 || HexCoord.Distance(destination, game.Area.Objective) >= currentDistance) return null;
            return new AiDecision { Action = ActionKind.Move, Hex = destination, MoveMode = mode, Rationale = "Improve position toward the public operational objective." };
        }

        private static Salvo? SelectSalvo(FormationState actor, ContactState contact)
        {
            int distance = HexCoord.Distance(actor.Position, contact.LastKnownPosition);
            bool heavyAvailable = contact.Identity == IdentityQuality.Identified && contact.Location == LocationQuality.High && actor.CanHeavySalvo;
            if (heavyAvailable && distance <= Rules.StrikeRange(actor.Kind, Salvo.Heavy)) return Salvo.Heavy;
            if (distance <= Rules.StrikeRange(actor.Kind, Salvo.Standard)) return Salvo.Standard;
            if (distance <= Rules.StrikeRange(actor.Kind, Salvo.Light)) return Salvo.Light;
            return null;
        }
    }
}
