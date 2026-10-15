#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using SeaOfUncertainty.Core;
using SeaOfUncertainty.Prototype;
using UnityEngine;

namespace SeaOfUncertainty.Editor
{
    public static class BallisticLaunchTests
    {
        public static void RunUnit()
        {
            Check((int)FormationKind.LogisticsGroup == 4 && (int)FormationKind.BallisticMissileBattery == 5 && (int)ContactDomain.Air == 3, "New enums append without changing legacy numeric meanings");
            foreach (string scenarioId in new[] { "meridian-veil", "northern-gateway" })
            {
                var original = new PrototypeGame(1978, scenarioId == "meridian-veil" ? ScenarioCatalog.MeridianVeil() : ScenarioCatalog.LuzonStrait());
                Check(original.Formations.All(item => item.BallisticWeapons.Capacity == 0), "Legacy scenario gains no ballistic capability: " + scenarioId);
            }
            foreach (string caseName in new[] { "hit", "empty", "false", "stale", "wrong-domain", "deep" })
            {
                PrototypeGame game = Fixture();
                FormationState attacker = game.Active;
                ContactState contact = Report(game);
                FormationState target = game.Find(contact.TargetId);
                HexCoord aim = contact.LastKnownPosition;
                if (caseName == "hit") { attacker.BallisticWeapons.Range = 2; contact.Location = LocationQuality.Medium; }
                if (caseName == "empty" || caseName == "stale") target.Position = new HexCoord(3, 0);
                if (caseName == "stale") contact.Age = 2;
                if (caseName == "false") contact.IsFalse = true;
                if (caseName == "wrong-domain") target.Kind = FormationKind.AirGroup;
                if (caseName == "deep") { target.Kind = FormationKind.Submarine; contact.Domain = ContactDomain.Subsurface; attacker.BallisticWeapons.TargetDomains.Add(ContactDomain.Subsurface); attacker.BallisticWeapons.AllowShallowSubmarines = true; }
                int conventional = attacker.Weapons.Standard;
                Check(game.CanStrikeContact(attacker, contact, aim, Salvo.Standard, WeaponDelivery.Ballistic, out _), "Report-only preview does not inspect hidden occupancy/domain/depth: " + caseName);
                Check(game.StrikeContact(attacker, contact, aim, Salvo.Standard, Reaction.None, null, out CombatResult result, out string message, WeaponDelivery.Ballistic), "Ballistic commitment resolves: " + caseName + " " + message);
                Check(attacker.BallisticWeapons.Ammunition == 2 && attacker.Weapons.Standard == conventional && !attacker.WeaponExpended, "Exactly one ballistic package is spent without touching conventional magazines: " + caseName);
                Check(attacker.Loud && game.ContactFor(Side.Red, attacker.Id) == null, "Launch raises signature without creating an exact-position Contact: " + caseName);
                if (caseName != "hit") Check(message.Contains("no confirmed effect") && result.Roll == 0, "Ineligible or absent actual target reports only no confirmed effect: " + caseName);
                else Check(result.MissileDefenseModifier == 0 && !game.VisibleLog(Side.Red).Any(line => line.Contains(attacker.Name) || line.Contains(attacker.Position.ToString())), "Ballistic hit bypasses conventional missile defense and does not reveal launcher in defender logs");
            }
            TestRejections();
            TestTerrainAndLogistics();
            TestChecks.ThrowIfAny("Ballistic launch");
            Debug.Log("Sea of Uncertainty ballistic launch tests passed.");
        }

        public static void RunIntegration()
        {
            TestPersistenceAndAuthority();
            TestAuthoring();
            TestChecks.ThrowIfAny("Ballistic integration");
            Debug.Log("Sea of Uncertainty ballistic integration tests passed.");
        }

