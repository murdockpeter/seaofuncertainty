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
        private const float GeographicLandSurfaceY = -.05f;
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
        // Static, not per-instance: SeaUIToolkitController.ShowGame() rebuilds a fresh TacticalMapElement
        // (and thus a fresh OperationalMap3D) after almost every action, so tracking last-known position
        // and facing has to outlive this instance to detect movement across that rebuild. Callers must
        // invoke ResetPresentationMemory() whenever a new match/scenario begins, so a formation ID reused
        // by a fresh scenario doesn't inherit stale motion from a previous match.
        private static readonly Dictionary<string, HexCoord> presentedPositions = new Dictionary<string, HexCoord>();
        private static readonly Dictionary<string, Quaternion> presentedRotations = new Dictionary<string, Quaternion>();

        public static void ResetPresentationMemory()
        {
            presentedPositions.Clear();
            presentedRotations.Clear();
        }
        private readonly List<Motion> motions = new List<Motion>();
        private readonly List<LodPair> lodPairs = new List<LodPair>();
        private readonly Dictionary<OperationalEffectKind, Stack<GameObject>> effectPools = new Dictionary<OperationalEffectKind, Stack<GameObject>>();
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
        private readonly Material foamMaterial;
        private readonly Material shallowWaterMaterial;
        private readonly Material atmosphericMaterial;
        private readonly Material cloudShadowMaterial;
        private readonly Material contactMaterial;
        private readonly Material warningMaterial;
        private readonly Dictionary<string, Mesh> formationMeshes = new Dictionary<string, Mesh>();
        private LineRenderer hoverRing;
        private LineRenderer previewLine;
        private LineRenderer objectiveRing;
        private LineRenderer activeFormationRing;
        private float activePulsePhase;
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
        private Transform waterSurfaceTransform;
        private Transform cloudShadowRoot;
        private Transform sunTransform;
        private readonly List<Transform> billboardLabels = new List<Transform>();
        private Vector3 cameraPanVelocity;
        private float cameraYawVelocity;
        private float cameraPitchVelocity;
        private float cameraZoomVelocity;
        private CameraPose savedCameraPose;
        private bool hasSavedCameraPose;

        private sealed class Motion { public Transform Transform; public Vector3 Start; public Vector3 End; public Quaternion StartRotation; public Quaternion EndRotation; public float Progress; }
        private sealed class LodPair { public GameObject Detail; public GameObject Symbol; }
        private sealed class EffectInstance { public OperationalEffectKind Kind; public GameObject Object; public Vector3 BaseScale; public Vector3 Start; public float Age; public float Duration; }
        private struct CameraPose { public Vector3 Focus; public float Yaw; public float Pitch; public float Distance; }

        public RenderTexture Texture => targetTexture;
        public float Heading => Mathf.Repeat(yaw, 360f);
        public float CameraPitch => pitch;
        public float CameraDistance => distance;
        public int VisibleFormationCount { get; private set; }
        public int VisibleContactCount { get; private set; }
        public int ActiveEffectCount => activeEffects.Count;
        public int PooledEffectCount => effectPools.Values.Sum(pool => pool.Count);
        public int PooledMarkerCount => markerPool.Count;
        public int CoastlinePolygonCount { get; private set; }
        public int OffMapCoastlineVertexCount { get; private set; }
        public int TerrainReliefCount { get; private set; }
        public int WaterSurfaceVertexCount { get; private set; }
        public bool HasDirectionalSun { get; private set; }
        public float SunSourceAzimuthDegrees { get; private set; }
        public int FormationMeshVariantCount => formationMeshes.Count;
        public int GeographicLabelCount => billboardLabels.Count;
        public int CloudShadowCount { get; private set; }
        public bool HasAtmosphericHaze { get; private set; }
        public bool HasShallowWaterDetail { get; private set; }
        public bool HasCoastalFoam { get; private set; }
        public bool HasCoastlineDrivenShelf { get; private set; }
        public string WeatherPreset => string.IsNullOrWhiteSpace(area.Presentation.WeatherPreset) ? "Clear" : area.Presentation.WeatherPreset;
        public int PrecipitationStreakCount { get; private set; }
        public int PersistentWakeCount { get; private set; }
        public int AircraftContrailCount { get; private set; }
        public bool PersistentTrailsUseFormationSpace { get; private set; }
        public bool CameraIsSettling => cameraPanVelocity.sqrMagnitude > .0001f || Mathf.Abs(cameraYawVelocity) > .01f || Mathf.Abs(cameraPitchVelocity) > .01f || Mathf.Abs(cameraZoomVelocity) > .01f;
        public bool UsesProceduralSurfaceTextures => generatedTextures.Count >= 3;
        public bool PermanentGridVisible => hexLines.Any(pair => pair.Value != null && pair.Value.enabled && !pair.Key.Equals(area.Objective));
        public bool HasActiveFormationPulse => activeFormationRing != null;
        public bool ContainsRenderedName(string fragment)
        {
            if (string.IsNullOrEmpty(fragment)) return false;
            return root.GetComponentsInChildren<Transform>(true).Any(item => item.name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public void Resize(int width, int height)
        {
            width = Mathf.Clamp(width, 640, 3840);
            height = Mathf.Clamp(height, 360, 2160);
            if (Mathf.Abs(targetTexture.width - width) < 16 && Mathf.Abs(targetTexture.height - height) < 16) return;
            targetTexture.Release();
            targetTexture.width = width;
            targetTexture.height = height;
            targetTexture.Create();
        }
        public float ProbeRenderLuminance()
        {
            RenderTexture previous = RenderTexture.active;
            camera.Render();
            RenderTexture.active = targetTexture;
            const int sampleSize = 12;
            var sample = new Texture2D(sampleSize, sampleSize, TextureFormat.RGB24, false);
            sample.ReadPixels(new Rect(targetTexture.width * .5f - sampleSize * .5f, targetTexture.height * .5f - sampleSize * .5f, sampleSize, sampleSize), 0, 0, false);
            sample.Apply(false, false);
            Color[] pixels = sample.GetPixels();
            float luminance = pixels.Length == 0 ? 0f : pixels.Average(pixel => pixel.r * .2126f + pixel.g * .7152f + pixel.b * .0722f);
            DestroyObject(sample);
            RenderTexture.active = previous;
            return luminance;
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
            foamMaterial = TransparentMaterialFor("CoastalFoam", new Color(.74f, .92f, .9f, .34f), .08f, .52f);
            shallowWaterMaterial = TransparentMaterialFor("ShallowWater", new Color(.08f, .44f, .43f, .24f), .05f, .68f);
            atmosphericMaterial = TransparentMaterialFor("AtmosphericHaze", new Color(.24f, .46f, .55f, .12f), 0f, .1f);
            cloudShadowMaterial = TransparentMaterialFor("CloudShadow", new Color(.04f, .08f, .1f, .22f), 0f, .05f);
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
            bool overcast = WeatherPreset.IndexOf("Overcast", StringComparison.OrdinalIgnoreCase) >= 0 || WeatherPreset.IndexOf("Rain", StringComparison.OrdinalIgnoreCase) >= 0;
            Color daySky = Color.Lerp(new Color(.006f, .025f, .04f, 1f), new Color(.06f, .12f, .17f, 1f), 1f - area.Presentation.Visibility);
            camera.backgroundColor = night ? new Color(.002f, .008f, .018f, 1f) : overcast ? Color.Lerp(daySky, new Color(.14f, .18f, .2f, 1f), .58f) : daySky;
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
            sun.color = night ? new Color(.48f, .6f, .82f) : overcast ? new Color(.72f, .78f, .8f) : new Color(1f, .88f, .68f);
            sun.intensity = night ? .38f : (overcast ? .62f : Mathf.Lerp(1.05f, 1.45f, area.Presentation.Visibility));
            float sunElevation = Mathf.Lerp(24f, 52f, Mathf.Clamp01(1f - Mathf.Abs(area.Presentation.TimeOfDay - 12f) / 8f));
            SunSourceAzimuthDegrees = 67.5f;
            float sunRayHeading = Mathf.Repeat(SunSourceAzimuthDegrees + 180f, 360f);
            sun.transform.rotation = Quaternion.Euler(sunElevation, sunRayHeading, 0f);
            sunTransform = sun.transform;
            sun.cullingMask = 1 << MapLayer;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .68f;
            sun.shadowBias = .035f;
            sun.cookie = CreateCloudShadowCookie("Soft Maritime Cloud Cookie", 96, area.Presentation.CloudCover, area.Id == null ? 83 : area.Id.Aggregate(83, (value, character) => value * 31 + character));
            sun.cookieSize = Mathf.Max(area.Width, area.Height) * .82f;
            HasDirectionalSun = true;

            GameObject fillObject = Child("Sky Fill", root.transform);
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(.35f, .55f, .72f);
            fill.intensity = night ? .32f : overcast ? .42f : .22f;
            fill.transform.rotation = Quaternion.Euler(62f, SunSourceAzimuthDegrees, 0f);
            fill.cullingMask = 1 << MapLayer;
            fill.shadows = LightShadows.None;

            BuildSurface();
            BuildAtmosphere();
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
                if (waterMaterial.HasProperty("_BumpMap")) waterMaterial.SetTextureOffset("_BumpMap", new Vector2(waterScroll, waterScroll * .43f));
                if (waterSurfaceTransform != null) waterSurfaceTransform.localPosition = new Vector3(0f, WaterSurfaceY + Mathf.Sin(waterScroll * Mathf.PI * 18f) * .008f, 0f);
                if (sunTransform != null) sunTransform.position = new Vector3(waterScroll * 1.8f, 0f, waterScroll * .72f);
            }
            foreach (Transform label in billboardLabels) if (label != null) label.rotation = camera.transform.rotation;
            UpdateActiveFormationPulse(deltaTime);
            for (int i = motions.Count - 1; i >= 0; i--)
            {
                Motion motion = motions[i];
                if (motion.Transform == null) { motions.RemoveAt(i); continue; }
                motion.Progress = reducedMotion ? 1f : Mathf.Clamp01(motion.Progress + deltaTime * 2.5f);
                float eased = motion.Progress * motion.Progress * (3f - 2f * motion.Progress);
                motion.Transform.position = Vector3.Lerp(motion.Start, motion.End, eased);
                motion.Transform.rotation = Quaternion.Slerp(motion.StartRotation, motion.EndRotation, eased);
                if (motion.Progress >= 1f) motions.RemoveAt(i);
            }
            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                EffectInstance effect = activeEffects[i];
                effect.Age += deltaTime;
                if (!reducedMotion) AnimateEffect(effect);
                if (effect.Age >= effect.Duration) { ReleaseEffect(effect.Object); activeEffects.RemoveAt(i); }
            }
        }

        private void AnimateEffect(EffectInstance effect)
        {
            float progress = Mathf.Clamp01(effect.Age / effect.Duration);
            switch (effect.Kind)
            {
                case OperationalEffectKind.SearchSweep:
                    effect.Object.transform.localScale = Vector3.Lerp(effect.BaseScale * .12f, effect.BaseScale, progress);
                    effect.Object.transform.Rotate(Vector3.up, 95f * Time.deltaTime, Space.World);
                    break;
                case OperationalEffectKind.MovementWake:
                    effect.Object.transform.localScale = new Vector3(effect.BaseScale.x * (1f + progress * 1.8f), effect.BaseScale.y, effect.BaseScale.z * (1f + progress));
                    effect.Object.transform.position = effect.Start + new Vector3(0f, Mathf.Sin(effect.Age * 8f) * .018f, -progress * .34f);
                    break;
                case OperationalEffectKind.Launch:
                    effect.Object.transform.position = effect.Start + Vector3.up * progress * 1.5f;
                    effect.Object.transform.localScale = effect.BaseScale * Mathf.Lerp(1f, .35f, progress);
                    break;
                case OperationalEffectKind.Interception:
                    effect.Object.transform.Rotate(Vector3.up, 260f * Time.deltaTime, Space.World);
                    effect.Object.transform.localScale = effect.BaseScale * (1f + Mathf.Sin(effect.Age * 12f) * .18f);
                    break;
                case OperationalEffectKind.Impact:
                    effect.Object.transform.localScale = effect.BaseScale * Mathf.Sin(progress * Mathf.PI) * 1.8f;
                    break;
                case OperationalEffectKind.Damage:
                    effect.Object.transform.position = effect.Start + Vector3.up * progress * .7f;
                    effect.Object.transform.localScale = effect.BaseScale * Mathf.Lerp(1f, .3f, progress);
                    break;
                default:
                    effect.Object.transform.localScale = effect.BaseScale * (1f + Mathf.Sin(effect.Age * 5f) * .12f);
                    break;
            }
        }

        public void TriggerEffect(OperationalEffectKind kind, HexCoord hex)
        {
            if (!effectPools.TryGetValue(kind, out Stack<GameObject> pool)) effectPools[kind] = pool = new Stack<GameObject>();
            GameObject effect = pool.Count > 0 ? pool.Pop() : CreateEffectObject(kind);
            effect.name = "VFX " + kind;
            effect.SetActive(true);
            effect.transform.SetParent(root.transform, false);
            effect.transform.position = HexToWorld(hex) + Vector3.up * (kind == OperationalEffectKind.MovementWake || kind == OperationalEffectKind.SearchSweep ? .035f : .3f);
            effect.transform.rotation = Quaternion.identity;
            Vector3 scale;
            if (kind == OperationalEffectKind.SearchSweep) scale = new Vector3(1.5f, .025f, 1.5f);
            else if (kind == OperationalEffectKind.MovementWake) scale = new Vector3(.18f, .025f, .8f);
            else if (kind == OperationalEffectKind.Launch) scale = new Vector3(.08f, .7f, .08f);
            else if (kind == OperationalEffectKind.Interception) scale = new Vector3(.42f, .08f, .42f);
            else if (kind == OperationalEffectKind.Impact || kind == OperationalEffectKind.Damage) scale = Vector3.one * .42f;
            else scale = Vector3.one * .3f;
            effect.transform.localScale = scale;
            activeEffects.Add(new EffectInstance { Kind = kind, Object = effect, BaseScale = scale, Start = effect.transform.position, Duration = reducedMotion ? 3600f : 1.35f });
        }

        private GameObject CreateEffectObject(OperationalEffectKind kind)
        {
            PrimitiveType type = kind == OperationalEffectKind.SearchSweep || kind == OperationalEffectKind.Interception ? PrimitiveType.Cylinder
                : kind == OperationalEffectKind.MovementWake ? PrimitiveType.Cube
                : kind == OperationalEffectKind.Launch ? PrimitiveType.Capsule
                : kind == OperationalEffectKind.Detection ? PrimitiveType.Cube
                : PrimitiveType.Sphere;
            Material material = kind == OperationalEffectKind.SearchSweep || kind == OperationalEffectKind.Detection ? contactMaterial
                : kind == OperationalEffectKind.MovementWake ? foamMaterial : warningMaterial;
            GameObject effect = Primitive(type, "Pooled " + kind + " Effect", root.transform, material);
            if (kind == OperationalEffectKind.Detection) effect.transform.localRotation = Quaternion.Euler(35f, 45f, 35f);
            return effect;
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
            string suffix = effect.name.StartsWith("VFX ", StringComparison.Ordinal) ? effect.name.Substring(4) : string.Empty;
            if (!Enum.TryParse(suffix, out OperationalEffectKind kind)) kind = OperationalEffectKind.Impact;
            if (!effectPools.TryGetValue(kind, out Stack<GameObject> pool)) effectPools[kind] = pool = new Stack<GameObject>();
            pool.Push(effect);
        }

        public void SetHoverHex(HexCoord? hex)
        {
            if (hoverRing == null || previewLine == null) return;
            bool visible = hex.HasValue && area.Contains(hex.Value);
            hoverRing.gameObject.SetActive(visible);
            previewLine.gameObject.SetActive(false);
            if (!visible) return;

            SetRingPositions(hoverRing, hex.Value, .82f, .13f);
            bool legalMove = actionMode == ToolkitActionMode.Move && game?.Active != null && game.IsLegalMoveDestination(game.Active, hex.Value, moveMode);
            bool legalSearch = actionMode == ToolkitActionMode.Search && game?.Active != null && HexCoord.Distance(game.Active.Position, hex.Value) <= game.SearchRangeFor(game.Active, searchMode);
            Color hoverColor = legalMove ? new Color(.18f, 1f, .58f, 1f) : legalSearch ? new Color(.16f, .82f, 1f, 1f) : new Color(.88f, .95f, 1f, .82f);
            hoverRing.startColor = hoverRing.endColor = hoverColor;
            hoverRing.widthMultiplier = legalMove || legalSearch ? .085f : .05f;
            if (legalMove)
            {
                previewLine.gameObject.SetActive(true);
                game.TryGetMovePath(game.Active, hex.Value, moveMode, out List<HexCoord> path, out _);
                previewLine.positionCount = path.Count;
                for (int i = 0; i < path.Count; i++) previewLine.SetPosition(i, HexToWorld(path[i]) + Vector3.up * .2f);
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
            StopCameraInertia();
            Vector3 right = camera.transform.right;
            Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
            float scale = distance * .0015f;
            focus += (-right * pixels.x - forward * pixels.y) * scale;
            ClampFocus();
            UpdateCamera();
        }

        public void Orbit(Vector2 pixels)
        {
            StopCameraInertia();
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
            float response = 1f - Mathf.Exp(-deltaTime * 9f);
            float panSpeed = Mathf.Max(5f, distance * .52f) * multiplier;
            Vector3 targetPan = (right * rightInput + forward * forwardInput) * panSpeed;
            cameraPanVelocity = Vector3.Lerp(cameraPanVelocity, targetPan, response);
            cameraYawVelocity = Mathf.Lerp(cameraYawVelocity, yawInput * 62f * multiplier, response);
            cameraPitchVelocity = Mathf.Lerp(cameraPitchVelocity, pitchInput * 44f * multiplier, response);
            cameraZoomVelocity = Mathf.Lerp(cameraZoomVelocity, zoomInput * 1.25f * multiplier, response);
            if (targetPan.sqrMagnitude < .001f) cameraPanVelocity = Vector3.MoveTowards(cameraPanVelocity, Vector3.zero, panSpeed * deltaTime * 4f);
            if (Mathf.Abs(yawInput) < .001f) cameraYawVelocity = Mathf.MoveTowards(cameraYawVelocity, 0f, 180f * deltaTime);
            if (Mathf.Abs(pitchInput) < .001f) cameraPitchVelocity = Mathf.MoveTowards(cameraPitchVelocity, 0f, 150f * deltaTime);
            if (Mathf.Abs(zoomInput) < .001f) cameraZoomVelocity = Mathf.MoveTowards(cameraZoomVelocity, 0f, 4f * deltaTime);
            focus += cameraPanVelocity * deltaTime;
            yaw = Mathf.Repeat(yaw + cameraYawVelocity * deltaTime, 360f);
            pitch = Mathf.Clamp(pitch + cameraPitchVelocity * deltaTime, 24f, 82f);
            distance *= Mathf.Exp(cameraZoomVelocity * deltaTime);
            distance = Mathf.Clamp(distance, area.Presentation.MinimumZoom, Mathf.Max(area.Width, area.Height) * area.Presentation.MaximumZoomMultiplier);
            ClampFocus();
            UpdateCamera();
            UpdateLod();
        }

        public void Rotate(float direction)
        {
            StopCameraInertia();
            yaw = Mathf.Round((yaw + Mathf.Sign(direction) * 30f) / 30f) * 30f;
            UpdateCamera();
        }

        public void Zoom(float wheelDelta)
        {
            StopCameraInertia();
            distance = Mathf.Clamp(distance * (wheelDelta > 0 ? .86f : 1.16f), area.Presentation.MinimumZoom, Mathf.Max(area.Width, area.Height) * area.Presentation.MaximumZoomMultiplier);
            UpdateCamera();
            UpdateLod();
        }

        public void ResetCamera()
        {
            StopCameraInertia();
            Vector3 first = HexToWorld(new HexCoord(0, 0));
            Vector3 last = HexToWorld(new HexCoord(area.Width - 1, area.Height - 1));
            focus = (first + last) * .5f;
            yaw = area.Presentation.CameraYaw;
            pitch = area.Presentation.CameraPitch;
            distance = Mathf.Max(area.Width, area.Height) * area.Presentation.CameraZoomMultiplier;
            UpdateCamera();
        }

        public void FocusHex(HexCoord hex, bool close = true)
        {
            if (!area.Contains(hex)) return;
            StopCameraInertia();
            focus = HexToWorld(hex);
            if (close) distance = Mathf.Clamp(Mathf.Max(area.Presentation.MinimumZoom, Mathf.Max(area.Width, area.Height) * .72f), area.Presentation.MinimumZoom, Mathf.Max(area.Width, area.Height) * area.Presentation.MaximumZoomMultiplier);
            ClampFocus();
            UpdateCamera();
            UpdateLod();
        }

        public void SaveCameraView()
        {
            savedCameraPose = new CameraPose { Focus = focus, Yaw = yaw, Pitch = pitch, Distance = distance };
            hasSavedCameraPose = true;
        }

        public bool RecallCameraView()
        {
            if (!hasSavedCameraPose) return false;
            StopCameraInertia();
            focus = savedCameraPose.Focus; yaw = savedCameraPose.Yaw; pitch = savedCameraPose.Pitch; distance = savedCameraPose.Distance;
            ClampFocus(); UpdateCamera(); UpdateLod();
            return true;
        }

        private void StopCameraInertia()
        {
            cameraPanVelocity = Vector3.zero;
            cameraYawVelocity = cameraPitchVelocity = cameraZoomVelocity = 0f;
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
            waterSurfaceTransform = waterSurface.transform;

            foreach (TerrainHexDefinition terrain in area.Terrain.Where(item => item.Terrain == OperationalTerrain.Littoral || item.Terrain == OperationalTerrain.Strait))
            {
                HasShallowWaterDetail = true;
                LineRenderer depthContour = Ring("Bathymetric Hex Contour " + terrain.Hex, terrain.Hex, .88f, new Color(.18f, .62f, .66f, .16f), .015f);
                depthContour.transform.SetParent(root.transform, true);
                SetRingPositions(depthContour, terrain.Hex, .88f, WaterSurfaceY + .025f);
            }

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
                ridge.transform.position = new Vector3(center.x, GeographicLandSurfaceY, center.z);
                ridge.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(polygonWidth, polygonDepth) * Mathf.Rad2Deg * .18f, 0f);
                Renderer renderer = ridge.GetComponent<Renderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;
                TerrainReliefCount++;
                for (int shoulderIndex = 0; shoulderIndex < 2; shoulderIndex++)
                {
                    float side = shoulderIndex == 0 ? -1f : 1f;
                    GameObject shoulder = MeshObject("Terrain Shoulder", reliefRoot.transform, shoulderIndex == 0 ? landMaterial : highlandMaterial, CreateTerrainRidgeMesh(width * .54f, depth * .48f, height * .56f, seed + 97 + shoulderIndex * 131));
                    shoulder.transform.position = new Vector3(center.x + side * width * .18f, GeographicLandSurfaceY, center.z + side * depth * .12f);
                    shoulder.transform.rotation = Quaternion.Euler(0f, side * 17f, 0f);
                    Renderer shoulderRenderer = shoulder.GetComponent<Renderer>();
                    shoulderRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    shoulderRenderer.receiveShadows = true;
                    TerrainReliefCount++;
                }
            }
        }

        private bool BuildPolygonCoastline(Vector3 first, Vector3 last)
        {
            if (string.IsNullOrWhiteSpace(area.CoastlineResource)) return false;
            TextAsset source = Resources.Load<TextAsset>(area.CoastlineResource);
            if (source == null) { Debug.LogError("Missing coastline resource: " + area.CoastlineResource); return false; }
            CoastlineData coastline = JsonUtility.FromJson<CoastlineData>(source.text);
            var projectionBounds = Rect.MinMaxRect(Mathf.Min(first.x, last.x) - 1f, Mathf.Min(first.z, last.z) - .87f, Mathf.Max(first.x, last.x) + 1f, Mathf.Max(first.z, last.z) + .87f);
            Mesh mesh = CoastlinePolygonMesh.Create(coastline, projectionBounds, GeographicLandSurfaceY, out List<Vector3[]> shorelines);
            if (mesh == null) { Debug.LogError("Coastline resource produced no valid polygon mesh: " + area.CoastlineResource); return false; }
            MeshObject("Natural Earth Land", root.transform, landMaterial, mesh);
            ApplyCoastlineDrivenShelf(shorelines, first, last);
            foreach (Vector3[] shoreline in shorelines)
            {
                GameObject foamObject = Child("Natural Earth Coastal Foam", root.transform);
                LineRenderer foam = foamObject.AddComponent<LineRenderer>();
                foam.sharedMaterial = foamMaterial;
                foam.useWorldSpace = true;
                foam.loop = true;
                foam.positionCount = shoreline.Length;
                foam.widthMultiplier = .048f;
                foam.startColor = foam.endColor = new Color(.74f, .92f, .9f, .27f);
                foam.SetPositions(shoreline.Select(point => point + Vector3.up * .006f).ToArray());
                GameObject lineObject = Child("Natural Earth Sandy Shoreline", root.transform);
                LineRenderer line = lineObject.AddComponent<LineRenderer>();
                line.sharedMaterial = lineMaterial;
                line.useWorldSpace = true;
                line.loop = true;
                line.positionCount = shoreline.Length;
                line.widthMultiplier = .021f;
                line.startColor = line.endColor = new Color(.68f, .69f, .43f, .62f);
                line.SetPositions(shoreline.Select(point => point + Vector3.up * .012f).ToArray());
                HasCoastalFoam = true;
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
                if (location.Kind == LocationKind.Port)
                {
                    GameObject pier = Primitive(PrimitiveType.Cube, "Port Pier — " + location.Name, root.transform, navalDeckMaterial);
                    pier.transform.position = HexToWorld(location.Hex) + new Vector3(.12f, .13f, -.05f);
                    pier.transform.localScale = new Vector3(.34f, .035f, .07f);
                }
                else if (location.Kind == LocationKind.Airfield)
                {
                    GameObject crossRunway = Primitive(PrimitiveType.Cube, "Airfield Cross Runway — " + location.Name, root.transform, navalDeckMaterial);
                    crossRunway.transform.position = marker.transform.position + Vector3.up * .005f;
                    crossRunway.transform.rotation = Quaternion.Euler(0f, -45f, 0f);
                    crossRunway.transform.localScale = new Vector3(.22f, .02f, .035f);
                }
                bool night = area.Presentation.TimeOfDay < 6f || area.Presentation.TimeOfDay > 19f;
                if (night && (location.Kind == LocationKind.Port || location.Kind == LocationKind.Airfield))
                {
                    GameObject lightObject = Child("Coastal Facility Light — " + location.Name, root.transform);
                    lightObject.transform.position = HexToWorld(location.Hex) + Vector3.up * .34f;
                    Light facilityLight = lightObject.AddComponent<Light>();
                    facilityLight.type = LightType.Point;
                    facilityLight.color = location.Kind == LocationKind.Airfield ? new Color(.72f, .9f, 1f) : new Color(1f, .72f, .28f);
                    facilityLight.intensity = .48f;
                    facilityLight.range = .9f;
                    facilityLight.cullingMask = 1 << MapLayer;
                    facilityLight.shadows = LightShadows.None;
                }
                BuildGeographicLabel(location);
            }
        }

        private void BuildGeographicLabel(OperationalLocationDefinition location)
        {
            GameObject labelObject = Child("Geographic Label — " + location.Name, root.transform);
            labelObject.transform.position = HexToWorld(location.Hex) + new Vector3(.22f, .56f, .08f);
            TextMesh text = labelObject.AddComponent<TextMesh>();
            text.text = location.Name.ToUpperInvariant();
            text.fontSize = 36;
            text.characterSize = .018f;
            text.anchor = TextAnchor.MiddleLeft;
            text.alignment = TextAlignment.Left;
            text.color = location.Kind == LocationKind.Objective ? new Color(1f, .74f, .18f, .95f) : new Color(.72f, .88f, .9f, .78f);
            MeshRenderer renderer = labelObject.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sortingOrder = 2;
            billboardLabels.Add(labelObject.transform);
        }

        private void BuildAtmosphere()
        {
            Vector3 first = HexToWorld(new HexCoord(0, 0));
            Vector3 last = HexToWorld(new HexCoord(area.Width - 1, area.Height - 1));
            Vector3 center = (first + last) * .5f;
            float width = Mathf.Abs(last.x - first.x) + 14f;
            float depth = Mathf.Abs(last.z - first.z) + 14f;
            // Haze is expressed through the camera background, visibility-dependent light balance,
            // and distant cloud shadows. A full-map transparent slab is intentionally avoided: on
            // some Windows render paths it can sort as opaque and cover the tactical scene.
            HasAtmosphericHaze = true;

            cloudShadowRoot = Child("Moving Cloud Shadows", root.transform).transform;
            int cloudCount = Mathf.Clamp(Mathf.RoundToInt(2f + area.Presentation.CloudCover * 12f), 2, 8);
            CloudShadowCount = cloudCount;
            var random = new System.Random(area.Id == null ? 83 : area.Id.Aggregate(83, (value, character) => value * 31 + character));
            if (area.Presentation.Precipitation > .01f || WeatherPreset.IndexOf("Rain", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                int streaks = Mathf.Clamp(Mathf.RoundToInt(18f + area.Presentation.Precipitation * 42f), 18, 60);
                for (int i = 0; i < streaks; i++)
                {
                    float x = Mathf.Lerp(-width * .46f, width * .46f, (float)random.NextDouble());
                    float z = Mathf.Lerp(-depth * .46f, depth * .46f, (float)random.NextDouble());
                    Vector3 start = center + new Vector3(x, 2.4f, z);
                    LineRenderer rain = Segment("Rain Streak", start, start + new Vector3(-.12f, -.72f, .04f), new Color(.55f, .72f, .8f, .24f), .012f);
                    rain.transform.SetParent(root.transform, true);
                    PrecipitationStreakCount++;
                }
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
                        bool legal = game.IsLegalMoveDestination(game.Active, pair.Key, moveMode);
                        if (legal) { color = new Color(.18f, 1f, .58f, 1f); width = .07f; }
                    }
                    else if (actionMode == ToolkitActionMode.Search)
                    {
                        int range = game.SearchRangeFor(game.Active, searchMode);
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
                    else if (actionMode == ToolkitActionMode.Replenish)
                    {
                        bool facility = game.LogisticsFacilitiesFor(game.Active).Any(location => location.Hex.Equals(pair.Key));
                        if (facility)
                        {
                            bool current = pair.Key.Equals(game.Active.Position);
                            color = current ? new Color(.18f, 1f, .58f, 1f) : new Color(1f, .72f, .16f, .92f);
                            width = current ? .1f : .075f;
                        }
                        else { color = new Color(.25f, .32f, .38f, .14f); width = .018f; }
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
            activeFormationRing = null;
            foreach (GameObject stateObject in stateObjects) RecycleMarker(stateObject);
            stateObjects.Clear();
            motions.Clear();
            lodPairs.Clear();
            VisibleFormationCount = 0;
            VisibleContactCount = 0;
            PersistentWakeCount = 0;
            AircraftContrailCount = 0;
            PersistentTrailsUseFormationSpace = true;
            if (game?.Active == null) return;
            Side viewer = game.Active.Side;
            foreach (FormationState formation in game.Formations.Where(f => !f.IsDestroyed && f.Side == viewer))
            {
                stateObjects.Add(BuildFormation(formation));
                VisibleFormationCount++;
            }
            foreach (FormationState formation in game.Formations.Where(f => !f.IsDestroyed && f.Side == viewer && (f.PatrolActive || f.SupportActive)))
                stateObjects.Add(BuildOperationalAssignment(formation));
            foreach (ContactState contact in game.Contacts.Where(c => c.Owner == viewer && !c.IsLost))
            {
                stateObjects.Add(BuildContact(contact));
                VisibleContactCount++;
            }
        }

        private GameObject BuildOperationalAssignment(FormationState formation)
        {
            GameObject marker = AcquireMarker(formation.Name + " Operational Assignment");
            if (formation.PatrolActive)
            {
                Color color = formation.PatrolPosture == PatrolPosture.Defensive ? new Color(.18f, .72f, 1f, .78f)
                    : formation.PatrolPosture == PatrolPosture.Aggressive ? new Color(1f, .38f, .12f, .84f)
                    : new Color(.25f, 1f, .65f, .76f);
                LineRenderer areaRing = Ring("Patrol Screen Area", formation.PatrolCenter, 1.62f, color, formation.PatrolInterceptionAvailable ? .075f : .035f);
                areaRing.transform.SetParent(marker.transform, true);
                LineRenderer link = Segment("Screen Assignment", HexToWorld(formation.Position) + Vector3.up * .16f, HexToWorld(formation.PatrolCenter) + Vector3.up * .16f, color, .045f);
                link.transform.SetParent(marker.transform, true);
            }
            if (formation.SupportActive)
            {
                FormationState recipient = game.Find(formation.SupportRecipientId);
                if (recipient != null && !recipient.IsDestroyed)
                {
                    Color color = new Color(.72f, .48f, 1f, .88f);
                    LineRenderer link = Segment("Support Relationship", HexToWorld(formation.Position) + Vector3.up * .24f, HexToWorld(recipient.Position) + Vector3.up * .24f, color, .065f);
                    link.transform.SetParent(marker.transform, true);
                    LineRenderer recipientRing = Ring("Support Recipient", recipient.Position, .7f, color, .055f);
                    recipientRing.transform.SetParent(marker.transform, true);
                }
            }
            return marker;
        }

        private GameObject BuildFormation(FormationState formation)
        {
            GameObject marker = AcquireMarker(formation.Name);
            float markerHeight = formation.Kind == FormationKind.AirGroup ? 1.15f : formation.Kind == FormationKind.Submarine ? .025f : .015f;
            Vector3 destination = HexToWorld(formation.Position) + Vector3.up * markerHeight;
            Vector3 start = presentedPositions.TryGetValue(formation.Id, out HexCoord oldHex) ? HexToWorld(oldHex) + Vector3.up * markerHeight : destination;
            Quaternion previousRotation = presentedRotations.TryGetValue(formation.Id, out Quaternion storedRotation) ? storedRotation : Quaternion.identity;
            Vector3 travelDirection = destination - start;
            travelDirection.y = 0f;
            Quaternion targetRotation = travelDirection.sqrMagnitude > .0001f ? Quaternion.LookRotation(travelDirection.normalized, Vector3.up) : previousRotation;
            marker.transform.position = reducedMotion ? destination : start;
            marker.transform.rotation = reducedMotion ? targetRotation : previousRotation;
            if (start != destination) motions.Add(new Motion { Transform = marker.transform, Start = start, End = destination, StartRotation = previousRotation, EndRotation = targetRotation });
            presentedPositions[formation.Id] = formation.Position;
            presentedRotations[formation.Id] = targetRotation;
            Material material = formation.Side == Side.Blue ? blueMaterial : redMaterial;
            GameObject detail = Child("Production Formation Model", marker.transform);
            switch (formation.Kind)
            {
                case FormationKind.CarrierGroup: BuildCarrierGroup(detail.transform, material); break;
                case FormationKind.SurfaceGroup: BuildSurfaceGroup(detail.transform, material); break;
                case FormationKind.Submarine: BuildSubmarine(detail.transform, material); break;
                case FormationKind.AirGroup: BuildAircraft(detail.transform, material); break;
                case FormationKind.LogisticsGroup: BuildLogisticsGroup(detail.transform, material); break;
            }
            AddRecognitionMarking(detail.transform, formation.Kind, material);
            AddPersistentWake(marker.transform, formation.Kind);
            PrimitiveType symbolType = formation.Kind == FormationKind.Submarine ? PrimitiveType.Sphere : formation.Kind == FormationKind.AirGroup ? PrimitiveType.Cylinder : PrimitiveType.Cube;
            GameObject symbol = Primitive(symbolType, formation.Kind + " Distant Operational Symbol", marker.transform, material);
            symbol.transform.localScale = formation.Kind == FormationKind.CarrierGroup ? new Vector3(.24f, .025f, .42f)
                : formation.Kind == FormationKind.SurfaceGroup ? new Vector3(.17f, .025f, .34f)
                : formation.Kind == FormationKind.Submarine ? new Vector3(.17f, .06f, .29f)
                : formation.Kind == FormationKind.AirGroup ? new Vector3(.28f, .025f, .28f) : new Vector3(.24f, .025f, .30f);
            if (formation.Kind == FormationKind.AirGroup) symbol.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            lodPairs.Add(new LodPair { Detail = detail, Symbol = symbol });
            bool active = formation == game.Active;
            LineRenderer ring = Ring(active ? "Active Formation Pulse" : "Formation Selection", formation.Position, active ? .72f : .48f, active ? new Color(1f, .68f, .14f, 1f) : material.color, active ? .16f : .045f);
            ring.transform.SetParent(marker.transform, true);
            if (active)
            {
                activeFormationRing = ring;
                activePulsePhase = 0f;
                LineRenderer coreRing = Ring("Active Formation Core Ring", formation.Position, .42f, new Color(1f, .92f, .32f, 1f), .065f);
                coreRing.transform.SetParent(marker.transform, true);
                GameObject beacon = Primitive(PrimitiveType.Cylinder, "Ready Beacon", marker.transform, warningMaterial);
                beacon.transform.localPosition = new Vector3(-.55f, .72f, 0f);
                beacon.transform.localScale = new Vector3(.06f, .72f, .06f);
                GameObject beaconCap = Primitive(PrimitiveType.Sphere, "Active Formation Beacon Cap", marker.transform, warningMaterial);
                beaconCap.transform.localPosition = new Vector3(-.55f, 1.46f, 0f);
                beaconCap.transform.localScale = Vector3.one * .14f;
            }
            AddFormationStatusCues(marker, formation);
            UpdateLod();
            return marker;
        }

        private void AddRecognitionMarking(Transform parent, FormationKind kind, Material sideMaterial)
        {
            GameObject marking = Primitive(PrimitiveType.Cube, kind + " Side Recognition Marking", parent, sideMaterial);
            marking.transform.localPosition = kind == FormationKind.AirGroup ? new Vector3(0f, .035f, -.05f) : new Vector3(0f, .18f, 0f);
            marking.transform.localScale = kind == FormationKind.CarrierGroup ? new Vector3(.035f, .008f, .62f)
                : kind == FormationKind.SurfaceGroup ? new Vector3(.035f, .008f, .38f)
                : kind == FormationKind.Submarine ? new Vector3(.035f, .035f, .26f)
                : kind == FormationKind.AirGroup ? new Vector3(.52f, .008f, .035f) : new Vector3(.24f, .008f, .30f);
        }

        private void AddPersistentWake(Transform parent, FormationKind kind)
        {
            if (kind == FormationKind.Submarine) return;
            if (kind == FormationKind.AirGroup)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 start = new Vector3(side * .055f, -.08f, -.38f);
                    Vector3 end = new Vector3(side * .055f, -.08f, -1.22f);
                    LineRenderer contrail = LocalTrail(parent, "Persistent Aircraft Contrail", start, end,
                        new Color(.9f, .97f, 1f, .68f), new Color(.72f, .9f, 1f, .06f), .018f, .07f, lineMaterial);
                    PersistentTrailsUseFormationSpace &= !contrail.useWorldSpace;
                    PersistentWakeCount++;
                    AircraftContrailCount++;
                }
                return;
            }

            float length = kind == FormationKind.CarrierGroup ? .92f : kind == FormationKind.LogisticsGroup ? .78f : .72f;
            float spread = kind == FormationKind.CarrierGroup ? .22f : .13f;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 start = new Vector3(side * spread, .025f, -.22f);
                Vector3 end = new Vector3(side * spread * 2.2f, .025f, -.22f - length);
                LineRenderer wake = LocalTrail(parent, "Persistent Surface Wake", start, end,
                    new Color(.76f, .96f, .96f, .52f), new Color(.62f, .88f, .9f, .05f), .035f, .085f, foamMaterial);
                PersistentTrailsUseFormationSpace &= !wake.useWorldSpace;
                PersistentWakeCount++;
            }
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

        private void BuildLogisticsGroup(Transform parent, Material sideMaterial)
        {
            Mesh oilerMesh = FormationHull("Fleet Oiler Hull", .72f, .31f, .17f, .75f);
            MeshObject("Fleet Oiler Hull", parent, navalHullMaterial, oilerMesh);
            GameObject deckhouse = Primitive(PrimitiveType.Cube, "Fleet Oiler Deckhouse", parent, sideMaterial);
            deckhouse.transform.localPosition = new Vector3(0f, .18f, .18f);
            deckhouse.transform.localScale = new Vector3(.20f, .16f, .18f);
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject tank = Primitive(PrimitiveType.Cylinder, "Replenishment Tank", parent, navalDeckMaterial);
                tank.transform.localPosition = new Vector3(side * .08f, .15f, -.12f);
                tank.transform.localScale = new Vector3(.055f, .13f, .055f);
                tank.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
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
            bool eligible = actionMode == ToolkitActionMode.Search && game?.Active != null && HexCoord.Distance(game.Active.Position, contact.LastKnownPosition) <= game.SearchRangeFor(game.Active, searchMode) || actionMode == ToolkitActionMode.Strike && StrikeEligible(contact);
            Color stateColor = eligible ? new Color(.2f, 1f, .62f, 1f) : contact.Age >= 3 ? new Color(.62f, .49f, .38f, .72f) : contactMaterial.color;

            GameObject diamond = Primitive(PrimitiveType.Cube, contact.Identity == IdentityQuality.Identified ? "Identified Contact" : "Unknown Contact", marker.transform, contactMaterial);
            float scale = contact.Identity == IdentityQuality.Identified ? .36f : contact.Identity == IdentityQuality.General ? .31f : .26f;
            diamond.transform.localScale = Vector3.one * scale;
            diamond.transform.localRotation = Quaternion.Euler(35f, 45f, 35f);
            if (contact.HasContradictoryPosition)
            {
                GameObject contradictory = Primitive(PrimitiveType.Cube, "Contradictory Contact Fix", marker.transform, contactMaterial);
                contradictory.transform.localPosition = HexToWorld(contact.ContradictoryPosition) - HexToWorld(contact.LastKnownPosition);
                contradictory.transform.localScale = Vector3.one * (scale * .82f);
                contradictory.transform.localRotation = Quaternion.Euler(35f, 45f, 35f);
            }

            IReadOnlyList<HexCoord> possibleHexes = game.ContactPossibleHexes(contact);
            for (int i = 0; i < possibleHexes.Count; i++)
            {
                HexCoord possible = possibleHexes[i];
                int distanceFromFix = HexCoord.Distance(contact.LastKnownPosition, possible);
                Color ringColor = new Color(stateColor.r, stateColor.g, stateColor.b, Mathf.Clamp01(stateColor.a - distanceFromFix * .14f));
                LineRenderer ring = Ring(i == 0 ? "Contact Fix" : "Possible Contact Hex " + i, possible, .46f, ringColor, eligible && i == 0 ? .09f : .032f);
                ring.transform.SetParent(marker.transform, true);
            }
            return marker;
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

        private static LineRenderer LocalTrail(Transform parent, string name, Vector3 start, Vector3 end, Color startColor, Color endColor, float startWidth, float endWidth, Material material)
        {
            GameObject lineObject = Child(name, parent);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.startColor = startColor;
            line.endColor = endColor;
            line.startWidth = startWidth;
            line.endWidth = endWidth;
            return line;
        }

        private void UpdateObjectiveControl()
        {
            if (objectiveRing == null || game == null) return;
            Side viewingSide = game.Active?.Side ?? Side.Blue;
            bool blue = viewingSide == Side.Blue && game.HasControlPresence(Side.Blue, area.Objective);
            bool red = viewingSide == Side.Red && game.HasControlPresence(Side.Red, area.Objective);
            Color color = blue && !red ? blueMaterial.color : red && !blue ? redMaterial.color : blue && red ? new Color(1f, .72f, .14f, 1f) : new Color(.82f, .9f, .94f, .75f);
            objectiveRing.startColor = objectiveRing.endColor = color;
            objectiveRing.widthMultiplier = blue || red ? .12f : .075f;
        }

        private void UpdateActiveFormationPulse(float deltaTime)
        {
            if (activeFormationRing == null || game?.Active == null) return;
            if (reducedMotion)
            {
                activeFormationRing.widthMultiplier = .12f;
                activeFormationRing.startColor = activeFormationRing.endColor = new Color(1f, .68f, .14f, .95f);
                SetRingPositions(activeFormationRing, game.Active.Position, .62f, .12f);
                return;
            }
            activePulsePhase = Mathf.Repeat(activePulsePhase + deltaTime * 1.35f, 1f);
            float wave = .5f - .5f * Mathf.Cos(activePulsePhase * Mathf.PI * 2f);
            float radius = Mathf.Lerp(.5f, .82f, wave);
            float alpha = Mathf.Lerp(.95f, .3f, wave);
            activeFormationRing.widthMultiplier = Mathf.Lerp(.15f, .055f, wave);
            activeFormationRing.startColor = activeFormationRing.endColor = new Color(1f, .68f, .14f, alpha);
            SetRingPositions(activeFormationRing, game.Active.Position, radius, .12f);
        }

        private bool StrikeEligible(ContactState contact)
        {
            if (contact == null) return false;
            if (!game.ContactPossibleHexes(contact).Any(hex => HexCoord.Distance(game.Active.Position, hex) <= Rules.StrikeRange(game.Active.Kind, salvo))) return false;
            return salvo != Salvo.Heavy || game.Active.CanHeavySalvo;
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
            Texture2D waterTexture = CreateOceanSurfaceTexture("Theater Ocean Color and Depth", 512);
            Texture2D landTexture = CreateSurfaceTexture("Procedural Land Detail", 192, new Color(.11f, .15f, .06f), new Color(.33f, .34f, .16f), 41, false);
            Texture2D littoralTexture = CreateSurfaceTexture("Procedural Littoral Detail", 128, new Color(.025f, .19f, .21f), new Color(.18f, .52f, .42f), 73, false);
            Texture2D waterNormal = CreateNormalTexture("Multi-scale Ocean Normals", 256, 17, true, .72f + area.Presentation.SeaState * .32f);
            Texture2D landNormal = CreateNormalTexture("Terrain Relief Normals", 192, 41, false, 2.3f);
            waterMaterial.mainTexture = waterTexture;
            waterMaterial.mainTextureScale = Vector2.one;
            waterMaterial.color = Color.white;
            ApplyNormalMap(waterMaterial, waterNormal, .055f + area.Presentation.SeaState * .065f);
            if (waterMaterial.HasProperty("_Glossiness")) waterMaterial.SetFloat("_Glossiness", .36f);
            if (waterMaterial.HasProperty("_Metallic")) waterMaterial.SetFloat("_Metallic", .025f);
            landMaterial.mainTexture = landTexture;
            landMaterial.mainTextureScale = new Vector2(4f, 4f);
            landMaterial.color = Color.white;
            ApplyNormalMap(landMaterial, landNormal, .62f);
            highlandMaterial.mainTexture = landTexture;
            highlandMaterial.mainTextureScale = new Vector2(2.5f, 2.5f);
            highlandMaterial.color = Color.white;
            ApplyNormalMap(highlandMaterial, landNormal, .84f);
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

        private Texture2D CreateOceanSurfaceTexture(string name, int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var colors = new Color[size * size];
            Color deepLow = new Color(.012f, .075f, .12f), deepHigh = new Color(.026f, .16f, .19f);
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float u = x / (float)(size - 1), v = y / (float)(size - 1);
                float broad = Mathf.PerlinNoise(u * 4.7f + 12.4f, v * 4.7f + 31.6f);
                float detail = Mathf.PerlinNoise(u * 17.3f + 47.1f, v * 17.3f + 8.9f);
                float variation = Mathf.Clamp01(.32f + broad * .46f + detail * .22f);
                colors[y * size + x] = Color.Lerp(deepLow, deepHigh, variation);
            }
            texture.SetPixels(colors); texture.Apply(true, false); generatedTextures.Add(texture); return texture;
        }

        private void ApplyCoastlineDrivenShelf(IEnumerable<Vector3[]> shorelines, Vector3 first, Vector3 last)
        {
            if (!(waterMaterial.mainTexture is Texture2D texture)) return;
            int width = texture.width, height = texture.height;
            float worldWidth = last.x - first.x + 12f, worldDepth = last.z - first.z + 12f;
            Vector3 worldCenter = (first + last) * .5f;
            float pixelWidth = worldWidth / Mathf.Max(1, width - 1), pixelDepth = worldDepth / Mathf.Max(1, height - 1);
            float diagonal = Mathf.Sqrt(pixelWidth * pixelWidth + pixelDepth * pixelDepth);
            var distanceToCoast = Enumerable.Repeat(float.MaxValue, width * height).ToArray();

            foreach (Vector3[] shoreline in shorelines)
            {
                if (shoreline == null || shoreline.Length < 2) continue;
                for (int index = 0; index < shoreline.Length; index++)
                {
                    Vector3 start = shoreline[index];
                    Vector3 end = shoreline[(index + 1) % shoreline.Length];
                    float startX = (start.x - worldCenter.x) / worldWidth + .5f;
                    float startY = (start.z - worldCenter.z) / worldDepth + .5f;
                    float endX = (end.x - worldCenter.x) / worldWidth + .5f;
                    float endY = (end.z - worldCenter.z) / worldDepth + .5f;
                    int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(Mathf.Abs(endX - startX) * width, Mathf.Abs(endY - startY) * height)));
                    for (int step = 0; step <= steps; step++)
                    {
                        float amount = step / (float)steps;
                        int x = Mathf.RoundToInt(Mathf.Lerp(startX, endX, amount) * (width - 1));
                        int y = Mathf.RoundToInt(Mathf.Lerp(startY, endY, amount) * (height - 1));
                        if (x >= 0 && x < width && y >= 0 && y < height) distanceToCoast[y * width + x] = 0f;
                    }
                }
            }

            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                int pixel = y * width + x;
                float distance = distanceToCoast[pixel];
                if (x > 0) distance = Mathf.Min(distance, distanceToCoast[pixel - 1] + pixelWidth);
                if (y > 0) distance = Mathf.Min(distance, distanceToCoast[pixel - width] + pixelDepth);
                if (x > 0 && y > 0) distance = Mathf.Min(distance, distanceToCoast[pixel - width - 1] + diagonal);
                if (x + 1 < width && y > 0) distance = Mathf.Min(distance, distanceToCoast[pixel - width + 1] + diagonal);
                distanceToCoast[pixel] = distance;
            }
            for (int y = height - 1; y >= 0; y--) for (int x = width - 1; x >= 0; x--)
            {
                int pixel = y * width + x;
                float distance = distanceToCoast[pixel];
                if (x + 1 < width) distance = Mathf.Min(distance, distanceToCoast[pixel + 1] + pixelWidth);
                if (y + 1 < height) distance = Mathf.Min(distance, distanceToCoast[pixel + width] + pixelDepth);
                if (x + 1 < width && y + 1 < height) distance = Mathf.Min(distance, distanceToCoast[pixel + width + 1] + diagonal);
                if (x > 0 && y + 1 < height) distance = Mathf.Min(distance, distanceToCoast[pixel + width - 1] + diagonal);
                distanceToCoast[pixel] = distance;
            }

            Color[] colors = texture.GetPixels();
            Color shelfColor = new Color(.045f, .285f, .31f, 1f);
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                float u = x / (float)(width - 1), v = y / (float)(height - 1);
                float broad = Mathf.PerlinNoise(u * 4.1f + 19.7f, v * 4.1f + 7.3f);
                float detail = Mathf.PerlinNoise(u * 13.7f + 3.8f, v * 13.7f + 31.2f);
                float shelfWidth = Mathf.Lerp(.38f, 1.42f, broad * .76f + detail * .24f);
                float shelf = Mathf.Clamp01(1f - distanceToCoast[y * width + x] / shelfWidth);
                shelf = Mathf.SmoothStep(0f, 1f, shelf);
                float mottling = Mathf.Lerp(.76f, 1f, detail);
                colors[y * width + x] = Color.Lerp(colors[y * width + x], shelfColor, shelf * .43f * mottling);
            }
            texture.SetPixels(colors);
            texture.Apply(true, false);
            HasCoastlineDrivenShelf = true;
            HasShallowWaterDetail = true;
        }

        private Texture2D CreateNormalTexture(string name, int size, int seed, bool waves, float strength)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var heights = new float[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                heights[y * size + x] = waves
                    ? TileableNoise(u, v, 4f, 7f, seed * .13f, seed * .29f) * .52f
                        + TileableNoise(u, v, 11f, 17f, seed * .41f, seed * .07f) * .31f
                        + TileableNoise(u, v, 23f, 31f, seed * .19f, seed * .53f) * .17f
                    : Mathf.PerlinNoise(u * 6f + seed, v * 6f + seed * .31f) * .72f + Mathf.PerlinNoise(u * 24f + seed * .17f, v * 24f + seed * .53f) * .28f;
            }
            var colors = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float left = heights[y * size + (x - 1 + size) % size], right = heights[y * size + (x + 1) % size];
                float down = heights[((y - 1 + size) % size) * size + x], up = heights[((y + 1) % size) * size + x];
                Vector3 normal = new Vector3((left - right) * strength, 1f, (down - up) * strength).normalized;
                colors[y * size + x] = new Color(normal.x * .5f + .5f, normal.y * .5f + .5f, normal.z * .5f + .5f, 1f);
            }
            texture.SetPixels(colors); texture.Apply(true, false); generatedTextures.Add(texture); return texture;
        }

        private static float TileableNoise(float u, float v, float scaleX, float scaleY, float offsetX, float offsetY)
        {
            float a = Mathf.PerlinNoise(u * scaleX + offsetX, v * scaleY + offsetY);
            float b = Mathf.PerlinNoise((u - 1f) * scaleX + offsetX, v * scaleY + offsetY);
            float c = Mathf.PerlinNoise(u * scaleX + offsetX, (v - 1f) * scaleY + offsetY);
            float d = Mathf.PerlinNoise((u - 1f) * scaleX + offsetX, (v - 1f) * scaleY + offsetY);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        private Texture2D CreateCloudShadowCookie(string name, int size, float coverage, int seed)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGB24, true) { name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear };
            var colors = new Color[size * size];
            float cover = Mathf.Clamp01(coverage);
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float broad = Mathf.PerlinNoise(u * 3.1f + seed * .0017f, v * 3.1f + seed * .0023f);
                float softCloud = Mathf.SmoothStep(.48f, .76f, broad) * cover;
                float lightTransmission = Mathf.Lerp(1f, .76f, softCloud);
                colors[y * size + x] = new Color(lightTransmission, lightTransmission, lightTransmission, 1f);
            }
            texture.SetPixels(colors); texture.Apply(true, false); generatedTextures.Add(texture); return texture;
        }

        private static void ApplyNormalMap(Material material, Texture2D normal, float strength)
        {
            if (!material.HasProperty("_BumpMap")) return;
            material.SetTexture("_BumpMap", normal);
            if (material.HasProperty("_BumpScale")) material.SetFloat("_BumpScale", strength);
            material.EnableKeyword("_NORMALMAP");
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
                    float broad = Mathf.PerlinNoise(u * 4.3f + 5.7f, v * 3.8f + 17.2f) - .5f;
                    float detail = Mathf.PerlinNoise(u * 13.1f + 29.4f, v * 11.7f + 3.6f) - .5f;
                    float wave = broad * .006f + detail * .002f;
                    int vertex = row * (columns + 1) + column;
                    vertices[vertex] = new Vector3((u - .5f) * width, wave, (v - .5f) * depth);
                    uvs[vertex] = new Vector2(u, v);
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
            float labelScale = Mathf.Clamp(distance / Mathf.Max(12f, Mathf.Max(area.Width, area.Height)), .72f, 1.55f);
            foreach (Transform label in billboardLabels)
            {
                if (label == null) continue;
                label.rotation = camera.transform.rotation;
                label.localScale = Vector3.one * labelScale;
            }
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

        private static Material TransparentMaterialFor(string name, Color color, float metallic, float smoothness)
        {
            Material material = MaterialFor(name, "Standard", color, metallic, smoothness);
            material.name = name;
            if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 3f);
            if (material.HasProperty("_SrcBlend")) material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON"); material.EnableKeyword("_ALPHABLEND_ON"); material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;
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
            DestroyObject(foamMaterial); DestroyObject(shallowWaterMaterial); DestroyObject(atmosphericMaterial); DestroyObject(cloudShadowMaterial);
            DestroyObject(contactMaterial); DestroyObject(warningMaterial);
        }

        private static void DestroyObject(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value);
        }
    }
}
