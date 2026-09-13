using System;
using System.Collections.Generic;
using System.Linq;
using SeaOfUncertainty.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeaOfUncertainty.Prototype
{
    public enum ToolkitActionMode { None, Move, Search, Strike, Replenish }

    public sealed class TacticalMapElement : VisualElement
    {
        private readonly List<Label> markers = new List<Label>();
        private readonly List<MarkerLayout> markerLayouts = new List<MarkerLayout>();
        private readonly MarkerLeaderOverlay markerLeaders;
        private readonly HashSet<KeyCode> cameraKeys = new HashSet<KeyCode>();
        private PrototypeGame game;
        private ToolkitActionMode actionMode;
        private MoveMode moveMode;
        private SearchMode searchMode;
        private Salvo salvo;
        private HexCoord keyboardHex;
        private OperationalMap3D presentation;
        private int dragButton = -1;
        private Vector2 lastPointer;
        private Vector2 dragTravel;
        private bool activeFormationContextCandidate;
        private readonly Label hexReadout;
        private readonly Label headingReadout;
        private bool reducedMotion;
        private Vector2 edgePanInput;

        private sealed class MarkerLayout
        {
            public Label Label;
            public Vector2 Anchor;
            public Vector2 Size;
            public int Priority;
        }

        private sealed class MarkerLeaderOverlay : VisualElement
        {
            private readonly List<Vector4> lines = new List<Vector4>();

            public MarkerLeaderOverlay()
            {
                name = "map-marker-leaders";
                pickingMode = PickingMode.Ignore;
                style.position = Position.Absolute;
                style.left = 0f;
                style.right = 0f;
                style.top = 0f;
                style.bottom = 0f;
                generateVisualContent += DrawLines;
            }

            public void SetLines(IEnumerable<Vector4> values)
            {
                lines.Clear();
                lines.AddRange(values);
                MarkDirtyRepaint();
            }

            private void DrawLines(MeshGenerationContext context)
            {
                Painter2D painter = context.painter2D;
                painter.lineWidth = 1.15f;
                painter.strokeColor = new Color(.38f, .78f, .84f, .38f);
                foreach (Vector4 line in lines)
                {
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(line.x, line.y));
                    painter.LineTo(new Vector2(line.z, line.w));
                    painter.Stroke();
                }
            }
        }

        public Action<HexCoord> HexChosen;
        public Action<ContactState> ContactChosen;
        public Action<ContactState> ContactHovered;
        public Action<Vector2> ActiveFormationContextRequested;
        public bool CameraInputActive => cameraKeys.Count > 0;
        public bool EdgeScrollEnabled { get; private set; }
        public int DeclutteredMarkerCount { get; private set; }
        public int MarkerLeaderLineCount { get; private set; }
        public bool LabelsUseDistanceFade { get; private set; }

        public TacticalMapElement()
        {
            AddToClassList("map");
            focusable = true;
            tabIndex = 0;
            VisualElement commandHud = new VisualElement { pickingMode = PickingMode.Ignore };
            commandHud.AddToClassList("map-command-hud");
            hexReadout = new Label("HOVER MAP FOR HEX RANGE") { pickingMode = PickingMode.Ignore };
            hexReadout.AddToClassList("map-readout");
            headingReadout = new Label("NORTH 000°") { pickingMode = PickingMode.Ignore };
            headingReadout.AddToClassList("map-heading");
            commandHud.Add(hexReadout);
            commandHud.Add(headingReadout);
            Add(commandHud);
            markerLeaders = new MarkerLeaderOverlay();
            Add(markerLeaders);
            RegisterCallback<AttachToPanelEvent>(_ => EnsurePresentation());
            RegisterCallback<DetachFromPanelEvent>(_ => DisposePresentation());
            RegisterCallback<GeometryChangedEvent>(evt =>
            {
                presentation?.Resize(Mathf.RoundToInt(evt.newRect.width), Mathf.RoundToInt(evt.newRect.height));
                UpdateMarkers();
            });
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerLeaveEvent>(_ => { edgePanInput = Vector2.zero; ContactHovered?.Invoke(null); presentation?.SetHoverHex(null); UpdateReadout(null); });
            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<KeyDownEvent>(OnKeyDown);
            RegisterCallback<KeyUpEvent>(OnKeyUp);
            RegisterCallback<FocusOutEvent>(_ => cameraKeys.Clear());
            RegisterCallback<NavigationSubmitEvent>(_ => SubmitCursor());
            schedule.Execute(TickPresentation).Every(16);
        }

        public void SetState(PrototypeGame currentGame, ToolkitActionMode mode, MoveMode currentMoveMode, SearchMode currentSearchMode, Salvo currentSalvo, bool useReducedMotion = false, bool showHexGrid = false)
        {
            game = currentGame;
            actionMode = mode;
            moveMode = currentMoveMode;
            searchMode = currentSearchMode;
            salvo = currentSalvo;
            reducedMotion = useReducedMotion;
            if (game?.Active != null) keyboardHex = game.Active.Position;
            EnsurePresentation();
            presentation?.SetState(game, actionMode, moveMode, searchMode, salvo, reducedMotion, showHexGrid);
            UpdateMarkers();
        }

        public void Refresh()
        {
            UpdateMarkers();
            MarkDirtyRepaint();
        }

        private void EnsurePresentation()
        {
            if (presentation != null || game?.Area == null) return;
            presentation = new OperationalMap3D(game.Area);
            style.backgroundImage = new StyleBackground(Background.FromRenderTexture(presentation.Texture));
        }

        private void DisposePresentation()
        {
            presentation?.Dispose();
            presentation = null;
        }

        private void UpdateMarkers()
        {
            foreach (Label marker in markers) marker.RemoveFromHierarchy();
            markers.Clear();
            markerLayouts.Clear();
            markerLeaders.SetLines(Array.Empty<Vector4>());
            DeclutteredMarkerCount = 0;
            MarkerLeaderLineCount = 0;
            LabelsUseDistanceFade = false;
            if (game?.Active == null || presentation == null || contentRect.width < 10f) return;
            Side viewer = game.Active.Side;
            foreach (FormationState formation in game.Formations.Where(f => !f.IsDestroyed && f.Side == viewer))
            {
                bool active = formation == game.Active;
                Label marker = Marker(formation.Position, active ? "ACTIVE\n" + Code(formation.Kind) : Code(formation.Kind), active ? 100 : 50, active ? new Vector2(72f, 58f) : new Vector2(54f, 42f), "map-marker", formation.Side == Side.Blue ? "blue" : "red");
                if (active) marker.AddToClassList("active-formation");
                marker.tooltip = $"{formation.Name}\n{formation.Kind} • Ready T{formation.ReadyTime:00}\n{formation.Cohesion}{(active ? "\nACTIVE — right-click for orders" : string.Empty)}";
            }
            foreach (ContactState contact in game.Contacts.Where(c => c.Owner == viewer && !c.IsLost))
            {
                bool eligible = actionMode == ToolkitActionMode.Search ? SearchEligible(contact) : actionMode == ToolkitActionMode.Strike && StrikeEligible(contact);
                FormationState target = game.Find(contact.TargetId);
                string contactCode = contact.Identity == IdentityQuality.Identified && target != null ? Code(target.Kind) : "?";
                Label marker = Marker(contact.LastKnownPosition, $"◇ {contactCode}\n{contact.Location.ToString()[0]} A{contact.Age}", eligible ? 88 : 76, new Vector2(64f, 52f), "map-marker", "contact");
                if (eligible) marker.AddToClassList("eligible");
                int radius = Rules.ContactUncertaintyRadius(contact);
                int possibleHexes = game.ContactPossibleHexes(contact).Count;
                marker.tooltip = $"{contact.Summary}\nLast known {contact.LastKnownPosition}\nPossible area: {possibleHexes} hex{(possibleHexes == 1 ? string.Empty : "es")} within radius {radius} • Age or observed movement expands the estimate\n{(eligible ? "Eligible — click to commit" : "Not eligible for selected action")}";
                if (contact.HasContradictoryPosition)
                {
                    Label contradictory = Marker(contact.ContradictoryPosition, "?", eligible ? 84 : 72, new Vector2(64f, 52f), "map-marker", "contact");
                    if (eligible) contradictory.AddToClassList("eligible");
                    contradictory.tooltip = $"CONTRADICTORY REPORT\nSecond possible fix {contact.ContradictoryPosition}\nSelect either marker to inspect the shared possible area.";
                }
            }
            LayoutMarkers();
        }

        private Label Marker(HexCoord hex, string text, int priority, Vector2 size, params string[] classes)
        {
            Vector2 center = presentation.Project(hex, contentRect);
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            foreach (string className in classes) label.AddToClassList(className);
            Add(label);
            markers.Add(label);
            markerLayouts.Add(new MarkerLayout { Label = label, Anchor = center, Size = size, Priority = priority });
            return label;
        }

        private void LayoutMarkers()
        {
            var occupied = new List<Rect>();
            var leaders = new List<Vector4>();
            Vector2[] offsets =
            {
                Vector2.zero, new Vector2(0f, -58f), new Vector2(0f, 58f), new Vector2(66f, 0f), new Vector2(-66f, 0f),
                new Vector2(62f, -48f), new Vector2(-62f, -48f), new Vector2(62f, 48f), new Vector2(-62f, 48f)
            };
            float distanceVisibility = presentation == null ? 1f : presentation.MapLabelVisibility;
            foreach (MarkerLayout marker in markerLayouts.OrderByDescending(item => item.Priority))
            {
                Rect baseRect = new Rect(marker.Anchor.x - marker.Size.x * .5f, marker.Anchor.y - marker.Size.y - 11f, marker.Size.x, marker.Size.y);
                Rect chosen = baseRect;
                float bestPenalty = float.MaxValue;
                foreach (Vector2 offset in offsets)
                {
                    Rect candidate = new Rect(marker.Anchor.x - marker.Size.x * .5f + offset.x, marker.Anchor.y - marker.Size.y - 11f + offset.y, marker.Size.x, marker.Size.y);
                    candidate.x = Mathf.Clamp(candidate.x, 8f, Mathf.Max(8f, contentRect.width - candidate.width - 8f));
                    candidate.y = Mathf.Clamp(candidate.y, 52f, Mathf.Max(52f, contentRect.height - candidate.height - 8f));
                    float overlap = occupied.Sum(existing => IntersectionArea(candidate, existing));
                    float penalty = overlap + offset.sqrMagnitude * .0007f;
                    if (penalty >= bestPenalty) continue;
                    bestPenalty = penalty;
                    chosen = candidate;
                    if (penalty < .01f) break;
                }
                occupied.Add(chosen);
                marker.Label.style.left = chosen.x;
                marker.Label.style.top = chosen.y;
                float priorityFloor = marker.Priority >= 100 ? .9f : marker.Priority >= 70 ? .68f : .42f;
                float opacity = Mathf.Lerp(priorityFloor, 1f, distanceVisibility);
                marker.Label.style.opacity = opacity;
                LabelsUseDistanceFade |= opacity < .99f;
                Vector2 labelCenter = chosen.center;
                if (Vector2.Distance(chosen.position, baseRect.position) > 8f)
                {
                    leaders.Add(new Vector4(marker.Anchor.x, marker.Anchor.y, labelCenter.x, labelCenter.y));
                    DeclutteredMarkerCount++;
                }
            }
            MarkerLeaderLineCount = leaders.Count;
            markerLeaders.SetLines(leaders);
        }

        private static float IntersectionArea(Rect first, Rect second)
        {
            float x = Mathf.Max(0f, Mathf.Min(first.xMax, second.xMax) - Mathf.Max(first.xMin, second.xMin));
            float y = Mathf.Max(0f, Mathf.Min(first.yMax, second.yMax) - Mathf.Max(first.yMin, second.yMin));
            return x * y;
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            Focus();
            if (evt.button == 1 || evt.button == 2)
            {
                dragButton = evt.button;
                activeFormationContextCandidate = evt.button == 1 && IsOverActiveFormation(evt.localPosition);
                lastPointer = evt.localPosition;
                dragTravel = Vector2.zero;
                this.CapturePointer(evt.pointerId);
                evt.StopPropagation();
                return;
            }
            if (evt.button != 0) return;
            if ((actionMode == ToolkitActionMode.Search || actionMode == ToolkitActionMode.Strike) && NearestContact(evt.localPosition, 58f, out ContactState contact))
            {
                ContactChosen?.Invoke(contact);
                evt.StopPropagation();
                return;
            }
            if (presentation != null && presentation.TryPickHex(evt.localPosition, contentRect, out HexCoord hex))
            {
                keyboardHex = hex;
                presentation.SetHoverHex(hex);
                UpdateReadout(hex);
                HexChosen?.Invoke(hex);
            }
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (dragButton >= 0)
            {
                Vector2 delta = (Vector2)evt.localPosition - lastPointer;
                lastPointer = evt.localPosition;
                dragTravel += delta;
                if (dragButton == 2) presentation?.Pan(delta);
                else if (dragButton == 1)
                {
                    if (dragTravel.magnitude > 7f) activeFormationContextCandidate = false;
                    if (!activeFormationContextCandidate) presentation?.Orbit(delta);
                }
                UpdateMarkers();
                UpdateReadout(null);
                evt.StopPropagation();
                return;
            }
            UpdateEdgePan(evt.localPosition);
            if (presentation != null && presentation.TryPickHex(evt.localPosition, contentRect, out HexCoord hoverHex))
            {
                presentation.SetHoverHex(hoverHex);
                UpdateReadout(hoverHex);
            }
            else
            {
                presentation?.SetHoverHex(null);
                UpdateReadout(null);
            }
            ContactHovered?.Invoke(NearestContact(evt.localPosition, 58f, out ContactState contact) ? contact : null);
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (dragButton < 0 || evt.button != dragButton) return;
            bool openOrders = dragButton == 1 && activeFormationContextCandidate && dragTravel.magnitude <= 7f;
            dragButton = -1;
            activeFormationContextCandidate = false;
            if (this.HasPointerCapture(evt.pointerId)) this.ReleasePointer(evt.pointerId);
            UpdateMarkers();
            UpdateReadout(null);
            if (openOrders) ActiveFormationContextRequested?.Invoke(evt.localPosition);
            evt.StopPropagation();
        }

        private bool IsOverActiveFormation(Vector2 point)
            => game?.Active != null && presentation != null && Vector2.Distance(presentation.Project(game.Active.Position, contentRect), point) <= 62f;

        private void OnWheel(WheelEvent evt)
        {
            presentation?.Zoom(evt.delta.y);
            UpdateMarkers();
            UpdateReadout(null);
            evt.StopPropagation();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (game?.Area == null) return;
            if (SetCameraKey(evt.keyCode, true))
            {
                evt.StopPropagation();
                return;
            }
            int q = keyboardHex.Q;
            int r = keyboardHex.R;
            if (evt.keyCode == KeyCode.LeftArrow) q--;
            else if (evt.keyCode == KeyCode.RightArrow) q++;
            else if (evt.keyCode == KeyCode.UpArrow) r--;
            else if (evt.keyCode == KeyCode.DownArrow) r++;
            else if (evt.keyCode == KeyCode.Home) { presentation?.ResetCamera(); UpdateMarkers(); UpdateReadout(null); evt.StopPropagation(); return; }
            else if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.Space) { SubmitCursor(); evt.StopPropagation(); return; }
            else return;
            keyboardHex = new HexCoord(Mathf.Clamp(q, 0, game.Area.Width - 1), Mathf.Clamp(r, 0, game.Area.Height - 1));
            presentation?.SetHoverHex(keyboardHex);
            UpdateReadout(keyboardHex);
            evt.StopPropagation();
        }

        private void OnKeyUp(KeyUpEvent evt)
        {
            if (!SetCameraKey(evt.keyCode, false)) return;
            evt.StopPropagation();
        }

        public bool SetCameraKey(KeyCode key, bool pressed)
        {
            if (!IsCameraKey(key)) return false;
            if (pressed) cameraKeys.Add(key); else cameraKeys.Remove(key);
            return true;
        }

        public void ClearCameraKeys() => cameraKeys.Clear();

        public void SetEdgeScroll(bool enabled)
        {
            EdgeScrollEnabled = enabled;
            if (!enabled) edgePanInput = Vector2.zero;
            UpdateReadout(null);
        }

        public void FocusActiveFormation()
        {
            if (game?.Active == null) return;
            presentation?.FocusHex(game.Active.Position);
            UpdateMarkers(); UpdateReadout(game.Active.Position);
        }

        public void FocusObjective()
        {
            if (game?.Area == null) return;
            presentation?.FocusHex(game.Area.Objective);
            UpdateMarkers(); UpdateReadout(game.Area.Objective);
        }

        public void SaveCameraView() => presentation?.SaveCameraView();
        public bool RecallCameraView()
        {
            bool recalled = presentation != null && presentation.RecallCameraView();
            if (recalled) { UpdateMarkers(); UpdateReadout(null); }
            return recalled;
        }

        public void ResetCameraView() { presentation?.ResetCamera(); UpdateMarkers(); UpdateReadout(null); }

        private void UpdateEdgePan(Vector2 pointer)
        {
            edgePanInput = Vector2.zero;
            if (!EdgeScrollEnabled || contentRect.width < 10f || contentRect.height < 10f) return;
            const float edge = 18f;
            if (pointer.x <= edge) edgePanInput.x = -Mathf.Clamp01((edge - pointer.x) / edge);
            else if (pointer.x >= contentRect.width - edge) edgePanInput.x = Mathf.Clamp01((pointer.x - (contentRect.width - edge)) / edge);
            if (pointer.y <= edge) edgePanInput.y = Mathf.Clamp01((edge - pointer.y) / edge);
            else if (pointer.y >= contentRect.height - edge) edgePanInput.y = -Mathf.Clamp01((pointer.y - (contentRect.height - edge)) / edge);
        }

        private void TickPresentation()
        {
            presentation?.Tick(Time.deltaTime);
            if (presentation == null) return;
            float right = Axis(KeyCode.D, KeyCode.A) + edgePanInput.x;
            float forward = Axis(KeyCode.W, KeyCode.S) + edgePanInput.y;
            float yaw = Axis(KeyCode.E, KeyCode.Q);
            float pitch = Axis(KeyCode.R, KeyCode.F);
            float zoom = Axis(KeyCode.X, KeyCode.Z) + Axis(KeyCode.KeypadMinus, KeyCode.KeypadPlus);
            bool fast = cameraKeys.Contains(KeyCode.LeftShift) || cameraKeys.Contains(KeyCode.RightShift);
            if (Mathf.Approximately(right, 0f) && Mathf.Approximately(forward, 0f) && Mathf.Approximately(yaw, 0f) && Mathf.Approximately(pitch, 0f) && Mathf.Approximately(zoom, 0f) && !presentation.CameraIsSettling) return;
            presentation.FlyCamera(right, forward, yaw, pitch, zoom, Time.deltaTime, fast);
            UpdateMarkers();
            UpdateReadout(null);
        }

        private float Axis(KeyCode positive, KeyCode negative) => (cameraKeys.Contains(positive) ? 1f : 0f) - (cameraKeys.Contains(negative) ? 1f : 0f);

        private static bool IsCameraKey(KeyCode key)
        {
            return key == KeyCode.W || key == KeyCode.A || key == KeyCode.S || key == KeyCode.D ||
                   key == KeyCode.Q || key == KeyCode.E || key == KeyCode.R || key == KeyCode.F ||
                   key == KeyCode.Z || key == KeyCode.X || key == KeyCode.KeypadPlus || key == KeyCode.KeypadMinus ||
                   key == KeyCode.LeftShift || key == KeyCode.RightShift;
        }

        private void UpdateReadout(HexCoord? hex)
        {
            string overlay = actionMode == ToolkitActionMode.Move ? "MOVE AREA" : actionMode == ToolkitActionMode.Search ? "SEARCH COVERAGE" : actionMode == ToolkitActionMode.Strike ? "STRIKE RANGE" : actionMode == ToolkitActionMode.Replenish ? "LOGISTICS ACCESS" : "COMMAND VIEW";
            if (headingReadout != null) headingReadout.text = presentation == null ? "NORTH 000°" : $"HDG {presentation.Heading:000}°  •  PITCH {presentation.CameraPitch:00}°  •  EDGE {(EdgeScrollEnabled ? "ON" : "OFF")}  •  {overlay}";
            if (hexReadout == null) return;
            if (!hex.HasValue || game?.Active == null) { hexReadout.text = $"{game?.Area?.NauticalMilesPerHex ?? 20} NM HEX SCALE"; return; }
            int range = game.Area.NauticalMiles(game.Active.Position, hex.Value);
            TerrainHexDefinition terrain = game.Area.Terrain.FirstOrDefault(item => item.Hex.Equals(hex.Value));
            string terrainName = terrain == null ? "DEEP WATER" : terrain.Terrain.ToString().ToUpperInvariant();
            string objective = hex.Value.Equals(game.Area.Objective) ? "  •  OBJECTIVE" : string.Empty;
            string searchFootprint = actionMode == ToolkitActionMode.Search ? $"  •  SEARCH AREA R{Rules.SearchAreaRadius}" : string.Empty;
            hexReadout.text = $"HEX {hex.Value}  •  {terrainName}  •  RANGE {range} NM{searchFootprint}{objective}";
        }

        public void MoveCursor(int qDelta, int rDelta)
        {
            if (game?.Area == null) return;
            keyboardHex = new HexCoord(Mathf.Clamp(keyboardHex.Q + qDelta, 0, game.Area.Width - 1), Mathf.Clamp(keyboardHex.R + rDelta, 0, game.Area.Height - 1));
        }

        public void SubmitCursor()
        {
            if ((actionMode == ToolkitActionMode.Search || actionMode == ToolkitActionMode.Strike) && game != null)
            {
                ContactState contact = game.Contacts.FirstOrDefault(c => c.Owner == game.Active.Side && !c.IsLost && c.LastKnownPosition.Equals(keyboardHex));
                if (contact != null) ContactChosen?.Invoke(contact); else HexChosen?.Invoke(keyboardHex);
            }
            else HexChosen?.Invoke(keyboardHex);
        }

        private bool NearestContact(Vector2 point, float range, out ContactState contact)
        {
            contact = null;
            if (game?.Active == null || presentation == null) return false;
            contact = game.Contacts.Where(c => c.Owner == game.Active.Side && !c.IsLost)
                .OrderBy(c => Math.Min(Vector2.Distance(presentation.Project(c.LastKnownPosition, contentRect), point), c.HasContradictoryPosition ? Vector2.Distance(presentation.Project(c.ContradictoryPosition, contentRect), point) : float.MaxValue)).FirstOrDefault();
            float contactDistance = contact == null ? float.MaxValue : Math.Min(Vector2.Distance(presentation.Project(contact.LastKnownPosition, contentRect), point), contact.HasContradictoryPosition ? Vector2.Distance(presentation.Project(contact.ContradictoryPosition, contentRect), point) : float.MaxValue);
            if (contact == null || contactDistance > range) { contact = null; return false; }
            return true;
        }

        private bool StrikeEligible(ContactState contact)
        {
            if (game?.Active == null || contact == null) return false;
            if (!game.ContactPossibleHexes(contact).Any(hex => HexCoord.Distance(game.Active.Position, hex) <= Rules.StrikeRange(game.Active.Kind, salvo))) return false;
            return salvo != Salvo.Heavy || game.Active.CanHeavySalvo;
        }

        private bool SearchEligible(ContactState contact) => game?.Active != null && contact != null && HexCoord.Distance(game.Active.Position, contact.LastKnownPosition) <= game.SearchRangeFor(game.Active, searchMode);

        private static string Code(FormationKind kind) => kind == FormationKind.CarrierGroup ? "CV" : kind == FormationKind.SurfaceGroup ? "SG" : kind == FormationKind.Submarine ? "SS" : kind == FormationKind.AirGroup ? "AG" : "LG";
    }
}
