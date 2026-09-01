using System;
using System.Collections.Generic;
using System.Linq;

namespace SeaOfUncertainty.Core
{
    public enum OperationalTerrain { DeepWater, Littoral, Land, Strait }
    public enum MapProjectionKind { LocalAzimuthalEquidistant, FictionalPlanar }
    public enum LocationKind { Port, Airfield, Objective, Strait, Anchorage, NamedWaters }

    [Serializable]
    public sealed class GeographicPoint
    {
        public double Latitude;
        public double Longitude;
    }

    [Serializable]
    public sealed class OperationalLocationDefinition
    {
        public string Id;
        public string Name;
        public LocationKind Kind;
        public int Q;
        public int R;
        public GeographicPoint Geographic = new GeographicPoint();
        public HexCoord Hex => new HexCoord(Q, R);
    }

    [Serializable]
    public sealed class OperationalRegionDefinition
    {
        public string Id;
        public string Name;
        public List<HexCoord> Hexes = new List<HexCoord>();
    }

    [Serializable]
    public sealed class PresentationProfileDefinition
    {
        public float CameraPitch = 58f;
        public float CameraYaw;
        public float CameraZoomMultiplier = 1.8f;
        public string LightingProfile = "MaritimeDay";
        public string WaterProfile = "DeepBlue";
        public string TerrainAssetSet = "PrototypeOperational";
        public float TimeOfDay = 12f;
        public float Visibility = 1f;
        public ColorProfile Palette = ColorProfile.Maritime;
        public float MinimumZoom = 9f;
        public float MaximumZoomMultiplier = 2.5f;
        public float PanPadding = 1f;
    }

    public enum ColorProfile { Maritime, NightOperations, HighContrast }

    [Serializable]
    public sealed class TerrainHexDefinition
    {
        public int Q;
        public int R;
        public OperationalTerrain Terrain;
        public string Name;
        public HexCoord Hex => new HexCoord(Q, R);
    }

    [Serializable]
    public sealed class OperationalAreaDefinition
    {
        public int SchemaVersion = 2;
        public string Id;
        public string DisplayName;
        public string Version;
        public string Attribution;
        public int Width;
        public int Height;
        public int NauticalMilesPerHex = 20;
        public float ReadyTimeHours = 2f;
        public MapProjectionKind Projection = MapProjectionKind.FictionalPlanar;
        public GeographicPoint GeographicOrigin = new GeographicPoint();
        public string CoastlineResource;
        public float GridOrientationDegrees;
        public HexCoord Objective;
        public List<TerrainHexDefinition> Terrain = new List<TerrainHexDefinition>();
        public List<HexCoord> ValidHexes = new List<HexCoord>();
        public List<OperationalLocationDefinition> Locations = new List<OperationalLocationDefinition>();
        public List<OperationalRegionDefinition> Regions = new List<OperationalRegionDefinition>();
        public List<OperationalRegionDefinition> RestrictedAreas = new List<OperationalRegionDefinition>();
        public PresentationProfileDefinition Presentation = new PresentationProfileDefinition();

        public bool Contains(HexCoord hex) => hex.Q >= 0 && hex.Q < Width && hex.R >= 0 && hex.R < Height && (ValidHexes.Count == 0 || ValidHexes.Contains(hex));
        public int NauticalMiles(HexCoord a, HexCoord b) => HexCoord.Distance(a, b) * NauticalMilesPerHex;
        public OperationalTerrain TerrainAt(HexCoord hex) => Terrain.FirstOrDefault(item => item.Hex.Equals(hex))?.Terrain ?? OperationalTerrain.DeepWater;
    }

    [Serializable]
    public sealed class FormationDefinition
    {
        public string Id;
        public string Name;
        public Side Side;
        public FormationKind Kind;
        public int Q;
        public int R;
        public int ReadyTime;
        public Ratings Ratings;
    }

