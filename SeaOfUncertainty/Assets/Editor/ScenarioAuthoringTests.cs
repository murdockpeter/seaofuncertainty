#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using SeaOfUncertainty.Core;
using UnityEngine;

namespace SeaOfUncertainty.Editor
{
    public static class ScenarioAuthoringTests
    {
        public static void Run()
        {
            TestShippedBallisticOperations();
            AuthoredScenarioDefinition entry = Fixture();
            var catalog = new AuthoredScenarioCatalog { Scenarios = new List<AuthoredScenarioDefinition> { entry } };
            // Exercise the shipped serializer, including omitted fields, rather than only object initializers.
            catalog = JsonUtility.FromJson<AuthoredScenarioCatalog>(JsonUtility.ToJson(catalog));
            IReadOnlyList<ScenarioDefinition> resolved = Resolve(catalog);
            ScenarioDefinition operation = resolved.Single(item => item.Id == entry.Id);
            Check(resolved.Count == 3 && operation.Area.Id == "luzon-strait-northern-approaches", "A previously unknown authored ID joins the selectable catalog using a known theater");
            Check(operation.Formations.Count == 2 && operation.Objectives.Single().Id == "custom-denial" && operation.ScheduledEvents.Count == 2,
                "New operation uses its own forces, objectives and events");
            Check(operation.Contacts.Count == 0 && operation.DeploymentRegions.Count == 0 && operation.LogisticsRegions.Count == 0 && operation.ExitRegions.Count == 0,
                "Empty authored lists clear operation data without inheriting template state");
            Check(operation.Area.Regions.Count == 0 && operation.Area.RestrictedAreas.Count > 0, "Replace true plus [] clears area regions; omitted restriction override inherits geography");
            var game = new PrototypeGame(20261015, operation);
            Check(game.Scenario.Id == entry.Id && game.Formations.Select(item => item.Id).SequenceEqual(new[] { "CUSTOM-B", "CUSTOM-R" }), "New operation starts under its own stable ID");
            Check(ScenarioScoring.ScoreSide(game, Side.Blue).Objectives.Single().Points == 5, "Authored scoring points drive runtime results");
            Check(game.SearchRangeFor(game.Active, SearchMode.Passive) == 2 && game.StrikeRangeFor(game.Active, Salvo.Light) == 1,
                "Authored sensor and weapon profiles drive runtime envelopes");
            Check(game.Sides[Side.Blue].Architecture == CommandArchitecture.Centralized && game.RuntimeWeatherSeverity == 1, "Authored command architecture and weather drive initial state");
            FormationState target = game.Formations.Single(item => item.Side != game.Active.Side);
            HexCoord farAim = game.LegalMoveDestinations(game.Active, MoveMode.HighTempo).First(hex => HexCoord.Distance(game.Active.Position, hex) > 1);
            target.Position = farAim;
            game.Contacts.Add(new ContactState { Owner = game.Active.Side, TargetId = target.Id, LastKnownPosition = farAim, Location = LocationQuality.High, Identity = IdentityQuality.Identified });
            int ammunition = game.Active.Weapons.Light;
            Check(!game.Strike(game.Active, target, Salvo.Light, Reaction.Hold, out _, out _) && game.Active.Weapons.Light == ammunition,
                "Authored range boundary rejects an otherwise conventional legal strike without spending ammunition");
            Check(!game.EligibleSynchronizedStrikeParticipants(game.Active.Side, game.Contacts.Last(), farAim, Salvo.Light).Any(), "Synchronized eligibility respects authored weapon range");
            game.Contacts.Last().LastKnownPosition = new HexCoord(game.Active.Position.Q, game.Active.Position.R + 4);
            Check(PrototypeAiCommander.Choose(game, AiStrategyProfile.Aggressive).Action != ActionKind.Strike, "AI does not select a conventional strike beyond the authored maximum range");
            game.Contacts.Last().LastKnownPosition = farAim;

            AdvanceAcross(game, 2);
            Check(game.RuntimeWeather == "Clear" && game.ResolvedScheduledEventIds.Contains("custom-weather"), "Authored weather event executes");
            AdvanceAcross(game, 3);
            Check(game.Find("CUSTOM-REINFORCEMENT") != null && game.ResolvedScheduledEventIds.Contains("custom-arrival"), "Authored reinforcement enters through its own region");
            var restored = new PrototypeGame(20261015, Resolve(catalog).Single(item => item.Id == entry.Id));
            restored.RestoreState(JsonUtility.FromJson<PrototypeGame.SaveData>(JsonUtility.ToJson(game.CaptureState())));
            Check(restored.Scenario.Id == entry.Id && restored.Find("CUSTOM-REINFORCEMENT") != null && restored.RuntimeWeather == game.RuntimeWeather, "New operation state survives save/load");

            ScenarioDefinition independent = Resolve(catalog).Single(item => item.Id == entry.Id);
            operation.Area.RestrictedAreas[0].Hexes.Clear();
            operation.SensorRanges[0].Passive = 99;
            game.Formations[0].Ratings.Move = 99;
            Check(independent.Area.RestrictedAreas[0].Hexes.Count > 0 && independent.SensorRanges[0].Passive != 99 && independent.Formations[0].Ratings.Move != 99,
                "Resolved operations own independent geography, profiles and formation ratings");
            Check(catalog.Scenarios[0].ScheduledEvents[1].Reinforcement.Q == 0 && catalog.Scenarios[0].Formations[0].Ratings.Move != 99,
                "Running a scenario never changes authored reinforcement or rating data");

            AuthoredScenarioDefinition inherited = Fixture();
            inherited.ReplaceSensorRanges = inherited.ReplaceWeaponRanges = inherited.ReplaceMissileDefenseProfiles = false;
            inherited.SensorRanges.Clear(); inherited.WeaponRanges.Clear(); inherited.MissileDefenseProfiles.Clear();
            string omittedJson = JsonUtility.ToJson(inherited).Replace("\"Contacts\":[],", "").Replace("\"DeploymentRegions\":[],", "");
            ScenarioDefinition inheritedOperation = Resolve(new AuthoredScenarioCatalog { Scenarios = new List<AuthoredScenarioDefinition> { JsonUtility.FromJson<AuthoredScenarioDefinition>(omittedJson) } }).Single(item => item.Id == inherited.Id);
            Check(inheritedOperation.Contacts.Count == 0 && inheritedOperation.DeploymentRegions.Count == 0 && inheritedOperation.WeaponRanges.Single(item => item.Kind == FormationKind.SurfaceGroup).Light == 3,
                "Omitted operation lists stay empty while disabled profile replacements inherit baseline values");
            AuthoredScenarioDefinition blocked = Fixture();
            blocked.ScheduledEvents[1].Time = 0;
            blocked.ReinforcementRegions[0].Hexes[0] = new HexCoord(blocked.Formations[0].Q, blocked.Formations[0].R);
            var blockedGame = new PrototypeGame(20261015, Resolve(new AuthoredScenarioCatalog { Scenarios = new List<AuthoredScenarioDefinition> { blocked } }).Single(item => item.Id == blocked.Id));
            Check(blockedGame.Active != null && blockedGame.Find("CUSTOM-REINFORCEMENT") == null, "A blocked time-zero reinforcement does not freeze the Ready-Time loop");
            HexCoord departure = blockedGame.LegalMoveDestinations(blockedGame.Active, MoveMode.Normal).First();
            Check(blockedGame.Move(blockedGame.Active, departure, MoveMode.Normal, out _) && blockedGame.Find("CUSTOM-REINFORCEMENT") != null,
                "A deferred reinforcement retries when its friendly blocker leaves");
            AuthoredScenarioDefinition portOperation = ScenarioAuthoringCatalog.FromScenario(ScenarioCatalog.MeridianVeil());
            portOperation.Formations.First(item => item.Kind == FormationKind.SurfaceGroup).Q = 1;
            portOperation.Formations.First(item => item.Kind == FormationKind.SurfaceGroup).R = 2;
            Check(ScenarioAuthoringCatalog.Validate(new AuthoredScenarioCatalog { Scenarios = new List<AuthoredScenarioDefinition> { portOperation } }).Count == 0,
                "A naval formation can deploy at a land hex containing a legal port");

            Invalid(item => item.TheaterTemplateId = "typo", "Unknown TheaterTemplateId");
            Invalid(item => item.Formations[0].Side = (Side)99, "valid side");
            Invalid(item => item.Formations[0].Weapons.Light = -1, "inventories");
            Invalid(item => item.Formations[0].Weapons.Light = 99, "capacity");
            Invalid(item => { item.Formations[0].Q = -1; }, "deployment");
            HexCoord land = ScenarioCatalog.LuzonStrait().Area.Terrain.First(item => item.Terrain == OperationalTerrain.Land).Hex;
            Invalid(item => { item.Formations[0].Q = land.Q; item.Formations[0].R = land.R; }, "terrain");
            Invalid(item => { item.Formations[0].Q = 6; item.Formations[0].R = 6; }, "restricted");
            Invalid(item => item.Formations[1].Id = item.Formations[0].Id.ToLowerInvariant(), "duplicated");
            Invalid(item => item.Formations = null, "deployed combat");
            Invalid(item => item.Contacts.Add(new ContactDefinition { TargetId = "missing" }), "target does not exist");
            Invalid(item => item.Contacts.Add(new ContactDefinition { Owner = Side.Blue, TargetId = "CUSTOM-B", Q = 3, R = 7 }), "opposing");
            Invalid(item => item.Objectives[0].Kind = (ScenarioObjectiveKind)99, "objective kind");
            Invalid(item => item.Objectives[0].Kind = ScenarioObjectiveKind.Escort, "requires a deployed carrier");
            Invalid(item => item.Objectives[0].Kind = ScenarioObjectiveKind.Transit, "missing Blue/Red regions");
            Invalid(item => item.Objectives.Clear(), "scored scenario objective");
            Invalid(item => item.WeaponRanges.Clear(), "weapon-range profile");
            Invalid(item => item.SensorRanges[0].Passive = -1, "Sensor ranges");
            Invalid(item => item.Presentation.Palette = (ColorProfile)99, "palette");
            Invalid(item => item.ScheduledEvents[1].Reinforcement.Id = "CUSTOM-B", "Reinforcement formation ID");
            Invalid(item => item.ScheduledEvents[1].RegionId = "missing", "entry region");
            Invalid(item => item.ScheduledEvents[1].Side = Side.Red, "must match");
            Invalid(item => item.ScheduledEvents[0].Id = "custom-arrival", "event ID");
            Invalid(item => item.ScheduledEvents[0].Time = 999, "in-horizon");
            Invalid(item => item.AreaRegions.Add(new OperationalRegionDefinition { Id = "custom-entry", Hexes = new List<HexCoord> { new HexCoord(0, 8) } }), "conflicting hexes");
            Invalid(item => item.ExitRegions.Add(new OperationalRegionDefinition { Id = "land-exit", Hexes = new List<HexCoord> { land } }), "cannot reach");
            var duplicateCatalog = new AuthoredScenarioCatalog { Scenarios = new List<AuthoredScenarioDefinition> { Fixture(), Fixture() } };
            Check(string.Join(" ", ScenarioAuthoringCatalog.Validate(duplicateCatalog)).Contains("Scenario ID is missing or duplicated"), "Duplicate scenario IDs fail authoring");
            duplicateCatalog.Version = 999;
            Check(string.Join(" ", ScenarioAuthoringCatalog.Validate(duplicateCatalog)).Contains("Unsupported"), "Future catalog versions fail explicitly");

            AuthoredScenarioCatalog legacy = JsonUtility.FromJson<AuthoredScenarioCatalog>(Resources.Load<TextAsset>(ScenarioAuthoringCatalog.ResourcePath).text);
            IReadOnlyList<ScenarioDefinition> before = Resolve(legacy);
            AuthoredScenarioCatalog upgraded = ScenarioAuthoringCatalog.Upgrade(legacy);
            IReadOnlyList<ScenarioDefinition> after = Resolve(upgraded);
            foreach (ScenarioDefinition original in before)
                Check(JsonUtility.ToJson(original) == JsonUtility.ToJson(after.Single(item => item.Id == original.Id)), "Catalog migration preserves original scenario behavior: " + original.Id);
            var unknownLegacy = new AuthoredScenarioCatalog { Version = 1, Scenarios = new List<AuthoredScenarioDefinition> { Fixture() } };
            Check(string.Join(" ", ScenarioAuthoringCatalog.Validate(unknownLegacy)).Contains("Version 1 only"), "Legacy authoring reports unknown IDs instead of silently dropping them");
            TestChecks.ThrowIfAny("scenario authoring");
            Debug.Log("Sea of Uncertainty scenario authoring tests passed.");
        }

