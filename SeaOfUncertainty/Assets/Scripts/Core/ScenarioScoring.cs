using System;
using System.Collections.Generic;
using System.Linq;

namespace SeaOfUncertainty.Core
{
    public enum ScenarioObjectiveKind { Control, Transit, Escort, Denial, Withdrawal }

    [Serializable]
    public sealed class ScenarioObjectiveDefinition
    {
        public string Id;
        public string Title;
        public ScenarioObjectiveKind Kind;
        public int Points;
        public int Radius;
        public string BlueRegionId;
        public string RedRegionId;
    }

    [Serializable]
    public sealed class ScenarioObjectiveResult
    {
        public string Id;
        public string Title;
        public ScenarioObjectiveKind Kind;
        public int Points;
        public bool Achieved;
    }

    [Serializable]
    public sealed class ScenarioSideScore
    {
        public Side Side;
        public int OperationalPoints;
        public int CombatCapableFormations;
        public int DamageBurden;
        public int ClosestObjectiveRange;
        public bool Eliminated;
        public List<ScenarioObjectiveResult> Objectives = new List<ScenarioObjectiveResult>();
    }

    [Serializable]
    public sealed class ScenarioOutcome
    {
        public ScenarioSideScore Blue;
        public ScenarioSideScore Red;
        public bool HasWinner;
        public Side Winner;
        public string DecidingFactor;
        public string Summary;
    }

    public static class ScenarioScoring
    {
        public static ScenarioOutcome Evaluate(PrototypeGame game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            ScenarioSideScore blue = ScoreSide(game, Side.Blue);
            ScenarioSideScore red = ScoreSide(game, Side.Red);
            var outcome = new ScenarioOutcome { Blue = blue, Red = red };
            int comparison;
            if (blue.Eliminated != red.Eliminated)
            {
                comparison = blue.Eliminated ? -1 : 1;
                outcome.DecidingFactor = "operational force eliminated";
            }
            else if (blue.OperationalPoints != red.OperationalPoints)
            {
                comparison = blue.OperationalPoints.CompareTo(red.OperationalPoints);
                outcome.DecidingFactor = "operational objectives";
            }
            else if (blue.CombatCapableFormations != red.CombatCapableFormations)
            {
                comparison = blue.CombatCapableFormations.CompareTo(red.CombatCapableFormations);
                outcome.DecidingFactor = "combat-capable formations";
            }
            else if (blue.DamageBurden != red.DamageBurden)
            {
                comparison = red.DamageBurden.CompareTo(blue.DamageBurden);
                outcome.DecidingFactor = "lower damage burden";
            }
            else if (blue.ClosestObjectiveRange != red.ClosestObjectiveRange)
            {
                comparison = red.ClosestObjectiveRange.CompareTo(blue.ClosestObjectiveRange);
                outcome.DecidingFactor = "closest surviving naval formation";
            }
            else
            {
                comparison = 0;
                outcome.DecidingFactor = "all operational and capability tie-breaks equal";
            }
            outcome.HasWinner = comparison != 0;
            if (comparison != 0) outcome.Winner = comparison > 0 ? Side.Blue : Side.Red;
            outcome.Summary = !outcome.HasWinner ? "OPERATION CONTESTED" : $"{outcome.Winner.ToString().ToUpperInvariant()} OPERATIONAL VICTORY — {outcome.DecidingFactor.ToUpperInvariant()}";
            return outcome;
        }