    [Serializable]
    public sealed class ContactDefinition
    {
        public Side Owner;
        public string TargetId;
        public int Q;
        public int R;
        public LocationQuality Location;
        public IdentityQuality Identity;
        public int Age;
    }

    [Serializable]
    public sealed class ScenarioDefinition
    {
        public int SchemaVersion = 2;
        public string Id;
        public string DisplayName;
        public string Summary;
        public int Horizon;
        public string Weather = "Clear";
        public int WeatherSeverity;
        public string SpecialRules;
        public string VictoryConditions;
        public OperationalAreaDefinition Area;
        public List<FormationDefinition> Formations = new List<FormationDefinition>();
        public List<ContactDefinition> Contacts = new List<ContactDefinition>();
        public List<OperationalRegionDefinition> DeploymentRegions = new List<OperationalRegionDefinition>();
        public List<OperationalRegionDefinition> ReinforcementRegions = new List<OperationalRegionDefinition>();
        public List<OperationalRegionDefinition> ExitRegions = new List<OperationalRegionDefinition>();
        public List<OperationalRegionDefinition> LogisticsRegions = new List<OperationalRegionDefinition>();
    }

    public static class ScenarioCatalog
    {
        public static ScenarioDefinition MeridianVeil()
        {
            var area = new OperationalAreaDefinition
            {
                Id = "meridian-veil-archipelago",
                DisplayName = "Meridian Veil Archipelago",
                Version = "1.0",
                Attribution = "Fictional prototype operational area; generated presentation assets.",
                Width = 12,
                Height = 10,
                Objective = new HexCoord(6, 5),
                Projection = MapProjectionKind.FictionalPlanar,
                GeographicOrigin = new GeographicPoint { Latitude = 0, Longitude = 0 }
            };

            AddTerrain(area, OperationalTerrain.Land, "Western Reefs", (0, 0), (0, 1), (1, 0), (1, 1), (1, 2), (2, 1));
            AddTerrain(area, OperationalTerrain.Littoral, "North Passage", (4, 0), (5, 0), (8, 0), (9, 0), (10, 1));
            AddTerrain(area, OperationalTerrain.Land, "Inner Sea Islands", (5, 4), (6, 4), (7, 5), (6, 6));
            AddTerrain(area, OperationalTerrain.Littoral, "Inner Sea Shoals", (4, 4), (5, 5), (7, 4), (7, 6));
            AddTerrain(area, OperationalTerrain.Land, "Eastern Massif", (10, 7), (11, 7), (10, 8), (11, 8), (11, 9));
            AddTerrain(area, OperationalTerrain.Land, "Southern Chain", (2, 9), (3, 8), (4, 9), (8, 9));
            AddTerrain(area, OperationalTerrain.Strait, "Meridian Narrows", (8, 4), (8, 5));

            var scenario = new ScenarioDefinition
            {
                Id = "meridian-veil",
                DisplayName = "Meridian Veil",
                Summary = "Two naval forces converge on the Inner Sea.",
                Horizon = 16,
                Area = area,
                Weather = "Clear",
                SpecialRules = "Automatic Defend reaction; fixed paired-test deployment.",
                VictoryConditions = "Control the Inner Sea at T16; preserve the carrier; score enemy damage."
            };

            AddFormation(scenario, "B-CV", "CSG Resolute", Side.Blue, FormationKind.CarrierGroup, 1, 4, 1, 2, 3, 1, 4, 3, 1);
            AddFormation(scenario, "B-SG", "SAG Valiant", Side.Blue, FormationKind.SurfaceGroup, 2, 7, 0, 2, 2, 2, 3, 3, 2);
            AddFormation(scenario, "B-SS", "SSN Nightfall", Side.Blue, FormationKind.Submarine, 0, 7, 0, 2, 3, -1, 3, 2, 3);
            AddFormation(scenario, "B-AG", "Air Group Kestrel", Side.Blue, FormationKind.AirGroup, 3, 2, 1, 3, 3, 2, 3, 2, 2);
            AddFormation(scenario, "R-CV", "CVG Tempest", Side.Red, FormationKind.CarrierGroup, 10, 5, 1, 2, 3, 1, 4, 3, 1);
            AddFormation(scenario, "R-SG", "Surface Action Two", Side.Red, FormationKind.SurfaceGroup, 9, 2, 0, 2, 2, 2, 3, 3, 2);
            AddFormation(scenario, "R-SS", "Hunter Submarine", Side.Red, FormationKind.Submarine, 11, 2, 0, 2, 3, -1, 3, 2, 3);
            AddFormation(scenario, "R-AG", "Air Group Ember", Side.Red, FormationKind.AirGroup, 8, 8, 1, 3, 3, 2, 3, 2, 2);
            scenario.Contacts.Add(new ContactDefinition { Owner = Side.Blue, TargetId = "R-SG", Q = 8, R = 3, Location = LocationQuality.Low, Identity = IdentityQuality.Unknown, Age = 2 });
            scenario.Contacts.Add(new ContactDefinition { Owner = Side.Red, TargetId = "B-CV", Q = 2, R = 4, Location = LocationQuality.Medium, Identity = IdentityQuality.General, Age = 1 });
            return scenario;
        }

