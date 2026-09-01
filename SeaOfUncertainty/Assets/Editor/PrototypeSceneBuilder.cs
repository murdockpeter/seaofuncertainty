#if UNITY_EDITOR
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

            var game = new PrototypeGame();
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
                Assert(operationalMap.VisibleFormationCount == game.Formations.FindAll(formation => formation.Side == game.Active.Side && !formation.IsDestroyed).Count, "3D view instantiates only the active side's friendly formations");
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
                game.Contacts.Add(new ContactState { Owner = game.Active.Side, TargetId = "FALSE-TEST", LastKnownPosition = new HexCoord(4, 4), Location = LocationQuality.Low, Identity = IdentityQuality.Unknown, Age = 3, IsFalse = true });
                operationalMap.SetState(game, ToolkitActionMode.Search, MoveMode.Normal, SearchMode.Focused, Salvo.Standard);
                game.Contacts.RemoveAll(contact => contact.TargetId == "FALSE-TEST");
                if (game.Contacts.Count > baselineContactCount) game.Contacts.RemoveRange(baselineContactCount, game.Contacts.Count - baselineContactCount);
                operationalMap.SetState(game, ToolkitActionMode.None, MoveMode.Normal, SearchMode.Passive, Salvo.Standard);
                Assert(operationalMap.PooledMarkerCount >= 1, "Formation and Contact marker roots return to the object pool");
                operationalMap.Rotate(1f);
                Assert(Mathf.Approximately(operationalMap.Heading, 30f), "3D command camera uses stepped rotation");
                operationalMap.Pan(new Vector2(20f, -15f));
                operationalMap.Zoom(1f);
                operationalMap.ResetCamera();
                Assert(Mathf.Approximately(operationalMap.Heading, 0f), "3D command camera reset");
            }
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
            Assert(saveData.Version == 2 && saveData.ScenarioId == "meridian-veil" && saveData.OperationalAreaId == "meridian-veil-archipelago", "Version 2 save uses stable scenario and area IDs");
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
