#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using SeaOfUncertainty.Core;
using UnityEditor;
using UnityEngine;

namespace SeaOfUncertainty.Editor
{
    internal sealed class ScenarioCatalogEditingState : ScriptableObject
    {
        public AuthoredScenarioCatalog Catalog;
    }

    public sealed class ScenarioCatalogEditorWindow : EditorWindow
    {
        private const string AssetPath = "Assets/Resources/Data/scenario-catalog.json";
        private ScenarioCatalogEditingState state;
        private SerializedObject serializedState;
        private int scenarioIndex;
        private Vector2 scroll;
        private string validation = "Not validated.";
        private bool valid;

        [MenuItem("Sea of Uncertainty/Scenario Catalog Editor")]
        public static void Open() => GetWindow<ScenarioCatalogEditorWindow>("Scenario Catalog");
        private void OnEnable() => LoadCatalog();
        private void OnDisable() { if (state != null) DestroyImmediate(state); }

        private void LoadCatalog()
        {
            if (state == null) state = CreateInstance<ScenarioCatalogEditingState>();
            state.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                TextAsset source = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetPath);
                AuthoredScenarioCatalog loaded = source == null ? new AuthoredScenarioCatalog() : JsonUtility.FromJson<AuthoredScenarioCatalog>(source.text);
                state.Catalog = ScenarioAuthoringCatalog.Upgrade(loaded);
                validation = loaded.Version == 1 ? "Version 1 migrated in memory. Validate and Save JSON to persist version 2." : "Not validated.";
                valid = false;
                serializedState = new SerializedObject(state);
                scenarioIndex = Mathf.Clamp(scenarioIndex, 0, Math.Max(0, state.Catalog.Scenarios.Count - 1));
            }
            catch (Exception exception)
            {
                serializedState = null;
                validation = "Cannot load catalog: " + exception.Message + "\nCorrect the JSON and Reload.";
                valid = false;
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("External Scenario and Formation Authoring", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Version 2: choose a Theater Template Id. Forces, Contacts, objectives, events and scenario region lists always replace; [] clears them. Area regions, restrictions, presentation and sensor/weapon/defense profiles inherit unless their Replace/Override switch is enabled. Expand lists to edit all fields.", MessageType.Info);
            if (serializedState == null)
            {
                EditorGUILayout.HelpBox(validation, MessageType.Error);
                if (GUILayout.Button("Reload")) LoadCatalog();
                return;
            }
            AuthoredScenarioCatalog catalog = state.Catalog;
            EditorGUILayout.LabelField("Catalog Version", catalog.Version.ToString());
            EditorGUILayout.LabelField("Theater Template IDs", string.Join(" / ", ScenarioAuthoringCatalog.TheaterTemplateIds));
            if (catalog.Scenarios.Count > 0)
            {
                scenarioIndex = Mathf.Clamp(scenarioIndex, 0, catalog.Scenarios.Count - 1);
                scenarioIndex = EditorGUILayout.Popup("Scenario", scenarioIndex, catalog.Scenarios.Select(item => item?.DisplayName ?? item?.Id ?? "(null entry)").ToArray());
                serializedState.Update();
                scroll = EditorGUILayout.BeginScrollView(scroll);
                SerializedProperty entry = serializedState.FindProperty("Catalog").FindPropertyRelative("Scenarios").GetArrayElementAtIndex(scenarioIndex);
                EditorGUILayout.PropertyField(entry, new GUIContent("Scenario Definition"), true);
                EditorGUILayout.EndScrollView();
                if (serializedState.ApplyModifiedProperties()) { valid = false; validation = "Changed. Validate before saving."; }
            }
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Scenario"))
            {
                catalog.Scenarios.Add(new AuthoredScenarioDefinition { Id = UniqueId("new-scenario"), DisplayName = "New Scenario", TheaterTemplateId = ScenarioAuthoringCatalog.TheaterTemplateIds[0], Horizon = 16, Weather = "Clear", SpecialRules = "Standard operational rules." });
                scenarioIndex = catalog.Scenarios.Count - 1;
                valid = false;
            }
            if (catalog.Scenarios.Count > 0 && GUILayout.Button("Duplicate Selected"))
            {
                AuthoredScenarioDefinition copy = JsonUtility.FromJson<AuthoredScenarioDefinition>(JsonUtility.ToJson(catalog.Scenarios[scenarioIndex]));
                copy.Id = UniqueId(copy.Id + "-copy");
                copy.DisplayName += " Copy";
                catalog.Scenarios.Add(copy);
                scenarioIndex = catalog.Scenarios.Count - 1;
                valid = false;
            }
            if (catalog.Scenarios.Count > 0 && GUILayout.Button("Remove Selected"))
            {
                catalog.Scenarios.RemoveAt(scenarioIndex);
                scenarioIndex = Math.Max(0, scenarioIndex - 1);
                valid = false;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox(validation, valid ? MessageType.Info : MessageType.Warning);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Validate")) ValidateCatalog();
            if (GUILayout.Button("Save JSON")) SaveCatalog();
            if (GUILayout.Button("Reload")) LoadCatalog();
            EditorGUILayout.EndHorizontal();
        }

        private string UniqueId(string prefix)
        {
            string candidate = prefix;
            for (int suffix = 2; state.Catalog.Scenarios.Any(item => string.Equals(item.Id, candidate, StringComparison.OrdinalIgnoreCase)); suffix++) candidate = prefix + "-" + suffix;
            return candidate;
        }

        private void ValidateCatalog()
        {
            var errors = ScenarioAuthoringCatalog.Validate(state.Catalog);
            valid = errors.Count == 0;
            validation = valid ? $"Valid: {state.Catalog.Scenarios.Count} authored scenarios." : string.Join("\n", errors);
        }

        private void SaveCatalog()
        {
            ValidateCatalog();
            if (!valid) return;
            File.WriteAllText(Path.GetFullPath(AssetPath), JsonUtility.ToJson(state.Catalog, true));
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
            validation += " Saved.";
        }
    }
}
#endif
