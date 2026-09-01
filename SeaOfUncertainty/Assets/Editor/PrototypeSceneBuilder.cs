#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using SeaOfUncertainty.Core;
using SeaOfUncertainty.Prototype;

namespace SeaOfUncertainty.Editor
{
    public static class PrototypeSceneBuilder
    {
        private const string RuntimePanelPath = "Assets/Resources/UI/SeaRuntimePanelSettings.asset";

        [MenuItem("Sea of Uncertainty/Rebuild Prototype Scene")]
        public static void Build()
        {
            EnsureRuntimePanelSettings();
            Ensure3DMaterials();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Prototype Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.01f, .03f, .05f);
            camera.orthographic = true;
            cameraObject.tag = "MainCamera";
            const string path = "Assets/Scenes/Prototype.unity";
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, path);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(path, true) };
            PlayerSettings.productName = "Sea of Uncertainty";
            PlayerSettings.companyName = "Sea of Uncertainty Design Lab";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            AssetDatabase.SaveAssets();
            Debug.Log("Sea of Uncertainty prototype scene rebuilt.");
        }

        public static void BuildWindows()
        {
            Build();
            System.IO.Directory.CreateDirectory("Builds/Windows");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Prototype.unity" },
                locationPathName = "Builds/Windows/SeaOfUncertainty.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new System.Exception("Prototype build failed: " + report.summary.result);
            Debug.Log("Sea of Uncertainty Windows prototype built successfully.");
        }

        public static void RunEntropyRevealRegressionTests()
        {
            var game = new PrototypeGame(1978);
            FormationState player = game.Active;
            FormationState opponent = game.Formations.First(formation => formation.Side != player.Side);
            EntropyEffectDefinition playerCard = game.MarkEntropy(player, EntropySource.Friction);
            EntropyEffectDefinition opponentCard = game.MarkEntropy(opponent, EntropySource.Friction);
            Assert(playerCard != null && opponentCard != null, "Both sides draw independent Friction cards");
            Assert(game.PendingEntropyEffectFor(player.Side)?.Id == playerCard.Id, "The player's reveal remains queued after an opposing draw");
            Assert(game.PendingEntropyEffectFor(opponent.Side)?.Id == opponentCard.Id, "The opposing reveal remains independently queued");
            game.ConsumePendingEntropyEffect(opponent.Side);
            Assert(game.PendingEntropyEffectFor(player.Side)?.Id == playerCard.Id, "Consuming the opposing reveal cannot consume the player's reveal");

            string saveJson = JsonUtility.ToJson(game.CaptureState());
            var restored = new PrototypeGame(1978);
            restored.RestoreState(JsonUtility.FromJson<PrototypeGame.SaveData>(saveJson));
            Assert(restored.PendingEntropyEffectFor(player.Side)?.Id == playerCard.Id, "The pending player reveal survives save and load");
            Debug.Log("Entropy reveal regression tests passed.");
        }

        public static void RunCardExpansionTests()
        {
            Assert(EntropyEffectCatalog.All.Count == 36, "The Entropy catalog contains all 36 cards");
            foreach (EntropySource source in System.Enum.GetValues(typeof(EntropySource))) Assert(EntropyEffectCatalog.For(source).Count() == 12, source + " contains 12 cards");
            Assert(CommandResponseCatalog.All.Count == 24 && CommandResponseCatalog.All.Select(card => card.Id).Distinct().Count() == 24, "The Command Response catalog contains 24 unique cards");

            var game = new PrototypeGame(1978);
            Assert(game.EntropyDecks.All(deck => deck.DrawPile.Count == 12), "Each Entropy source deck begins with 12 cards");
            Assert(game.CommandResponseDecks.Count == 2 && game.CommandResponseDecks.All(deck => deck.Hand.Count == 3 && deck.DrawPile.Count == 21), "Each side begins with three private Responses");

            var missionEventGame = new PrototypeGame(1978);
            ActionKind? completedMission = null;
            missionEventGame.ActionCompleted += (formation, mission) => completedMission = mission;
            Assert(missionEventGame.Hold(missionEventGame.Active, out string heldMessage) && completedMission == ActionKind.Hold, "Completing a mission emits its distinct audio cue signal: " + heldMessage);

            var stackGame = new PrototypeGame(1978);
            FormationState stackFormation = stackGame.Active;
            EntropyEffectDefinition firstStackCard = stackGame.MarkEntropy(stackFormation, EntropySource.Friction);
            EntropyEffectDefinition secondStackCard = stackGame.MarkEntropy(stackFormation, EntropySource.Friction);
            Assert(firstStackCard != null && secondStackCard != null && firstStackCard.Id != secondStackCard.Id && stackFormation.ActiveEffectCardIds.Count == 2, "Repeated Friction events draw and stack distinct physical cards");
            Assert(stackGame.Recover(stackFormation, out string stackedRecovery) && stackFormation.Friction && stackFormation.ActiveEffectCardIds.Count == 1, "Recover discards one stacked card while the remaining card keeps its source marked: " + stackedRecovery);

            FormationState actor = game.Active;
            actor.ActiveEffectCardIds.Add("F-07");
            Assert(Rules.MoveAllowance(actor, MoveMode.Normal) == 1, "Navigation Drift reduces the first normal Move by one hex");
            actor.ActiveEffectCardIds.Add("D-08");
            Assert(!game.SearchArea(actor, actor.Position, SearchMode.Focused, out string jammedMessage) && jammedMessage.Contains("Jammed Circuits"), "Jammed Circuits blocks Focused Search");
            actor.ActiveEffectCardIds.Add("X-11");
            Assert(!actor.CanHeavySalvo, "Launcher Damage blocks Heavy Salvo");

            CommandResponseDeckState hand = game.CommandResponseDecks.First(deck => deck.Side == actor.Side);
            hand.DrawPile.Remove("C-22");
            if (!hand.Hand.Contains("C-22")) hand.Hand.Add("C-22");
            int command = actor.EffectiveCommand;
            Assert(game.PlayCommandResponse(actor.Side, "C-22", actor, null, null, out string responseMessage) && actor.EffectiveCommand == command + 1, "Local Initiative applies +1 Command: " + responseMessage);
            Assert(hand.DiscardPile.Contains("C-22") && !hand.Hand.Contains("C-22"), "A played Response moves to discard");

            var replanGame = new PrototypeGame(1978);
            FormationState replanned = replanGame.Active;
            CommandResponseDeckState replanHand = replanGame.CommandResponseDecks.First(deck => deck.Side == replanned.Side);
            replanHand.DrawPile.Remove("C-04");
            if (!replanHand.Hand.Contains("C-04")) replanHand.Hand.Add("C-04");
            ActionKind newMission = replanned.Mission == ActionKind.Strike ? ActionKind.Search : ActionKind.Strike;
            int commandBeforeReplan = replanGame.Sides[replanned.Side].CommandSlots;
            Assert(replanGame.PlayCommandResponse(replanned.Side, "C-04", replanned, null, null, newMission, out string replanMessage), "Rapid Replan resolves: " + replanMessage);
            Assert(replanned.Mission == newMission && replanned.NextReadyTimeBonus == 1 && replanGame.Sides[replanned.Side].CommandSlots == commandBeforeReplan, "Rapid Replan changes Mission, adds next-Ready cost, and occupies no Command Slot");

            var withdrawalGame = new PrototypeGame(1978);
            FormationState withdrawalAttacker = withdrawalGame.Active;
            FormationState withdrawalTarget = withdrawalGame.Formations.First(candidate => candidate.Side != withdrawalAttacker.Side && candidate.Kind == FormationKind.Submarine);
            withdrawalTarget.Position = new HexCoord(1, 7);
            withdrawalTarget.Ratings.Defense = 20;
            ContactState withdrawalContact = withdrawalGame.ContactFor(withdrawalAttacker.Side, withdrawalTarget.Id);
            if (withdrawalContact == null)
            {
                withdrawalContact = new ContactState { Owner = withdrawalAttacker.Side, TargetId = withdrawalTarget.Id };
                withdrawalGame.Contacts.Add(withdrawalContact);
            }
            withdrawalContact.LastKnownPosition = withdrawalTarget.Position;
            withdrawalContact.Location = LocationQuality.High;
            CommandResponseDeckState withdrawalHand = withdrawalGame.CommandResponseDecks.First(deck => deck.Side == withdrawalTarget.Side);
            withdrawalHand.DrawPile.Remove("C-18");
            if (!withdrawalHand.Hand.Contains("C-18")) withdrawalHand.Hand.Add("C-18");
            Assert(withdrawalGame.PlayCommandResponse(withdrawalTarget.Side, "C-18", withdrawalTarget, null, null, out string withdrawalMessage) && withdrawalTarget.OrderlyWithdrawalReady, "Orderly Withdrawal can be prepared: " + withdrawalMessage);
            HexCoord withdrawalStart = withdrawalTarget.Position;
            int rangeBeforeWithdrawal = HexCoord.Distance(withdrawalAttacker.Position, withdrawalStart);
            Assert(withdrawalGame.Strike(withdrawalAttacker, withdrawalTarget, Salvo.Light, Reaction.Defend, out CombatResult withdrawalResult, out string strikeMessage), "Strike against prepared withdrawal resolves: " + strikeMessage);
            Assert(withdrawalResult.Reaction == Reaction.Evade && withdrawalResult.Withdrew && HexCoord.Distance(withdrawalStart, withdrawalTarget.Position) <= 2 && HexCoord.Distance(withdrawalAttacker.Position, withdrawalTarget.Position) > rangeBeforeWithdrawal && !withdrawalTarget.OrderlyWithdrawalReady, "Orderly Withdrawal uses Evade, moves up to two hexes away, and is consumed");

            string json = JsonUtility.ToJson(game.CaptureState());
            var restored = new PrototypeGame(1978);
            restored.RestoreState(JsonUtility.FromJson<PrototypeGame.SaveData>(json));
            Assert(restored.CommandResponseDecks.SelectMany(deck => deck.Hand).SequenceEqual(game.CommandResponseDecks.SelectMany(deck => deck.Hand)), "Response hands survive save and load");
            Assert(restored.Find(actor.Id).ActiveEffectCardIds.Contains("X-11"), "Expanded Entropy attachments survive save and load");
            Debug.Log("Card expansion tests passed: 36 Entropy cards, 24 Command Responses, decks, supported mechanics, and save/load.");
        }

        public static void RunCoreSmokeTests()
        {
            EnsureRuntimePanelSettings();
            Ensure3DMaterials();
            ThemeStyleSheet runtimeTheme = Resources.Load<ThemeStyleSheet>("UI/UnityDefaultRuntimeTheme");
            Assert(runtimeTheme != null, "UI Toolkit runtime theme is packaged");
            PanelSettings runtimePanel = AssetDatabase.LoadAssetAtPath<PanelSettings>(RuntimePanelPath);
            Assert(runtimePanel != null && runtimePanel.themeStyleSheet == runtimeTheme, "Runtime panel has its packaged theme assigned");
            Assert(Rules.MoveDistance(MoveMode.Cautious) == 1, "Cautious movement");
            Assert(Rules.MoveDistance(MoveMode.HighTempo) == 3, "High Tempo movement");
            Assert(Rules.SearchTarget(0) == 5 && Rules.SearchTarget(5) == 1, "Search bands");
            Assert(Rules.BandFor(-2) == CombatBand.Poor, "Poor combat boundary");
            Assert(Rules.BandFor(-1) == CombatBand.Even, "Even combat boundary");
            Assert(Rules.BandFor(2) == CombatBand.Favorable, "Favorable combat boundary");
            Assert(Rules.BandFor(4) == CombatBand.Dominant, "Dominant combat boundary");
            Assert(Rules.DamageFor(CombatBand.Dominant, 6) == DamageState.Destroyed, "Dominant maximum result");
            Assert(HexCoord.Distance(new HexCoord(0, 0), new HexCoord(0, 1)) == 1, "Adjacent hex distance");
            Assert(Rules.MoveDistance(FormationKind.AirGroup, MoveMode.Cautious) == 4, "Air mission cautious radius");
            Assert(Rules.MoveDistance(FormationKind.AirGroup, MoveMode.HighTempo) == 8, "Air mission high-tempo radius");
            Assert(Rules.SearchRange(SearchMode.Passive) == 8 && Rules.SearchRange(SearchMode.Focused) == 12, "20 nm Search boundaries");
            Assert(Rules.SearchAreaRadius == 1, "Search covers the selected hex and its six adjacent hexes");
            Assert(Rules.StrikeRange(FormationKind.SurfaceGroup, Salvo.Standard) == 6, "Surface standard Strike boundary");
            Assert(Rules.StrikeRange(FormationKind.AirGroup, Salvo.Heavy) == 14, "Air heavy Strike boundary");
            Assert(Rules.InterceptionRange == 1, "Interception range is one 20 nm hex");
            Assert(EntropyEffectCatalog.All.Count == 36 && EntropyEffectCatalog.For(EntropySource.Friction).Count() == 12 && EntropyEffectCatalog.For(EntropySource.Disruption).Count() == 12 && EntropyEffectCatalog.For(EntropySource.Destruction).Count() == 12, "All 36 prototype Entropy effect cards are structured game data");
            Assert(CommandResponseCatalog.All.Count == 24 && CommandResponseCatalog.All.Select(card => card.Id).Distinct().Count() == 24, "All 24 Command Response cards are structured game data");

            var responseGame = new PrototypeGame(1978);
            Assert(responseGame.CommandResponseDecks.Count == 2 && responseGame.CommandResponseDecks.All(deck => deck.Hand.Count == 3 && deck.DrawPile.Count == 21), "Each side begins with a deterministic three-card Response hand");
            CommandResponseDeckState blueResponses = responseGame.CommandResponseDecks.First(deck => deck.Side == Side.Blue);
            blueResponses.DrawPile.Remove("C-22");
            if (!blueResponses.Hand.Contains("C-22")) blueResponses.Hand.Add("C-22");
            FormationState responseFormation = responseGame.Active.Side == Side.Blue ? responseGame.Active : responseGame.Formations.First(formation => formation.Side == Side.Blue);
            int baseCommand = responseFormation.EffectiveCommand;
            Assert(responseGame.PlayCommandResponse(Side.Blue, "C-22", responseFormation, null, null, out string responseMessage) && responseFormation.EffectiveCommand == baseCommand + 1, "A supported Command Response resolves and discards: " + responseMessage);
            Assert(!blueResponses.Hand.Contains("C-22") && blueResponses.DiscardPile.Contains("C-22"), "Played Command Response leaves the hand for the discard pile");

            var cardGame = new PrototypeGame(1978);
            FormationState cardFormation = cardGame.Active;
            int frictionDrawCount = cardGame.EntropyDecks.First(deck => deck.Source == EntropySource.Friction).DrawPile.Count;
            EntropyEffectDefinition frictionCard = cardGame.MarkEntropy(cardFormation, EntropySource.Friction);
            Assert(frictionCard != null && cardFormation.Friction && cardFormation.ActiveEffectCardIds.Contains(frictionCard.Id), "New Friction draws and attaches one matching effect card");
            Assert(cardGame.PendingEntropyEffectFor(cardFormation.Side)?.Id == frictionCard.Id && cardGame.PendingEntropyFormationFor(cardFormation.Side) == cardFormation, "A card pull queues a reveal for its owning side");
            EntropyEffectDefinition stackedFrictionCard = cardGame.MarkEntropy(cardFormation, EntropySource.Friction);
            Assert(stackedFrictionCard != null && cardFormation.ActiveEffectCardIds.Count == 2, "A repeated Entropy event draws and stacks another physical card");
            Assert(cardGame.EntropyDecks.First(deck => deck.Source == EntropySource.Friction).DrawPile.Count == frictionDrawCount - 2, "Each pull advances the deterministic source deck");
            FormationState opposingFormation = cardGame.Formations.First(formation => formation.Side != cardFormation.Side);
            EntropyEffectDefinition opposingCard = cardGame.MarkEntropy(opposingFormation, EntropySource.Friction);
            Assert(opposingCard != null && cardGame.PendingEntropyEffectFor(opposingFormation.Side)?.Id == opposingCard.Id, "The opposing side receives its own independent reveal");
            cardGame.ConsumePendingEntropyEffect(opposingFormation.Side);
            Assert(cardGame.PendingEntropyEffectFor(cardFormation.Side)?.Id == frictionCard.Id, "Acknowledging an opposing draw cannot overwrite or consume the player's queued reveal");
            string queuedSave = JsonUtility.ToJson(cardGame.CaptureState());
            var restoredCardGame = new PrototypeGame(1978);
            restoredCardGame.RestoreState(JsonUtility.FromJson<PrototypeGame.SaveData>(queuedSave));
            Assert(restoredCardGame.PendingEntropyEffectFor(cardFormation.Side)?.Id == frictionCard.Id, "Pending card reveals survive save and load");
            cardGame.ConsumePendingEntropyEffect(cardFormation.Side);
            Assert(cardGame.Recover(cardFormation, out string cardRecoveryMessage) && cardFormation.Friction && cardFormation.ActiveEffectCardIds.Count == 1, "Recover discards one stacked card and retains the marked source while another remains: " + cardRecoveryMessage);
            var modifierProbe = new FormationState { Kind = FormationKind.CarrierGroup, Ratings = new Ratings { Move = 3, Search = 3, Strike = 3, Defense = 3, Command = 3 }, ActiveEffectCardIds = new List<string> { "X-01", "X-02", "X-03", "X-04", "X-05", "X-06" } };
            Assert(modifierProbe.EffectiveMove == 2 && modifierProbe.EffectiveSearch == 2 && modifierProbe.EffectiveStrike == 2 && modifierProbe.EffectiveDefense == 2 && modifierProbe.EffectiveCommand == 2 && !modifierProbe.CanHeavySalvo, "Supported Destruction cards modify formation capabilities");

            var exactTie = new List<FormationState>
            {
                new FormationState { Id = "B-TEST", Side = Side.Blue, ReadyTime = 0, Ratings = new Ratings { Command = 9 } },
                new FormationState { Id = "R-TEST", Side = Side.Red, ReadyTime = 0, Ratings = new Ratings { Command = 0 } }
            };
            Assert(Rules.NextReady(exactTie).Side == Side.Blue, "Unseeded exact tie uses formation quality and stable ID");
            Assert(Rules.NextReady(exactTie, Side.Blue).Side == Side.Red, "Cross-side exact tie passes priority away from the most recent acting side");

            var game = new PrototypeGame();
            Assert(game.Formations.Exists(f => f.Side == Side.Blue && f.ReadyTime == 0) && game.Formations.Exists(f => f.Side == Side.Red && f.ReadyTime == 0), "Both sides have formations in the opening Ready-Time cohort");
            Assert(game.Formations.Exists(f => f.Side == Side.Blue && f.ReadyTime == 1) && game.Formations.Exists(f => f.Side == Side.Red && f.ReadyTime == 1), "Both sides have interleaved follow-on readiness");
            var cadenceGame = new PrototypeGame();
            var cadence = new List<Side>();
            for (int i = 0; i < 8; i++)
            {
                cadence.Add(cadenceGame.Active.Side);
                Assert(cadenceGame.Hold(cadenceGame.Active, out string cadenceMessage), "Continuous activation cadence action: " + cadenceMessage);
            }
            for (int i = 1; i < cadence.Count; i++) Assert(cadence[i] != cadence[i - 1], "Opening Ready-Time cadence alternates only where both sides are tied");
            Assert(game.Area.Width == 12 && game.Area.Height == 10, "Data-driven Meridian Veil dimensions");
            Assert(game.Area.NauticalMilesPerHex == 20, "Operational scale is 20 nautical miles per hex");
            Assert(game.Area.NauticalMiles(new HexCoord(0, 0), new HexCoord(0, 3)) == 60, "Hex range converts to nautical miles");
            Assert(game.Area.ReadyTimeHours == 2f, "Ready-Time point converts to two hours");
            Assert(OperationalDataValidator.Validate(game.Scenario).Count == 0, "Operational-area and scenario validation");
            Assert(ScenarioCatalog.All().Count == 2, "Scenario catalog exposes both MVP theaters");
            ScenarioDefinition luzon = ScenarioCatalog.LuzonStrait();
            ScenarioDefinition migrationSample = ScenarioCatalog.LuzonStrait();
            migrationSample.SchemaVersion = 1; migrationSample.Area.SchemaVersion = 1; migrationSample.Area.RestrictedAreas = null;
            OperationalDataMigration.Migrate(migrationSample);
            Assert(migrationSample.SchemaVersion == OperationalDataMigration.CurrentSchemaVersion && migrationSample.Area.SchemaVersion == OperationalDataMigration.CurrentSchemaVersion && migrationSample.Area.RestrictedAreas != null, "Operational-area and scenario v1 data migrate to the current schema");
            Assert(luzon.Area.Width == 24 && luzon.Area.Height == 20 && luzon.Area.NauticalMilesPerHex == 20, "Luzon pilot dimensions and scale");
            Assert(luzon.Horizon == 24 && Mathf.Approximately(luzon.Horizon * luzon.Area.ReadyTimeHours, 48f), "Luzon scenario is a 48-hour operation");
            Assert(OperationalDataValidator.Validate(luzon).Count == 0, "Luzon operational-area and scenario validation");
            ScenarioDefinition invalidData = ScenarioCatalog.LuzonStrait();
            invalidData.Area.Presentation.TerrainAssetSet = string.Empty;
            invalidData.Area.Locations.Add(invalidData.Area.Locations[0]);
            invalidData.DeploymentRegions.Add(new OperationalRegionDefinition { Id = "unreachable", Name = "Unreachable", Hexes = new System.Collections.Generic.List<HexCoord> { new HexCoord(0, 0) } });
            string validationReport = string.Join(" | ", OperationalDataValidator.Validate(invalidData));
            Assert(validationReport.Contains("Presentation assets") && validationReport.Contains("duplicated") && validationReport.Contains("cannot reach"), "Validator reports missing assets, duplicate IDs, and unreachable setup zones");
            OperationalLocationDefinition basco = luzon.Area.Locations.Find(location => location.Id == "basco");
            OperationalLocationDefinition aparri = luzon.Area.Locations.Find(location => location.Id == "aparri");
            double geographicNm = Geodesy.NauticalMiles(basco.Geographic, aparri.Geographic);
            int logicalNm = luzon.Area.NauticalMiles(basco.Hex, aparri.Hex);
            Assert(System.Math.Abs(geographicNm - logicalNm) / geographicNm < .2, "Luzon geographic and hex distances agree within 20 percent");
            OperationalLocationDefinition bashi = luzon.Area.Locations.Find(location => location.Id == "bashi-channel");
            OperationalLocationDefinition balintang = luzon.Area.Locations.Find(location => location.Id == "balintang-channel");
            Assert(System.Math.Abs(Geodesy.NauticalMiles(bashi.Geographic, balintang.Geographic) - luzon.Area.NauticalMiles(bashi.Hex, balintang.Hex)) / Geodesy.NauticalMiles(bashi.Geographic, balintang.Geographic) < .2, "North-south channel distance agrees within 20 percent");
            Assert(System.Math.Abs(Geodesy.NauticalMiles(bashi.Geographic, basco.Geographic) - luzon.Area.NauticalMiles(bashi.Hex, basco.Hex)) / Geodesy.NauticalMiles(bashi.Geographic, basco.Geographic) < .2, "Bashi-to-Basco distance agrees within 20 percent");
            using (var luzonMap = new OperationalMap3D(luzon.Area, 480, 270))
            {
                Assert(luzonMap.CoastlinePolygonCount >= 50, "Luzon renderer uses the local Natural Earth polygon coastline library");
                Assert(luzonMap.OffMapCoastlineVertexCount >= 100, "Authentic Natural Earth land geometry continues beyond the playable projection to the table boundary");
                Assert(luzonMap.HasDirectionalSun, "3D theater has a directional maritime sun");
                Assert(Mathf.Approximately(luzonMap.SunSourceAzimuthDegrees, 67.5f), "Maritime sun illuminates the theater from east-northeast");
                Assert(luzonMap.UsesProceduralSurfaceTextures, "Land, littoral, and ocean use procedural surface textures");
                Assert(luzonMap.WaterSurfaceVertexCount >= 4000, "Ocean surface has enough geometry for restrained wave relief");
                Assert(luzonMap.TerrainReliefCount >= 2, "Major polygon land masses receive coastline-aligned dimensional relief");
                Assert(luzonMap.HasShallowWaterDetail && luzonMap.HasCoastalFoam && luzonMap.HasCoastlineDrivenShelf, "Coastline-driven shelves, bathymetric contours, and coastal foam enrich the sea-land transition");
                Assert(luzonMap.HasAtmosphericHaze && luzonMap.CloudShadowCount >= 2 && luzonMap.WeatherPreset == "Haze", "Data-driven haze and moving cloud-shadow layers establish maritime atmosphere");
                Assert(luzonMap.GeographicLabelCount == luzon.Area.Locations.Count, "Ports, airfields, straits, and objectives receive map-space geographic labels");
                float renderLuminance = luzonMap.ProbeRenderLuminance();
                Assert(renderLuminance > .01f && renderLuminance < .95f, "The integrated ocean, terrain, atmosphere, and label camera produces a valid non-black render");
                Vector3 edge = luzonMap.HexToWorld(new HexCoord(23, 19));
                Assert(edge.x > 0f && edge.z > 0f, "Floating-origin world coordinates center the theater");
                Assert(luzonMap.TryWorldToHex(luzonMap.HexToWorld(new HexCoord(11, 9)), out HexCoord roundTrip) && roundTrip.Equals(new HexCoord(11, 9)), "Hex/world conversion round trip");
                Assert(luzonMap.TryWorldToHex(luzonMap.HexToWorld(new HexCoord(23, 19)), out HexCoord edgeRoundTrip) && edgeRoundTrip.Equals(new HexCoord(23, 19)), "Edge hex selection round trip");
                luzonMap.Pan(new Vector2(100000f, -100000f));
                Assert(luzonMap.FocusWithinBounds, "Camera pan remains inside theater bounds");
            }
            using (var operationalMap = new OperationalMap3D(game.Area, 320, 180))
            {
                float adjacentWorldDistance = Vector3.Distance(operationalMap.HexToWorld(new HexCoord(0, 0)), operationalMap.HexToWorld(new HexCoord(0, 1)));
                Assert(Mathf.Approximately(adjacentWorldDistance, Mathf.Sqrt(3f)), "3D world coordinates preserve adjacent hex spacing");
                operationalMap.SetState(game, ToolkitActionMode.Move, MoveMode.Normal, SearchMode.Passive, Salvo.Standard);
                operationalMap.SetHoverHex(new HexCoord(2, 3));
                operationalMap.SetState(game, ToolkitActionMode.Search, MoveMode.Normal, SearchMode.Active, Salvo.Standard);
                int baselineContactCount = game.Contacts.Count;
                string enemyId = game.Formations.Find(formation => formation.Side != game.Active.Side).Id;
                game.Contacts.Add(new ContactState { Owner = game.Active.Side, TargetId = enemyId, LastKnownPosition = new HexCoord(4, 3), Location = LocationQuality.High, Identity = IdentityQuality.Identified, Age = 0 });
                game.Contacts.Add(new ContactState { Owner = game.Active.Side, TargetId = enemyId, LastKnownPosition = new HexCoord(5, 3), Location = LocationQuality.Low, Identity = IdentityQuality.Unknown, Age = 2 });
                game.Contacts.Add(new ContactState { Owner = game.Active.Side, TargetId = enemyId, LastKnownPosition = new HexCoord(6, 3), Location = LocationQuality.Medium, Identity = IdentityQuality.General, Age = 3 });
                game.Contacts.Add(new ContactState { Owner = game.Active.Side, TargetId = enemyId, LastKnownPosition = new HexCoord(7, 3), Location = LocationQuality.Low, Identity = IdentityQuality.Unknown, Age = 3, IsFalse = true });
                game.Contacts.Add(new ContactState { Owner = game.Active.Side, TargetId = enemyId, LastKnownPosition = new HexCoord(8, 3), Location = LocationQuality.Low, Identity = IdentityQuality.Unknown, Age = 4, IsLost = true });
                operationalMap.SetState(game, ToolkitActionMode.Strike, MoveMode.Normal, SearchMode.Active, Salvo.Standard);
                Assert(operationalMap.HasActiveFormationPulse, "The active formation has a dedicated pulse ring");
                Assert(operationalMap.VisibleFormationCount == game.Formations.FindAll(formation => formation.Side == game.Active.Side && !formation.IsDestroyed).Count, "3D view instantiates only the active side's friendly formations");
                Assert(operationalMap.FormationMeshVariantCount >= 2 && operationalMap.ContainsRenderedName("Tapered Hull"), "Close formation models use reusable tapered naval meshes instead of stretched-cube hull blockouts");
                Assert(operationalMap.ContainsRenderedName("Swept Wing") && operationalMap.ContainsRenderedName("Hydrodynamic Pressure Hull"), "Air-group and submarine silhouettes remain recognizable by geometry");
                Assert(operationalMap.VisibleContactCount == game.Contacts.FindAll(contact => contact.Owner == game.Active.Side && !contact.IsLost).Count, "3D view instantiates only the active side's Contacts");
                FormationState hiddenEnemy = game.Formations.Find(formation => formation.Side != game.Active.Side);
                Assert(!operationalMap.ContainsRenderedName(hiddenEnemy.Name) && !operationalMap.ContainsRenderedName(hiddenEnemy.Id), "3D scene hierarchy does not expose a hidden enemy identity");
                int secureFormationCount = operationalMap.VisibleFormationCount, secureContactCount = operationalMap.VisibleContactCount;
                operationalMap.Zoom(-1f); operationalMap.Zoom(-1f); operationalMap.Zoom(1f);
                Assert(operationalMap.VisibleFormationCount == secureFormationCount && operationalMap.VisibleContactCount == secureContactCount && !operationalMap.ContainsRenderedName(hiddenEnemy.Name), "Zoom level cannot change or reveal authoritative information");
                foreach (OperationalEffectKind kind in System.Enum.GetValues(typeof(OperationalEffectKind))) operationalMap.TriggerEffect(kind, game.Active.Position);
                Assert(operationalMap.ActiveEffectCount >= 7, "Restrained prototype effect library covers all MVP effect categories");
                operationalMap.SetState(game, ToolkitActionMode.None, MoveMode.Normal, SearchMode.Passive, Salvo.Standard, true);
                Assert(operationalMap.ActiveEffectCount == 0 && operationalMap.PooledEffectCount >= 7, "Transient effects return to the object pool");
                Assert(!operationalMap.PermanentGridVisible, "Permanent hex grid is optional");
                operationalMap.SetState(game, ToolkitActionMode.None, MoveMode.Normal, SearchMode.Passive, Salvo.Standard, false, true);
                Assert(operationalMap.PermanentGridVisible, "Optional permanent hex grid can be enabled");
                operationalMap.SetState(game, ToolkitActionMode.None, MoveMode.Normal, SearchMode.Passive, Salvo.Standard, false, false);
                Assert(!operationalMap.PermanentGridVisible, "Optional permanent hex grid can be disabled");
                game.Contacts.Add(new ContactState { Owner = game.Active.Side, TargetId = "FALSE-TEST", LastKnownPosition = new HexCoord(4, 4), Location = LocationQuality.Low, Identity = IdentityQuality.Unknown, Age = 3, IsFalse = true });
                operationalMap.SetState(game, ToolkitActionMode.Search, MoveMode.Normal, SearchMode.Focused, Salvo.Standard);
                game.Contacts.RemoveAll(contact => contact.TargetId == "FALSE-TEST");
                if (game.Contacts.Count > baselineContactCount) game.Contacts.RemoveRange(baselineContactCount, game.Contacts.Count - baselineContactCount);
                operationalMap.SetState(game, ToolkitActionMode.None, MoveMode.Normal, SearchMode.Passive, Salvo.Standard);
                Assert(operationalMap.PooledMarkerCount >= 1, "Formation and Contact marker roots return to the object pool");
                operationalMap.Rotate(1f);
                Assert(Mathf.Approximately(operationalMap.Heading, 30f), "3D command camera uses stepped rotation");
                float orbitPitch = operationalMap.CameraPitch;
                operationalMap.Orbit(new Vector2(45f, 30f));
                Assert(!Mathf.Approximately(operationalMap.Heading, 30f) && operationalMap.CameraPitch < orbitPitch, "Right-drag camera supports continuous orbit and tilt");
                float flyDistance = operationalMap.CameraDistance;
                operationalMap.FlyCamera(1f, 1f, 1f, 1f, -1f, .25f, true);
                Assert(operationalMap.FocusWithinBounds && operationalMap.CameraDistance < flyDistance, "Keyboard camera movement, rotation, tilt, and zoom remain bounded");
                operationalMap.SaveCameraView();
                float bookmarkedHeading = operationalMap.Heading;
                operationalMap.FlyCamera(0f, 0f, 1f, 0f, 0f, .4f);
                Assert(!Mathf.Approximately(operationalMap.Heading, bookmarkedHeading) && operationalMap.RecallCameraView() && Mathf.Approximately(operationalMap.Heading, bookmarkedHeading), "A command-camera view can be saved and recalled");
                operationalMap.FocusHex(game.Area.Objective);
                Assert(operationalMap.FocusWithinBounds, "Active/objective focus commands remain inside theater bounds");
                operationalMap.Pan(new Vector2(20f, -15f));
                operationalMap.Zoom(1f);
                operationalMap.ResetCamera();
                Assert(Mathf.Approximately(operationalMap.Heading, 0f), "3D command camera reset");
            }
            var cameraInputProbe = new TacticalMapElement();
            Assert(cameraInputProbe.SetCameraKey(KeyCode.W, true) && cameraInputProbe.CameraInputActive, "WASD camera input can be routed from the operation screen without map focus");
            cameraInputProbe.SetCameraKey(KeyCode.W, false);
            Assert(!cameraInputProbe.CameraInputActive, "Released global camera keys do not leave movement stuck active");
            cameraInputProbe.SetEdgeScroll(true);
            Assert(cameraInputProbe.EdgeScrollEnabled, "Optional edge scrolling can be enabled independently of WASD movement");
            FormationState actor = game.Active;
            int originalTime = game.Time;
            var destination = new HexCoord(actor.Position.Q, actor.Position.R > 0 ? actor.Position.R - 1 : actor.Position.R + 1);
            Assert(game.Move(actor, destination, MoveMode.Cautious, out string message), "Legal move: " + message);
            Assert(actor.Position.Equals(destination), "Move updates position");
            Assert(actor.ReadyTime > originalTime, "Move schedules readiness");

            var emptySearchGame = new PrototypeGame();
            emptySearchGame.Contacts.Clear();
            FormationState emptySearcher = emptySearchGame.Active;
            int emptySearchReadyBefore = emptySearcher.ReadyTime;
            Assert(emptySearchGame.SearchArea(emptySearcher, emptySearcher.Position, SearchMode.Passive, out string emptySearchMessage), "An empty area is a legal Search: " + emptySearchMessage);
            Assert(emptySearchMessage.Contains("no detections"), "Empty Search reports no detections without exposing hidden formations");
            Assert(!emptySearchMessage.Contains("Tempest") && !emptySearchMessage.Contains("Ember") && !emptySearchMessage.Contains("Hunter"), "Failed Search does not leak enemy identities");
            Assert(emptySearcher.ReadyTime > emptySearchReadyBefore && emptySearchGame.Active != emptySearcher, "Empty Search consumes time and advances to the next Ready formation");

            var discoveryGame = new PrototypeGame();
            discoveryGame.Contacts.Clear();
            FormationState discoverySearcher = discoveryGame.Active;
            FormationState discoveryTarget = discoveryGame.Formations.Find(formation => formation.Side != discoverySearcher.Side);
            discoveryTarget.Position = new HexCoord(discoverySearcher.Position.Q + 1, discoverySearcher.Position.R);
            discoveryTarget.Ratings.Signature = 20;
            Assert(discoveryGame.SearchArea(discoverySearcher, discoverySearcher.Position, SearchMode.Passive, out string discoveryMessage), "Area Search resolves against a hidden formation: " + discoveryMessage);
            Assert(discoveryGame.ContactFor(discoverySearcher.Side, discoveryTarget.Id) != null, "Successful area Search creates a Contact for a target in an adjacent footprint hex");

            var rejectedSearchGame = new PrototypeGame();
            FormationState rejectedSearcher = rejectedSearchGame.Active;
            int rejectedReadyBefore = rejectedSearcher.ReadyTime;
            int focusedCommandBefore = rejectedSearchGame.Sides[rejectedSearcher.Side].CommandSlots;
            Assert(!rejectedSearchGame.SearchArea(rejectedSearcher, new HexCoord(rejectedSearchGame.Area.Width - 1, rejectedSearchGame.Area.Height - 1), SearchMode.Passive, out string rejectedSearchMessage), "Out-of-range area Search is rejected");
            Assert(rejectedSearcher.ReadyTime == rejectedReadyBefore && rejectedSearchGame.Sides[rejectedSearcher.Side].CommandSlots == focusedCommandBefore, "Rejected Search consumes neither time nor Command");

            var focusedSearchGame = new PrototypeGame();
            FormationState focusedSearcher = focusedSearchGame.Active;
            int commandBefore = focusedSearchGame.Sides[focusedSearcher.Side].CommandSlots;
            Assert(focusedSearchGame.SearchArea(focusedSearcher, focusedSearcher.Position, SearchMode.Focused, out string focusedMessage), "Focused area Search resolves: " + focusedMessage);
            Assert(focusedSearchGame.Sides[focusedSearcher.Side].CommandSlots == commandBefore, "Focused Search releases its temporary Command Slot after resolution");

            string saveJson = JsonUtility.ToJson(game.CaptureState());
            var saveData = JsonUtility.FromJson<PrototypeGame.SaveData>(saveJson);
            var restored = new PrototypeGame();
            restored.RestoreState(saveData);
            Assert(restored.Time == game.Time, "Save restores operational Time");
            Assert(restored.Active != null && restored.Active.Id == game.Active.Id, "Save restores active formation");
            Assert(restored.Formations.Count == game.Formations.Count, "Save restores formations");
            Assert(restored.Contacts.Count == game.Contacts.Count, "Save restores Contacts");
            Assert(saveData.Version == 5 && saveData.ScenarioId == "meridian-veil" && saveData.OperationalAreaId == "meridian-veil-archipelago", "Version 5 save preserves stable IDs, Entropy decks, and Response hands");
            Assert(restored.EntropyDecks.Count == 3 && restored.EntropyDecks.Sum(deck => deck.DrawPile.Count + deck.DiscardPile.Count) == game.EntropyDecks.Sum(deck => deck.DrawPile.Count + deck.DiscardPile.Count), "Save restores Entropy deck state");
            Assert(restored.CommandResponseDecks.Count == 2 && restored.CommandResponseDecks.All(deck => deck.Hand.Count == 3), "Save restores both private Command Response hands");
            Assert(saveData.HasLastActingSide && restored.LastActingSide == saveData.LastActingSide, "Save restores continuous-activation tie priority");
            saveData.Version = 1; saveData.ScenarioId = null; saveData.OperationalAreaId = null;
            new PrototypeGame().RestoreState(saveData);
            bool mismatchRejected = false;
            try { new PrototypeGame(1978, luzon).RestoreState(JsonUtility.FromJson<PrototypeGame.SaveData>(saveJson)); }
            catch (System.ArgumentException exception) { mismatchRejected = exception.Message.Contains("does not match"); }
            Assert(mismatchRejected, "Incompatible scenario saves fail with a clear message");

            var completeScenario = new PrototypeGame();
            int completionGuard = 0;
            while (completeScenario.Time < completeScenario.Scenario.Horizon && completeScenario.Active != null && completionGuard++ < 200)
                Assert(completeScenario.Hold(completeScenario.Active, out string holdMessage), "Grid-free scenario simulation action: " + holdMessage);
            Assert(completeScenario.Time >= completeScenario.Scenario.Horizon, "A complete scenario can resolve without a permanent grid");

            var aiGame = new PrototypeGame(1978, luzon);
            int aiGuard = 0;
            while (aiGame.Time < aiGame.Scenario.Horizon && aiGame.Active != null && aiGuard++ < 300)
            {
                FormationState aiActor = aiGame.Active;
                AiDecision decision = PrototypeAiCommander.Choose(aiGame);
                Assert(decision != null, "AI produces a decision for every Ready formation");
                Assert(PrototypeAiCommander.Execute(aiGame, decision, out string aiMessage), $"AI decision is legal ({decision?.Action}): {aiMessage}");
                Assert(aiActor.ReadyTime > aiGame.Time || aiGame.Active != aiActor, "AI action schedules or advances the Ready formation");
            }
            Assert(aiGame.Time >= aiGame.Scenario.Horizon, "AI commander can complete a full 20 nm scenario without stalling");

            var extendedStrikeGame = new PrototypeGame();
            FormationState extendedStrikeActor = extendedStrikeGame.Active;
            FormationState extendedStrikeTarget = extendedStrikeGame.Formations.Find(formation => formation.Side != extendedStrikeActor.Side);
            extendedStrikeGame.Contacts.Clear();
            HexCoord extendedFix = extendedStrikeActor.Position;
            bool foundExtendedFix = false;
            for (int q = 0; q < extendedStrikeGame.Area.Width && !foundExtendedFix; q++) for (int r = 0; r < extendedStrikeGame.Area.Height && !foundExtendedFix; r++)
            {
                var candidate = new HexCoord(q, r);
                int candidateRange = HexCoord.Distance(extendedStrikeActor.Position, candidate);
                if (candidateRange > Rules.StrikeRange(extendedStrikeActor.Kind, Salvo.Standard) && candidateRange <= Rules.StrikeRange(extendedStrikeActor.Kind, Salvo.Heavy)) { extendedFix = candidate; foundExtendedFix = true; }
            }
            extendedStrikeGame.Contacts.Add(new ContactState { Owner = extendedStrikeActor.Side, TargetId = extendedStrikeTarget.Id, LastKnownPosition = extendedFix, Location = LocationQuality.High, Identity = IdentityQuality.Identified, Age = 0 });
            AiDecision extendedStrike = PrototypeAiCommander.Choose(extendedStrikeGame);
            int extendedRange = HexCoord.Distance(extendedStrikeActor.Position, extendedFix);
            Assert(foundExtendedFix && extendedRange > Rules.StrikeRange(extendedStrikeActor.Kind, Salvo.Standard) && extendedRange <= Rules.StrikeRange(extendedStrikeActor.Kind, Salvo.Heavy) && extendedStrike.Action == ActionKind.Strike && extendedStrike.Salvo == Salvo.Heavy, "AI recognizes a legitimate high-quality Contact in extended Heavy-only range");

            var terrainGame = new PrototypeGame(1978, luzon);
            FormationState surface = terrainGame.Active;
            surface.Position = new HexCoord(0, 5);
            Assert(!terrainGame.Move(surface, new HexCoord(0, 3), MoveMode.HighTempo, out string landMessage) && landMessage.Contains("Land"), "Non-air movement cannot end on land");

            var recorder = new PlaytestRecorder();
            recorder.StartNew(restored);
            FormationState telemetryActor = restored.Active;
            PlaytestRecorder.Observation observation = recorder.Observe(restored, telemetryActor);
            Assert(restored.Hold(telemetryActor, out string telemetryOutcome), "Telemetry test action");
            recorder.RecordAction(restored, observation, telemetryActor, "Hold", "Quiet", "", "Move; Search; Strike; Hold", 1.25f, "Base Time 1", telemetryOutcome);
            Assert(recorder.SerializeJson().Contains("ActionResolved"), "Telemetry JSON serialization");
            Assert(recorder.SerializeCsv().Contains("DecisionSeconds"), "Telemetry CSV serialization");
            var tacticalMap = new TacticalMapElement();
            tacticalMap.SetState(restored, ToolkitActionMode.Move, MoveMode.Normal, SearchMode.Passive, Salvo.Standard);
            Assert(tacticalMap.focusable, "UI Toolkit tactical map keyboard focus");
            Debug.Log("Sea of Uncertainty core smoke tests passed.");
        }

        public static void CaptureMapPreview()
        {
            ScenarioDefinition scenario = ScenarioCatalog.LuzonStrait();
            var game = new PrototypeGame(1978, scenario);
            using (var map = new OperationalMap3D(scenario.Area, 1280, 720))
            {
                map.SetState(game, ToolkitActionMode.None, MoveMode.Normal, SearchMode.Passive, Salvo.Standard, false, true);
                map.Orbit(new Vector2(0f, -1000f));
                map.ProbeRenderLuminance();
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = map.Texture;
                var image = new Texture2D(map.Texture.width, map.Texture.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, map.Texture.width, map.Texture.height), 0, 0, false);
                image.Apply(false, false);
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Windows/3d-map-preview.png"));
                File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                RenderTexture.active = previous;
                Debug.Log("3D map preview captured: " + path);
            }
        }

        private static void EnsureRuntimePanelSettings()
        {
            ThemeStyleSheet runtimeTheme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/Resources/UI/UnityDefaultRuntimeTheme.tss");
            if (runtimeTheme == null) throw new System.Exception("Default runtime theme asset is missing.");

            PanelSettings panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(RuntimePanelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                panel.name = "Sea Runtime Panel";
                AssetDatabase.CreateAsset(panel, RuntimePanelPath);
            }

            panel.themeStyleSheet = runtimeTheme;
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = .5f;
            panel.sortingOrder = 10;
            EditorUtility.SetDirty(panel);
            AssetDatabase.SaveAssets();
        }

        private static void Ensure3DMaterials()
        {
            const string folder = "Assets/Resources/Materials/3D";
            System.IO.Directory.CreateDirectory(folder);
            EnsureMaterial(folder + "/CommandLine.mat", "Sprites/Default", new Color(.2f, .74f, .82f, .38f));
            EnsureMaterial(folder + "/CommandWater.mat", "Standard", new Color(.018f, .13f, .19f, 1f));
            EnsureMaterial(folder + "/CommandLand.mat", "Standard", new Color(.22f, .31f, .22f, 1f));
            EnsureMaterial(folder + "/CommandHighland.mat", "Standard", new Color(.29f, .31f, .16f, 1f));
            EnsureMaterial(folder + "/CommandLittoral.mat", "Standard", new Color(.09f, .34f, .34f, 1f));
            EnsureMaterial(folder + "/CommandBlue.mat", "Standard", new Color(.08f, .72f, .95f, 1f));
            EnsureMaterial(folder + "/CommandRed.mat", "Standard", new Color(.94f, .25f, .18f, 1f));
            EnsureMaterial(folder + "/CommandContact.mat", "Standard", new Color(1f, .48f, .13f, 1f));
            EnsureMaterial(folder + "/CommandWarning.mat", "Standard", new Color(1f, .72f, .12f, 1f));
            AssetDatabase.SaveAssets();
        }

        private static void EnsureMaterial(string path, string shaderName, Color color)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new System.Exception("Required shader is unavailable: " + shaderName);
            if (material == null)
            {
                material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.color = color;
            EditorUtility.SetDirty(material);
        }

        private static void Assert(bool condition, string label)
        {
            if (!condition) throw new System.Exception("Core smoke test failed: " + label);
        }
    }
}
#endif
