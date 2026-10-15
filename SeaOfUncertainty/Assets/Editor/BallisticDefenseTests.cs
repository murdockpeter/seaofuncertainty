#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using SeaOfUncertainty.Core;
using SeaOfUncertainty.Prototype;
using UnityEngine;

namespace SeaOfUncertainty.Editor
{
    public static class BallisticDefenseTests
    {
        public static void RunUnit()
        {
            foreach (string id in new[] { "meridian-veil", "northern-gateway" })
                Check(new PrototypeGame(1978, id == "meridian-veil" ? ScenarioCatalog.MeridianVeil() : ScenarioCatalog.LuzonStrait()).Formations.All(item => item.BallisticDefense.Capacity == 0), "Compiled legacy template gains no interceptor equipment: " + id);
            TestSelectionAndBoundaries();
            TestDomainsAndSelfDefense();
            TestCommitmentAndPrivacy();
            TestCoveragePresentationQueries();
            TestChecks.ThrowIfAny("Ballistic defense unit");
            Debug.Log("Ballistic defense unit checks passed.");
        }

        public static void RunIntegration()
        {
            TestReloadAndMigration();
            TestSynchronizedExhaustion();
            TestChecks.ThrowIfAny("Ballistic defense integration");
            Debug.Log("Ballistic defense integration checks passed.");
        }

        private static void TestCoveragePresentationQueries()
        {
            PrototypeGame game = Fixture();
            FormationState target = Target(game);
            FormationState provider = Provider(game, "DEF-UI", new HexCoord(3, 0), 4, 1);
            Check(game.FriendlyBallisticDefenseAvailable(Side.Red, provider) && game.FriendlyBallisticProtectedFormations(Side.Red, provider).Contains(target), "Friendly presentation queries use current interception eligibility");
            Check(game.FriendlyBallisticCoverageHexes(Side.Red, provider).All(hex => game.Area.Contains(hex) && HexCoord.Distance(hex, provider.Position) <= 1), "Coverage footprint clips exact hex radius to map bounds");
            foreach (int q in new[] { 2, 3 })
            {
                provider.Position = new HexCoord(q, 3);
                var footprint = game.FriendlyBallisticCoverageHexes(Side.Red, provider);
                Check(footprint.Count == 7 && footprint.Contains(provider.Position) && footprint.Count(hex => HexCoord.Distance(hex, provider.Position) == 1) == 6,
                    "Radius one contains the center and all six neighbors in even and odd columns: " + q);
            }
            provider.BallisticDefense.SelfDefenseOnly = true;
            Check(game.FriendlyBallisticCoverageHexes(Side.Red, provider).SequenceEqual(new[] { provider.Position }), "Self-defense footprint contains only the provider hex even with a larger authored radius");
            provider.BallisticDefense.SelfDefenseOnly = false;
            provider.Position = new HexCoord(3, 0);
            Check(game.FriendlyBallisticCoverageHexes(Side.Blue, provider).Count == 0 && game.FriendlyBallisticProtectedFormations(Side.Blue, provider).Count == 0
                && game.FriendlyBallisticDefenseStatus(Side.Blue, provider) == string.Empty && !game.FriendlyBallisticDefenseAvailable(Side.Blue, provider), "All defense presentation queries reject another side's equipment");
            Check(game.FriendlyBallisticDefenseStatus(Side.Red, provider, game.Active) == string.Empty, "Status reasons reject an enemy protected target too");
            target.Position = new HexCoord(5, 0);
            Check(game.FriendlyBallisticDefenseStatus(Side.Red, provider, target).Contains("Outside"), "Coverage failure explains the authoritative range restriction");
            provider.BallisticDefense.Interceptors = 0;
            Check(!game.FriendlyBallisticDefenseAvailable(Side.Red, provider) && game.FriendlyBallisticDefenseStatus(Side.Red, provider).Contains("depleted")
                && game.FriendlyBallisticProtectedFormations(Side.Red, provider).Count == 0, "Depleted equipment explains lack of coverage and protects no formations");
            provider.BallisticDefense.Interceptors = 2; provider.Replenishing = true;
            Check(game.FriendlyBallisticDefenseStatus(Side.Red, provider).Contains("replenishing"), "Servicing explains disabled interception");
        }

