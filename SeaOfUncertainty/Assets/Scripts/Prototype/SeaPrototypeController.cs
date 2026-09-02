using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SeaOfUncertainty.Core;
using UnityEngine;

namespace SeaOfUncertainty.Prototype
{
    public sealed class SeaPrototypeController : MonoBehaviour
    {
        public bool LegacyUiEnabled { get; set; }
        public PrototypeGame Game => game;
        public PlaytestRecorder Telemetry => telemetry;
        public PlaytestFeedback Feedback => feedback;
        public string LastMessage => toast;
        public bool HasSave => File.Exists(SavePath);
        public bool IsComplete => game != null && game.Time >= game.Scenario.Horizon;
        public int BlueFinalScore => blueFinalScore;
        public int RedFinalScore => redFinalScore;
        public string ResultSummary => resultSummary;
        public bool AgeTwoPenalty => ageTwoTargetingPenalty;
        public bool AudioEnabled => audioEnabled;
        public bool HighContrast => highContrast;
        public bool ReducedMotion => reducedMotion;
        public bool HexGridVisible => hexGridVisible;
        public OperationMode SelectedMode => operationMode;
        public Side HumanSide => humanSide;
        public bool IsAiTurn => operationMode == OperationMode.SoloVsAi && game?.Active != null && game.Active.Side != humanSide;
        public bool LastToolkitActionSucceeded => lastActionSucceeded;
        public EntropyEffectDefinition ToolkitPendingEntropyEffect(Side side) => game?.PendingEntropyEffectFor(side);
        public FormationState ToolkitPendingEntropyFormation(Side side) => game?.PendingEntropyFormationFor(side);
        public IReadOnlyList<CommandResponseDefinition> ToolkitResponseHand(Side side) => game?.ResponseHand(side) ?? new List<CommandResponseDefinition>();

        [Serializable]
        private sealed class OperationSave
        {
            public int Version = 4;
            public OperationMode OperationMode;
            public Side HumanSide = Side.Blue;
            public PrototypeGame.SaveData Game;
            public PlaytestSession Playtest;
            public PlaytestFeedback Feedback;
        }

        private struct CombatPreview
        {
            public int Attack;
            public int Defense;
            public int Difference;
            public CombatBand Band;
        }

        private enum PendingAction { None, MoveCautious, MoveNormal, MoveHigh, SearchPassive, SearchActive, SearchFocused, StrikeLight, StrikeStandard, StrikeHeavy }
        private enum AppScreen { MainMenu, ScenarioSelect, Briefing, Setup, PassDevice, Game, Results }
        private enum Overlay { None, Pause, Settings, Rules, EventLog, ConfirmHeavy, Feedback }

        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;
        private PrototypeGame game;
        private Texture2D mapTexture;
        private Texture2D pixel;
        private GUIStyle title;
        private GUIStyle eyebrow;
        private GUIStyle body;
        private GUIStyle small;
        private GUIStyle tiny;
        private GUIStyle panelTitle;
        private GUIStyle button;
        private GUIStyle buttonActive;
        private GUIStyle badge;
        private GUIStyle center;
        private Font font;
        private PendingAction pending;
        private AppScreen screen = AppScreen.MainMenu;
        private Overlay overlay;
        private FormationState inspected;
        private ContactState hoveredContact;
        private FormationState confirmationTarget;
        private string hoverTooltip;
        private Side handoffSide;
        private bool ageTwoTargetingPenalty = true;
        private bool audioEnabled = true;
        private OperationalMissionAudio missionAudio;
        private PrototypeGame audioEventGame;
        private bool highContrast;
        private bool reducedMotion;
        private bool hexGridVisible;
        private OperationMode operationMode = OperationMode.LocalHotseat;
        private Side humanSide = Side.Blue;
        private bool lastActionSucceeded;
        private int blueFinalScore;
        private int redFinalScore;
        private string resultSummary;
        private PlaytestRecorder telemetry;
        private PlaytestFeedback feedback = new PlaytestFeedback();
        private float decisionStartedAt;
        private string toast = "Select the Ready formation's action. Every choice previews its cost.";
        private float toastUntil;
        private Rect mapRect;
        private readonly Dictionary<HexCoord, Vector2> centers = new Dictionary<HexCoord, Vector2>();
        private string SavePath => Path.Combine(Application.persistentDataPath, "meridian-veil-save.json");

        private static void EnsureController()
        {
            if (FindAnyObjectByType<SeaPrototypeController>() == null)
            {
                var root = new GameObject("Sea of Uncertainty — Prototype");
                DontDestroyOnLoad(root);
                root.AddComponent<SeaPrototypeController>();
            }
        }

        private void Awake()
        {
            ageTwoTargetingPenalty = PlayerPrefs.GetInt("AgeTwoPenalty", 1) == 1;
            audioEnabled = PlayerPrefs.GetInt("AudioEnabled", 1) == 1;
            highContrast = PlayerPrefs.GetInt("HighContrast", 0) == 1;
            reducedMotion = PlayerPrefs.GetInt("ReducedMotion", 0) == 1;
            hexGridVisible = PlayerPrefs.GetInt("HexGridVisible", 0) == 1;
            if (Environment.GetCommandLineArgs().Contains("-showHexGrid")) hexGridVisible = true;
            game = new PrototypeGame();
            missionAudio = GetComponent<OperationalMissionAudio>() ?? gameObject.AddComponent<OperationalMissionAudio>();
            AttachMissionAudio(game);
            game.AgeTwoTargetingPenalty = ageTwoTargetingPenalty;
            telemetry = new PlaytestRecorder();
            telemetry.StartNew(game);
            inspected = game.Active;
            mapTexture = Resources.Load<Texture2D>("Art/tactical-archipelago-v1");
            pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "UI Pixel" };
            pixel.SetPixel(0, 0, Color.white);
            pixel.Apply();
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            AudioListener.volume = audioEnabled ? 1 : 0;
            if (Environment.GetCommandLineArgs().Contains("-capturePrototype")) StartCoroutine(CaptureAndQuit());
        }

