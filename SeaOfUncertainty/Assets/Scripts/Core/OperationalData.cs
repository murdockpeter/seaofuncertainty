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
        public float SeaState = .35f;
        public float CloudCover = .18f;
        public float HazeDensity = .22f;
        public string WeatherPreset = "Clear";
        public float Precipitation;
        public string ElevationResource;
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
        public int SchemaVersion = 3;
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
        public WeaponInventoryState Weapons = new WeaponInventoryState();
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
    public sealed class SensorRangeDefinition
    {
        public FormationKind Kind;
        public int Passive;
        public int Active;
        public int Focused;

        public int Range(SearchMode mode) => mode == SearchMode.Passive ? Passive : mode == SearchMode.Active ? Active : Focused;
    }

    [Serializable]
    public sealed class MissileDefenseProfileDefinition
    {
        public FormationKind Kind;
        public int OuterLayer;
        public int AreaLayer;
        public int PointLayer;

        public int Modifier(Salvo salvo)
            => PointLayer + (salvo == Salvo.Heavy ? 0 : AreaLayer) + (salvo == Salvo.Light ? OuterLayer : 0);
    }

    public enum ScheduledEventKind { Reinforcement, WeatherChange, CommandArchitectureChange }

    [Serializable]
    public sealed class ScheduledScenarioEventDefinition
    {
        public string Id;
        public int Time;
        public ScheduledEventKind Kind;
        public Side Side;
        public string RegionId;
        public string Text;
        public string Weather;
        public int WeatherSeverity;
        public CommandArchitecture CommandArchitecture;
        public FormationDefinition Reinforcement;
    }

    [Serializable]
    public sealed class ScenarioDefinition
    {
        public int SchemaVersion = 4;
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
        public List<SensorRangeDefinition> SensorRanges = new List<SensorRangeDefinition>();
        public List<MissileDefenseProfileDefinition> MissileDefenseProfiles = new List<MissileDefenseProfileDefinition>();
        public List<ScheduledScenarioEventDefinition> ScheduledEvents = new List<ScheduledScenarioEventDefinition>();
        public CommandArchitecture BlueCommandArchitecture = CommandArchitecture.MissionCommand;
        public CommandArchitecture RedCommandArchitecture = CommandArchitecture.MissionCommand;
        public List<OperationalRegionDefinition> DeploymentRegions = new List<OperationalRegionDefinition>();
        public List<OperationalRegionDefinition> ReinforcementRegions = new List<OperationalRegionDefinition>();
        public List<OperationalRegionDefinition> ExitRegions = new List<OperationalRegionDefinition>();
        public List<OperationalRegionDefinition> LogisticsRegions = new List<OperationalRegionDefinition>();
        public List<ScenarioObjectiveDefinition> Objectives = new List<ScenarioObjectiveDefinition>();
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

            AddLocation(area, "west-haven", "West Haven", LocationKind.Port, 1, 2, 0, 0);
            AddLocation(area, "west-haven-airfield", "West Haven Airfield", LocationKind.Airfield, 1, 1, 0, 0);
            AddLocation(area, "east-haven", "East Haven", LocationKind.Port, 10, 7, 0, 0);
            AddLocation(area, "east-haven-airfield", "East Haven Airfield", LocationKind.Airfield, 10, 8, 0, 0);
            area.Regions.Add(Region("blue-transit-zone", "Eastern Transit Zone", Enumerable.Range(0, area.Height).Select(r => new HexCoord(9, r)).Where(hex => area.TerrainAt(hex) != OperationalTerrain.Land)));
            area.Regions.Add(Region("red-transit-zone", "Western Transit Zone", Enumerable.Range(0, area.Height).Select(r => new HexCoord(2, r)).Where(hex => area.TerrainAt(hex) != OperationalTerrain.Land)));
            area.Regions.Add(Region("blue-withdrawal-zone", "Western Withdrawal Zone", Enumerable.Range(0, area.Height).SelectMany(r => new[] { new HexCoord(1, r), new HexCoord(2, r) }).Where(hex => area.TerrainAt(hex) != OperationalTerrain.Land)));
            area.Regions.Add(Region("red-withdrawal-zone", "Eastern Withdrawal Zone", Enumerable.Range(0, area.Height).SelectMany(r => new[] { new HexCoord(9, r), new HexCoord(10, r) }).Where(hex => area.TerrainAt(hex) != OperationalTerrain.Land)));

            var scenario = new ScenarioDefinition
            {
                Id = "meridian-veil",
                DisplayName = "Meridian Veil",
                Summary = "Two naval forces converge on the Inner Sea.",
                Horizon = 16,
                Area = area,
                Weather = "Clear",
                SpecialRules = "Automatic Defend reaction; fixed paired-test deployment.",
                VictoryConditions = "Score operational control, transit, escort, denial, and carrier preservation/withdrawal at T16. Damage breaks tied operational scores."
            };
            AddOperationalObjectives(scenario, "blue-transit-zone", "red-transit-zone", "blue-withdrawal-zone", "red-withdrawal-zone");
            scenario.LogisticsRegions.Add(Region("west-haven-logistics", "West Haven Logistics Access", new[] { new HexCoord(1, 2), new HexCoord(1, 1) }));
            scenario.LogisticsRegions.Add(Region("east-haven-logistics", "East Haven Logistics Access", new[] { new HexCoord(10, 7), new HexCoord(10, 8) }));

            AddFormation(scenario, "B-CV", "CSG Resolute", Side.Blue, FormationKind.CarrierGroup, 1, 4, 1, 2, 3, 1, 4, 3, 1);
            AddFormation(scenario, "B-SG", "SAG Valiant", Side.Blue, FormationKind.SurfaceGroup, 2, 7, 0, 2, 2, 2, 3, 3, 2);
            AddFormation(scenario, "B-SS", "SSN Nightfall", Side.Blue, FormationKind.Submarine, 0, 7, 0, 2, 3, -1, 3, 2, 3);
            AddFormation(scenario, "B-AG", "Air Group Kestrel", Side.Blue, FormationKind.AirGroup, 3, 2, 1, 3, 3, 2, 3, 2, 2);
            AddFormation(scenario, "R-CV", "CVG Tempest", Side.Red, FormationKind.CarrierGroup, 10, 5, 1, 2, 3, 1, 4, 3, 1);
            AddFormation(scenario, "R-SG", "Surface Action Two", Side.Red, FormationKind.SurfaceGroup, 9, 2, 0, 2, 2, 2, 3, 3, 2);
            AddFormation(scenario, "R-SS", "Hunter Submarine", Side.Red, FormationKind.Submarine, 11, 2, 0, 2, 3, -1, 3, 2, 3);
            AddFormation(scenario, "R-AG", "Air Group Ember", Side.Red, FormationKind.AirGroup, 8, 8, 1, 3, 3, 2, 3, 2, 2);
            AddFormation(scenario, "B-LG", "Fleet Train Haven", Side.Blue, FormationKind.LogisticsGroup, 1, 3, 2, 1, 1, 3, 1, 2, 1);
            AddFormation(scenario, "R-LG", "Fleet Train Harbor", Side.Red, FormationKind.LogisticsGroup, 10, 6, 2, 1, 1, 3, 1, 2, 1);
            scenario.Contacts.Add(new ContactDefinition { Owner = Side.Blue, TargetId = "R-SG", Q = 8, R = 3, Location = LocationQuality.Low, Identity = IdentityQuality.Unknown, Age = 2 });
            scenario.Contacts.Add(new ContactDefinition { Owner = Side.Red, TargetId = "B-CV", Q = 2, R = 4, Location = LocationQuality.Medium, Identity = IdentityQuality.General, Age = 1 });
            AddPrototypeSensorRanges(scenario);
            AddPrototypeMissileDefenseProfiles(scenario);
            return scenario;
        }

        public static IReadOnlyList<ScenarioDefinition> All() => ScenarioAuthoringCatalog.Apply(new[] { MeridianVeil(), LuzonStrait() });

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
                Presentation = new PresentationProfileDefinition { CameraPitch = 58f, CameraYaw = 0, CameraZoomMultiplier = 1.65f, LightingProfile = "MaritimeHaze", WaterProfile = "PhilippineSea", TerrainAssetSet = "LuzonMvp", TimeOfDay = 16f, Visibility = .72f, SeaState = .42f, CloudCover = .28f, HazeDensity = .38f, WeatherPreset = "Haze" }
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
                VictoryConditions = "Score operational control, transit, escort, denial, and carrier preservation/withdrawal at T24. Damage breaks tied operational scores.",
                Area = area
            };
            AddOperationalObjectives(scenario, "red-east", "blue-west", "blue-west", "red-east");
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
            AddFormation(scenario, "B-LG-L", "Fleet Train West", Side.Blue, FormationKind.LogisticsGroup, 2, 12, 2, 1, 1, 3, 1, 2, 1);
            AddFormation(scenario, "R-LG-L", "Fleet Train East", Side.Red, FormationKind.LogisticsGroup, 21, 10, 2, 1, 1, 3, 1, 2, 1);
            scenario.Contacts.Add(new ContactDefinition { Owner = Side.Blue, TargetId = "R-SG-L", Q = 17, R = 11, Location = LocationQuality.Low, Identity = IdentityQuality.Unknown, Age = 2 });
            scenario.Contacts.Add(new ContactDefinition { Owner = Side.Red, TargetId = "B-CV-L", Q = 6, R = 10, Location = LocationQuality.Low, Identity = IdentityQuality.General, Age = 2 });
            AddPrototypeSensorRanges(scenario);
            AddPrototypeMissileDefenseProfiles(scenario);
            scenario.BlueCommandArchitecture = CommandArchitecture.Distributed;
            scenario.RedCommandArchitecture = CommandArchitecture.Centralized;
            scenario.ReinforcementRegions.Add(area.Regions[0]);
            scenario.ReinforcementRegions.Add(area.Regions[1]);
            scenario.ScheduledEvents.Add(new ScheduledScenarioEventDefinition
            {
                Id = "weather-clears", Time = 12, Kind = ScheduledEventKind.WeatherChange, Text = "The maritime haze begins to clear.", Weather = "Clear", WeatherSeverity = 0
            });
            scenario.ScheduledEvents.Add(new ScheduledScenarioEventDefinition
            {
                Id = "blue-reinforcement", Time = 8, Kind = ScheduledEventKind.Reinforcement, Side = Side.Blue, RegionId = "blue-west", Text = "Blue reinforcement enters from the western approach.",
                Reinforcement = CreateFormation("B-SG-R1", "SAG Relay", Side.Blue, FormationKind.SurfaceGroup, 0, 7, 8, 2, 2, 2, 3, 3, 2)
            });
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

        private static void AddOperationalObjectives(ScenarioDefinition scenario, string blueTransit, string redTransit, string blueWithdrawal, string redWithdrawal)
        {
            scenario.Objectives.Add(new ScenarioObjectiveDefinition { Id = "control", Title = "Sole control of the operational objective", Kind = ScenarioObjectiveKind.Control, Points = 6, Radius = Rules.ControlRadius });
            scenario.Objectives.Add(new ScenarioObjectiveDefinition { Id = "transit", Title = "Transit a combat-capable naval formation into the opposing approach", Kind = ScenarioObjectiveKind.Transit, Points = 3, BlueRegionId = blueTransit, RedRegionId = redTransit });
            scenario.Objectives.Add(new ScenarioObjectiveDefinition { Id = "escort", Title = "Maintain a combat-capable naval escort within two hexes of the carrier", Kind = ScenarioObjectiveKind.Escort, Points = 3, Radius = 2 });
            scenario.Objectives.Add(new ScenarioObjectiveDefinition { Id = "denial", Title = "Deny enemy combat-capable naval presence within two hexes of the objective", Kind = ScenarioObjectiveKind.Denial, Points = 2, Radius = 2 });
            scenario.Objectives.Add(new ScenarioObjectiveDefinition { Id = "withdrawal", Title = "Preserve the carrier or withdraw it after Heavy damage", Kind = ScenarioObjectiveKind.Withdrawal, Points = 2, BlueRegionId = blueWithdrawal, RedRegionId = redWithdrawal });
        }

        private static void AddFormation(ScenarioDefinition scenario, string id, string name, Side side, FormationKind kind, int q, int r, int ready, int move, int search, int signature, int strike, int defense, int command)
        {
            scenario.Formations.Add(CreateFormation(id, name, side, kind, q, r, ready, move, search, signature, strike, defense, command));
        }

        private static FormationDefinition CreateFormation(string id, string name, Side side, FormationKind kind, int q, int r, int ready, int move, int search, int signature, int strike, int defense, int command)
        {
            int asw = kind == FormationKind.SurfaceGroup || kind == FormationKind.Submarine ? 3 : kind == FormationKind.LogisticsGroup ? 0 : kind == FormationKind.CarrierGroup ? 1 : 2;
            int ew = kind == FormationKind.CarrierGroup || kind == FormationKind.AirGroup ? 2 : kind == FormationKind.LogisticsGroup ? 0 : 1;
            int cyber = kind == FormationKind.CarrierGroup ? 2 : kind == FormationKind.LogisticsGroup ? 0 : 1;
            int light = kind == FormationKind.LogisticsGroup ? 1 : kind == FormationKind.Submarine ? 3 : 4;
            int standard = kind == FormationKind.LogisticsGroup ? 0 : kind == FormationKind.Submarine || kind == FormationKind.AirGroup ? 2 : 3;
            int heavy = kind == FormationKind.LogisticsGroup ? 0 : 1;
            return new FormationDefinition
            {
                Id = id, Name = name, Side = side, Kind = kind, Q = q, R = r, ReadyTime = ready,
                Ratings = new Ratings { Move = move, Search = search, Signature = signature, Strike = strike, Defense = defense, Asw = asw, Command = command, ElectronicWarfare = ew, Cyber = cyber },
                Weapons = new WeaponInventoryState { Light = light, MaxLight = light, Standard = standard, MaxStandard = standard, Heavy = heavy, MaxHeavy = heavy }
            };
        }

        public static void AddPrototypeSensorRanges(ScenarioDefinition scenario)
        {
            if (scenario.SensorRanges == null) scenario.SensorRanges = new List<SensorRangeDefinition>();
            if (scenario.SensorRanges.Count > 0) return;
            scenario.SensorRanges.Add(new SensorRangeDefinition { Kind = FormationKind.CarrierGroup, Passive = 8, Active = 10, Focused = 12 });
            scenario.SensorRanges.Add(new SensorRangeDefinition { Kind = FormationKind.SurfaceGroup, Passive = 7, Active = 9, Focused = 11 });
            scenario.SensorRanges.Add(new SensorRangeDefinition { Kind = FormationKind.Submarine, Passive = 6, Active = 8, Focused = 10 });
            scenario.SensorRanges.Add(new SensorRangeDefinition { Kind = FormationKind.AirGroup, Passive = 10, Active = 12, Focused = 14 });
            scenario.SensorRanges.Add(new SensorRangeDefinition { Kind = FormationKind.LogisticsGroup, Passive = 4, Active = 6, Focused = 8 });
        }

        public static void AddPrototypeMissileDefenseProfiles(ScenarioDefinition scenario)
        {
            if (scenario.MissileDefenseProfiles == null) scenario.MissileDefenseProfiles = new List<MissileDefenseProfileDefinition>();
            if (scenario.MissileDefenseProfiles.Count > 0) return;
            scenario.MissileDefenseProfiles.Add(new MissileDefenseProfileDefinition { Kind = FormationKind.CarrierGroup, OuterLayer = 1, AreaLayer = 1, PointLayer = 1 });
            scenario.MissileDefenseProfiles.Add(new MissileDefenseProfileDefinition { Kind = FormationKind.SurfaceGroup, OuterLayer = 1, AreaLayer = 1, PointLayer = 1 });
            scenario.MissileDefenseProfiles.Add(new MissileDefenseProfileDefinition { Kind = FormationKind.Submarine, OuterLayer = 0, AreaLayer = 0, PointLayer = 0 });
            scenario.MissileDefenseProfiles.Add(new MissileDefenseProfileDefinition { Kind = FormationKind.AirGroup, OuterLayer = 0, AreaLayer = 1, PointLayer = 1 });
            scenario.MissileDefenseProfiles.Add(new MissileDefenseProfileDefinition { Kind = FormationKind.LogisticsGroup, OuterLayer = 0, AreaLayer = 0, PointLayer = 1 });
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
            if (scenario.Objectives == null || scenario.Objectives.Count == 0) errors.Add("At least one scored scenario objective is required.");
            else
            {
                foreach (IGrouping<string, ScenarioObjectiveDefinition> duplicate in scenario.Objectives.GroupBy(objective => objective.Id).Where(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1)) errors.Add("Scenario objective ID is missing or duplicated: " + duplicate.Key);
                foreach (ScenarioObjectiveDefinition objective in scenario.Objectives.Where(objective => objective.Points <= 0 || string.IsNullOrWhiteSpace(objective.Title))) errors.Add("Scenario objective requires a title and positive points: " + objective.Id);
                HashSet<string> regionIds = new HashSet<string>((scenario.Area.Regions ?? new List<OperationalRegionDefinition>())
                    .Concat(scenario.DeploymentRegions ?? new List<OperationalRegionDefinition>()).Concat(scenario.ReinforcementRegions ?? new List<OperationalRegionDefinition>())
                    .Concat(scenario.ExitRegions ?? new List<OperationalRegionDefinition>()).Concat(scenario.LogisticsRegions ?? new List<OperationalRegionDefinition>()).Select(region => region.Id));
                foreach (ScenarioObjectiveDefinition objective in scenario.Objectives.Where(objective => objective.Kind == ScenarioObjectiveKind.Transit || objective.Kind == ScenarioObjectiveKind.Withdrawal))
                    if (!regionIds.Contains(objective.BlueRegionId) || !regionIds.Contains(objective.RedRegionId)) errors.Add("Scenario objective references missing Blue/Red regions: " + objective.Id);
            }
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
            if (scenario.SensorRanges == null || scenario.SensorRanges.Count != Enum.GetValues(typeof(FormationKind)).Length) errors.Add("One sensor-range profile is required for every Formation kind.");
            else
            {
                foreach (FormationKind kind in Enum.GetValues(typeof(FormationKind)))
                    if (scenario.SensorRanges.Count(range => range.Kind == kind) != 1) errors.Add("Sensor-range profile is missing or duplicated: " + kind);
                foreach (SensorRangeDefinition range in scenario.SensorRanges.Where(range => range.Passive < 1 || range.Active < range.Passive || range.Focused < range.Active)) errors.Add("Sensor ranges must be positive and ordered Passive <= Active <= Focused: " + range.Kind);
            }
            if (scenario.MissileDefenseProfiles == null || scenario.MissileDefenseProfiles.Count != Enum.GetValues(typeof(FormationKind)).Length) errors.Add("One missile-defense profile is required for every Formation kind.");
            else foreach (FormationKind kind in Enum.GetValues(typeof(FormationKind)))
                if (scenario.MissileDefenseProfiles.Count(profile => profile.Kind == kind) != 1) errors.Add("Missile-defense profile is missing or duplicated: " + kind);
            foreach (ScheduledScenarioEventDefinition scheduled in scenario.ScheduledEvents ?? new List<ScheduledScenarioEventDefinition>())
            {
                if (string.IsNullOrWhiteSpace(scheduled.Id) || scheduled.Time < 0 || scheduled.Time > scenario.Horizon) errors.Add("Scheduled event requires a stable ID and in-horizon time.");
                if (scheduled.Kind == ScheduledEventKind.Reinforcement && (scheduled.Reinforcement == null || !allScenarioRegions.Any(region => region.Id == scheduled.RegionId))) errors.Add("Reinforcement event requires a formation and valid entry region: " + scheduled.Id);
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
        public const int CurrentSchemaVersion = 5;

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
            if (scenario.SensorRanges == null) scenario.SensorRanges = new List<SensorRangeDefinition>();
            if (scenario.MissileDefenseProfiles == null) scenario.MissileDefenseProfiles = new List<MissileDefenseProfileDefinition>();
            if (scenario.ScheduledEvents == null) scenario.ScheduledEvents = new List<ScheduledScenarioEventDefinition>();
            if (scenario.Objectives == null) scenario.Objectives = new List<ScenarioObjectiveDefinition>();
            ScenarioCatalog.AddPrototypeSensorRanges(scenario);
            ScenarioCatalog.AddPrototypeMissileDefenseProfiles(scenario);
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