        private static void TestShippedBallisticOperations()
        {
            foreach (string id in new[] { "meridian-veil", "northern-gateway" })
            {
                ScenarioDefinition scenario = ScenarioCatalog.Require(id);
                Check(OperationalDataValidator.Validate(scenario).Count == 0, "Shipped ballistic operation validates: " + id);
                Check(scenario.Formations.Count(item => item.Kind == FormationKind.BallisticMissileBattery) == 1,
                    "Shipped operation includes one land-deployed Red battery: " + id);
                foreach (FormationDefinition definition in scenario.Formations.Where(item => item.BallisticWeapons.Capacity > 0))
                {
                    var game = new PrototypeGame(1978, scenario);
                    FormationState actor = game.Find(definition.Id);
                    actor.ReadyTime = game.Time;
                    PrototypeGame.SaveData state = game.CaptureState();
                    state.ActiveFormationId = actor.Id;
                    game.RestoreState(state);
                    actor = game.Active;
                    FormationState target = game.Formations.Single(item => item.Side != actor.Side && item.Kind == FormationKind.CarrierGroup);
                    ContactState contact = game.ContactFor(actor.Side, target.Id);
                    FormationState escort = game.FriendlyBallisticCoverage(target.Side, target).FirstOrDefault();
                    Check(contact != null && game.CanStrikeContact(actor, contact, contact.LastKnownPosition, Salvo.Standard, WeaponDelivery.Ballistic, out _),
                        "Authored opening intelligence permits a ballistic shot without moving hidden targets: " + id + "/" + actor.Id);
                    Check(escort != null && escort.Kind == FormationKind.SurfaceGroup && escort.BallisticDefense.InterceptionRating == 4,
                        "Opening carrier position is inside the stronger friendly escort coverage: " + id + "/" + actor.Id);
                    int stocks = game.Formations.Sum(item => item.BallisticDefense.Interceptors);
                    bool launched = game.StrikeContact(actor, contact, contact.LastKnownPosition, Salvo.Standard, Reaction.Hold, null,
                        out CombatResult result, out string message, WeaponDelivery.Ballistic);
                    Check(launched && actor.BallisticWeapons.Ammunition == 1 && result.BallisticInterceptionModifier == 4
                        && game.Formations.Sum(item => item.BallisticDefense.Interceptors) == stocks - 1,
                        "Shipped loadout spends exactly one launch and one escort interception package: " + id + "/" + actor.Id + " " + message);
                    var restored = new PrototypeGame(1978, scenario);
                    restored.RestoreState(JsonUtility.FromJson<PrototypeGame.SaveData>(JsonUtility.ToJson(game.CaptureState())));
                    Check(AuthoritativeReplay.StateDigest(game) == AuthoritativeReplay.StateDigest(restored), "Shipped ballistic engagement saves depleted stocks: " + id + "/" + actor.Id);
                }
                var reloadGame = new PrototypeGame(1978, scenario);
                FormationState battery = reloadGame.Formations.Single(item => item.Kind == FormationKind.BallisticMissileBattery);
                Check(reloadGame.HasBallisticReloadAccess(battery) && reloadGame.LegalMoveDestinations(battery, MoveMode.Normal).All(hex => reloadGame.Area.TerrainAt(hex) == OperationalTerrain.Land),
                    "Authored battery has an airfield reload service and land-only movement: " + id);
                FormationState provider = reloadGame.Formations.First(item => item.BallisticDefense.Capacity > 0);
                provider.Position = scenario.LogisticsRegions.SelectMany(region => region.Hexes)
                    .First(hex => reloadGame.Area.Locations.Any(location => location.Hex.Equals(hex) && location.Kind == LocationKind.Port));
                Check(reloadGame.HasInterceptorReloadAccess(provider), "Authored port supports interceptor reloads: " + id);

                ScenarioDefinition baseline = id == "meridian-veil" ? ScenarioCatalog.MeridianVeil() : ScenarioCatalog.LuzonStrait();
                Check(scenario.Objectives.Select(item => JsonUtility.ToJson(item)).SequenceEqual(baseline.Objectives.Select(item => JsonUtility.ToJson(item)))
                    && scenario.ScheduledEvents.Select(item => JsonUtility.ToJson(item)).SequenceEqual(baseline.ScheduledEvents.Select(item => JsonUtility.ToJson(item))) && scenario.Horizon == baseline.Horizon,
                    "Ballistic playtest keeps executable objectives, scheduled events and duration: " + id);
                var oldGame = new PrototypeGame(1978, baseline);
                PrototypeGame.SaveData oldSave = oldGame.CaptureState();
                oldSave.Version = 11;
                reloadGame.RestoreState(oldSave);
                Check(reloadGame.Formations.Count == oldGame.Formations.Count && reloadGame.Formations.All(item => item.BallisticWeapons.Capacity == 0 && item.BallisticDefense.Capacity == 0),
                    "Loading an old operation retains its forces and zero ballistic equipment despite the new catalog: " + id);
                foreach (int seed in new[] { 1978, 4200 })
                {
                    var simulation = new PrototypeGame(seed, scenario);
                    int launches = 0, interceptions = 0, actions = 0;
                    simulation.CommandEvent += (formation, kind, detail) => { if (kind == "BallisticInterception") interceptions++; };
                    while (simulation.Time < scenario.Horizon && actions++ < 600)
                    {
                        AiDecision decision = PrototypeAiCommander.Choose(simulation);
                        bool legal = decision != null && PrototypeAiCommander.Execute(simulation, decision, out _);
                        Check(legal, "Shipped ballistic AI decision is legal: " + id + "/" + seed + "/" + actions);
                        if (!legal) break;
                        if (decision.Action == ActionKind.Strike && decision.WeaponDelivery == WeaponDelivery.Ballistic) launches++;
                    }
                    Check(simulation.Time >= scenario.Horizon && actions < 600 && launches > 0 && interceptions > 0,
                        "Seeded authored operation reaches its horizon with ballistic launches and interceptions: " + id + "/" + seed);
                    Debug.Log($"Ballistic scenario simulation {id}, seed {seed}: {actions} actions, {launches} launches, {interceptions} interceptions, T{simulation.Time}.");
                }
            }
        }

