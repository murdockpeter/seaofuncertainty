#if UNITY_EDITOR
using System.Linq;
using SeaOfUncertainty.Core;
using UnityEditor;
using UnityEngine;

namespace SeaOfUncertainty.Editor
{
    public sealed class OperationalAreaPreviewWindow : EditorWindow
    {
        private int scenarioIndex;
        private Vector2 scroll;

        [MenuItem("Sea of Uncertainty/Operational Area Preview")]
        public static void Open() => GetWindow<OperationalAreaPreviewWindow>("Operational Area Preview");

        private void OnGUI()
        {
            var catalog = ScenarioCatalog.All();
            string[] names = catalog.Select(item => item.DisplayName).ToArray();
            scenarioIndex = EditorGUILayout.Popup("Scenario", Mathf.Clamp(scenarioIndex, 0, names.Length - 1), names);
            ScenarioDefinition scenario = catalog[scenarioIndex];
            OperationalAreaDefinition area = scenario.Area;
            EditorGUILayout.LabelField(area.DisplayName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"{area.Width} x {area.Height} hexes  |  {area.NauticalMilesPerHex} nm/hex  |  {scenario.Horizon * area.ReadyTimeHours:0} hours");
            foreach (string error in OperationalDataValidator.Validate(scenario)) EditorGUILayout.HelpBox(error, MessageType.Error);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            float size = 22f;
            Rect canvas = GUILayoutUtility.GetRect(area.Width * size + size, area.Height * size + size);
            EditorGUI.DrawRect(canvas, new Color(.015f, .09f, .13f));
            for (int q = 0; q < area.Width; q++) for (int r = 0; r < area.Height; r++)
            {
                HexCoord hex = new HexCoord(q, r);
                if (!area.Contains(hex)) continue;
                float x = canvas.x + q * size * .76f + size * .5f;
                float y = canvas.y + (r + (q & 1) * .5f) * size * .88f + size * .5f;
                OperationalTerrain terrain = area.TerrainAt(hex);
                Color color = terrain == OperationalTerrain.Land ? new Color(.45f, .52f, .34f) : terrain == OperationalTerrain.Littoral ? new Color(.1f, .46f, .48f) : terrain == OperationalTerrain.Strait ? new Color(.15f, .58f, .7f) : new Color(.04f, .2f, .3f);
                EditorGUI.DrawRect(new Rect(x - 7f, y - 7f, 14f, 14f), color);
                if (hex.Equals(area.Objective)) GUI.Label(new Rect(x - 8f, y - 10f, 20f, 20f), "◎");
            }
            foreach (OperationalLocationDefinition location in area.Locations)
            {
                float x = canvas.x + location.Q * size * .76f + size * .5f;
                float y = canvas.y + (location.R + (location.Q & 1) * .5f) * size * .88f + size * .5f;
                GUI.Label(new Rect(x + 8f, y - 9f, 180f, 20f), location.Name, EditorStyles.miniLabel);
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
#endif