        public static IReadOnlyList<ScenarioDefinition> All() => new[] { MeridianVeil(), LuzonStrait() };

        public static ScenarioDefinition Find(string id) => All().FirstOrDefault(scenario => string.Equals(scenario.Id, id, StringComparison.OrdinalIgnoreCase)) ?? MeridianVeil();

        public static ScenarioDefinition LuzonStrait()
        {
            var area = new OperationalAreaDefinition
            {
                Id = "luzon-strait-northern-approaches",
                DisplayName = "Luzon Strait — Northern Approaches",
                Version = "MVP-1",
                Attribution = "Coastline geometry generalized from Natural Earth public-domain data. Bathymetry categories informed by GEBCO Grid; GEBCO Compilation Group. Not for navigation.",
                Width = 24,
                Height = 20,
                NauticalMilesPerHex = 20,
                ReadyTimeHours = 2f,
                Projection = MapProjectionKind.LocalAzimuthalEquidistant,
                GeographicOrigin = new GeographicPoint { Latitude = 20.25, Longitude = 121.25 },
                CoastlineResource = "Geography/luzon-strait-coastline",
                GridOrientationDegrees = 0,
                Objective = new HexCoord(11, 9),
                Presentation = new PresentationProfileDefinition { CameraPitch = 58f, CameraYaw = 0, CameraZoomMultiplier = 1.65f, LightingProfile = "MaritimeHaze", WaterProfile = "PhilippineSea", TerrainAssetSet = "LuzonMvp", TimeOfDay = 16f, Visibility = .72f }
            };

            AddBlock(area, OperationalTerrain.Land, "Southern Taiwan", 0, 0, 7, 3);
            AddBlock(area, OperationalTerrain.Littoral, "Taiwan Shelf", 0, 4, 8, 5);
            AddBlock(area, OperationalTerrain.Land, "Northern Luzon", 5, 17, 17, 19);
            AddBlock(area, OperationalTerrain.Littoral, "Luzon Shelf", 4, 15, 18, 16);
            AddTerrain(area, OperationalTerrain.Land, "Batan Island", (11, 10));
            AddTerrain(area, OperationalTerrain.Land, "Itbayat", (10, 8));
            AddTerrain(area, OperationalTerrain.Land, "Sabtang", (11, 11));
            AddTerrain(area, OperationalTerrain.Land, "Babuyan Islands", (9, 14), (12, 14), (14, 15));
            AddTerrain(area, OperationalTerrain.Land, "Orchid Island", (7, 5));
            AddTerrain(area, OperationalTerrain.Strait, "Bashi Channel", (9, 9), (11, 9), (12, 9), (13, 9));
            AddTerrain(area, OperationalTerrain.Strait, "Balintang Channel", (9, 12), (10, 12), (11, 12), (12, 12), (13, 12));

            AddLocation(area, "bashi-channel", "Bashi Channel", LocationKind.Strait, 11, 9, 20.75, 121.75);
            AddLocation(area, "balintang-channel", "Balintang Channel", LocationKind.Strait, 11, 12, 19.67, 121.55);
            AddLocation(area, "basco", "Basco", LocationKind.Port, 11, 10, 20.45, 121.97);
            AddLocation(area, "basco-airfield", "Basco Airfield", LocationKind.Airfield, 11, 10, 20.45, 121.98);
            AddLocation(area, "aparri", "Aparri", LocationKind.Port, 11, 16, 18.36, 121.64);
            AddLocation(area, "inner-passage", "Northern Passage Objective", LocationKind.Objective, 11, 9, 20.25, 121.75);
            area.Regions.Add(Region("blue-west", "Blue Western Entry", Enumerable.Range(5, 9).Select(r => new HexCoord(0, r))));
            area.Regions.Add(Region("red-east", "Red Eastern Entry", Enumerable.Range(5, 9).Select(r => new HexCoord(23, r))));
            area.Regions.Add(Region("objective-zone", "Luzon Strait Objective Zone", new[] { area.Objective }));
            area.RestrictedAreas.Add(Region("prototype-safety-zone", "Prototype Safety Exclusion", new[] { new HexCoord(6, 6), new HexCoord(6, 7) }));

            var scenario = new ScenarioDefinition
            {
                Id = "northern-gateway",
                DisplayName = "Northern Gateway",
                Summary = "Fictional forces contest the Luzon Strait and its northern approaches.",
                Horizon = 24,
                Weather = "Maritime haze",
                WeatherSeverity = 1,
                SpecialRules = "Real geography with fictional factions; prototype logistics and off-map entry regions.",
                VictoryConditions = "Control the Northern Passage at T24; preserve the carrier; score enemy damage.",
                Area = area
            };
            scenario.DeploymentRegions.Add(area.Regions[0]);
            scenario.DeploymentRegions.Add(area.Regions[1]);
            scenario.ExitRegions.Add(area.Regions[0]);
            scenario.ExitRegions.Add(area.Regions[1]);
            scenario.LogisticsRegions.Add(Region("basco-logistics", "Basco Logistics Access", new[] { new HexCoord(11, 10) }));

            AddFormation(scenario, "B-CV-L", "CSG Horizon", Side.Blue, FormationKind.CarrierGroup, 2, 10, 1, 2, 3, 1, 4, 3, 2);
            AddFormation(scenario, "B-SG-L", "SAG Lantern", Side.Blue, FormationKind.SurfaceGroup, 3, 7, 0, 2, 2, 2, 3, 3, 2);
            AddFormation(scenario, "B-SS-L", "SSN Wayfinder", Side.Blue, FormationKind.Submarine, 4, 13, 0, 2, 3, -1, 3, 2, 3);
            AddFormation(scenario, "B-AG-L", "Air Group Meridian", Side.Blue, FormationKind.AirGroup, 5, 9, 1, 3, 3, 2, 3, 2, 2);
            AddFormation(scenario, "R-CV-L", "CVG Typhoon", Side.Red, FormationKind.CarrierGroup, 21, 8, 1, 2, 3, 1, 4, 3, 2);
            AddFormation(scenario, "R-SG-L", "SAG Monsoon", Side.Red, FormationKind.SurfaceGroup, 20, 12, 0, 2, 2, 2, 3, 3, 2);
            AddFormation(scenario, "R-SS-L", "Submarine Shade", Side.Red, FormationKind.Submarine, 18, 6, 0, 2, 3, -1, 3, 2, 3);
            AddFormation(scenario, "R-AG-L", "Air Group Squall", Side.Red, FormationKind.AirGroup, 19, 10, 1, 3, 3, 2, 3, 2, 2);
            scenario.Contacts.Add(new ContactDefinition { Owner = Side.Blue, TargetId = "R-SG-L", Q = 17, R = 11, Location = LocationQuality.Low, Identity = IdentityQuality.Unknown, Age = 2 });
            scenario.Contacts.Add(new ContactDefinition { Owner = Side.Red, TargetId = "B-CV-L", Q = 6, R = 10, Location = LocationQuality.Low, Identity = IdentityQuality.General, Age = 2 });
            return scenario;
        }