        private static AuthoredScenarioDefinition Fixture()
        {
            AuthoredScenarioDefinition entry = ScenarioAuthoringCatalog.FromScenario(ScenarioCatalog.LuzonStrait());
            entry.Id = "third-authored-operation";
            entry.DisplayName = "Authoring Boundary Fixture";
            entry.Formations = entry.Formations.Where(item => item.Kind == FormationKind.SurfaceGroup).ToList();
            entry.Formations[0].Id = "CUSTOM-B";
            entry.Formations[1].Id = "CUSTOM-R";
            entry.Contacts.Clear();
            entry.AreaRegions.Clear();
            entry.RestrictedAreas.Clear();
            entry.ReplaceRestrictedAreas = false;
            entry.DeploymentRegions.Clear();
            entry.ReinforcementRegions = new List<OperationalRegionDefinition> { new OperationalRegionDefinition { Id = "custom-entry", Hexes = new List<HexCoord> { new HexCoord(0, 7) } } };
            entry.ExitRegions.Clear();
            entry.LogisticsRegions.Clear();
            entry.Objectives = new List<ScenarioObjectiveDefinition> { new ScenarioObjectiveDefinition { Id = "custom-denial", Title = "Deny the center", Kind = ScenarioObjectiveKind.Denial, Points = 5, Radius = 3 } };
            entry.BlueCommandArchitecture = CommandArchitecture.Centralized;
            foreach (SensorRangeDefinition range in entry.SensorRanges) { range.Passive = 2; range.Active = 3; range.Focused = 4; }
            foreach (WeaponRangeDefinition range in entry.WeaponRanges.Where(item => item.Kind != FormationKind.BallisticMissileBattery)) { range.Light = 1; range.Standard = 2; range.Heavy = 3; }
            entry.ScheduledEvents = new List<ScheduledScenarioEventDefinition>
            {
                new ScheduledScenarioEventDefinition { Id = "custom-weather", Time = 2, Kind = ScheduledEventKind.WeatherChange, Weather = "Clear" },
                new ScheduledScenarioEventDefinition { Id = "custom-arrival", Time = 3, Side = Side.Blue, Kind = ScheduledEventKind.Reinforcement, RegionId = "custom-entry", Reinforcement = new FormationDefinition
                    { Id = "CUSTOM-REINFORCEMENT", Name = "Custom entry", Side = Side.Blue, Kind = FormationKind.SurfaceGroup, Q = 0, R = 7, Ratings = new Ratings { Move = 2 }, Weapons = new WeaponInventoryState() } }
            };
            return entry;
        }

