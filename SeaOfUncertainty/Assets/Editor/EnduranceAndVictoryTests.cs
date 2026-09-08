using System;
using System.Linq;
using SeaOfUncertainty.Core;
using UnityEditor;
using UnityEngine;

namespace SeaOfUncertainty.EditorTools
{
    public static class EnduranceAndVictoryTests
    {
        [MenuItem("Sea of Uncertainty/Tests/All Regression Suites")]
        public static void RunAllRegressionTests()
        {
            SeaOfUncertainty.Editor.PrototypeSceneBuilder.RunCoreSmokeTests();
            SeaOfUncertainty.Editor.PrototypeSceneBuilder.RunEntropyRevealRegressionTests();
            SeaOfUncertainty.Editor.PrototypeSceneBuilder.RunCardExpansionTests();
            SeaOfUncertainty.Editor.PrototypeSceneBuilder.RunCommandMissionTests();
            SeaOfUncertainty.Editor.PrototypeSceneBuilder.RunReplenishmentTests();
            SeaOfUncertainty.Editor.PrototypeSceneBuilder.RunPatrolSupportTests();
            SeaOfUncertainty.Editor.PrototypeSceneBuilder.RunReactionTests();
            SynchronizedStrikeFeatureTests.Run();
            Run();
            Debug.Log("ALL REGRESSION SUITES PASSED");
        }

        [MenuItem("Sea of Uncertainty/Tests/Endurance and Scenario Victory")]
        public static void Run()
        {
            TestEnduranceStatesAndProgress();
            TestEveryObjectiveType();
            TestContestedControlTieEliminationAndPriority();
            Debug.Log("ENDURANCE / SCENARIO VICTORY TESTS PASSED");
        }

        private static void TestEnduranceStatesAndProgress()
        {
            var extendedGame = new PrototypeGame(5501);
            FormationState extended = extendedGame.Active;
            extended.Endurance = Endurance.Extended;
            extended.Mission = ActionKind.Recover;
            extended.Friction = true;
            int start = extendedGame.Time;
            Assert(extendedGame.Recover(extended, EntropySource.Friction, null, out string recovered), recovered);
            Assert(extended.ReadyTime == start + 3, "Extended Recover must cost base 2 plus 1 Time.");

            var criticalGame = new PrototypeGame(5502);
            FormationState critical = criticalGame.Active;
            critical.Endurance = Endurance.Critical;
            critical.Mission = ActionKind.Search;
            Assert(criticalGame.SearchArea(critical, critical.Position, SearchMode.Passive, out string searched), searched);
            Assert(critical.Friction, "A Critical complex Action must mark Friction.");

            var holdGame = new PrototypeGame(5503);
            FormationState holding = holdGame.Active;
            holding.Endurance = Endurance.Critical;
            holding.Mission = ActionKind.Hold;
            Assert(holdGame.Hold(holding, out string held) && !holding.Friction, "Critical Hold remains non-complex and must not mark Friction: " + held);
            Assert(!holding.CanHeavySalvo && Rules.MoveAllowance(holding, MoveMode.Normal) == Math.Max(1, Math.Min(2, holding.Ratings.Move - 1)), "Critical must prohibit Heavy Salvo and reduce Move by one.");

            var progressGame = new PrototypeGame(5504);
            FormationState progress = progressGame.Active;
            progress.MajorActions = 2;
            progress.Mission = ActionKind.Search;
            Assert(progress.EnduranceProgress == "2/3 Major Actions", "Endurance progress text must expose the next degradation threshold.");
            Assert(progressGame.SearchArea(progress, progress.Position, SearchMode.Passive, out string third), third);
            Assert(progress.Endurance == Endurance.Extended && progress.MajorActions == 0 && progress.EnduranceProgress == "0/3 Major Actions", "The third Major Action must degrade and reset visible progress.");
        }