        private static void AddTerrain(OperationalAreaDefinition area, OperationalTerrain terrain, string name, params (int q, int r)[] hexes)
        {
            foreach ((int q, int r) in hexes)
            {
                var hex = new HexCoord(q, r);
                area.Terrain.RemoveAll(item => item.Hex.Equals(hex));
                area.Terrain.Add(new TerrainHexDefinition { Q = q, R = r, Terrain = terrain, Name = name });
            }
        }

        private static void AddBlock(OperationalAreaDefinition area, OperationalTerrain terrain, string name, int qMin, int rMin, int qMax, int rMax)
        {
            for (int q = qMin; q <= qMax; q++) for (int r = rMin; r <= rMax; r++) area.Terrain.Add(new TerrainHexDefinition { Q = q, R = r, Terrain = terrain, Name = name });
        }

        private static void AddLocation(OperationalAreaDefinition area, string id, string name, LocationKind kind, int q, int r, double latitude, double longitude)
        {
            area.Locations.Add(new OperationalLocationDefinition { Id = id, Name = name, Kind = kind, Q = q, R = r, Geographic = new GeographicPoint { Latitude = latitude, Longitude = longitude } });
        }

        private static OperationalRegionDefinition Region(string id, string name, IEnumerable<HexCoord> hexes) => new OperationalRegionDefinition { Id = id, Name = name, Hexes = hexes.ToList() };