        private static void TestSelectionAndBoundaries()
        {
            PrototypeGame game = Fixture();
            FormationState target = Target(game);
            FormationState far = Provider(game, "DEF-A", new HexCoord(4, 0), 5, 2);
            FormationState near = Provider(game, "DEF-B", new HexCoord(3, 0), 4, 2);
            Check(Coverage(game).First() == far, "Highest effective rating wins even when another provider is closer");
            near.BallisticDefense.InterceptionRating = 5;
            Check(Coverage(game).First() == near, "Equal ratings choose the shorter coverage distance");
            far.Position = new HexCoord(2, 1);
            game.Formations.Reverse();
            Check(Coverage(game).First() == far, "Equal ratings and distances choose ordinal ID independently of list order");
            far.Damage = DamageState.Heavy;
            Check(Coverage(game).First() == near && far.BallisticDefense.EffectiveRating(far) == 3, "Heavy damage reduces interception rating by two before ranking");
            foreach (string restriction in new[] { "outside", "off-map", "zero", "crippled", "destroyed", "replenishing", "not-ready", "launcher", "magazine", "wrong-side", "domain", "self-only", "deep-provider", "invalid-rating" })
            {
                game = Fixture(); target = Target(game);
                FormationState defender = Provider(game, "DEF-A", new HexCoord(3, 0), 4, 1);
                Check(Coverage(game).Contains(defender), "Coverage includes the exact radius boundary");
                switch (restriction)
                {
                    case "outside": defender.Position = new HexCoord(4, 0); break;
                    case "off-map": defender.Position = new HexCoord(2, -1); break;
                    case "zero": defender.BallisticDefense.Interceptors = 0; break;
                    case "crippled": defender.Damage = DamageState.Crippled; break;
                    case "destroyed": defender.Damage = DamageState.Destroyed; break;
                    case "replenishing": defender.Replenishing = true; break;
                    case "not-ready": defender.BallisticDefense.RequireReady = true; defender.ReadyTime = game.Time + 1; break;
                    case "launcher": defender.ActiveEffectCardIds.Add(EntropyCardIds.LauncherDamage); break;
                    case "magazine": defender.ActiveEffectCardIds.Add(EntropyCardIds.MagazineDamage); break;
                    case "wrong-side": defender.Side = Side.Blue; break;
                    case "domain": defender.BallisticDefense.ProtectedDomains.Clear(); defender.BallisticDefense.ProtectedDomains.Add(ContactDomain.Air); break;
                    case "self-only": defender.BallisticDefense.SelfDefenseOnly = true; break;
                    case "deep-provider": defender.Kind = FormationKind.Submarine; defender.SubmarineDepth = SubmarineDepthState.Deep; break;
                    case "invalid-rating": defender.BallisticDefense.InterceptionRating = 7; break;
                }
                Check(!Coverage(game).Contains(defender), "Ineligible provider excluded: " + restriction);
                Check(game.FriendlyBallisticCoverage(Side.Blue, target).Count == 0, "An opponent cannot use the friendly coverage query: " + restriction);
            }
            game = Fixture(); target = Target(game);
            target.BallisticDefense = Profile(4, 0);
            target.BallisticDefense.SelfDefenseOnly = true;
            Check(Coverage(game).Single() == target, "Equipped targets may defend themselves at radius zero");
            Check(Launch(game, out CombatResult self, out _) && self.BallisticInterceptionModifier == 4 && target.BallisticDefense.Interceptors == 1 && target.HasReacted, "Self-defense spends one package while preserving the already-spent ordinary Reaction");
            target.BallisticDefense.SelfDefenseOnly = false;
            target.BallisticDefense.CanProtectSelf = false;
            Check(Coverage(game).Count == 0, "Explicit self-defense exclusion is respected");
        }

