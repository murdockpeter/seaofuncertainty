#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SeaOfUncertainty.Core;
using SeaOfUncertainty.Prototype;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeaOfUncertainty.Editor
{
    public static class ContinuationTests
    {
        public static void Run()
        {
            TestReplayVersions();
            TestLegacyMigration();
            TestPreviews();
            TestPatrolReacquisitionTelemetry();
            TestReactionContinuation();
            TestSynchronizedContinuation();
            TestNarrowsShield();
            foreach (string id in new[] { "meridian-veil", "northern-gateway", "narrows-shield" })
                foreach (int seed in new[] { 4200, -1729 }) TestGameContinuation(id, seed);
            TestChecks.ThrowIfAny("Continuation and acceptance");
        }

        private static void TestGameContinuation(string id, int seed)
        {
            var uninterrupted = new PrototypeGame(seed, ScenarioCatalog.Require(id));
            PrototypeGame resumed = RoundTrip(uninterrupted);
            int actions = 0, launches = 0, interceptions = 0;
            uninterrupted.CommandEvent += (_, kind, detail) => { if (kind == "BallisticLaunch") launches++; if (kind == "BallisticInterception") interceptions++; };
            while (uninterrupted.Time < uninterrupted.Scenario.Horizon && actions++ < 600)
            {
                AiDecision choice = PrototypeAiCommander.Choose(uninterrupted);
                bool first = PrototypeAiCommander.Execute(uninterrupted, choice, out string message);
                bool second = PrototypeAiCommander.Execute(resumed, JsonUtility.FromJson<AiDecision>(JsonUtility.ToJson(choice)), out _);
                Check(first && second, id + " legal seeded decision " + actions + ": " + message);
                Check(AuthoritativeReplay.StateDigest(uninterrupted) == AuthoritativeReplay.StateDigest(resumed), id + " identical future rolls and state after action " + actions);
                if (AuthoritativeReplay.StateDigest(uninterrupted) != AuthoritativeReplay.StateDigest(resumed))
                {
                    Directory.CreateDirectory(Path.Combine(Application.dataPath, "../TestResults"));
                    File.WriteAllText(Path.Combine(Application.dataPath, "../TestResults/divergence-original.json"), JsonUtility.ToJson(uninterrupted.CaptureState(), true));
                    File.WriteAllText(Path.Combine(Application.dataPath, "../TestResults/divergence-restored.json"), JsonUtility.ToJson(resumed.CaptureState(), true));
                    throw new InvalidOperationException("Continuation diverged: " + id + " action " + actions + "; inspect divergence JSON.");
                }
                if (!first || !second) break;
                if (actions % 5 == 0) resumed = RoundTrip(resumed);
            }
            Check(uninterrupted.Time >= uninterrupted.Scenario.Horizon && actions < 600, id + " completes without stalls");
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../TestResults/acceptance-2026-10-19"));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, id + "-" + seed + ".json"), JsonUtility.ToJson(new OperationSave { Game = uninterrupted.CaptureState() }, true));
            Debug.Log($"CONTINUATION ACCEPTANCE {id} seed={seed} actions={actions} launches={launches} interceptions={interceptions} T{uninterrupted.Time}; repeated loads match uninterrupted play.");
        }

        private static void TestReplayVersions()
        {
            foreach (int version in new[] { 1, 2 })
            {
                var game = new PrototypeGame(4200);
                var document = new AuthoritativeReplayDocument { Version = version };
                for (int i = 0; i < 12; i++)
                    AuthoritativeReplay.Record(document, game, AuthoritativeCommand.FromAiDecision(game, PrototypeAiCommander.Choose(game)));
                Check(game.CaptureState().RandomDrawCount > 0, "Replay fixture consumes combat randomness");
                AuthoritativeReplayResult replayed = AuthoritativeReplay.Replay(AuthoritativeReplay.FromJson(AuthoritativeReplay.ToJson(document)));
                Check(replayed.Success && AuthoritativeReplay.StateDigest(game) == AuthoritativeReplay.StateDigest(replayed.Game), "Replay v" + version + " reproduces random-consuming commands");
                var server = new AuthoritativeMatchServer("recovery-v" + version, document);
                Check(AuthoritativeReplay.StateDigest(server.Game) == AuthoritativeReplay.StateDigest(game), "Authority checkpoint recovery accepts replay v" + version);
            }
        }

        private static void TestLegacyMigration()
        {
            PrototypeGame.SaveData state = new PrototypeGame(4200).CaptureState();
            state.Version = 13;
            state.RandomSeed = 99; state.RandomDrawCount = 123;
            PrototypeGame migrated = OperationSaveStore.Decode(JsonUtility.ToJson(state)).Game;
            Check(migrated.Seed == 1978 && migrated.CaptureState().RandomDrawCount == 0, "Legacy saves get an explicit repeatable random baseline");
            state.Version = 14; state.RandomDrawCount = -1;
            bool rejected = false;
            try { OperationSaveStore.Decode(JsonUtility.ToJson(state)); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Invalid random progress is rejected before loading");
        }

        private static void TestPreviews()
        {
            var game = new PrototypeGame();
            string before = AuthoritativeReplay.StateDigest(game);
            foreach (ActionKind action in Enum.GetValues(typeof(ActionKind))) game.CanPlanAction(game.Active, action, out _);
            foreach (Salvo salvo in Enum.GetValues(typeof(Salvo))) game.CanPlanStrike(game.Active, salvo, WeaponDelivery.Ballistic, out _);
            foreach (SearchMode mode in Enum.GetValues(typeof(SearchMode))) game.CanSearchArea(game.Active, game.Active.Position, mode, out _);
            game.CanAssignStandingMission(game.Active, ActionKind.Search, game.Active.MissionObjective, null, game.Active.Position, MissionPosture.Balanced, MissionTrigger.OnReady, out _);
            Check(before == AuthoritativeReplay.StateDigest(game), "Order previews do not spend Command, ammunition, randomness or mutate missions");
            game.Active.Mission = ActionKind.Search;
            game.Active.ActiveEffectCardIds.Add(EntropyCardIds.JammedCircuits);
            Check(!game.CanSearchArea(game.Active, game.Active.Position, SearchMode.Focused, out string reason) && reason.Contains("Jammed Circuits"), "Search preview explains the same blocking effect as execution");
            Check(!game.SearchArea(game.Active, game.Active.Position, SearchMode.Focused, out string execution) && execution == reason, "Blocked Search preview and execution agree");
        }

        private static void TestPatrolReacquisitionTelemetry()
        {
            var game = new PrototypeGame(4200, ScenarioCatalog.MeridianVeil());
            FormationState moving = game.Formations.First(item => item.Side == Side.Red && item.Kind == FormationKind.CarrierGroup);
            FormationState screen = game.Formations.First(item => item.Side == Side.Blue && item.Kind == FormationKind.SurfaceGroup);
            screen.Position = new HexCoord(8, 5); screen.PatrolActive = true; screen.PatrolCenter = new HexCoord(9, 5); screen.PatrolInterceptionAvailable = true;
            var lost = new ContactState { Owner = Side.Blue, TargetId = moving.Id, LastKnownPosition = moving.Position, IsLost = true };
            game.Contacts.Add(lost);
            PrototypeGame.SaveData state = game.CaptureState(); state.ActiveFormationId = moving.Id; game.RestoreState(state);
            var recorder = new PlaytestRecorder(); recorder.StartNew(game);
            PlaytestRecorder.Observation before = recorder.Observe(game, moving);
            Check(game.Move(moving, new HexCoord(9, 5), MoveMode.Normal, out string message), "Move through a hostile Screen exercises reacquisition");
            Check(!lost.IsLost && game.Contacts.Count(item => item.Owner == Side.Blue && item.TargetId == moving.Id) == 1, "Patrol reacquires its lost report without duplicate owner/target IDs");
            recorder.RecordAction(game, before, moving, "Move", "Normal", "", "", 0, "", message);
            Check(recorder.Session.Events.Last().ContactChanges.Contains(moving.Id), "Telemetry records the reacquisition without interrupting the action");
            game.Contacts.Add(new ContactState { Owner = Side.Blue, TargetId = moving.Id, IsLost = true });
            before = recorder.Observe(game, moving);
            lost.Age++;
            recorder.RecordAction(game, before, moving, "Legacy duplicate", "", "", "", 0, "", "");
            Check(recorder.Session.Events.Last().ContactChanges.Contains("Lost"), "Existing duplicate reports in legacy saves remain observable and cannot crash telemetry");
        }

        private static void TestReactionContinuation()
        {
            bool background = Application.runInBackground;
            var host = new GameObject("Reaction continuation acceptance");
            string path = Path.Combine(Application.temporaryCachePath, "reaction-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var backend = host.AddComponent<SeaPrototypeController>();
                backend.EditorSavePathOverride = path;
                backend.EditorEnsureInitializedForTests();
                var ui = host.AddComponent<SeaUIToolkitController>();
                ui.EditorEnsureInitializedForTests();
                foreach (Side side in new[] { Side.Blue, Side.Red })
                {
                    backend.ToolkitNewScenario("meridian-veil");
                    backend.ToolkitSetOperationMode(OperationMode.LocalHotseat);
                    FormationState actor = backend.Game.Formations.First(item => item.Side == side && item.Kind == FormationKind.Submarine);
                    PrototypeGame.SaveData state = backend.Game.CaptureState();
                    state.ActiveFormationId = actor.Id; backend.Game.RestoreState(state);
                    ContactState contact = backend.Game.Contacts.First(item => item.Owner == side && item.TargetId == backend.Game.Formations.First(f => f.Side != side && f.Kind == FormationKind.CarrierGroup).Id);
                    ui.EditorBeginReaction(contact, WeaponDelivery.Ballistic);
                    Check(backend.PendingReaction != null && backend.ToolkitSave(), "Committed reaction saves before rolling: " + side);
                    Check(ui.EditorInvokeButton("ASSUME DEFENSIVE CONTROL"), "Original defender assumes secure control");
                    Check(ui.EditorInvokeButton("HOLD  •  PRESERVE POSITION  •  NO MODIFIER"), "Original reaction resolves");
                    string expected = AuthoritativeReplay.StateDigest(backend.Game);
                    Check(backend.ToolkitLoad(), "Reaction boundary reload succeeds");
                    ui.EditorContinueOperation();
                    string text = string.Join("\n", ui.EditorRoot.Query<Label>().ToList().Select(item => item.text));
                    Check(backend.Game.Formations.All(item => !text.Contains(item.Name)), "Reload returns to concealed defender handoff without either side's formation names");
                    Check(ui.EditorInvokeButton("ASSUME DEFENSIVE CONTROL") && ui.EditorInvokeButton("HOLD  •  PRESERVE POSITION  •  NO MODIFIER"), "Reloaded defender can resolve the same reaction");
                    Check(backend.PendingReaction == null && expected == AuthoritativeReplay.StateDigest(backend.Game), "Reaction reload preserves exactly-once ammunition and future rolls: " + side);
                }
                backend.ToolkitNewScenario("meridian-veil");
                backend.ToolkitSetOperationMode(OperationMode.SoloVsAi, Side.Blue);
                FormationState aiActor = backend.Game.Formations.First(item => item.Side == Side.Red && item.Kind == FormationKind.Submarine);
                PrototypeGame.SaveData aiState = backend.Game.CaptureState(); aiState.ActiveFormationId = aiActor.Id; backend.Game.RestoreState(aiState);
                ContactState aiContact = backend.Game.Contacts.First(item => item.Owner == Side.Red && backend.Game.Find(item.TargetId)?.Kind == FormationKind.CarrierGroup);
                var aiChoice = new AiDecision { Action = ActionKind.Strike, TargetId = aiContact.TargetId, Hex = aiContact.LastKnownPosition, Salvo = Salvo.Standard, WeaponDelivery = WeaponDelivery.Ballistic, Rationale = "Continuation acceptance" };
                ui.EditorBeginReaction(aiContact, WeaponDelivery.Ballistic, aiChoice);
                Check(backend.ToolkitSave() && backend.PendingReaction.HasAiDecision, "Solo AI declaration saves its pending human defender choice");
                Check(ui.EditorInvokeButton("HOLD  \u2022  PRESERVE POSITION  \u2022  NO MODIFIER"), "Human defender resolves original Solo AI attack");
                string aiExpected = AuthoritativeReplay.StateDigest(backend.Game);
                Check(backend.ToolkitLoad(), "Solo pending AI attack reloads"); ui.EditorContinueOperation();
                Check(ui.EditorInvokeButton("HOLD  \u2022  PRESERVE POSITION  \u2022  NO MODIFIER") && aiExpected == AuthoritativeReplay.StateDigest(backend.Game), "Solo reload routes the saved AI decision exactly once with matching rolls");
                ui.EditorShowRules();
                Check(ui.EditorInvokeButton("MAP QUICK START") && ui.EditorRoot.Q<VisualElement>("dialog-mapquickstart") != null, "Quick start can be reopened from Field Manual");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                Application.runInBackground = background;
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        }

        private static void TestSynchronizedContinuation()
        {
            foreach (int replayVersion in new[] { 1, 2 })
            {
                var game = new PrototypeGame(4200, ScenarioCatalog.Require("meridian-veil"));
                var replay = new AuthoritativeReplayDocument { Version = replayVersion };
                int guard = 0;
                while ((game.Active.Side != Side.Red || game.Active.Kind != FormationKind.Submarine) && guard++ < 20)
                    AuthoritativeReplay.Record(replay, game, new AuthoritativeCommand { Side = game.Active.Side, ActorId = game.Active.Id, Action = ActionKind.Hold });
                FormationState leader = game.Active;
                ContactState contact = game.Contacts.First(item => item.Owner == Side.Red && game.Find(item.TargetId)?.Kind == FormationKind.CarrierGroup);
                FormationState second = game.Formations.First(item => item.Side == Side.Red && item.Kind == FormationKind.BallisticMissileBattery);
                var declaration = new AuthoritativeCommand { Kind = AuthoritativeCommandKind.DeclareSynchronizedStrike, Side = leader.Side, ActorId = leader.Id,
                    TargetId = contact.TargetId, Q = contact.LastKnownPosition.Q, R = contact.LastKnownPosition.R, Salvo = Salvo.Standard, WeaponDelivery = WeaponDelivery.Ballistic,
                    ParticipantIds = new List<string> { leader.Id, second.Id } };
                Check(AuthoritativeReplay.Record(replay, game, declaration).Accepted, "Ballistic synchronization declaration accepted");
                PrototypeGame restored = RoundTrip(game);
                Check(AuthoritativeReplay.StateDigest(game) == AuthoritativeReplay.StateDigest(restored), "Synchronization declaration round trip preserves state");
                while (game.ReadySynchronizedStrikeFor(game.Active) == null && guard++ < 40)
                {
                    var hold = new AuthoritativeCommand { Side = game.Active.Side, ActorId = game.Active.Id, Action = ActionKind.Hold };
                    AuthoritativeReplay.Record(replay, game, hold);
                    Check(AuthoritativeCommandProcessor.Execute(restored, hold, out _), "Reloaded synchronization advances to the same boundary");
                }
                SynchronizedStrikeState strike = game.ReadySynchronizedStrikeFor(game.Active);
                Check(strike != null, "Synchronized strike reaches resolution boundary");
                var operation = new OperationSave { Game = game.CaptureState(), HasPendingReaction = true,
                    PendingReaction = new PendingReactionSave { ActorId = game.Active.Id, TargetId = contact.TargetId, ContactTargetId = contact.TargetId,
                        Aim = strike.Aim, Salvo = Salvo.Standard, Delivery = WeaponDelivery.Ballistic, SynchronizedStrikeId = strike.Id } };
                RestoredOperation pending = OperationSaveStore.Decode(JsonUtility.ToJson(operation));
                Check(pending.Save.PendingReaction?.SynchronizedStrikeId == strike.Id, "Pending synchronized defender choice persists");
                var resolve = new AuthoritativeCommand { Kind = AuthoritativeCommandKind.ResolveSynchronizedStrike, Side = game.Active.Side, ActorId = game.Active.Id,
                    EventId = strike.Id, Reaction = Reaction.Hold };
                Check(AuthoritativeReplay.Record(replay, game, resolve).Accepted && AuthoritativeCommandProcessor.Execute(pending.Game, resolve, out _), "Both synchronized reactions resolve");
                Check(AuthoritativeReplay.StateDigest(game) == AuthoritativeReplay.StateDigest(pending.Game), "Synchronized reload preserves sequential interception and combat rolls");
                string after = AuthoritativeReplay.StateDigest(pending.Game);
                Check(!AuthoritativeCommandProcessor.Execute(pending.Game, resolve, out _) && after == AuthoritativeReplay.StateDigest(pending.Game), "Resolved volley cannot spend ammunition twice");
                Check(AuthoritativeReplay.Replay(replay).Success, "End-to-end ballistic synchronization replay v" + replayVersion);
            }
        }

        private static void TestNarrowsShield()
        {
            ScenarioDefinition scenario = ScenarioCatalog.Require("narrows-shield");
            Check(OperationalDataValidator.Validate(scenario).Count == 0 && scenario.Formations.Count == 6 && scenario.Horizon == 12, "Narrows Shield validates as an independent compact operation");
            Check(scenario.Objectives.Select(item => item.Kind).OrderBy(item => item).SequenceEqual(new[] { ScenarioObjectiveKind.Control, ScenarioObjectiveKind.Escort, ScenarioObjectiveKind.Denial, ScenarioObjectiveKind.Withdrawal }.OrderBy(item => item)), "Narrows Shield uses four executable objectives");
            var game = new PrototypeGame(4200, scenario);
            foreach (Side side in new[] { Side.Blue, Side.Red })
            {
                FormationState carrier = game.Formations.Single(item => item.Side == side && item.Kind == FormationKind.CarrierGroup);
                Check(game.FriendlyBallisticCoverage(side, carrier).First().Kind == FormationKind.SurfaceGroup, "Opening escort protects " + side + " carrier");
            }
            Check(!scenario.LogisticsRegions.Any(item => item.SupportsBallisticReload || item.SupportsInterceptorReload), "Finite scenario stocks cannot be refilled");
            ScenarioDefinition original = ScenarioCatalog.Require("meridian-veil");
            Check(original.Horizon == 16 && original.Formations.Count == 11 && ScenarioCatalog.MeridianVeil().Formations.All(item => item.BallisticWeapons.Capacity == 0), "Original live operation and conventional compiled template remain intact");
        }

        private static PrototypeGame RoundTrip(PrototypeGame game)
            => OperationSaveStore.Decode(JsonUtility.ToJson(new OperationSave { Game = game.CaptureState() })).Game;
        private static void Check(bool condition, string label) => TestChecks.Check(condition, label);
    }
}
#endif