        private static void AddFormation(ScenarioDefinition scenario, string id, string name, Side side, FormationKind kind, int q, int r, int ready, int move, int search, int signature, int strike, int defense, int command)
        {
            scenario.Formations.Add(new FormationDefinition
            {
                Id = id, Name = name, Side = side, Kind = kind, Q = q, R = r, ReadyTime = ready,
                Ratings = new Ratings { Move = move, Search = search, Signature = signature, Strike = strike, Defense = defense, Asw = 2, Command = command }
            });
        }
    }

    public static class OperationalDataValidator
    {
        public static List<string> Validate(ScenarioDefinition scenario)
        {
            var errors = new List<string>();
            if (scenario == null) { errors.Add("Scenario is null."); return errors; }
            if (string.IsNullOrWhiteSpace(scenario.Id)) errors.Add("Scenario ID is required.");
            if (scenario.Area == null) { errors.Add("Operational area is required."); return errors; }
            if (string.IsNullOrWhiteSpace(scenario.Area.Id) || string.IsNullOrWhiteSpace(scenario.Area.DisplayName) || string.IsNullOrWhiteSpace(scenario.Area.Version) || string.IsNullOrWhiteSpace(scenario.Area.Attribution)) errors.Add("Operational-area identity, version, and attribution are required.");
            if (scenario.Horizon < 1 || string.IsNullOrWhiteSpace(scenario.Weather) || string.IsNullOrWhiteSpace(scenario.SpecialRules) || string.IsNullOrWhiteSpace(scenario.VictoryConditions)) errors.Add("Scenario horizon, weather, special rules, and victory conditions are required.");
            if (scenario.Area.Width < 1 || scenario.Area.Height < 1) errors.Add("Operational area dimensions must be positive.");
            if (scenario.Area.NauticalMilesPerHex < 1) errors.Add("Hex scale must be positive.");
            if (scenario.Area.Presentation == null || string.IsNullOrWhiteSpace(scenario.Area.Presentation.LightingProfile) || string.IsNullOrWhiteSpace(scenario.Area.Presentation.WaterProfile) || string.IsNullOrWhiteSpace(scenario.Area.Presentation.TerrainAssetSet)) errors.Add("Presentation assets or profiles are incomplete.");
            if (scenario.Area.Projection == MapProjectionKind.LocalAzimuthalEquidistant && string.IsNullOrWhiteSpace(scenario.Area.CoastlineResource)) errors.Add("Geographic operational areas require a coastline polygon resource.");
            if (!scenario.Area.Contains(scenario.Area.Objective)) errors.Add("Objective is outside the operational area.");
            foreach (IGrouping<string, FormationDefinition> duplicate in scenario.Formations.GroupBy(f => f.Id).Where(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1))
                errors.Add("Formation ID is missing or duplicated: " + duplicate.Key);
            foreach (FormationDefinition formation in scenario.Formations.Where(f => !scenario.Area.Contains(new HexCoord(f.Q, f.R))))
                errors.Add($"Formation {formation.Id} is outside the operational area.");
            foreach (TerrainHexDefinition terrain in scenario.Area.Terrain.Where(t => !scenario.Area.Contains(t.Hex)))
                errors.Add($"Terrain hex {terrain.Hex} is outside the operational area.");
            foreach (IGrouping<HexCoord, TerrainHexDefinition> duplicate in scenario.Area.Terrain.GroupBy(item => item.Hex).Where(group => group.Count() > 1)) errors.Add("Overlapping terrain definitions at " + duplicate.Key);
            foreach (IGrouping<string, OperationalLocationDefinition> duplicate in scenario.Area.Locations.GroupBy(location => location.Id).Where(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1))
                errors.Add("Location ID is missing or duplicated: " + duplicate.Key);
            foreach (OperationalLocationDefinition location in scenario.Area.Locations.Where(location => !scenario.Area.Contains(location.Hex))) errors.Add("Location is outside the operational area: " + location.Id);
            IEnumerable<OperationalRegionDefinition> allScenarioRegions = scenario.DeploymentRegions.Concat(scenario.ReinforcementRegions).Concat(scenario.ExitRegions).Concat(scenario.LogisticsRegions).Concat(scenario.Area.Regions).Concat(scenario.Area.RestrictedAreas).Distinct();
            foreach (IGrouping<string, OperationalRegionDefinition> duplicate in allScenarioRegions.GroupBy(region => region.Id).Where(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1)) errors.Add("Region ID is missing or duplicated: " + duplicate.Key);
            foreach (OperationalRegionDefinition region in allScenarioRegions)
            {
                if (string.IsNullOrWhiteSpace(region.Id)) errors.Add("Region ID is missing.");
                if (region.Hexes.Count == 0) errors.Add("Region has no hexes: " + region.Id);
                if (region.Hexes.Any(hex => !scenario.Area.Contains(hex))) errors.Add("Region contains an invalid hex: " + region.Id);
                bool requiresNavigableWater = scenario.DeploymentRegions.Contains(region) || scenario.ReinforcementRegions.Contains(region) || scenario.ExitRegions.Contains(region);
                if (requiresNavigableWater && region.Hexes.All(hex => scenario.Area.TerrainAt(hex) == OperationalTerrain.Land)) errors.Add("Region has no navigable access hex: " + region.Id);
                bool isLogistics = scenario.LogisticsRegions.Contains(region);
                bool anchoredByFacility = region.Hexes.Any(hex => scenario.Area.Locations.Any(location => location.Hex.Equals(hex) && (location.Kind == LocationKind.Port || location.Kind == LocationKind.Airfield || location.Kind == LocationKind.Anchorage)));
                if (isLogistics && !anchoredByFacility) errors.Add("Logistics region has no port, airfield, or anchorage: " + region.Id);
                if ((scenario.DeploymentRegions.Contains(region) || scenario.ReinforcementRegions.Contains(region)) && !region.Hexes.Any(hex => HasWaterPath(scenario.Area, hex, scenario.Area.Objective))) errors.Add("Setup region cannot reach the objective by water: " + region.Id);
            }
            foreach (ContactDefinition contact in scenario.Contacts)
            {
                if (!scenario.Formations.Any(f => f.Id == contact.TargetId)) errors.Add("Contact target does not exist: " + contact.TargetId);
                if (!scenario.Area.Contains(new HexCoord(contact.Q, contact.R))) errors.Add("Contact is outside the operational area: " + contact.TargetId);
            }
            return errors;
        }