        private static void TestDomainsAndSelfDefense()
        {
            foreach (FormationKind kind in Enum.GetValues(typeof(FormationKind)))
            {
                PrototypeGame game = Fixture();
                FormationState actor = game.Active, target = Target(game);
                target.Kind = kind;
                if (kind == FormationKind.BallisticMissileBattery) { target.Position = new HexCoord(0, 1); Report(game).LastKnownPosition = target.Position; }
                ContactDomain domain = kind == FormationKind.AirGroup ? ContactDomain.Air : kind == FormationKind.Submarine ? ContactDomain.Subsurface : kind == FormationKind.BallisticMissileBattery ? ContactDomain.Land : ContactDomain.Surface;
                Report(game).Domain = domain;
                actor.BallisticWeapons.TargetDomains = new List<ContactDomain> { domain };
                actor.BallisticWeapons.AllowDeepSubmarines = domain == ContactDomain.Subsurface;
                FormationState provider = Provider(game, "DEF-A", kind == FormationKind.BallisticMissileBattery ? new HexCoord(1, 0) : new HexCoord(3, 0), 4, 1);
                provider.BallisticDefense.ProtectedDomains = new List<ContactDomain> { domain };
                provider.BallisticDefense.AllowDeepSubmarines = domain == ContactDomain.Subsurface;
                bool reactionSpent = provider.HasReacted = true;
                Check(Launch(game, out CombatResult result, out _) && result.BallisticInterceptionModifier == 4 && provider.BallisticDefense.Interceptors == 1, "Explicit domain coverage protects target kind: " + kind);
                Check(provider.HasReacted == reactionSpent && provider.ReadyTime == 0, "Interception does not spend ordinary Reaction or Ready Time: " + kind);
            }
            foreach (FormationKind kind in Enum.GetValues(typeof(FormationKind)))
            {
                PrototypeGame game = Fixture();
                FormationState provider = Provider(game, "DEF-A", kind == FormationKind.BallisticMissileBattery ? new HexCoord(0, 1) : new HexCoord(3, 0), 4, 2);
                provider.Kind = kind; provider.BallisticDefense.CanInterceptWhileDeep = kind == FormationKind.Submarine;
                Check(Launch(game, out CombatResult result, out _) && result.BallisticInterceptionModifier == 4 && provider.BallisticDefense.Interceptors == 1, "Explicitly equipped provider kind may intercept: " + kind);
            }
            PrototypeGame submarine = Fixture();
            FormationState sub = Target(submarine); sub.Kind = FormationKind.Submarine;
            submarine.Active.BallisticWeapons.TargetDomains = new List<ContactDomain> { ContactDomain.Subsurface };
            submarine.Active.BallisticWeapons.AllowShallowSubmarines = true;
            Report(submarine).Domain = ContactDomain.Subsurface;
            FormationState escort = Provider(submarine, "DEF-A", new HexCoord(3, 0), 5, 1);
            escort.BallisticDefense.ProtectedDomains = new List<ContactDomain> { ContactDomain.Subsurface };
            escort.BallisticDefense.AllowDeepSubmarines = true;
            Check(Launch(submarine, out CombatResult missed, out string message) && missed.Roll == 0 && message.Contains("no confirmed effect") && escort.BallisticDefense.Interceptors == 2,
                "Coverage cannot legalize an attack against an ineligible hidden submarine depth");
            FormationState launcher = submarine.Find("BLUE-BATTERY");
            sub.SubmarineDepth = SubmarineDepthState.Shallow; Select(submarine, launcher);
            Check(Launch(submarine, out CombatResult uncovered, out _) && uncovered.BallisticInterceptionModifier == 0 && escort.BallisticDefense.Interceptors == 2, "A legal shallow target needs separate shallow coverage permission");
            escort.BallisticDefense.AllowShallowSubmarines = true; Select(submarine, launcher);
            Check(Launch(submarine, out CombatResult covered, out _) && covered.BallisticInterceptionModifier == 5 && escort.BallisticDefense.Interceptors == 1, "Explicit shallow target coverage enables interception");
        }