        private static void TestRejections()
        {
            foreach (string reason in new[] { "owner", "lost", "foreign-report", "range", "quality", "domain", "empty-magazine", "crippled", "destroyed", "replenishing", "launcher", "magazine", "critical-heavy", "authorization" })
            {
                PrototypeGame game = Fixture();
                FormationState actor = game.Active;
                ContactState report = Report(game);
                Salvo salvo = Salvo.Standard;
                switch (reason)
                {
                    case "owner": report.Owner = Side.Red; break;
                    case "lost": report.IsLost = true; break;
                    case "foreign-report": game.Contacts.Remove(report); break;
                    case "range": actor.BallisticWeapons.Range = 1; break;
                    case "quality": report.Location = LocationQuality.Low; break;
                    case "domain": report.Domain = ContactDomain.Unknown; break;
                    case "empty-magazine": actor.BallisticWeapons.Ammunition = 0; break;
                    case "crippled": actor.Damage = DamageState.Crippled; break;
                    case "destroyed": actor.Damage = DamageState.Destroyed; break;
                    case "replenishing": actor.Replenishing = true; break;
                    case "launcher": actor.ActiveEffectCardIds.Add(EntropyCardIds.LauncherDamage); break;
                    case "magazine": actor.ActiveEffectCardIds.Add(EntropyCardIds.MagazineDamage); break;
                    case "critical-heavy": actor.Endurance = Endurance.Critical; salvo = Salvo.Heavy; break;
                    case "authorization": actor.Friction = actor.Disruption = actor.Destruction = true; break;
                }
                string before = AuthoritativeReplay.StateDigest(game);
                Check(!game.StrikeContact(actor, report, report.LastKnownPosition, salvo, Reaction.None, null, out _, out _, WeaponDelivery.Ballistic), "Illegal ballistic launch rejected: " + reason);
                Check(before == AuthoritativeReplay.StateDigest(game), "Rejection spends no ammo, time, support, or Command: " + reason);
            }
        }

        private static void TestTerrainAndLogistics()
        {
            PrototypeGame game = Fixture();
            FormationState battery = game.Active;
            Check(game.TryGetMovePath(battery, new HexCoord(0, 1), MoveMode.Normal, out _, out string routeMessage), "Battery routes stay on contiguous Land: " + routeMessage);
            Check(!game.IsLegalMoveDestination(battery, new HexCoord(1, 0), MoveMode.Normal), "Battery cannot enter water");
            Check(!game.HasControlPresence(Side.Blue, battery.Position), "A land battery does not provide naval objective control");
            Check(!battery.CanFire(Salvo.Light) && !game.EligibleSynchronizedStrikeParticipants(Side.Blue, Report(game), new HexCoord(2, 0)).Contains(battery), "Battery cannot silently use conventional fire or synchronized conventional salvos");
            battery.BallisticWeapons.Ammunition = 0;
            game.Scenario.Area.Locations.Add(new OperationalLocationDefinition { Id = "battery-depot", Name = "Battery depot", Kind = LocationKind.Airfield, Q = 0, R = 0 });
            var service = new OperationalRegionDefinition { Id = "battery-service", Name = "Battery service", Hexes = new List<HexCoord> { battery.Position } };
            game.Scenario.LogisticsRegions.Add(service);
            Check(game.HasLogisticsAccess(battery) && !game.HasBallisticReloadAccess(battery), "A land airfield provides general service but no implicit ballistic reload");
            service.SupportsBallisticReload = true;
            Check(game.Replenish(battery, null, out _) && battery.BallisticWeapons.Ammunition == 3, "Explicit compatible logistics restores the ballistic magazine");
            Check(game.Formations.Where(item => item != battery).All(item => item.BallisticWeapons.Capacity == 0), "Per-formation capabilities do not spread to other formations");
        }

