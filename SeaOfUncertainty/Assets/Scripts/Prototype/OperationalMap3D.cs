using System;
using System.Collections.Generic;
using System.Linq;
using SeaOfUncertainty.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SeaOfUncertainty.Prototype
{
    public enum OperationalEffectKind { SearchSweep, Detection, MovementWake, Launch, Interception, Impact, Damage }

    public sealed class OperationalMap3D : IDisposable
    {
        private const int MapLayer = 30;
        private const float HexRadius = 1f;
        private const float WaterSurfaceY = -.08f;
        private const float ShoalSurfaceY = .015f;
        private readonly OperationalAreaDefinition area;
        private readonly GameObject root;
        private readonly Scene mapScene;
        private readonly Camera camera;
        private readonly RenderTexture targetTexture;
        private readonly Dictionary<HexCoord, LineRenderer> hexLines = new Dictionary<HexCoord, LineRenderer>();
        private readonly List<GameObject> stateObjects = new List<GameObject>();
        private readonly List<Mesh> generatedMeshes = new List<Mesh>();
        private readonly Dictionary<string, HexCoord> presentedPositions = new Dictionary<string, HexCoord>();
        private readonly List<Motion> motions = new List<Motion>();
        private readonly List<LodPair> lodPairs = new List<LodPair>();
        private readonly Stack<GameObject> effectPool = new Stack<GameObject>();
        private readonly Stack<GameObject> markerPool = new Stack<GameObject>();
        private readonly List<EffectInstance> activeEffects = new List<EffectInstance>();
        private readonly Material lineMaterial;
        private readonly Material waterMaterial;
        private readonly Material landMaterial;
        private readonly Material littoralMaterial;
        private readonly Material blueMaterial;
        private readonly Material redMaterial;
        private readonly Material contactMaterial;
        private readonly Material warningMaterial;
        private LineRenderer hoverRing;
        private LineRenderer previewLine;
        private LineRenderer objectiveRing;
        private Vector3 focus;
        private float yaw;
        private float distance;
        private float pitch = 58f;
        private PrototypeGame game;
        private ToolkitActionMode actionMode;
        private MoveMode moveMode;
        private SearchMode searchMode;
        private Salvo salvo;
        private bool reducedMotion;

        private sealed class Motion { public Transform Transform; public Vector3 Start; public Vector3 End; public float Progress; }
        private sealed class LodPair { public GameObject Detail; public GameObject Symbol; }
        private sealed class EffectInstance { public GameObject Object; public Vector3 BaseScale; public float Age; public float Duration; }

        public RenderTexture Texture => targetTexture;
        public float Heading => Mathf.Repeat(yaw, 360f);
        public int VisibleFormationCount { get; private set; }
        public int VisibleContactCount { get; private set; }
        public int ActiveEffectCount => activeEffects.Count;
        public int PooledEffectCount => effectPool.Count;
        public int PooledMarkerCount => markerPool.Count;
        public int CoastlinePolygonCount { get; private set; }
        public bool PermanentGridVisible => hexLines.Any(pair => pair.Value != null && pair.Value.enabled && !pair.Key.Equals(area.Objective));
        public bool ContainsRenderedName(string fragment)
        {
            if (string.IsNullOrEmpty(fragment)) return false;
            return root.GetComponentsInChildren<Transform>(true).Any(item => item.name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0);
        }
        public bool FocusWithinBounds
        {
            get
            {
                Vector3 first = HexToWorld(new HexCoord(0, 0));
                Vector3 last = HexToWorld(new HexCoord(area.Width - 1, area.Height - 1));
                float padding = area.Presentation.PanPadding + .01f;
                return focus.x >= Mathf.Min(first.x, last.x) - padding && focus.x <= Mathf.Max(first.x, last.x) + padding && focus.z >= Mathf.Min(first.z, last.z) - padding && focus.z <= Mathf.Max(first.z, last.z) + padding;
            }
        }

        public OperationalMap3D(OperationalAreaDefinition definition, int width = 1280, int height = 720)
        {
            area = definition ?? throw new ArgumentNullException(nameof(definition));
            lineMaterial = MaterialFor("CommandLine", "Sprites/Default", new Color(.2f, .74f, .82f, .38f));
            waterMaterial = MaterialFor("CommandWater", "Standard", new Color(.018f, .13f, .19f, 1f), .18f, .72f);
            landMaterial = MaterialFor("CommandLand", "Standard", new Color(.22f, .31f, .22f, 1f), .05f, .55f);
            littoralMaterial = MaterialFor("CommandLittoral", "Standard", new Color(.09f, .34f, .34f, 1f), .08f, .6f);
            blueMaterial = MaterialFor("CommandBlue", "Standard", new Color(.08f, .72f, .95f, 1f), .28f, .45f);
            redMaterial = MaterialFor("CommandRed", "Standard", new Color(.94f, .25f, .18f, 1f), .2f, .48f);
            contactMaterial = MaterialFor("CommandContact", "Standard", new Color(1f, .48f, .13f, 1f), .3f, .4f);
            warningMaterial = MaterialFor("CommandWarning", "Standard", new Color(1f, .72f, .12f, 1f), .18f, .4f);

            root = new GameObject("Operational Map 3D — " + area.DisplayName) { layer = MapLayer };
            if (Application.isPlaying)
            {
                mapScene = SceneManager.CreateScene("Operational Map — " + area.Id + " — " + Guid.NewGuid().ToString("N"));
                SceneManager.MoveGameObjectToScene(root, mapScene);
            }
            Vector3 first = HexToWorld(new HexCoord(0, 0));
            Vector3 last = HexToWorld(new HexCoord(area.Width - 1, area.Height - 1));
            focus = (first + last) * .5f;
            pitch = area.Presentation.CameraPitch;
            yaw = area.Presentation.CameraYaw;
            distance = Mathf.Max(area.Width, area.Height) * area.Presentation.CameraZoomMultiplier;

            targetTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "Operational Map 3D Render",
                antiAliasing = 4,
                useMipMap = false
            };
            targetTexture.Create();

            GameObject cameraObject = Child("Operational Camera", root.transform);
            camera = cameraObject.AddComponent<Camera>();
            camera.targetTexture = targetTexture;
            camera.clearFlags = CameraClearFlags.SolidColor;
            bool night = area.Presentation.TimeOfDay < 6f || area.Presentation.TimeOfDay > 19f;
            camera.backgroundColor = night ? new Color(.002f, .008f, .018f, 1f) : Color.Lerp(new Color(.006f, .025f, .04f, 1f), new Color(.06f, .12f, .17f, 1f), 1f - area.Presentation.Visibility);
            camera.cullingMask = 1 << MapLayer;
            camera.fieldOfView = 34f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 200f;
            camera.allowHDR = true;
            Camera mainCamera = Camera.main;
            if (mainCamera != null && mainCamera != camera) mainCamera.cullingMask &= ~(1 << MapLayer);

            GameObject lightObject = Child("Command Table Sun", root.transform);
            Light sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(.78f, .9f, 1f);
            sun.intensity = night ? .45f : Mathf.Lerp(.8f, 1.25f, area.Presentation.Visibility);
            sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            sun.cullingMask = 1 << MapLayer;

            BuildSurface();
            BuildHexGrid();
            BuildLocations();
            BuildInteractionOverlays();
            UpdateCamera();
        }

        public void SetState(PrototypeGame currentGame, ToolkitActionMode mode, MoveMode currentMoveMode, SearchMode currentSearchMode, Salvo currentSalvo, bool useReducedMotion = false)
        {
            game = currentGame;
            actionMode = mode;
            moveMode = currentMoveMode;
            searchMode = currentSearchMode;
            salvo = currentSalvo;
            reducedMotion = useReducedMotion;
            UpdateHexStyles();
            RebuildStateObjects();
            UpdateObjectiveControl();
            SetHoverHex(null);
            ClearEffects();
            if (game?.Active != null)
            {
                if (actionMode == ToolkitActionMode.Search) TriggerEffect(OperationalEffectKind.SearchSweep, game.Active.Position);
                else if (actionMode == ToolkitActionMode.Move) TriggerEffect(OperationalEffectKind.MovementWake, game.Active.Position);
                else if (actionMode == ToolkitActionMode.Strike) TriggerEffect(OperationalEffectKind.Launch, game.Active.Position);
            }
        }

        public void Tick(float deltaTime)
        {
            for (int i = motions.Count - 1; i >= 0; i--)
            {
                Motion motion = motions[i];
                if (motion.Transform == null) { motions.RemoveAt(i); continue; }
                motion.Progress = reducedMotion ? 1f : Mathf.Clamp01(motion.Progress + deltaTime * 2.5f);
                float eased = motion.Progress * motion.Progress * (3f - 2f * motion.Progress);
                motion.Transform.position = Vector3.Lerp(motion.Start, motion.End, eased);
                if (motion.Progress >= 1f) motions.RemoveAt(i);
            }
            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                EffectInstance effect = activeEffects[i];
                effect.Age += deltaTime;
                if (!reducedMotion) effect.Object.transform.localScale = effect.BaseScale * (1f + Mathf.Sin(effect.Age * 5f) * .12f);
                if (effect.Age >= effect.Duration) { ReleaseEffect(effect.Object); activeEffects.RemoveAt(i); }
            }
        }

        public void TriggerEffect(OperationalEffectKind kind, HexCoord hex)
        {
            GameObject effect = effectPool.Count > 0 ? effectPool.Pop() : Primitive(PrimitiveType.Sphere, "Pooled Operational Effect", root.transform, warningMaterial);
            effect.name = "VFX " + kind;
            effect.SetActive(true);
            effect.transform.SetParent(root.transform, false);
            effect.transform.position = HexToWorld(hex) + Vector3.up * .3f;
            Vector3 scale;
            if (kind == OperationalEffectKind.SearchSweep) scale = new Vector3(1.5f, .025f, 1.5f);
            else if (kind == OperationalEffectKind.MovementWake) scale = new Vector3(.18f, .025f, .8f);
            else if (kind == OperationalEffectKind.Launch) scale = new Vector3(.08f, .7f, .08f);
            else if (kind == OperationalEffectKind.Interception) scale = new Vector3(.42f, .08f, .42f);
            else if (kind == OperationalEffectKind.Impact || kind == OperationalEffectKind.Damage) scale = Vector3.one * .42f;
            else scale = Vector3.one * .3f;
            effect.transform.localScale = scale;
            activeEffects.Add(new EffectInstance { Object = effect, BaseScale = scale, Duration = reducedMotion ? 3600f : 1.2f });
        }

        private void ClearEffects()
        {
            foreach (EffectInstance effect in activeEffects) ReleaseEffect(effect.Object);
            activeEffects.Clear();
        }

        private void ReleaseEffect(GameObject effect)
        {
            if (effect == null) return;
            effect.SetActive(false);
            effectPool.Push(effect);
        }

        public void SetHoverHex(HexCoord? hex)
        {
            if (hoverRing == null || previewLine == null) return;
            bool visible = hex.HasValue && area.Contains(hex.Value);
            hoverRing.gameObject.SetActive(visible);
            previewLine.gameObject.SetActive(false);
            if (!visible) return;

            SetRingPositions(hoverRing, hex.Value, .82f, .13f);
            bool legalMove = actionMode == ToolkitActionMode.Move && game?.Active != null && HexCoord.Distance(game.Active.Position, hex.Value) > 0 && HexCoord.Distance(game.Active.Position, hex.Value) <= Rules.MoveAllowance(game.Active, moveMode);
            bool legalSearch = actionMode == ToolkitActionMode.Search && game?.Active != null && HexCoord.Distance(game.Active.Position, hex.Value) <= Rules.SearchRange(searchMode);
            Color hoverColor = legalMove ? new Color(.18f, 1f, .58f, 1f) : legalSearch ? new Color(.16f, .82f, 1f, 1f) : new Color(.88f, .95f, 1f, .82f);
            hoverRing.startColor = hoverRing.endColor = hoverColor;
            hoverRing.widthMultiplier = legalMove || legalSearch ? .085f : .05f;
            if (legalMove)
            {
                previewLine.gameObject.SetActive(true);
                previewLine.positionCount = 2;
                previewLine.SetPosition(0, HexToWorld(game.Active.Position) + Vector3.up * .2f);
                previewLine.SetPosition(1, HexToWorld(hex.Value) + Vector3.up * .2f);
                previewLine.startColor = new Color(.2f, 1f, .62f, .9f);
                previewLine.endColor = new Color(.2f, 1f, .62f, .35f);
            }
        }

        public Vector3 HexToWorld(HexCoord hex)
        {
            float rawX = hex.Q * HexRadius * 1.5f;
            float rawZ = (hex.R + (hex.Q & 1) * .5f) * HexRadius * Mathf.Sqrt(3f);
            float lastX = (area.Width - 1) * HexRadius * 1.5f;
            float lastZ = (area.Height - 1 + ((area.Width - 1) & 1) * .5f) * HexRadius * Mathf.Sqrt(3f);
            float x = rawX - lastX * .5f;
            float z = rawZ - lastZ * .5f;
            return new Vector3(x, 0f, z);
        }

        public bool TryPickHex(Vector2 localPosition, Rect contentRect, out HexCoord hex)
        {
            hex = default;
            if (contentRect.width < 1f || contentRect.height < 1f) return false;
            Vector3 viewport = new Vector3(localPosition.x / contentRect.width, 1f - localPosition.y / contentRect.height, 0f);
            Ray ray = camera.ViewportPointToRay(viewport);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out float enter)) return false;
            Vector3 point = ray.GetPoint(enter);
            float nearest = float.MaxValue;
            for (int q = 0; q < area.Width; q++)
            {
                for (int r = 0; r < area.Height; r++)
                {
                    HexCoord candidate = new HexCoord(q, r);
                    float sqr = (HexToWorld(candidate) - point).sqrMagnitude;
                    if (sqr < nearest) { nearest = sqr; hex = candidate; }
                }
            }
            return nearest <= 1.15f;
        }

        public bool TryWorldToHex(Vector3 worldPosition, out HexCoord hex)
        {
            hex = default;
            float nearest = float.MaxValue;
            for (int q = 0; q < area.Width; q++)
            {
                for (int r = 0; r < area.Height; r++)
                {
                    HexCoord candidate = new HexCoord(q, r);
                    float sqr = (HexToWorld(candidate) - worldPosition).sqrMagnitude;
                    if (sqr < nearest) { nearest = sqr; hex = candidate; }
                }
            }
            return nearest <= 1.15f && area.Contains(hex);
        }

        public Vector2 Project(HexCoord hex, Rect contentRect)
        {
            Vector3 viewport = camera.WorldToViewportPoint(HexToWorld(hex) + Vector3.up * .7f);
            return new Vector2(viewport.x * contentRect.width, (1f - viewport.y) * contentRect.height);
        }

        public void Pan(Vector2 pixels)
        {
            Vector3 right = camera.transform.right;
            Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
            float scale = distance * .0015f;
            focus += (-right * pixels.x - forward * pixels.y) * scale;
            ClampFocus();
            UpdateCamera();
        }

        public void Rotate(float direction)
        {
            yaw = Mathf.Round((yaw + Mathf.Sign(direction) * 30f) / 30f) * 30f;
            UpdateCamera();
        }

        public void Zoom(float wheelDelta)
        {
            distance = Mathf.Clamp(distance * (wheelDelta > 0 ? .86f : 1.16f), area.Presentation.MinimumZoom, Mathf.Max(area.Width, area.Height) * area.Presentation.MaximumZoomMultiplier);
            UpdateCamera();
            UpdateLod();
        }

        public void ResetCamera()
        {
            Vector3 first = HexToWorld(new HexCoord(0, 0));
            Vector3 last = HexToWorld(new HexCoord(area.Width - 1, area.Height - 1));
            focus = (first + last) * .5f;
            yaw = area.Presentation.CameraYaw;
            pitch = area.Presentation.CameraPitch;
            distance = Mathf.Max(area.Width, area.Height) * area.Presentation.CameraZoomMultiplier;
            UpdateCamera();
        }

        private void BuildSurface()
        {
            Vector3 first = HexToWorld(new HexCoord(0, 0));
            Vector3 last = HexToWorld(new HexCoord(area.Width - 1, area.Height - 1));
            Vector3 center = (first + last) * .5f;
            GameObject water = Primitive(PrimitiveType.Cube, "Deep Operational Water", root.transform, waterMaterial);
            water.transform.position = center + Vector3.up * (WaterSurfaceY - .24f);
            water.transform.localScale = new Vector3(last.x - first.x + 3.2f, .48f, last.z - first.z + 3.2f);

            for (int ring = 0; ring < 3; ring++)
            {
                float radius = 2.4f + ring * 2.1f;
                LineRenderer contour = Ring("Bathymetry Contour " + ring, area.Objective, radius, new Color(.15f, .48f, .58f, .18f), .018f);
                contour.transform.SetParent(root.transform, true);
                SetRingPositions(contour, area.Objective, radius, WaterSurfaceY + .015f);
            }

            bool usesPolygonCoastline = BuildPolygonCoastline(first, last);
            foreach (TerrainHexDefinition terrain in area.Terrain)
            {
                if (terrain.Terrain == OperationalTerrain.DeepWater || terrain.Terrain == OperationalTerrain.Strait) continue;
                if (usesPolygonCoastline) continue;
                Vector3 position = HexToWorld(terrain.Hex);
                if (terrain.Terrain == OperationalTerrain.Littoral)
                {
                    GameObject shoal = MeshObject("Shoal " + terrain.Hex, root.transform, littoralMaterial, CreateLandformMesh(terrain.Q, terrain.R, 1.02f, ShoalSurfaceY, WaterSurfaceY - .025f, 14));
                    shoal.transform.position = position;
                }
                else
                {
                    int reliefSeed = terrain.Name == null ? 0 : terrain.Name.Aggregate(17, (value, character) => value * 31 + character);
                    float height = .16f + Mathf.Abs(reliefSeed % 3) * .025f;
                    GameObject island = MeshObject(terrain.Name + " " + terrain.Hex, root.transform, landMaterial, CreateLandformMesh(terrain.Q, terrain.R, 1.03f, height, WaterSurfaceY - .06f, 14));
                    island.transform.position = position;
                    island.transform.rotation = Quaternion.Euler(0f, (terrain.Q * 31 + terrain.R * 19) % 30, 0f);
                    if (Mathf.Abs(terrain.Q * 17 + terrain.R * 11 + reliefSeed) % 5 == 0)
                    {
                        GameObject ridge = Primitive(PrimitiveType.Sphere, "Sparse Relief", island.transform, landMaterial);
                        ridge.transform.localPosition = new Vector3(.08f, height + .015f, -.06f);
                        ridge.transform.localScale = new Vector3(.32f, .09f, .28f);
                    }
                }
            }

            objectiveRing = Ring("Inner Sea Objective", area.Objective, 1.1f, new Color(1f, .62f, .12f, 1f), .09f);
            objectiveRing.transform.SetParent(root.transform, true);
        }

        private bool BuildPolygonCoastline(Vector3 first, Vector3 last)
        {
            if (string.IsNullOrWhiteSpace(area.CoastlineResource)) return false;
            TextAsset source = Resources.Load<TextAsset>(area.CoastlineResource);
            if (source == null) { Debug.LogError("Missing coastline resource: " + area.CoastlineResource); return false; }
            CoastlineData coastline = JsonUtility.FromJson<CoastlineData>(source.text);
            var bounds = Rect.MinMaxRect(Mathf.Min(first.x, last.x) - 1f, Mathf.Min(first.z, last.z) - .87f, Mathf.Max(first.x, last.x) + 1f, Mathf.Max(first.z, last.z) + .87f);
            Mesh mesh = CoastlinePolygonMesh.Create(coastline, bounds, .105f, out List<Vector3[]> shorelines);
            if (mesh == null) { Debug.LogError("Coastline resource produced no valid polygon mesh: " + area.CoastlineResource); return false; }
            MeshObject("Natural Earth Land", root.transform, landMaterial, mesh);
            foreach (Vector3[] shoreline in shorelines)
            {
                GameObject lineObject = Child("Natural Earth Shoreline", root.transform);
                LineRenderer line = lineObject.AddComponent<LineRenderer>();
                line.sharedMaterial = lineMaterial;
                line.useWorldSpace = true;
                line.loop = true;
                line.positionCount = shoreline.Length;
                line.widthMultiplier = .045f;
                line.startColor = line.endColor = new Color(.22f, .82f, .82f, .82f);
                line.SetPositions(shoreline);
            }
            CoastlinePolygonCount = shorelines.Count;
            Debug.Log($"Loaded {shorelines.Count} Natural Earth coastline polygons for {area.DisplayName}.");
            return true;
        }

        private void BuildLocations()
        {
            foreach (OperationalLocationDefinition location in area.Locations)
            {
                GameObject marker = Primitive(location.Kind == LocationKind.Airfield ? PrimitiveType.Cube : PrimitiveType.Cylinder, location.Kind + " — " + location.Name, root.transform, warningMaterial);
                marker.transform.position = HexToWorld(location.Hex) + Vector3.up * .22f;
                marker.transform.localScale = location.Kind == LocationKind.Airfield ? new Vector3(.28f, .035f, .08f) : new Vector3(.11f, .08f, .11f);
                if (location.Kind == LocationKind.Airfield) marker.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            }
        }

        private void BuildHexGrid()
        {
            for (int q = 0; q < area.Width; q++)
            {
                for (int r = 0; r < area.Height; r++)
                {
                    HexCoord hex = new HexCoord(q, r);
                    GameObject lineObject = Child("Hex " + hex, root.transform);
                    LineRenderer line = lineObject.AddComponent<LineRenderer>();
                    line.sharedMaterial = lineMaterial;
                    line.useWorldSpace = true;
                    line.loop = true;
                    line.positionCount = 6;
                    line.widthMultiplier = .025f;
                    for (int i = 0; i < 6; i++)
                    {
                        float angle = Mathf.Deg2Rad * (30f + i * 60f);
                        Vector3 point = HexToWorld(hex) + new Vector3(Mathf.Cos(angle), .06f, Mathf.Sin(angle)) * HexRadius;
                        line.SetPosition(i, point);
                    }
                    hexLines[hex] = line;
                }
            }
        }

        private void BuildInteractionOverlays()
        {
            GameObject hoverObject = Child("Hovered Hex", root.transform);
            hoverRing = hoverObject.AddComponent<LineRenderer>();
            hoverRing.sharedMaterial = lineMaterial;
            hoverRing.useWorldSpace = true;
            hoverRing.loop = true;
            hoverRing.positionCount = 32;
            hoverRing.gameObject.SetActive(false);

            GameObject previewObject = Child("Movement Preview", root.transform);
            previewLine = previewObject.AddComponent<LineRenderer>();
            previewLine.sharedMaterial = lineMaterial;
            previewLine.useWorldSpace = true;
            previewLine.widthMultiplier = .075f;
            previewLine.gameObject.SetActive(false);
        }

        private void UpdateHexStyles()
        {
            foreach (KeyValuePair<HexCoord, LineRenderer> pair in hexLines)
            {
                pair.Value.enabled = actionMode != ToolkitActionMode.None || pair.Key.Equals(area.Objective);
                Color color = new Color(.18f, .62f, .7f, .28f);
                float width = .025f;
                if (game?.Active != null)
                {
                    int distanceToHex = HexCoord.Distance(game.Active.Position, pair.Key);
                    if (actionMode == ToolkitActionMode.Move)
                    {
                        bool legal = distanceToHex > 0 && distanceToHex <= Rules.MoveAllowance(game.Active, moveMode);
                        if (legal) { color = new Color(.18f, 1f, .58f, 1f); width = .07f; }
                    }
                    else if (actionMode == ToolkitActionMode.Search)
                    {
                        int range = Rules.SearchRange(searchMode);
                        float strength = Mathf.Clamp01(1f - distanceToHex / (float)range);
                        color = distanceToHex <= range ? new Color(.16f, .82f, 1f, .3f + strength * .55f) : new Color(.25f, .32f, .38f, .16f);
                        width = distanceToHex <= Math.Max(1, game.Active.EffectiveSearch) ? .06f : distanceToHex <= range ? .035f : .018f;
                    }
                    else if (actionMode == ToolkitActionMode.Strike)
                    {
                        bool inRange = distanceToHex <= Rules.StrikeRange(game.Active.Kind, salvo);
                        color = inRange ? new Color(1f, .45f, .14f, .9f) : new Color(.3f, .34f, .38f, .2f);
                        width = inRange ? .06f : .02f;
                    }
                    if (pair.Key.Equals(area.Objective))
                    {
                        color = Color.Lerp(color, new Color(1f, .72f, .16f, 1f), .65f);
                        width = Mathf.Max(width, .075f);
                    }
                }
                pair.Value.startColor = pair.Value.endColor = color;
                pair.Value.widthMultiplier = width;
            }
        }

        private void RebuildStateObjects()
        {
            foreach (GameObject stateObject in stateObjects) RecycleMarker(stateObject);
            stateObjects.Clear();
            motions.Clear();
            lodPairs.Clear();
            VisibleFormationCount = 0;
            VisibleContactCount = 0;
            if (game?.Active == null) return;
            Side viewer = game.Active.Side;
            foreach (FormationState formation in game.Formations.Where(f => !f.IsDestroyed && f.Side == viewer))
            {
                stateObjects.Add(BuildFormation(formation));
                VisibleFormationCount++;
            }
            foreach (ContactState contact in game.Contacts.Where(c => c.Owner == viewer && !c.IsLost))
            {
                stateObjects.Add(BuildContact(contact));
                VisibleContactCount++;
            }
        }

        private GameObject BuildFormation(FormationState formation)
        {
            GameObject marker = AcquireMarker(formation.Name);
            float markerHeight = formation.Kind == FormationKind.AirGroup ? 1.15f : formation.Kind == FormationKind.Submarine ? .18f : .48f;
            Vector3 destination = HexToWorld(formation.Position) + Vector3.up * markerHeight;
            Vector3 start = presentedPositions.TryGetValue(formation.Id, out HexCoord oldHex) ? HexToWorld(oldHex) + Vector3.up * markerHeight : destination;
            marker.transform.position = reducedMotion ? destination : start;
            if (start != destination) motions.Add(new Motion { Transform = marker.transform, Start = start, End = destination });
            presentedPositions[formation.Id] = formation.Position;
            Material material = formation.Side == Side.Blue ? blueMaterial : redMaterial;
            GameObject detail = Child("Close 3D Model", marker.transform);
            if (formation.Kind == FormationKind.AirGroup)
            {
                GameObject fuselage = Primitive(PrimitiveType.Cube, "Aircraft", detail.transform, material);
                fuselage.transform.localScale = new Vector3(.12f, .08f, .7f);
                GameObject wing = Primitive(PrimitiveType.Cube, "Wings", detail.transform, material);
                wing.transform.localScale = new Vector3(.72f, .055f, .16f);
            }
            else
            {
                GameObject hull = Primitive(PrimitiveType.Cube, "Hull", detail.transform, material);
                hull.transform.localScale = formation.Kind == FormationKind.CarrierGroup ? new Vector3(.56f, .12f, 1.05f) : new Vector3(.34f, .16f, .82f);
                if (formation.Kind != FormationKind.Submarine)
                {
                    GameObject island = Primitive(PrimitiveType.Cube, "Superstructure", detail.transform, material);
                    island.transform.localPosition = new Vector3(.06f, .18f, -.06f);
                    island.transform.localScale = new Vector3(.18f, .22f, .28f);
                }
            }
            GameObject symbol = Primitive(PrimitiveType.Cylinder, "Distant Operational Symbol", marker.transform, material);
            symbol.transform.localScale = new Vector3(.3f, .025f, .3f);
            lodPairs.Add(new LodPair { Detail = detail, Symbol = symbol });
            LineRenderer ring = Ring("Formation Selection", formation.Position, .48f, formation == game.Active ? new Color(1f, .68f, .14f, 1f) : material.color, formation == game.Active ? .09f : .045f);
            ring.transform.SetParent(marker.transform, true);
            if (formation == game.Active)
            {
                GameObject beacon = Primitive(PrimitiveType.Cylinder, "Ready Beacon", marker.transform, warningMaterial);
                beacon.transform.localPosition = new Vector3(-.42f, .28f, 0f);
                beacon.transform.localScale = new Vector3(.055f, .28f, .055f);
            }
            AddFormationStatusCues(marker, formation);
            UpdateLod();
            return marker;
        }

        private void AddFormationStatusCues(GameObject marker, FormationState formation)
        {
            int cue = 0;
            void AddCue(string name, PrimitiveType type, Vector3 scale, Vector3 rotation)
            {
                GameObject stateCue = Primitive(type, name, marker.transform, warningMaterial);
                stateCue.transform.localPosition = new Vector3(.38f + cue++ * .16f, .31f, 0f);
                stateCue.transform.localScale = scale;
                stateCue.transform.localRotation = Quaternion.Euler(rotation);
            }
            if (formation.Friction) AddCue("Friction Diamond", PrimitiveType.Cube, Vector3.one * .1f, new Vector3(0f, 45f, 45f));
            if (formation.Disruption) AddCue("Disruption Bar", PrimitiveType.Cube, new Vector3(.05f, .16f, .05f), Vector3.zero);
            if (formation.Destruction || formation.Damage != DamageState.None) AddCue("Damage Block", PrimitiveType.Cube, formation.Damage >= DamageState.Heavy ? Vector3.one * .16f : Vector3.one * .11f, Vector3.zero);
            if (formation.Loud) AddCue("Loud Beacon", PrimitiveType.Sphere, Vector3.one * .12f, Vector3.zero);
            if (formation.Cohesion != "Cohesive") AddCue("Cohesion Split", PrimitiveType.Capsule, new Vector3(.07f, .14f, .07f), new Vector3(0f, 0f, 35f));
        }

        private void UpdateLod()
        {
            bool showDetail = distance <= Mathf.Max(area.Width, area.Height) * 1.25f;
            foreach (LodPair pair in lodPairs)
            {
                if (pair.Detail != null) pair.Detail.SetActive(showDetail);
                if (pair.Symbol != null) pair.Symbol.SetActive(!showDetail);
            }
        }

        private GameObject BuildContact(ContactState contact)
        {
            GameObject marker = AcquireMarker("Contact Marker");
            marker.transform.position = HexToWorld(contact.LastKnownPosition) + Vector3.up * .62f;
            bool eligible = actionMode == ToolkitActionMode.Search && game?.Active != null && HexCoord.Distance(game.Active.Position, contact.LastKnownPosition) <= Rules.SearchRange(searchMode) || actionMode == ToolkitActionMode.Strike && StrikeEligible(contact);
            Color stateColor = eligible ? new Color(.2f, 1f, .62f, 1f) : contact.Age >= 3 ? new Color(.62f, .49f, .38f, .72f) : contactMaterial.color;

            if (contact.IsFalse)
            {
                AddFalseContactCross(marker, stateColor);
            }
            else
            {
                GameObject diamond = Primitive(PrimitiveType.Cube, contact.Identity == IdentityQuality.Identified ? "Identified Contact" : "Unknown Contact", marker.transform, contactMaterial);
                float scale = contact.Identity == IdentityQuality.Identified ? .36f : contact.Identity == IdentityQuality.General ? .31f : .26f;
                diamond.transform.localScale = Vector3.one * scale;
                diamond.transform.localRotation = Quaternion.Euler(35f, 45f, 35f);
            }

            int uncertaintyRings = contact.Location == LocationQuality.High ? 1 : contact.Location == LocationQuality.Medium ? 2 : 3;
            float ageExpansion = Mathf.Min(contact.Age, 3) * .09f;
            for (int i = 0; i < uncertaintyRings; i++)
            {
                float radius = .52f + ageExpansion + i * .17f;
                Color ringColor = new Color(stateColor.r, stateColor.g, stateColor.b, Mathf.Clamp01(stateColor.a - i * .18f));
                LineRenderer ring = Ring(i == 0 ? "Contact Fix" : "Uncertainty Ring " + i, contact.LastKnownPosition, radius, ringColor, eligible && i == 0 ? .09f : .045f);
                ring.transform.SetParent(marker.transform, true);
            }
            return marker;
        }

        private void AddFalseContactCross(GameObject marker, Color color)
        {
            Vector3 center = marker.transform.position;
            LineRenderer first = Segment("False Contact Slash", center + new Vector3(-.35f, 0f, -.35f), center + new Vector3(.35f, 0f, .35f), color, .08f);
            LineRenderer second = Segment("False Contact Backslash", center + new Vector3(-.35f, 0f, .35f), center + new Vector3(.35f, 0f, -.35f), color, .08f);
            first.transform.SetParent(marker.transform, true);
            second.transform.SetParent(marker.transform, true);
        }

        private LineRenderer Segment(string name, Vector3 start, Vector3 end, Color color, float width)
        {
            GameObject lineObject = Child(name, root.transform);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.sharedMaterial = lineMaterial;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.widthMultiplier = width;
            line.startColor = line.endColor = color;
            return line;
        }

        private void UpdateObjectiveControl()
        {
            if (objectiveRing == null || game == null) return;
            bool blue = game.Formations.Any(f => f.Side == Side.Blue && !f.IsDestroyed && HexCoord.Distance(f.Position, area.Objective) <= 1);
            bool red = game.Formations.Any(f => f.Side == Side.Red && !f.IsDestroyed && HexCoord.Distance(f.Position, area.Objective) <= 1);
            Color color = blue && !red ? blueMaterial.color : red && !blue ? redMaterial.color : blue && red ? new Color(1f, .72f, .14f, 1f) : new Color(.82f, .9f, .94f, .75f);
            objectiveRing.startColor = objectiveRing.endColor = color;
            objectiveRing.widthMultiplier = blue || red ? .12f : .075f;
        }

        private bool StrikeEligible(ContactState contact)
        {
            if (HexCoord.Distance(game.Active.Position, contact.LastKnownPosition) > Rules.StrikeRange(game.Active.Kind, salvo)) return false;
            return salvo != Salvo.Heavy || !game.Active.WeaponExpended && game.Active.Endurance != Endurance.Critical && game.Active.Damage != DamageState.Crippled;
        }

        private LineRenderer Ring(string name, HexCoord hex, float radius, Color color, float width)
        {
            GameObject ringObject = Child(name, root.transform);
            LineRenderer ring = ringObject.AddComponent<LineRenderer>();
            ring.sharedMaterial = lineMaterial;
            ring.useWorldSpace = true;
            ring.loop = true;
            ring.positionCount = 32;
            ring.widthMultiplier = width;
            ring.startColor = ring.endColor = color;
            SetRingPositions(ring, hex, radius, .12f);
            return ring;
        }

        private void SetRingPositions(LineRenderer ring, HexCoord hex, float radius, float height)
        {
            ring.positionCount = 32;
            for (int i = 0; i < 32; i++)
            {
                float angle = i / 32f * Mathf.PI * 2f;
                ring.SetPosition(i, HexToWorld(hex) + new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius));
            }
        }

        private static Mesh CreateLandformMesh(int q, int r, float radius, float top, float bottom, int segments)
        {
            var random = new System.Random(q * 73856093 ^ r * 19349663 ^ segments * 83492791);
            var vertices = new Vector3[1 + segments * 3];
            vertices[0] = new Vector3(0f, top, 0f);
            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                float irregularity = .82f + (float)random.NextDouble() * .28f;
                float stretch = 1f + Mathf.Sin(angle * 2f + q * .7f) * .1f;
                float x = Mathf.Cos(angle) * radius * irregularity * stretch;
                float z = Mathf.Sin(angle) * radius * irregularity / stretch;
                vertices[1 + i] = new Vector3(x, top, z);
                vertices[1 + segments + i] = new Vector3(x, top, z);
                vertices[1 + segments * 2 + i] = new Vector3(x * 1.06f, bottom, z * 1.06f);
            }

            var triangles = new int[segments * 9];
            int index = 0;
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                int topA = 1 + i;
                int topB = 1 + next;
                int sideA = 1 + segments + i;
                int sideB = 1 + segments + next;
                int bottomA = 1 + segments * 2 + i;
                int bottomB = 1 + segments * 2 + next;
                triangles[index++] = 0; triangles[index++] = topB; triangles[index++] = topA;
                triangles[index++] = sideA; triangles[index++] = sideB; triangles[index++] = bottomA;
                triangles[index++] = sideB; triangles[index++] = bottomB; triangles[index++] = bottomA;
            }

            var mesh = new Mesh { name = $"Landform {q},{r}" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void UpdateCamera()
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            camera.transform.position = focus + rotation * new Vector3(0f, 0f, -distance);
            camera.transform.LookAt(focus);
        }

        private void ClampFocus()
        {
            Vector3 first = HexToWorld(new HexCoord(0, 0));
            Vector3 last = HexToWorld(new HexCoord(area.Width - 1, area.Height - 1));
            focus.x = Mathf.Clamp(focus.x, Mathf.Min(first.x, last.x) - area.Presentation.PanPadding, Mathf.Max(first.x, last.x) + area.Presentation.PanPadding);
            focus.z = Mathf.Clamp(focus.z, Mathf.Min(first.z, last.z) - area.Presentation.PanPadding, Mathf.Max(first.z, last.z) + area.Presentation.PanPadding);
            focus.y = 0f;
        }

        private static Material MaterialFor(string resourceName, string shaderName, Color color, float metallic = 0f, float smoothness = .3f)
        {
            Material packaged = Resources.Load<Material>("Materials/3D/" + resourceName);
            Shader shader = packaged != null ? packaged.shader : Shader.Find(shaderName) ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Unlit/Color");
            if (shader == null) throw new InvalidOperationException("No packaged 3D map shader is available for " + resourceName + ".");
            var material = packaged != null ? new Material(packaged) : new Material(shader);
            material.color = color;
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            return material;
        }

        private static GameObject Child(string name, Transform parent)
        {
            var child = new GameObject(name) { layer = MapLayer };
            child.transform.SetParent(parent, false);
            return child;
        }

        private GameObject AcquireMarker(string name)
        {
            GameObject marker = markerPool.Count > 0 ? markerPool.Pop() : Child("Pooled Marker", root.transform);
            marker.name = name;
            marker.transform.SetParent(root.transform, false);
            marker.transform.localRotation = Quaternion.identity;
            marker.transform.localScale = Vector3.one;
            marker.SetActive(true);
            return marker;
        }

        private void RecycleMarker(GameObject marker)
        {
            if (marker == null) return;
            for (int i = marker.transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = marker.transform.GetChild(i).gameObject;
                child.SetActive(false);
                DestroyObject(child);
            }
            marker.name = "Pooled Marker";
            marker.SetActive(false);
            markerPool.Push(marker);
        }

        private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.layer = MapLayer;
            primitive.transform.SetParent(parent, false);
            Collider collider = primitive.GetComponent<Collider>();
            if (collider != null) DestroyObject(collider);
            Renderer renderer = primitive.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            return primitive;
        }

        private GameObject MeshObject(string name, Transform parent, Material material, Mesh mesh)
        {
            GameObject meshObject = Child(name, parent);
            MeshFilter filter = meshObject.AddComponent<MeshFilter>();
            MeshRenderer renderer = meshObject.AddComponent<MeshRenderer>();
            filter.sharedMesh = mesh;
            renderer.sharedMaterial = material;
            generatedMeshes.Add(mesh);
            return meshObject;
        }

        public void Dispose()
        {
            if (camera != null) camera.targetTexture = null;
            if (targetTexture != null) { targetTexture.Release(); DestroyObject(targetTexture); }
            if (root != null) DestroyObject(root);
            if (Application.isPlaying && mapScene.IsValid() && mapScene.isLoaded) SceneManager.UnloadSceneAsync(mapScene);
            foreach (Mesh mesh in generatedMeshes) DestroyObject(mesh);
            generatedMeshes.Clear();
            DestroyObject(lineMaterial); DestroyObject(waterMaterial); DestroyObject(landMaterial); DestroyObject(littoralMaterial);
            DestroyObject(blueMaterial); DestroyObject(redMaterial); DestroyObject(contactMaterial); DestroyObject(warningMaterial);
        }

        private static void DestroyObject(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value);
        }
    }
}
