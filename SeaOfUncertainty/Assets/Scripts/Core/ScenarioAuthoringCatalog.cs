using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SeaOfUncertainty.Core
{
    [Serializable]
    public sealed class AuthoredScenarioCatalog
    {
        public int Version = 4;
        public List<AuthoredScenarioDefinition> Scenarios = new List<AuthoredScenarioDefinition>();
    }

    [Serializable]
    public sealed class AuthoredScenarioDefinition
    {
        public string Id;
        public string TheaterTemplateId;
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
        public List<ContactDefinition> Contacts = new List<ContactDefinition>();
        public List<ScenarioObjectiveDefinition> Objectives = new List<ScenarioObjectiveDefinition>();
        public List<ScheduledScenarioEventDefinition> ScheduledEvents = new List<ScheduledScenarioEventDefinition>();
        public List<OperationalRegionDefinition> DeploymentRegions = new List<OperationalRegionDefinition>();
        public List<OperationalRegionDefinition> ReinforcementRegions = new List<OperationalRegionDefinition>();
        public List<OperationalRegionDefinition> ExitRegions = new List<OperationalRegionDefinition>();
        public List<OperationalRegionDefinition> LogisticsRegions = new List<OperationalRegionDefinition>();
        public bool ReplaceAreaRegions;
        public List<OperationalRegionDefinition> AreaRegions = new List<OperationalRegionDefinition>();
        public bool ReplaceRestrictedAreas;
        public List<OperationalRegionDefinition> RestrictedAreas = new List<OperationalRegionDefinition>();
        public bool OverrideObjective;
        public HexCoord Objective;
        public bool OverridePresentation;
        public PresentationProfileDefinition Presentation = new PresentationProfileDefinition();
        public bool ReplaceSensorRanges;
        public List<SensorRangeDefinition> SensorRanges = new List<SensorRangeDefinition>();
        public bool ReplaceWeaponRanges;
        public List<WeaponRangeDefinition> WeaponRanges = new List<WeaponRangeDefinition>();
        public bool ReplaceMissileDefenseProfiles;
        public List<MissileDefenseProfileDefinition> MissileDefenseProfiles = new List<MissileDefenseProfileDefinition>();
    }

    public static class ScenarioAuthoringCatalog
    {
        public const string ResourcePath = "Data/scenario-catalog";
        public const int CurrentVersion = 4;
        public static readonly string[] TheaterTemplateIds = { "meridian-veil-archipelago", "luzon-strait-northern-approaches" };

        public static IReadOnlyList<ScenarioDefinition> Apply(IEnumerable<ScenarioDefinition> builtIns)
        {
            TextAsset source = Resources.Load<TextAsset>(ResourcePath);
            if (source == null) return builtIns.Select(Clone).ToList();
            return Apply(builtIns, JsonUtility.FromJson<AuthoredScenarioCatalog>(source.text));
        }

        // The editor and tests use exactly the same resolution and validation path as Resources loading.
        public static IReadOnlyList<ScenarioDefinition> Apply(IEnumerable<ScenarioDefinition> builtIns, AuthoredScenarioCatalog authored)
        {
            List<ScenarioDefinition> scenarios = builtIns.Select(Clone).ToList();
            if (authored == null || authored.Scenarios == null) throw new ArgumentException("Scenario catalog and Scenarios list are required.");
            if (authored.Version < 1 || authored.Version > CurrentVersion) throw new ArgumentException("Unsupported scenario catalog version: " + authored.Version);
            var errors = new List<string>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (AuthoredScenarioDefinition entry in authored.Scenarios)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id) || !ids.Add(entry.Id))
                {
                    errors.Add("Scenario ID is missing or duplicated: " + entry?.Id);
                    continue;
                }
                ScenarioDefinition scenario = scenarios.FirstOrDefault(item => string.Equals(item.Id, entry.Id, StringComparison.OrdinalIgnoreCase));
                try
                {
                    if (authored.Version == 1)
                    {
                        if (scenario == null) throw new ArgumentException("Version 1 only overrides known scenario IDs. Upgrade to version 4 and select a TheaterTemplateId for new operations.");
                        ApplyLegacy(scenario, entry);
                    }
                    else
                    {
                        ScenarioDefinition resolved = Resolve(entry);
                        if (scenario == null) scenarios.Add(resolved);
                        else scenarios[scenarios.IndexOf(scenario)] = resolved;
                        scenario = resolved;
                    }
                    if (authored.Version < 3)
                    {
                        foreach (FormationDefinition formation in (scenario.Formations ?? new List<FormationDefinition>()).Where(item => item != null).Concat((scenario.ScheduledEvents ?? new List<ScheduledScenarioEventDefinition>()).Where(item => item != null && item.Reinforcement != null).Select(item => item.Reinforcement))) formation.BallisticWeapons = new BallisticWeaponProfile();
                        foreach (OperationalRegionDefinition region in (scenario.LogisticsRegions ?? new List<OperationalRegionDefinition>()).Where(item => item != null)) region.SupportsBallisticReload = false;
                        OperationalDataMigration.AddBatteryProfiles(scenario);
                    }
                    if (authored.Version < 4)
                    {
                        foreach (FormationDefinition formation in (scenario.Formations ?? new List<FormationDefinition>()).Where(item => item != null).Concat((scenario.ScheduledEvents ?? new List<ScheduledScenarioEventDefinition>()).Where(item => item != null && item.Reinforcement != null).Select(item => item.Reinforcement))) formation.BallisticDefense = new BallisticDefenseProfile();
                        foreach (OperationalRegionDefinition region in (scenario.LogisticsRegions ?? new List<OperationalRegionDefinition>()).Where(item => item != null)) region.SupportsInterceptorReload = false;
                    }
                    errors.AddRange(OperationalDataValidator.Validate(scenario).Select(error => entry.Id + ": " + error));
                }
                catch (ArgumentException exception) { errors.Add(entry.Id + ": " + exception.Message); }
            }
            if (errors.Count > 0) throw new ArgumentException("Invalid scenario catalog:\n" + string.Join("\n", errors));
            return scenarios;
        }

        private static ScenarioDefinition Resolve(AuthoredScenarioDefinition source)
        {
            // Copy authoring objects too: a running game must never change the editor's catalog.
            AuthoredScenarioDefinition entry = Clone(source);
            ScenarioDefinition template = Template(entry.TheaterTemplateId);
            var scenario = new ScenarioDefinition
            {
                SchemaVersion = OperationalDataMigration.CurrentSchemaVersion,
                Id = entry.Id, DisplayName = entry.DisplayName, Summary = entry.Summary,
                Horizon = entry.Horizon, Weather = entry.Weather, WeatherSeverity = entry.WeatherSeverity,
                SpecialRules = entry.SpecialRules, VictoryConditions = entry.VictoryConditions,
                BlueCommandArchitecture = entry.BlueCommandArchitecture, RedCommandArchitecture = entry.RedCommandArchitecture,
                Area = template.Area,
                Formations = entry.Formations, Contacts = entry.Contacts, Objectives = entry.Objectives,
                ScheduledEvents = entry.ScheduledEvents, DeploymentRegions = entry.DeploymentRegions,
                ReinforcementRegions = entry.ReinforcementRegions, ExitRegions = entry.ExitRegions, LogisticsRegions = entry.LogisticsRegions,
                SensorRanges = entry.ReplaceSensorRanges ? entry.SensorRanges : template.SensorRanges,
                WeaponRanges = entry.ReplaceWeaponRanges ? entry.WeaponRanges : DefaultWeaponRanges(),
                MissileDefenseProfiles = entry.ReplaceMissileDefenseProfiles ? entry.MissileDefenseProfiles : template.MissileDefenseProfiles
            };
            if (entry.ReplaceAreaRegions) scenario.Area.Regions = entry.AreaRegions;
            if (entry.ReplaceRestrictedAreas) scenario.Area.RestrictedAreas = entry.RestrictedAreas;
            if (entry.OverrideObjective) scenario.Area.Objective = entry.Objective;
            if (entry.OverridePresentation) scenario.Area.Presentation = entry.Presentation;
            return scenario;
        }

        private static ScenarioDefinition Template(string id)
        {
            if (id == TheaterTemplateIds[0]) return ScenarioCatalog.MeridianVeil();
            if (id == TheaterTemplateIds[1]) return ScenarioCatalog.LuzonStrait();
            throw new ArgumentException("Unknown TheaterTemplateId '" + id + "'. Expected " + string.Join(" or ", TheaterTemplateIds) + ".");
        }

        public static List<WeaponRangeDefinition> DefaultWeaponRanges()
            => Enum.GetValues(typeof(FormationKind)).Cast<FormationKind>().Select(kind => new WeaponRangeDefinition
            {
                Kind = kind, Light = Rules.StrikeRange(kind, Salvo.Light), Standard = Rules.StrikeRange(kind, Salvo.Standard), Heavy = Rules.StrikeRange(kind, Salvo.Heavy)
            }).ToList();

        public static List<string> Validate(AuthoredScenarioCatalog catalog)
        {
            try { Apply(new[] { ScenarioCatalog.MeridianVeil(), ScenarioCatalog.LuzonStrait() }, catalog); return new List<string>(); }
            catch (ArgumentException exception) { return new List<string> { exception.Message }; }
        }

        public static AuthoredScenarioCatalog Upgrade(AuthoredScenarioCatalog catalog)
        {
            if (catalog == null) throw new ArgumentException("Scenario catalog is required.");
            if (catalog.Version == CurrentVersion) return Clone(catalog);
            IReadOnlyList<ScenarioDefinition> resolved = Apply(new[] { ScenarioCatalog.MeridianVeil(), ScenarioCatalog.LuzonStrait() }, catalog);
            return new AuthoredScenarioCatalog { Scenarios = catalog.Scenarios.Select(entry => FromScenario(resolved.First(item => item.Id == entry.Id))).ToList() };
        }

        public static AuthoredScenarioDefinition FromScenario(ScenarioDefinition scenario)
        {
            scenario = Clone(scenario);
            return new AuthoredScenarioDefinition
            {
                Id = scenario.Id, TheaterTemplateId = scenario.Area.Id, DisplayName = scenario.DisplayName, Summary = scenario.Summary,
                Horizon = scenario.Horizon, Weather = scenario.Weather, WeatherSeverity = scenario.WeatherSeverity,
                SpecialRules = scenario.SpecialRules, VictoryConditions = scenario.VictoryConditions,
                BlueCommandArchitecture = scenario.BlueCommandArchitecture, RedCommandArchitecture = scenario.RedCommandArchitecture,
                Formations = scenario.Formations, Contacts = scenario.Contacts, Objectives = scenario.Objectives, ScheduledEvents = scenario.ScheduledEvents,
                DeploymentRegions = scenario.DeploymentRegions, ReinforcementRegions = scenario.ReinforcementRegions,
                ExitRegions = scenario.ExitRegions, LogisticsRegions = scenario.LogisticsRegions,
                ReplaceAreaRegions = true, AreaRegions = scenario.Area.Regions, ReplaceRestrictedAreas = true, RestrictedAreas = scenario.Area.RestrictedAreas,
                OverrideObjective = true, Objective = scenario.Area.Objective, OverridePresentation = true, Presentation = scenario.Area.Presentation,
                ReplaceSensorRanges = true, SensorRanges = scenario.SensorRanges,
                ReplaceWeaponRanges = true, WeaponRanges = scenario.WeaponRanges.Count == 0 ? DefaultWeaponRanges() : scenario.WeaponRanges,
                ReplaceMissileDefenseProfiles = true, MissileDefenseProfiles = scenario.MissileDefenseProfiles
            };
        }

        private static T Clone<T>(T value) => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));

        // Version 1 compatibility: known IDs only, nonempty formations replace; other lists remain compiled defaults.
        private static void ApplyLegacy(ScenarioDefinition scenario, AuthoredScenarioDefinition entry)
        {
            if (!string.IsNullOrWhiteSpace(entry.DisplayName)) scenario.DisplayName = entry.DisplayName;
            if (!string.IsNullOrWhiteSpace(entry.Summary)) scenario.Summary = entry.Summary;
            if (entry.Horizon > 0) scenario.Horizon = entry.Horizon;
            if (!string.IsNullOrWhiteSpace(entry.Weather)) scenario.Weather = entry.Weather;
            scenario.WeatherSeverity = Math.Max(0, entry.WeatherSeverity);
            if (!string.IsNullOrWhiteSpace(entry.SpecialRules)) scenario.SpecialRules = entry.SpecialRules;
            if (!string.IsNullOrWhiteSpace(entry.VictoryConditions)) scenario.VictoryConditions = entry.VictoryConditions;
            scenario.BlueCommandArchitecture = entry.BlueCommandArchitecture;
            scenario.RedCommandArchitecture = entry.RedCommandArchitecture;
            if (entry.Formations != null && entry.Formations.Count > 0) scenario.Formations = Clone(entry).Formations;
        }
    }
}