        private static void TestCommitmentAndPrivacy()
        {
            PrototypeGame game = Fixture();
            FormationState provider = Provider(game, "DEF-SECRET", new HexCoord(3, 0), 4, 1);
            provider.Ratings.ElectronicWarfare = 20;
            var telemetry = new PlaytestRecorder(); telemetry.StartNew(game);
            game.CommandEvent += (actor, category, detail) => telemetry.RecordCommandEvent(game, actor, category, detail);
            string[] attackerPreview = { AuthoritativeReplay.StateDigest(game), string.Join("|", game.VisibleLog(Side.Blue)) };
            Check(game.CanStrikeContact(game.Active, Report(game), Report(game).LastKnownPosition, Salvo.Standard, WeaponDelivery.Ballistic, out _)
                && provider.BallisticDefense.Interceptors == 2 && attackerPreview[0] == AuthoritativeReplay.StateDigest(game), "Previews never reserve or spend interceptors");
            Check(Launch(game, out CombatResult result, out string outcome) && result.BallisticInterceptionModifier == 4 && result.MissileDefenseModifier == 0 && provider.BallisticDefense.Interceptors == 1,
                "One eligible provider spends exactly one package on a committed ballistic hit");
            Check(!outcome.Contains(provider.Id) && !outcome.Contains(provider.Name) && !JsonUtility.ToJson(result).Contains(provider.Id)
                && !game.VisibleLog(Side.Blue).Any(line => line.Contains(provider.Id) || line.Contains(provider.Name)), "Attacker result and visible logs contain no hidden provider identity");
            Check(game.VisibleLog(Side.Red).Any(line => line.Contains(provider.Id) && line.Contains("interceptors 1/2"))
                && telemetry.Session.Events.Where(item => item.Category == "BallisticInterception").All(item => item.Side == "Red")
                && telemetry.Session.Events.Count(item => item.Category == "BallisticInterception") == 1, "Defender receipt and telemetry record provider and expenditure on its own side");
            var server = new AuthoritativeMatchServer("defense-privacy", 1978, game.Scenario);
            server.Game.RestoreState(JsonUtility.FromJson<PrototypeGame.SaveData>(JsonUtility.ToJson(game.CaptureState())));
            Check(!JsonUtility.ToJson(server.SnapshotFor(Side.Blue)).Contains(provider.Id) && JsonUtility.ToJson(server.SnapshotFor(Side.Red)).Contains(provider.Id), "Side snapshots reveal interceptor stock only to the owner");
            foreach (string reason in new[] { "false", "empty", "unauthorized", "reaction", "conventional" })
            {
                game = Fixture(); provider = Provider(game, "DEF-A", new HexCoord(3, 0), 4, 1);
                if (reason == "false") Report(game).IsFalse = true;
                if (reason == "empty") Target(game).Position = new HexCoord(4, 0);
                if (reason == "unauthorized") game.Active.Friction = game.Active.Disruption = game.Active.Destruction = true;
                if (reason == "reaction") Target(game).HasReacted = false;
                if (reason == "conventional") { game.Active.Kind = FormationKind.SurfaceGroup; game.Active.Weapons = new WeaponInventoryState { Standard = 2, MaxStandard = 2 }; }
                game.StrikeContact(game.Active, Report(game), Report(game).LastKnownPosition, Salvo.Standard, Reaction.None, null, out _, out _, reason == "conventional" ? WeaponDelivery.Conventional : WeaponDelivery.Ballistic);
                Check(provider.BallisticDefense.Interceptors == 2, "No ballistic interception expenditure for " + reason);
            }
        }

        private static void TestReloadAndMigration()
        {
            PrototypeGame game = Fixture();
            FormationState provider = Provider(game, "DEF-A", new HexCoord(8, 0), 4, 1);
            provider.BallisticDefense.Interceptors = 0; provider.MajorActions = 1;
            game.Area.Locations.Add(new OperationalLocationDefinition { Id = "interceptor-depot", Name = "Interceptor depot", Kind = LocationKind.Port, Q = 8, R = 0 });
            var region = new OperationalRegionDefinition { Id = "defense-reload", Name = "Defense reload", SupportsBallisticReload = true, Hexes = new List<HexCoord> { provider.Position } };
            game.Scenario.LogisticsRegions.Add(region);
            Select(game, provider);
            Check(game.Replenish(provider, null, out _) && provider.BallisticDefense.Interceptors == 0, "Ballistic launch reload service does not implicitly reload interceptors");
            region.SupportsInterceptorReload = true; provider.Replenishing = false;
            Select(game, provider);
            Check(game.Replenish(provider, null, out _) && provider.BallisticDefense.Interceptors == 2, "Only explicit compatible interceptor logistics restores stock");
            PrototypeGame.SaveData saved = JsonUtility.FromJson<PrototypeGame.SaveData>(JsonUtility.ToJson(game.CaptureState()));
            var restored = new PrototypeGame(1978, game.Scenario); restored.RestoreState(saved);
            Check(AuthoritativeReplay.StateDigest(game) == AuthoritativeReplay.StateDigest(restored), "Save v13 round trip preserves interceptor stocks and status");
            saved.Version = 12; restored.RestoreState(saved);
            Check(restored.Formations.All(item => item.BallisticDefense.Capacity == 0) && restored.Find("BLUE-BATTERY").BallisticWeapons.Capacity == 3, "Pre-v13 migration gives zero defense while retaining v12 ballistic weapons");
            AuthoredScenarioDefinition entry = ScenarioAuthoringCatalog.FromScenario(ScenarioCatalog.MeridianVeil());
            entry.Formations[0].BallisticDefense = Profile(4, 2);
            var catalog = new AuthoredScenarioCatalog { Scenarios = new List<AuthoredScenarioDefinition> { entry } };
            Check(ScenarioAuthoringCatalog.Validate(catalog).Count == 0, "Catalog v4 accepts explicit per-formation ballistic defense");
            entry.Formations[0].BallisticDefense.Interceptors = 3;
            Check(ScenarioAuthoringCatalog.Validate(catalog).Any(error => error.Contains("Ballistic defense")), "Authoring rejects interceptor stocks over capacity");
            entry.Formations[0].BallisticDefense.Interceptors = 2; catalog.Version = 3;
            Check(ScenarioAuthoringCatalog.Apply(new[] { ScenarioCatalog.MeridianVeil() }, catalog).Single().Formations.All(item => item.BallisticDefense.Capacity == 0), "Legacy catalog v3 cannot gain interceptor equipment from extra fields");
        }