        private static void TestPersistenceAndAuthority()
        {
            PrototypeGame game = Fixture();
            var server = new AuthoritativeMatchServer("ballistic-privacy", 1978, game.Scenario);
            Check(server.SnapshotFor(Side.Blue).Version == 3 && server.SnapshotFor(Side.Blue).Formations.Any(item => item.BallisticWeapons.Capacity == 3)
                && !server.SnapshotFor(Side.Red).Formations.Any(item => item.Id == "BLUE-BATTERY"), "Snapshot v3 includes owned capabilities without exposing the enemy launcher or its magazine");
            var command = new AuthoritativeCommand { Side = Side.Blue, ActorId = game.Active.Id, Action = ActionKind.Strike, TargetId = Report(game).TargetId, Q = 2, R = 0, Salvo = Salvo.Standard, WeaponDelivery = WeaponDelivery.Ballistic };
            PrototypeGame twin = Fixture();
            Check(AuthoritativeCommandProcessor.Execute(twin, JsonUtility.FromJson<AuthoritativeCommand>(JsonUtility.ToJson(command)), out _), "Serialized authority command retains weapon selection");
            Check(AuthoritativeCommandProcessor.Execute(game, command, out _), "Current authority command preserves ballistic weapon selection");
            Check(AuthoritativeReplay.StateDigest(game) == AuthoritativeReplay.StateDigest(twin), "Seeded replay of a serialized ballistic command produces the same digest");
            PrototypeGame.SaveData saved = JsonUtility.FromJson<PrototypeGame.SaveData>(JsonUtility.ToJson(game.CaptureState()));
            var restored = new PrototypeGame(1978, game.Scenario);
            restored.RestoreState(saved);
            Check(AuthoritativeReplay.StateDigest(game) == AuthoritativeReplay.StateDigest(restored) && restored.Find("BLUE-BATTERY").BallisticWeapons.Ammunition == 2, "Current save round trip preserves capability, spent ammunition and launch exposure");
            saved.Version = 11;
            restored.RestoreState(saved);
            Check(restored.Formations.All(item => item.BallisticWeapons.Capacity == 0), "Legacy v11 migration strips ballistic capability even when fields are present");
            game = Fixture(); command.Version = 1;
            Check(!AuthoritativeCommandProcessor.Execute(game, command, out _) && game.Active.BallisticWeapons.Ammunition == 3, "Legacy commands cannot reinterpret a ballistic weapon field");
            AiDecision decision = PrototypeAiCommander.Choose(game);
            Check(decision.Action == ActionKind.Strike && decision.WeaponDelivery == WeaponDelivery.Ballistic && PrototypeAiCommander.Execute(game, decision, out _), "AI chooses and executes a legal ballistic attack from owned reports");
            PrototypeGame.SaveData exposed = game.CaptureState();
            exposed.ActiveFormationId = "BLUE-BATTERY";
            game.RestoreState(exposed);
            AiDecision relocation = PrototypeAiCommander.Choose(game);
            Check(relocation.Action == ActionKind.Move && game.Area.TerrainAt(relocation.Hex) == OperationalTerrain.Land
                && PrototypeAiCommander.Execute(game, relocation, out _), "Battery AI relocates legally after launch exposure when Command is available");
            foreach (FormationKind kind in Enum.GetValues(typeof(FormationKind)))
            {
                game = Fixture();
                game.Active.Kind = kind;
                FormationState actor = game.Active;
                Check(game.StrikeContact(actor, Report(game), Report(game).LastKnownPosition, Salvo.Light, Reaction.None, null, out _, out _, WeaponDelivery.Ballistic)
                    && actor.BallisticWeapons.Ammunition == 2, "An explicitly equipped formation can launch regardless of kind: " + kind);
            }
        }