        private static void TestEveryObjectiveType()
        {
            var game = new PrototypeGame(5601, ScenarioCatalog.MeridianVeil());
            Assert(game.Scenario.Objectives.Select(item => item.Kind).Distinct().Count() == 5, "Meridian Veil must define all five objective types.");
            Assert(OperationalDataValidator.Validate(game.Scenario).Count == 0, "Meridian Veil objective data must validate.");
            FormationState blueCarrier = game.Formations.First(item => item.Side == Side.Blue && item.Kind == FormationKind.CarrierGroup);
            FormationState blueSurface = game.Formations.First(item => item.Side == Side.Blue && item.Kind == FormationKind.SurfaceGroup);
            foreach (FormationState formation in game.Formations) { formation.Damage = DamageState.None; formation.Position = formation.Side == Side.Blue ? new HexCoord(3, 3) : new HexCoord(10, 2); }

            ScenarioObjectiveDefinition control = Objective(game, ScenarioObjectiveKind.Control);
            blueSurface.Position = game.Area.Objective;
            Assert(ScenarioScoring.EvaluateObjective(game, Side.Blue, control), "Control objective must recognize sole qualifying control.");

            ScenarioObjectiveDefinition transit = Objective(game, ScenarioObjectiveKind.Transit);
            OperationalRegionDefinition transitRegion = game.Area.Regions.First(region => region.Id == transit.BlueRegionId);
            blueSurface.Position = transitRegion.Hexes.First();
            Assert(ScenarioScoring.EvaluateObjective(game, Side.Blue, transit), "Transit objective must recognize a capable naval formation in its side-specific destination.");

            ScenarioObjectiveDefinition escort = Objective(game, ScenarioObjectiveKind.Escort);
            blueCarrier.Position = new HexCoord(3, 3); blueSurface.Position = blueCarrier.Position;
            Assert(ScenarioScoring.EvaluateObjective(game, Side.Blue, escort), "Escort objective must recognize a capable naval escort within its radius.");

            ScenarioObjectiveDefinition denial = Objective(game, ScenarioObjectiveKind.Denial);
            foreach (FormationState enemy in game.Formations.Where(item => item.Side == Side.Red)) enemy.Position = new HexCoord(10, 2);
            Assert(ScenarioScoring.EvaluateObjective(game, Side.Blue, denial), "Denial objective must recognize an objective area clear of capable enemy naval formations.");

            ScenarioObjectiveDefinition withdrawal = Objective(game, ScenarioObjectiveKind.Withdrawal);
            blueCarrier.Damage = DamageState.Heavy; blueCarrier.Position = new HexCoord(6, 3);
            Assert(!ScenarioScoring.EvaluateObjective(game, Side.Blue, withdrawal), "A heavily damaged carrier outside its withdrawal zone must fail Withdrawal.");
            blueCarrier.Position = game.Area.Regions.First(region => region.Id == withdrawal.BlueRegionId).Hexes.First();
            Assert(ScenarioScoring.EvaluateObjective(game, Side.Blue, withdrawal), "A heavily damaged combat-capable carrier in its withdrawal zone must satisfy Withdrawal.");
            blueCarrier.Damage = DamageState.Destroyed;
            Assert(!ScenarioScoring.EvaluateObjective(game, Side.Blue, withdrawal), "A destroyed carrier cannot satisfy Withdrawal.");
        }

        private static void TestContestedControlTieEliminationAndPriority()
        {
            var contested = new PrototypeGame(5701);
            FormationState blueSurface = contested.Formations.First(item => item.Side == Side.Blue && item.Kind == FormationKind.SurfaceGroup);
            FormationState redSurface = contested.Formations.First(item => item.Side == Side.Red && item.Kind == FormationKind.SurfaceGroup);
            blueSurface.Position = contested.Area.Objective; redSurface.Position = contested.Area.Objective;
            Assert(!contested.Controls(Side.Blue, contested.Area.Objective) && !contested.Controls(Side.Red, contested.Area.Objective), "Opposing capable naval presence must contest control for both sides.");

            var mutual = new PrototypeGame(5702);
            foreach (FormationState formation in mutual.Formations) formation.Damage = DamageState.Destroyed;
            ScenarioOutcome tied = ScenarioScoring.Evaluate(mutual);
            Assert(!tied.HasWinner && tied.Blue.Eliminated && tied.Red.Eliminated, "Mutual elimination with equal objectives and tie-breaks must remain contested.");

            var eliminated = new PrototypeGame(5703);
            foreach (FormationState formation in eliminated.Formations.Where(item => item.Side == Side.Blue)) formation.Damage = DamageState.Destroyed;
            ScenarioOutcome elimination = ScenarioScoring.Evaluate(eliminated);
            Assert(elimination.HasWinner && elimination.Winner == Side.Red && elimination.DecidingFactor.Contains("eliminated"), "One-sided operational elimination must override points.");

            var damageTieBreak = new PrototypeGame(5704);
            foreach (FormationState formation in damageTieBreak.Formations)
            {
                formation.Position = formation.Side == Side.Blue ? new HexCoord(3, 3) : new HexCoord(10, 2);
                formation.Damage = DamageState.None;
            }
            damageTieBreak.Formations.First(item => item.Side == Side.Red && item.Kind == FormationKind.AirGroup).Damage = DamageState.Heavy;
            ScenarioOutcome damage = ScenarioScoring.Evaluate(damageTieBreak);
            Assert(damage.Blue.OperationalPoints == damage.Red.OperationalPoints && damage.HasWinner && damage.Winner == Side.Blue && damage.DecidingFactor == "lower damage burden", "Damage must break an operational tie without awarding VP.");

            var operationalPriority = new PrototypeGame(5705);
            foreach (FormationState formation in operationalPriority.Formations)
            {
                formation.Position = formation.Side == Side.Blue ? new HexCoord(3, 3) : new HexCoord(10, 2);
                formation.Damage = formation.Side == Side.Blue ? DamageState.Heavy : DamageState.None;
            }
            operationalPriority.Formations.First(item => item.Side == Side.Blue && item.Kind == FormationKind.SurfaceGroup).Position = operationalPriority.Area.Objective;
            ScenarioOutcome priority = ScenarioScoring.Evaluate(operationalPriority);
            Assert(priority.Blue.OperationalPoints > priority.Red.OperationalPoints && priority.HasWinner && priority.Winner == Side.Blue && priority.DecidingFactor == "operational objectives", "Operational points must outrank a worse damage burden.");
        }

        private static ScenarioObjectiveDefinition Objective(PrototypeGame game, ScenarioObjectiveKind kind)
            => game.Scenario.Objectives.First(item => item.Kind == kind);

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