        private static bool HasWaterPath(OperationalAreaDefinition area, HexCoord start, HexCoord destination)
        {
            if (area.TerrainAt(start) == OperationalTerrain.Land || area.TerrainAt(destination) == OperationalTerrain.Land) return false;
            var queue = new Queue<HexCoord>();
            var visited = new HashSet<HexCoord>();
            queue.Enqueue(start); visited.Add(start);
            while (queue.Count > 0)
            {
                HexCoord current = queue.Dequeue();
                if (current.Equals(destination)) return true;
                for (int q = Math.Max(0, current.Q - 1); q <= Math.Min(area.Width - 1, current.Q + 1); q++)
                {
                    for (int r = Math.Max(0, current.R - 1); r <= Math.Min(area.Height - 1, current.R + 1); r++)
                    {
                        HexCoord next = new HexCoord(q, r);
                        if (HexCoord.Distance(current, next) == 1 && area.Contains(next) && area.TerrainAt(next) != OperationalTerrain.Land && visited.Add(next)) queue.Enqueue(next);
                    }
                }
            }
            return false;
        }

    }

    public static class OperationalDataMigration
    {
        public const int CurrentSchemaVersion = 2;

        public static ScenarioDefinition Migrate(ScenarioDefinition scenario)
        {
            if (scenario == null || scenario.Area == null) throw new ArgumentException("Cannot migrate an empty scenario or operational area.");
            if (scenario.SchemaVersion < 1 || scenario.Area.SchemaVersion < 1) throw new ArgumentException("Unsupported pre-release operational-data schema.");
            if (scenario.SchemaVersion > CurrentSchemaVersion || scenario.Area.SchemaVersion > CurrentSchemaVersion) throw new ArgumentException("Operational data was created by a newer game version.");
            if (scenario.Area.Presentation == null) scenario.Area.Presentation = new PresentationProfileDefinition();
            if (scenario.DeploymentRegions == null) scenario.DeploymentRegions = new List<OperationalRegionDefinition>();
            if (scenario.ReinforcementRegions == null) scenario.ReinforcementRegions = new List<OperationalRegionDefinition>();
            if (scenario.ExitRegions == null) scenario.ExitRegions = new List<OperationalRegionDefinition>();
            if (scenario.LogisticsRegions == null) scenario.LogisticsRegions = new List<OperationalRegionDefinition>();
            if (scenario.Area.RestrictedAreas == null) scenario.Area.RestrictedAreas = new List<OperationalRegionDefinition>();
            scenario.SchemaVersion = CurrentSchemaVersion;
            scenario.Area.SchemaVersion = CurrentSchemaVersion;
            return scenario;
        }
    }

    public static class Geodesy
    {
        public static double NauticalMiles(GeographicPoint first, GeographicPoint second)
        {
            const double earthRadiusNm = 3440.065;
            double lat1 = first.Latitude * Math.PI / 180.0;
            double lat2 = second.Latitude * Math.PI / 180.0;
            double deltaLat = (second.Latitude - first.Latitude) * Math.PI / 180.0;
            double deltaLon = (second.Longitude - first.Longitude) * Math.PI / 180.0;
            double sinLat = Math.Sin(deltaLat / 2.0), sinLon = Math.Sin(deltaLon / 2.0);
            double a = sinLat * sinLat + Math.Cos(lat1) * Math.Cos(lat2) * sinLon * sinLon;
            return earthRadiusNm * 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        }
    }
}