        private static void TestSynchronizedExhaustion()
        {
            PrototypeGame game = Fixture();
            FormationState leader = game.Active;
            FormationState second = JsonUtility.FromJson<FormationState>(JsonUtility.ToJson(leader));
            second.Id = "ZZ-BATTERY"; second.Name = "Second battery"; second.Position = new HexCoord(0, 1);
            game.Formations.Add(second);
            FormationState alpha = Provider(game, "DEF-A", new HexCoord(3, 0), 5, 1);
            FormationState beta = Provider(game, "DEF-B", new HexCoord(2, 1), 3, 1);
            alpha.BallisticDefense.Interceptors = alpha.BallisticDefense.Capacity = 1;
            beta.BallisticDefense.Interceptors = beta.BallisticDefense.Capacity = 1;
            var declaration = new AuthoritativeCommand { Version = 2, Kind = AuthoritativeCommandKind.DeclareSynchronizedStrike, Side = Side.Blue, ActorId = leader.Id,
                TargetId = Report(game).TargetId, Q = 2, R = 0, Salvo = Salvo.Standard, WeaponDelivery = WeaponDelivery.Ballistic, ParticipantIds = new List<string> { second.Id, leader.Id } };
            string undeclared = AuthoritativeReplay.StateDigest(game);
            Check(!AuthoritativeCommandProcessor.Execute(game, declaration, out _) && undeclared == AuthoritativeReplay.StateDigest(game), "Version 2 cannot silently reinterpret a ballistic synchronized declaration");
            declaration.Version = 3;
            Check(AuthoritativeCommandProcessor.Execute(game, JsonUtility.FromJson<AuthoritativeCommand>(JsonUtility.ToJson(declaration)), out _), "Version 3 serialized declaration uses owned Contact and per-participant capability");
            SynchronizedStrikeState strike = game.SynchronizedStrikes.Single();
            int guard = 0;
            while (game.ReadySynchronizedStrikeFor(leader) == null && game.Active != null && guard++ < 40) game.Hold(game.Active, out _);
            Check(game.ReadySynchronizedStrikeFor(leader) == strike, "Ballistic synchronized reservation reaches its Strike Time");
            PrototypeGame.SaveData saved = JsonUtility.FromJson<PrototypeGame.SaveData>(JsonUtility.ToJson(game.CaptureState()));
            var restored = new PrototypeGame(1978, game.Scenario); restored.RestoreState(saved);
            Check(restored.SynchronizedStrikes.Single().Participants.All(item => item.WeaponDelivery == WeaponDelivery.Ballistic), "Save/load preserves synchronized weapon delivery and interceptor stocks before resolution");
            second.BallisticWeapons.Ammunition = 0;
            string before = AuthoritativeReplay.StateDigest(game);
            Check(!game.ResolveSynchronizedStrike(strike, Reaction.None, null, false, out _, out _) && before == AuthoritativeReplay.StateDigest(game), "A depleted participant rejects the volley without spending any attack or defense inventory");
            second.BallisticWeapons.Ammunition = 3;
            Check(game.ResolveSynchronizedStrike(strike, Reaction.None, null, false, out SynchronizedStrikeResult result, out string outcome), "Synchronized ballistic volley resolves");
            Check(result.Attacks.Select(item => item.BallisticInterceptionModifier).SequenceEqual(new[] { 5, 3 }) && alpha.BallisticDefense.Interceptors == 0 && beta.BallisticDefense.Interceptors == 0,
                "Each participant recomputes coverage, exhausting the best provider before selecting the next");
            Check(leader.BallisticWeapons.Ammunition == 2 && second.BallisticWeapons.Ammunition == 2 && leader.Loud && second.Loud, "Each synchronized launcher spends exactly once and retains launch exposure");
            Check(restored.ResolveSynchronizedStrike(restored.SynchronizedStrikes.Single(), Reaction.None, null, false, out _, out _)
                && AuthoritativeReplay.StateDigest(game) == AuthoritativeReplay.StateDigest(restored), "Resolution after save/load yields the same final authoritative digest");
            Check(!outcome.Contains(alpha.Id) && !outcome.Contains(beta.Id) && !game.VisibleLog(Side.Blue).Any(line => line.Contains(alpha.Id) || line.Contains(beta.Id)), "Synchronized receipts do not reveal hidden defending providers");
            string completed = AuthoritativeReplay.StateDigest(game);
            Check(!game.ResolveSynchronizedStrike(strike, Reaction.None, null, false, out _, out _) && completed == AuthoritativeReplay.StateDigest(game), "Resolving the same volley twice cannot spend another interception package");

            game = Fixture(); leader = game.Active;
            second = JsonUtility.FromJson<FormationState>(JsonUtility.ToJson(leader)); second.Id = "ZZ-BATTERY"; second.Position = new HexCoord(0, 1); game.Formations.Add(second);
            FormationState self = Target(game); self.Ratings.Defense = 0; self.Damage = DamageState.Heavy; self.BallisticDefense = Profile(6, 0);
            leader.Ratings.Strike = second.Ratings.Strike = 20;
            FormationState reserve = Provider(game, "DEF-RESERVE", new HexCoord(3, 0), 1, 1);
            Check(game.DeclareSynchronizedStrike(leader, new[] { second, leader }, Report(game), Report(game).LastKnownPosition, Salvo.Standard, false, null, out strike, out _, WeaponDelivery.Ballistic), "Self-defense loss fixture declares legally");
            guard = 0; while (game.ReadySynchronizedStrikeFor(leader) == null && game.Active != null && guard++ < 40) game.Hold(game.Active, out _);
            Check(game.ResolveSynchronizedStrike(strike, Reaction.None, null, false, out result, out _) && result.Attacks[0].BallisticInterceptionModifier == 4,
                "The damaged target's effective self-defense applies before the first hit");
            bool killedFirst = result.Attacks[0].ResultingDamage == DamageState.Destroyed;
            Check(self.Damage >= DamageState.Crippled && result.Attacks[1].BallisticInterceptionModifier == (killedFirst ? 0 : 1)
                && self.BallisticDefense.Interceptors == 1 && reserve.BallisticDefense.Interceptors == (killedFirst ? 2 : 1),
                "A provider crippled/destroyed by the first hit cannot protect the next participant");
        }