        private static void TestAuthoring()
        {
            ScenarioDefinition scenario = ScenarioCatalog.MeridianVeil();
            HexCoord land = scenario.Area.Terrain.First(item => item.Terrain == OperationalTerrain.Land && !scenario.Area.RestrictedAreas.Any(region => region.Hexes.Contains(item.Hex))).Hex;
            scenario.Formations.Add(Battery(land));
            AuthoredScenarioDefinition entry = ScenarioAuthoringCatalog.FromScenario(scenario);
            entry.Id = "ballistic-authoring-test";
            var catalog = new AuthoredScenarioCatalog { Scenarios = new List<AuthoredScenarioDefinition> { entry } };
            Check(ScenarioAuthoringCatalog.Validate(catalog).Count == 0, "A third authored ID validates with a land battery and ballistic profile");
            ScenarioDefinition resolved = ScenarioAuthoringCatalog.Apply(new[] { ScenarioCatalog.MeridianVeil() }, JsonUtility.FromJson<AuthoredScenarioCatalog>(JsonUtility.ToJson(catalog))).Last();
            Check(new PrototypeGame(1978, resolved).Find("BLUE-BATTERY").BallisticWeapons.Ammunition == 3, "Current catalog carries executable per-formation ballistic capability");
            var malformedLegacy = new AuthoredScenarioCatalog { Version = 2, Scenarios = new List<AuthoredScenarioDefinition> { new AuthoredScenarioDefinition { Id = "bad-legacy", TheaterTemplateId = ScenarioAuthoringCatalog.TheaterTemplateIds[0], Formations = null, ScheduledEvents = null, LogisticsRegions = null } } };
            Check(ScenarioAuthoringCatalog.Validate(malformedLegacy).Count > 0, "Malformed legacy authoring reports validation errors rather than crashing migration");
            entry.Formations.Last().BallisticWeapons.Ammunition = 4;
            Check(ScenarioAuthoringCatalog.Validate(catalog).Any(error => error.Contains("Ballistic profile")), "Authoring rejects ammunition beyond capacity");
            entry.Formations.Last().BallisticWeapons.Ammunition = 3;
            entry.Formations.Last().BallisticWeapons.TargetDomains.Add((ContactDomain)999);
            Check(ScenarioAuthoringCatalog.Validate(catalog).Any(error => error.Contains("Ballistic profile")), "Authoring rejects invalid target domains");
        }

        internal static PrototypeGame Fixture()
        {
            ScenarioDefinition scenario = ScenarioCatalog.MeridianVeil();
            foreach (FormationDefinition formation in scenario.Formations) formation.ReadyTime = 3;
            for (int r = 0; r < 3; r++)
            {
                scenario.Area.Terrain.RemoveAll(item => item.Q == 0 && item.R == r);
                scenario.Area.Terrain.Add(new TerrainHexDefinition { Q = 0, R = r, Terrain = OperationalTerrain.Land, Name = "Battery test land" });
            }
            scenario.Area.Terrain.RemoveAll(item => item.Q == 1 && item.R == 0);
            scenario.Area.RestrictedAreas.Clear();
            FormationDefinition target = scenario.Formations.First(item => item.Side == Side.Red && item.Kind == FormationKind.SurfaceGroup);
            target.Q = 2; target.R = 0;
            scenario.Area.Terrain.RemoveAll(item => item.Q == 2 && item.R == 0);
            scenario.Formations.Add(Battery(new HexCoord(0, 0)));
            scenario.Contacts.Clear();
            scenario.Contacts.Add(new ContactDefinition { Owner = Side.Blue, TargetId = target.Id, Q = 2, R = 0, Location = LocationQuality.High, Identity = IdentityQuality.General });
            var game = new PrototypeGame(1978, scenario);
            game.Find(target.Id).HasReacted = true;
            return game;
        }

        private static FormationDefinition Battery(HexCoord hex) => new FormationDefinition
        {
            Id = "BLUE-BATTERY", Name = "Hidden Mobile Launcher", Side = Side.Blue, Kind = FormationKind.BallisticMissileBattery, Q = hex.Q, R = hex.R,
            Ratings = new Ratings { Move = 2, Search = 2, Signature = 1, Strike = 4, Defense = 1, Command = 2 },
            BallisticWeapons = new BallisticWeaponProfile { Range = 4, AttackModifier = 1, Capacity = 3, Ammunition = 3, TargetDomains = new List<ContactDomain> { ContactDomain.Surface, ContactDomain.Land } }
        };
        private static ContactState Report(PrototypeGame game) => game.Contacts.First(item => item.Owner == Side.Blue);
        private static void Check(bool condition, string label) => TestChecks.Check(condition, label);
    }
}
#endif
