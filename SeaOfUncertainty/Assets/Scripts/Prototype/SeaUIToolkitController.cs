using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SeaOfUncertainty.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeaOfUncertainty.Prototype
{
    public sealed class SeaUIToolkitController : MonoBehaviour
    {
        private enum View { Main, Mode, Online, Scenario, Briefing, Setup, Handoff, AiPlanning, Game, Results }

        private SeaPrototypeController backend;
        private OnlineMultiplayerCoordinator online;
        private UIDocument document;
        private VisualElement root;
        private VisualElement app;
        private VisualElement dossier;
        private TacticalMapElement map;
        private View view;
        private ToolkitActionMode actionMode;
        private MoveMode moveMode = MoveMode.Normal;
        private SearchMode searchMode = SearchMode.Passive;
        private SearchPriority searchPriority = SearchPriority.Location;
        private Salvo salvo = Salvo.Standard;
        private Side previousSide;
        private bool aiRunning;
        private bool edgeScrollEnabled;
        private bool edgeHudVisible;
        private bool entropyRevealBlocking;
        private FormationState pendingReactionTarget;
        private Salvo pendingReactionSalvo;
        private AiDecision pendingAiStrike;
        private ContactState pendingStrikeContact;
        private HexCoord pendingStrikeAim;
        private SynchronizedStrikeState pendingSynchronizedStrike;
        private bool pendingSynchronizedBlind;
        private bool controllerAxisReleased = true;
        private VisualElement focusBeforeOverlay;
        private Button edgeHudToggle;
        private bool onlineKillSwitchRefreshed;

#if UNITY_EDITOR
        public VisualElement EditorRoot => root;
        public SeaPrototypeController EditorBackend => backend;
        public string EditorView => view.ToString();
        public bool EditorOverlayOpen => root?.Q<VisualElement>("overlay") != null;

        public void EditorEnsureInitializedForTests()
        {
            backend = GetComponent<SeaPrototypeController>();
            backend?.EditorEnsureInitializedForTests();
            if (root == null) Awake();
        }

        public void EditorShowGame() { backend.ToolkitBeginDecision(); ShowGame(); }
        public void EditorShowHandoff() => ShowHandoff();
        public void EditorShowSettings() => ShowSettings();
        public void EditorShowRules() => ShowRules();
        public void EditorShowResponseHand() => ShowResponseHand();
        public void EditorShowStrikeAim(ContactState contact) => ShowStrikeAim(contact);
        public bool EditorPersistentCardHandVisible => root?.Q<VisualElement>(className: "card-hand") != null;
        public bool EditorEdgeHudVisible => edgeHudVisible;
        public void EditorToggleEdgeHud() => ToggleEdgeHud();
        public void EditorShowFormationOrders() => ShowFormationOrders(new Vector2(640f, 320f));
        public string EditorActionMode => actionMode.ToString();

        public bool EditorInvokeButton(string text)
        {
            Button button = root?.Query<Button>().ToList().FirstOrDefault(item => string.Equals(item.text, text, StringComparison.Ordinal));
            if (button?.userData is Action action && button.enabledInHierarchy) { action(); return true; }
            return false;
        }

        public Toggle EditorToggle(string label)
            => root?.Query<Toggle>().ToList().FirstOrDefault(item => string.Equals(item.label, label, StringComparison.Ordinal));

        public Slider EditorSlider(string label)
            => root?.Query<Slider>().ToList().FirstOrDefault(item => string.Equals(item.label, label, StringComparison.Ordinal));

        public DropdownField EditorDropdown(string label)
            => root?.Query<DropdownField>().ToList().FirstOrDefault(item => string.Equals(item.label, label, StringComparison.Ordinal));

        public static FullScreenMode EditorRequestedFullscreenMode(bool currentlyFullscreen)
            => RequestedFullscreenMode(currentlyFullscreen);
#endif

        private static FullScreenMode RequestedFullscreenMode(bool currentlyFullscreen)
            => currentlyFullscreen ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<SeaUIToolkitController>() != null) return;
            var host = new GameObject("Sea of Uncertainty — UI Toolkit");
            DontDestroyOnLoad(host);
            SeaPrototypeController session = host.AddComponent<SeaPrototypeController>();
            session.LegacyUiEnabled = false;
            host.AddComponent<SeaUIToolkitController>();
        }

        private void Awake()
        {
            backend = GetComponent<SeaPrototypeController>();
            online = GetComponent<OnlineMultiplayerCoordinator>() ?? gameObject.AddComponent<OnlineMultiplayerCoordinator>();
            online.Changed -= OnOnlineChanged;
            online.Changed += OnOnlineChanged;
            PanelSettings panelSettings = Resources.Load<PanelSettings>("UI/SeaRuntimePanelSettings");
            if (panelSettings == null || panelSettings.themeStyleSheet == null)
            {
                Debug.LogError("Sea of Uncertainty could not load its configured UI Toolkit panel. Interface text may not render.");
                return;
            }
            document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            root = document.rootVisualElement;
            StyleSheet theme = Resources.Load<StyleSheet>("UI/SeaTheme");
            if (theme != null) root.styleSheets.Add(theme);
            root.AddToClassList("app");
            root.focusable = true;
            root.RegisterCallback<KeyDownEvent>(OnGlobalKeyDown);
            root.RegisterCallback<KeyUpEvent>(OnGlobalKeyUp);
            root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            EnsureNativeFullscreenResolution();
            if (Environment.GetCommandLineArgs().Contains("-captureReactionPrototype"))
            {
                backend.ToolkitNewScenario("meridian-veil");
                backend.ToolkitSetOperationMode(OperationMode.LocalHotseat);
                FormationState attacker = backend.Game.Active;
                FormationState defender = backend.Game.Formations.First(formation => formation.Side != attacker.Side && formation.Kind == FormationKind.Submarine);
                defender.Position = new HexCoord(attacker.Position.Q + 1, attacker.Position.R);
                ContactState defenderContact = backend.Game.ContactFor(defender.Side, attacker.Id);
                if (defenderContact == null)
                {
                    defenderContact = new ContactState { Owner = defender.Side, TargetId = attacker.Id };
                    backend.Game.Contacts.Add(defenderContact);
                }
                defenderContact.LastKnownPosition = attacker.Position;
                defenderContact.Location = LocationQuality.High;
                defenderContact.Identity = IdentityQuality.Identified;
                pendingReactionTarget = defender;
                pendingReactionSalvo = Salvo.Standard;
                ShowReactionChoice();
            }
            else if (Environment.GetCommandLineArgs().Contains("-capture3DPrototype"))
            {
                string scenarioArgument = Environment.GetCommandLineArgs().FirstOrDefault(argument => argument.StartsWith("-scenario=", StringComparison.OrdinalIgnoreCase));
                backend.ToolkitNewScenario(scenarioArgument == null ? "meridian-veil" : scenarioArgument.Substring("-scenario=".Length));
                backend.ToolkitBeginDecision();
                ShowGame();
            }
            else if (Environment.GetCommandLineArgs().Contains("-captureModeSelect")) ShowMode();
            else ShowMain();
        }

        private void Update()
        {
            if (root == null || !backend.ControllerNavigation || !Input.GetJoystickNames().Any(name => !string.IsNullOrEmpty(name))) return;
            float horizontal = Input.GetAxisRaw("ControllerHorizontal");
            float vertical = Input.GetAxisRaw("ControllerVertical");
            float magnitude = Mathf.Max(Mathf.Abs(horizontal), Mathf.Abs(vertical));
            if (magnitude < backend.ControllerDeadZone * .65f) controllerAxisReleased = true;
            else if (controllerAxisReleased && magnitude >= backend.ControllerDeadZone)
            {
                controllerAxisReleased = false;
                VisualElement focused = root.panel?.focusController?.focusedElement as VisualElement;
                if (focused is Slider focusedSlider && Mathf.Abs(horizontal) >= Mathf.Abs(vertical))
                    focusedSlider.value = Mathf.Clamp(focusedSlider.value + (horizontal > 0f ? 5f : -5f), focusedSlider.lowValue, focusedSlider.highValue);
                else if (focused == map)
                {
                    if (Mathf.Abs(horizontal) >= Mathf.Abs(vertical)) map.MoveCursor(horizontal > 0f ? 1 : -1, 0);
                    else map.MoveCursor(0, vertical > 0f ? -1 : 1);
                }
                else MoveFocus(horizontal < 0f || vertical > 0f ? -1 : 1);
            }
            if (Input.GetKeyDown(KeyCode.JoystickButton0)) SubmitFocused();
            if (Input.GetKeyDown(KeyCode.JoystickButton1)) CancelNavigation();
            if (view == View.Game && Input.GetKeyDown(KeyCode.JoystickButton3)) ToggleEdgeHud();
        }

        private void ShowMain()
        {
            view = View.Main;
            BeginScreen("OPERATIONAL NAVAL WARGAME  •  P0 INTERFACE FOUNDATION");
            VisualElement page = El("front-page");
            VisualElement card = Panel("hero-card");
            card.Add(Text("COMMAND • INFORMATION • TEMPO", "eyebrow"));
            card.Add(Text("OPERATE INSIDE\nUNCERTAINTY", "front-title"));
            card.Add(Text("You are not trying to eliminate uncertainty. You are trying to operate better than your opponent inside it.", "body-copy"));
            card.Add(ActionButton("BEGIN OPERATION", ShowMode, "primary"));
            if (backend.HasSave) card.Add(ActionButton("CONTINUE SAVED OPERATION", LoadOperationAndContinue));
            VisualElement row = El("row");
            row.Add(ActionButton("SETTINGS", ShowSettings));
            row.Add(ActionButton("FIELD MANUAL", ShowRules));
            row.Add(ActionButton("EXIT GAME", ShowExitConfirmation, "danger-button"));
            card.Add(row);
            card.Add(Text(OnlineMultiplayerCoordinator.IsDeveloperPreviewEnabled()
                ? "Solo vs AI • Local pass-and-play • Developer-gated online validation"
                : "Solo vs AI • Local pass-and-play • Online multiplayer remains gated for validation", "muted"));
            page.Add(card);
            app.Add(page);
        }

        private void ShowMode()
        {
            view = View.Mode;
            onlineKillSwitchRefreshed = false;
            BeginScreen("SELECT COMMAND MODE");
            VisualElement page = El("front-page");
            VisualElement card = Panel("hero-card");
            card.Add(Text("WHO HOLDS THE OPPOSING COMMAND?", "heading"));
            card.Add(Text("Both modes use the same continuous Ready-Time sequence, hidden-information rules, combat tables, and scenarios.", "body-copy"));
            card.Add(ActionButton("SOLO VS AI  •  COMMAND BLUE", () => { backend.ToolkitSetOperationMode(OperationMode.SoloVsAi, Side.Blue); ShowScenario(); }, "primary"));
            card.Add(Text("The deterministic OPFOR commander controls Red using only its own Contacts, formations, public terrain, and objective information.", "muted"));
            card.Add(ActionButton("LOCAL HOTSEAT  •  BLUE VS RED", () => { backend.ToolkitSetOperationMode(OperationMode.LocalHotseat); ShowScenario(); }));
            card.Add(Text("Two players share this device with secure information-handoff screens whenever command changes sides.", "muted"));
            if (OnlineMultiplayerCoordinator.IsDeveloperPreviewEnabled())
            {
                card.Add(ActionButton("ONLINE PREVIEW  •  DIRECT / PRIVATE RELAY", ShowOnline));
                card.Add(Text("Validation harness for two authenticated commanders. Playable command routing and public activation remain gated on privacy and two-build QA.", "muted"));
            }
            card.Add(ActionButton("BACK", ShowMain));
            page.Add(card);
            app.Add(page);
        }

        private void ShowOnline()
        {
            view = View.Online;
            if (!onlineKillSwitchRefreshed)
            {
                onlineKillSwitchRefreshed = true;
                RefreshKillSwitchThenRedraw();
            }
            BeginScreen("ONLINE MULTIPLAYER  •  VALIDATION PREVIEW");
            VisualElement page = El("front-page");
            VisualElement card = Panel("hero-card");
            card.Add(Text("PRIVATE TWO-COMMANDER SESSION", "heading"));
            card.Add(Text("Direct IP works without Unity Cloud. Relay optionally provides managed membership, NAT traversal, DTLS encryption, IP concealment, and migration coordination.", "body-copy"));

            if (online.Session == null && !online.IsDirect)
            {
                TextField name = new TextField("COMMANDER NAME") { value = PlayerPrefs.GetString("OnlineCommanderName", "Commander") };
                name.name = "online-commander-name";
                TextField code = new TextField("JOIN CODE") { value = string.Empty };
                code.name = "online-join-code";
                code.maxLength = 12;
                TextField address = new TextField("HOST IP") { value = PlayerPrefs.GetString("DirectHostAddress", "127.0.0.1") };
                address.name = "direct-host-address";
                IntegerField port = new IntegerField("UDP PORT") { value = PlayerPrefs.GetInt("DirectHostPort", 7978) };
                port.name = "direct-host-port";
                card.Add(name);
                card.Add(Text("DIRECT IP  •  NO CLOUD REQUIRED", "eyebrow"));
                card.Add(address);
                card.Add(port);
                card.Add(code);
                VisualElement directRow = El("row");
                directRow.Add(ActionButton("HOST DIRECT", () => HostDirect(name.value, port.value), "primary"));
                directRow.Add(ActionButton("JOIN DIRECT", () => JoinDirect(address.value, port.value, code.value, name.value)));
                card.Add(directRow);
                card.Add(Text("Use LAN/VPN, or configure firewall and UDP port forwarding. Direct traffic is not DTLS-encrypted and exposes the host address.", "muted"));
                card.Add(Text("UNITY RELAY", "eyebrow"));
                VisualElement relayRow = El("row");
                relayRow.Add(ActionButton("HOST RELAY LOBBY", () => HostOnline(name.value), "primary"));
                relayRow.Add(ActionButton("JOIN RELAY CODE", () => JoinOnline(code.value, name.value)));
                relayRow.SetEnabled(MultiplayerKillSwitch.IsEnabled);
                card.Add(relayRow);
                if (!MultiplayerKillSwitch.IsEnabled) card.Add(Text(MultiplayerKillSwitch.DisabledMessage, "muted"));
            }
            else
            {
                string joinCode = online.IsDirect ? online.DirectJoinCode : online.JoinCode;
                card.Add(Text($"{(online.IsDirect ? "DIRECT" : "RELAY")} JOIN CODE  •  {joinCode.ToUpperInvariant()}", "front-title"));
                card.Add(Text(online.PlayerSummary(), "body-copy"));
                VisualElement row = El("row");
                row.Add(ActionButton(online.IsReady ? "CLEAR READY" : "MARK READY", ToggleOnlineReady, online.IsReady ? "warning" : "primary"));
                if (!online.IsDirect) row.Add(ActionButton("REFRESH LOBBY", RefreshOnline));
                card.Add(row);
                if (online.IsHost)
                {
                    Button start = ActionButton(online.IsDirect ? "START DIRECT MATCH" : "START DTLS RELAY", StartOnlineRelay, "primary");
                    start.SetEnabled(online.CanStart && online.State != OnlinePreviewState.Working && online.State != OnlinePreviewState.Starting);
                    card.Add(start);
                }
                if (online.State == OnlinePreviewState.Connected)
                    card.Add(ActionButton("MEASURE AUTHORITY RTT", online.SendPing));
                card.Add(ActionButton("LEAVE SESSION", LeaveOnline, "danger-button"));
            }

            card.Add(Text("STATUS", "eyebrow"));
            card.Add(Text(online.Status + (online.LastRoundTripMilliseconds >= 0 ? $"\nLAST RTT  •  {online.LastRoundTripMilliseconds} MS" : string.Empty), "muted"));
            card.Add(Text("PUBLIC ACTIVATION GATES\n1. Full Local Hotseat privacy playtest\n2. Playable UI routed through side-scoped authority receipts\n3. Two-build latency, reconnect, migration, and adversarial leak evidence\n\nRemote Config controls admission to new Relay sessions only; Direct IP remains available in this preview.", "body-copy"));
            card.Add(ActionButton("BACK", ShowMode));
            page.Add(card);
            app.Add(page);
        }

        private async void HostOnline(string commanderName)
        {
            PlayerPrefs.SetString("OnlineCommanderName", commanderName ?? "Commander");
            await online.HostAsync(commanderName, "meridian-veil");
        }

        private async void JoinOnline(string code, string commanderName)
        {
            PlayerPrefs.SetString("OnlineCommanderName", commanderName ?? "Commander");
            await online.JoinAsync(code, commanderName);
        }

        private async void HostDirect(string commanderName, int port)
        {
            PlayerPrefs.SetString("OnlineCommanderName", commanderName ?? "Commander");
            PlayerPrefs.SetInt("DirectHostPort", Mathf.Clamp(port, 1, ushort.MaxValue));
            await online.HostDirectAsync(commanderName, (ushort)Mathf.Clamp(port, 1, ushort.MaxValue));
        }

        private async void JoinDirect(string address, int port, string code, string commanderName)
        {
            PlayerPrefs.SetString("OnlineCommanderName", commanderName ?? "Commander");
            PlayerPrefs.SetString("DirectHostAddress", address ?? "127.0.0.1");
            PlayerPrefs.SetInt("DirectHostPort", Mathf.Clamp(port, 1, ushort.MaxValue));
            await online.JoinDirectAsync(address, (ushort)Mathf.Clamp(port, 1, ushort.MaxValue), code, commanderName);
        }

        private async void ToggleOnlineReady() => await online.SetReadyAsync(!online.IsReady);
        private async void RefreshOnline() => await online.RefreshAsync();
        private async void StartOnlineRelay() => await online.StartAsync();
        private async void LeaveOnline() => await online.LeaveAsync();

        private void OnOnlineChanged()
        {
            if (view == View.Online && root != null) ShowOnline();
        }

        private async void RefreshKillSwitchThenRedraw()
        {
            await MultiplayerKillSwitch.RefreshAsync();
            if (view == View.Online && root != null) ShowOnline();
        }

        private void ShowScenario()
        {
            view = View.Scenario;
            BeginScreen("SELECT OPERATION");
            VisualElement page = El("front-page");
            ScrollView list = new ScrollView();
            list.AddToClassList("scenario-list");
            int operation = 1;
            foreach (ScenarioDefinition scenario in ScenarioCatalog.All())
            {
                VisualElement card = Panel("scenario-card", "scenario-option");
                card.Add(Text($"OPERATION {operation++:00}  •  {(scenario.Id == "meridian-veil" ? "CORE-LOOP TEST" : "20 NM THEATER PILOT")}", "eyebrow"));
                card.Add(Text(scenario.DisplayName.ToUpperInvariant(), "heading"));
                card.Add(Text(scenario.Summary, "body-copy"));
                card.Add(Text($"MAP       {scenario.Area.Width} × {scenario.Area.Height}  •  {scenario.Area.NauticalMilesPerHex} NM HEXES\nFORCES    {scenario.Formations.Count(f => f.Side == Side.Blue)} BLUE / {scenario.Formations.Count(f => f.Side == Side.Red)} RED\nHORIZON   T{scenario.Horizon} / {scenario.Horizon * scenario.Area.ReadyTimeHours:0} HOURS\nWEATHER   {scenario.Weather.ToUpperInvariant()}", "body-copy"));
                card.Add(ActionButton("REVIEW BRIEFING", () => { backend.ToolkitNewScenario(scenario.Id); OperationalMap3D.ResetPresentationMemory(); ShowBriefing(); }, "primary"));
                list.Add(card);
            }
            list.Add(ActionButton("BACK", ShowMode));
            page.Add(list);
            app.Add(page);
        }

        private void ShowBriefing()
        {
            view = View.Briefing;
            BeginScreen("OPERATION ORDER  •  " + backend.Game.Scenario.DisplayName.ToUpperInvariant());
            VisualElement page = El("front-page");
            VisualElement card = Panel("hero-card");
            card.Add(Text("CONTROL THE OPERATIONAL OBJECTIVE", "heading"));
            card.Add(Text("MISSION", "eyebrow"));
            card.Add(Text(backend.Game.Scenario.Summary + $" Resolve at Time {backend.Game.Scenario.Horizon}. Preserve your carrier group in combat-capable condition. Destruction is a means to those operational ends.", "body-copy"));
            card.Add(Text("OPERATIONAL SCORING", "eyebrow"));
            card.Add(Text(string.Join("\n", backend.Game.Scenario.Objectives.Select(objective => $"{objective.Points} VP — {objective.Title}")) + "\nTIES — combat-capable formations, lower damage burden, then closest surviving naval formation", "body-copy"));
            card.Add(Text($"ASSUMPTIONS\n20 nm hexes • 2 hours / Ready Time • Formation-specific Strike ranges • Age-2 penalty {(backend.AgeTwoPenalty ? "ON" : "OFF")} • Automatic Defend • MODE {(backend.SelectedMode == OperationMode.SoloVsAi ? "SOLO VS AI" : "LOCAL HOTSEAT")}", "muted"));
            VisualElement row = El("row");
            row.Add(ActionButton("REVIEW SETUP", ShowSetup, "primary"));
            row.Add(ActionButton("BACK", ShowScenario));
            card.Add(row);
            page.Add(card);
            app.Add(page);
        }

        private void ShowSetup()
        {
            view = View.Setup;
            BeginScreen("INITIAL SETUP  •  FIXED TEST DEPLOYMENT");
            VisualElement page = El("front-page");
            VisualElement card = Panel("hero-card");
            card.Add(Text("FORCES ENTER " + backend.Game.Area.DisplayName.ToUpperInvariant(), "heading"));
            card.Add(Text("Fixed starting positions ensure paired playtests begin from identical conditions.", "body-copy"));
            VisualElement forces = El("row");
            forces.style.justifyContent = Justify.SpaceBetween;
            forces.Add(OrderOfBattle(Side.Blue));
            forces.Add(OrderOfBattle(Side.Red));
            card.Add(forces);
            OperationalLocationDefinition objective = backend.Game.Area.Locations.FirstOrDefault(location => location.Kind == LocationKind.Objective);
            card.Add(Text($"Objective: {(objective?.Name ?? "OPERATIONAL CONTROL").ToUpperInvariant()} • {backend.Game.Area.Objective}", "subheading", "amber"));
            VisualElement row = El("row");
            row.Add(ActionButton("LOCK SETUP & BEGIN", BeginSelectedOperation, "primary"));
            row.Add(ActionButton("BACK", ShowBriefing));
            card.Add(row);
            page.Add(card);
            app.Add(page);
        }

        private VisualElement OrderOfBattle(Side side)
        {
            VisualElement panel = Panel();
            panel.style.width = Length.Percent(47);
            panel.Add(Text(side.ToString().ToUpperInvariant() + " ORDER OF BATTLE", "subheading"));
            foreach (FormationState formation in backend.Game.Formations.Where(f => f.Side == side))
                panel.Add(Text($"{formation.Name}  •  {formation.Kind}  •  HEX {formation.Position}", "body-copy"));
            return panel;
        }

        private void ShowHandoff()
        {
            view = View.Handoff;
            BeginScreen("SECURE INFORMATION HANDOFF");
            VisualElement page = El("front-page");
            VisualElement card = Panel("hero-card", "center");
            card.Add(Text(backend.Game.Active.Side.ToString().ToUpperInvariant() + " COMMAND", "front-title"));
            card.Add(Text("Pass control to the indicated player. Hidden formations and Contacts remain concealed until they assume command.", "body-copy"));
            card.Add(Text($"NEXT READY  •  {backend.Game.Active.Name.ToUpperInvariant()}  •  T{backend.Game.Time:00}", "subheading", "amber"));
            card.Add(ActionButton("ASSUME COMMAND", () => { backend.ToolkitBeginDecision(); ShowGame(); }, "primary"));
            page.Add(card);
            app.Add(page);
        }

        private void BeginSelectedOperation()
        {
            previousSide = backend.Game.Active.Side;
            ContinueOperation();
        }

        private void LoadOperationAndContinue()
        {
            backend.ToolkitLoad();
            OperationalMap3D.ResetPresentationMemory();
            ContinueOperation();
        }

        private void ContinueOperation()
        {
            if (backend.IsComplete) { ShowResults(); return; }
            if (backend.SelectedMode == OperationMode.SoloVsAi)
            {
                if (backend.IsAiTurn) BeginAiSequence();
                else { backend.ToolkitBeginDecision(); ShowGame(); }
            }
            else ShowHandoff();
        }

        private void BeginAiSequence()
        {
            if (aiRunning) return;
            ShowAiPlanning();
            StartCoroutine(RunAiSequence());
        }

        private IEnumerator RunAiSequence()
        {
            aiRunning = true;
            yield return new WaitForSecondsRealtime(backend.ReducedMotion ? .1f : .55f);
            int guard = 0;
            while (backend.IsAiTurn && !backend.IsComplete && guard++ < 64)
            {
                backend.ToolkitBeginDecision();
                yield return new WaitForSecondsRealtime(backend.ReducedMotion ? .05f : .22f);
                AiDecision decision = backend.ToolkitChooseAiDecision();
                if (decision?.Action == ActionKind.Strike && !decision.DeclareSynchronizedStrike)
                {
                    ContactState contact = backend.Game.Contacts.FirstOrDefault(item => item.Owner == backend.Game.Active.Side && !item.IsLost && item.TargetId == decision.TargetId);
                    FormationState target = backend.Game.Find(decision.TargetId);
                    if (contact != null && target != null && target.Side == backend.HumanSide && target.Position.Equals(decision.Hex))
                    {
                        aiRunning = false;
                        BeginStrikeReaction(target, decision.Salvo, decision);
                        yield break;
                    }
                }
                backend.ToolkitExecuteAiTurn(decision, null, null);
                if (!backend.LastToolkitActionSucceeded) break;
                if (backend.IsAiTurn && !backend.IsComplete) ShowAiPlanning();
                yield return new WaitForSecondsRealtime(backend.ReducedMotion ? .05f : .35f);
            }
            aiRunning = false;
            if (backend.IsComplete) ShowResults();
            else if (backend.IsAiTurn) ShowAiPlanning(true);
            else { backend.ToolkitBeginDecision(); ShowGame(); }
        }

        private void ShowAiPlanning(bool stalled = false)
        {
            view = View.AiPlanning;
            actionMode = ToolkitActionMode.None;
            BeginScreen($"OPERATIONAL TIME T{backend.Game.Time:00}  •  SOLO VS AI");
            VisualElement page = El("front-page");
            VisualElement card = Panel("hero-card", "center");
            card.Add(Text(stalled ? "OPFOR DECISION PAUSED" : "OPFOR PLANNING", "front-title"));
            card.Add(Text(stalled ? "The AI could not complete its preferred legal action. Retry the decision or return safely to the main menu." : "Red formations are resolving through the same Ready-Time queue and legal action system. Hidden OPFOR information remains concealed.", "body-copy"));
            if (stalled)
            {
                card.Add(ActionButton("RETRY AI DECISION", BeginAiSequence, "primary"));
                card.Add(ActionButton("MAIN MENU", ShowMain));
            }
            page.Add(card);
            app.Add(page);
        }

        private void ShowGame()
        {
            view = View.Game;
            BeginScreen($"OPERATIONAL TIME T{backend.Game.Time:00}", true);
            FormationState active = backend.Game.Active;
            previousSide = active.Side;

            VisualElement workspace = El("workspace");
            workspace.Add(BuildTimeline());
            VisualElement mapColumn = El("map-column");
            map = new TacticalMapElement();
            map.HexChosen = OnHexChosen;
            map.ContactChosen = OnContactChosen;
            map.ContactHovered = contact => UpdateDossier(contact);
            map.ActiveFormationContextRequested = ShowFormationOrders;
            map.SetEdgeScroll(edgeScrollEnabled);
            map.SetState(backend.Game, actionMode, moveMode, searchMode, salvo, backend.ReducedMotion, backend.HexGridVisible);
            mapColumn.Add(map);
            Label mapHelp = Text($"3D COMMAND MAP  •  {backend.Game.Area.NauticalMilesPerHex} NM / HEX  •  TAB EDGE HUD  •  H CARDS  •  L LOG  •  WASD MOVE  •  C ACTIVE  •  O OBJECTIVE  •  V EDGE PAN  •  HOME RESET", "muted", "map-help");
            mapColumn.Add(mapHelp);
            edgeHudToggle = ActionButton(edgeHudVisible ? "HIDE EDGE HUD  •  TAB" : "SHOW EDGE HUD  •  TAB", ToggleEdgeHud, "map-hud-toggle");
            mapColumn.Add(edgeHudToggle);
            workspace.Add(mapColumn);
            VisualElement dossierPanel = Panel("dossier");
            var dossierScroll = new ScrollView();
            dossierScroll.AddToClassList("dossier-scroll");
            dossierScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            dossierScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            dossier = dossierScroll;
            UpdateDossier(null);
            dossierPanel.Add(dossierScroll);
            workspace.Add(dossierPanel);
            app.Add(workspace);

            VisualElement bottom = El("bottom-deck", "command-dock");
            bottom.Add(BuildActions());
            app.Add(bottom);
            ApplyEdgeHudState();
            if (backend.AudioDescriptions)
            {
                Label audioDescription = Text("AUDIO DESCRIPTION  •  " + backend.LastAudioDescription, "audio-description");
                audioDescription.name = "audio-description-status";
                app.Add(audioDescription);
            }
            map.schedule.Execute(() => { map.Refresh(); map.Focus(); });
            if (backend.ToolkitPendingEntropyEffect(active.Side) != null && backend.ToolkitPendingEntropyFormation(active.Side) != null)
            {
                if (backend.SelectedMode == OperationMode.SoloVsAi && active.Side != backend.HumanSide)
                {
                    while (backend.ToolkitPendingEntropyEffect(active.Side) != null) backend.ToolkitAcknowledgeEntropyEffect(active.Side);
                }
                else root.schedule.Execute(() => ShowEntropyPull(active.Side));
            }
        }

        private VisualElement BuildTimeline()
        {
            VisualElement panel = Panel("timeline");
            panel.Add(Text("FORMATION TIMELINE", "subheading"));
            Label reason = Text(backend.ToolkitActivationReason(), "muted");
            reason.tooltip = "Lowest Ready Time. On a cross-side tie, priority passes away from the side that acted most recently; within that side use lower Entropy, higher Command, then stable ID.";
            panel.Add(reason);
            ScrollView scroll = new ScrollView();
            foreach (SynchronizedStrikeState strike in backend.Game.SynchronizedStrikes.Where(item => item.Side == backend.Game.Active.Side).OrderBy(item => item.StrikeTime))
            {
                Button sync = ActionButton($"SYNC EVENT  •  T{strike.StrikeTime:00}\n{strike.Participants.Count} FORMATIONS  •  AIM {strike.Aim}\nCOMMAND SLOT HELD", () => UpdateDossier(null), "selected");
                sync.AddToClassList("timeline-row");
                scroll.Add(sync);
            }
            int queuePosition = 0;
            foreach (FormationState formation in backend.Game.ActivationQueue())
            {
                string queueLabel = queuePosition++ == 0 ? "NOW" : $"NEXT {queuePosition - 1}";
                string reactionState = formation.Replenishing ? "LOCKED" : formation.HasReacted ? "SPENT" : "READY";
                string assignment = formation.PatrolActive ? $" • Screen {formation.PatrolPosture}" : formation.SupportActive ? $" • Support {formation.SupportKind}" : string.Empty;
                Button row = ActionButton($"{queueLabel}  •  T{formation.ReadyTime:00}\n{formation.Name}\n{KindTag(formation.Kind)}  •  R:{reactionState}  •  E:{formation.Endurance.ToString().ToUpperInvariant()} {Math.Min(formation.MajorActions, 2)}/3", () => UpdateDossier(null));
                row.AddToClassList("timeline-row");
                if (formation == backend.Game.Active) row.AddToClassList("active");
                row.tooltip = $"Activation queue position {queuePosition} • {formation.Side} • Ready T{formation.ReadyTime:00} • {formation.Kind} • {formation.Cohesion} • Reaction {reactionState} • Endurance {formation.EnduranceProgress} • Entropy {formation.EntropySources} • Command {formation.EffectiveCommand}{assignment}";
                scroll.Add(row);
            }
            panel.Add(scroll);
            return panel;
        }

        private VisualElement BuildActions()
        {
            VisualElement panel = Panel("actions");
            SynchronizedStrikeState readyStrike = backend.Game.ReadySynchronizedStrikeFor(backend.Game.Active);
            if (readyStrike != null)
            {
                ContactState current = backend.Game.Contacts.FirstOrDefault(item => item.Owner == readyStrike.Side && item.TargetId == readyStrike.ContactTargetId);
                panel.Add(Text($"SYNCHRONIZED STRIKE READY  •  T{readyStrike.StrikeTime:00}", "subheading", "amber"));
                panel.Add(Text($"{readyStrike.Participants.Count} reserved formations  •  aim {readyStrike.Aim}  •  one defender Reaction  •  Defense −1 after each attack\nContact: {(current == null || current.IsLost ? "LOST — abort, retask, or continue blind" : current.Summary)}", "body-copy"));
                VisualElement resolution = El("row");
                resolution.Add(ActionButton(current == null || current.IsLost ? "CONTINUE BLIND" : "RESOLVE VOLLEY", () => BeginSynchronizedStrikeResolution(readyStrike, current == null || current.IsLost), "primary"));
                resolution.Add(ActionButton("RETASK  •  +1 TIME / FRICTION", () => ShowSynchronizedRetaskContacts(readyStrike), "warning"));
                resolution.Add(ActionButton("ABORT  •  +1 TIME / FRICTION", () => ResolveAction(() => backend.ToolkitAbortSynchronizedStrike(readyStrike))));
                panel.Add(resolution);
                panel.Add(Text(backend.LastMessage, "muted"));
                return panel;
            }
            SideState commandSide = backend.Game.Sides[backend.Game.Active.Side];
            VisualElement actionHeader = El("row");
            actionHeader.Add(Text("CHOOSE AN ACTION", "subheading"));
            actionHeader.Add(El("spacer"));
            actionHeader.Add(ActionButton("MISSION", ShowMissionFormationChoices));
            if (backend.Game.Active.EntropySources >= 3) actionHeader.Add(ActionButton("PUSH THROUGH • +1 STRAIN", () => { backend.ToolkitPushThrough(); ShowGame(); }, "warning"));
            if (backend.Game.Active.Kind == FormationKind.CarrierGroup && backend.Game.HasLogisticsAccess(backend.Game.Active) && commandSide.CommandStrain > 0)
                actionHeader.Add(ActionButton("HQ RECOVERY • 2", () => ResolveAction(() => backend.ToolkitRestoreCommand()), "primary"));
            panel.Add(actionHeader);
            string slotSummary = string.Join("  ", backend.Game.CommandSlotsFor(commandSide.Side).Select(slot => $"◆{slot.Index} {slot.Status.ToString().ToUpperInvariant()}{(string.IsNullOrEmpty(slot.Purpose) ? string.Empty : " " + slot.Purpose)}"));
            Label commandSummary = Text($"COMMAND {commandSide.CommandSlots}/3 FREE  •  STRAIN {commandSide.CommandStrain}", "muted", "command-summary");
            commandSummary.tooltip = slotSummary;
            panel.Add(commandSummary);
            VisualElement actions = El("row", "action-row");
            actions.Add(ActionButton("1  ⇢ MOVE  •2", () => SelectAction(ToolkitActionMode.Move), actionMode == ToolkitActionMode.Move ? "selected" : null));
            actions.Add(ActionButton("2  ◎ SEARCH  •2", () => SelectAction(ToolkitActionMode.Search), actionMode == ToolkitActionMode.Search ? "selected" : null));
            actions.Add(ActionButton("3  ✦ STRIKE  •2", () => SelectAction(ToolkitActionMode.Strike), actionMode == ToolkitActionMode.Strike ? "selected" : null));
            actions.Add(ActionButton("4  ↻ RECOVER  •2", () => ResolveAction(() => backend.ToolkitRecover())));
            actions.Add(ActionButton("5  ■ HOLD  •1", () => ResolveAction(() => backend.ToolkitHold())));
            panel.Add(actions);
            if (actionMode == ToolkitActionMode.None)
            {
                VisualElement assignments = El("row", "action-row");
                assignments.Add(ActionButton("6  ◉ SCREEN  •1", ShowPatrolSetup));
                assignments.Add(ActionButton("7  + SUPPORT  •1", ShowSupportSetup));
                assignments.Add(ActionButton("8  ⟳ REPLENISH  •3", () => { SelectAction(ToolkitActionMode.Replenish); ShowReplenishmentSetup(); }));
                assignments.Add(ActionButton("9  ✦✦ SYNC STRIKE", ShowSynchronizedStrikeContacts));
                panel.Add(assignments);
            }
            VisualElement modes = El("row", "action-row");
            if (actionMode == ToolkitActionMode.Move)
            {
                FormationState active = backend.Game.Active;
                int cautious = Rules.MoveAllowance(active, MoveMode.Cautious), normal = Rules.MoveAllowance(active, MoveMode.Normal), high = Rules.MoveAllowance(active, MoveMode.HighTempo);
                modes.Add(ModeButton($"CAUTIOUS • {cautious} / {cautious * backend.Game.Area.NauticalMilesPerHex} NM", MoveMode.Cautious)); modes.Add(ModeButton($"NORMAL • {normal} / {normal * backend.Game.Area.NauticalMilesPerHex} NM", MoveMode.Normal)); modes.Add(ModeButton($"HIGH TEMPO • {high} / {high * backend.Game.Area.NauticalMilesPerHex} NM + F", MoveMode.HighTempo));
            }
            else if (actionMode == ToolkitActionMode.Search)
            {
                int scale = backend.Game.Area.NauticalMilesPerHex;
                FormationState active = backend.Game.Active;
                modes.Add(ModeButton($"PASSIVE • +0 / {backend.Game.SearchRangeFor(active, SearchMode.Passive) * scale} NM", SearchMode.Passive)); modes.Add(ModeButton($"ACTIVE • +1 / {backend.Game.SearchRangeFor(active, SearchMode.Active) * scale} NM LOUD", SearchMode.Active)); modes.Add(ModeButton($"FOCUSED • +2 / {backend.Game.SearchRangeFor(active, SearchMode.Focused) * scale} NM / 1◆", SearchMode.Focused));
                modes.Add(ModeButton("IMPROVE LOCATION", SearchPriority.Location));
                modes.Add(ModeButton("IMPROVE IDENTITY", SearchPriority.Identity));
            }
            else if (actionMode == ToolkitActionMode.Strike)
            {
                FormationKind kind = backend.Game.Active.Kind;
                int scale = backend.Game.Area.NauticalMilesPerHex;
                foreach (Salvo option in Enum.GetValues(typeof(Salvo)))
                {
                    int remaining = backend.Game.Active.Weapons?.Available(option) ?? -1;
                    Button weapon = ModeButton($"{option.ToString().ToUpperInvariant()} • +{Rules.SalvoModifier(option)} / {Rules.StrikeRange(kind, option) * scale} NM • {(remaining < 0 ? "LEGACY" : remaining.ToString())} LEFT", option);
                    weapon.SetEnabled(backend.Game.Active.CanFire(option));
                    modes.Add(weapon);
                }
            }
            panel.Add(modes);
            panel.Add(Text(ActionPreview(), "body-copy", "amber", "action-preview"));
            if (actionMode == ToolkitActionMode.None) panel.Add(Text(backend.LastMessage, "muted"));
            return panel;
        }

        private void ShowHeldEntropyCard(FormationState formation, EntropyEffectDefinition card)
        {
            VisualElement modal = Modal(card.Id + "  •  " + card.Title.ToUpperInvariant());
            VisualElement effect = Panel("entropy-card", card.Source.ToString().ToLowerInvariant());
            effect.Add(Text("ATTACHED TO  •  " + formation.Name.ToUpperInvariant(), "eyebrow"));
            effect.Add(Text(card.Effect, "entropy-effect"));
            if (!string.IsNullOrEmpty(card.Response)) effect.Add(Text($"RESPONSE  •  {card.Response}\nWINDOW  •  BEFORE THIS FORMATION COMPLETES ITS NEXT ACTION", "body-copy", "amber"));
            modal.Add(effect);
            if (!string.IsNullOrEmpty(card.Response) && formation.HasEffect(card.Id))
            {
                Button respond = ActionButton("SPEND 1 COMMAND TO RESPOND", () =>
                {
                    backend.ToolkitRespondToEntropy(formation, card.Id);
                    ShowGame();
                }, "warning");
                respond.SetEnabled(backend.ToolkitCanRespondToEntropy(formation, card.Id));
                modal.Add(respond);
            }
            if (formation == backend.Game.Active && card.Source != EntropySource.Destruction)
                modal.Add(ActionButton("RECOVER & DISCARD THIS CARD • 2", () => { CloseOverlay(); ResolveAction(() => backend.ToolkitRecover(card.Source, card.Id)); }, "primary"));
            modal.Add(ActionButton("CLOSE", CloseOverlay, "primary"));
        }

        private void UpdateDossier(ContactState contact)
        {
            if (dossier == null) return;
            dossier.Clear();
            if (contact != null && actionMode != ToolkitActionMode.None)
            {
                FormationState target = backend.Game.Find(contact.TargetId);
                dossier.Add(Text("PRE-COMMITMENT PREVIEW", "eyebrow"));
                dossier.Add(Text(contact.Identity == IdentityQuality.Identified && target != null ? target.Name.ToUpperInvariant() : "UNRESOLVED CONTACT", "heading"));
                dossier.Add(Text(contact.Summary + "\nLAST KNOWN " + contact.LastKnownPosition, "body-copy"));
                if (actionMode == ToolkitActionMode.Search)
                {
                    int centerRange = HexCoord.Distance(backend.Game.Active.Position, contact.LastKnownPosition);
                    dossier.Add(Text($"AREA CENTER {contact.LastKnownPosition}\nCENTER RANGE {centerRange} / {backend.Game.SearchRangeFor(backend.Game.Active, searchMode)}\nFOOTPRINT RADIUS {Rules.SearchAreaRadius}\nIMPROVEMENT PRIORITY {searchPriority.ToString().ToUpperInvariant()}\nSEARCH {backend.Game.Active.EffectiveSearch}\nMODE {backend.Game.SearchModifierFor(backend.Game.Active, searchMode):+0;-0;0}\n\nHidden Signature, Loud status, and exact position resolve privately.", "body-copy"));
                }
                else if (actionMode == ToolkitActionMode.Strike)
                {
                    int attack = backend.Game.Active.EffectiveStrike + Rules.SalvoModifier(salvo) + backend.Game.PendingSupportBonus(backend.Game.Active, SupportKind.Strike) + Rules.TargetingModifier(contact, backend.Game.AgeTwoTargetingPenalty);
                    int possible = backend.Game.ContactPossibleHexes(contact).Count;
                    dossier.Add(Text($"ATTACK {attack}\nDEFENSE UNRESOLVED\nPOSSIBLE AREA {possible} HEX{(possible == 1 ? string.Empty : "ES")}\n\nSelect the Contact, then commit one aim hex. Hidden occupancy, target condition, defensive Support, and Reaction resolve only after commitment.", "body-copy", "amber"));
                }
                return;
            }

            FormationState formation = backend.Game.Active;
            dossier.Add(Text(formation.Side.ToString().ToUpperInvariant() + " FORMATION DOSSIER", "eyebrow"));
            dossier.Add(Text(formation.Name.ToUpperInvariant(), "heading"));
            dossier.Add(Text(formation.Kind + " • HEX " + formation.Position, "body-copy"));
            VisualElement stats = El("stat-grid");
            stats.Add(Stat("MOVE", formation.EffectiveMove, "Maximum movement after damage and Endurance."));
            stats.Add(Stat("SEARCH", formation.EffectiveSearch, "Detection after Disruption."));
            stats.Add(Stat("STRIKE", formation.EffectiveStrike, "Offense after Destruction."));
            stats.Add(Stat("DEFENSE", formation.EffectiveDefense, "Survivability before Reaction and attached effects."));
            stats.Add(Stat("SIGNATURE", formation.EffectiveSignature, "Higher is easier to detect."));
            stats.Add(Stat("COMMAND", formation.EffectiveCommand, "Same-Time initiative and autonomy after attached effects."));
            stats.Add(Stat("EW", formation.Ratings.ElectronicWarfare, "Reduces detectability and strengthens layered missile defense."));
            stats.Add(Stat("CYBER", formation.Ratings.Cyber, "Improves spectrum exploitation during Search resolution."));
            dossier.Add(stats);
            string reactionStatus = formation.Replenishing ? "UNAVAILABLE" : formation.HasReacted ? "SPENT" : "AVAILABLE";
            string objective = string.IsNullOrEmpty(formation.MissionObjectiveId) ? formation.MissionObjectiveHex.ToString() : formation.MissionObjectiveId;
            dossier.Add(Text($"MISSION  {formation.Mission.ToString().ToUpperInvariant()}  •  {Words(formation.MissionObjective.ToString())} {objective}\n{formation.MissionPosture.ToString().ToUpperInvariant()}  •  TRIGGER {Words(formation.MissionTrigger.ToString())}\nLAST  {(formation.LastActionFollowedMission ? "FOLLOWED MISSION" : "IMMEDIATE RETASK")}{(formation.PendingMissionChange ? $"  •  DELIVERY T{formation.MissionDeliveryTime:00}" : string.Empty)}", "body-copy", "dossier-summary"));
            dossier.Add(Text($"READY T{formation.ReadyTime:00}  •  REACTION {reactionStatus}\nENDURANCE {formation.Endurance.ToString().ToUpperInvariant()}  •  {formation.EnduranceProgress.ToUpperInvariant()}\nDAMAGE {formation.Damage.ToString().ToUpperInvariant()}  •  {formation.Cohesion.ToUpperInvariant()}", "body-copy", "dossier-summary"));
            dossier.Add(Text($"ENTROPY  •  F {(formation.Friction ? "MARKED" : "CLEAR")}  •  D {(formation.Disruption ? "MARKED" : "CLEAR")}  •  X {(formation.Destruction ? "MARKED" : "CLEAR")}{(formation.OrderlyWithdrawalReady ? "\nORDERLY WITHDRAWAL PREPARED" : string.Empty)}", "muted", formation.EntropySources > 0 ? "amber" : null));
            dossier.Add(Text($"MAGAZINES  •  {formation.Weapons?.Summary ?? "LEGACY LOADOUT"}\nCOMMAND  •  {backend.Game.Sides[formation.Side].Architecture.ToString().ToUpperInvariant()}  •  {backend.Game.Sides[formation.Side].CommandSlots} FREE{(formation.Kind == FormationKind.Submarine ? "\nDEPTH  •  " + formation.SubmarineDepth.ToString().ToUpperInvariant() : string.Empty)}\nWEATHER  •  {backend.Game.RuntimeWeather.ToUpperInvariant()}  •  SEVERITY {backend.Game.RuntimeWeatherSeverity}", "muted"));
            dossier.Add(Text(Rules.EnduranceEffect(formation.Endurance).ToUpperInvariant(), "muted", formation.Endurance == Endurance.Critical ? "amber" : null));
            if (formation.PatrolActive)
                dossier.Add(Text($"SCREEN {formation.PatrolPosture.ToString().ToUpperInvariant()} • CENTER {formation.PatrolCenter} • RADIUS {Rules.PatrolRadius}\nINTERCEPTION {(formation.PatrolInterceptionAvailable ? "READY" : "SPENT")}{(string.IsNullOrEmpty(formation.PatrolProtectedFormationId) ? string.Empty : " • PROTECTING " + (backend.Game.Find(formation.PatrolProtectedFormationId)?.Name ?? formation.PatrolProtectedFormationId))}", "body-copy", "amber"));
            if (formation.SupportActive)
                dossier.Add(Text($"SUPPORT {formation.SupportKind.ToString().ToUpperInvariant()} +1 → {backend.Game.Find(formation.SupportRecipientId)?.Name ?? formation.SupportRecipientId}\nPERSISTS UNTIL USED, OUT OF RANGE, OR SUPPORTER'S NEXT ACTION", "body-copy", "amber"));
            if (formation.SupportBlockedUntilRecover) dossier.Add(Text("SUPPORT BLOCKED UNTIL RECOVER", "body-copy", "amber"));
            IReadOnlyList<OperationalLocationDefinition> logistics = backend.Game.LogisticsFacilitiesFor(formation);
            string logisticsStatus = backend.Game.HasLogisticsAccess(formation) ? "LOGISTICS ACCESS AVAILABLE HERE" : logistics.Count == 0 ? "NO COMPATIBLE SCENARIO LOGISTICS FACILITY" : $"NEAREST LOGISTICS • {logistics[0].Name.ToUpperInvariant()} AT {logistics[0].Hex} • {HexCoord.Distance(formation.Position, logistics[0].Hex)} HEX";
            dossier.Add(Text(logisticsStatus + (backend.Game.NeedsReplenishment(formation) ? "\n" + backend.Game.ReplenishmentPreview(formation) : string.Empty), "muted"));
            foreach (string cardId in formation.ActiveEffectCardIds ?? new List<string>())
            {
                EntropyEffectDefinition card = EntropyEffectCatalog.Find(cardId);
                if (card == null) continue;
                VisualElement effect = Panel("attached-effect", card.Source.ToString().ToLowerInvariant());
                effect.Add(Text($"{card.Id}  â€¢  {card.Source.ToString().ToUpperInvariant()}", "eyebrow"));
                effect.Add(Text(card.Title.ToUpperInvariant(), "subheading"));
                effect.Add(Text(card.Effect, "muted"));
                if (!EntropyEffectCatalog.IsFullyMechanicallySupported(card)) effect.Add(Text("NOT FULLY ACTIVE • PARENT SYSTEM PENDING", "muted", "amber"));
                dossier.Add(effect);
            }
        }

        private void ShowEntropyPull(Side side, Action onComplete = null)
        {
            EntropyEffectDefinition card = backend.ToolkitPendingEntropyEffect(side);
            FormationState formation = backend.ToolkitPendingEntropyFormation(side);
            if (card == null || formation == null)
            {
                entropyRevealBlocking = false;
                onComplete?.Invoke();
                return;
            }
            entropyRevealBlocking = true;
            VisualElement modal = Modal(card.Source.ToString().ToUpperInvariant() + " EFFECT DRAWN");
            VisualElement effect = Panel("entropy-card", card.Source.ToString().ToLowerInvariant());
            effect.Add(Text(card.Id, "entropy-card-id"));
            effect.Add(Text(card.Title.ToUpperInvariant(), "front-title"));
            effect.Add(Text("ATTACHED TO  â€¢  " + formation.Name.ToUpperInvariant(), "eyebrow"));
            effect.Add(Text(card.Effect, "entropy-effect"));
            if (!EntropyEffectCatalog.IsFullyMechanicallySupported(card)) effect.Add(Text("This card is tracked and displayed, but some or all of its parent Mission, Synchronization, Support, Replenish, or uncertainty-geometry system is not active yet.", "body-copy", "amber"));
            if (!string.IsNullOrEmpty(card.Response)) effect.Add(Text($"RESPONSE  •  {card.Response}\nWINDOW  •  BEFORE THIS FORMATION COMPLETES ITS NEXT ACTION", "body-copy", "amber"));
            modal.Add(effect);
            if (!string.IsNullOrEmpty(card.Response) && formation.HasEffect(card.Id))
            {
                Button respond = ActionButton("SPEND 1 COMMAND TO RESPOND", () =>
                {
                    backend.ToolkitRespondToEntropy(formation, card.Id);
                    ShowEntropyPull(side, onComplete);
                }, "warning");
                respond.SetEnabled(backend.ToolkitCanRespondToEntropy(formation, card.Id));
                modal.Add(respond);
            }
            modal.Add(ActionButton("ACKNOWLEDGE CARD", () =>
            {
                backend.ToolkitAcknowledgeEntropyEffect(side);
                if (backend.ToolkitPendingEntropyEffect(side) != null) ShowEntropyPull(side, onComplete);
                else
                {
                    entropyRevealBlocking = false;
                    CloseOverlay();
                    onComplete?.Invoke();
                }
            }, "primary"));
        }

        private void ShowPatrolSetup()
        {
            FormationState active = backend.Game.Active;
            VisualElement modal = Modal("PATROL / SCREEN • SELECT AREA");
            modal.Add(Text($"Establish a radius-{Rules.PatrolRadius} Screen in the current or an adjacent hex. It persists until this Formation's next non-Patrol action and provides one extra interception.", "body-copy"));
            var choices = new List<Tuple<HexCoord, FormationState, string>> { Tuple.Create(active.Position, (FormationState)null, $"AREA AT {active.Position}") };
            if (HexCoord.Distance(active.Position, backend.Game.Area.Objective) <= 1 && !active.Position.Equals(backend.Game.Area.Objective))
                choices.Add(Tuple.Create(backend.Game.Area.Objective, (FormationState)null, $"OBJECTIVE AT {backend.Game.Area.Objective}"));
            foreach (FormationState friendly in backend.Game.Formations.Where(candidate => candidate.Side == active.Side && candidate != active && !candidate.IsDestroyed && HexCoord.Distance(active.Position, candidate.Position) <= 1))
                choices.Add(Tuple.Create(friendly.Position, friendly, "PROTECT " + friendly.Name.ToUpperInvariant()));
            foreach (var choice in choices)
            {
                HexCoord center = choice.Item1; FormationState protectedFormation = choice.Item2;
                modal.Add(ActionButton(choice.Item3, () => ShowPatrolPostures(center, protectedFormation)));
            }
            modal.Add(ActionButton("CANCEL", CloseOverlay));
        }

        private void ShowPatrolPostures(HexCoord center, FormationState protectedFormation)
        {
            VisualElement modal = Modal("PATROL / SCREEN • SELECT POSTURE");
            modal.Add(Text($"CENTER {center} • RADIUS {Rules.PatrolRadius}\nDefensive grants +1 Defense inside the area. Balanced preserves normal Signature. Aggressive grants +1 interception Attack and makes the screener Loud.", "body-copy"));
            foreach (PatrolPosture posture in Enum.GetValues(typeof(PatrolPosture)))
            {
                PatrolPosture selected = posture;
                modal.Add(ActionButton(selected.ToString().ToUpperInvariant(), () => { CloseOverlay(); ResolveAction(() => backend.ToolkitPatrol(center, selected, protectedFormation)); }, selected == PatrolPosture.Balanced ? "primary" : null));
            }
            modal.Add(ActionButton("BACK", ShowPatrolSetup));
        }

        private void ShowSupportSetup()
        {
            FormationState active = backend.Game.Active;
            VisualElement modal = Modal("SUPPORT • SELECT RECIPIENT");
            modal.Add(Text($"Assign a single-use +1 bonus to another friendly Formation within {Rules.SupportRange} hexes. It persists until used, range is broken, or the supporter takes another action.", "body-copy"));
            List<FormationState> recipients = backend.Game.Formations.Where(candidate => candidate.Side == active.Side && candidate != active && !candidate.IsDestroyed && HexCoord.Distance(active.Position, candidate.Position) <= Rules.SupportRange).OrderBy(candidate => candidate.Id).ToList();
            foreach (FormationState recipient in recipients)
            {
                FormationState selected = recipient;
                modal.Add(ActionButton($"{selected.Name.ToUpperInvariant()} • HEX {selected.Position}", () => ShowSupportKinds(selected)));
            }
            if (recipients.Count == 0) modal.Add(Text("No friendly Formation is currently in Support range.", "muted", "amber"));
            modal.Add(ActionButton("CANCEL", CloseOverlay));
        }

        private void ShowSupportKinds(FormationState recipient)
        {
            VisualElement modal = Modal("SUPPORT • SELECT EFFECT");
            modal.Add(Text("Strike, Search, Defense, and ASW Search apply +1 to the recipient's matching next action or defense. Synchronization stores +1 coordination for the synchronized-action system.", "body-copy"));
            foreach (SupportKind kind in Enum.GetValues(typeof(SupportKind)))
            {
                SupportKind selected = kind;
                Button choice = ActionButton(selected.ToString().ToUpperInvariant(), () => { CloseOverlay(); ResolveAction(() => backend.ToolkitSupport(recipient, selected)); });
                if (selected == SupportKind.Synchronization)
                {
                    choice.SetEnabled(backend.Game.CanParticipateInSynchronization(backend.Game.Active) && backend.Game.CanParticipateInSynchronization(recipient));
                    if (backend.Game.Active.HasEffect("F-05") || recipient.HasEffect("F-05")) choice.text += "  •  COORDINATION DRIFT";
                }
                modal.Add(choice);
            }
            modal.Add(ActionButton("BACK", ShowSupportSetup));
        }

        private void ShowReplenishmentSetup()
        {
            FormationState active = backend.Game.Active;
            VisualElement modal = Modal("REPLENISHMENT • LOGISTICS");
            IReadOnlyList<OperationalLocationDefinition> facilities = backend.Game.LogisticsFacilitiesFor(active);
            bool access = backend.Game.HasLogisticsAccess(active);
            modal.Add(Text($"{active.Name.ToUpperInvariant()} • HEX {active.Position}\n{(access ? "VALID LOGISTICS ACCESS" : "NO LOGISTICS ACCESS AT CURRENT HEX")}\n\n{backend.Game.ReplenishmentPreview(active)}\n\nBase Time 3{(active.HasEffect("X-10") ? " +1 Hull Breach" : string.Empty)}. The Formation cannot React while servicing and becomes available again when its Ready Time arrives.", "body-copy", access ? "amber" : null));
            if (!access)
            {
                foreach (OperationalLocationDefinition facility in facilities)
                    modal.Add(Text($"{facility.Kind.ToString().ToUpperInvariant()} • {facility.Name.ToUpperInvariant()} • HEX {facility.Hex} • RANGE {HexCoord.Distance(active.Position, facility.Hex)}", "muted"));
                if (facilities.Count == 0) modal.Add(Text("This scenario defines no compatible logistics facility for this Formation type.", "muted", "amber"));
            }
            else if (!backend.Game.NeedsReplenishment(active)) modal.Add(Text("Nothing currently requires restoration.", "muted"));
            else
            {
                IReadOnlyList<string> cards = backend.Game.RepairableDestructionCards(active);
                if (cards.Count == 0) modal.Add(ActionButton("BEGIN REPLENISHMENT • 3", () => { CloseOverlay(); ResolveAction(() => backend.ToolkitReplenish()); }, "primary"));
                else
                {
                    modal.Add(Text("SELECT ONE DESTRUCTION CARD TO REPAIR", "eyebrow"));
                    foreach (string cardId in cards)
                    {
                        string selected = cardId;
                        EntropyEffectDefinition effect = EntropyEffectCatalog.Find(selected);
                        modal.Add(ActionButton($"{selected} • {effect?.Title.ToUpperInvariant()}", () => { CloseOverlay(); ResolveAction(() => backend.ToolkitReplenish(selected)); }, "primary"));
                    }
                }
            }
            modal.Add(ActionButton("CANCEL", CloseOverlay));
        }

        private void ShowMissionFormationChoices()
        {
            Side side = backend.Game.Active.Side;
            VisualElement modal = Modal("STANDING MISSION • SELECT FORMATION");
            modal.Add(Text("A Standing Mission contains Task, Objective, Posture, and Trigger. Assigning or changing it occupies Command Attention; executing its Task does not.", "body-copy"));
            foreach (FormationState formation in backend.Game.Formations.Where(candidate => candidate.Side == side && !candidate.IsDestroyed).OrderBy(candidate => candidate.Id))
            {
                FormationState selected = formation;
                modal.Add(ActionButton($"{selected.Name.ToUpperInvariant()} • {selected.Mission} • READY T{selected.ReadyTime:00}", () => ShowMissionTasks(selected)));
            }
            modal.Add(ActionButton("CANCEL", CloseOverlay));
        }

        private void ShowMissionTasks(FormationState formation)
        {
            VisualElement modal = Modal("STANDING MISSION • SELECT TASK");
            modal.Add(Text($"{formation.Name.ToUpperInvariant()} currently executes {formation.Mission}. Select the action this Formation may perform without Command Attention.", "body-copy"));
            foreach (ActionKind task in Enum.GetValues(typeof(ActionKind)))
            {
                ActionKind selected = task;
                modal.Add(ActionButton(selected.ToString().ToUpperInvariant(), () => ShowMissionObjectives(formation, selected), selected == formation.Mission ? "selected" : null));
            }
            modal.Add(ActionButton("BACK", ShowMissionFormationChoices));
        }

        private void ShowMissionObjectives(FormationState formation, ActionKind task)
        {
            VisualElement modal = Modal("STANDING MISSION • SELECT OBJECTIVE");
            modal.Add(ActionButton($"CURRENT AREA • {formation.Position}", () => ShowMissionPostures(formation, task, MissionObjectiveKind.CurrentArea, null, formation.Position)));
            modal.Add(ActionButton($"OPERATIONAL OBJECTIVE • {backend.Game.Area.Objective}", () => ShowMissionPostures(formation, task, MissionObjectiveKind.OperationalObjective, null, backend.Game.Area.Objective)));
            foreach (FormationState friendly in backend.Game.Formations.Where(candidate => candidate.Side == formation.Side && candidate != formation && !candidate.IsDestroyed).OrderBy(candidate => candidate.Id))
            {
                FormationState selected = friendly;
                modal.Add(ActionButton($"FRIENDLY • {selected.Name.ToUpperInvariant()} • {selected.Position}", () => ShowMissionPostures(formation, task, MissionObjectiveKind.FriendlyFormation, selected.Id, selected.Position)));
            }
            foreach (ContactState contact in backend.Game.Contacts.Where(candidate => candidate.Owner == formation.Side && !candidate.IsLost).OrderBy(candidate => candidate.TargetId))
            {
                ContactState selected = contact;
                modal.Add(ActionButton($"CONTACT • {selected.Summary} • {selected.LastKnownPosition}", () => ShowMissionPostures(formation, task, MissionObjectiveKind.Contact, selected.TargetId, selected.LastKnownPosition)));
            }
            foreach (OperationalLocationDefinition facility in backend.Game.LogisticsFacilitiesFor(formation))
            {
                OperationalLocationDefinition selected = facility;
                modal.Add(ActionButton($"LOGISTICS • {selected.Name.ToUpperInvariant()} • {selected.Hex}", () => ShowMissionPostures(formation, task, MissionObjectiveKind.LogisticsFacility, selected.Id, selected.Hex)));
            }
            modal.Add(ActionButton("BACK", () => ShowMissionTasks(formation)));
        }

        private void ShowMissionPostures(FormationState formation, ActionKind task, MissionObjectiveKind objective, string objectiveId, HexCoord objectiveHex)
        {
            VisualElement modal = Modal("STANDING MISSION • SELECT POSTURE");
            foreach (MissionPosture posture in Enum.GetValues(typeof(MissionPosture)))
            {
                MissionPosture selected = posture;
                modal.Add(ActionButton(selected.ToString().ToUpperInvariant(), () => ShowMissionTriggers(formation, task, objective, objectiveId, objectiveHex, selected), selected == MissionPosture.Balanced ? "selected" : null));
            }
            modal.Add(ActionButton("BACK", () => ShowMissionObjectives(formation, task)));
        }

        private void ShowMissionTriggers(FormationState formation, ActionKind task, MissionObjectiveKind objective, string objectiveId, HexCoord objectiveHex, MissionPosture posture)
        {
            VisualElement modal = Modal("STANDING MISSION • SELECT TRIGGER");
            modal.Add(Text("On Ready keeps the assigned Task. Conditional triggers may automatically retask to Strike, Recover, Replenish, or Patrol when their public condition becomes true.", "body-copy"));
            foreach (MissionTrigger trigger in Enum.GetValues(typeof(MissionTrigger)))
            {
                MissionTrigger selected = trigger;
                modal.Add(ActionButton(selected.ToString().ToUpperInvariant(), () =>
                {
                    backend.ToolkitAssignStandingMission(formation, task, objective, objectiveId, objectiveHex, posture, selected);
                    if (backend.LastToolkitActionSucceeded) ShowGame(); else ShowMissionTriggers(formation, task, objective, objectiveId, objectiveHex, posture);
                }, selected == MissionTrigger.OnReady ? "selected" : null));
            }
            modal.Add(ActionButton("BACK", () => ShowMissionPostures(formation, task, objective, objectiveId, objectiveHex)));
        }

        private void ShowSynchronizedStrikeContacts()
        {
            FormationState leader = backend.Game.Active;
            VisualElement modal = Modal("SYNCHRONIZED STRIKE • SELECT CONTACT");
            modal.Add(Text("Reserve two to four formations now. The event executes at the latest participant Ready Time, no earlier than T+1, and holds one Command Slot.", "body-copy"));
            foreach (ContactState contact in backend.Game.Contacts.Where(item => item.Owner == leader.Side && !item.IsLost).OrderByDescending(item => item.Location).ThenBy(item => item.TargetId))
            {
                ContactState selected = contact;
                modal.Add(ActionButton($"{selected.Summary}  •  {selected.LastKnownPosition}", () => ShowSynchronizedStrikeAims(selected)));
            }
            modal.Add(ActionButton("CANCEL", CloseOverlay));
        }

        private void ShowSynchronizedStrikeAims(ContactState contact)
        {
            VisualElement modal = Modal("SYNCHRONIZED STRIKE • SELECT AIM");
            modal.Add(Text("Every selected participant must be in range of this aim at declaration and resolution.", "body-copy"));
            foreach (HexCoord aim in backend.Game.ContactPossibleHexes(contact).OrderBy(hex => HexCoord.Distance(hex, contact.LastKnownPosition)).ThenBy(hex => hex.Q).ThenBy(hex => hex.R))
            {
                HexCoord selected = aim;
                int eligible = backend.Game.EligibleSynchronizedStrikeParticipants(backend.Game.Active.Side, contact, selected, salvo).Count;
                Button button = ActionButton($"HEX {selected}  •  {eligible} ELIGIBLE AT {salvo.ToString().ToUpperInvariant()}", () => ShowSynchronizedStrikeParticipants(contact, selected));
                button.SetEnabled(eligible >= 2 && backend.Game.EligibleSynchronizedStrikeParticipants(backend.Game.Active.Side, contact, selected, salvo).Contains(backend.Game.Active));
                modal.Add(button);
            }
            modal.Add(ActionButton("BACK", ShowSynchronizedStrikeContacts));
        }

        private void ShowSynchronizedStrikeParticipants(ContactState contact, HexCoord aim)
        {
            FormationState leader = backend.Game.Active;
            List<FormationState> eligible = backend.Game.EligibleSynchronizedStrikeParticipants(leader.Side, contact, aim, salvo).ToList();
            VisualElement modal = Modal("SYNCHRONIZED STRIKE • PARTICIPANTS");
            modal.Add(Text($"Leader {leader.Name}. Choose a package; all participants are reserved with no intervening actions. {salvo} is used by every participant.", "body-copy"));
            bool hasC02 = backend.ToolkitResponseHand(leader.Side).Any(card => card.Id == "C-02");
            bool hasC10 = backend.ToolkitResponseHand(leader.Side).Any(card => card.Id == "C-10");
            foreach (List<FormationState> package in SynchronizedPackages(leader, eligible))
            {
                List<FormationState> selected = package;
                string names = string.Join(" + ", selected.Select(item => item.Name.ToUpperInvariant()));
                modal.Add(ActionButton(names, () => CommitSynchronizedStrike(selected, contact, aim, false, null), "primary"));
                if (hasC02) modal.Add(ActionButton(names + "  •  C-02 PRIOR PLANNING", () => CommitSynchronizedStrike(selected, contact, aim, true, null)));
                if (hasC10)
                {
                    FormationState deconflicted = selected.Last();
                    modal.Add(ActionButton(names + $"  •  C-10 FOR {deconflicted.Name.ToUpperInvariant()}", () => CommitSynchronizedStrike(selected, contact, aim, false, deconflicted.Id)));
                    if (hasC02) modal.Add(ActionButton(names + $"  •  C-02 + C-10 FOR {deconflicted.Name.ToUpperInvariant()}", () => CommitSynchronizedStrike(selected, contact, aim, true, deconflicted.Id)));
                }
            }
            modal.Add(ActionButton("BACK", () => ShowSynchronizedStrikeAims(contact)));
        }

        private static IEnumerable<List<FormationState>> SynchronizedPackages(FormationState leader, List<FormationState> eligible)
        {
            List<FormationState> others = eligible.Where(item => item != leader).OrderBy(item => item.Id).ToList();
            int combinations = 1 << others.Count;
            for (int mask = 1; mask < combinations; mask++)
            {
                var package = new List<FormationState> { leader };
                for (int index = 0; index < others.Count; index++) if ((mask & (1 << index)) != 0) package.Add(others[index]);
                if (package.Count <= 4) yield return package;
            }
        }

        private void CommitSynchronizedStrike(List<FormationState> participants, ContactState contact, HexCoord aim, bool priorPlanning, string deconflictedFormationId)
        {
            CloseOverlay();
            ResolveAction(() => backend.ToolkitDeclareSynchronizedStrike(participants, contact, aim, salvo, priorPlanning, deconflictedFormationId));
        }

        private void BeginSynchronizedStrikeResolution(SynchronizedStrikeState strike, bool continueBlind)
        {
            pendingSynchronizedStrike = strike;
            pendingSynchronizedBlind = continueBlind;
            ContactState contact = backend.Game.Contacts.FirstOrDefault(item => item.Owner == strike.Side && item.TargetId == strike.ContactTargetId);
            FormationState target = contact == null || contact.IsFalse ? null : backend.Game.Find(contact.TargetId);
            if (target != null && !target.IsDestroyed && target.Position.Equals(strike.Aim))
            {
                Salvo firstSalvo = strike.Participants.Count == 0 ? Salvo.Standard : strike.Participants[0].Salvo;
                BeginStrikeReaction(target, firstSalvo);
            }
            else ResolveAction(() => backend.ToolkitResolveSynchronizedStrike(strike, Reaction.None, null, continueBlind));
        }

        private void ShowSynchronizedRetaskContacts(SynchronizedStrikeState strike)
        {
            VisualElement modal = Modal("SYNCHRONIZED STRIKE • RETASK CONTACT");
            foreach (ContactState contact in backend.Game.Contacts.Where(item => item.Owner == strike.Side && !item.IsLost).OrderByDescending(item => item.Location).ThenBy(item => item.TargetId))
            {
                ContactState selected = contact;
                modal.Add(ActionButton($"{selected.Summary}  •  {selected.LastKnownPosition}", () => ShowSynchronizedRetaskAims(strike, selected)));
            }
            modal.Add(ActionButton("CANCEL", CloseOverlay));
        }

        private void ShowSynchronizedRetaskAims(SynchronizedStrikeState strike, ContactState contact)
        {
            VisualElement modal = Modal("SYNCHRONIZED STRIKE • RETASK AIM");
            foreach (HexCoord aim in backend.Game.ContactPossibleHexes(contact).OrderBy(hex => HexCoord.Distance(hex, contact.LastKnownPosition)).ThenBy(hex => hex.Q).ThenBy(hex => hex.R))
            {
                HexCoord selected = aim;
                modal.Add(ActionButton("HEX " + selected, () =>
                {
                    backend.ToolkitRetaskSynchronizedStrike(strike, contact, selected);
                    if (backend.LastToolkitActionSucceeded) ShowGame(); else ShowSynchronizedRetaskAims(strike, contact);
                }));
            }
            modal.Add(ActionButton("BACK", () => ShowSynchronizedRetaskContacts(strike)));
        }

        private void SelectAction(ToolkitActionMode mode)
        {
            actionMode = actionMode == mode ? ToolkitActionMode.None : mode;
            backend.ToolkitRecordChoice(mode.ToString(), mode == ToolkitActionMode.Move ? moveMode.ToString() : mode == ToolkitActionMode.Search ? searchMode.ToString() : salvo.ToString());
            ShowGame();
        }

        private void ShowFormationOrders(Vector2 mapPosition)
        {
            FormationState active = backend.Game.Active;
            if (active == null) return;
            VisualElement menu = ContextPanel("ACTIVE FORMATION ORDERS", mapPosition);
            VisualElement identity = El("context-identity");
            identity.Add(Text(active.Name.ToUpperInvariant(), "context-formation-name"));
            identity.Add(Text($"{KindTag(active.Kind)}  •  HEX {active.Position}  •  READY T{active.ReadyTime:00}", "muted"));
            VisualElement chips = El("row", "context-chip-row");
            chips.Add(Text(active.Mission.ToString().ToUpperInvariant(), "context-chip", "mission"));
            chips.Add(Text(active.Endurance.ToString().ToUpperInvariant(), "context-chip", "endurance"));
            chips.Add(Text(active.Cohesion.ToUpperInvariant(), "context-chip", "cohesion"));
            identity.Add(chips);
            menu.Add(identity);
            menu.Add(Text("PRIMARY ORDERS", "eyebrow", "context-section"));
            menu.Add(ActionButton("⇢  MOVE ORDERS  ›", () => ShowMoveOrders(mapPosition), actionMode == ToolkitActionMode.Move ? "selected" : null));
            menu.Add(ActionButton("◎  SEARCH ORDERS  ›", () => ShowSearchOrders(mapPosition), actionMode == ToolkitActionMode.Search ? "selected" : null));
            menu.Add(ActionButton("✦  STRIKE ORDERS  ›", () => ShowStrikeOrders(mapPosition), actionMode == ToolkitActionMode.Strike ? "selected" : null));
            menu.Add(Text("IMMEDIATE ACTION", "eyebrow", "context-section"));
            VisualElement immediate = El("row", "context-order-row");
            immediate.Add(ActionButton("↻  RECOVER • 2", () => ResolveAction(() => backend.ToolkitRecover())));
            immediate.Add(ActionButton("■  HOLD • 1", () => ResolveAction(() => backend.ToolkitHold())));
            menu.Add(immediate);
            menu.Add(Text("ASSIGNMENTS & COMMAND", "eyebrow", "context-section"));
            VisualElement assignments = El("row", "context-order-row");
            assignments.Add(ActionButton("◉  SCREEN", ShowPatrolSetup));
            assignments.Add(ActionButton("+  SUPPORT", ShowSupportSetup));
            menu.Add(assignments);
            menu.Add(ActionButton("⟳  REPLENISHMENT  ›", () => { actionMode = ToolkitActionMode.Replenish; ShowReplenishmentSetup(); }));
            menu.Add(ActionButton("✦✦  SYNCHRONIZED STRIKE  ›", ShowSynchronizedStrikeContacts));
            menu.Add(ActionButton("MISSION ORDERS  ›", ShowMissionFormationChoices));
            menu.Add(ActionButton("▤  OPEN CARD TABLE  •  H", ShowResponseHand, "context-utility"));
        }

        private void ShowMoveOrders(Vector2 mapPosition)
        {
            FormationState active = backend.Game.Active;
            VisualElement menu = ContextPanel("MOVE ORDERS", mapPosition);
            menu.Add(Text("Choose movement posture, then select a highlighted destination directly on the 3D map.", "muted"));
            foreach (MoveMode option in Enum.GetValues(typeof(MoveMode)))
            {
                MoveMode selected = option;
                int distance = Rules.MoveAllowance(active, selected);
                menu.Add(ActionButton($"{selected.ToString().ToUpperInvariant()}  •  {distance} HEX / {distance * backend.Game.Area.NauticalMilesPerHex} NM", () => BeginMapOrder(ToolkitActionMode.Move, selected, searchMode, searchPriority, salvo), selected == moveMode ? "selected" : null));
            }
            menu.Add(ActionButton("‹  BACK TO ORDERS", () => ShowFormationOrders(mapPosition)));
        }

        private void ShowSearchOrders(Vector2 mapPosition)
        {
            FormationState active = backend.Game.Active;
            VisualElement menu = ContextPanel("SEARCH ORDERS", mapPosition);
            menu.Add(Text("Choose a sensor posture and intelligence priority, then select the search area or Contact on the 3D map.", "muted"));
            foreach (SearchMode modeOption in Enum.GetValues(typeof(SearchMode)))
            {
                foreach (SearchPriority priorityOption in Enum.GetValues(typeof(SearchPriority)))
                {
                    SearchMode selectedMode = modeOption;
                    SearchPriority selectedPriority = priorityOption;
                    int range = backend.Game.SearchRangeFor(active, selectedMode) * backend.Game.Area.NauticalMilesPerHex;
                    menu.Add(ActionButton($"{selectedMode.ToString().ToUpperInvariant()}  •  {selectedPriority.ToString().ToUpperInvariant()}  •  {range} NM", () => BeginMapOrder(ToolkitActionMode.Search, moveMode, selectedMode, selectedPriority, salvo), selectedMode == searchMode && selectedPriority == searchPriority ? "selected" : null));
                }
            }
            menu.Add(ActionButton("‹  BACK TO ORDERS", () => ShowFormationOrders(mapPosition)));
        }

        private void ShowStrikeOrders(Vector2 mapPosition)
        {
            FormationState active = backend.Game.Active;
            VisualElement menu = ContextPanel("STRIKE ORDERS", mapPosition);
            menu.Add(Text("Choose the salvo, then select a Contact on the 3D map and confirm its aim hex.", "muted"));
            foreach (Salvo option in Enum.GetValues(typeof(Salvo)))
            {
                Salvo selected = option;
                int range = Rules.StrikeRange(active.Kind, selected) * backend.Game.Area.NauticalMilesPerHex;
                int remaining = active.Weapons?.Available(selected) ?? -1;
                Button order = ActionButton($"{selected.ToString().ToUpperInvariant()}  •  {range} NM  •  {(remaining < 0 ? "AVAILABLE" : remaining + " LEFT")}", () => BeginMapOrder(ToolkitActionMode.Strike, moveMode, searchMode, searchPriority, selected), selected == salvo ? "selected" : null);
                order.SetEnabled(active.CanFire(selected));
                menu.Add(order);
            }
            menu.Add(ActionButton("‹  BACK TO ORDERS", () => ShowFormationOrders(mapPosition)));
        }

        private void BeginMapOrder(ToolkitActionMode mode, MoveMode selectedMove, SearchMode selectedSearch, SearchPriority selectedPriority, Salvo selectedSalvo)
        {
            actionMode = mode;
            moveMode = selectedMove;
            searchMode = selectedSearch;
            searchPriority = selectedPriority;
            salvo = selectedSalvo;
            string detail = mode == ToolkitActionMode.Move ? moveMode.ToString() : mode == ToolkitActionMode.Search ? searchMode + "/" + searchPriority : salvo.ToString();
            backend.ToolkitRecordChoice(mode.ToString(), detail);
            ShowGame();
        }

        private VisualElement ContextPanel(string title, Vector2 mapPosition)
        {
            VisualElement previousFocus = root?.panel?.focusController?.focusedElement as VisualElement;
            CloseOverlay();
            focusBeforeOverlay = previousFocus ?? map;
            VisualElement shade = El("context-shade");
            shade.name = "overlay";
            shade.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.target == shade) CloseOverlay();
            });
            VisualElement menu = Panel("orders-context");
            menu.name = "dialog-" + AccessibleId(title);
            Vector2 rootPosition = map != null ? map.ChangeCoordinatesTo(root, mapPosition) : mapPosition;
            const float menuWidth = 408f;
            const float formationClearance = 82f;
            float rightCandidate = rootPosition.x + formationClearance;
            float leftCandidate = rootPosition.x - formationClearance - menuWidth;
            bool openLeft = rootPosition.x > root.resolvedStyle.width * .56f || rightCandidate + menuWidth > root.resolvedStyle.width - 12f;
            float maxLeft = Mathf.Max(12f, root.resolvedStyle.width - menuWidth - 12f);
            float maxTop = Mathf.Max(12f, root.resolvedStyle.height - 704f);
            menu.style.left = Mathf.Clamp(openLeft ? leftCandidate : rightCandidate, 12f, maxLeft);
            menu.style.top = Mathf.Clamp(rootPosition.y - 150f, 12f, maxTop);
            VisualElement titleBar = El("row", "context-titlebar");
            titleBar.Add(Text(title, "eyebrow"));
            titleBar.Add(El("spacer"));
            titleBar.Add(ActionButton("×", CloseOverlay, "context-close"));
            menu.Add(titleBar);
            menu.Add(El("context-accent-rule"));
            ScrollView scroll = new ScrollView();
            scroll.AddToClassList("context-scroll");
            scroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            menu.Add(scroll);
            shade.Add(menu);
            root.Add(shade);
            menu.schedule.Execute(() => menu.Query<Button>().First()?.Focus());
            return scroll.contentContainer;
        }

        private Button ModeButton(string text, MoveMode mode) => ActionButton(text, () => { moveMode = mode; backend.ToolkitRecordChoice("Move", mode.ToString()); ShowGame(); }, moveMode == mode ? "selected" : null);
        private Button ModeButton(string text, SearchMode mode) => ActionButton(text, () => { searchMode = mode; backend.ToolkitRecordChoice("Search", mode.ToString()); ShowGame(); }, searchMode == mode ? "selected" : null);
        private Button ModeButton(string text, SearchPriority priority) => ActionButton(text, () => { searchPriority = priority; backend.ToolkitRecordChoice("Search priority", priority.ToString()); ShowGame(); }, searchPriority == priority ? "selected" : null);
        private Button ModeButton(string text, Salvo mode) => ActionButton(text, () => { salvo = mode; backend.ToolkitRecordChoice("Strike", mode.ToString()); ShowGame(); }, salvo == mode ? "selected" : null);

        private void OnHexChosen(HexCoord hex)
        {
            if (actionMode == ToolkitActionMode.Move) ResolveAction(() => backend.ToolkitMove(hex, moveMode));
            else if (actionMode == ToolkitActionMode.Search) ResolveAction(() => backend.ToolkitSearchHex(hex, searchMode, searchPriority));
        }

        private void OnContactChosen(ContactState contact)
        {
            FormationState target = backend.Game.Find(contact.TargetId);
            if (actionMode == ToolkitActionMode.Search)
            {
                if (target == null) ResolveAction(() => backend.ToolkitSearchHex(contact.LastKnownPosition, searchMode, searchPriority));
                else ResolveAction(() => backend.ToolkitSearch(target, searchMode, searchPriority));
            }
            else if (actionMode == ToolkitActionMode.Strike)
            {
                ShowStrikeAim(contact);
            }
        }

        private void ShowStrikeAim(ContactState contact)
        {
            int range = Rules.StrikeRange(backend.Game.Active.Kind, salvo);
            List<HexCoord> aims = backend.Game.ContactPossibleHexes(contact)
                .Where(hex => HexCoord.Distance(backend.Game.Active.Position, hex) <= range)
                .OrderBy(hex => HexCoord.Distance(hex, contact.LastKnownPosition)).ThenBy(hex => hex.Q).ThenBy(hex => hex.R).ToList();
            VisualElement modal = Modal("SELECT STRIKE AIM HEX");
            modal.Add(Text($"{contact.Summary}\nChoose one hex inside the Contact's possible area. Occupancy is tested only after commitment; a false or stale fix reports no confirmed effect.", "body-copy"));
            ScrollView choices = new ScrollView();
            choices.AddToClassList("scroll");
            foreach (HexCoord hex in aims)
            {
                HexCoord selected = hex;
                choices.Add(ActionButton($"HEX {selected}  •  RANGE {HexCoord.Distance(backend.Game.Active.Position, selected)} / {range}", () => CommitStrikeAim(contact, selected)));
            }
            modal.Add(choices);
            modal.Add(ActionButton("CANCEL", CloseOverlay));
        }

        private void CommitStrikeAim(ContactState contact, HexCoord aim)
        {
            pendingStrikeContact = contact;
            pendingStrikeAim = aim;
            if (salvo == Salvo.Heavy) ConfirmHeavyContact();
            else ContinueCommittedStrike();
        }

        private void ContinueCommittedStrike()
        {
            CloseOverlay();
            FormationState target = pendingStrikeContact == null || pendingStrikeContact.IsFalse ? null : backend.Game.Find(pendingStrikeContact.TargetId);
            if (target != null && !target.IsDestroyed && target.Position.Equals(pendingStrikeAim)) BeginStrikeReaction(target, salvo);
            else
            {
                ContactState contact = pendingStrikeContact;
                HexCoord aim = pendingStrikeAim;
                pendingStrikeContact = null;
                ResolveAction(() => backend.ToolkitStrikeContact(contact, aim, salvo));
            }
        }

        private void BeginStrikeReaction(FormationState target, Salvo committedSalvo, AiDecision aiDecision = null)
        {
            pendingReactionTarget = target;
            pendingReactionSalvo = committedSalvo;
            pendingAiStrike = aiDecision;
            IReadOnlyList<Reaction> legal = backend.Game.AvailableReactions(backend.Game.Active, target);
            if (legal.Count == 1 && legal[0] == Reaction.None)
            {
                ResolvePendingStrike(Reaction.None, null);
                return;
            }

            ReactionControl control = PrototypeGame.ReactionController(backend.SelectedMode, backend.HumanSide, target.Side);
            if (control == ReactionControl.Ai)
            {
                PrototypeAiCommander.TryPrepareReactionResponse(backend.Game, target, out _);
                Reaction aiReaction = PrototypeAiCommander.ChooseReaction(backend.Game, backend.Game.Active, target);
                HexCoord? aiDestination = aiReaction == Reaction.Evade ? PrototypeAiCommander.ChooseEvadeDestination(backend.Game, backend.Game.Active, target) : null;
                ResolvePendingStrike(aiReaction, aiDestination);
                return;
            }

            if (control == ReactionControl.HumanHandoff) ShowReactionHandoff();
            else ShowReactionChoice();
        }

        private void ShowReactionHandoff()
        {
            BeginScreen("SECURE REACTION HANDOFF");
            VisualElement page = El("front-page");
            VisualElement card = Panel("hero-card", "center");
            card.Add(Text(pendingReactionTarget.Side.ToString().ToUpperInvariant() + " DEFENDER", "front-title"));
            card.Add(Text("Pass control to the defending player. The attack is committed; choose the Formation's Reaction before combat is rolled.", "body-copy"));
            card.Add(ActionButton("ASSUME DEFENSIVE CONTROL", ShowReactionChoice, "primary"));
            page.Add(card);
            app.Add(page);
        }

        private void ShowReactionChoice()
        {
            FormationState attacker = backend.Game.Active;
            FormationState defender = pendingReactionTarget;
            ContactState attackerContact = backend.Game.ContactFor(defender.Side, attacker.Id);
            string attackerLabel = attackerContact?.Identity == IdentityQuality.Identified ? attacker.Name : attackerContact == null ? "UNLOCATED STRIKE ORIGIN" : $"HOSTILE CONTACT AT {attackerContact.LastKnownPosition}";
            BeginScreen($"REACTION  •  {defender.Side.ToString().ToUpperInvariant()} DEFENSE");
            VisualElement page = El("front-page");
            VisualElement card = Panel("hero-card");
            card.Add(Text("INCOMING " + pendingReactionSalvo.ToString().ToUpperInvariant() + " STRIKE", "eyebrow"));
            card.Add(Text(defender.Name.ToUpperInvariant(), "front-title"));
            card.Add(Text($"Attacker: {attackerLabel}\nDefender Reaction is available and does not change Ready Time. The choice is consumed until this Formation completes its own Action.", "body-copy"));
            IReadOnlyList<Reaction> legal = backend.Game.AvailableReactions(attacker, defender);
            IReadOnlyList<CommandResponseDefinition> responseHand = backend.ToolkitResponseHand(defender.Side);
            if (responseHand.Any(response => response.Id == "C-16") && defender.ReactionDefenseBonus == 0)
                card.Add(ActionButton("PLAY C-16 COVERING FIRES  •  +1 DEFENSE", () => { backend.ToolkitPlayResponseForSide(defender.Side, "C-16", defender); ShowReactionChoice(); }, "warning"));
            if (responseHand.Any(response => response.Id == "C-18") && !defender.OrderlyWithdrawalReady)
                card.Add(ActionButton("PLAY C-18 ORDERLY WITHDRAWAL  •  EVADE 2", () => { backend.ToolkitPlayResponseForSide(defender.Side, "C-18", defender); ShowReactionChoice(); }, "warning"));
            if (legal.Contains(Reaction.Defend)) card.Add(ActionButton("DEFEND  •  +1 DEFENSE", () => ResolvePendingStrike(Reaction.Defend, null), "primary"));
            if (legal.Contains(Reaction.Evade)) card.Add(ActionButton($"EVADE  •  +1 DEFENSE  •  MOVE {(defender.OrderlyWithdrawalReady ? 2 : 1)} AFTER", ShowEvadeDestinations));
            if (legal.Contains(Reaction.Counterattack)) card.Add(ActionButton("COUNTERATTACK  •  LIGHT STRIKE AFTER  •  NO DEFENSE BONUS", () => ResolvePendingStrike(Reaction.Counterattack, null), "warning"));
            if (legal.Contains(Reaction.Hold)) card.Add(ActionButton("HOLD  •  PRESERVE POSITION  •  NO MODIFIER", () => ResolvePendingStrike(Reaction.Hold, null)));
            page.Add(card);
            app.Add(page);
        }

        private void ShowEvadeDestinations()
        {
            FormationState attacker = backend.Game.Active;
            FormationState defender = pendingReactionTarget;
            ContactState attackerContact = backend.Game.ContactFor(defender.Side, attacker.Id);
            string attackerLabel = attackerContact?.Identity == IdentityQuality.Identified ? attacker.Name : attackerContact == null ? "the unknown strike origin" : "the last-known hostile position";
            int allowance = defender.OrderlyWithdrawalReady ? 2 : 1;
            IReadOnlyList<HexCoord> destinations = backend.Game.LegalEvadeDestinations(attacker, defender, allowance);
            if (destinations.Count == 0) { ResolvePendingStrike(Reaction.Evade, null); return; }
            BeginScreen("SELECT EVADE DESTINATION");
            VisualElement page = El("front-page");
            VisualElement card = Panel("hero-card");
            card.Add(Text(defender.Name.ToUpperInvariant(), "front-title"));
            card.Add(Text($"Choose a valid hex farther from {attackerLabel}. This movement occurs after combat and does not change Ready Time.", "body-copy"));
            ScrollView choices = new ScrollView();
            choices.AddToClassList("scroll");
            foreach (HexCoord destination in destinations)
            {
                HexCoord selected = destination;
                int movement = HexCoord.Distance(defender.Position, selected);
                int separation = HexCoord.Distance(attacker.Position, selected);
                choices.Add(ActionButton($"HEX {selected}  •  MOVE {movement}  •  SEPARATION {separation}", () => ResolvePendingStrike(Reaction.Evade, selected)));
            }
            card.Add(choices);
            card.Add(ActionButton("BACK TO REACTIONS", ShowReactionChoice));
            page.Add(card);
            app.Add(page);
        }

        private void ResolvePendingStrike(Reaction reaction, HexCoord? evadeDestination)
        {
            FormationState target = pendingReactionTarget;
            AiDecision aiDecision = pendingAiStrike;
            SynchronizedStrikeState synchronizedStrike = pendingSynchronizedStrike;
            bool continuedBlind = pendingSynchronizedBlind;
            ContactState contact = pendingStrikeContact;
            HexCoord aim = pendingStrikeAim;
            pendingReactionTarget = null;
            pendingAiStrike = null;
            pendingStrikeContact = null;
            pendingSynchronizedStrike = null;
            pendingSynchronizedBlind = false;
            if (synchronizedStrike != null)
            {
                ResolveAction(() => backend.ToolkitResolveSynchronizedStrike(synchronizedStrike, reaction, evadeDestination, continuedBlind));
                return;
            }
            if (aiDecision == null)
            {
                if (contact != null) ResolveAction(() => backend.ToolkitStrikeContact(contact, aim, pendingReactionSalvo, reaction, evadeDestination));
                else ResolveAction(() => backend.ToolkitStrike(target, pendingReactionSalvo, reaction, evadeDestination));
                return;
            }

            backend.ToolkitExecuteAiTurn(aiDecision, reaction, evadeDestination);
            if (!backend.LastToolkitActionSucceeded) { ShowAiPlanning(true); return; }
            if (backend.IsComplete) { ShowResults(); return; }
            if (backend.IsAiTurn) BeginAiSequence();
            else { backend.ToolkitBeginDecision(); ShowGame(); }
        }

        private void ResolveAction(Func<string> action)
        {
            Side actingSide = backend.Game.Active.Side;
            string message = action();
            if (backend.IsComplete) { ShowResults(); return; }
            if (!backend.LastToolkitActionSucceeded) { ShowGame(); return; }
            actionMode = ToolkitActionMode.None;
            if (backend.ToolkitPendingEntropyEffect(actingSide) != null)
            {
                ShowEntropyPull(actingSide, () => ContinueAfterResolvedAction(actingSide));
                return;
            }
            ContinueAfterResolvedAction(actingSide);
        }

        private void ContinueAfterResolvedAction(Side actingSide)
        {
            if (backend.SelectedMode == OperationMode.SoloVsAi)
            {
                if (backend.IsAiTurn) BeginAiSequence();
                else { backend.ToolkitBeginDecision(); ShowGame(); }
            }
            else if (backend.Game.Active.Side != actingSide) ShowHandoff();
            else { backend.ToolkitBeginDecision(); ShowGame(); }
        }

        private string ActionPreview()
        {
            if (actionMode == ToolkitActionMode.None) return "SELECT AN ACTION • Keyboard shortcuts: 1–8.";
            if (actionMode == ToolkitActionMode.Replenish) return $"REPLENISH AT A COMPATIBLE LOGISTICS FACILITY\nTIME 3{(backend.Game.Active.HasEffect("X-10") ? " +1 HULL BREACH" : string.Empty)} • {backend.Game.ReplenishmentPreview(backend.Game.Active)}";
            if (actionMode == ToolkitActionMode.None) return "SELECT AN ACTION  •  Keyboard shortcuts: 1–5.";
            FormationState active = backend.Game.Active;
            int friction = active.Friction || actionMode == ToolkitActionMode.Move && moveMode == MoveMode.HighTempo ? 1 : 0;
            string consequence = actionMode == ToolkitActionMode.Move ? (moveMode == MoveMode.Cautious ? "Signature −1" : moveMode == MoveMode.HighTempo ? "Signature +1 • mark Friction" : "balanced movement") : actionMode == ToolkitActionMode.Search ? (searchMode == SearchMode.Active ? "become Loud" : searchMode == SearchMode.Focused ? "temporarily occupy 1 Command" : "remain quiet") : salvo == Salvo.Heavy ? "expend Heavy capability" : $"Attack {Rules.SalvoModifier(salvo):+0;-0;0}";
            if (actionMode == ToolkitActionMode.Search) consequence += $"; improve {searchPriority} first; one step maximum";
            ActionKind previewAction = actionMode == ToolkitActionMode.Move ? ActionKind.Move : actionMode == ToolkitActionMode.Search ? ActionKind.Search : ActionKind.Strike;
            ActionKind? triggeredTask = backend.Game.TriggeredMissionFor(active);
            consequence += backend.Game.ActionFollowsMission(active, previewAction) ? " • follows Standing Mission • no Command Attention" : triggeredTask == previewAction ? " • Trigger authorized • no Command Attention" : " • immediate retask • occupy 1 Command Slot through resolution";
            if (active.EntropySources >= 3 && !active.PushThroughReady) consequence += " • PUSH THROUGH REQUIRED";
            string instruction = actionMode == ToolkitActionMode.Move ? "CLICK A GREEN DESTINATION HEX" : actionMode == ToolkitActionMode.Search ? "CLICK ANY HIGHLIGHTED HEX TO SEARCH AN AREA" : "CLICK AN ELIGIBLE CONTACT TO STRIKE";
            int nextMajor = Math.Min(3, active.MajorActions + 1);
            Endurance nextEndurance = nextMajor == 3 && active.Endurance < Endurance.Critical ? (Endurance)((int)active.Endurance + 1) : active.Endurance;
            return $"{instruction}\nTIME 2{(friction > 0 ? " +1 Friction" : string.Empty)} → NEXT READY T{backend.Game.Time + 2 + friction:00} • {consequence}\nENDURANCE {active.Endurance.ToString().ToUpperInvariant()} • MAJOR ACTION {nextMajor}/3{(nextMajor == 3 ? $" • AFTER ACTION {nextEndurance.ToString().ToUpperInvariant()}" : string.Empty)}";
        }

        private void ShowPause()
        {
            VisualElement modal = Modal("OPERATION PAUSED");
            modal.Add(ActionButton("RESUME", ShowGame, "primary"));
            modal.Add(ActionButton("SAVE OPERATION", () => { backend.ToolkitSave(); ShowGame(); }));
            if (backend.HasSave) modal.Add(ActionButton("LOAD SAVED OPERATION", LoadOperationAndContinue));
            modal.Add(ActionButton("EXPORT PLAYTEST DATA", () => { backend.ToolkitExport(); ShowGame(); }));
            modal.Add(ActionButton("SETTINGS", ShowSettings));
            modal.Add(ActionButton("RESTART OPERATION", () => { backend.ToolkitNewScenario(); OperationalMap3D.ResetPresentationMemory(); ShowBriefing(); }, "warning"));
            modal.Add(ActionButton("RETURN TO MAIN MENU", ShowMain, "danger-button"));
            modal.Add(ActionButton("EXIT GAME", ShowExitConfirmation, "danger-button"));
        }

        private void ShowExitConfirmation()
        {
            VisualElement modal = Modal("EXIT GAME?");
            modal.Add(Text("Exit to the desktop? Any progress since your last save will be lost.", "body-copy"));
            modal.Add(ActionButton("EXIT TO DESKTOP", ExitGame, "danger-button"));
            modal.Add(ActionButton("CANCEL", CloseOverlay, "primary"));
        }

        private static void ExitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void ShowSettings()
        {
            VisualElement modal = Modal("SETTINGS");
            ScrollView settings = new ScrollView();
            settings.AddToClassList("settings-scroll");
            Toggle age = new Toggle("Age-2 Contacts receive −1 targeting") { value = backend.AgeTwoPenalty };
            Slider master = new Slider("Master volume", 0f, 100f) { value = backend.MasterVolume * 100f, showInputField = true };
            Slider effects = new Slider("Effects volume", 0f, 100f) { value = backend.EffectsVolume * 100f, showInputField = true };
            Slider ambience = new Slider("Music / ambient volume", 0f, 100f) { value = backend.AmbientVolume * 100f, showInputField = true };
            Toggle descriptions = new Toggle("Show text descriptions for audio cues") { value = backend.AudioDescriptions };
            Toggle contrast = new Toggle("High-contrast side colors") { value = backend.HighContrast };
            Toggle motion = new Toggle("Reduced motion") { value = backend.ReducedMotion };
            Toggle grid = new Toggle("Show hex grid on the map") { value = backend.HexGridVisible };
            var textSizes = new List<string> { "100%", "125%", "150%" };
            int textSizeIndex = backend.TextScale >= 1.4f ? 2 : backend.TextScale >= 1.15f ? 1 : 0;
            DropdownField textScale = new DropdownField("Text size", textSizes, textSizeIndex);
            Toggle controller = new Toggle("Controller navigation") { value = backend.ControllerNavigation };
            Slider deadZone = new Slider("Controller stick dead zone", 30f, 90f) { value = backend.ControllerDeadZone * 100f, showInputField = true };
            settings.Add(Text("GAMEPLAY", "eyebrow")); settings.Add(age);
            settings.Add(Text("AUDIO", "eyebrow")); settings.Add(master); settings.Add(effects); settings.Add(ambience); settings.Add(descriptions);
            settings.Add(Text("DISPLAY & ACCESSIBILITY", "eyebrow")); settings.Add(contrast); settings.Add(motion); settings.Add(grid); settings.Add(textScale); settings.Add(controller); settings.Add(deadZone);
            settings.Add(Text("Audio descriptions identify each operational cue. Controller sticks must return below the dead zone before another focus step, preventing accidental repeated movement.", "muted"));
            settings.Add(Text($"CURRENT {Screen.width} × {Screen.height}  •  DESKTOP {Display.main.systemWidth} × {Display.main.systemHeight}", "muted"));
            settings.Add(ActionButton(Screen.fullScreen ? "SWITCH TO WINDOWED" : "SWITCH TO NATIVE FULLSCREEN", ToggleFullscreen));
            settings.Add(ActionButton("APPLY", () =>
            {
                float selectedTextScale = textScale.index == 2 ? 1.5f : textScale.index == 1 ? 1.25f : 1f;
                backend.ToolkitSetSettings(age.value, master.value / 100f, effects.value / 100f, ambience.value / 100f, descriptions.value, contrast.value, motion.value, grid.value, selectedTextScale, controller.value, deadZone.value / 100f);
                CloseOverlay();
                if (view == View.Game) ShowGame(); else ApplyAccessibilityClasses();
            }, "primary"));
            settings.Add(ActionButton("CANCEL", CloseOverlay));
            modal.Add(settings);
        }

        private void ShowRules()
        {
            VisualElement modal = Modal("FIELD MANUAL");
            modal.Add(Text("COMMAND MODES\nSolo vs AI assigns Blue to the player and Red to the deterministic OPFOR commander. Local Hotseat uses secure handoffs between two players. Both modes use identical rules and Ready-Time sequencing.\n\nSCALE\n20 nautical miles per hex. One Ready-Time is two hours.\n\nMAP CAMERA\nWASD moves with smooth acceleration; Q/E rotates; R/F tilts; Z/X zooms; Shift accelerates; Home resets. C focuses the active formation, O focuses the objective, V toggles optional edge scrolling, and F9/F10 save/recall a command view. Middle-drag pans, right-drag freely orbits, and the wheel zooms.\n\nMAP GRID\nUse HEXES OFF / HEXES ON in the command bar, or press G, to toggle the persistent hex overlay. The preference is saved between sessions.\n\nREADY TIME\nThere are no player turns. The earliest formation acts. On a cross-side tie, priority passes away from the side that acted most recently; within that side use lower Entropy, higher Command, then stable formation order. If only one side is Ready, it continues.\n\nSTANDING MISSIONS\nEach Formation tracks a current Mission task. Rapid Replan changes that task without occupying Command and adds +1 Time when the Formation next schedules Ready.\n\nMOVE\nSurface/carrier/sub: Cautious 20 / Normal 40 / High Tempo 60 nm. Air missions: 80 / 120 / 160 nm. Cautious gives Signature -1; High Tempo makes the Formation Loud; either lasts until that Formation completes its next Action. Routes cannot cross Land or restricted areas, and a naval Formation entering a Strait must stop. Littoral anywhere on the route adds one Ready-Time. One friendly Formation may end in a hex; opposing Formations may coexist without revealing hidden positions.\n\nCONTROL\nA combat-capable non-air Formation controls an objective within one hex unless the opponent has a qualifying Formation there too. Control is adjudicated from actual positions only for final scoring; the live map does not reveal hidden enemy presence.\n\nSEARCH\nSelect a highlighted hex. The selected hex and its six neighbors are searched. Passive reaches 160 nm; Active 200 nm and becomes Loud; Focused 240 nm and temporarily occupies Command. A Search with no detections still costs 2 Time and advances the Ready queue.\n\nSTRIKE AND REACTION\nAfter a Strike is committed and before the roll, an eligible defender chooses one Reaction. Defend adds +1 Defense. Evade adds +1 Defense and moves one valid hex away after combat. Counterattack makes one Light return Strike if the defender has a usable Contact and is capable. Hold preserves position with no modifier. A Reaction does not change Ready Time, is spent once used, and refreshes after that Formation completes its own Action. Disrupted and Crippled formations cannot Counterattack; replenishing formations cannot react. Local Hotseat uses a secure defender handoff. Solo pauses for a human defender and lets the AI choose for its own formation. Orderly Withdrawal extends one Evade to two hexes.\n\nCONTACTS\nLocation, Identity, and Age define what a side knows.\n\nENTROPY AND CARD HAND\nEvery Entropy event draws a physical card, even if that source is already marked. Attached effects stack and appear with Command Responses in the active side's Card Hand at the bottom of the interface. Recover selects and discards one Friction or Disruption card; a source remains marked while another matching card remains. Destruction cards await repair or reorganization.", "body-copy"));
            modal.Add(Text("SEARCH, STRIKE, AND DAMAGE UPDATE\nThe distances above are the carrier baseline; live commitment buttons show the active formation's scenario-defined sensor envelope. Declare Location or Identity priority before Search. At Age 0, High/Medium/Low Location covers radius 0/1/2; Age or tracked movement adds up to two rings. Search can privately disprove False Contacts. A Strike selects one aim hex in a Contact's possible area; hidden occupancy is tested only after commitment. Light damage gives −1 Defense through the formation's next completed own Action. Equal repeated damage escalates one step; higher replaces lower. Crippled formations may only Defend or Hold.", "body-copy", "amber"));
            modal.Add(ActionButton("CLOSE", CloseOverlay, "primary"));
        }

        private void ShowEventInspector()
        {
            VisualElement modal = Modal("EVENT INSPECTOR");
            ScrollView scroll = new ScrollView();
            scroll.AddToClassList("scroll");
            foreach (string entry in backend.Game.VisibleLog(backend.Game.Active.Side)) scroll.Add(Text(entry, "body-copy"));
            modal.Add(scroll);
            modal.Add(ActionButton("CLOSE", CloseOverlay, "primary"));
        }

        private void ShowResponseHand()
        {
            Side side = backend.Game.Active.Side;
            var heldEntropy = HeldEntropyCards(side);
            IReadOnlyList<CommandResponseDefinition> hand = backend.ToolkitResponseHand(side);
            VisualElement modal = Modal(side.ToString().ToUpperInvariant() + " COMMAND TABLE");
            modal.AddToClassList("command-table");
            modal.parent.AddToClassList("command-table-shade");
            modal.Add(Text($"YOUR HAND  •  {hand.Count} COMMAND RESPONSE{(hand.Count == 1 ? string.Empty : "S")}  •  {heldEntropy.Count} ATTACHED ENTROPY", "eyebrow"));
            modal.Add(Text("Select a card to inspect or play it. The operational map remains visible beneath the table.", "muted"));
            ScrollView scroll = new ScrollView();
            scroll.AddToClassList("card-table-scroll");
            scroll.contentContainer.AddToClassList("card-table-grid");
            foreach (var held in heldEntropy)
            {
                FormationState formation = held.Formation;
                EntropyEffectDefinition card = held.Card;
                VisualElement face = El("physical-card", card.Source.ToString().ToLowerInvariant());
                face.Add(Text(card.Id + "  •  " + card.Source.ToString().ToUpperInvariant(), "physical-card-id"));
                face.Add(Text(card.Title.ToUpperInvariant(), "physical-card-title"));
                face.Add(Text(card.Effect, "physical-card-effect"));
                if (!string.IsNullOrEmpty(card.Response)) face.Add(Text("RESPONSE  •  " + card.Response, "physical-card-cost"));
                face.Add(El("physical-card-rule"));
                face.Add(Text("ATTACHED TO\n" + formation.Name.ToUpperInvariant(), "physical-card-owner"));
                Button inspect = ActionButton("INSPECT ATTACHED CARD", () => ShowHeldEntropyCard(formation, card));
                inspect.AddToClassList("physical-card-action");
                face.Add(inspect);
                scroll.Add(face);
            }
            foreach (CommandResponseDefinition card in hand)
            {
                VisualElement response = El("physical-card", "response");
                response.Add(Text(card.Id + "  •  COMMAND RESPONSE", "physical-card-id"));
                response.Add(Text(card.Title.ToUpperInvariant(), "physical-card-title"));
                response.Add(Text(card.Play, "physical-card-effect"));
                response.Add(Text(string.IsNullOrEmpty(card.Cost) ? "NO ADDITIONAL COST" : "COST  •  " + card.Cost, "physical-card-cost"));
                response.Add(El("physical-card-rule"));
                response.Add(Text("HELD IN COMMAND\nPLAY DURING A LEGAL WINDOW", "physical-card-owner"));
                Button play = ActionButton(card.MechanicallySupported ? "SELECT TARGET & PLAY" : "SYSTEM NOT YET ACTIVE", () => ShowResponseTargets(card), card.MechanicallySupported ? "primary" : null);
                play.SetEnabled(card.MechanicallySupported);
                play.AddToClassList("physical-card-action");
                response.Add(play);
                scroll.Add(response);
            }
            if (heldEntropy.Count == 0 && hand.Count == 0)
            {
                VisualElement empty = El("empty-hand");
                empty.Add(Text("NO CARDS ON THE TABLE", "subheading"));
                empty.Add(Text("Command Responses are drawn through play. Entropy cards appear here when attached to a friendly Formation.", "body-copy"));
                scroll.Add(empty);
            }
            modal.Add(scroll);
            modal.Add(ActionButton("RETURN TO COMMAND MAP", CloseOverlay, "primary"));
        }

        private List<(FormationState Formation, EntropyEffectDefinition Card)> HeldEntropyCards(Side side)
            => backend.Game.Formations
                .Where(formation => formation.Side == side)
                .SelectMany(formation => (formation.ActiveEffectCardIds ?? new List<string>())
                    .Select(id => (Formation: formation, Card: EntropyEffectCatalog.Find(id))))
                .Where(item => item.Card != null)
                .ToList();

        private int HeldCardCount(Side side) => HeldEntropyCards(side).Count + backend.ToolkitResponseHand(side).Count;

        private void ShowResponseTargets(CommandResponseDefinition card)
        {
            Side side = backend.Game.Active.Side;
            VisualElement modal = Modal(card.Id + "  •  " + card.Title.ToUpperInvariant());
            modal.Add(Text(card.Play + (string.IsNullOrEmpty(card.Cost) ? string.Empty : "\nCOST  •  " + card.Cost), "body-copy"));
            if (card.Target == ResponseTarget.SynchronizedStrike)
            {
                modal.Add(Text("This card is consumed atomically during declaration, after participants and an aim are chosen.", "muted"));
                modal.Add(ActionButton("DECLARE SYNCHRONIZED STRIKE", ShowSynchronizedStrikeContacts, "primary"));
            }
            else if (card.Target == ResponseTarget.Contact)
            {
                foreach (ContactState contact in backend.Game.Contacts.Where(candidate => candidate.Owner == side && !candidate.IsLost))
                {
                    ContactState selected = contact;
                    modal.Add(ActionButton($"{selected.Summary}  •  {selected.LastKnownPosition}", () => PlayResponse(card, null, selected, null)));
                }
            }
            else if (card.Target == ResponseTarget.Hex)
            {
                modal.Add(Text("Select a friendly Formation's current hex as the False Contact location.", "muted"));
                foreach (FormationState formation in backend.Game.Formations.Where(candidate => candidate.Side == side && !candidate.IsDestroyed))
                {
                    FormationState selected = formation;
                    modal.Add(ActionButton($"{selected.Name.ToUpperInvariant()}  •  HEX {selected.Position}", () => PlayResponse(card, null, null, selected.Position)));
                }
            }
            else
            {
                foreach (FormationState formation in backend.Game.Formations.Where(candidate => candidate.Side == side && !candidate.IsDestroyed))
                {
                    FormationState selected = formation;
                    modal.Add(ActionButton($"{selected.Name.ToUpperInvariant()}  •  MISSION {selected.Mission.ToString().ToUpperInvariant()}  •  READY T{selected.ReadyTime:00}", () =>
                    {
                        if (card.Id == "C-04" || card.Id == "C-17") ShowMissionChoices(card, selected);
                        else PlayResponse(card, selected, null, null);
                    }));
                }
            }
            modal.Add(ActionButton("BACK TO HAND", ShowResponseHand));
        }

        private void PlayResponse(CommandResponseDefinition card, FormationState formation, ContactState contact, HexCoord? hex)
        {
            backend.ToolkitPlayResponse(card.Id, formation, contact, hex);
            if (backend.LastToolkitActionSucceeded) ShowGame(); else ShowResponseTargets(card);
        }

        private void ShowMissionChoices(CommandResponseDefinition card, FormationState formation)
        {
            VisualElement modal = Modal(card.Id + "  •  SELECT NEW MISSION");
            modal.Add(Text($"{formation.Name.ToUpperInvariant()} currently has Mission {formation.Mission}. {(card.Id == "C-17" ? "Flash Order penetrates Broken Link immediately and marks 1 Command Strain." : "Rapid Replan changes its task without occupying Command; its next Ready scheduling receives +1 Time.")}", "body-copy"));
            ActionKind[] playableMissions = { ActionKind.Move, ActionKind.Search, ActionKind.Strike, ActionKind.Patrol, ActionKind.Support, ActionKind.Recover, ActionKind.Replenish, ActionKind.Hold };
            foreach (ActionKind mission in playableMissions)
            {
                ActionKind selectedMission = mission;
                Button choice = ActionButton(selectedMission.ToString().ToUpperInvariant(), () =>
                {
                    backend.ToolkitPlayResponse(card.Id, formation, null, null, selectedMission);
                    if (backend.LastToolkitActionSucceeded) ShowGame(); else ShowMissionChoices(card, formation);
                }, formation.Mission == selectedMission ? "selected" : null);
                choice.SetEnabled(formation.Mission != selectedMission);
                modal.Add(choice);
            }
            modal.Add(ActionButton("BACK TO FORMATIONS", () => ShowResponseTargets(card)));
        }

        private void ConfirmHeavyContact()
        {
            ContactState contact = pendingStrikeContact;
            int attack = backend.Game.Active.EffectiveStrike + 2 + backend.Game.PendingSupportBonus(backend.Game.Active, SupportKind.Strike) + Rules.TargetingModifier(contact, backend.Game.AgeTwoTargetingPenalty);
            VisualElement modal = Modal("CONFIRM HEAVY SALVO");
            modal.Add(Text($"Heavy capability will be expended.\n\nAIM HEX {pendingStrikeAim}\nATTACK {attack}\nDEFENSE AND OCCUPANCY UNRESOLVED\n\nIf the hidden target occupies the aim hex, its defender chooses a legal Reaction before the combat roll. Otherwise the action reports no confirmed effect.", "body-copy"));
            modal.Add(ActionButton("COMMIT HEAVY SALVO", ContinueCommittedStrike, "warning"));
            modal.Add(ActionButton("CANCEL", CloseOverlay));
        }

        private void ShowResults()
        {
            view = View.Results;
            BeginScreen($"OPERATION COMPLETE  •  TIME {backend.Game.Scenario.Horizon}");
            VisualElement page = El("front-page");
            VisualElement card = Panel("hero-card", "center");
            card.Add(Text(backend.ResultSummary, "front-title"));
            card.Add(Text($"BLUE  {backend.BlueFinalScore} VP        RED  {backend.RedFinalScore} VP", "heading"));
            if (backend.FinalOutcome != null)
            {
                VisualElement breakdowns = El("row");
                breakdowns.Add(ResultBreakdown(backend.FinalOutcome.Blue));
                breakdowns.Add(ResultBreakdown(backend.FinalOutcome.Red));
                card.Add(breakdowns);
                card.Add(Text("DECIDED BY  •  " + backend.FinalOutcome.DecidingFactor.ToUpperInvariant(), "eyebrow", "amber"));
            }
            card.Add(Text("Playtest data was exported automatically. Complete the debrief to add qualitative evidence.", "body-copy"));
            card.Add(ActionButton("PLAYTEST DEBRIEF", ShowFeedback, "primary"));
            card.Add(ActionButton("PLAY AGAIN", () => { backend.ToolkitNewScenario(); OperationalMap3D.ResetPresentationMemory(); ShowBriefing(); }));
            card.Add(ActionButton("MAIN MENU", ShowMain));
            page.Add(card);
            app.Add(page);
        }

        private VisualElement ResultBreakdown(ScenarioSideScore score)
        {
            VisualElement panel = Panel();
            panel.style.width = Length.Percent(47);
            panel.Add(Text(score.Side.ToString().ToUpperInvariant() + " OBJECTIVES", "subheading"));
            foreach (ScenarioObjectiveResult objective in score.Objectives)
                panel.Add(Text($"{(objective.Achieved ? "✓" : "—")}  {objective.Title}  •  {(objective.Achieved ? objective.Points : 0)}/{objective.Points} VP", "muted", objective.Achieved ? "amber" : null));
            panel.Add(Text($"TIE-BREAKS  •  CAPABLE {score.CombatCapableFormations}  •  DAMAGE {score.DamageBurden}  •  RANGE {(score.ClosestObjectiveRange == int.MaxValue ? "—" : score.ClosestObjectiveRange.ToString())}", "muted"));
            return panel;
        }

        private void ShowFeedback()
        {
            PlaytestFeedback feedback = backend.Feedback;
            VisualElement modal = Modal("PLAYTEST DEBRIEF");
            ScrollView scroll = new ScrollView();
            scroll.AddToClassList("scroll");
            IntegerField ready = Rating("Ready-Time clarity", feedback.ReadyTimeClarity);
            IntegerField info = Rating("Information affected decisions", feedback.InformationImpact);
            IntegerField combat = Rating("Combat fairness", feedback.CombatFairness);
            IntegerField entropy = Rating("Entropy interest", feedback.EntropyInterest);
            Toggle obvious = new Toggle("Obvious choice occurred") { value = feedback.ObviousChoiceOccurred };
            Toggle lookup = new Toggle("Repeated rules lookup") { value = feedback.RepeatedRuleLookup };
            Toggle unfair = new Toggle("Unfair-feeling roll") { value = feedback.UnfairFeelingRoll };
            Toggle lull = new Toggle("Inactive-player lull") { value = feedback.InactivePlayerLull };
            TextField best = Area("Best decision", feedback.BestDecision);
            TextField confusing = Area("Most confusing moment", feedback.MostConfusingMoment);
            TextField adjustment = Area("Suggested rules adjustment", feedback.RulesAdjustment);
            scroll.Add(ready); scroll.Add(info); scroll.Add(combat); scroll.Add(entropy); scroll.Add(obvious); scroll.Add(lookup); scroll.Add(unfair); scroll.Add(lull); scroll.Add(best); scroll.Add(confusing); scroll.Add(adjustment);
            scroll.Add(ActionButton("EXPORT DEBRIEF", () =>
            {
                feedback.ReadyTimeClarity = Mathf.Clamp(ready.value, 1, 5); feedback.InformationImpact = Mathf.Clamp(info.value, 1, 5); feedback.CombatFairness = Mathf.Clamp(combat.value, 1, 5); feedback.EntropyInterest = Mathf.Clamp(entropy.value, 1, 5);
                feedback.ObviousChoiceOccurred = obvious.value; feedback.RepeatedRuleLookup = lookup.value; feedback.UnfairFeelingRoll = unfair.value; feedback.InactivePlayerLull = lull.value;
                feedback.BestDecision = best.value; feedback.MostConfusingMoment = confusing.value; feedback.RulesAdjustment = adjustment.value;
                backend.ToolkitExport(); CloseOverlay();
            }, "primary"));
            modal.Add(scroll);
        }

        private void BeginScreen(string context, bool gameHeader = false)
        {
            root.Clear();
            app = El("app");
            app.EnableInClassList("hud-hidden", gameHeader && !edgeHudVisible);
            if (backend.HighContrast) app.AddToClassList("high-contrast");
            ApplyAccessibilityClasses();
            root.Add(app);
            VisualElement top = El("topbar");
            VisualElement brand = new VisualElement();
            brand.Add(Text("SEA OF UNCERTAINTY", "brand"));
            brand.Add(Text(context, "eyebrow"));
            top.Add(brand);
            top.Add(El("spacer"));
            if (gameHeader)
            {
                if (backend.SelectedMode == OperationMode.SoloVsAi) top.Add(Text("SOLO • BLUE COMMAND", "eyebrow"));
                top.Add(Text($"NOW READY  •  {backend.Game.Active.Name.ToUpperInvariant()}", "subheading"));
                top.Add(ActionButton($"CARD TABLE {HeldCardCount(backend.Game.Active.Side)}  •  H", ShowResponseHand, "table-access"));
                top.Add(ActionButton("LOG  •  L", ShowEventInspector));
                top.Add(ActionButton(backend.HexGridVisible ? "HEXES ON" : "HEXES OFF", () =>
                {
                    backend.ToolkitToggleHexGrid();
                    ShowGame();
                }, backend.HexGridVisible ? "selected" : null));
                top.Add(ActionButton("RULES", ShowRules));
                top.Add(ActionButton("MENU", ShowPause));
            }
            app.Add(top);
            ApplyResponsiveClass(root.resolvedStyle.width, root.resolvedStyle.height);
        }

        private VisualElement Modal(string title)
        {
            VisualElement previousFocus = root?.panel?.focusController?.focusedElement as VisualElement;
            CloseOverlay();
            focusBeforeOverlay = previousFocus;
            VisualElement shade = El("overlay-shade");
            shade.name = "overlay";
            VisualElement modal = Panel("modal");
            modal.name = "dialog-" + AccessibleId(title);
            modal.Add(Text(title, "heading"));
            shade.Add(modal);
            root.Add(shade);
            modal.schedule.Execute(() => modal.Query<Button>().First()?.Focus());
            return modal;
        }

        private void CloseOverlay()
        {
            root?.Q<VisualElement>("overlay")?.RemoveFromHierarchy();
            VisualElement restore = focusBeforeOverlay;
            focusBeforeOverlay = null;
            if (restore != null && restore.panel != null) restore.schedule.Execute(restore.Focus);
        }

        private void OnGlobalKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Tab && view == View.Game && root.Q<VisualElement>("overlay") == null)
            {
                ToggleEdgeHud();
                evt.PreventDefault();
                evt.StopPropagation();
                return;
            }
            if (evt.keyCode == KeyCode.Escape)
            {
                if (entropyRevealBlocking) { evt.StopPropagation(); return; }
                if (root.Q<VisualElement>("overlay") != null) CloseOverlay(); else if (view == View.Game) ShowPause();
                return;
            }
            VisualElement focused = root.panel?.focusController?.focusedElement as VisualElement;
            bool editing = focused is TextField || focused is IntegerField || focused is Slider || focused is DropdownField;
            if (!editing && (root.Q<VisualElement>("overlay") != null || view != View.Game || focused != map) &&
                (evt.keyCode == KeyCode.LeftArrow || evt.keyCode == KeyCode.UpArrow || evt.keyCode == KeyCode.RightArrow || evt.keyCode == KeyCode.DownArrow))
            {
                MoveFocus(evt.keyCode == KeyCode.LeftArrow || evt.keyCode == KeyCode.UpArrow ? -1 : 1);
                evt.StopPropagation();
                return;
            }
            if (view != View.Game || root.Q<VisualElement>("overlay") != null) return;
            if (map != null && map.SetCameraKey(evt.keyCode, true)) { evt.StopPropagation(); return; }
            if (evt.keyCode == KeyCode.C) { map?.FocusActiveFormation(); evt.StopPropagation(); return; }
            if (evt.keyCode == KeyCode.O) { map?.FocusObjective(); evt.StopPropagation(); return; }
            if (evt.keyCode == KeyCode.V) { edgeScrollEnabled = !edgeScrollEnabled; map?.SetEdgeScroll(edgeScrollEnabled); evt.StopPropagation(); return; }
            if (evt.keyCode == KeyCode.F9) { map?.SaveCameraView(); evt.StopPropagation(); return; }
            if (evt.keyCode == KeyCode.F10) { map?.RecallCameraView(); evt.StopPropagation(); return; }
            if (evt.keyCode == KeyCode.Home) { map?.ResetCameraView(); evt.StopPropagation(); return; }
            if (evt.keyCode == KeyCode.G) { backend.ToolkitToggleHexGrid(); ShowGame(); return; }
            if (evt.keyCode == KeyCode.H) { ShowResponseHand(); evt.StopPropagation(); return; }
            if (evt.keyCode == KeyCode.L) { ShowEventInspector(); evt.StopPropagation(); return; }
            if (evt.keyCode == KeyCode.Alpha1 || evt.keyCode == KeyCode.Keypad1) SelectAction(ToolkitActionMode.Move);
            else if (evt.keyCode == KeyCode.Alpha2 || evt.keyCode == KeyCode.Keypad2) SelectAction(ToolkitActionMode.Search);
            else if (evt.keyCode == KeyCode.Alpha3 || evt.keyCode == KeyCode.Keypad3) SelectAction(ToolkitActionMode.Strike);
            else if (evt.keyCode == KeyCode.Alpha4 || evt.keyCode == KeyCode.Keypad4) ResolveAction(() => backend.ToolkitRecover());
            else if (evt.keyCode == KeyCode.Alpha5 || evt.keyCode == KeyCode.Keypad5) ResolveAction(() => backend.ToolkitHold());
            else if (evt.keyCode == KeyCode.Alpha6 || evt.keyCode == KeyCode.Keypad6) ShowPatrolSetup();
            else if (evt.keyCode == KeyCode.Alpha7 || evt.keyCode == KeyCode.Keypad7) ShowSupportSetup();
            else if (evt.keyCode == KeyCode.Alpha8 || evt.keyCode == KeyCode.Keypad8) { SelectAction(ToolkitActionMode.Replenish); ShowReplenishmentSetup(); }
        }

        private void OnGlobalKeyUp(KeyUpEvent evt)
        {
            if (view != View.Game || map == null) return;
            if (map.SetCameraKey(evt.keyCode, false)) evt.StopPropagation();
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            ApplyResponsiveClass(evt.newRect.width, evt.newRect.height);
            EnsureNativeFullscreenResolution();
        }

        private void ToggleEdgeHud()
        {
            if (view != View.Game || app == null) return;
            edgeHudVisible = !edgeHudVisible;
            ApplyEdgeHudState();
            map?.schedule.Execute(map.Refresh);
        }

        private void ApplyEdgeHudState()
        {
            if (app == null || view != View.Game) return;
            app.EnableInClassList("hud-hidden", !edgeHudVisible);
            if (edgeHudToggle == null) return;
            edgeHudToggle.text = edgeHudVisible ? "HIDE EDGE HUD  •  TAB" : "SHOW EDGE HUD  •  TAB";
            edgeHudToggle.tooltip = edgeHudVisible ? "Hide the 2D timeline, dossier, and action controls." : "Show the 2D timeline, dossier, and action controls.";
        }

        private void ToggleFullscreen()
        {
            if (RequestedFullscreenMode(Screen.fullScreen) == FullScreenMode.Windowed)
            {
                int width = Mathf.Min(1600, Display.main.systemWidth);
                int height = Mathf.Min(900, Display.main.systemHeight);
                Screen.SetResolution(width, height, FullScreenMode.Windowed);
            }
            else Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, FullScreenMode.FullScreenWindow);
            root.schedule.Execute(ShowSettings).ExecuteLater(150);
        }

        private static void EnsureNativeFullscreenResolution()
        {
            if (!Screen.fullScreen || Display.main == null) return;
            if (Screen.width != Display.main.systemWidth || Screen.height != Display.main.systemHeight || Screen.fullScreenMode != FullScreenMode.FullScreenWindow)
                Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, FullScreenMode.FullScreenWindow);
        }
        private void ApplyResponsiveClass(float width, float height)
        {
            if (app == null) return;
            app.EnableInClassList("compact", width > 0 && width < 1250);
            app.EnableInClassList("short", height > 0 && height < 900);
        }

        private void ApplyAccessibilityClasses()
        {
            if (app == null || backend == null) return;
            app.EnableInClassList("large-text", backend.TextScale >= 1.15f && backend.TextScale < 1.4f);
            app.EnableInClassList("extra-large-text", backend.TextScale >= 1.4f);
        }

        private void MoveFocus(int direction)
        {
            VisualElement scope = root?.Q<VisualElement>("overlay") ?? app;
            if (scope == null) return;
            var focusables = new List<VisualElement>();
            CollectFocusables(scope, focusables);
            if (focusables.Count == 0) return;
            VisualElement focused = root.panel?.focusController?.focusedElement as VisualElement;
            int index = focusables.IndexOf(focused);
            index = index < 0 ? (direction > 0 ? 0 : focusables.Count - 1) : (index + direction + focusables.Count) % focusables.Count;
            focusables[index].Focus();
        }

        private static void CollectFocusables(VisualElement parent, List<VisualElement> result)
        {
            if (parent.focusable && parent.enabledInHierarchy && parent.resolvedStyle.display != DisplayStyle.None) result.Add(parent);
            foreach (VisualElement child in parent.Children()) CollectFocusables(child, result);
        }

        private void SubmitFocused()
        {
            VisualElement focused = root?.panel?.focusController?.focusedElement as VisualElement;
            if (focused == null) { MoveFocus(1); return; }
            using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = focused;
                focused.SendEvent(submit);
            }
        }

        private void CancelNavigation()
        {
            if (entropyRevealBlocking) return;
            if (root?.Q<VisualElement>("overlay") != null) CloseOverlay();
            else if (view == View.Game) ShowPause();
        }

        private static string Words(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var words = new System.Text.StringBuilder(value.Length + 4);
            for (int i = 0; i < value.Length; i++)
            {
                if (i > 0 && char.IsUpper(value[i]) && !char.IsUpper(value[i - 1])) words.Append(' ');
                words.Append(char.ToUpperInvariant(value[i]));
            }
            return words.ToString();
        }

        private static string KindTag(FormationKind kind)
            => kind == FormationKind.CarrierGroup ? "CVG" : kind == FormationKind.SurfaceGroup ? "SAG" : kind == FormationKind.Submarine ? "SSN" : kind == FormationKind.AirGroup ? "AIR" : "LOG";

        private VisualElement Stat(string name, int value, string tooltip)
        {
            VisualElement stat = El("stat"); stat.tooltip = tooltip; stat.Add(Text(name, "stat-name")); stat.Add(Text(value.ToString(), "stat-value")); return stat;
        }

        private IntegerField Rating(string label, int value) { var field = new IntegerField(label) { value = value }; field.tooltip = "Enter a rating from 1 to 5."; return field; }
        private TextField Area(string label, string value) { var field = new TextField(label) { value = value, multiline = true }; field.AddToClassList("feedback-field"); return field; }
        private VisualElement Panel(params string[] classes) { VisualElement panel = El("panel"); foreach (string className in classes.Where(c => !string.IsNullOrEmpty(c))) panel.AddToClassList(className); panel.name = "region-" + AccessibleId(classes.FirstOrDefault(c => !string.IsNullOrEmpty(c)) ?? "panel"); return panel; }
        private VisualElement El(params string[] classes) { var element = new VisualElement(); foreach (string className in classes.Where(c => !string.IsNullOrEmpty(c))) element.AddToClassList(className); return element; }
        private Label Text(string value, params string[] classes) { var label = new Label(value) { name = "text-" + AccessibleId(value) }; foreach (string className in classes.Where(c => !string.IsNullOrEmpty(c))) label.AddToClassList(className); return label; }
        private Button ActionButton(string text, Action action, params string[] classes)
        {
            var button = new Button(action) { text = text, focusable = true, tabIndex = 0, name = "action-" + AccessibleId(text), tooltip = text };
            button.userData = action;
            button.AddToClassList("button");
            foreach (string className in classes.Where(c => !string.IsNullOrEmpty(c))) button.AddToClassList(className);
            return button;
        }

        private static string AccessibleId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "unnamed";
            return new string(value.ToLowerInvariant().Where(character => char.IsLetterOrDigit(character) || character == '-').Take(48).ToArray());
        }

        private static string Odds(CombatBand band)
        {
            if (band == CombatBand.Poor) return "No effect 83% • Light 17%";
            if (band == CombatBand.Even) return "No effect 17% • Light 67% • Heavy 17%";
            if (band == CombatBand.Favorable) return "Light 17% • Heavy 67% • Crippled 17%";
            return "Heavy 17% • Crippled 67% • Destroyed 17%";
        }
    }
}