        private static PrototypeGame Fixture()
        {
            PrototypeGame game = BallisticLaunchTests.Fixture();
            Target(game).Ratings.Defense = 30;
            return game;
        }
        private static BallisticDefenseProfile Profile(int rating, int radius) => new BallisticDefenseProfile { InterceptionRating = rating, Interceptors = 2, Capacity = 2, CoverageRadius = radius, ProtectedDomains = new List<ContactDomain> { ContactDomain.Surface } };
        private static FormationState Provider(PrototypeGame game, string id, HexCoord hex, int rating, int radius)
        {
            var provider = new FormationState { Id = id, Name = "Hidden escort " + id, Side = Side.Red, Kind = FormationKind.SurfaceGroup, Position = hex, Ratings = new Ratings { Move = 2, Command = 2 }, BallisticDefense = Profile(rating, radius) };
            game.Formations.Add(provider); return provider;
        }
        private static void Select(PrototypeGame game, FormationState actor) { actor.ReadyTime = game.Time; PrototypeGame.SaveData state = game.CaptureState(); state.ActiveFormationId = actor.Id; game.RestoreState(state); }
        private static ContactState Report(PrototypeGame game) => game.Contacts.First(item => item.Owner == Side.Blue);
        private static FormationState Target(PrototypeGame game) => game.Find(Report(game).TargetId);
        private static IReadOnlyList<FormationState> Coverage(PrototypeGame game) => game.FriendlyBallisticCoverage(Side.Red, Target(game));
        private static bool Launch(PrototypeGame game, out CombatResult result, out string message) => game.StrikeContact(game.Active, Report(game), Report(game).LastKnownPosition, Salvo.Standard, Reaction.None, null, out result, out message, WeaponDelivery.Ballistic);
        private static void Check(bool condition, string label) => TestChecks.Check(condition, label);
    }
}
#endif
