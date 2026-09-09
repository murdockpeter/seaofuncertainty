#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using SeaOfUncertainty.Core;
using UnityEditor;
using UnityEngine;

namespace SeaOfUncertainty.Editor
{
    public sealed class ScenarioCatalogEditorWindow : EditorWindow
    {
        private const string AssetPath = "Assets/Resources/Data/scenario-catalog.json";
        private AuthoredScenarioCatalog catalog;
        private int scenarioIndex;
        private Vector2 scroll;
        private string validation = "Not validated.";

        [MenuItem("Sea of Uncertainty/Scenario Catalog Editor")]
        public static void Open() => GetWindow<ScenarioCatalogEditorWindow>("Scenario Catalog");

        private void OnEnable() => LoadCatalog();

        private void LoadCatalog()
        {
            TextAsset source = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetPath);
            catalog = source == null ? new AuthoredScenarioCatalog() : JsonUtility.FromJson<AuthoredScenarioCatalog>(source.text) ?? new AuthoredScenarioCatalog();
            if (catalog.Scenarios == null) catalog.Scenarios = new System.Collections.Generic.List<AuthoredScenarioDefinition>();
            scenarioIndex = Mathf.Clamp(scenarioIndex, 0, Math.Max(0, catalog.Scenarios.Count - 1));
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("External Scenario and Formation Authoring", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Edits the distributable JSON catalog. Operational-area terrain remains in the validated area definition.", MessageType.Info);
            if (catalog == null) LoadCatalog();
            if (catalog.Scenarios.Count == 0)
            {
                if (GUILayout.Button("Add Scenario")) catalog.Scenarios.Add(new AuthoredScenarioDefinition { Id = "new-scenario", DisplayName = "New Scenario", Horizon = 16 });
                return;
            }

            string[] names = catalog.Scenarios.Select(item => string.IsNullOrWhiteSpace(item.DisplayName) ? item.Id : item.DisplayName).ToArray();
            scenarioIndex = EditorGUILayout.Popup("Scenario", scenarioIndex, names);
            AuthoredScenarioDefinition scenario = catalog.Scenarios[scenarioIndex];
            scroll = EditorGUILayout.BeginScrollView(scroll);
            scenario.Id = EditorGUILayout.TextField("Stable ID", scenario.Id);
            scenario.DisplayName = EditorGUILayout.TextField("Display Name", scenario.DisplayName);
            scenario.Summary = EditorGUILayout.TextField("Summary", scenario.Summary);
            scenario.Horizon = EditorGUILayout.IntField("Horizon", scenario.Horizon);
            scenario.Weather = EditorGUILayout.TextField("Weather", scenario.Weather);
            scenario.WeatherSeverity = EditorGUILayout.IntSlider("Weather Severity", scenario.WeatherSeverity, 0, 3);
            scenario.BlueCommandArchitecture = (CommandArchitecture)EditorGUILayout.EnumPopup("Blue Command", scenario.BlueCommandArchitecture);
            scenario.RedCommandArchitecture = (CommandArchitecture)EditorGUILayout.EnumPopup("Red Command", scenario.RedCommandArchitecture);
            scenario.SpecialRules = EditorGUILayout.TextArea(scenario.SpecialRules, GUILayout.MinHeight(45));
            scenario.VictoryConditions = EditorGUILayout.TextArea(scenario.VictoryConditions, GUILayout.MinHeight(45));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Formations", EditorStyles.boldLabel);
            for (int index = 0; index < scenario.Formations.Count; index++)
            {
                FormationDefinition formation = scenario.Formations[index];
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{formation.Id} — {formation.Name}", EditorStyles.boldLabel);
                if (GUILayout.Button("Remove", GUILayout.Width(70))) { scenario.Formations.RemoveAt(index--); EditorGUILayout.EndHorizontal(); EditorGUILayout.EndVertical(); continue; }
                EditorGUILayout.EndHorizontal();
                formation.Id = EditorGUILayout.TextField("ID", formation.Id);
                formation.Name = EditorGUILayout.TextField("Name", formation.Name);
                formation.Side = (Side)EditorGUILayout.EnumPopup("Side", formation.Side);
                formation.Kind = (FormationKind)EditorGUILayout.EnumPopup("Kind", formation.Kind);
                formation.Q = EditorGUILayout.IntField("Q", formation.Q);
                formation.R = EditorGUILayout.IntField("R", formation.R);
                formation.ReadyTime = EditorGUILayout.IntField("Ready Time", formation.ReadyTime);
                if (formation.Ratings == null) formation.Ratings = new Ratings();
                formation.Ratings.Move = EditorGUILayout.IntField("Move", formation.Ratings.Move);
                formation.Ratings.Search = EditorGUILayout.IntField("Search", formation.Ratings.Search);
                formation.Ratings.Signature = EditorGUILayout.IntField("Signature", formation.Ratings.Signature);
                formation.Ratings.Strike = EditorGUILayout.IntField("Strike", formation.Ratings.Strike);
                formation.Ratings.Defense = EditorGUILayout.IntField("Defense", formation.Ratings.Defense);
                formation.Ratings.Asw = EditorGUILayout.IntField("ASW", formation.Ratings.Asw);
                formation.Ratings.Command = EditorGUILayout.IntField("Command", formation.Ratings.Command);
                formation.Ratings.ElectronicWarfare = EditorGUILayout.IntField("EW", formation.Ratings.ElectronicWarfare);
                formation.Ratings.Cyber = EditorGUILayout.IntField("Cyber", formation.Ratings.Cyber);
                if (formation.Weapons == null) formation.Weapons = new WeaponInventoryState();
                formation.Weapons.MaxLight = formation.Weapons.Light = EditorGUILayout.IntField("Light Salvos", formation.Weapons.MaxLight);
                formation.Weapons.MaxStandard = formation.Weapons.Standard = EditorGUILayout.IntField("Standard Salvos", formation.Weapons.MaxStandard);
                formation.Weapons.MaxHeavy = formation.Weapons.Heavy = EditorGUILayout.IntField("Heavy Salvos", formation.Weapons.MaxHeavy);
                EditorGUILayout.EndVertical();
            }
            if (GUILayout.Button("Add Formation")) scenario.Formations.Add(new FormationDefinition { Id = "NEW", Name = "New Formation", Ratings = new Ratings(), Weapons = new WeaponInventoryState() });
            EditorGUILayout.EndScrollView();

            EditorGUILayout.HelpBox(validation, validation.StartsWith("Valid") ? MessageType.Info : MessageType.Warning);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Validate")) ValidateCatalog();
            if (GUILayout.Button("Save JSON")) SaveCatalog();
            if (GUILayout.Button("Reload")) LoadCatalog();
            EditorGUILayout.EndHorizontal();
        }

