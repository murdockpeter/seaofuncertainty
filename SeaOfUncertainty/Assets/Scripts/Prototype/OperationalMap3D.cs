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
        private readonly List<Texture2D> generatedTextures = new List<Texture2D>();
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
        private readonly Material highlandMaterial;
        private readonly Material blueMaterial;
        private readonly Material redMaterial;
        private readonly Material navalHullMaterial;
        private readonly Material navalDeckMaterial;
        private readonly Material canopyMaterial;
        private readonly Material contactMaterial;
        private readonly Material warningMaterial;
        private readonly Dictionary<string, Mesh> formationMeshes = new Dictionary<string, Mesh>();
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
        private bool showPermanentGrid;
        private float waterScroll;

        private sealed class Motion { public Transform Transform; public Vector3 Start; public Vector3 End; public float Progress; }
        private sealed class LodPair { public GameObject Detail; public GameObject Symbol; }
        private sealed class EffectInstance { public GameObject Object; public Vector3 BaseScale; public float Age; public float Duration; }

        public RenderTexture Texture => targetTexture;
        public float Heading => Mathf.Repeat(yaw, 360f);
        public float CameraPitch => pitch;
        public float CameraDistance => distance;
        public int VisibleFormationCount { get; private set; }
        public int VisibleContactCount { get; private set; }
        public int ActiveEffectCount => activeEffects.Count;
        public int PooledEffectCount => effectPool.Count;
        public int PooledMarkerCount => markerPool.Count;
        public int CoastlinePolygonCount { get; private set; }
        public int OffMapCoastlineVertexCount { get; private set; }
        public int TerrainReliefCount { get; private set; }
        public int WaterSurfaceVertexCount { get; private set; }
        public bool HasDirectionalSun { get; private set; }
        public float SunSourceAzimuthDegrees { get; private set; }
        public int FormationMeshVariantCount => formationMeshes.Count;
        public bool UsesProceduralSurfaceTextures => generatedTextures.Count >= 3;
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
            waterMaterial = MaterialFor("CommandWater", "Standard", new Color(.018f, .13f, .19f, 1f), .08f, .78f);
            landMaterial = MaterialFor("CommandLand", "Standard", new Color(.27f, .34f, .19f, 1f), .02f, .34f);
            littoralMaterial = MaterialFor("CommandLittoral", "Standard", new Color(.08f, .36f, .34f, 1f), .04f, .58f);
            highlandMaterial = MaterialFor("CommandHighland", "Standard", new Color(.29f, .31f, .16f, 1f), .01f, .26f);
            blueMaterial = MaterialFor("CommandBlue", "Standard", new Color(.08f, .72f, .95f, 1f), .28f, .45f);
            redMaterial = MaterialFor("CommandRed", "Standard", new Color(.94f, .25f, .18f, 1f), .2f, .48f);
            navalHullMaterial = MaterialFor("FormationHull", "Standard", new Color(.16f, .22f, .24f, 1f), .32f, .42f);
            navalDeckMaterial = MaterialFor("FormationDeck", "Standard", new Color(.075f, .105f, .115f, 1f), .18f, .3f);
            canopyMaterial = MaterialFor("FormationCanopy", "Standard", new Color(.025f, .10f, .15f, 1f), .48f, .72f);
            contactMaterial = MaterialFor("CommandContact", "Standard", new Color(1f, .48f, .13f, 1f), .3f, .4f);
            warningMaterial = MaterialFor("CommandWarning", "Standard", new Color(1f, .72f, .12f, 1f), .18f, .4f);
            ConfigureSurfaceMaterials();

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

            GameObject lightObject = Child("Maritime Sun", root.transform);
            Light sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = night ? new Color(.48f, .6f, .82f) : new Color(1f, .88f, .68f);
            sun.intensity = night ? .38f : Mathf.Lerp(1.05f, 1.45f, area.Presentation.Visibility);
            float sunElevation = Mathf.Lerp(24f, 52f, Mathf.Clamp01(1f - Mathf.Abs(area.Presentation.TimeOfDay - 12f) / 8f));
            SunSourceAzimuthDegrees = 67.5f;
            float sunRayHeading = Mathf.Repeat(SunSourceAzimuthDegrees + 180f, 360f);
            sun.transform.rotation = Quaternion.Euler(sunElevation, sunRayHeading, 0f);
            sun.cullingMask = 1 << MapLayer;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .68f;
            sun.shadowBias = .035f;
            HasDirectionalSun = true;

            GameObject fillObject = Child("Sky Fill", root.transform);
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(.35f, .55f, .72f);
            fill.intensity = night ? .32f : .22f;
            fill.transform.rotation = Quaternion.Euler(62f, SunSourceAzimuthDegrees, 0f);
            fill.cullingMask = 1 << MapLayer;
            fill.shadows = LightShadows.None;

            BuildSurface();
            BuildHexGrid();
            BuildLocations();
            BuildInteractionOverlays();
            UpdateCamera();
        }

        public void SetState(PrototypeGame currentGame, ToolkitActionMode mode, MoveMode currentMoveMode, SearchMode currentSearchMode, Salvo currentSalvo, bool useReducedMotion = false, bool showGrid = false)
        {
            game = currentGame;
            actionMode = mode;
            moveMode = currentMoveMode;
            searchMode = currentSearchMode;
            salvo = currentSalvo;
            reducedMotion = useReducedMotion;
            showPermanentGrid = showGrid;
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
            if (!reducedMotion && waterMaterial.mainTexture != null)
            {
                waterScroll = Mathf.Repeat(waterScroll + deltaTime * .004f, 1f);
                waterMaterial.mainTextureOffset = new Vector2(waterScroll, waterScroll * .43f);
            }
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

        public void Orbit(Vector2 pixels)
        {
            yaw = Mathf.Repeat(yaw + pixels.x * .22f, 360f);
            pitch = Mathf.Clamp(pitch - pixels.y * .18f, 24f, 82f);
            UpdateCamera();
        }

        public void FlyCamera(float rightInput, float forwardInput, float yawInput, float pitchInput, float zoomInput, float deltaTime, bool fast = false)
        {
            if (deltaTime <= 0f) return;
            float multiplier = fast ? 2.5f : 1f;
            Vector3 right = Vector3.ProjectOnPlane(camera.transform.right, Vector3.up).normalized;
            Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
            float panSpeed = Mathf.Max(5f, distance * .52f) * multiplier * deltaTime;
            focus += (right * rightInput + forward * forwardInput) * panSpeed;
            yaw = Mathf.Repeat(yaw + yawInput * 62f * multiplier * deltaTime, 360f);
            pitch = Mathf.Clamp(pitch + pitchInput * 44f * multiplier * deltaTime, 24f, 82f);
            if (Mathf.Abs(zoomInput) > .001f)
            {
                distance *= Mathf.Exp(zoomInput * 1.25f * multiplier * deltaTime);
                distance = Mathf.Clamp(distance, area.Presentation.MinimumZoom, Mathf.Max(area.Width, area.Height) * area.Presentation.MaximumZoomMultiplier);
            }
            ClampFocus();
            UpdateCamera();
            UpdateLod();
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
            water.transform.position = center + Vector3.up * (WaterSurfaceY - .30f);
            water.transform.localScale = new Vector3(last.x - first.x + 3.2f, .48f, last.z - first.z + 3.2f);
            float waterWidth = last.x - first.x + 12f;
            float waterDepth = last.z - first.z + 12f;
            Mesh waterSurfaceMesh = CreateWaterSurfaceMesh(waterWidth, waterDepth, 72, 56);
            WaterSurfaceVertexCount = waterSurfaceMesh.vertexCount;
            GameObject waterSurface = MeshObject("Sunlit Ocean Surface", root.transform, waterMaterial, waterSurfaceMesh);
            waterSurface.transform.position = center + Vector3.up * WaterSurfaceY;

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

        private void BuildCoastlineTerrainRelief(IEnumerable<Vector3[]> shorelines)
        {
            GameObject reliefRoot = Child("Coastline-Aligned Terrain Relief", root.transform);
            foreach (Vector3[] shoreline in shorelines)
            {
                if (shoreline == null || shoreline.Length < 3) continue;
                float minX = shoreline.Min(point => point.x), maxX = shoreline.Max(point => point.x);
                float minZ = shoreline.Min(point => point.z), maxZ = shoreline.Max(point => point.z);
                float polygonWidth = maxX - minX, polygonDepth = maxZ - minZ;
                if (polygonWidth < 1.8f || polygonDepth < 1.8f) continue;
                Vector3 center = shoreline.Aggregate(Vector3.zero, (sum, point) => sum + point) / shoreline.Length;
                float width = Mathf.Clamp(polygonWidth * .42f, .9f, 3.2f);
                float depth = Mathf.Clamp(polygonDepth * .42f, .9f, 3.2f);
                float height = Mathf.Clamp(Mathf.Min(width, depth) * .13f, .14f, .34f);
                int seed = shoreline.Length * 397 ^ Mathf.RoundToInt(center.x * 100f) ^ Mathf.RoundToInt(center.z * 100f);
                GameObject ridge = MeshObject("Major Landmass Ridge", reliefRoot.transform, highlandMaterial, CreateTerrainRidgeMesh(width, depth, height, seed));
                ridge.transform.position = new Vector3(center.x, .105f, center.z);
                ridge.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(polygonWidth, polygonDepth) * Mathf.Rad2Deg * .18f, 0f);
                Renderer renderer = ridge.GetComponent<Renderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;
                TerrainReliefCount++;
            }
        }

        private bool BuildPolygonCoastline(Vector3 first, Vector3 last)
        {
            if (string.IsNullOrWhiteSpace(area.CoastlineResource)) return false;
            TextAsset source = Resources.Load<TextAsset>(area.CoastlineResource);
            if (source == null) { Debug.LogError("Missing coastline resource: " + area.CoastlineResource); return false; }
            CoastlineData coastline = JsonUtility.FromJson<CoastlineData>(source.text);
            var projectionBounds = Rect.MinMaxRect(Mathf.Min(first.x, last.x) - 1f, Mathf.Min(first.z, last.z) - .87f, Mathf.Max(first.x, last.x) + 1f, Mathf.Max(first.z, last.z) + .87f);
            Mesh mesh = CoastlinePolygonMesh.Create(coastline, projectionBounds, .105f, out List<Vector3[]> shorelines);
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
            BuildCoastlineTerrainRelief(shorelines);
            CoastlinePolygonCount = shorelines.Count;
            OffMapCoastlineVertexCount = shorelines.Sum(shoreline => shoreline.Count(point => point.x < projectionBounds.xMin || point.x > projectionBounds.xMax || point.z < projectionBounds.yMin || point.z > projectionBounds.yMax));
            Debug.Log($"Loaded {shorelines.Count} Natural Earth coastline polygons for {area.DisplayName}; {OffMapCoastlineVertexCount} authentic vertices continue beyond the playable projection.");
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
                pair.Value.enabled = showPermanentGrid || actionMode != ToolkitActionMode.None || pair.Key.Equals(area.Objective);
                OperationalTerrain terrain = area.TerrainAt(pair.Key);
                Color color = terrain == OperationalTerrain.Land
                    ? new Color(.78f, .76f, .43f, showPermanentGrid ? .37f : .18f)
                    : new Color(.16f, .75f, .86f, showPermanentGrid ? .41f : .18f);
                float width = showPermanentGrid ? .044f : .025f;
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
            float markerHeight = formation.Kind == FormationKind.AirGroup ? 1.15f : formation.Kind == FormationKind.Submarine ? .025f : .015f;
            Vector3 destination = HexToWorld(formation.Position) + Vector3.up * markerHeight;
            Vector3 start = presentedPositions.TryGetValue(formation.Id, out HexCoord oldHex) ? HexToWorld(oldHex) + Vector3.up * markerHeight : destination;
            marker.transform.position = reducedMotion ? destination : start;
            if (start != destination) motions.Add(new Motion { Transform = marker.transform, Start = start, End = destination });
            presentedPositions[formation.Id] = formation.Position;
            Material material = formation.Side == Side.Blue ? blueMaterial : redMaterial;
            GameObject detail = Child("Close 3D Model", marker.transform);
            switch (formation.Kind)
            {
                case FormationKind.CarrierGroup: BuildCarrierGroup(detail.transform, material); break;
                case FormationKind.SurfaceGroup: BuildSurfaceGroup(detail.transform, material); break;
                case FormationKind.Submarine: BuildSubmarine(detail.transform, material); break;
                case FormationKind.AirGroup: BuildAircraft(detail.transform, material); break;
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

        private void BuildCarrierGroup(Transform parent, Material sideMaterial)
        {
            Mesh hullMesh = FormationHull("Carrier Hull", 1.08f, .47f, .18f, .72f);
            Mesh escortMesh = FormationHull("Escort Hull", .54f, .17f, .10f, .66f);
            MeshObject("Carrier Tapered Hull", parent, navalHullMaterial, hullMesh);

            GameObject deck = Primitive(PrimitiveType.Cube, "Angled Flight Deck", parent, navalDeckMaterial);
            deck.transform.localPosition = new Vector3(-.025f, .115f, -.015f);
            deck.transform.localRotation = Quaternion.Euler(0f, -2.5f, 0f);
            deck.transform.localScale = new Vector3(.48f, .035f, .94f);

            GameObject island = Primitive(PrimitiveType.Cube, "Carrier Island", parent, sideMaterial);
            island.transform.localPosition = new Vector3(.18f, .21f, -.08f);
            island.transform.localScale = new Vector3(.105f, .16f, .22f);
            AddMastAndRadar(island.transform, sideMaterial, .16f);

            AddDeckMarking(parent, new Vector3(-.09f, .143f, .18f), new Vector3(.035f, .006f, .34f), sideMaterial);
            AddDeckMarking(parent, new Vector3(.115f, .143f, -.28f), new Vector3(.15f, .006f, .18f), sideMaterial);
            AddEscort(parent, escortMesh, new Vector3(-.48f, -.015f, -.25f), Quaternion.Euler(0f, -12f, 0f), sideMaterial);
            AddEscort(parent, escortMesh, new Vector3(.48f, -.015f, .29f), Quaternion.Euler(0f, 14f, 0f), sideMaterial);
        }

        private void BuildSurfaceGroup(Transform parent, Material sideMaterial)
        {
            Mesh destroyerMesh = FormationHull("Surface Combatant Hull", .82f, .29f, .16f, .64f);
            MeshObject("Surface Combatant Tapered Hull", parent, navalHullMaterial, destroyerMesh);

            GameObject forwardDeck = Primitive(PrimitiveType.Cube, "Forward Deck", parent, navalDeckMaterial);
            forwardDeck.transform.localPosition = new Vector3(0f, .10f, .18f);
            forwardDeck.transform.localScale = new Vector3(.22f, .026f, .30f);
            GameObject bridge = Primitive(PrimitiveType.Cube, "Faceted Bridge", parent, sideMaterial);
            bridge.transform.localPosition = new Vector3(0f, .18f, -.04f);
            bridge.transform.localScale = new Vector3(.18f, .17f, .23f);
            GameObject upperBridge = Primitive(PrimitiveType.Cube, "Upper Bridge", bridge.transform, navalDeckMaterial);
            upperBridge.transform.localPosition = new Vector3(0f, .62f, -.08f);
            upperBridge.transform.localScale = new Vector3(.68f, .3f, .58f);
            AddMastAndRadar(bridge.transform, sideMaterial, .19f);
            AddGunMount(parent, new Vector3(0f, .145f, .31f), sideMaterial);
            GameObject launchCells = Primitive(PrimitiveType.Cube, "Vertical Launch Battery", parent, navalDeckMaterial);
            launchCells.transform.localPosition = new Vector3(0f, .13f, -.25f);
            launchCells.transform.localScale = new Vector3(.17f, .045f, .15f);

            Mesh escortMesh = FormationHull("Surface Escort Hull", .47f, .15f, .09f, .66f);
            AddEscort(parent, escortMesh, new Vector3(-.42f, -.025f, -.22f), Quaternion.Euler(0f, -18f, 0f), sideMaterial);
            AddEscort(parent, escortMesh, new Vector3(.42f, -.025f, .20f), Quaternion.Euler(0f, 17f, 0f), sideMaterial);
        }

        private void BuildSubmarine(Transform parent, Material sideMaterial)
        {
            GameObject pressureHull = Primitive(PrimitiveType.Capsule, "Hydrodynamic Pressure Hull", parent, navalHullMaterial);
            pressureHull.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            pressureHull.transform.localScale = new Vector3(.18f, .44f, .18f);

            GameObject sail = Primitive(PrimitiveType.Cube, "Submarine Sail", parent, sideMaterial);
            sail.transform.localPosition = new Vector3(0f, .15f, -.03f);
            sail.transform.localScale = new Vector3(.10f, .18f, .20f);
            GameObject fairwaterPlanes = Primitive(PrimitiveType.Cube, "Fairwater Planes", parent, sideMaterial);
            fairwaterPlanes.transform.localPosition = new Vector3(0f, .14f, -.01f);
            fairwaterPlanes.transform.localScale = new Vector3(.42f, .028f, .10f);
            GameObject sternPlanes = Primitive(PrimitiveType.Cube, "Stern Control Planes", parent, navalDeckMaterial);
            sternPlanes.transform.localPosition = new Vector3(0f, .01f, -.40f);
            sternPlanes.transform.localScale = new Vector3(.34f, .025f, .10f);
            GameObject rudder = Primitive(PrimitiveType.Cube, "Stern Rudder", parent, navalDeckMaterial);
            rudder.transform.localPosition = new Vector3(0f, .08f, -.40f);
            rudder.transform.localScale = new Vector3(.025f, .25f, .10f);
            GameObject propulsor = Primitive(PrimitiveType.Cylinder, "Shrouded Propulsor", parent, sideMaterial);
            propulsor.transform.localPosition = new Vector3(0f, 0f, -.47f);
            propulsor.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            propulsor.transform.localScale = new Vector3(.115f, .025f, .115f);
            GameObject periscope = Primitive(PrimitiveType.Cylinder, "Periscope", sail.transform, sideMaterial);
            periscope.transform.localPosition = new Vector3(0f, .72f, 0f);
            periscope.transform.localScale = new Vector3(.09f, .30f, .09f);
        }

        private void BuildAircraft(Transform parent, Material sideMaterial)
        {
            parent.localRotation = Quaternion.Euler(0f, -12f, 0f);
            GameObject fuselage = Primitive(PrimitiveType.Capsule, "Aircraft Fuselage", parent, sideMaterial);
            fuselage.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            fuselage.transform.localScale = new Vector3(.095f, .38f, .095f);
            GameObject nose = Primitive(PrimitiveType.Sphere, "Aircraft Nose", parent, sideMaterial);
            nose.transform.localPosition = new Vector3(0f, 0f, .38f);
            nose.transform.localScale = new Vector3(.095f, .075f, .15f);
            GameObject canopy = Primitive(PrimitiveType.Sphere, "Tinted Canopy", parent, canopyMaterial);
            canopy.transform.localPosition = new Vector3(0f, .075f, .13f);
            canopy.transform.localScale = new Vector3(.09f, .055f, .15f);

            AddSweptWing(parent, "Port Swept Wing", -1f, sideMaterial);
            AddSweptWing(parent, "Starboard Swept Wing", 1f, sideMaterial);
            GameObject tailplane = Primitive(PrimitiveType.Cube, "Tailplane", parent, sideMaterial);
            tailplane.transform.localPosition = new Vector3(0f, .01f, -.28f);
            tailplane.transform.localScale = new Vector3(.36f, .025f, .10f);
            GameObject fin = Primitive(PrimitiveType.Cube, "Vertical Tail", parent, sideMaterial);
            fin.transform.localPosition = new Vector3(0f, .11f, -.30f);
            fin.transform.localRotation = Quaternion.Euler(-16f, 0f, 0f);
            fin.transform.localScale = new Vector3(.025f, .22f, .12f);
            GameObject exhaust = Primitive(PrimitiveType.Cylinder, "Engine Exhaust Nozzle", parent, navalDeckMaterial);
            exhaust.transform.localPosition = new Vector3(0f, 0f, -.39f);
            exhaust.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            exhaust.transform.localScale = new Vector3(.07f, .025f, .07f);
        }

        private void AddSweptWing(Transform parent, string name, float side, Material material)
        {
            GameObject wing = Primitive(PrimitiveType.Cube, name, parent, material);
            wing.transform.localPosition = new Vector3(side * .19f, 0f, -.015f);
            wing.transform.localRotation = Quaternion.Euler(0f, side * 19f, 0f);
            wing.transform.localScale = new Vector3(.42f, .025f, .14f);
        }

        private void AddEscort(Transform parent, Mesh mesh, Vector3 position, Quaternion rotation, Material sideMaterial)
        {
            GameObject escort = MeshObject("Escort Vessel", parent, navalHullMaterial, mesh);
            escort.transform.localPosition = position;
            escort.transform.localRotation = rotation;
            GameObject bridge = Primitive(PrimitiveType.Cube, "Escort Bridge", escort.transform, sideMaterial);
            bridge.transform.localPosition = new Vector3(0f, .12f, -.04f);
            bridge.transform.localScale = new Vector3(.12f, .11f, .15f);
        }

        private void AddMastAndRadar(Transform parent, Material sideMaterial, float height)
        {
            GameObject mast = Primitive(PrimitiveType.Cylinder, "Sensor Mast", parent, navalDeckMaterial);
            mast.transform.localPosition = new Vector3(0f, .68f, 0f);
            mast.transform.localScale = new Vector3(.075f, height, .075f);
            GameObject radar = Primitive(PrimitiveType.Sphere, "Radar Array", mast.transform, sideMaterial);
            radar.transform.localPosition = new Vector3(0f, .63f, 0f);
            radar.transform.localScale = new Vector3(.55f, .18f, .18f);
        }

        private void AddGunMount(Transform parent, Vector3 position, Material sideMaterial)
        {
            GameObject turret = Primitive(PrimitiveType.Cylinder, "Forward Gun Mount", parent, sideMaterial);
            turret.transform.localPosition = position;
            turret.transform.localScale = new Vector3(.09f, .045f, .09f);
            GameObject barrel = Primitive(PrimitiveType.Cube, "Gun Barrel", turret.transform, navalDeckMaterial);
            barrel.transform.localPosition = new Vector3(0f, .35f, .75f);
            barrel.transform.localScale = new Vector3(.18f, .18f, 1.2f);
        }

        private void AddDeckMarking(Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject marking = Primitive(PrimitiveType.Cube, "Flight Deck Centerline", parent, material);
            marking.transform.localPosition = position;
            marking.transform.localScale = scale;
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

        private void ConfigureSurfaceMaterials()
        {
            Texture2D waterTexture = CreateSurfaceTexture("Procedural Ocean Detail", 192, new Color(.012f, .095f, .145f), new Color(.035f, .19f, .215f), 17, true);
            Texture2D landTexture = CreateSurfaceTexture("Procedural Land Detail", 192, new Color(.11f, .15f, .06f), new Color(.33f, .34f, .16f), 41, false);
            Texture2D littoralTexture = CreateSurfaceTexture("Procedural Littoral Detail", 128, new Color(.025f, .19f, .21f), new Color(.18f, .52f, .42f), 73, false);
            waterMaterial.mainTexture = waterTexture;
            waterMaterial.mainTextureScale = Vector2.one;
            waterMaterial.color = Color.white;
            landMaterial.mainTexture = landTexture;
            landMaterial.mainTextureScale = new Vector2(4f, 4f);
            landMaterial.color = Color.white;
            highlandMaterial.mainTexture = landTexture;
            highlandMaterial.mainTextureScale = new Vector2(2.5f, 2.5f);
            highlandMaterial.color = Color.white;
            littoralMaterial.mainTexture = littoralTexture;
            littoralMaterial.mainTextureScale = new Vector2(3f, 3f);
            littoralMaterial.color = Color.white;
        }

        private Texture2D CreateSurfaceTexture(string name, int size, Color low, Color high, int seed, bool periodicWaves)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var colors = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)(size - 1), v = y / (float)(size - 1);
                    float value;
                    if (periodicWaves)
                    {
                        float first = Mathf.Sin((u * 5f + v * 2f) * Mathf.PI * 2f);
                        float second = Mathf.Sin((u * 9f - v * 4f) * Mathf.PI * 2f + .7f);
                        float third = Mathf.Cos((u * 2f + v * 11f) * Mathf.PI * 2f + 1.4f);
                        value = Mathf.Clamp01(.5f + first * .18f + second * .1f + third * .06f);
                    }
                    else
                    {
                        float broad = Mathf.PerlinNoise(u * 4.2f + seed, v * 4.2f + seed * .17f);
                        float detail = Mathf.PerlinNoise(u * 15f + seed * .31f, v * 15f + seed * .53f);
                        value = Mathf.Clamp01(broad * .72f + detail * .28f);
                    }
                    colors[y * size + x] = Color.Lerp(low, high, value);
                }
            }
            texture.SetPixels(colors);
            texture.Apply(true, false);
            generatedTextures.Add(texture);
            return texture;
        }

        private static Mesh CreateWaterSurfaceMesh(float width, float depth, int columns, int rows)
        {
            var vertices = new Vector3[(columns + 1) * (rows + 1)];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[columns * rows * 6];
            for (int row = 0; row <= rows; row++)
            {
                float v = row / (float)rows;
                for (int column = 0; column <= columns; column++)
                {
                    float u = column / (float)columns;
                    float wave = Mathf.Sin((u * 7f + v * 3f) * Mathf.PI * 2f) * .012f + Mathf.Sin((u * 13f - v * 8f) * Mathf.PI * 2f) * .006f;
                    int vertex = row * (columns + 1) + column;
                    vertices[vertex] = new Vector3((u - .5f) * width, wave, (v - .5f) * depth);
                    uvs[vertex] = new Vector2(u * 3f, v * 3f);
                }
            }
            int triangle = 0;
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    int a = row * (columns + 1) + column;
                    int b = a + 1;
                    int c = a + columns + 1;
                    int d = c + 1;
                    triangles[triangle++] = a; triangles[triangle++] = c; triangles[triangle++] = d;
                    triangles[triangle++] = a; triangles[triangle++] = d; triangles[triangle++] = b;
                }
            }
            var mesh = new Mesh { name = "Procedural Sunlit Ocean Mesh", vertices = vertices, uv = uvs, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateTerrainRidgeMesh(float width, float depth, float height, int seed)
        {
            const int rings = 4;
            const int segments = 18;
            var random = new System.Random(seed);
            var edgeVariation = new float[segments];
            for (int segment = 0; segment < segments; segment++) edgeVariation[segment] = Mathf.Lerp(.82f, 1.12f, (float)random.NextDouble());
            var vertices = new List<Vector3> { new Vector3(0f, height, 0f) };
            var uvs = new List<Vector2> { new Vector2(.5f, .5f) };
            for (int ring = 1; ring <= rings; ring++)
            {
                float radius = ring / (float)rings;
                float elevation = height * Mathf.Pow(1f - radius, 1.35f);
                for (int segment = 0; segment < segments; segment++)
                {
                    float angle = segment / (float)segments * Mathf.PI * 2f;
                    float irregularity = Mathf.Lerp(1f, edgeVariation[segment], radius);
                    float x = Mathf.Cos(angle) * width * .5f * radius * irregularity;
                    float z = Mathf.Sin(angle) * depth * .5f * radius * irregularity;
                    vertices.Add(new Vector3(x, elevation, z));
                    uvs.Add(new Vector2(x / width + .5f, z / depth + .5f));
                }
            }
            var triangles = new List<int>();
            for (int segment = 0; segment < segments; segment++)
            {
                int next = (segment + 1) % segments;
                triangles.Add(0); triangles.Add(1 + next); triangles.Add(1 + segment);
            }
            for (int ring = 1; ring < rings; ring++)
            {
                int inner = 1 + (ring - 1) * segments;
                int outer = 1 + ring * segments;
                for (int segment = 0; segment < segments; segment++)
                {
                    int next = (segment + 1) % segments;
                    triangles.Add(inner + segment); triangles.Add(inner + next); triangles.Add(outer + segment);
                    triangles.Add(inner + next); triangles.Add(outer + next); triangles.Add(outer + segment);
                }
            }
            var mesh = new Mesh { name = "Procedural Coastline Ridge" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
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

        private Mesh FormationHull(string name, float length, float width, float height, float sternRatio)
        {
            if (formationMeshes.TryGetValue(name, out Mesh existing)) return existing;
            float halfLength = length * .5f;
            float halfWidth = width * .5f;
            float top = height * .5f;
            float bottom = -height * .5f;
            float[] z = { -halfLength, -halfLength * .56f, halfLength * .18f, halfLength * .68f, halfLength };
            float[] beam = { sternRatio, 1f, 1f, .72f, .04f };
            var vertices = new List<Vector3>(z.Length * 4);
            for (int i = 0; i < z.Length; i++)
            {
                float upper = halfWidth * beam[i];
                float chine = upper * .70f;
                vertices.Add(new Vector3(-upper, top, z[i]));
                vertices.Add(new Vector3(upper, top, z[i]));
                vertices.Add(new Vector3(chine, bottom, z[i]));
                vertices.Add(new Vector3(-chine, bottom, z[i]));
            }
            var triangles = new List<int>();
            void Quad(int a, int b, int c, int d)
            {
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
                triangles.Add(a); triangles.Add(c); triangles.Add(d);
            }
            for (int i = 0; i < z.Length - 1; i++)
            {
                int a = i * 4, b = (i + 1) * 4;
                Quad(a, b, b + 1, a + 1);
                Quad(a + 1, b + 1, b + 2, a + 2);
                Quad(a + 3, a + 2, b + 2, b + 3);
                Quad(a, a + 3, b + 3, b);
            }
            Quad(0, 1, 2, 3);
            int end = (z.Length - 1) * 4;
            Quad(end + 3, end + 2, end + 1, end);
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            formationMeshes[name] = mesh;
            generatedMeshes.Add(mesh);
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
            if (!generatedMeshes.Contains(mesh)) generatedMeshes.Add(mesh);
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
            formationMeshes.Clear();
            foreach (Texture2D texture in generatedTextures) DestroyObject(texture);
            generatedTextures.Clear();
            DestroyObject(lineMaterial); DestroyObject(waterMaterial); DestroyObject(landMaterial); DestroyObject(littoralMaterial); DestroyObject(highlandMaterial);
            DestroyObject(blueMaterial); DestroyObject(redMaterial); DestroyObject(navalHullMaterial); DestroyObject(navalDeckMaterial); DestroyObject(canopyMaterial);
            DestroyObject(contactMaterial); DestroyObject(warningMaterial);
        }

        private static void DestroyObject(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value);
        }
    }
}
