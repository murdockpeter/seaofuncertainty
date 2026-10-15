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
        public PrototypeGame Game => game;
        public PlaytestRecorder Telemetry => telemetry;
        public PlaytestFeedback Feedback => feedback;
        public string LastMessage => toast;
        public bool HasSave => File.Exists(SavePath);
        public bool IsComplete => game != null && game.Time >= game.Scenario.Horizon;
        public int BlueFinalScore => blueFinalScore;
        public int RedFinalScore => redFinalScore;
        public string ResultSummary => resultSummary;
        public ScenarioOutcome FinalOutcome => finalOutcome;
        public bool AgeTwoPenalty => ageTwoTargetingPenalty;
        public bool AudioEnabled => audioEnabled;
        public float MasterVolume => masterVolume;
        public float EffectsVolume => effectsVolume;
        public float AmbientVolume => ambientVolume;
        public bool AudioDescriptions => audioDescriptions;
        public float TextScale => textScale;
        public bool ControllerNavigation => controllerNavigation;
        public float ControllerDeadZone => controllerDeadZone;
        public string LastAudioDescription => lastAudioDescription;
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
        public bool ToolkitCanRespondToEntropy(FormationState formation, string cardId) => game != null && game.CanRespondToEntropy(formation, cardId);

#if UNITY_EDITOR
        public void EditorEnsureInitializedForTests()
        {
            if (game == null) Awake();
        }