        private void ValidateCatalog()
        {
            string[] duplicateScenarioIds = catalog.Scenarios.GroupBy(item => item.Id).Where(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1).Select(group => group.Key).ToArray();
            int invalidFormations = catalog.Scenarios.Sum(scenario => scenario.Formations.Count(item => string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(item.Name) || item.Ratings == null || item.Weapons == null));
            int duplicateFormations = catalog.Scenarios.Sum(scenario => scenario.Formations.GroupBy(item => item.Id).Count(group => group.Count() > 1));
            validation = duplicateScenarioIds.Length == 0 && invalidFormations == 0 && duplicateFormations == 0 && catalog.Scenarios.All(item => item.Horizon > 0)
                ? $"Valid: {catalog.Scenarios.Count} scenarios and {catalog.Scenarios.Sum(item => item.Formations.Count)} formations."
                : $"Invalid: {duplicateScenarioIds.Length} scenario-ID issues, {invalidFormations} incomplete formations, {duplicateFormations} duplicate formation IDs.";
        }

        private void SaveCatalog()
        {
            ValidateCatalog();
            if (!validation.StartsWith("Valid")) return;
            File.WriteAllText(Path.GetFullPath(AssetPath), JsonUtility.ToJson(catalog, true));
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
            validation += " Saved.";
        }
    }
}
#endif
