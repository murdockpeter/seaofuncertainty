using System;
using System.Collections.Generic;
using System.Linq;
using SeaOfUncertainty.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeaOfUncertainty.Prototype
{
    public sealed class SeaUIToolkitController : MonoBehaviour
    {
        private enum View { Main, Scenario, Briefing, Setup, Handoff, Game, Results }

        private SeaPrototypeController backend;
        private UIDocument document;
        private VisualElement root;
        private VisualElement app;
        private VisualElement dossier;
        private TacticalMapElement map;
        private View view;
        private ToolkitActionMode actionMode;
        private MoveMode moveMode = MoveMode.Normal;
        private SearchMode searchMode = SearchMode.Passive;
        private Salvo salvo = Salvo.Standard;
        private Side previousSide;

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
            root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            if (Environment.GetCommandLineArgs().Contains("-capture3DPrototype"))
            {
                string scenarioArgument = Environment.GetCommandLineArgs().FirstOrDefault(argument => argument.StartsWith("-scenario=", StringComparison.OrdinalIgnoreCase));
                backend.ToolkitNewScenario(scenarioArgument == null ? "meridian-veil" : scenarioArgument.Substring("-scenario=".Length));
                backend.ToolkitBeginDecision();
                ShowGame();
            }
            else ShowMain();
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
            card.Add(ActionButton("BEGIN OPERATION", ShowScenario, "primary"));
            if (backend.HasSave) card.Add(ActionButton("CONTINUE SAVED OPERATION", () => { backend.ToolkitLoad(); previousSide = backend.Game.Active.Side; ShowHandoff(); }));
            VisualElement row = El("row");
            row.Add(ActionButton("SETTINGS", ShowSettings));
            row.Add(ActionButton("FIELD MANUAL", ShowRules));
            row.Add(ActionButton("EXIT GAME", ShowExitConfirmation, "danger-button"));
            card.Add(row);
            card.Add(Text("Local pass-and-play • Multiplayer intentionally deferred • Mouse and keyboard enabled", "muted"));
            page.Add(card);
            app.Add(page);
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
                card.Add(ActionButton("REVIEW BRIEFING", () => { backend.ToolkitNewScenario(scenario.Id); ShowBriefing(); }, "primary"));
                list.Add(card);
            }
            list.Add(ActionButton("BACK", ShowMain));
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
            card.Add(Text("PROVISIONAL SCORING", "eyebrow"));
            card.Add(Text("5 VP — Sole objective control\n3 VP — Carrier remains combat capable\n2 VP — Enemy Destroyed\n1 VP — Enemy Crippled", "body-copy"));
            card.Add(Text($"ASSUMPTIONS\n20 nm hexes • 2 hours / Ready Time • Formation-specific Strike ranges • Age-2 penalty {(backend.AgeTwoPenalty ? "ON" : "OFF")} • Automatic Defend", "muted"));
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
            row.Add(ActionButton("LOCK SETUP & BEGIN", () => { previousSide = backend.Game.Active.Side; ShowHandoff(); }, "primary"));
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
            map.SetState(backend.Game, actionMode, moveMode, searchMode, salvo, backend.ReducedMotion);
            mapColumn.Add(map);
            mapColumn.Add(Text($"3D COMMAND MAP  •  {backend.Game.Area.NauticalMilesPerHex} NM / HEX  •  WHEEL ZOOM  •  MIDDLE-DRAG PAN  •  RIGHT-DRAG ROTATE  •  HOME RESET", "muted"));
            workspace.Add(mapColumn);
            dossier = Panel("dossier");
            UpdateDossier(null);
            workspace.Add(dossier);
            app.Add(workspace);

            VisualElement bottom = El("bottom-deck");
            bottom.Add(BuildActions());
            bottom.Add(BuildEventFeed());
            app.Add(bottom);
            map.schedule.Execute(() => map.Refresh());
        }

        private VisualElement BuildTimeline()
        {
            VisualElement panel = Panel("timeline");
            panel.Add(Text("FORMATION TIMELINE", "subheading"));
            Label reason = Text(backend.ToolkitActivationReason(), "muted");
            reason.tooltip = "Lowest Ready Time, then lower Entropy, then higher Command, then the reproducible prototype tie-break.";
            panel.Add(reason);
            ScrollView scroll = new ScrollView();
            foreach (FormationState formation in backend.Game.Formations.Where(f => !f.IsDestroyed).OrderBy(f => f.ReadyTime).ThenBy(f => f.EntropySources).ThenByDescending(f => f.Ratings.Command))
            {
                Button row = ActionButton($"{formation.Name}\n{formation.Kind} • {formation.Cohesion}                    T{formation.ReadyTime:00}", () => UpdateDossier(null));
                row.AddToClassList("timeline-row");
                if (formation == backend.Game.Active) row.AddToClassList("active");
                row.tooltip = $"Ready T{formation.ReadyTime:00} • Entropy {formation.EntropySources} • Command {formation.Ratings.Command}";
                scroll.Add(row);
            }
            panel.Add(scroll);
            return panel;
        }

        private VisualElement BuildActions()
        {
            VisualElement panel = Panel("actions");
            panel.Add(Text("CHOOSE AN ACTION", "subheading"));
            VisualElement actions = El("row");
            actions.Add(ActionButton("1  MOVE • 2", () => SelectAction(ToolkitActionMode.Move), actionMode == ToolkitActionMode.Move ? "selected" : null));
            actions.Add(ActionButton("2  SEARCH • 2", () => SelectAction(ToolkitActionMode.Search), actionMode == ToolkitActionMode.Search ? "selected" : null));
            actions.Add(ActionButton("3  STRIKE • 2", () => SelectAction(ToolkitActionMode.Strike), actionMode == ToolkitActionMode.Strike ? "selected" : null));
            actions.Add(ActionButton("4  RECOVER • 2", () => ResolveAction(() => backend.ToolkitRecover())));
            actions.Add(ActionButton("5  HOLD • 1", () => ResolveAction(() => backend.ToolkitHold())));
            panel.Add(actions);
            VisualElement modes = El("row");
            if (actionMode == ToolkitActionMode.Move)
            {
                FormationState active = backend.Game.Active;
                int cautious = Rules.MoveAllowance(active, MoveMode.Cautious), normal = Rules.MoveAllowance(active, MoveMode.Normal), high = Rules.MoveAllowance(active, MoveMode.HighTempo);
                modes.Add(ModeButton($"CAUTIOUS • {cautious} / {cautious * backend.Game.Area.NauticalMilesPerHex} NM", MoveMode.Cautious)); modes.Add(ModeButton($"NORMAL • {normal} / {normal * backend.Game.Area.NauticalMilesPerHex} NM", MoveMode.Normal)); modes.Add(ModeButton($"HIGH TEMPO • {high} / {high * backend.Game.Area.NauticalMilesPerHex} NM + F", MoveMode.HighTempo));
            }
            else if (actionMode == ToolkitActionMode.Search)
            {
                int scale = backend.Game.Area.NauticalMilesPerHex;
                modes.Add(ModeButton($"PASSIVE • +0 / {Rules.SearchRange(SearchMode.Passive) * scale} NM", SearchMode.Passive)); modes.Add(ModeButton($"ACTIVE • +1 / {Rules.SearchRange(SearchMode.Active) * scale} NM LOUD", SearchMode.Active)); modes.Add(ModeButton($"FOCUSED • +2 / {Rules.SearchRange(SearchMode.Focused) * scale} NM / 1◆", SearchMode.Focused));
            }
            else if (actionMode == ToolkitActionMode.Strike)
            {
                FormationKind kind = backend.Game.Active.Kind;
                int scale = backend.Game.Area.NauticalMilesPerHex;
                modes.Add(ModeButton($"LIGHT • +0 / {Rules.StrikeRange(kind, Salvo.Light) * scale} NM", Salvo.Light)); modes.Add(ModeButton($"STANDARD • +1 / {Rules.StrikeRange(kind, Salvo.Standard) * scale} NM", Salvo.Standard)); modes.Add(ModeButton($"HEAVY • +2 / {Rules.StrikeRange(kind, Salvo.Heavy) * scale} NM / EXP", Salvo.Heavy));
            }
            panel.Add(modes);
            panel.Add(Text(ActionPreview(), "body-copy", "amber"));
            panel.Add(Text(backend.LastMessage, "muted"));
            return panel;
        }

        private VisualElement BuildEventFeed()
        {
            VisualElement panel = Panel("event-feed");
            VisualElement header = El("row");
            header.Add(Text("AFTER-ACTION FEED", "subheading"));
            header.Add(El("spacer"));
            header.Add(ActionButton("INSPECT", ShowEventInspector));
            panel.Add(header);
            foreach (string entry in backend.Game.Log.Take(4)) panel.Add(Text(entry, "muted"));
            return panel;
        }

        private void UpdateDossier(ContactState contact)
        {
            if (dossier == null) return;
            dossier.Clear();
            if (contact != null && actionMode != ToolkitActionMode.None)
            {
                FormationState target = backend.Game.Find(contact.TargetId);
                dossier.Add(Text("PRE-COMMITMENT PREVIEW", "eyebrow"));
                dossier.Add(Text(contact.Identity == IdentityQuality.Identified ? target.Name.ToUpperInvariant() : "UNRESOLVED CONTACT", "heading"));
                dossier.Add(Text(contact.Summary + "\nLAST KNOWN " + contact.LastKnownPosition, "body-copy"));
                if (actionMode == ToolkitActionMode.Search)
                {
                    int centerRange = HexCoord.Distance(backend.Game.Active.Position, contact.LastKnownPosition);
                    dossier.Add(Text($"AREA CENTER {contact.LastKnownPosition}\nCENTER RANGE {centerRange} / {Rules.SearchRange(searchMode)}\nFOOTPRINT RADIUS {Rules.SearchAreaRadius}\nSEARCH {backend.Game.Active.EffectiveSearch}\nMODE {Rules.SearchModifier(searchMode):+0;-0;0}\n\nHidden Signature, Loud status, and exact position resolve privately.", "body-copy"));
                }
                else if (actionMode == ToolkitActionMode.Strike)
                {
                    int attack = backend.Game.Active.EffectiveStrike + Rules.SalvoModifier(salvo) + Rules.TargetingModifier(contact, backend.Game.AgeTwoTargetingPenalty);
                    int defense = target.Ratings.Defense + 1 - (target.Destruction ? 1 : 0);
                    CombatBand band = Rules.BandFor(attack - defense);
                    dossier.Add(Text($"ATTACK {attack}\nDEFENSE {defense}\nDIFFERENCE {attack - defense:+0;-0;0}\n\n{band.ToString().ToUpperInvariant()} BAND\n{Odds(band)}", "body-copy"));
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
            stats.Add(Stat("DEFENSE", formation.Ratings.Defense, "Survivability before Reaction."));
            stats.Add(Stat("SIGNATURE", formation.Ratings.Signature + (formation.Loud ? 1 : 0), "Higher is easier to detect."));
            stats.Add(Stat("COMMAND", formation.Ratings.Command, "Same-Time initiative and autonomy."));
            dossier.Add(stats);
            dossier.Add(Text($"READY T{formation.ReadyTime:00}\nENDURANCE {formation.Endurance}\nDAMAGE {formation.Damage}\nCOHESION {formation.Cohesion}\nFRICTION {(formation.Friction ? "MARKED" : "CLEAR")}\nDISRUPTION {(formation.Disruption ? "MARKED" : "CLEAR")}\nDESTRUCTION {(formation.Destruction ? "MARKED" : "CLEAR")}", "body-copy"));
        }

        private void SelectAction(ToolkitActionMode mode)
        {
            actionMode = mode;
            backend.ToolkitRecordChoice(mode.ToString(), mode == ToolkitActionMode.Move ? moveMode.ToString() : mode == ToolkitActionMode.Search ? searchMode.ToString() : salvo.ToString());
            ShowGame();
        }

        private Button ModeButton(string text, MoveMode mode) => ActionButton(text, () => { moveMode = mode; backend.ToolkitRecordChoice("Move", mode.ToString()); ShowGame(); }, moveMode == mode ? "selected" : null);
        private Button ModeButton(string text, SearchMode mode) => ActionButton(text, () => { searchMode = mode; backend.ToolkitRecordChoice("Search", mode.ToString()); ShowGame(); }, searchMode == mode ? "selected" : null);
        private Button ModeButton(string text, Salvo mode) => ActionButton(text, () => { salvo = mode; backend.ToolkitRecordChoice("Strike", mode.ToString()); ShowGame(); }, salvo == mode ? "selected" : null);

        private void OnHexChosen(HexCoord hex)
        {
            if (actionMode == ToolkitActionMode.Move) ResolveAction(() => backend.ToolkitMove(hex, moveMode));
            else if (actionMode == ToolkitActionMode.Search) ResolveAction(() => backend.ToolkitSearchHex(hex, searchMode));
        }

        private void OnContactChosen(ContactState contact)
        {
            FormationState target = backend.Game.Find(contact.TargetId);
            if (actionMode == ToolkitActionMode.Search) ResolveAction(() => backend.ToolkitSearch(target, searchMode));
            else if (actionMode == ToolkitActionMode.Strike)
            {
                if (salvo == Salvo.Heavy) ConfirmHeavy(target); else ResolveAction(() => backend.ToolkitStrike(target, salvo));
            }
        }

        private void ResolveAction(Func<string> action)
        {
            Side actingSide = backend.Game.Active.Side;
            string message = action();
            if (backend.IsComplete) { ShowResults(); return; }
            if (!backend.LastToolkitActionSucceeded) { ShowGame(); return; }
            actionMode = ToolkitActionMode.None;
            if (backend.Game.Active.Side != actingSide) ShowHandoff();
            else { backend.ToolkitBeginDecision(); ShowGame(); }
        }

        private string ActionPreview()
        {
            if (actionMode == ToolkitActionMode.None) return "SELECT AN ACTION  •  Keyboard shortcuts: 1–5.";
            FormationState active = backend.Game.Active;
            int friction = active.Friction || actionMode == ToolkitActionMode.Move && moveMode == MoveMode.HighTempo ? 1 : 0;
            string consequence = actionMode == ToolkitActionMode.Move ? (moveMode == MoveMode.Cautious ? "Signature −1" : moveMode == MoveMode.HighTempo ? "Signature +1 • mark Friction" : "balanced movement") : actionMode == ToolkitActionMode.Search ? (searchMode == SearchMode.Active ? "become Loud" : searchMode == SearchMode.Focused ? "temporarily occupy 1 Command" : "remain quiet") : salvo == Salvo.Heavy ? "expend Heavy capability" : $"Attack {Rules.SalvoModifier(salvo):+0;-0;0}";
            string instruction = actionMode == ToolkitActionMode.Move ? "CLICK A GREEN DESTINATION HEX" : actionMode == ToolkitActionMode.Search ? "CLICK ANY HIGHLIGHTED HEX TO SEARCH AN AREA" : "CLICK AN ELIGIBLE CONTACT TO STRIKE";
            return $"{instruction}\nTIME 2{(friction > 0 ? " +1 Friction" : string.Empty)} → NEXT READY T{backend.Game.Time + 2 + friction:00} • {consequence}";
        }

        private void ShowPause()
        {
            VisualElement modal = Modal("OPERATION PAUSED");
            modal.Add(ActionButton("RESUME", ShowGame, "primary"));
            modal.Add(ActionButton("SAVE OPERATION", () => { backend.ToolkitSave(); ShowGame(); }));
            if (backend.HasSave) modal.Add(ActionButton("LOAD SAVED OPERATION", () => { backend.ToolkitLoad(); ShowHandoff(); }));
            modal.Add(ActionButton("EXPORT PLAYTEST DATA", () => { backend.ToolkitExport(); ShowGame(); }));
            modal.Add(ActionButton("SETTINGS", ShowSettings));
            modal.Add(ActionButton("RESTART OPERATION", () => { backend.ToolkitNewScenario(); ShowBriefing(); }, "warning"));
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
            Toggle age = new Toggle("Age-2 Contacts receive −1 targeting") { value = backend.AgeTwoPenalty };
            Toggle audio = new Toggle("Master audio enabled") { value = backend.AudioEnabled };
            Toggle contrast = new Toggle("High-contrast side colors") { value = backend.HighContrast };
            Toggle motion = new Toggle("Reduced motion") { value = backend.ReducedMotion };
            modal.Add(age); modal.Add(audio); modal.Add(contrast); modal.Add(motion);
            modal.Add(ActionButton(Screen.fullScreen ? "SWITCH TO WINDOWED" : "SWITCH TO FULLSCREEN", () => { Screen.fullScreen = !Screen.fullScreen; ShowSettings(); }));
            modal.Add(ActionButton("APPLY", () => { backend.ToolkitSetSettings(age.value, audio.value, contrast.value, motion.value); CloseOverlay(); }, "primary"));
            modal.Add(ActionButton("CANCEL", CloseOverlay));
        }

        private void ShowRules()
        {
            VisualElement modal = Modal("FIELD MANUAL");
            modal.Add(Text("SCALE\n20 nautical miles per hex. One Ready-Time is two hours.\n\nREADY TIME\nThe earliest formation acts. Ties use Entropy, Command, then the prototype tie-break.\n\nMOVE\nSurface/carrier/sub: Cautious 20 / Normal 40 / High Tempo 60 nm. Air missions: 80 / 120 / 160 nm. Littoral adds one Ready-Time.\n\nSEARCH\nSelect a highlighted hex. The selected hex and its six neighbors are searched. Passive reaches 160 nm; Active 200 nm and becomes Loud; Focused 240 nm and temporarily occupies Command. A Search with no detections still costs 2 Time and advances the Ready queue.\n\nSTRIKE\nRange depends on formation and Light / Standard / Heavy commitment; the selected range is shown before commitment.\n\nCONTACTS\nLocation, Identity, and Age define what a side knows.\n\nENTROPY\nFriction delays, Disruption impairs information, Destruction impairs offense.", "body-copy"));
            modal.Add(ActionButton("CLOSE", CloseOverlay, "primary"));
        }

        private void ShowEventInspector()
        {
            VisualElement modal = Modal("EVENT INSPECTOR");
            ScrollView scroll = new ScrollView();
            scroll.AddToClassList("scroll");
            foreach (string entry in backend.Game.Log) scroll.Add(Text(entry, "body-copy"));
            modal.Add(scroll);
            modal.Add(ActionButton("CLOSE", CloseOverlay, "primary"));
        }

        private void ConfirmHeavy(FormationState target)
        {
            ContactState contact = backend.Game.ContactFor(backend.Game.Active.Side, target.Id);
            int attack = backend.Game.Active.EffectiveStrike + 2 + Rules.TargetingModifier(contact, backend.Game.AgeTwoTargetingPenalty);
            int defense = target.Ratings.Defense + 1 - (target.Destruction ? 1 : 0);
            CombatBand band = Rules.BandFor(attack - defense);
            VisualElement modal = Modal("CONFIRM HEAVY SALVO");
            modal.Add(Text($"Heavy capability will be expended.\n\nATTACK {attack} vs DEFENSE {defense}\n{band.ToString().ToUpperInvariant()} BAND\n{Odds(band)}", "body-copy"));
            modal.Add(ActionButton("COMMIT HEAVY SALVO", () => { CloseOverlay(); ResolveAction(() => backend.ToolkitStrike(target, Salvo.Heavy)); }, "warning"));
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
            card.Add(Text("Playtest data was exported automatically. Complete the debrief to add qualitative evidence.", "body-copy"));
            card.Add(ActionButton("PLAYTEST DEBRIEF", ShowFeedback, "primary"));
            card.Add(ActionButton("PLAY AGAIN", () => { backend.ToolkitNewScenario(); ShowBriefing(); }));
            card.Add(ActionButton("MAIN MENU", ShowMain));
            page.Add(card);
            app.Add(page);
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
            if (!gameHeader)
            {
                Texture2D backdrop = Resources.Load<Texture2D>("Art/tactical-archipelago-v1");
                if (backdrop != null) app.style.backgroundImage = new StyleBackground(backdrop);
            }
            if (backend.HighContrast) app.AddToClassList("high-contrast");
            root.Add(app);
            VisualElement top = El("topbar");
            VisualElement brand = new VisualElement();
            brand.Add(Text("SEA OF UNCERTAINTY", "brand"));
            brand.Add(Text(context, "eyebrow"));
            top.Add(brand);
            top.Add(El("spacer"));
            if (gameHeader)
            {
                top.Add(Text($"NOW READY  •  {backend.Game.Active.Name.ToUpperInvariant()}", "subheading"));
                top.Add(ActionButton("RULES", ShowRules));
                top.Add(ActionButton("MENU", ShowPause));
            }
            app.Add(top);
            ApplyResponsiveClass(root.resolvedStyle.width);
        }

        private VisualElement Modal(string title)
        {
            CloseOverlay();
            VisualElement shade = El("overlay-shade");
            shade.name = "overlay";
            VisualElement modal = Panel("modal");
            modal.Add(Text(title, "heading"));
            shade.Add(modal);
            root.Add(shade);
            modal.schedule.Execute(() => modal.Query<Button>().First()?.Focus());
            return modal;
        }

        private void CloseOverlay() => root?.Q<VisualElement>("overlay")?.RemoveFromHierarchy();

        private void OnGlobalKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape) { if (root.Q<VisualElement>("overlay") != null) CloseOverlay(); else if (view == View.Game) ShowPause(); return; }
            if (view != View.Game || root.Q<VisualElement>("overlay") != null) return;
            if (evt.keyCode == KeyCode.Alpha1 || evt.keyCode == KeyCode.Keypad1) SelectAction(ToolkitActionMode.Move);
            else if (evt.keyCode == KeyCode.Alpha2 || evt.keyCode == KeyCode.Keypad2) SelectAction(ToolkitActionMode.Search);
            else if (evt.keyCode == KeyCode.Alpha3 || evt.keyCode == KeyCode.Keypad3) SelectAction(ToolkitActionMode.Strike);
            else if (evt.keyCode == KeyCode.Alpha4 || evt.keyCode == KeyCode.Keypad4) ResolveAction(() => backend.ToolkitRecover());
            else if (evt.keyCode == KeyCode.Alpha5 || evt.keyCode == KeyCode.Keypad5) ResolveAction(() => backend.ToolkitHold());
        }

        private void OnGeometryChanged(GeometryChangedEvent evt) => ApplyResponsiveClass(evt.newRect.width);
        private void ApplyResponsiveClass(float width)
        {
            if (app == null) return;
            app.EnableInClassList("compact", width > 0 && width < 1250);
        }

        private VisualElement Stat(string name, int value, string tooltip)
        {
            VisualElement stat = El("stat"); stat.tooltip = tooltip; stat.Add(Text(name, "stat-name")); stat.Add(Text(value.ToString(), "stat-value")); return stat;
        }

        private IntegerField Rating(string label, int value) { var field = new IntegerField(label) { value = value }; field.tooltip = "Enter a rating from 1 to 5."; return field; }
        private TextField Area(string label, string value) { var field = new TextField(label) { value = value, multiline = true }; field.AddToClassList("feedback-field"); return field; }
        private VisualElement Panel(params string[] classes) { VisualElement panel = El("panel"); foreach (string className in classes.Where(c => !string.IsNullOrEmpty(c))) panel.AddToClassList(className); return panel; }
        private VisualElement El(params string[] classes) { var element = new VisualElement(); foreach (string className in classes.Where(c => !string.IsNullOrEmpty(c))) element.AddToClassList(className); return element; }
        private Label Text(string value, params string[] classes) { var label = new Label(value); foreach (string className in classes.Where(c => !string.IsNullOrEmpty(c))) label.AddToClassList(className); return label; }
        private Button ActionButton(string text, Action action, params string[] classes)
        {
            var button = new Button(action) { text = text, focusable = true, tabIndex = 0 };
            button.AddToClassList("button");
            foreach (string className in classes.Where(c => !string.IsNullOrEmpty(c))) button.AddToClassList(className);
            return button;
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