#endif

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

        private PrototypeGame game;
        private PendingAction pending;
        private SearchPriority searchPriority = SearchPriority.Location;
        private FormationState inspected;
        private bool ageTwoTargetingPenalty = true;
        private bool audioEnabled = true;
        private float masterVolume = .8f;
        private float effectsVolume = .7f;
        private float ambientVolume = .35f;
        private bool audioDescriptions = true;
        private float textScale = 1f;
        private bool controllerNavigation = true;
        private float controllerDeadZone = .55f;
        private string lastAudioDescription = "Ambient audio: quiet open-sea wash.";
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
        private ScenarioOutcome finalOutcome;
        private PlaytestRecorder telemetry;
        private PlaytestFeedback feedback = new PlaytestFeedback();
        private float decisionStartedAt;
        private string toast = "Select the Ready formation's action. Every choice previews its cost.";
        private string SavePath => Path.Combine(Application.persistentDataPath, "meridian-veil-save.json");

        private void Awake()
        {
            ageTwoTargetingPenalty = PlayerPrefs.GetInt("AgeTwoPenalty", 1) == 1;
            audioEnabled = PlayerPrefs.GetInt("AudioEnabled", 1) == 1;
            masterVolume = PlayerPrefs.GetFloat("MasterVolume", audioEnabled ? .8f : 0f);
            effectsVolume = PlayerPrefs.GetFloat("EffectsVolume", .7f);
            ambientVolume = PlayerPrefs.GetFloat("AmbientVolume", .35f);
            audioDescriptions = PlayerPrefs.GetInt("AudioDescriptions", 1) == 1;
            textScale = Mathf.Clamp(PlayerPrefs.GetFloat("TextScale", 1f), 1f, 1.5f);
            controllerNavigation = PlayerPrefs.GetInt("ControllerNavigation", 1) == 1;
            controllerDeadZone = Mathf.Clamp(PlayerPrefs.GetFloat("ControllerDeadZone", .55f), .3f, .9f);
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
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            ApplyAudioSettings();
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

        public AiDecision ToolkitChooseAiDecision()
        {
            if (!IsAiTurn) return null;
            FormationState actor = game.Active;
            if (PrototypeAiCommander.TryPlayUsefulResponse(game, out string responseMessage))
                telemetry.RecordChoice(game, actor, "AiCommandResponsePlayed", "Response", responseMessage, LegalAlternatives());
            return PrototypeAiCommander.Choose(game);
        }

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
            telemetry.RecordAiDecision(game, actor, decision, alternatives);
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
            return ToolkitPlayResponseForSide(game.Active.Side, cardId, formation, contact, hex, mission);
        }
        public string ToolkitPlayResponseForSide(Side side, string cardId, FormationState formation = null, ContactState contact = null, HexCoord? hex = null, ActionKind? mission = null)
        {
            lastActionSucceeded = game.PlayCommandResponse(side, cardId, formation, contact, hex, mission, out string message);
            if (lastActionSucceeded) telemetry.RecordChoice(game, formation ?? game.Active, "CommandResponsePlayed", cardId, message, LegalAlternatives());
            Toast(message);
            return message;
        }
        public string ToolkitDeclareSynchronizedStrike(IEnumerable<FormationState> participants, ContactState contact, HexCoord aim, Salvo salvo, bool priorPlanning, string deconflictedFormationId)
        {
            lastActionSucceeded = false;
            FormationState actor = game.Active;
            string alternatives = LegalAlternatives();
            PlaytestRecorder.Observation before = telemetry.Observe(game, actor);
            lastActionSucceeded = game.DeclareSynchronizedStrike(actor, participants, contact, aim, salvo, priorPlanning, deconflictedFormationId, out SynchronizedStrikeState strike, out string message);
            if (lastActionSucceeded)
            {
                telemetry.RecordAction(game, before, actor, "Synchronized Strike Declared", salvo.ToString(), contact?.TargetId ?? aim.ToString(), alternatives, DecisionSeconds(), $"Strike Time T{strike.StrikeTime:00}; {strike.Participants.Count} reserved; C-02 {priorPlanning}; C-10 {deconflictedFormationId}", message);
                inspected = game.Active;
            }
            else telemetry.RecordRejected(game, actor, "Synchronized Strike Declared", salvo.ToString(), aim.ToString(), message, DecisionSeconds());
            Toast(message);
            return message;
        }
        public string ToolkitResolveSynchronizedStrike(SynchronizedStrikeState strike, Reaction reaction, HexCoord? evadeDestination, bool continueBlind)
        {
            lastActionSucceeded = false;
            FormationState actor = game.Active;
            string alternatives = LegalAlternatives();
            PlaytestRecorder.Observation before = telemetry.Observe(game, actor);
            lastActionSucceeded = game.ResolveSynchronizedStrike(strike, reaction, evadeDestination, continueBlind, out SynchronizedStrikeResult result, out string message);
            if (lastActionSucceeded)
            {
                telemetry.RecordAction(game, before, actor, "Synchronized Strike Resolved", "Sequential Volley", strike.Aim.ToString(), alternatives, DecisionSeconds(), $"{strike.Participants.Count} attacks; one Reaction; Defense -1 per subsequent attack; blind {continueBlind}", message);
                inspected = game.Active;
                AfterSuccessfulAction(actor.Side);
            }
            else telemetry.RecordRejected(game, actor, "Synchronized Strike Resolved", "Sequential Volley", strike?.Aim.ToString() ?? "", message, DecisionSeconds());
            Toast(message);
            return message;
        }
        public string ToolkitAbortSynchronizedStrike(SynchronizedStrikeState strike)
        {
            FormationState actor = game.Active;
            lastActionSucceeded = game.AbortSynchronizedStrike(strike, out string message);
            telemetry.RecordChoice(game, actor, lastActionSucceeded ? "SynchronizedStrikeAborted" : "SynchronizedStrikeAbortRejected", "Abort", strike?.Id ?? "", LegalAlternatives());
            if (lastActionSucceeded) inspected = game.Active;
            Toast(message);
            return message;
        }
        public string ToolkitRetaskSynchronizedStrike(SynchronizedStrikeState strike, ContactState contact, HexCoord aim)
        {
            lastActionSucceeded = game.RetaskSynchronizedStrike(strike, contact, aim, out string message);
            telemetry.RecordChoice(game, game.Active, lastActionSucceeded ? "SynchronizedStrikeRetasked" : "SynchronizedStrikeRetaskRejected", "Retask", contact?.TargetId ?? aim.ToString(), LegalAlternatives());
            Toast(message);
            return message;
        }
        public void ToolkitSetSettings(bool agePenalty, float master, float effects, float ambience, bool descriptions, bool contrast, bool reduceMotion, bool showHexGrid, float uiTextScale, bool controller, float deadZone)
        {
            ageTwoTargetingPenalty = agePenalty;
            masterVolume = Mathf.Clamp01(master);
            effectsVolume = Mathf.Clamp01(effects);
            ambientVolume = Mathf.Clamp01(ambience);
            audioEnabled = masterVolume > .001f;
            audioDescriptions = descriptions;
            highContrast = contrast;
            reducedMotion = reduceMotion;
            hexGridVisible = showHexGrid;
            textScale = Mathf.Clamp(uiTextScale, 1f, 1.5f);
            controllerNavigation = controller;
            controllerDeadZone = Mathf.Clamp(deadZone, .3f, .9f);
            game.AgeTwoTargetingPenalty = agePenalty;
            ApplyAudioSettings();
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

        public string ToolkitSearch(FormationState target, SearchMode mode, SearchPriority priority = SearchPriority.Location)
        {
            lastActionSucceeded = false;
            pending = mode == SearchMode.Passive ? PendingAction.SearchPassive : mode == SearchMode.Active ? PendingAction.SearchActive : PendingAction.SearchFocused;
            searchPriority = priority;
            ExecuteSearch(target);
            return toast;
        }

        public string ToolkitSearchHex(HexCoord center, SearchMode mode, SearchPriority priority = SearchPriority.Location)
        {
            lastActionSucceeded = false;
            pending = mode == SearchMode.Passive ? PendingAction.SearchPassive : mode == SearchMode.Active ? PendingAction.SearchActive : PendingAction.SearchFocused;
            searchPriority = priority;
            ExecuteSearchArea(center);
            return toast;
        }

        public string ToolkitStrike(FormationState target, Salvo salvo, Reaction reaction = Reaction.Defend, HexCoord? evadeDestination = null)
        {
            lastActionSucceeded = false;
            pending = salvo == Salvo.Light ? PendingAction.StrikeLight : salvo == Salvo.Standard ? PendingAction.StrikeStandard : PendingAction.StrikeHeavy;
            ExecuteStrike(target, reaction, evadeDestination);
            return toast;
        }

        public string ToolkitStrikeContact(ContactState contact, HexCoord aim, Salvo salvo, Reaction reaction = Reaction.None, HexCoord? evadeDestination = null)
        {
            lastActionSucceeded = false;
            pending = salvo == Salvo.Light ? PendingAction.StrikeLight : salvo == Salvo.Standard ? PendingAction.StrikeStandard : PendingAction.StrikeHeavy;
            FormationState actor = game.Active;
            PlaytestRecorder.Observation before = telemetry.Observe(game, actor);
            string alternatives = LegalAlternatives();
            int range = HexCoord.Distance(actor.Position, aim);
            int attack = actor.EffectiveStrike + Rules.SalvoModifier(salvo) + game.PendingSupportBonus(actor, SupportKind.Strike) + Rules.TargetingModifier(contact, game.AgeTwoTargetingPenalty);
            string calculation = $"Contact-area aim {aim}; range {range}/{game.StrikeRangeFor(actor, salvo)}; Attack {attack}; hidden occupancy and defense resolved after commitment; reaction {reaction}";
            lastActionSucceeded = game.StrikeContact(actor, contact, aim, salvo, reaction, evadeDestination, out CombatResult result, out string message);
            if (lastActionSucceeded)
            {
                string outcome = message + $" Roll {result.Roll}; result {result.Damage}; cumulative damage {result.ResultingDamage}.";
                telemetry.RecordAction(game, before, actor, "Contact-area Strike", salvo.ToString(), aim.ToString(), alternatives, DecisionSeconds(), calculation, outcome);
                pending = PendingAction.None; inspected = game.Active; AfterSuccessfulAction(actor.Side);
            }
            else telemetry.RecordRejected(game, actor, "Contact-area Strike", salvo.ToString(), aim.ToString(), message, DecisionSeconds());
            Toast(message);
            return message;
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

        private void ResetScenario(string scenarioId = null)
        {
            game = new PrototypeGame(1978, ScenarioCatalog.Require(string.IsNullOrEmpty(scenarioId) ? "meridian-veil" : scenarioId));
            AttachMissionAudio(game);
            game.AgeTwoTargetingPenalty = ageTwoTargetingPenalty;
            if (telemetry == null) telemetry = new PlaytestRecorder();
            telemetry.StartNew(game);
            feedback = new PlaytestFeedback();
            finalOutcome = null;
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
            PlayerPrefs.SetFloat("MasterVolume", masterVolume);
            PlayerPrefs.SetFloat("EffectsVolume", effectsVolume);
            PlayerPrefs.SetFloat("AmbientVolume", ambientVolume);
            PlayerPrefs.SetInt("AudioDescriptions", audioDescriptions ? 1 : 0);
            PlayerPrefs.SetFloat("TextScale", textScale);
            PlayerPrefs.SetInt("ControllerNavigation", controllerNavigation ? 1 : 0);
            PlayerPrefs.SetFloat("ControllerDeadZone", controllerDeadZone);
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
            if (formation == null) return;
            if (operationMode == OperationMode.SoloVsAi && formation.Side != humanSide) return;
            lastAudioDescription = OperationalMissionAudio.Description(mission);
            if (!audioEnabled) return;
            missionAudio?.Play(mission);
        }

        private void ApplyAudioSettings()
        {
            AudioListener.volume = masterVolume;
            missionAudio?.Configure(masterVolume, effectsVolume, ambientVolume);
        }

        private void OnDestroy()
        {
            if (audioEventGame != null) { audioEventGame.ActionCompleted -= OnMissionCompleted; audioEventGame.CommandEvent -= OnCommandEvent; }
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
            finalOutcome = ScenarioScoring.Evaluate(game);
            blueFinalScore = finalOutcome.Blue.OperationalPoints;
            redFinalScore = finalOutcome.Red.OperationalPoints;
            resultSummary = finalOutcome.Summary;
            telemetry.RecordScore(game, blueFinalScore, redFinalScore, resultSummary);
            ExportPlaytest();
            pending = PendingAction.None;
        }

        private void AfterSuccessfulAction(Side actingSide)
        {
            if (game.Time >= game.Scenario.Horizon) { CompleteScenario(); return; }
            if (game.Active == null || game.Active.Side == actingSide) decisionStartedAt = Time.realtimeSinceStartup;
        }

        private void ExecuteMove(HexCoord destination)
        {
            MoveMode mode = pending == PendingAction.MoveCautious ? MoveMode.Cautious : pending == PendingAction.MoveHigh ? MoveMode.HighTempo : MoveMode.Normal;
            FormationState actor = game.Active;
            PlaytestRecorder.Observation before = telemetry.Observe(game, actor);
            string alternatives = LegalAlternatives();
            game.TryGetMovePath(actor, destination, mode, out List<HexCoord> route, out _);
            bool littoral = actor.Kind != FormationKind.AirGroup && route.Skip(1).Any(hex => game.Area.TerrainAt(hex) == OperationalTerrain.Littoral);
            string calculation = $"Route {string.Join("-", route)}; distance {Math.Max(0, route.Count - 1)}; allowance {Rules.MoveAllowance(actor, mode)}; base Time 2; Littoral {(littoral ? "+1" : "+0")}; Friction {(actor.Friction || mode == MoveMode.HighTempo ? "+1" : "+0")}";
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
            string calculation = $"Area {center}; center range {range}/{game.SearchRangeFor(actor, mode)} hexes; footprint radius {Rules.SearchAreaRadius}; priority {searchPriority}; one improvement maximum; Search {actor.EffectiveSearch}; Mode {game.SearchModifierFor(actor, mode):+0;-0;0}; hidden Signature and Loud modifiers resolved privately; base Time 2; Friction {(actor.Friction ? "+1" : "+0")}{(mode == SearchMode.Focused ? $"; Command temporarily occupied {game.Sides[actor.Side].CommandSlots}->{game.Sides[actor.Side].CommandSlots - 1}->{game.Sides[actor.Side].CommandSlots}" : string.Empty)}";
            if (game.SearchArea(actor, center, mode, searchPriority, out string message))
            {
                lastActionSucceeded = true;
                telemetry.RecordAction(game, before, actor, "Area Search", mode.ToString(), center.ToString(), alternatives, DecisionSeconds(), calculation, message);
                pending = PendingAction.None; inspected = game.Active; AfterSuccessfulAction(actor.Side);
            }
            else telemetry.RecordRejected(game, actor, "Area Search", mode.ToString(), center.ToString(), message, DecisionSeconds());
            Toast(message);
        }

        private void ExecuteStrike(FormationState target, Reaction selectedReaction = Reaction.Defend, HexCoord? evadeDestination = null)
        {
            Salvo salvo = pending == PendingAction.StrikeLight ? Salvo.Light : pending == PendingAction.StrikeHeavy ? Salvo.Heavy : Salvo.Standard;
            FormationState actor = game.Active;
            PlaytestRecorder.Observation before = telemetry.Observe(game, actor);
            string alternatives = LegalAlternatives();
            Reaction reaction = selectedReaction;
            CombatPreview preview = GetCombatPreview(actor, target, salvo, reaction);
            string calculation = $"Attack {preview.Attack} vs Defense {preview.Defense}; difference {preview.Difference}; band {preview.Band}; reaction {reaction}; odds {CombatOddsText(preview.Band).Replace("\n", "; ")}";
            if (game.Strike(actor, target, salvo, reaction, evadeDestination, out CombatResult result, out string message))
            {
                lastActionSucceeded = true;
                string outcome = message + $" Roll {result.Roll}; result {result.Damage}; cumulative damage {result.ResultingDamage}.";
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
        private void Toast(string message) => toast = message;

        private SearchMode CurrentSearchMode() => pending == PendingAction.SearchActive ? SearchMode.Active : pending == PendingAction.SearchFocused ? SearchMode.Focused : SearchMode.Passive;
        private Salvo CurrentSalvo() => pending == PendingAction.StrikeLight ? Salvo.Light : pending == PendingAction.StrikeHeavy ? Salvo.Heavy : Salvo.Standard;


        private string LegalAlternatives()
        {
            if (game.Active == null) return string.Empty;
            IEnumerable<HexCoord> mapHexes = Enumerable.Range(0, game.Area.Width).SelectMany(q => Enumerable.Range(0, game.Area.Height).Select(r => new HexCoord(q, r))).Where(game.Area.Contains);
            int legalMoveHexes = mapHexes.Count(hex => { int distance = HexCoord.Distance(game.Active.Position, hex); return distance > 0 && distance <= game.Active.EffectiveMove; });
            int searchable = mapHexes.Count(hex => HexCoord.Distance(game.Active.Position, hex) <= game.SearchRangeFor(game.Active, CurrentSearchMode()));
            int strikeable = game.Contacts.Count(c => c.Owner == game.Active.Side && !c.IsLost && HexCoord.Distance(game.Active.Position, c.LastKnownPosition) <= game.StrikeRangeFor(game.Active, CurrentSalvo()));
            bool recover = game.Active.Friction || game.Active.Disruption;
            int supportable = game.Formations.Count(candidate => candidate.Side == game.Active.Side && candidate != game.Active && !candidate.IsDestroyed && HexCoord.Distance(game.Active.Position, candidate.Position) <= Rules.SupportRange);
            return $"Move({legalMoveHexes} hexes); Search({searchable} area hexes); Strike({strikeable} Contacts); Patrol(true); Support({supportable} formations); Recover({recover}); Replenish({game.HasLogisticsAccess(game.Active) && game.NeedsReplenishment(game.Active)}); Hold(true)";
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

    }
}
