using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SeaOfUncertainty.Core
{
    [Serializable]
    public sealed class AuthoredScenarioCatalog
    {
        public int Version = 1;
        public List<AuthoredScenarioDefinition> Scenarios = new List<AuthoredScenarioDefinition>();
    }

    [Serializable]
    public sealed class AuthoredScenarioDefinition
    {
        public string Id;
        public string DisplayName;
        public string Summary;
        public int Horizon;
        public string Weather;
        public int WeatherSeverity;
        public string SpecialRules;
        public string VictoryConditions;
        public CommandArchitecture BlueCommandArchitecture = CommandArchitecture.MissionCommand;
        public CommandArchitecture RedCommandArchitecture = CommandArchitecture.MissionCommand;
        public List<FormationDefinition> Formations = new List<FormationDefinition>();
    }

    public static class ScenarioAuthoringCatalog
    {
        public const string ResourcePath = "Data/scenario-catalog";

        public static IReadOnlyList<ScenarioDefinition> Apply(IEnumerable<ScenarioDefinition> builtIns)
        {
            List<ScenarioDefinition> scenarios = builtIns.ToList();
            TextAsset source = Resources.Load<TextAsset>(ResourcePath);
            if (source == null) return scenarios;
            AuthoredScenarioCatalog authored = JsonUtility.FromJson<AuthoredScenarioCatalog>(source.text);
            if (authored?.Scenarios == null || authored.Version != 1) return scenarios;
            foreach (AuthoredScenarioDefinition entry in authored.Scenarios)
            {
                ScenarioDefinition scenario = scenarios.FirstOrDefault(item => string.Equals(item.Id, entry.Id, StringComparison.OrdinalIgnoreCase));
                if (scenario == null) continue;
                if (!string.IsNullOrWhiteSpace(entry.DisplayName)) scenario.DisplayName = entry.DisplayName;
                if (!string.IsNullOrWhiteSpace(entry.Summary)) scenario.Summary = entry.Summary;
                if (entry.Horizon > 0) scenario.Horizon = entry.Horizon;
                if (!string.IsNullOrWhiteSpace(entry.Weather)) scenario.Weather = entry.Weather;
                scenario.WeatherSeverity = Math.Max(0, entry.WeatherSeverity);
                if (!string.IsNullOrWhiteSpace(entry.SpecialRules)) scenario.SpecialRules = entry.SpecialRules;
                if (!string.IsNullOrWhiteSpace(entry.VictoryConditions)) scenario.VictoryConditions = entry.VictoryConditions;
                scenario.BlueCommandArchitecture = entry.BlueCommandArchitecture;
                scenario.RedCommandArchitecture = entry.RedCommandArchitecture;
                if (entry.Formations != null && entry.Formations.Count > 0) scenario.Formations = entry.Formations;
            }
            return scenarios;
        }
    }
}