        public static ScenarioSideScore ScoreSide(PrototypeGame game, Side side)
        {
            List<FormationState> friendly = game.Formations.Where(item => item.Side == side).ToList();
            List<FormationState> capable = friendly.Where(IsCombatCapable).Where(item => item.Kind != FormationKind.LogisticsGroup).ToList();
            List<ScenarioObjectiveResult> objectives = (game.Scenario.Objectives ?? new List<ScenarioObjectiveDefinition>())
                .Select(definition => new ScenarioObjectiveResult
                {
                    Id = definition.Id, Title = definition.Title, Kind = definition.Kind, Points = definition.Points,
                    Achieved = EvaluateObjective(game, side, definition)
                }).ToList();
            return new ScenarioSideScore
            {
                Side = side,
                OperationalPoints = objectives.Where(item => item.Achieved).Sum(item => item.Points),
                CombatCapableFormations = capable.Count,
                DamageBurden = friendly.Sum(item => (int)item.Damage),
                ClosestObjectiveRange = capable.Where(IsOperationalCombatant).Select(item => HexCoord.Distance(item.Position, game.Area.Objective)).DefaultIfEmpty(int.MaxValue).Min(),
                Eliminated = capable.Count == 0,
                Objectives = objectives
            };
        }

        public static bool EvaluateObjective(PrototypeGame game, Side side, ScenarioObjectiveDefinition objective)
        {
            if (game == null || objective == null) return false;
            List<FormationState> friendly = game.Formations.Where(item => item.Side == side && IsCombatCapable(item)).ToList();
            Side enemySide = side == Side.Blue ? Side.Red : Side.Blue;
            switch (objective.Kind)
            {
                case ScenarioObjectiveKind.Control:
                    return game.Controls(side, game.Area.Objective);
                case ScenarioObjectiveKind.Transit:
                    OperationalRegionDefinition transit = FindRegion(game.Scenario, side == Side.Blue ? objective.BlueRegionId : objective.RedRegionId);
                    return transit != null && friendly.Any(item => IsOperationalCombatant(item) && transit.Hexes.Contains(item.Position));
                case ScenarioObjectiveKind.Escort:
                    FormationState carrier = friendly.FirstOrDefault(item => item.Kind == FormationKind.CarrierGroup);
                    return carrier != null && friendly.Any(item => item != carrier && IsOperationalCombatant(item) && HexCoord.Distance(item.Position, carrier.Position) <= Math.Max(1, objective.Radius));
                case ScenarioObjectiveKind.Denial:
                    return !game.Formations.Any(item => item.Side == enemySide && IsCombatCapable(item) && IsOperationalCombatant(item) && HexCoord.Distance(item.Position, game.Area.Objective) <= Math.Max(1, objective.Radius));
                case ScenarioObjectiveKind.Withdrawal:
                    FormationState protectedCarrier = friendly.FirstOrDefault(item => item.Kind == FormationKind.CarrierGroup);
                    if (protectedCarrier == null) return false;
                    if (protectedCarrier.Damage < DamageState.Heavy) return true;
                    OperationalRegionDefinition withdrawal = FindRegion(game.Scenario, side == Side.Blue ? objective.BlueRegionId : objective.RedRegionId);
                    return withdrawal != null && withdrawal.Hexes.Contains(protectedCarrier.Position);
                default: return false;
            }
        }

        private static bool IsCombatCapable(FormationState formation)
            => formation != null && !formation.IsDestroyed && formation.Damage < DamageState.Crippled;

        private static bool IsOperationalCombatant(FormationState formation)
            => formation != null && formation.Kind != FormationKind.AirGroup && formation.Kind != FormationKind.LogisticsGroup;

        private static OperationalRegionDefinition FindRegion(ScenarioDefinition scenario, string id)
        {
            if (scenario == null || string.IsNullOrEmpty(id)) return null;
            IEnumerable<OperationalRegionDefinition> regions = (scenario.Area?.Regions ?? new List<OperationalRegionDefinition>())
                .Concat(scenario.DeploymentRegions ?? new List<OperationalRegionDefinition>())
                .Concat(scenario.ReinforcementRegions ?? new List<OperationalRegionDefinition>())
                .Concat(scenario.ExitRegions ?? new List<OperationalRegionDefinition>())
                .Concat(scenario.LogisticsRegions ?? new List<OperationalRegionDefinition>());
            return regions.FirstOrDefault(region => string.Equals(region.Id, id, StringComparison.OrdinalIgnoreCase));
        }
    }
}