        private static IReadOnlyList<ScenarioDefinition> Resolve(AuthoredScenarioCatalog catalog)
            => ScenarioAuthoringCatalog.Apply(new[] { ScenarioCatalog.MeridianVeil(), ScenarioCatalog.LuzonStrait() }, catalog);

        private static void AdvanceAcross(PrototypeGame game, int time)
        {
            PrototypeGame.SaveData state = game.CaptureState();
            state.Time = time - 1;
            foreach (FormationState formation in state.Formations) formation.ReadyTime = time;
            state.ActiveFormationId = state.Formations[0].Id;
            state.Formations[0].ReadyTime = time - 1;
            game.RestoreState(state);
            Check(game.Hold(game.Active, out _), "Authoring fixture advances the event timeline");
        }

        private static void Invalid(Action<AuthoredScenarioDefinition> change, string expected)
        {
            AuthoredScenarioDefinition entry = Fixture();
            change(entry);
            string errors = string.Join(" ", ScenarioAuthoringCatalog.Validate(new AuthoredScenarioCatalog { Scenarios = new List<AuthoredScenarioDefinition> { entry } }));
            Check(errors.Contains(expected), "Invalid authoring reports " + expected + ": " + errors);
        }

        private static void Check(bool condition, string label) => TestChecks.Check(condition, label);
    }
}
#endif