        private IEnumerator CaptureAndQuit()
        {
            yield return new WaitForSecondsRealtime(1.5f);
            yield return new WaitForEndOfFrame();
            string captureArgument = Environment.GetCommandLineArgs().FirstOrDefault(argument => argument.StartsWith("-captureName=", StringComparison.OrdinalIgnoreCase));
            string captureName = captureArgument == null ? "PrototypeCapture.png" : captureArgument.Substring("-captureName=".Length);
            foreach (char invalid in Path.GetInvalidFileNameChars()) captureName = captureName.Replace(invalid, '_');
            if (!captureName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) captureName += ".png";
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, captureName);
            ScreenCapture.CaptureScreenshot(path, 1);
            yield return new WaitForSecondsRealtime(2f);
            Application.Quit();
        }

        private void OnGUI()
        {
            if (!LegacyUiEnabled) return;
            float scale = Mathf.Min(Screen.width / ReferenceWidth, Screen.height / ReferenceHeight);
            float offsetX = (Screen.width - ReferenceWidth * scale) * .5f;
            float offsetY = (Screen.height - ReferenceHeight * scale) * .5f;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offsetX, offsetY, 0), Quaternion.identity, new Vector3(scale, scale, 1));
            hoveredContact = null;
            hoverTooltip = null;
            EnsureStyles();
            DrawBackground();
            if (screen == AppScreen.Game)
            {
                DrawTopBar();
                DrawRoster();
                DrawMap();
                DrawDossier();
                DrawBottomDeck();
                HandleMapInput(Event.current);
            }
            else DrawFrontEnd();
            if (overlay != Overlay.None) DrawOverlay();
            DrawTooltip();
        }

        public void ToolkitNewScenario() => ResetScenario(game?.Scenario?.Id);
        public void ToolkitNewScenario(string scenarioId) => ResetScenario(scenarioId);
        public void ToolkitBeginDecision() => decisionStartedAt = Time.realtimeSinceStartup;
        public void ToolkitSave() => SaveOperation();
        public void ToolkitLoad() => LoadOperation();
        public void ToolkitExport() => ExportPlaytest();
        public string ToolkitActivationReason() => CurrentActivationExplanation();
        public string ToolkitLegalAlternatives() => LegalAlternatives();
        public void ToolkitRecordChoice(string action, string mode) => telemetry.RecordChoice(game, game.Active, "ActionChosen", action, mode, LegalAlternatives());
        public void ToolkitSetOperationMode(OperationMode mode, Side playerSide = Side.Blue)
        {
            operationMode = mode;
            humanSide = playerSide;
        }

        public AiDecision ToolkitChooseAiDecision() => IsAiTurn ? PrototypeAiCommander.Choose(game) : null;

        public string ToolkitExecuteAiTurn() => ToolkitExecuteAiTurn(null, null, null);

        public string ToolkitExecuteAiTurn(AiDecision preparedDecision, Reaction? selectedReaction, HexCoord? evadeDestination)
        {
            lastActionSucceeded = false;
            if (!IsAiTurn) return "No AI-controlled formation is Ready.";
            FormationState actor = game.Active;
            string alternatives = LegalAlternatives();
            PlaytestRecorder.Observation before = telemetry.Observe(game, actor);
            AiDecision decision = preparedDecision ?? PrototypeAiCommander.Choose(game);
            if (decision == null) return "AI could not form a legal decision.";
            telemetry.RecordChoice(game, actor, "AiActionChosen", decision.Action.ToString(), decision.ModeName, alternatives);
            if (!PrototypeAiCommander.Execute(game, decision, selectedReaction, evadeDestination, out string message))
            {
                var fallback = new AiDecision { Action = ActionKind.Hold, Rationale = "Fallback after the preferred AI action became illegal." };
                if (!PrototypeAiCommander.Execute(game, fallback, out message)) { Toast(message); return message; }
                decision = fallback;
            }
            lastActionSucceeded = true;
            telemetry.RecordAction(game, before, actor, decision.Action.ToString(), decision.ModeName, decision.TargetId ?? decision.Hex.ToString(), alternatives, DecisionSeconds(), decision.Rationale, message);
            pending = PendingAction.None;
            inspected = game.Active;
            Toast("OPFOR: " + message);
            AfterSuccessfulAction(actor.Side);
            // AI-owned draws resolve privately, but must never discard a pending human reveal.
            while (game.PendingEntropyEffectFor(actor.Side) != null) game.ConsumePendingEntropyEffect(actor.Side);
            return message;
        }
        public void ToolkitAcknowledgeEntropyEffect(Side side) => game?.ConsumePendingEntropyEffect(side);
        public string ToolkitRespondToEntropy(FormationState formation, string cardId)
        {
            if (game.RespondToEntropy(formation, cardId, out string message)) lastActionSucceeded = true;
            else lastActionSucceeded = false;
            Toast(message);
            return message;
        }

        public string ToolkitPlayResponse(string cardId, FormationState formation = null, ContactState contact = null, HexCoord? hex = null, ActionKind? mission = null)
        {
            lastActionSucceeded = game.PlayCommandResponse(game.Active.Side, cardId, formation, contact, hex, mission, out string message);
            Toast(message);
            return message;
        }
        public void ToolkitSetSettings(bool agePenalty, bool audio, bool contrast, bool reduceMotion, bool showHexGrid)
        {
            ageTwoTargetingPenalty = agePenalty;
            audioEnabled = audio;
            highContrast = contrast;
            reducedMotion = reduceMotion;
            hexGridVisible = showHexGrid;
            game.AgeTwoTargetingPenalty = agePenalty;
            AudioListener.volume = audio ? 1 : 0;
            SaveSettings();
        }

        public void ToolkitToggleHexGrid()
        {
            hexGridVisible = !hexGridVisible;
            SaveSettings();
        }

        public string ToolkitMove(HexCoord destination, MoveMode mode)
        {
            lastActionSucceeded = false;
            pending = mode == MoveMode.Cautious ? PendingAction.MoveCautious : mode == MoveMode.HighTempo ? PendingAction.MoveHigh : PendingAction.MoveNormal;
            ExecuteMove(destination);
            return toast;
        }

        public string ToolkitSearch(FormationState target, SearchMode mode)
        {
            lastActionSucceeded = false;
            pending = mode == SearchMode.Passive ? PendingAction.SearchPassive : mode == SearchMode.Active ? PendingAction.SearchActive : PendingAction.SearchFocused;
            ExecuteSearch(target);
            return toast;
        }

        public string ToolkitSearchHex(HexCoord center, SearchMode mode)
        {
            lastActionSucceeded = false;
            pending = mode == SearchMode.Passive ? PendingAction.SearchPassive : mode == SearchMode.Active ? PendingAction.SearchActive : PendingAction.SearchFocused;
            ExecuteSearchArea(center);
            return toast;
        }

        public string ToolkitStrike(FormationState target, Salvo salvo, Reaction reaction = Reaction.Defend, HexCoord? evadeDestination = null)
        {
            lastActionSucceeded = false;
            pending = salvo == Salvo.Light ? PendingAction.StrikeLight : salvo == Salvo.Standard ? PendingAction.StrikeStandard : PendingAction.StrikeHeavy;
            ExecuteStrike(target, true, reaction, evadeDestination);
            return toast;
        }

        public string ToolkitHold() { lastActionSucceeded = false; ExecuteHold(); return toast; }
        public string ToolkitPatrol(HexCoord center, PatrolPosture posture, FormationState protectedFormation = null)
        {
            lastActionSucceeded = false;
            FormationState actor = game.Active;
            PlaytestRecorder.Observation before = telemetry.Observe(game, actor);
            string alternatives = LegalAlternatives();
            lastActionSucceeded = game.Patrol(actor, center, posture, protectedFormation, out string message);
            if (lastActionSucceeded)
            {
                telemetry.RecordAction(game, before, actor, "Patrol", posture.ToString(), protectedFormation?.Id ?? center.ToString(), alternatives, DecisionSeconds(), $"Time 1; radius {Rules.PatrolRadius}; one interception", message);
                pending = PendingAction.None; inspected = game.Active; AfterSuccessfulAction(actor.Side);
            }
            else telemetry.RecordRejected(game, actor, "Patrol", posture.ToString(), center.ToString(), message, DecisionSeconds());
            Toast(message);
            return message;
        }
        public string ToolkitSupport(FormationState recipient, SupportKind kind)
        {
            lastActionSucceeded = false;
            FormationState actor = game.Active;
            PlaytestRecorder.Observation before = telemetry.Observe(game, actor);
            string alternatives = LegalAlternatives();
            lastActionSucceeded = game.Support(actor, recipient, kind, out string message);
            if (lastActionSucceeded)
            {
                telemetry.RecordAction(game, before, actor, "Support", kind.ToString(), recipient?.Id ?? string.Empty, alternatives, DecisionSeconds(), $"Time 1; range {Rules.SupportRange}; +1 until used", message);
                pending = PendingAction.None; inspected = game.Active; AfterSuccessfulAction(actor.Side);
            }
            else telemetry.RecordRejected(game, actor, "Support", kind.ToString(), recipient?.Id ?? string.Empty, message, DecisionSeconds());
            Toast(message);
            return message;
        }
        public string ToolkitReplenish(string destructionCardId = null)
        {
            lastActionSucceeded = false;
            FormationState actor = game.Active;
            PlaytestRecorder.Observation before = telemetry.Observe(game, actor);
            string alternatives = LegalAlternatives();
            string preview = game.ReplenishmentPreview(actor);
            lastActionSucceeded = game.Replenish(actor, destructionCardId, out string message);
            if (lastActionSucceeded)
            {
                telemetry.RecordAction(game, before, actor, "Replenish", destructionCardId ?? "Sustainment", actor.Position.ToString(), alternatives, DecisionSeconds(), $"Base Time 3; {preview}", message);
                pending = PendingAction.None; inspected = game.Active; AfterSuccessfulAction(actor.Side);
            }
            else telemetry.RecordRejected(game, actor, "Replenish", destructionCardId ?? "Sustainment", actor.Position.ToString(), message, DecisionSeconds());
            Toast(message);
            return message;
        }
        public string ToolkitAssignStandingMission(FormationState formation, ActionKind task, MissionObjectiveKind objective, string objectiveId, HexCoord objectiveHex, MissionPosture posture, MissionTrigger trigger)
        {
            lastActionSucceeded = game.AssignStandingMission(formation, task, objective, objectiveId, objectiveHex, posture, trigger, out string message);
            telemetry.RecordChoice(game, formation, lastActionSucceeded ? "MissionChanged" : "MissionChangeRejected", task.ToString(), $"{objective}/{posture}/{trigger}", LegalAlternatives());
            Toast(message);
            return message;
        }
        public string ToolkitPushThrough()
        {
            lastActionSucceeded = game.PushThrough(game.Active, out string message);
            telemetry.RecordChoice(game, game.Active, lastActionSucceeded ? "CommandStrainMarked" : "PushThroughRejected", "PushThrough", game.Sides[game.Active.Side].CommandStrain.ToString(), LegalAlternatives());
            Toast(message);
            return message;
        }
        public string ToolkitRestoreCommand()
        {
            lastActionSucceeded = false;
            FormationState actor = game.Active;
            PlaytestRecorder.Observation before = telemetry.Observe(game, actor);
            string alternatives = LegalAlternatives();
            lastActionSucceeded = game.RestoreCommand(actor, out string message);
            if (lastActionSucceeded)
            {
                telemetry.RecordAction(game, before, actor, "RestoreCommand", "HQRecovery", actor.Position.ToString(), alternatives, DecisionSeconds(), "Recover action at logistics; remove up to 2 Command Strain", message);
                pending = PendingAction.None; inspected = game.Active; AfterSuccessfulAction(actor.Side);
            }
            else telemetry.RecordRejected(game, actor, "RestoreCommand", "HQRecovery", actor.Position.ToString(), message, DecisionSeconds());
            Toast(message);
            return message;
        }
        public string ToolkitRecover() { lastActionSucceeded = false; ExecuteRecover(); return toast; }
        public string ToolkitRecover(EntropySource source, string cardId)
        {
            lastActionSucceeded = game.Recover(game.Active, source, cardId, out string message);
            Toast(message);
            return message;
        }

        private void EnsureStyles()
        {
            if (body != null) return;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Bahnschrift", "Segoe UI", "Arial" }, 18);
            title = Style(32, FontStyle.Bold, new Color(.92f, .96f, .98f), TextAnchor.MiddleLeft);
            eyebrow = Style(12, FontStyle.Bold, new Color(.95f, .67f, .24f), TextAnchor.MiddleLeft);
            panelTitle = Style(18, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            body = Style(16, FontStyle.Normal, new Color(.82f, .88f, .91f), TextAnchor.UpperLeft); body.wordWrap = true;
            small = Style(13, FontStyle.Normal, new Color(.61f, .72f, .77f), TextAnchor.UpperLeft); small.wordWrap = true;
            tiny = Style(11, FontStyle.Bold, new Color(.68f, .79f, .83f), TextAnchor.MiddleCenter);
            center = Style(15, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            badge = Style(12, FontStyle.Bold, new Color(.9f, .95f, .97f), TextAnchor.MiddleCenter);
            button = Style(13, FontStyle.Bold, new Color(.79f, .88f, .91f), TextAnchor.MiddleCenter);
            button.normal.background = MakeTexture(new Color(.055f, .13f, .17f, .98f));
            button.hover.background = MakeTexture(new Color(.08f, .25f, .3f, .98f));
            buttonActive = new GUIStyle(button); buttonActive.normal.background = MakeTexture(new Color(.08f, .43f, .49f, 1)); buttonActive.normal.textColor = Color.white;
        }

        private GUIStyle Style(int size, FontStyle style, Color color, TextAnchor alignment)
        {
            return new GUIStyle { font = font, fontSize = size, fontStyle = style, normal = { textColor = color }, alignment = alignment, padding = new RectOffset(8, 8, 4, 4) };
        }

        private Texture2D MakeTexture(Color color)
        {
            var texture = new Texture2D(1, 1); texture.SetPixel(0, 0, color); texture.Apply(); return texture;
        }

        private void DrawBackground()
        {
            Fill(new Rect(0, 0, ReferenceWidth, ReferenceHeight), new Color(.015f, .045f, .065f));
            Fill(new Rect(0, 0, ReferenceWidth, 76), new Color(.02f, .07f, .095f, .98f));
            Line(new Vector2(0, 76), new Vector2(ReferenceWidth, 76), new Color(.14f, .57f, .64f, .35f), 1);
        }

        private void DrawFrontEnd()
        {
            if (mapTexture != null)
            {
                GUI.DrawTexture(new Rect(0, 76, ReferenceWidth, ReferenceHeight - 76), mapTexture, ScaleMode.ScaleAndCrop);
                Fill(new Rect(0, 76, ReferenceWidth, ReferenceHeight - 76), new Color(.006f, .035f, .052f, .66f));
            }
            GUI.Label(new Rect(34, 11, 720, 20), "OPERATIONAL NAVAL WARGAME  •  DEVELOPMENT BUILD", eyebrow);
            GUI.Label(new Rect(34, 27, 720, 42), "SEA OF UNCERTAINTY", title);

            switch (screen)
            {
                case AppScreen.MainMenu: DrawMainMenu(); break;
                case AppScreen.ScenarioSelect: DrawScenarioSelect(); break;
                case AppScreen.Briefing: DrawBriefing(); break;
                case AppScreen.Setup: DrawSetup(); break;
                case AppScreen.PassDevice: DrawPassDevice(); break;
                case AppScreen.Results: DrawResults(); break;
            }
        }

        private void DrawMainMenu()
        {
            Rect hero = new Rect(118, 182, 830, 650);
            Panel(hero, new Color(.015f, .065f, .09f, .95f));
            GUI.Label(new Rect(hero.x + 48, hero.y + 48, 700, 24), "COMMAND • INFORMATION • TEMPO", eyebrow);
            GUI.Label(new Rect(hero.x + 43, hero.y + 82, 730, 130), "OPERATE INSIDE\nUNCERTAINTY", new GUIStyle(title) { fontSize = 54, wordWrap = true, alignment = TextAnchor.UpperLeft });
            GUI.Label(new Rect(hero.x + 50, hero.y + 230, 680, 95), "You are not trying to eliminate uncertainty. You are trying to operate better than your opponent inside it.", new GUIStyle(body) { fontSize = 21 });
            if (GUI.Button(new Rect(hero.x + 50, hero.y + 370, 310, 62), "BEGIN OPERATION", buttonActive)) screen = AppScreen.ScenarioSelect;
            if (File.Exists(SavePath) && GUI.Button(new Rect(hero.x + 380, hero.y + 370, 260, 62), "CONTINUE", button)) LoadOperation();
            if (GUI.Button(new Rect(hero.x + 50, hero.y + 446, 200, 48), "SETTINGS", button)) overlay = Overlay.Settings;
            if (GUI.Button(new Rect(hero.x + 264, hero.y + 446, 200, 48), "FIELD MANUAL", button)) overlay = Overlay.Rules;
            GUI.Label(new Rect(hero.x + 50, hero.y + 546, 690, 55), "Prototype milestone P0 • Local pass-and-play • Multiplayer intentionally deferred", small);

            Rect doctrine = new Rect(1050, 236, 700, 480);
            Panel(doctrine, new Color(.018f, .07f, .095f, .94f));
            GUI.Label(new Rect(doctrine.x + 38, doctrine.y + 34, 620, 30), "THE OPERATIONAL LOOP", panelTitle);
            string loop = "01  READY     Find the formation that can act now.\n\n02  CHOOSE    Trade time for position, information, or force.\n\n03  REACT      The enemy protects capability or geometry.\n\n04  RESOLVE   Bounded luck reflects the position you created.\n\n05  SCHEDULE  Accept what the enemy can do before you return.";
            GUI.Label(new Rect(doctrine.x + 38, doctrine.y + 86, 620, 320), loop, new GUIStyle(body) { fontSize = 18 });
        }

        private void DrawScenarioSelect()
        {
            GUI.Label(new Rect(120, 126, 700, 50), "SELECT OPERATION", new GUIStyle(title) { fontSize = 38 });
            GUI.Label(new Rect(120, 174, 900, 35), "Choose a test scenario. Additional operations will unlock as the rules mature.", body);
            Rect card = new Rect(120, 244, 760, 570);
            Panel(card, new Color(.018f, .075f, .1f, .97f));
            Fill(new Rect(card.x, card.y, card.width, 8), new Color(.91f, .58f, .18f));
            GUI.Label(new Rect(card.x + 36, card.y + 38, 680, 22), "OPERATION 01  •  CORE-LOOP TEST", eyebrow);
            GUI.Label(new Rect(card.x + 31, card.y + 76, 690, 52), "MERIDIAN VEIL", new GUIStyle(title) { fontSize = 40 });
            GUI.Label(new Rect(card.x + 36, card.y + 143, 680, 72), "Two naval forces converge on the Inner Sea. Control the central waters at Time 16 while preserving combat capability.", new GUIStyle(body) { fontSize = 19 });
            string details = "MAP       12 × 10 ARCHIPELAGO\nFORCES    4 BLUE / 4 RED\nHORIZON   OPERATIONAL TIME 16\nFOCUS      READY TIME • CONTACTS • COMBAT\nMODE       LOCAL PASS-AND-PLAY";
            GUI.Label(new Rect(card.x + 36, card.y + 248, 680, 170), details, body);
            if (GUI.Button(new Rect(card.x + 36, card.y + 454, 310, 62), "REVIEW BRIEFING", buttonActive))
            {
                ResetScenario();
                screen = AppScreen.Briefing;
            }
            if (GUI.Button(new Rect(120, 846, 180, 48), "BACK", button)) screen = AppScreen.MainMenu;

            Rect locked = new Rect(980, 244, 560, 240);
            Panel(locked, new Color(.018f, .06f, .075f, .8f));
            GUI.Label(new Rect(locked.x + 30, locked.y + 30, 490, 30), "FUTURE OPERATION", panelTitle);
            GUI.Label(new Rect(locked.x + 30, locked.y + 80, 490, 110), "Reserved for deception, logistics, synchronization, and distinct submarine behavior after the core loop is validated.", body);
        }

        private void DrawBriefing()
        {
            Rect sheet = new Rect(150, 122, 1620, 820);
            Panel(sheet, new Color(.014f, .06f, .085f, .98f));
            GUI.Label(new Rect(sheet.x + 45, sheet.y + 36, 650, 22), "OPERATION ORDER 01  •  MERIDIAN VEIL", eyebrow);
            GUI.Label(new Rect(sheet.x + 40, sheet.y + 68, 720, 56), "CONTROL THE INNER SEA", new GUIStyle(title) { fontSize = 39 });
            GUI.Label(new Rect(sheet.x + 45, sheet.y + 134, 690, 100), "Both forces are maneuvering through a contested archipelago. Contacts are incomplete and will age as the operational clock advances.", new GUIStyle(body) { fontSize = 19 });

            GUI.Label(new Rect(sheet.x + 45, sheet.y + 266, 680, 30), "MISSION", panelTitle);
            GUI.Label(new Rect(sheet.x + 45, sheet.y + 304, 680, 120), "Control the Inner Sea objective at Time 16. Preserve your carrier group in combat-capable condition. Destruction matters only insofar as it supports those aims.", body);
            GUI.Label(new Rect(sheet.x + 45, sheet.y + 452, 680, 30), "INITIAL INTELLIGENCE", panelTitle);
            GUI.Label(new Rect(sheet.x + 45, sheet.y + 490, 680, 120), "Each side begins with one imperfect Contact. The map displays only the active side’s formations and information picture.", body);

            GUI.Label(new Rect(sheet.x + 850, sheet.y + 82, 650, 30), "PROVISIONAL SCORING", panelTitle);
            string scoring = "5 VP   Sole control within one hex of the Inner Sea at T16\n\n3 VP   Designated carrier group remains combat capable\n\n2 VP   Each enemy formation Destroyed\n\n1 VP   Each enemy formation Crippled\n\nTIE     Side closest to the objective, then preservation";
            GUI.Label(new Rect(sheet.x + 850, sheet.y + 130, 650, 290), scoring, body);
            GUI.Label(new Rect(sheet.x + 850, sheet.y + 452, 650, 30), "PROTOTYPE ASSUMPTIONS", panelTitle);
            string assumptions = $"Strike range: 3 hexes\nAge-2 targeting penalty: {(ageTwoTargetingPenalty ? "ON" : "OFF")}\nReaction: automatic Defend unless Orderly Withdrawal is prepared\nContact improvement: Location first\nEndurance: three major Actions";
            GUI.Label(new Rect(sheet.x + 850, sheet.y + 496, 650, 170), assumptions, body);
            if (GUI.Button(new Rect(sheet.x + 850, sheet.y + 694, 330, 64), "REVIEW DEPLOYMENT", buttonActive)) screen = AppScreen.Setup;
            if (GUI.Button(new Rect(sheet.x + 1196, sheet.y + 694, 190, 64), "SETTINGS", button)) overlay = Overlay.Settings;
            if (GUI.Button(new Rect(sheet.x + 45, sheet.y + 706, 180, 48), "BACK", button)) screen = AppScreen.ScenarioSelect;
        }

        private void DrawSetup()
        {
            Rect panel = new Rect(120, 116, 1680, 846);
            Panel(panel, new Color(.014f, .06f, .085f, .98f));
            GUI.Label(new Rect(panel.x + 42, panel.y + 30, 800, 22), "INITIAL SETUP  •  FIXED TEST DEPLOYMENT", eyebrow);
            GUI.Label(new Rect(panel.x + 37, panel.y + 62, 850, 55), "FORCES ENTER THE ARCHIPELAGO", new GUIStyle(title) { fontSize = 37 });
            GUI.Label(new Rect(panel.x + 42, panel.y + 122, 780, 68), "This first scenario uses fixed starting positions so paired playtests begin from identical conditions.", new GUIStyle(body) { fontSize = 18 });

            DrawSetupSide(new Rect(panel.x + 42, panel.y + 225, 700, 410), Side.Blue);
            DrawSetupSide(new Rect(panel.x + panel.width - 742, panel.y + 225, 700, 410), Side.Red);
            Rect objective = new Rect(panel.center.x - 150, panel.y + 280, 300, 190);
            Fill(objective, new Color(.08f, .13f, .13f, .95f));
            DrawRing(objective.center, 52, new Color(.95f, .63f, .21f), 3);
            GUI.Label(new Rect(objective.x + 20, objective.y + 118, objective.width - 40, 26), "INNER SEA  •  G6", new GUIStyle(tiny) { normal = { textColor = new Color(.95f, .68f, .27f) } });

            GUI.Label(new Rect(panel.x + 42, panel.y + 675, panel.width - 84, 48), "Setup is public. Once the operation begins, each side sees only its own formations and Contact picture.", new GUIStyle(body) { alignment = TextAnchor.MiddleCenter });
            if (GUI.Button(new Rect(panel.center.x - 190, panel.y + 748, 380, 62), "LOCK SETUP & BEGIN", buttonActive)) BeginOperation();
            if (GUI.Button(new Rect(panel.x + 42, panel.y + 756, 170, 48), "BACK", button)) screen = AppScreen.Briefing;
        }

        private void DrawSetupSide(Rect rect, Side side)
        {
            Fill(rect, new Color(.024f, .105f, .135f, .92f));
            Fill(new Rect(rect.x, rect.y, rect.width, 7), SideColor(side));
            GUI.Label(new Rect(rect.x + 28, rect.y + 24, rect.width - 56, 35), side + " ORDER OF BATTLE", new GUIStyle(panelTitle) { alignment = TextAnchor.MiddleCenter });
            float y = rect.y + 82;
            foreach (FormationState formation in game.Formations.Where(f => f.Side == side))
            {
                GUI.Label(new Rect(rect.x + 34, y, rect.width - 190, 26), formation.Name, new GUIStyle(body) { fontStyle = FontStyle.Bold });
                GUI.Label(new Rect(rect.x + rect.width - 145, y, 105, 26), $"HEX {formation.Position}", new GUIStyle(badge) { alignment = TextAnchor.MiddleRight });
                GUI.Label(new Rect(rect.x + 34, y + 27, rect.width - 68, 22), $"{formation.Kind}  •  Ready T{formation.ReadyTime:00}", small);
                y += 72;
            }
        }

        private void DrawPassDevice()
        {
            Rect panel = new Rect(490, 230, 940, 590);
            Panel(panel, new Color(.012f, .055f, .075f, .99f));
            Color sideColor = SideColor(handoffSide);
            Fill(new Rect(panel.x, panel.y, panel.width, 9), sideColor);
            GUI.Label(new Rect(panel.x + 70, panel.y + 70, panel.width - 140, 28), "SECURE INFORMATION HANDOFF", eyebrow);
            GUI.Label(new Rect(panel.x + 64, panel.y + 124, panel.width - 128, 70), $"{handoffSide.ToString().ToUpperInvariant()} COMMAND", new GUIStyle(title) { fontSize = 45, alignment = TextAnchor.MiddleCenter });
            GUI.Label(new Rect(panel.x + 100, panel.y + 224, panel.width - 200, 88), "Pass control to the indicated player. The operational map remains concealed until they confirm readiness.", new GUIStyle(body) { fontSize = 20, alignment = TextAnchor.MiddleCenter });
            if (game.Active != null) GUI.Label(new Rect(panel.x + 100, panel.y + 328, panel.width - 200, 42), $"NEXT READY  •  {game.Active.Name.ToUpperInvariant()}  •  T{game.Time:00}", new GUIStyle(panelTitle) { alignment = TextAnchor.MiddleCenter, normal = { textColor = sideColor } });
            if (GUI.Button(new Rect(panel.center.x - 190, panel.y + 430, 380, 70), "ASSUME COMMAND", buttonActive)) EnterGame();
        }

        private void DrawResults()
        {
            Rect panel = new Rect(300, 152, 1320, 770);
            Panel(panel, new Color(.012f, .055f, .075f, .98f));
            GUI.Label(new Rect(panel.x + 60, panel.y + 42, panel.width - 120, 24), "OPERATION COMPLETE  •  TIME 16", eyebrow);
            GUI.Label(new Rect(panel.x + 54, panel.y + 86, panel.width - 108, 60), resultSummary, new GUIStyle(title) { fontSize = 42, alignment = TextAnchor.MiddleCenter });
            DrawScoreBlock(new Rect(panel.x + 150, panel.y + 192, 390, 250), Side.Blue, blueFinalScore);
            DrawScoreBlock(new Rect(panel.x + panel.width - 540, panel.y + 192, 390, 250), Side.Red, redFinalScore);
            GUI.Label(new Rect(panel.x + 210, panel.y + 478, panel.width - 420, 95), "Scoring is provisional and exists to test whether maneuver, information, and preservation matter more than simple destruction.", new GUIStyle(body) { alignment = TextAnchor.MiddleCenter, fontSize = 19 });
            if (!string.IsNullOrEmpty(telemetry.LastExportDirectory)) GUI.Label(new Rect(panel.x + 120, panel.y + 568, panel.width - 240, 38), "PLAYTEST DATA EXPORTED  •  " + telemetry.LastExportDirectory, new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });
            if (GUI.Button(new Rect(panel.center.x - 465, panel.y + 630, 280, 64), "PLAYTEST FEEDBACK", buttonActive)) overlay = Overlay.Feedback;
            if (GUI.Button(new Rect(panel.center.x - 140, panel.y + 630, 280, 64), "PLAY AGAIN", button)) { ResetScenario(); screen = AppScreen.Briefing; }
            if (GUI.Button(new Rect(panel.center.x + 185, panel.y + 630, 280, 64), "MAIN MENU", button)) screen = AppScreen.MainMenu;
        }

        private void DrawScoreBlock(Rect rect, Side side, int score)
        {
            Fill(rect, new Color(.025f, .12f, .15f, .92f));
            Fill(new Rect(rect.x, rect.y, rect.width, 7), SideColor(side));
            GUI.Label(new Rect(rect.x + 20, rect.y + 30, rect.width - 40, 34), side.ToString().ToUpperInvariant(), new GUIStyle(panelTitle) { alignment = TextAnchor.MiddleCenter });
            GUI.Label(new Rect(rect.x + 20, rect.y + 78, rect.width - 40, 100), score.ToString(), new GUIStyle(title) { alignment = TextAnchor.MiddleCenter, fontSize = 72 });
            GUI.Label(new Rect(rect.x + 20, rect.y + 184, rect.width - 40, 30), "VICTORY POINTS", tiny);
        }

        private void DrawOverlay()
        {
            Fill(new Rect(0, 0, ReferenceWidth, ReferenceHeight), new Color(0, 0, 0, .72f));
            Rect rect = new Rect(500, 160, 920, 760);
            Panel(rect, new Color(.018f, .07f, .095f, .995f));
            string overlayTitle = overlay == Overlay.Pause ? "OPERATION PAUSED" : overlay == Overlay.Settings ? "SETTINGS" : overlay == Overlay.EventLog ? "EVENT INSPECTOR" : overlay == Overlay.ConfirmHeavy ? "CONFIRM HEAVY SALVO" : overlay == Overlay.Feedback ? "PLAYTEST DEBRIEF" : "FIELD MANUAL";
            GUI.Label(new Rect(rect.x + 44, rect.y + 35, 760, 48), overlayTitle, new GUIStyle(title) { fontSize = 36 });
            if (overlay == Overlay.Pause)
            {
                GUI.Label(new Rect(rect.x + 44, rect.y + 100, 810, 60), "Manage the current local operation. Saving preserves both sides' hidden state.", body);
                if (GUI.Button(new Rect(rect.center.x - 190, rect.y + 180, 380, 54), "RESUME OPERATION", buttonActive)) overlay = Overlay.None;
                if (GUI.Button(new Rect(rect.center.x - 190, rect.y + 246, 380, 54), "SAVE OPERATION", button)) SaveOperation();
                GUI.enabled = File.Exists(SavePath);
                if (GUI.Button(new Rect(rect.center.x - 190, rect.y + 312, 380, 54), "LOAD SAVED OPERATION", button)) LoadOperation();
                GUI.enabled = true;
                if (GUI.Button(new Rect(rect.center.x - 190, rect.y + 378, 380, 54), "EXPORT PLAYTEST DATA", button)) ExportPlaytest();
                if (GUI.Button(new Rect(rect.center.x - 190, rect.y + 444, 380, 54), "SETTINGS", button)) overlay = Overlay.Settings;
                if (GUI.Button(new Rect(rect.center.x - 190, rect.y + 510, 380, 54), "RESTART OPERATION", button)) { ResetScenario(); screen = AppScreen.Briefing; overlay = Overlay.None; }
                if (GUI.Button(new Rect(rect.center.x - 190, rect.y + 576, 380, 54), "RETURN TO MAIN MENU", button)) { screen = AppScreen.MainMenu; overlay = Overlay.None; }
                GUI.Label(new Rect(rect.x + 44, rect.y + 645, 810, 40), toast, new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });
            }
            else if (overlay == Overlay.Settings)
            {
                GUI.Label(new Rect(rect.x + 44, rect.y + 120, 520, 30), "GAMEPLAY", panelTitle);
                bool newPenalty = GUI.Toggle(new Rect(rect.x + 48, rect.y + 170, 500, 38), ageTwoTargetingPenalty, "  Age-2 Contacts receive −1 targeting");
                if (newPenalty != ageTwoTargetingPenalty) { ageTwoTargetingPenalty = newPenalty; game.AgeTwoTargetingPenalty = ageTwoTargetingPenalty; SaveSettings(); }
                bool newAudio = GUI.Toggle(new Rect(rect.x + 48, rect.y + 230, 500, 38), audioEnabled, "  Audio enabled");
                if (newAudio != audioEnabled) { audioEnabled = newAudio; AudioListener.volume = audioEnabled ? 1 : 0; SaveSettings(); }
                GUI.Label(new Rect(rect.x + 44, rect.y + 310, 520, 30), "DISPLAY & ACCESSIBILITY", panelTitle);
                if (GUI.Button(new Rect(rect.x + 48, rect.y + 354, 330, 44), Screen.fullScreen ? "SWITCH TO WINDOWED" : "SWITCH TO FULLSCREEN", button)) Screen.fullScreen = !Screen.fullScreen;
                bool newContrast = GUI.Toggle(new Rect(rect.x + 48, rect.y + 422, 500, 38), highContrast, "  High-contrast side colors");
                if (newContrast != highContrast) { highContrast = newContrast; SaveSettings(); }
                bool newMotion = GUI.Toggle(new Rect(rect.x + 48, rect.y + 482, 500, 38), reducedMotion, "  Reduced motion");
                if (newMotion != reducedMotion) { reducedMotion = newMotion; SaveSettings(); }
                bool newGrid = GUI.Toggle(new Rect(rect.x + 48, rect.y + 542, 500, 38), hexGridVisible, "  Show hex grid on the map");
                if (newGrid != hexGridVisible) { hexGridVisible = newGrid; SaveSettings(); }
                GUI.Label(new Rect(rect.x + 44, rect.y + 602, 810, 65), "Settings persist between sessions, including the optional map grid.", body);
            }
            else if (overlay == Overlay.EventLog)
            {
                GUI.Label(new Rect(rect.x + 44, rect.y + 96, 810, 52), "Every entry records the operational Time, calculation or consequence, and scheduled return.", body);
                float eventY = rect.y + 165;
                foreach (string entry in game.Log.Take(8))
                {
                    Fill(new Rect(rect.x + 44, eventY, 810, 55), new Color(.028f, .12f, .15f, .9f));
                    GUI.Label(new Rect(rect.x + 56, eventY + 6, 786, 44), entry, small);
                    eventY += 64;
                }
            }
            else if (overlay == Overlay.ConfirmHeavy)
            {
                if (confirmationTarget == null) { overlay = Overlay.None; return; }
                CombatPreview preview = GetCombatPreview(game.Active, confirmationTarget, Salvo.Heavy, Reaction.Defend);
                ContactState contact = game.ContactFor(game.Active.Side, confirmationTarget.Id);
                GUI.Label(new Rect(rect.x + 44, rect.y + 104, 810, 70), "This commitment expends the formation's Heavy Salvo capability. It cannot be used again until Replenishment is implemented and available.", new GUIStyle(body) { fontSize = 18 });
                GUI.Label(new Rect(rect.x + 44, rect.y + 204, 810, 36), $"TARGET  {contact.Summary}  •  LAST KNOWN {contact.LastKnownPosition}", panelTitle);
                GUI.Label(new Rect(rect.x + 44, rect.y + 264, 810, 130), $"ATTACK {preview.Attack}  vs  DEFENSE {preview.Defense}\nDIFFERENCE {preview.Difference:+0;-0;0}  •  {preview.Band.ToString().ToUpperInvariant()} BAND\n\n{CombatOddsText(preview.Band)}", new GUIStyle(body) { alignment = TextAnchor.MiddleCenter, fontSize = 19 });
                GUI.Label(new Rect(rect.x + 44, rect.y + 430, 810, 54), $"TIME 2 → NEXT READY T{game.Time + 2 + (game.Active.Friction ? 1 : 0):00}", new GUIStyle(panelTitle) { alignment = TextAnchor.MiddleCenter });
                if (GUI.Button(new Rect(rect.center.x - 300, rect.y + 560, 280, 62), "CANCEL", button)) { confirmationTarget = null; overlay = Overlay.None; }
                if (GUI.Button(new Rect(rect.center.x + 20, rect.y + 560, 280, 62), "COMMIT HEAVY SALVO", buttonActive))
                {
                    FormationState target = confirmationTarget;
                    confirmationTarget = null;
                    overlay = Overlay.None;
                    ExecuteStrike(target, true);
                }
            }
            else if (overlay == Overlay.Feedback)
            {
                GUI.Label(new Rect(rect.x + 44, rect.y + 92, 810, 40), "Rate each quality from 1 (poor) to 5 (excellent), then record the moments that matter.", body);
                feedback.ReadyTimeClarity = RatingSelector(rect.x + 44, rect.y + 150, "Ready-Time clarity", feedback.ReadyTimeClarity);
                feedback.InformationImpact = RatingSelector(rect.x + 44, rect.y + 200, "Information affected decisions", feedback.InformationImpact);
                feedback.CombatFairness = RatingSelector(rect.x + 44, rect.y + 250, "Combat felt fair and explainable", feedback.CombatFairness);
                feedback.EntropyInterest = RatingSelector(rect.x + 44, rect.y + 300, "Entropy created interesting pressure", feedback.EntropyInterest);
                feedback.ObviousChoiceOccurred = GUI.Toggle(new Rect(rect.x + 500, rect.y + 150, 330, 30), feedback.ObviousChoiceOccurred, "  Obvious choice occurred");
                feedback.RepeatedRuleLookup = GUI.Toggle(new Rect(rect.x + 500, rect.y + 195, 330, 30), feedback.RepeatedRuleLookup, "  Repeated rules lookup");
                feedback.UnfairFeelingRoll = GUI.Toggle(new Rect(rect.x + 500, rect.y + 240, 330, 30), feedback.UnfairFeelingRoll, "  Unfair-feeling die result");
                feedback.InactivePlayerLull = GUI.Toggle(new Rect(rect.x + 500, rect.y + 285, 330, 30), feedback.InactivePlayerLull, "  Inactive-player lull");
                GUI.Label(new Rect(rect.x + 44, rect.y + 365, 250, 24), "BEST DECISION", tiny);
                feedback.BestDecision = GUI.TextArea(new Rect(rect.x + 44, rect.y + 394, 250, 120), feedback.BestDecision, body);
                GUI.Label(new Rect(rect.x + 335, rect.y + 365, 250, 24), "MOST CONFUSING MOMENT", tiny);
                feedback.MostConfusingMoment = GUI.TextArea(new Rect(rect.x + 335, rect.y + 394, 250, 120), feedback.MostConfusingMoment, body);
                GUI.Label(new Rect(rect.x + 626, rect.y + 365, 250, 24), "RULES ADJUSTMENT", tiny);
                feedback.RulesAdjustment = GUI.TextArea(new Rect(rect.x + 626, rect.y + 394, 250, 120), feedback.RulesAdjustment, body);
                if (GUI.Button(new Rect(rect.center.x - 180, rect.y + 570, 360, 60), "EXPORT DEBRIEF & DATA", buttonActive)) { ExportPlaytest(); overlay = Overlay.None; }
            }
            else
            {
                string manual = "READY TIME\nThe highlighted formation acts. Its action schedules when it returns.\n\nMOVE\nCautious 1 / Normal 2 / High Tempo 3 and Friction.\n\nSEARCH\nPassive +0 / Active +1 and Loud / Focused +2 and one Command Slot.\n\nSTRIKE\nLight +0 / Standard +1 / Heavy +2 and expend Heavy capability.\n\nCONTACTS\nLocation and Identity improve through Search. Age rises as Time advances.\n\nENTROPY\nFriction delays complex actions. Disruption harms Search. Destruction harms offense.";
                GUI.Label(new Rect(rect.x + 44, rect.y + 105, 830, 540), manual, new GUIStyle(body) { fontSize = 17 });
            }
            if (overlay != Overlay.Pause && overlay != Overlay.ConfirmHeavy && overlay != Overlay.Feedback && GUI.Button(new Rect(rect.center.x - 120, rect.yMax - 90, 240, 50), "CLOSE", buttonActive)) overlay = Overlay.None;
        }

        private int RatingSelector(float x, float y, string label, int value)
        {
            GUI.Label(new Rect(x, y, 280, 32), label, body);
            for (int i = 1; i <= 5; i++)
            {
                if (GUI.Button(new Rect(x + 285 + (i - 1) * 36, y, 30, 30), i.ToString(), value == i ? buttonActive : button)) value = i;
            }
            return value;
        }

        private void ResetScenario(string scenarioId = null)
        {
            game = new PrototypeGame(1978, ScenarioCatalog.Find(string.IsNullOrEmpty(scenarioId) ? "meridian-veil" : scenarioId));
            AttachMissionAudio(game);
            game.AgeTwoTargetingPenalty = ageTwoTargetingPenalty;
            if (telemetry == null) telemetry = new PlaytestRecorder();
            telemetry.StartNew(game);
            feedback = new PlaytestFeedback();
            inspected = game.Active;
            pending = PendingAction.None;
            toast = "Select the Ready formation's action. Every choice previews its cost.";
        }

        private void SaveOperation()
        {
            try
            {
                var save = new OperationSave { Game = game.CaptureState(), Playtest = telemetry.Session, Feedback = feedback, OperationMode = operationMode, HumanSide = humanSide };
                string json = JsonUtility.ToJson(save, true);
                File.WriteAllText(SavePath, json);
                Toast($"Operation saved at T{game.Time:00}.");
            }
            catch (Exception exception)
            {
                Toast("Save failed: " + exception.Message);
                Debug.LogException(exception);
            }
        }

        private void LoadOperation()
        {
            try
            {
                if (!File.Exists(SavePath)) { Toast("No saved operation was found."); return; }
                string json = File.ReadAllText(SavePath);
                OperationSave operation = JsonUtility.FromJson<OperationSave>(json);
                PrototypeGame.SaveData data = operation?.Game;
                if (data == null) data = JsonUtility.FromJson<PrototypeGame.SaveData>(json);
                ScenarioDefinition scenario = ScenarioCatalog.Find(string.IsNullOrEmpty(data.ScenarioId) ? "meridian-veil" : data.ScenarioId);
                var restored = new PrototypeGame(1978, scenario);
                restored.RestoreState(data);
                game = restored;
                AttachMissionAudio(game);
                if (telemetry == null) telemetry = new PlaytestRecorder();
                if (operation?.Playtest != null) telemetry.Restore(operation.Playtest); else telemetry.StartNew(game);
                feedback = operation?.Feedback ?? new PlaytestFeedback();
                operationMode = operation != null && operation.Version >= 4 ? operation.OperationMode : OperationMode.LocalHotseat;
                humanSide = operation != null && operation.Version >= 4 ? operation.HumanSide : Side.Blue;
                ageTwoTargetingPenalty = game.AgeTwoTargetingPenalty;
                inspected = game.Active;
                pending = PendingAction.None;
                overlay = Overlay.None;
                handoffSide = game.Active != null ? game.Active.Side : Side.Blue;
                screen = game.Time >= game.Scenario.Horizon ? AppScreen.Results : AppScreen.PassDevice;
                if (game.Time >= game.Scenario.Horizon) CompleteScenario();
                Toast($"Saved operation loaded at T{game.Time:00}.");
            }
            catch (Exception exception)
            {
                Toast("Load failed: " + exception.Message);
                Debug.LogException(exception);
            }
        }

        private void SaveSettings()
        {
            PlayerPrefs.SetInt("AgeTwoPenalty", ageTwoTargetingPenalty ? 1 : 0);
            PlayerPrefs.SetInt("AudioEnabled", audioEnabled ? 1 : 0);
            PlayerPrefs.SetInt("HighContrast", highContrast ? 1 : 0);
            PlayerPrefs.SetInt("ReducedMotion", reducedMotion ? 1 : 0);
            PlayerPrefs.SetInt("HexGridVisible", hexGridVisible ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void AttachMissionAudio(PrototypeGame current)
        {
            if (audioEventGame != null) { audioEventGame.ActionCompleted -= OnMissionCompleted; audioEventGame.CommandEvent -= OnCommandEvent; }
            audioEventGame = current;
            if (audioEventGame != null) { audioEventGame.ActionCompleted += OnMissionCompleted; audioEventGame.CommandEvent += OnCommandEvent; }
        }

        private void OnCommandEvent(FormationState formation, string category, string detail) => telemetry?.RecordCommandEvent(game, formation, category, detail);

        private void OnMissionCompleted(FormationState formation, ActionKind mission)
        {
            if (!audioEnabled || formation == null) return;
            if (operationMode == OperationMode.SoloVsAi && formation.Side != humanSide) return;
            missionAudio?.Play(mission);
        }

        private void OnDestroy()
        {
            if (audioEventGame != null) { audioEventGame.ActionCompleted -= OnMissionCompleted; audioEventGame.CommandEvent -= OnCommandEvent; }
        }

        private void BeginOperation()
        {
            ResetScenario();
            handoffSide = game.Active.Side;
            screen = AppScreen.PassDevice;
        }

        private void EnterGame()
        {
            decisionStartedAt = Time.realtimeSinceStartup;
            screen = AppScreen.Game;
        }

        private void ExportPlaytest()
        {
            try
            {
                string path = telemetry.Export(feedback);
                Toast("Playtest exported: " + path);
            }
            catch (Exception exception)
            {
                Toast("Export failed: " + exception.Message);
                Debug.LogException(exception);
            }
        }

        private void CompleteScenario()
        {
            HexCoord objective = game.Area.Objective;
            blueFinalScore = FinalScore(Side.Blue, objective);
            redFinalScore = FinalScore(Side.Red, objective);
            resultSummary = blueFinalScore == redFinalScore ? "OPERATION CONTESTED" : blueFinalScore > redFinalScore ? "BLUE OPERATIONAL VICTORY" : "RED OPERATIONAL VICTORY";
            telemetry.RecordScore(game, blueFinalScore, redFinalScore, resultSummary);
            ExportPlaytest();
            pending = PendingAction.None;
            screen = AppScreen.Results;
        }

        private int FinalScore(Side side, HexCoord objective)
        {
            List<FormationState> friendly = game.Formations.Where(f => f.Side == side && !f.IsDestroyed).ToList();
            List<FormationState> enemy = game.Formations.Where(f => f.Side != side).ToList();
            int friendlyRange = friendly.Count == 0 ? 99 : friendly.Min(f => HexCoord.Distance(f.Position, objective));
            int enemyRange = game.Formations.Where(f => f.Side != side && !f.IsDestroyed).Select(f => HexCoord.Distance(f.Position, objective)).DefaultIfEmpty(99).Min();
            int score = friendlyRange <= 1 && enemyRange > 1 ? 5 : 0;
            FormationState carrier = game.Formations.FirstOrDefault(f => f.Side == side && f.Kind == FormationKind.CarrierGroup);
            if (carrier != null && !carrier.IsDestroyed && carrier.Damage < DamageState.Crippled) score += 3;
            score += enemy.Count(f => f.Damage == DamageState.Destroyed) * 2;
            score += enemy.Count(f => f.Damage == DamageState.Crippled);
            return score;
        }

        private void AfterSuccessfulAction(Side actingSide)
        {
            if (game.Time >= game.Scenario.Horizon) { CompleteScenario(); return; }
            if (game.Active != null && game.Active.Side != actingSide)
            {
                handoffSide = game.Active.Side;
                screen = AppScreen.PassDevice;
            }
            else decisionStartedAt = Time.realtimeSinceStartup;
        }

        private void DrawTopBar()
        {
            GUI.Label(new Rect(28, 9, 650, 20), "OPERATIONAL NAVAL WARGAME  •  PROTOTYPE 0.1", eyebrow);
            GUI.Label(new Rect(28, 25, 650, 45), "SEA OF UNCERTAINTY", title);
            GUI.Label(new Rect(705, 12, 130, 18), "OPERATIONAL TIME", tiny);
            DrawBadge(new Rect(720, 32, 100, 34), $"T {game.Time:00}", new Color(.09f, .61f, .67f));
            if (game.Active != null)
            {
                GUI.Label(new Rect(860, 12, 260, 18), "NOW READY", tiny);
                GUI.Label(new Rect(860, 33, 340, 30), game.Active.Name.ToUpperInvariant(), panelTitle);
                SideState side = game.Sides[game.Active.Side];
                GUI.Label(new Rect(1280, 12, 190, 18), $"{game.Active.Side.ToString().ToUpperInvariant()} COMMAND", tiny);
                for (int i = 0; i < 3; i++) DrawBadge(new Rect(1295 + i * 48, 35, 36, 26), i < side.CommandSlots ? "◆" : "◇", i < side.CommandSlots ? new Color(.91f, .62f, .2f) : new Color(.2f, .28f, .31f));
                GUI.Label(new Rect(1510, 12, 210, 43), "READY → CHOOSE →\nRESOLVE → SCHEDULE", small);
                if (GUI.Button(new Rect(1730, 18, 74, 40), "RULES", button)) overlay = Overlay.Rules;
                if (GUI.Button(new Rect(1812, 18, 78, 40), "MENU", button)) overlay = Overlay.Pause;
            }
        }

        private void DrawRoster()
        {
            Rect rect = new Rect(18, 94, 250, 712);
            Panel(rect, new Color(.025f, .09f, .12f, .96f));
            GUI.Label(new Rect(rect.x + 16, rect.y + 12, 218, 24), "FORMATION TIMELINE", panelTitle);
            string activationReason = CurrentActivationExplanation();
            GUI.Label(new Rect(rect.x + 16, rect.y + 38, 218, 48), activationReason, small);
            SetTooltip(new Rect(rect.x + 10, rect.y + 34, rect.width - 20, 52), "Activation order: lowest Ready Time, then lower Entropy, then higher Command. The prototype uses stable ID as its reproducible final tie-break.");
            float y = rect.y + 88;
            foreach (FormationState formation in game.Formations.Where(f => !f.IsDestroyed).OrderBy(f => f.ReadyTime).ThenBy(f => f.EntropySources).ThenByDescending(f => f.EffectiveCommand))
            {
                bool active = formation == game.Active;
                bool selected = formation == inspected;
                Rect row = new Rect(rect.x + 10, y, rect.width - 20, 66);
                Fill(row, selected ? new Color(.07f, .25f, .29f, .96f) : new Color(.03f, .13f, .16f, .8f));
                if (active) Fill(new Rect(row.x, row.y, 4, row.height), SideColor(formation.Side));
                if (GUI.Button(row, GUIContent.none, GUIStyle.none)) { inspected = formation; pending = PendingAction.None; }
                GUI.Label(new Rect(row.x + 12, row.y + 7, 158, 22), formation.Name, new GUIStyle(body) { fontStyle = FontStyle.Bold });
                GUI.Label(new Rect(row.x + 12, row.y + 33, 155, 22), $"{formation.Kind}  •  {formation.Cohesion}", small);
                DrawBadge(new Rect(row.x + 176, row.y + 13, 42, 38), formation.ReadyTime.ToString("00"), active ? SideColor(formation.Side) : new Color(.13f, .25f, .28f));
                SetTooltip(row, $"{formation.Name}\nReady T{formation.ReadyTime:00} • Entropy {formation.EntropySources} • Command {formation.EffectiveCommand}\n{(active ? activationReason : "Click to inspect this friendly formation.")}");
                y += 72;
            }
        }

        private void DrawMap()
        {
            mapRect = new Rect(284, 94, 1240, 712);
            GUI.BeginGroup(mapRect);
            if (mapTexture != null) GUI.DrawTexture(new Rect(0, 0, mapRect.width, mapRect.height), mapTexture, ScaleMode.ScaleAndCrop);
            else Fill(new Rect(0, 0, mapRect.width, mapRect.height), new Color(.025f, .12f, .17f));
            Fill(new Rect(0, 0, mapRect.width, mapRect.height), new Color(.015f, .08f, .11f, .18f));
            ComputeHexCenters();
            DrawHexGrid();
            DrawObjective();
            DrawMapPieces();
            GUI.EndGroup();
            Line(new Vector2(mapRect.x, mapRect.y), new Vector2(mapRect.xMax, mapRect.y), new Color(.2f, .68f, .71f, .55f), 1);
        }

        private void ComputeHexCenters()
        {
            centers.Clear();
            float marginX = 58, marginY = 48;
            float dx = (mapRect.width - marginX * 2) / Mathf.Max(1, game.Area.Width - 1);
            float dy = (mapRect.height - marginY * 2 - dx * .25f) / Mathf.Max(1, game.Area.Height - 1);
            for (int q = 0; q < game.Area.Width; q++) for (int r = 0; r < game.Area.Height; r++)
                centers[new HexCoord(q, r)] = new Vector2(marginX + q * dx, marginY + r * dy + (q % 2 == 1 ? dy * .5f : 0));
        }

        private void DrawHexGrid()
        {
            Color color = new Color(.36f, .79f, .81f, .25f);
            foreach (var pair in centers)
            {
                Vector2 c = pair.Value;
                float radius = 35f;
                if (IsMovePending() && game.Active != null)
                {
                    int distance = HexCoord.Distance(game.Active.Position, pair.Key);
                    MoveMode mode = pending == PendingAction.MoveCautious ? MoveMode.Cautious : pending == PendingAction.MoveHigh ? MoveMode.HighTempo : MoveMode.Normal;
                    int allowance = Rules.MoveAllowance(game.Active, mode);
                    if (distance > 0 && distance <= allowance)
                    {
                        DrawRing(c, 30, new Color(.25f, .95f, .72f, .82f), 3);
                        GUI.Label(new Rect(c.x - 14, c.y - 13, 28, 26), distance.ToString(), new GUIStyle(tiny) { normal = { textColor = new Color(.65f, 1f, .85f) } });
                    }
                }
                Vector2 last = c + new Vector2(0, -radius);
                for (int i = 1; i <= 6; i++)
                {
                    float angle = -90 + i * 60;
                    Vector2 next = c + new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * radius;
                    Line(last, next, color, 1); last = next;
                }
                if (pair.Key.R == 0) GUI.Label(new Rect(c.x - 15, c.y - 31, 30, 18), ((char)('A' + pair.Key.Q)).ToString(), tiny);
            }
        }

        private void DrawObjective()
        {
            Vector2 c = centers[game.Area.Objective];
            DrawRing(c, 44, new Color(.94f, .61f, .2f, .8f), 2);
            DrawRing(c, 50, new Color(.94f, .61f, .2f, .22f), 1);
            GUI.Label(new Rect(c.x - 75, c.y + 48, 150, 20), "SCENARIO OBJECTIVE", tiny);
        }

        private void DrawMapPieces()
        {
            if (game.Active == null) return;
            Side viewer = game.Active.Side;
            foreach (FormationState formation in game.Formations.Where(f => !f.IsDestroyed && f.Side == viewer)) DrawFormation(formation);
            foreach (ContactState contact in game.Contacts.Where(c => c.Owner == viewer && !c.IsLost)) DrawContact(contact);
        }

        private void DrawFormation(FormationState formation)
        {
            Vector2 c = centers[formation.Position];
            bool active = formation == game.Active;
            bool selected = formation == inspected;
            if (active) DrawRing(c, 31, new Color(1f, .72f, .27f, .9f), 3);
            else if (selected) DrawRing(c, 29, Color.white, 2);
            Fill(new Rect(c.x - 24, c.y - 20, 48, 40), new Color(.025f, .12f, .16f, .96f));
            Fill(new Rect(c.x - 24, c.y - 20, 5, 40), SideColor(formation.Side));
            GUI.Label(new Rect(c.x - 18, c.y - 17, 37, 19), KindCode(formation.Kind), center);
            GUI.Label(new Rect(c.x - 44, c.y + 23, 88, 18), formation.Name.Replace("Group", "Grp"), tiny);
            if (formation.Friction) DrawBadge(new Rect(c.x + 18, c.y - 31, 18, 18), "F", new Color(.96f, .57f, .12f));
        }

        private void DrawContact(ContactState contact)
        {
            Vector2 c = centers[contact.LastKnownPosition];
            Color certainty = contact.Location == LocationQuality.High ? new Color(.96f, .31f, .28f) : contact.Location == LocationQuality.Medium ? new Color(.81f, .38f, .28f) : new Color(.65f, .48f, .32f);
            float radius = contact.Location == LocationQuality.High ? 23 : contact.Location == LocationQuality.Medium ? 30 : 38;
            bool eligible = IsSearchPending() && game.Active != null && HexCoord.Distance(game.Active.Position, contact.LastKnownPosition) <= Rules.SearchRange(CurrentSearchMode()) || IsStrikePending() && StrikeEligible(contact);
            if (eligible) DrawRing(c, radius + 8, new Color(.35f, 1f, .7f, .92f), 3);
            else if (IsStrikePending()) DrawRing(c, radius + 8, new Color(.55f, .2f, .2f, .72f), 2);
            DrawRing(c, radius, certainty, 2);
            Line(c + new Vector2(-radius, 0), c + new Vector2(radius, 0), certainty, 1);
            Line(c + new Vector2(0, -radius), c + new Vector2(0, radius), certainty, 1);
            string id = contact.Identity == IdentityQuality.Identified ? KindCode(game.Find(contact.TargetId).Kind) : "?";
            GUI.Label(new Rect(c.x - 17, c.y - 16, 34, 32), id, center);
            GUI.Label(new Rect(c.x - 75, c.y + radius + 2, 150, 18), $"CONTACT • A{contact.Age}", tiny);
            Rect hoverRect = new Rect(c.x - radius - 12, c.y - radius - 12, (radius + 12) * 2, (radius + 12) * 2);
            if (hoverRect.Contains(Event.current.mousePosition)) hoveredContact = contact;
            SetTooltip(hoverRect, $"{contact.Summary}\nLocation: targeting certainty. Identity: classification certainty. Age: elapsed Time since update.\nLast known: {contact.LastKnownPosition}\n{(eligible ? "Eligible for selected action" : IsStrikePending() ? "Outside Strike eligibility" : "Select Search or Strike to act")}");

            if (IsSearchPending() && game.Active != null)
            {
                GUI.Label(new Rect(c.x - 65, c.y - radius - 29, 130, 18), eligible ? $"SEARCH AREA R{Rules.SearchAreaRadius}" : "OUT OF RANGE", tiny);
            }
            else if (IsStrikePending() && StrikeEligible(contact))
            {
                FormationState previewTarget = game.Find(contact.TargetId);
                CombatPreview preview = GetCombatPreview(game.Active, previewTarget, CurrentSalvo(), Reaction.Defend);
                GUI.Label(new Rect(c.x - 65, c.y - radius - 29, 130, 18), preview.Band.ToString().ToUpperInvariant(), tiny);
            }
        }

        private void DrawDossier()
        {
            Rect rect = new Rect(1540, 94, 362, 712);
            Panel(rect, new Color(.025f, .09f, .12f, .97f));
            if (hoveredContact != null && (IsSearchPending() || IsStrikePending()))
            {
                DrawTargetPreview(rect, hoveredContact);
                return;
            }
            FormationState f = inspected ?? game.Active;
            if (f == null) return;
            GUI.Label(new Rect(rect.x + 18, rect.y + 14, 326, 18), f.Side == Side.Blue ? "BLUE FORMATION DOSSIER" : "RED FORMATION DOSSIER", eyebrow);
            GUI.Label(new Rect(rect.x + 18, rect.y + 39, 326, 52), f.Name.ToUpperInvariant(), title);
            GUI.Label(new Rect(rect.x + 18, rect.y + 91, 326, 24), $"{f.Kind}  •  HEX {f.Position}", body);
            DrawStat(rect.x + 18, rect.y + 132, "MOVE", f.EffectiveMove, new Color(.23f, .74f, .78f));
            DrawStat(rect.x + 128, rect.y + 132, "SEARCH", f.EffectiveSearch, new Color(.23f, .74f, .78f));
            DrawStat(rect.x + 238, rect.y + 132, "STRIKE", f.EffectiveStrike, new Color(.94f, .61f, .2f));
            DrawStat(rect.x + 18, rect.y + 205, "DEFENSE", f.EffectiveDefense, new Color(.41f, .76f, .61f));
            DrawStat(rect.x + 128, rect.y + 205, "SIGNATURE", f.Ratings.Signature + (f.Loud ? 1 : 0), new Color(.65f, .48f, .82f));
            DrawStat(rect.x + 238, rect.y + 205, "COMMAND", f.EffectiveCommand, new Color(.94f, .61f, .2f));
            GUI.Label(new Rect(rect.x + 18, rect.y + 288, 326, 24), "READINESS & CAPABILITY", panelTitle);
            string state = $"READY  T{f.ReadyTime:00}\nENDURANCE  {f.Endurance.ToString().ToUpperInvariant()}\nDAMAGE  {f.Damage.ToString().ToUpperInvariant()}\nWEAPONS  {(f.WeaponExpended ? "HEAVY EXPENDED" : "AVAILABLE")}";
            GUI.Label(new Rect(rect.x + 18, rect.y + 321, 326, 110), state, body);
            SetTooltip(new Rect(rect.x + 18, rect.y + 321, 326, 110), $"Ready Time: when the formation may act again.\nEndurance {f.Endurance}: operational staying power.\nDamage {f.Damage}: current physical impairment.\nWeapons: whether Heavy Salvo remains available.");
            GUI.Label(new Rect(rect.x + 18, rect.y + 441, 326, 24), "ENTROPY / COHESION", panelTitle);
            DrawEntropy(new Rect(rect.x + 18, rect.y + 478, 98, 50), "FRICTION", f.Friction, new Color(.96f, .57f, .12f));
            DrawEntropy(new Rect(rect.x + 126, rect.y + 478, 98, 50), "DISRUPTION", f.Disruption, new Color(.62f, .43f, .78f));
            DrawEntropy(new Rect(rect.x + 234, rect.y + 478, 110, 50), "DESTRUCTION", f.Destruction, new Color(.9f, .22f, .2f));
            GUI.Label(new Rect(rect.x + 18, rect.y + 541, 326, 26), f.Cohesion.ToUpperInvariant(), new GUIStyle(panelTitle) { normal = { textColor = f.EntropySources >= 2 ? new Color(.95f, .38f, .3f) : new Color(.42f, .8f, .65f) } });
            SetTooltip(new Rect(rect.x + 18, rect.y + 538, 326, 32), "Cohesion summarizes marked Entropy sources: 0–1 Cohesive, 2 Disrupted, 3 Disorganized.");
            GUI.Label(new Rect(rect.x + 18, rect.y + 580, 326, 92), $"Standing Mission\nTASK: {f.Mission}\nPOSTURE: Balanced\n{(f.OrderlyWithdrawalReady ? "REACTION: Orderly Withdrawal prepared" : "REACTION: Automatic Defend")}", small);
        }

        private void DrawTargetPreview(Rect rect, ContactState contact)
        {
            FormationState target = game.Find(contact.TargetId);
            GUI.Label(new Rect(rect.x + 18, rect.y + 14, 326, 18), "PRE-COMMITMENT TARGET PREVIEW", eyebrow);
            string targetName = contact.Identity == IdentityQuality.Identified && target != null ? target.Name.ToUpperInvariant() : "UNRESOLVED CONTACT";
            GUI.Label(new Rect(rect.x + 18, rect.y + 42, 326, 70), targetName, new GUIStyle(title) { fontSize = 27, wordWrap = true });
            GUI.Label(new Rect(rect.x + 18, rect.y + 114, 326, 72), $"{contact.Summary}\nLAST KNOWN  {contact.LastKnownPosition}", body);
            if (target == null) return;

            if (IsSearchPending())
            {
                SearchMode mode = CurrentSearchMode();
                int range = HexCoord.Distance(game.Active.Position, contact.LastKnownPosition);
                int modeModifier = game.SearchModifierFor(game.Active, mode);
                GUI.Label(new Rect(rect.x + 18, rect.y + 210, 326, 28), "AREA SEARCH PREVIEW", panelTitle);
                GUI.Label(new Rect(rect.x + 18, rect.y + 250, 326, 150), $"Area center         {contact.LastKnownPosition}\nCenter range         {range} / {Rules.SearchRange(mode)}\nFootprint radius     {Rules.SearchAreaRadius}\nSearch rating        {game.Active.EffectiveSearch:+0;-0;0}\nMode ({mode})      {modeModifier:+0;-0;0}", body);
                GUI.Label(new Rect(rect.x + 18, rect.y + 430, 326, 175), "Hidden Signature, Loud status, and exact position resolve privately. Success creates or improves Contacts. No detection still costs Time and advances the Ready queue.", small);
            }
            else
            {
                Salvo salvo = CurrentSalvo();
                CombatPreview preview = GetCombatPreview(game.Active, target, salvo, Reaction.Defend);
                GUI.Label(new Rect(rect.x + 18, rect.y + 210, 326, 28), "COMBAT CALCULATION", panelTitle);
                GUI.Label(new Rect(rect.x + 18, rect.y + 250, 326, 150), $"Strike rating      {game.Active.EffectiveStrike:+0;-0;0}\nSalvo ({salvo})      {Rules.SalvoModifier(salvo):+0;-0;0}\nTargeting            {Rules.TargetingModifier(contact, game.AgeTwoTargetingPenalty):+0;-0;0}\nATTACK                {preview.Attack}\nDEFENSE + Defend      {preview.Defense}\n────────────────\nDIFFERENCE            {preview.Difference:+0;-0;0}", body);
                GUI.Label(new Rect(rect.x + 18, rect.y + 420, 326, 38), preview.Band.ToString().ToUpperInvariant() + " BAND", new GUIStyle(title) { fontSize = 25, alignment = TextAnchor.MiddleCenter });
                GUI.Label(new Rect(rect.x + 18, rect.y + 472, 326, 100), CombatOddsText(preview.Band), new GUIStyle(body) { alignment = TextAnchor.MiddleCenter });
                GUI.Label(new Rect(rect.x + 18, rect.y + 590, 326, 70), salvo == Salvo.Heavy ? "Heavy Salvo expends the formation’s major offensive capability and requires confirmation." : "The defender chooses a legal Reaction after Strike commitment.", small);
            }
        }

        private void DrawProbability(Rect rect, string label, float percent, Color color)
        {
            Fill(rect, new Color(.03f, .13f, .16f, .95f));
            Fill(new Rect(rect.x, rect.yMax - 5, rect.width * Mathf.Clamp01(percent / 100f), 5), color);
            GUI.Label(new Rect(rect.x + 8, rect.y + 8, rect.width - 16, 24), label, tiny);
            GUI.Label(new Rect(rect.x + 8, rect.y + 28, rect.width - 16, 52), $"{percent:0}%", new GUIStyle(title) { fontSize = 32, alignment = TextAnchor.MiddleCenter });
        }

        private void DrawBottomDeck()
        {
            Rect actions = new Rect(18, 824, 1125, 238);
            Panel(actions, new Color(.025f, .09f, .12f, .98f));
            GUI.Label(new Rect(actions.x + 18, actions.y + 11, 260, 24), "CHOOSE AN ACTION", panelTitle);
            GUI.Label(new Rect(actions.x + 275, actions.y + 13, 820, 22), "The clock cost appears on every choice. Click a mode, then its destination or Contact.", small);
            float x = actions.x + 18, y = actions.y + 48;
            ActionButton(new Rect(x, y, 150, 48), "MOVE • 2", PendingAction.MoveNormal, new Color(.1f, .55f, .62f));
            ActionButton(new Rect(x + 160, y, 150, 48), "SEARCH • 2", PendingAction.SearchPassive, new Color(.1f, .55f, .62f));
            ActionButton(new Rect(x + 320, y, 150, 48), "STRIKE • 2", PendingAction.StrikeStandard, new Color(.86f, .42f, .19f));
            Rect recoverRect = new Rect(x + 480, y, 150, 48);
            Rect holdRect = new Rect(x + 640, y, 150, 48);
            if (GUI.Button(recoverRect, "RECOVER • 2", button)) ExecuteRecover();
            if (GUI.Button(holdRect, "HOLD • 1", button)) ExecuteHold();
            SetTooltip(recoverRect, "Spend 2 Time to clear recoverable Friction or Disruption, yielding operational tempo.");
            SetTooltip(holdRect, "Spend 1 Time, remain in place, and become Quiet.");
            GUI.Label(new Rect(x + 805, y, 280, 48), "PATROL • SUPPORT • SYNC\nqueued for the next rules slice", small);
            GUI.Label(new Rect(x, y + 63, 115, 20), "COMMITMENT", eyebrow);
            if (IsMovePending())
            {
                ModeButton(new Rect(x + 120, y + 61, 145, 34), "CAUTIOUS • 1 HEX", PendingAction.MoveCautious);
                ModeButton(new Rect(x + 275, y + 61, 145, 34), "NORMAL • 2 HEX", PendingAction.MoveNormal);
                ModeButton(new Rect(x + 430, y + 61, 170, 34), "HIGH TEMPO • 3 + F", PendingAction.MoveHigh);
            }
            else if (IsSearchPending())
            {
                ModeButton(new Rect(x + 120, y + 61, 145, 34), "PASSIVE • +0", PendingAction.SearchPassive);
                ModeButton(new Rect(x + 275, y + 61, 145, 34), "ACTIVE • +1 LOUD", PendingAction.SearchActive);
                ModeButton(new Rect(x + 430, y + 61, 170, 34), "FOCUSED • +2 / 1◆", PendingAction.SearchFocused);
            }
            else if (IsStrikePending())
            {
                ModeButton(new Rect(x + 120, y + 61, 145, 34), "LIGHT • +0", PendingAction.StrikeLight);
                ModeButton(new Rect(x + 275, y + 61, 145, 34), "STANDARD • +1", PendingAction.StrikeStandard);
                ModeButton(new Rect(x + 430, y + 61, 170, 34), "HEAVY • +2 / EXP", PendingAction.StrikeHeavy);
            }
            string prompt = pending == PendingAction.None ? "Select an action." : IsMovePending() ? "Select a destination hex." : "Select an enemy Contact marker.";
            string preview = ActionPreviewText();
            GUI.Label(new Rect(x, y + 106, 1060, 54), prompt + (string.IsNullOrEmpty(preview) ? string.Empty : "\n" + preview), new GUIStyle(body) { fontStyle = FontStyle.Bold, normal = { textColor = new Color(.95f, .68f, .26f) } });
            string feedback = Time.unscaledTime < toastUntil ? toast : "Prototype assumptions: Strike range 3; Age-2 targeting penalty ON; reactions default to Defend.";
            GUI.Label(new Rect(x, y + 166, 1060, 28), feedback, small);

            Rect log = new Rect(1160, 824, 742, 238);
            Panel(log, new Color(.025f, .09f, .12f, .98f));
            GUI.Label(new Rect(log.x + 18, log.y + 11, 260, 24), "AFTER-ACTION FEED", panelTitle);
            if (GUI.Button(new Rect(log.xMax - 138, log.y + 9, 120, 30), "INSPECT", button)) overlay = Overlay.EventLog;
            float ly = log.y + 45;
            foreach (string entry in game.Log.Take(5)) { GUI.Label(new Rect(log.x + 18, ly, log.width - 36, 34), entry, small); ly += 36; }
        }

        private string ActionPreviewText()
        {
            if (pending == PendingAction.None || game.Active == null) return string.Empty;
            int baseTime;
            string consequence;
            if (IsMovePending())
            {
                MoveMode mode = CurrentMoveMode();
                baseTime = 2;
                int distance = Rules.MoveAllowance(game.Active, mode);
                consequence = mode == MoveMode.Cautious ? $"up to {distance} hex • Signature −1" : mode == MoveMode.HighTempo ? $"up to {distance} hex • Signature +1 • mark Friction" : $"up to {distance} hex • no additional cost";
            }
            else if (IsSearchPending())
            {
                SearchMode mode = CurrentSearchMode();
                baseTime = 2;
                consequence = mode == SearchMode.Passive ? "concealment preserved" : mode == SearchMode.Active ? "+1 Search • become Loud" : "+2 Search • occupy 1 Command Slot during resolution";
            }
            else
            {
                Salvo salvo = CurrentSalvo();
                baseTime = 2;
                consequence = salvo == Salvo.Heavy ? "+2 Attack • expend Heavy capability • confirmation required" : salvo == Salvo.Standard ? "+1 Attack" : "+0 Attack • conserve weapons";
            }
            int frictionTime = game.Active.Friction ? 1 : IsMovePending() && CurrentMoveMode() == MoveMode.HighTempo ? 1 : 0;
            int total = baseTime + frictionTime;
            string endurance;
            if (game.Active.MajorActions + 1 >= 3)
            {
                Endurance next = game.Active.Endurance < Endurance.Critical ? (Endurance)((int)game.Active.Endurance + 1) : Endurance.Critical;
                endurance = $"Endurance {game.Active.Endurance} → {next}";
            }
            else endurance = $"Endurance action {game.Active.MajorActions + 1}/3";
            return $"TIME {baseTime}{(frictionTime > 0 ? " +1 Friction" : string.Empty)} → NEXT READY T{game.Time + total:00}  •  {consequence}  •  {endurance}";
        }

        private void HandleMapInput(Event e)
        {
            if (e.type != EventType.MouseDown || e.button != 0 || !mapRect.Contains(e.mousePosition)) return;
            Vector2 local = e.mousePosition - mapRect.position;
            HexCoord nearest = centers.OrderBy(pair => Vector2.SqrMagnitude(pair.Value - local)).First().Key;
            if (Vector2.Distance(centers[nearest], local) > 42) return;
            if (pending != PendingAction.None && game.Active != null)
            {
                if (IsMovePending()) ExecuteMove(nearest);
                else
                {
                    ContactState contact = game.Contacts.Where(c => c.Owner == game.Active.Side && !c.IsLost).OrderBy(c => Vector2.Distance(centers[c.LastKnownPosition], local)).FirstOrDefault();
                    if (contact != null && Vector2.Distance(centers[contact.LastKnownPosition], local) <= 48)
                    {
                        FormationState target = game.Find(contact.TargetId);
                        if (IsSearchPending()) ExecuteSearch(target); else ExecuteStrike(target);
                    }
                }
                e.Use(); return;
            }
            FormationState friendly = game.Formations.FirstOrDefault(f => !f.IsDestroyed && f.Side == game.Active.Side && f.Position.Equals(nearest));
            if (friendly != null) inspected = friendly;
            e.Use();
        }

        private void ExecuteMove(HexCoord destination)
        {
            MoveMode mode = pending == PendingAction.MoveCautious ? MoveMode.Cautious : pending == PendingAction.MoveHigh ? MoveMode.HighTempo : MoveMode.Normal;
            FormationState actor = game.Active;
            PlaytestRecorder.Observation before = telemetry.Observe(game, actor);
            string alternatives = LegalAlternatives();
            string calculation = $"Distance {HexCoord.Distance(actor.Position, destination)}; allowance {Rules.MoveAllowance(actor, mode)}; base Time 2; Friction {(actor.Friction || mode == MoveMode.HighTempo ? "+1" : "+0")}";
            if (game.Move(actor, destination, mode, out string message))
            {
                lastActionSucceeded = true;
                telemetry.RecordAction(game, before, actor, "Move", mode.ToString(), destination.ToString(), alternatives, DecisionSeconds(), calculation, message);
                pending = PendingAction.None; inspected = game.Active; AfterSuccessfulAction(actor.Side);
            }
            else telemetry.RecordRejected(game, actor, "Move", mode.ToString(), destination.ToString(), message, DecisionSeconds());
            Toast(message);
        }

        private void ExecuteSearch(FormationState target)
        {
            FormationState actor = game.Active;
            ContactState contact = target == null ? null : game.ContactFor(actor.Side, target.Id);
            if (contact == null) { Toast("That Contact is no longer available. Select an area hex to Search."); return; }
            ExecuteSearchArea(contact.LastKnownPosition);
        }

        private void ExecuteSearchArea(HexCoord center)
        {
            SearchMode mode = pending == PendingAction.SearchActive ? SearchMode.Active : pending == PendingAction.SearchFocused ? SearchMode.Focused : SearchMode.Passive;
            FormationState actor = game.Active;
            PlaytestRecorder.Observation before = telemetry.Observe(game, actor);
            string alternatives = LegalAlternatives();
            int range = HexCoord.Distance(actor.Position, center);
            string calculation = $"Area {center}; center range {range}/{Rules.SearchRange(mode)} hexes; footprint radius {Rules.SearchAreaRadius}; Search {actor.EffectiveSearch}; Mode {game.SearchModifierFor(actor, mode):+0;-0;0}; hidden Signature and Loud modifiers resolved privately; base Time 2; Friction {(actor.Friction ? "+1" : "+0")}{(mode == SearchMode.Focused ? $"; Command temporarily occupied {game.Sides[actor.Side].CommandSlots}->{game.Sides[actor.Side].CommandSlots - 1}->{game.Sides[actor.Side].CommandSlots}" : string.Empty)}";
            if (game.SearchArea(actor, center, mode, out string message))
            {
                lastActionSucceeded = true;
                telemetry.RecordAction(game, before, actor, "Area Search", mode.ToString(), center.ToString(), alternatives, DecisionSeconds(), calculation, message);
                pending = PendingAction.None; inspected = game.Active; AfterSuccessfulAction(actor.Side);
            }
            else telemetry.RecordRejected(game, actor, "Area Search", mode.ToString(), center.ToString(), message, DecisionSeconds());
            Toast(message);
        }

        private void ExecuteStrike(FormationState target, bool confirmed = false, Reaction selectedReaction = Reaction.Defend, HexCoord? evadeDestination = null)
        {
            Salvo salvo = pending == PendingAction.StrikeLight ? Salvo.Light : pending == PendingAction.StrikeHeavy ? Salvo.Heavy : Salvo.Standard;
            if (salvo == Salvo.Heavy && !confirmed)
            {
                confirmationTarget = target;
                overlay = Overlay.ConfirmHeavy;
                return;
            }
            FormationState actor = game.Active;
            PlaytestRecorder.Observation before = telemetry.Observe(game, actor);
            string alternatives = LegalAlternatives();
            Reaction reaction = selectedReaction;
            CombatPreview preview = GetCombatPreview(actor, target, salvo, reaction);
            string calculation = $"Attack {preview.Attack} vs Defense {preview.Defense}; difference {preview.Difference}; band {preview.Band}; reaction {reaction}; odds {CombatOddsText(preview.Band).Replace("\n", "; ")}";
            if (game.Strike(actor, target, salvo, reaction, evadeDestination, out CombatResult result, out string message))
            {
                lastActionSucceeded = true;
                string outcome = message + $" Roll {result.Roll}; damage {result.Damage}.";
                telemetry.RecordAction(game, before, actor, "Strike", salvo.ToString(), target.Id, alternatives, DecisionSeconds(), calculation, outcome);
                pending = PendingAction.None; inspected = game.Active; AfterSuccessfulAction(actor.Side);
            }
            else telemetry.RecordRejected(game, actor, "Strike", salvo.ToString(), target?.Id, message, DecisionSeconds());
            Toast(message);
        }

        private void ExecuteHold()
        {
            FormationState actor = game.Active;
            string alternatives = LegalAlternatives();
            telemetry.RecordChoice(game, actor, "ActionChosen", "Hold", "Quiet", alternatives);
            PlaytestRecorder.Observation before = telemetry.Observe(game, actor);
            if (game.Hold(actor, out string message)) { lastActionSucceeded = true; telemetry.RecordAction(game, before, actor, "Hold", "Quiet", "", alternatives, DecisionSeconds(), "Base Time 1; reduce Signature to Quiet", message); pending = PendingAction.None; inspected = game.Active; AfterSuccessfulAction(actor.Side); }
            else telemetry.RecordRejected(game, actor, "Hold", "Quiet", "", message, DecisionSeconds());
            Toast(message);
        }

        private void ExecuteRecover()
        {
            FormationState actor = game.Active;
            string recoverMode = actor.Friction ? "Friction" : actor.Disruption ? "Disruption" : "None";
            string alternatives = LegalAlternatives();
            telemetry.RecordChoice(game, actor, "ActionChosen", "Recover", recoverMode, alternatives);
            PlaytestRecorder.Observation before = telemetry.Observe(game, actor);
            if (game.Recover(actor, out string message)) { lastActionSucceeded = true; telemetry.RecordAction(game, before, actor, "Recover", recoverMode, "", alternatives, DecisionSeconds(), "Base Time 2; clear one recoverable Entropy source", message); pending = PendingAction.None; inspected = game.Active; AfterSuccessfulAction(actor.Side); }
            else telemetry.RecordRejected(game, actor, "Recover", "None", "", message, DecisionSeconds());
            Toast(message);
        }

        private float DecisionSeconds() => Mathf.Max(0, Time.realtimeSinceStartup - decisionStartedAt);
        private void Toast(string message) { toast = message; toastUntil = Time.unscaledTime + 8f; }

        private void ActionButton(Rect rect, string label, PendingAction action, Color color)
        {
            bool active = action == PendingAction.MoveNormal && IsMovePending() || action == PendingAction.SearchPassive && IsSearchPending() || action == PendingAction.StrikeStandard && IsStrikePending();
            GUIStyle style = active ? buttonActive : button;
            if (GUI.Button(rect, label, style))
            {
                pending = action;
                telemetry.RecordChoice(game, game.Active, "ActionChosen", ActionName(action), ModeName(action), LegalAlternatives());
            }
            if (!active) Fill(new Rect(rect.x, rect.yMax - 3, rect.width, 3), color);
            SetTooltip(rect, action == PendingAction.MoveNormal ? "Reposition this formation. Commitment controls distance, Signature, and Friction." : action == PendingAction.SearchPassive ? "Improve Location or Identity information about an enemy Contact." : "Attack a usable Contact. Salvo size changes Attack and weapon expenditure.");
        }

        private void ModeButton(Rect rect, string label, PendingAction value)
        {
            if (GUI.Button(rect, label, pending == value ? buttonActive : button))
            {
                pending = value;
                telemetry.RecordChoice(game, game.Active, "ModeChosen", ActionName(value), ModeName(value), LegalAlternatives());
            }
            SetTooltip(rect, ModeTooltip(value));
        }
        private bool IsMovePending() => pending == PendingAction.MoveCautious || pending == PendingAction.MoveNormal || pending == PendingAction.MoveHigh;
        private bool IsSearchPending() => pending == PendingAction.SearchPassive || pending == PendingAction.SearchActive || pending == PendingAction.SearchFocused;
        private bool IsStrikePending() => pending == PendingAction.StrikeLight || pending == PendingAction.StrikeStandard || pending == PendingAction.StrikeHeavy;

        private MoveMode CurrentMoveMode() => pending == PendingAction.MoveCautious ? MoveMode.Cautious : pending == PendingAction.MoveHigh ? MoveMode.HighTempo : MoveMode.Normal;
        private SearchMode CurrentSearchMode() => pending == PendingAction.SearchActive ? SearchMode.Active : pending == PendingAction.SearchFocused ? SearchMode.Focused : SearchMode.Passive;
        private Salvo CurrentSalvo() => pending == PendingAction.StrikeLight ? Salvo.Light : pending == PendingAction.StrikeHeavy ? Salvo.Heavy : Salvo.Standard;

        private string ActionName(PendingAction value)
        {
            if (value == PendingAction.None) return "None";
            if (value == PendingAction.MoveCautious || value == PendingAction.MoveNormal || value == PendingAction.MoveHigh) return "Move";
            if (value == PendingAction.SearchPassive || value == PendingAction.SearchActive || value == PendingAction.SearchFocused) return "Search";
            return "Strike";
        }

        private string ModeName(PendingAction value)
        {
            switch (value)
            {
                case PendingAction.MoveCautious: return MoveMode.Cautious.ToString();
                case PendingAction.MoveNormal: return MoveMode.Normal.ToString();
                case PendingAction.MoveHigh: return MoveMode.HighTempo.ToString();
                case PendingAction.SearchPassive: return SearchMode.Passive.ToString();
                case PendingAction.SearchActive: return SearchMode.Active.ToString();
                case PendingAction.SearchFocused: return SearchMode.Focused.ToString();
                case PendingAction.StrikeLight: return Salvo.Light.ToString();
                case PendingAction.StrikeStandard: return Salvo.Standard.ToString();
                case PendingAction.StrikeHeavy: return Salvo.Heavy.ToString();
                default: return string.Empty;
            }
        }

        private string LegalAlternatives()
        {
            if (game.Active == null) return string.Empty;
            IEnumerable<HexCoord> mapHexes = centers.Count > 0 ? centers.Keys : Enumerable.Range(0, game.Area.Width).SelectMany(q => Enumerable.Range(0, game.Area.Height).Select(r => new HexCoord(q, r))).Where(game.Area.Contains);
            int legalMoveHexes = mapHexes.Count(hex => { int distance = HexCoord.Distance(game.Active.Position, hex); return distance > 0 && distance <= game.Active.EffectiveMove; });
            int searchable = mapHexes.Count(hex => HexCoord.Distance(game.Active.Position, hex) <= Rules.SearchRange(CurrentSearchMode()));
            int strikeable = game.Contacts.Count(c => c.Owner == game.Active.Side && !c.IsLost && HexCoord.Distance(game.Active.Position, c.LastKnownPosition) <= Rules.StrikeRange(game.Active.Kind, CurrentSalvo()));
            bool recover = game.Active.Friction || game.Active.Disruption;
            int supportable = game.Formations.Count(candidate => candidate.Side == game.Active.Side && candidate != game.Active && !candidate.IsDestroyed && HexCoord.Distance(game.Active.Position, candidate.Position) <= Rules.SupportRange);
            return $"Move({legalMoveHexes} hexes); Search({searchable} area hexes); Strike({strikeable} Contacts); Patrol(true); Support({supportable} formations); Recover({recover}); Replenish({game.HasLogisticsAccess(game.Active) && game.NeedsReplenishment(game.Active)}); Hold(true)";
        }

        private bool StrikeEligible(ContactState contact)
        {
            if (game.Active == null || contact == null || contact.IsLost) return false;
            FormationState target = game.Find(contact.TargetId);
            if (target == null || target.IsDestroyed || HexCoord.Distance(game.Active.Position, contact.LastKnownPosition) > Rules.StrikeRange(game.Active.Kind, pending == PendingAction.StrikeLight ? Salvo.Light : pending == PendingAction.StrikeHeavy ? Salvo.Heavy : Salvo.Standard)) return false;
            return CurrentSalvo() != Salvo.Heavy || game.Active.CanHeavySalvo;
        }

        private CombatPreview GetCombatPreview(FormationState attacker, FormationState target, Salvo salvo, Reaction reaction)
        {
            ContactState contact = game.ContactFor(attacker.Side, target.Id);
            int attack = attacker.EffectiveStrike + Rules.SalvoModifier(salvo) + game.PendingSupportBonus(attacker, SupportKind.Strike) + Rules.TargetingModifier(contact, game.AgeTwoTargetingPenalty);
            int defense = target.EffectiveDefense + (reaction == Reaction.Defend || reaction == Reaction.Evade ? 1 : 0) + game.PendingSupportBonus(target, SupportKind.Defense) + game.PatrolDefenseBonus(target) - (target.Destruction ? 1 : 0);
            int difference = attack - defense;
            return new CombatPreview { Attack = attack, Defense = defense, Difference = difference, Band = Rules.BandFor(difference) };
        }

        private string CombatOddsText(CombatBand band)
        {
            switch (band)
            {
                case CombatBand.Poor: return "NO EFFECT  83%\nLIGHT  17%";
                case CombatBand.Even: return "NO EFFECT  17%\nLIGHT  67%\nHEAVY  17%";
                case CombatBand.Favorable: return "LIGHT  17%\nHEAVY  67%\nCRIPPLED  17%";
                default: return "HEAVY  17%\nCRIPPLED  67%\nDESTROYED  17%";
            }
        }

        private string CurrentActivationExplanation()
        {
            if (game.Active == null) return "No formation is currently Ready.";
            int minimumTime = game.Formations.Where(f => !f.IsDestroyed).Min(f => f.ReadyTime);
            List<FormationState> sameTime = game.Formations.Where(f => !f.IsDestroyed && f.ReadyTime == minimumTime).ToList();
            if (sameTime.Count == 1) return $"{game.Active.Name} is earliest at T{minimumTime:00}.";
            bool crossSideTie = sameTime.Select(f => f.Side).Distinct().Count() > 1;
            if (crossSideTie && game.LastActingSide.HasValue)
                return $"T{minimumTime:00} cross-side tie: priority passes from {game.LastActingSide.Value} to {game.Active.Side}; {game.Active.Name} leads that side's Ready formations.";
            List<FormationState> eligibleSide = crossSideTie ? sameTime.Where(f => f.Side == game.Active.Side).ToList() : sameTime;
            int minimumEntropy = eligibleSide.Min(f => f.EntropySources);
            List<FormationState> sameEntropy = eligibleSide.Where(f => f.EntropySources == minimumEntropy).ToList();
            if (sameEntropy.Count == 1) return $"T{minimumTime:00} tie: {game.Active.Name} has lower Entropy.";
            int maximumCommand = sameEntropy.Max(f => f.EffectiveCommand);
            List<FormationState> sameCommand = sameEntropy.Where(f => f.EffectiveCommand == maximumCommand).ToList();
            return sameCommand.Count == 1 ? $"T{minimumTime:00} tie: {game.Active.Name} has higher Command." : $"T{minimumTime:00} tie: stable prototype tie-break selected {game.Active.Name}.";
        }

        private string ModeTooltip(PendingAction value)
        {
            switch (value)
            {
                case PendingAction.MoveCautious: return "Move 1 hex and reduce Signature by 1. Best for concealment.";
                case PendingAction.MoveNormal: return "Move up to 2 hexes without an additional modifier.";
                case PendingAction.MoveHigh: return "Move up to 3 hexes, become easier to detect, and mark Friction.";
                case PendingAction.SearchPassive: return "No Search bonus and no additional exposure.";
                case PendingAction.SearchActive: return "+1 Search, but the formation becomes Loud.";
                case PendingAction.SearchFocused: return "+2 Search and temporarily requires one free Command Slot.";
                case PendingAction.StrikeLight: return "No Attack bonus. Conserves Heavy Salvo capability.";
                case PendingAction.StrikeStandard: return "+1 Attack without Heavy expenditure.";
                default: return "+2 Attack and expend Heavy Salvo capability. Requires confirmation.";
            }
        }

        private string RatingTooltip(string label)
        {
            switch (label)
            {
                case "MOVE": return "Maximum useful movement for the selected commitment, after Endurance and damage.";
                case "SEARCH": return "Detection capability after Disruption. Added to mode, Signature, and range modifiers.";
                case "STRIKE": return "Offensive strength after Destruction. Added to salvo and targeting modifiers.";
                case "DEFENSE": return "Survivability and defensive power before Reaction or Support.";
                case "SIGNATURE": return "Detectability. Higher values make this formation easier to find.";
                case "COMMAND": return "Initiative and autonomy. Breaks same-Time activation ties after Entropy.";
                default: return label;
            }
        }

        private void SetTooltip(Rect rect, string text)
        {
            if (rect.Contains(Event.current.mousePosition)) hoverTooltip = text;
        }

        private void DrawTooltip()
        {
            if (string.IsNullOrEmpty(hoverTooltip) || Event.current.type != EventType.Repaint) return;
            Vector2 mouse = Event.current.mousePosition;
            float width = 330;
            float height = small.CalcHeight(new GUIContent(hoverTooltip), width - 24) + 20;
            Rect rect = new Rect(Mathf.Min(mouse.x + 18, ReferenceWidth - width - 12), Mathf.Min(mouse.y + 18, ReferenceHeight - height - 12), width, height);
            Fill(rect, new Color(.01f, .045f, .06f, .99f));
            Line(new Vector2(rect.x, rect.y), new Vector2(rect.xMax, rect.y), new Color(.35f, .85f, .86f), 2);
            GUI.Label(new Rect(rect.x + 12, rect.y + 8, rect.width - 24, rect.height - 16), hoverTooltip, small);
        }

        private void DrawStat(float x, float y, string label, int value, Color color)
        {
            Rect rect = new Rect(x, y, 98, 62);
            Fill(rect, new Color(.035f, .14f, .17f, .9f)); Fill(new Rect(x, y + 59, 98, 3), color);
            GUI.Label(new Rect(x + 4, y + 4, 90, 19), label, tiny); GUI.Label(new Rect(x + 4, y + 22, 90, 37), value.ToString(), new GUIStyle(title) { alignment = TextAnchor.MiddleCenter, fontSize = 26 });
            SetTooltip(rect, RatingTooltip(label));
        }

        private void DrawEntropy(Rect rect, string label, bool marked, Color color)
        {
            Fill(rect, marked ? new Color(color.r, color.g, color.b, .26f) : new Color(.035f, .12f, .15f, .8f));
            Line(new Vector2(rect.x, rect.yMax), new Vector2(rect.xMax, rect.yMax), marked ? color : new Color(.18f, .27f, .29f), 2);
            GUI.Label(rect, marked ? label + "\nMARKED" : label + "\nCLEAR", tiny);
            SetTooltip(rect, label == "FRICTION" ? "Friction adds +1 Time after complex Actions." : label == "DISRUPTION" ? "Disruption reduces Search and synchronization effectiveness." : "Destruction reduces offensive capability and requires repair or replenishment.");
        }

        private void DrawBadge(Rect rect, string label, Color color) { Fill(rect, new Color(color.r, color.g, color.b, .24f)); Line(new Vector2(rect.x, rect.yMax), new Vector2(rect.xMax, rect.yMax), color, 2); GUI.Label(rect, label, badge); }
        private void Panel(Rect rect, Color color) { Fill(rect, color); Line(new Vector2(rect.x, rect.y), new Vector2(rect.xMax, rect.y), new Color(.2f, .6f, .65f, .4f), 1); }
        private void Fill(Rect rect, Color color) { Color old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, pixel); GUI.color = old; }
        private Color SideColor(Side side)
        {
            if (highContrast) return side == Side.Blue ? new Color(.12f, .78f, 1f) : new Color(1f, .72f, .08f);
            return side == Side.Blue ? new Color(.15f, .65f, .78f) : new Color(.9f, .28f, .24f);
        }
        private string KindCode(FormationKind kind) => kind == FormationKind.CarrierGroup ? "CV" : kind == FormationKind.SurfaceGroup ? "SG" : kind == FormationKind.Submarine ? "SS" : "AG";

        private void DrawRing(Vector2 centerPoint, float radius, Color color, float width)
        {
            Vector2 previous = centerPoint + new Vector2(radius, 0);
            for (int i = 1; i <= 40; i++) { float angle = i / 40f * Mathf.PI * 2; Vector2 next = centerPoint + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius; Line(previous, next, color, width); previous = next; }
        }

        private void Line(Vector2 a, Vector2 b, Color color, float width)
        {
            Matrix4x4 old = GUI.matrix;
            Color oldColor = GUI.color;
            float angle = Vector3.Angle(b - a, Vector2.right);
            if (a.y > b.y) angle = -angle;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(angle, a);
            GUI.DrawTexture(new Rect(a.x, a.y - width * .5f, (b - a).magnitude, width), pixel);
            GUI.matrix = old;
            GUI.color = oldColor;
        }
    }
}
