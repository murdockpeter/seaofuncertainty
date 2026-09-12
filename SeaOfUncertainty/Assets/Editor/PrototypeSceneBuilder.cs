#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using SeaOfUncertainty.Core;
using SeaOfUncertainty.Prototype;

namespace SeaOfUncertainty.Editor
{
    public static class PrototypeSceneBuilder
    {
        private const string RuntimePanelPath = "Assets/Resources/UI/SeaRuntimePanelSettings.asset";

        [MenuItem("Sea of Uncertainty/Rebuild Prototype Scene")]
        public static void Build()
        {
            EnsureRuntimePanelSettings();
            Ensure3DMaterials();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Prototype Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.01f, .03f, .05f);
            camera.orthographic = true;
            cameraObject.tag = "MainCamera";
            const string path = "Assets/Scenes/Prototype.unity";
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, path);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(path, true) };
            PlayerSettings.productName = "Sea of Uncertainty";
            PlayerSettings.companyName = "Sea of Uncertainty Design Lab";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            AssetDatabase.SaveAssets();
            Debug.Log("Sea of Uncertainty prototype scene rebuilt.");
        }

        public static void BuildWindows()
        {
            Build();
            System.IO.Directory.CreateDirectory("Builds/Windows");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Prototype.unity" },
                locationPathName = "Builds/Windows/Sea of Uncertainty.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new System.Exception("Prototype build failed: " + report.summary.result);
            Debug.Log("Sea of Uncertainty Windows prototype built successfully.");
        }

        public static void RunEntropyRevealRegressionTests()
        {
            var game = new PrototypeGame(1978);
            FormationState player = game.Active;
            FormationState opponent = game.Formations.First(formation => formation.Side != player.Side);
            EntropyEffectDefinition playerCard = game.MarkEntropy(player, EntropySource.Friction);
            EntropyEffectDefinition opponentCard = game.MarkEntropy(opponent, EntropySource.Friction);
            Assert(playerCard != null && opponentCard != null, "Both sides draw independent Friction cards");
            Assert(game.PendingEntropyEffectFor(player.Side)?.Id == playerCard.Id, "The player's reveal remains queued after an opposing draw");
            Assert(game.PendingEntropyEffectFor(opponent.Side)?.Id == opponentCard.Id, "The opposing reveal remains independently queued");
            game.ConsumePendingEntropyEffect(opponent.Side);
            Assert(game.PendingEntropyEffectFor(player.Side)?.Id == playerCard.Id, "Consuming the opposing reveal cannot consume the player's reveal");

            string saveJson = JsonUtility.ToJson(game.CaptureState());
            var restored = new PrototypeGame(1978);
            restored.RestoreState(JsonUtility.FromJson<PrototypeGame.SaveData>(saveJson));
            Assert(restored.PendingEntropyEffectFor(player.Side)?.Id == playerCard.Id, "The pending player reveal survives save and load");
            Debug.Log("Entropy reveal regression tests passed.");
        }

        public static void RunCardExpansionTests()
        {
            Assert(EntropyEffectCatalog.All.Count == 36, "The Entropy catalog contains all 36 cards");
            foreach (EntropySource source in System.Enum.GetValues(typeof(EntropySource))) Assert(EntropyEffectCatalog.For(source).Count() == 12, source + " contains 12 cards");
            Assert(EntropyEffectCatalog.All.All(EntropyEffectCatalog.IsFullyMechanicallySupported), "All 36 Entropy cards have an active mechanical path");
            Assert(EntropyEffectCatalog.All.Count(card => !string.IsNullOrEmpty(card.Response)) == 3 && EntropyEffectCatalog.All.Where(card => !string.IsNullOrEmpty(card.Response)).All(card => card.ResponseCommandCost == 1 && card.ResponseWindow == EntropyResponseWindow.BeforeNextOwnAction), "All printed Entropy responses cost one Command Slot and close after the affected Formation's next own Action");
            Assert(Rules.IsComplexAction(ActionKind.Move) && Rules.IsComplexAction(ActionKind.Search) && Rules.IsComplexAction(ActionKind.Strike) && Rules.IsComplexAction(ActionKind.Patrol) && Rules.IsComplexAction(ActionKind.Support) && !Rules.IsComplexAction(ActionKind.Recover) && !Rules.IsComplexAction(ActionKind.Replenish) && !Rules.IsComplexAction(ActionKind.Hold), "The exhaustive complex-Action list is Move, Search, Strike, Patrol, and Support");
            Assert(Rules.IsMajorAction(ActionKind.Move) && Rules.IsMajorAction(ActionKind.Search) && Rules.IsMajorAction(ActionKind.Strike) && !Rules.IsMajorAction(ActionKind.Patrol) && !Rules.IsMajorAction(ActionKind.Support), "The exhaustive major-Action list is Move, Search, and Strike");
            Assert(CommandResponseCatalog.All.Count == 24 && CommandResponseCatalog.All.Select(card => card.Id).Distinct().Count() == 24, "The Command Response catalog contains 24 unique cards");

            var game = new PrototypeGame(1978);
            Assert(game.EntropyDecks.All(deck => deck.DrawPile.Count == 12), "Each Entropy source deck begins with 12 cards");
            Assert(game.CommandResponseDecks.Count == 2 && game.CommandResponseDecks.All(deck => deck.Hand.Count == 3 && deck.DrawPile.Count == 21), "Each side begins with three private Responses");

            var missionEventGame = new PrototypeGame(1978);
            ActionKind? completedMission = null;
            missionEventGame.ActionCompleted += (formation, mission) => completedMission = mission;
            Assert(missionEventGame.Hold(missionEventGame.Active, out string heldMessage) && completedMission == ActionKind.Hold, "Completing a mission emits its distinct audio cue signal: " + heldMessage);

            var stackGame = new PrototypeGame(1978);
            FormationState stackFormation = stackGame.Active;
            EntropyEffectDefinition firstStackCard = stackGame.MarkEntropy(stackFormation, EntropySource.Friction);
            EntropyEffectDefinition secondStackCard = stackGame.MarkEntropy(stackFormation, EntropySource.Friction);
            Assert(firstStackCard != null && secondStackCard != null && firstStackCard.Id != secondStackCard.Id && stackFormation.ActiveEffectCardIds.Count == 2, "Repeated Friction events draw and stack distinct physical cards");
            Assert(stackGame.Recover(stackFormation, out string stackedRecovery) && stackFormation.Friction && stackFormation.ActiveEffectCardIds.Count == 1, "Recover discards one stacked card while the remaining card keeps its source marked: " + stackedRecovery);

            FormationState actor = game.Active;
            actor.ActiveEffectCardIds.Add("F-07");
            Assert(Rules.MoveAllowance(actor, MoveMode.Normal) == 1, "Navigation Drift reduces the first normal Move by one hex");
            actor.ActiveEffectCardIds.Add("D-08");
            Assert(!game.SearchArea(actor, actor.Position, SearchMode.Focused, out string jammedMessage) && jammedMessage.Contains("Jammed Circuits"), "Jammed Circuits blocks Focused Search");
            actor.ActiveEffectCardIds.Add("X-11");
            Assert(!actor.CanHeavySalvo, "Launcher Damage blocks Heavy Salvo");

            var contradictionGame = new PrototypeGame(1978);
            FormationState contradictionActor = contradictionGame.Active;
            ContactState contradictionContact = contradictionGame.Contacts.First(contact => contact.Owner == contradictionActor.Side && !contact.IsLost && !contact.IsFalse);
            int originalPossibleHexes = contradictionGame.ContactPossibleHexes(contradictionContact).Count;
            EntropyDeckState disruptionDeck = contradictionGame.EntropyDecks.First(deck => deck.Source == EntropySource.Disruption);
            disruptionDeck.DrawPile.Remove("D-03"); disruptionDeck.DrawPile.Insert(0, "D-03");
            Assert(contradictionGame.MarkEntropy(contradictionActor, EntropySource.Disruption)?.Id == "D-03" && contradictionContact.HasContradictoryPosition && HexCoord.Distance(contradictionContact.LastKnownPosition, contradictionContact.ContradictoryPosition) == 1, "D-03 places a second adjacent possible-position marker");
            Assert(contradictionGame.ContactPossibleHexes(contradictionContact).Count > originalPossibleHexes, "D-03 expands authoritative Contact uncertainty geometry");

            var synchronizationGame = new PrototypeGame(1978);
            FormationState synchronizationActor = synchronizationGame.Active;
            FormationState synchronizationRecipient = synchronizationGame.Formations.First(formation => formation.Side == synchronizationActor.Side && formation != synchronizationActor);
            synchronizationRecipient.Position = synchronizationActor.Position;
            synchronizationActor.Mission = ActionKind.Support;
            synchronizationActor.Friction = true;
            synchronizationActor.ActiveEffectCardIds.Add("F-05");
            int synchronizationStart = synchronizationGame.Time;
            Assert(synchronizationGame.Support(synchronizationActor, synchronizationRecipient, SupportKind.Synchronization, out string driftMessage) && synchronizationActor.ReadyTime == synchronizationStart + 3, "F-05 adds +1 Time to Synchronization Support while stacking with the universal Friction +1: " + driftMessage);

            var blockedSynchronizationGame = new PrototypeGame(1978);
            FormationState blockedSynchronizationActor = blockedSynchronizationGame.Active;
            FormationState blockedSynchronizationRecipient = blockedSynchronizationGame.Formations.First(formation => formation.Side == blockedSynchronizationActor.Side && formation != blockedSynchronizationActor);
            blockedSynchronizationRecipient.Position = blockedSynchronizationActor.Position;
            blockedSynchronizationActor.Friction = true;
            blockedSynchronizationActor.ActiveEffectCardIds.Add("F-02");
            Assert(!blockedSynchronizationGame.Support(blockedSynchronizationActor, blockedSynchronizationRecipient, SupportKind.Synchronization, out string overloadMessage) && overloadMessage.Contains("Staff Overload"), "F-02 blocks Synchronization Support before its expiry or response");
            blockedSynchronizationActor.ActiveEffectCardIds.Remove("F-02");
            blockedSynchronizationActor.Disruption = true;
            blockedSynchronizationActor.ActiveEffectCardIds.Add("D-12");
            Assert(!blockedSynchronizationGame.Support(blockedSynchronizationActor, blockedSynchronizationRecipient, SupportKind.Synchronization, out string plotMessage) && plotMessage.Contains("Compromised Plot"), "D-12 blocks Synchronization Support until Recover");
            Assert(blockedSynchronizationGame.Recover(blockedSynchronizationActor, EntropySource.Disruption, "D-12", out string plotRecovery) && !blockedSynchronizationActor.HasEffect("D-12") && blockedSynchronizationGame.CanParticipateInSynchronization(blockedSynchronizationActor), "Recover removes D-12 and restores Synchronization eligibility: " + plotRecovery);

            var overloadExpiryGame = new PrototypeGame(1978);
            FormationState overloadActor = overloadExpiryGame.Active;
            EntropyDeckState overloadDeck = overloadExpiryGame.EntropyDecks.First(deck => deck.Source == EntropySource.Friction);
            overloadDeck.DrawPile.Remove("F-02"); overloadDeck.DrawPile.Insert(0, "F-02");
            Assert(overloadExpiryGame.MarkEntropy(overloadActor, EntropySource.Friction)?.Id == "F-02" && !overloadExpiryGame.CanParticipateInSynchronization(overloadActor), "F-02 immediately blocks Synchronization");
            Assert(overloadExpiryGame.Hold(overloadActor, out _), "Advance the F-02 duration through its drawing Action");
            int overloadGuard = 0;
            while (overloadExpiryGame.Active != overloadActor && overloadGuard++ < 40) Assert(overloadExpiryGame.Hold(overloadExpiryGame.Active, out _), "Advance to the overloaded Formation's next Action");
            Assert(overloadExpiryGame.Active == overloadActor && overloadActor.HasEffect("F-02") && overloadExpiryGame.Hold(overloadActor, out _) && !overloadActor.HasEffect("F-02") && overloadExpiryGame.CanParticipateInSynchronization(overloadActor), "F-02 clears after the Formation completes another own Action");

            var entropyResponseGame = new PrototypeGame(1978);
            FormationState responseActor = entropyResponseGame.Active;
            EntropyDeckState responseFrictionDeck = entropyResponseGame.EntropyDecks.First(deck => deck.Source == EntropySource.Friction);
            responseFrictionDeck.DrawPile.Remove("F-01"); responseFrictionDeck.DrawPile.Insert(0, "F-01");
            Assert(entropyResponseGame.MarkEntropy(responseActor, EntropySource.Friction)?.Id == "F-01" && entropyResponseGame.CanRespondToEntropy(responseActor, "F-01"), "A printed Entropy response opens when its card is revealed");
            string responseSave = JsonUtility.ToJson(entropyResponseGame.CaptureState());
            var restoredResponseGame = new PrototypeGame(1978);
            restoredResponseGame.RestoreState(JsonUtility.FromJson<PrototypeGame.SaveData>(responseSave));
            FormationState restoredResponseActor = restoredResponseGame.Find(responseActor.Id);
            int freeCommandBeforeResponse = restoredResponseGame.Sides[restoredResponseActor.Side].CommandSlots;
            Assert(restoredResponseGame.CanRespondToEntropy(restoredResponseActor, "F-01"), "Entropy response windows survive save/load");
            Assert(restoredResponseGame.RespondToEntropy(restoredResponseActor, "F-01", out string entropyResponseMessage), "A restored Entropy response may be exercised: " + entropyResponseMessage);
            Assert(restoredResponseGame.Sides[restoredResponseActor.Side].CommandSlots == freeCommandBeforeResponse - 1 && !restoredResponseActor.HasEffect("F-01"), "The printed response occupies exactly one Command Slot and cancels the effect");
            Assert(restoredResponseGame.Hold(restoredResponseActor, out _) && restoredResponseGame.Sides[restoredResponseActor.Side].CommandSlots == freeCommandBeforeResponse, "The Entropy-response Command Slot releases after the affected Formation completes its next own Action");

            CommandResponseDeckState hand = game.CommandResponseDecks.First(deck => deck.Side == actor.Side);
            hand.DrawPile.Remove("C-22");
            if (!hand.Hand.Contains("C-22")) hand.Hand.Add("C-22");
            int command = actor.EffectiveCommand;
            Assert(game.PlayCommandResponse(actor.Side, "C-22", actor, null, null, out string responseMessage) && actor.EffectiveCommand == command + 1, "Local Initiative applies +1 Command: " + responseMessage);
            Assert(hand.DiscardPile.Contains("C-22") && !hand.Hand.Contains("C-22"), "A played Response moves to discard");

            var replanGame = new PrototypeGame(1978);
            FormationState replanned = replanGame.Active;
            CommandResponseDeckState replanHand = replanGame.CommandResponseDecks.First(deck => deck.Side == replanned.Side);
            replanHand.DrawPile.Remove("C-04");
            if (!replanHand.Hand.Contains("C-04")) replanHand.Hand.Add("C-04");
            ActionKind newMission = replanned.Mission == ActionKind.Strike ? ActionKind.Search : ActionKind.Strike;
            int commandBeforeReplan = replanGame.Sides[replanned.Side].CommandSlots;
            Assert(replanGame.PlayCommandResponse(replanned.Side, "C-04", replanned, null, null, newMission, out string replanMessage), "Rapid Replan resolves: " + replanMessage);
            Assert(replanned.Mission == newMission && replanned.NextReadyTimeBonus == 1 && replanGame.Sides[replanned.Side].CommandSlots == commandBeforeReplan, "Rapid Replan changes Mission, adds next-Ready cost, and occupies no Command Slot");

            var withdrawalGame = new PrototypeGame(1978);
            FormationState withdrawalAttacker = withdrawalGame.Active;
            FormationState withdrawalTarget = withdrawalGame.Formations.First(candidate => candidate.Side != withdrawalAttacker.Side && candidate.Kind == FormationKind.Submarine);
            withdrawalTarget.Position = new HexCoord(1, 7);
            withdrawalTarget.Ratings.Defense = 20;
            ContactState withdrawalContact = withdrawalGame.ContactFor(withdrawalAttacker.Side, withdrawalTarget.Id);
            if (withdrawalContact == null)
            {
                withdrawalContact = new ContactState { Owner = withdrawalAttacker.Side, TargetId = withdrawalTarget.Id };
                withdrawalGame.Contacts.Add(withdrawalContact);
            }
            withdrawalContact.LastKnownPosition = withdrawalTarget.Position;
            withdrawalContact.Location = LocationQuality.High;
            withdrawalContact.Identity = IdentityQuality.Identified;
            ContactState withdrawalThreatContact = withdrawalGame.ContactFor(withdrawalTarget.Side, withdrawalAttacker.Id);
            if (withdrawalThreatContact == null)
            {
                withdrawalThreatContact = new ContactState { Owner = withdrawalTarget.Side, TargetId = withdrawalAttacker.Id };
                withdrawalGame.Contacts.Add(withdrawalThreatContact);
            }
            withdrawalThreatContact.LastKnownPosition = withdrawalAttacker.Position;
            withdrawalThreatContact.Location = LocationQuality.High;
            CommandResponseDeckState withdrawalHand = withdrawalGame.CommandResponseDecks.First(deck => deck.Side == withdrawalTarget.Side);
            withdrawalHand.DrawPile.Remove("C-18");
            if (!withdrawalHand.Hand.Contains("C-18")) withdrawalHand.Hand.Add("C-18");
            Assert(withdrawalGame.PlayCommandResponse(withdrawalTarget.Side, "C-18", withdrawalTarget, null, null, out string withdrawalMessage) && withdrawalTarget.OrderlyWithdrawalReady, "Orderly Withdrawal can be prepared: " + withdrawalMessage);
            HexCoord withdrawalStart = withdrawalTarget.Position;
            int rangeBeforeWithdrawal = HexCoord.Distance(withdrawalAttacker.Position, withdrawalStart);
            HexCoord withdrawalDestination = withdrawalGame.LegalEvadeDestinations(withdrawalAttacker, withdrawalTarget, 2).First();
            Assert(withdrawalGame.Strike(withdrawalAttacker, withdrawalTarget, Salvo.Light, Reaction.Evade, withdrawalDestination, out CombatResult withdrawalResult, out string strikeMessage), "Strike against prepared withdrawal resolves: " + strikeMessage);
            Assert(withdrawalResult.Reaction == Reaction.Evade && withdrawalResult.Withdrew && HexCoord.Distance(withdrawalStart, withdrawalTarget.Position) <= 2 && HexCoord.Distance(withdrawalAttacker.Position, withdrawalTarget.Position) > rangeBeforeWithdrawal && !withdrawalTarget.OrderlyWithdrawalReady, "Orderly Withdrawal uses Evade, moves up to two hexes away, and is consumed");

            string json = JsonUtility.ToJson(game.CaptureState());
            var restored = new PrototypeGame(1978);
            restored.RestoreState(JsonUtility.FromJson<PrototypeGame.SaveData>(json));
            Assert(restored.CommandResponseDecks.SelectMany(deck => deck.Hand).SequenceEqual(game.CommandResponseDecks.SelectMany(deck => deck.Hand)), "Response hands survive save and load");
            Assert(restored.Find(actor.Id).ActiveEffectCardIds.Contains("X-11"), "Expanded Entropy attachments survive save and load");
            Debug.Log("Card expansion tests passed: 36 Entropy cards, 24 Command Responses, decks, supported mechanics, and save/load.");
        }

        public static void RunCommandMissionTests()
        {
            var freeGame = new PrototypeGame(1978);
            FormationState freeActor = freeGame.Active;
            Assert(freeGame.CommandSlotsFor(freeActor.Side).Count == 3 && freeGame.CommandSlotsFor(freeActor.Side).All(slot => slot.Status == CommandSlotStatus.Free), "Each side begins with three individually modeled free Command Slots");
            string missionActionMessage = string.Empty;
            Assert(freeActor.Mission == ActionKind.Search && freeGame.SearchArea(freeActor, freeActor.Position, SearchMode.Passive, out missionActionMessage), "Initial submarine Search follows its Standing Mission: " + missionActionMessage);
            Assert(freeActor.LastActionFollowedMission && freeGame.Sides[freeActor.Side].CommandSlots == 3, "A mission-following Action occupies no Command Attention");

            var retaskGame = new PrototypeGame(1978);
            FormationState retasked = retaskGame.Active;
            Assert(retaskGame.Hold(retasked, out string retaskMessage), "An immediate out-of-Mission Hold can be ordered: " + retaskMessage);
            Assert(!retasked.LastActionFollowedMission && retaskGame.Sides[retasked.Side].CommandSlots == 3 && retaskGame.Log.Any(entry => entry.Contains("COMMAND OCCUPIED") && entry.Contains("out-of-Mission")), "Immediate retasking occupies a Slot through action resolution, then releases it and records telemetry-visible log state");

            var editorGame = new PrototypeGame(1978);
            FormationState edited = editorGame.Active;
            HexCoord moveDestination = Enumerable.Range(0, editorGame.Area.Width).SelectMany(q => Enumerable.Range(0, editorGame.Area.Height).Select(r => new HexCoord(q, r)))
                .First(hex => editorGame.Area.Contains(hex) && editorGame.Area.TerrainAt(hex) != OperationalTerrain.Land && HexCoord.Distance(edited.Position, hex) == 1);
            Assert(editorGame.AssignStandingMission(edited, ActionKind.Move, MissionObjectiveKind.OperationalObjective, null, editorGame.Area.Objective, MissionPosture.Cautious, MissionTrigger.ObjectiveReached, out string editMessage), "Standing Mission editor assigns Task, Objective, Posture, and Trigger: " + editMessage);
            Assert(edited.Mission == ActionKind.Move && edited.MissionObjective == MissionObjectiveKind.OperationalObjective && edited.MissionPosture == MissionPosture.Cautious && edited.MissionTrigger == MissionTrigger.ObjectiveReached && editorGame.Sides[edited.Side].CommandSlots == 2, "Immediate Mission change occupies one individual Slot until the Formation acts");
            Assert(editorGame.Move(edited, moveDestination, MoveMode.Cautious, out string editedMoveMessage) && edited.LastActionFollowedMission && editorGame.Sides[edited.Side].CommandSlots == 3, "Executing the newly assigned Task is attention-free and releases Mission-change attention: " + editedMoveMessage);

            var priorityGame = new PrototypeGame(1978);
            FormationState priority = priorityGame.Active;
            priority.Friction = true; priority.ActiveEffectCardIds.Add("F-03"); priority.ActiveEffectCardIds.Add("F-12");
            Assert(priorityGame.AssignStandingMission(priority, ActionKind.Hold, MissionObjectiveKind.CurrentArea, null, priority.Position, MissionPosture.Balanced, MissionTrigger.OnReady, out _), "F-03 still permits a Mission change when two Slots are free");
            Assert(priorityGame.Sides[priority.Side].CommandSlots == 1 && priority.NextReadyTimeBonus == 1, "F-03 occupies one additional Slot and F-12 adds +1 Time to the next Ready schedule");

            var delegatedGame = new PrototypeGame(1978);
            FormationState delegated = delegatedGame.Active;
            CommandResponseDeckState delegatedHand = delegatedGame.CommandResponseDecks.First(deck => deck.Side == delegated.Side);
            delegatedHand.DrawPile.Remove("C-07"); if (!delegatedHand.Hand.Contains("C-07")) delegatedHand.Hand.Add("C-07");
            Assert(delegatedGame.PlayCommandResponse(delegated.Side, "C-07", delegated, null, null, out string delegateMessage) && delegated.MissionChangeLockedUntilAction, "C-07 grants delegated authority and locks Mission changes: " + delegateMessage);
            Assert(!delegatedGame.AssignStandingMission(delegated, ActionKind.Hold, MissionObjectiveKind.CurrentArea, null, delegated.Position, MissionPosture.Balanced, MissionTrigger.OnReady, out string lockedMessage) && lockedMessage.Contains("Delegated Authority"), "Delegated Authority rejects Mission editing until the Formation acts");

            var latencyGame = new PrototypeGame(1978);
            FormationState delayed = latencyGame.Active;
            ActionKind originalTask = delayed.Mission;
            delayed.Disruption = true; delayed.ActiveEffectCardIds.Add("D-02");
            Assert(latencyGame.AssignStandingMission(delayed, ActionKind.Move, MissionObjectiveKind.OperationalObjective, null, latencyGame.Area.Objective, MissionPosture.Balanced, MissionTrigger.ObjectiveReached, out string delayMessage), "D-02 accepts a delayed order: " + delayMessage);
            Assert(delayed.PendingMissionChange && delayed.Mission == originalTask && delayed.MissionDeliveryTime == latencyGame.Time + 1, "Communications Latency keeps the existing Mission in force for one Time");
            string delayedJson = JsonUtility.ToJson(latencyGame.CaptureState());
            var delayedRestore = new PrototypeGame(1978);
            delayedRestore.RestoreState(JsonUtility.FromJson<PrototypeGame.SaveData>(delayedJson));
            Assert(delayedRestore.Find(delayed.Id).PendingMissionChange && delayedRestore.CommandSlotsFor(delayed.Side).Any(slot => slot.Purpose == "Delayed Mission Order"), "Pending orders and occupied individual Slots survive save/load");
            int delayGuard = 0;
            while (latencyGame.Time < delayed.MissionDeliveryTime && delayGuard++ < 40) Assert(latencyGame.Hold(latencyGame.Active, out string cadence), "Advance delayed order clock: " + cadence);
            Assert(!delayed.PendingMissionChange && delayed.Mission == ActionKind.Move && latencyGame.CommandSlotsFor(delayed.Side).All(slot => slot.Purpose != "Delayed Mission Order"), "Delayed Mission arrives and its Command Slot releases at delivery Time");

            var brokenGame = new PrototypeGame(1978);
            FormationState broken = brokenGame.Active;
            broken.Disruption = true; broken.ActiveEffectCardIds.Add("D-04");
            broken.MissionTrigger = MissionTrigger.EntropyMarked;
            broken.Friction = true; broken.ActiveEffectCardIds.Add("F-01");
            Assert(!brokenGame.AssignStandingMission(broken, ActionKind.Hold, MissionObjectiveKind.CurrentArea, null, broken.Position, MissionPosture.Balanced, MissionTrigger.OnReady, out string brokenMessage) && brokenMessage.Contains("Broken Link"), "D-04 rejects direct new orders");
            Assert(!brokenGame.Recover(broken, EntropySource.Friction, "F-01", out string blockedTrigger) && blockedTrigger.Contains("Mission Command"), "Broken Link also blocks an otherwise valid trigger retask until C-03 authorizes it");
            CommandResponseDeckState brokenHand = brokenGame.CommandResponseDecks.First(deck => deck.Side == broken.Side);
            brokenHand.DrawPile.Remove("C-03"); if (!brokenHand.Hand.Contains("C-03")) brokenHand.Hand.Add("C-03");
            Assert(brokenGame.PlayCommandResponse(broken.Side, "C-03", broken, null, null, out string missionCommandMessage) && broken.TriggerMissionCommandReady, "C-03 prepares one Broken Link Trigger: " + missionCommandMessage);
            Assert(brokenGame.Recover(broken, EntropySource.Friction, "F-01", out string triggeredRecover) && broken.Mission == ActionKind.Recover && !broken.TriggerMissionCommandReady, "Authorized Entropy Trigger automatically retasks and executes without Command Attention: " + triggeredRecover);

            var flashGame = new PrototypeGame(1978);
            FormationState flash = flashGame.Active;
            flash.Disruption = true; flash.ActiveEffectCardIds.Add("D-04");
            CommandResponseDeckState flashHand = flashGame.CommandResponseDecks.First(deck => deck.Side == flash.Side);
            flashHand.DrawPile.Remove("C-17"); if (!flashHand.Hand.Contains("C-17")) flashHand.Hand.Add("C-17");
            Assert(flashGame.PlayCommandResponse(flash.Side, "C-17", flash, null, null, ActionKind.Hold, out string flashMessage) && flash.Mission == ActionKind.Hold && flashGame.Sides[flash.Side].CommandStrain == 1, "C-17 penetrates Broken Link immediately and marks Command Strain: " + flashMessage);
            flash.Friction = true; flash.Destruction = true;
            Assert(flashGame.PushThrough(flash, out string pushMessage) && flashGame.Sides[flash.Side].CommandStrain == 2 && flashGame.CommandSlotsFor(flash.Side).Count(slot => slot.Status == CommandSlotStatus.Strained) == 1, "Push Through reaches the two-Strain threshold and removes one Slot: " + pushMessage);

            var pushGame = new PrototypeGame(1978);
            FormationState pushed = pushGame.Active;
            pushed.Friction = pushed.Disruption = pushed.Destruction = true;
            HexCoord pushedDestination = Enumerable.Range(0, pushGame.Area.Width).SelectMany(q => Enumerable.Range(0, pushGame.Area.Height).Select(r => new HexCoord(q, r)))
                .First(hex => pushGame.Area.Contains(hex) && pushGame.Area.TerrainAt(hex) != OperationalTerrain.Land && HexCoord.Distance(pushed.Position, hex) == 1);
            Assert(!pushGame.Move(pushed, pushedDestination, MoveMode.Cautious, out string disorganizedMessage) && disorganizedMessage.Contains("Push Through"), "Disorganized formations cannot begin complex Actions without Push Through");
            string pushedMessage = string.Empty;
            Assert(pushGame.PushThrough(pushed, out _) && pushGame.Move(pushed, pushedDestination, MoveMode.Cautious, out pushedMessage), "Push Through authorizes exactly one complex Action: " + pushedMessage);

            Side flashSide = flash.Side;
            FormationState headquarters = flashGame.Formations.First(candidate => candidate.Side == flashSide && candidate.Kind == FormationKind.CarrierGroup);
            headquarters.Position = flashGame.LogisticsFacilitiesFor(headquarters).First().Hex;
            PrototypeGame.SaveData hqState = flashGame.CaptureState(); hqState.ActiveFormationId = headquarters.Id; flashGame.RestoreState(hqState); headquarters = flashGame.Find(headquarters.Id);
            Assert(flashGame.RestoreCommand(headquarters, out string restoreMessage) && flashGame.Sides[flashSide].CommandStrain == 0 && flashGame.CommandSlotsFor(flashSide).All(slot => slot.Status == CommandSlotStatus.Free), "Carrier HQ Recovery at logistics removes two Strain and restores the strained Slot: " + restoreMessage);

            var aiMissionGame = new PrototypeGame(1978);
            FormationState aiMissionActor = aiMissionGame.Active;
            foreach (CommandSlotState slot in aiMissionGame.CommandSlotsFor(aiMissionActor.Side)) { slot.Status = CommandSlotStatus.Strained; slot.Purpose = "Test Strain"; }
            aiMissionGame.Sides[aiMissionActor.Side].CommandSlots = 0;
            Assert(PrototypeAiCommander.Choose(aiMissionGame).Action == aiMissionActor.Mission, "AI continues its Standing Mission when no Command Attention is free");
            Debug.Log("Command Attention and Standing Mission tests passed: vocabulary, slot lifecycle, editor state, triggers, delayed orders, cards, strain, Push Through, HQ Recovery, AI-safe authorization, and save/load.");
        }

        public static void RunReplenishmentTests()
        {
            Assert(Rules.ActionTime(ActionKind.Replenish) == 3, "Replenishment has a three-Time base schedule");
            var accessGame = new PrototypeGame(1978);
            FormationState naval = accessGame.Active;
            FormationState air = accessGame.Formations.First(candidate => candidate.Side == naval.Side && candidate.Kind == FormationKind.AirGroup);
            foreach (FormationState logistics in accessGame.Formations.Where(candidate => candidate.Side == naval.Side && candidate.Kind == FormationKind.LogisticsGroup)) logistics.Damage = DamageState.Destroyed;
            HexCoord westPort = accessGame.Area.Locations.First(location => location.Id == "west-haven").Hex;
            HexCoord westAirfield = accessGame.Area.Locations.First(location => location.Id == "west-haven-airfield").Hex;
            naval.Position = westPort;
            air.Position = westPort;
            Assert(accessGame.HasLogisticsAccess(naval) && !accessGame.HasLogisticsAccess(air), "Naval formations use scenario Ports while air groups cannot replenish there");
            air.Position = westAirfield;
            naval.Position = westAirfield;
            Assert(accessGame.HasLogisticsAccess(air) && !accessGame.HasLogisticsAccess(naval), "Air groups use scenario Airfields while naval formations cannot replenish there");

            var portMoveGame = new PrototypeGame(1978);
            FormationState portMover = portMoveGame.Active;
            portMover.Position = new HexCoord(1, 3);
            Assert(portMoveGame.Move(portMover, westPort, MoveMode.Cautious, out string portMoveMessage), "A naval formation may enter a data-defined Land Port endpoint: " + portMoveMessage);

            var game = new PrototypeGame(1978);
            FormationState formation = game.Active;
            formation.Position = westPort;
            formation.Endurance = Endurance.Critical;
            formation.WeaponExpended = true;
            formation.Damage = DamageState.Crippled;
            formation.MajorActions = 2;
            formation.Destruction = true;
            formation.ActiveEffectCardIds.Add("X-10");
            int actionTime = game.Time;
            Assert(game.ReplenishmentPreview(formation).Contains("Heavy Salvo") && game.ReplenishmentPreview(formation).Contains("X") == false, "Replenishment preview exposes restoration without leaking unrelated state");
            Assert(game.Replenish(formation, "X-10", out string replenishMessage), "Eligible Formation replenishes: " + replenishMessage);
            Assert(formation.Endurance == Endurance.Extended && !formation.WeaponExpended && formation.Damage == DamageState.Heavy && formation.MajorActions == 0, "One service package improves Endurance and damage one step, reloads Heavy Salvo, and resets the action track");
            Assert(!formation.ActiveEffectCardIds.Contains("X-10") && !formation.Destruction && formation.ReadyTime == actionTime + 4, "Selected Destruction card is repaired and X-10 adds one Time to its own Replenishment");
            Assert(formation.Replenishing && game.AvailableReactions(game.Active, formation).SequenceEqual(new[] { Reaction.None }), "A servicing Formation cannot React before its Ready Time arrives");

            string json = JsonUtility.ToJson(game.CaptureState());
            var restored = new PrototypeGame(1978);
            restored.RestoreState(JsonUtility.FromJson<PrototypeGame.SaveData>(json));
            Assert(restored.Find(formation.Id).Replenishing && restored.Find(formation.Id).ReadyTime == formation.ReadyTime, "In-progress Replenishment survives save and load");
            int guard = 0;
            while (game.Active != formation && game.Active != null && guard++ < 80) Assert(game.Hold(game.Active, out string cadenceMessage), "Advance servicing clock: " + cadenceMessage);
            Assert(game.Active == formation && !formation.Replenishing, "Reaction availability returns when the replenished Formation becomes Ready");

            var emptyGame = new PrototypeGame(1978);
            FormationState empty = emptyGame.Active;
            empty.Position = westPort;
            Assert(!emptyGame.Replenish(empty, null, out string emptyMessage) && emptyMessage.Contains("no Endurance"), "Replenishment rejects a Formation with nothing to restore");
            empty.Endurance = Endurance.Extended;
            Assert(PrototypeAiCommander.Choose(emptyGame).Action == ActionKind.Replenish, "AI chooses legal Replenishment when useful at a compatible facility");
            Debug.Log("Replenishment and logistics tests passed: typed access, Port movement, restoration, X-10 timing, reaction lockout, AI, and save/load.");
        }

        public static void RunPatrolSupportTests()
        {
            Assert(Rules.PatrolRadius == 1 && Rules.SupportRange == 2 && Rules.ActionTime(ActionKind.Patrol) == 1 && Rules.ActionTime(ActionKind.Support) == 1, "Patrol and Support use their locked geometry and one-Time schedules");

            var patrolGame = new PrototypeGame(1978);
            FormationState screener = patrolGame.Active;
            HexCoord screenCenter = screener.Position;
            Assert(patrolGame.Patrol(screener, screenCenter, PatrolPosture.Aggressive, null, out string patrolMessage), "Aggressive Patrol can be assigned: " + patrolMessage);
            Assert(screener.PatrolActive && screener.PatrolInterceptionAvailable && screener.Loud && screener.ReadyTime == patrolGame.Time + 1, "Aggressive Screen persists, is Loud, and readies one Time later");
            string patrolJson = JsonUtility.ToJson(patrolGame.CaptureState());
            var restoredPatrol = new PrototypeGame(1978);
            restoredPatrol.RestoreState(JsonUtility.FromJson<PrototypeGame.SaveData>(patrolJson));
            Assert(restoredPatrol.Find(screener.Id).PatrolActive && restoredPatrol.Find(screener.Id).PatrolInterceptionAvailable, "Screen assignments survive save and load");

            FormationState mover = patrolGame.Formations.First(candidate => candidate.Side != screener.Side && !candidate.IsDestroyed);
            HexCoord entry = Enumerable.Range(0, patrolGame.Area.Width).SelectMany(q => Enumerable.Range(0, patrolGame.Area.Height).Select(r => new HexCoord(q, r)))
                .First(hex => patrolGame.Area.Contains(hex) && patrolGame.Area.TerrainAt(hex) != OperationalTerrain.Land && HexCoord.Distance(screenCenter, hex) == 1);
            HexCoord start = Enumerable.Range(0, patrolGame.Area.Width).SelectMany(q => Enumerable.Range(0, patrolGame.Area.Height).Select(r => new HexCoord(q, r)))
                .First(hex => patrolGame.Area.Contains(hex) && patrolGame.Area.TerrainAt(hex) != OperationalTerrain.Land && HexCoord.Distance(entry, hex) == 1 && HexCoord.Distance(screenCenter, hex) == 2);
            mover.Position = start;
            PrototypeGame.SaveData patrolState = patrolGame.CaptureState();
            patrolState.ActiveFormationId = mover.Id;
            patrolGame.RestoreState(patrolState);
            screener = patrolGame.Find(screener.Id); mover = patrolGame.Find(mover.Id);
            Assert(patrolGame.Move(mover, entry, MoveMode.Cautious, out string interceptMessage), "Enemy may enter a screened area: " + interceptMessage);
            Assert(!screener.PatrolInterceptionAvailable && patrolGame.ContactFor(screener.Side, mover.Id) != null && interceptMessage.Contains("intercepted"), "Entry triggers exactly one extra interception and creates targeting information");

            var supportGame = new PrototypeGame(1978);
            FormationState supporter = supportGame.Active;
            FormationState recipient = supportGame.Formations.First(candidate => candidate.Side == supporter.Side && candidate != supporter && !candidate.IsDestroyed);
            recipient.Position = supporter.Position;
            Assert(supportGame.Support(supporter, recipient, SupportKind.Strike, out string supportMessage), "Strike Support can be assigned: " + supportMessage);
            Assert(supporter.SupportActive && supporter.SupportRecipientId == recipient.Id && supportGame.PendingSupportBonus(recipient, SupportKind.Strike) == 1, "Support persists as a range-two +1 relationship until used");
            string supportJson = JsonUtility.ToJson(supportGame.CaptureState());
            var restoredSupport = new PrototypeGame(1978);
            restoredSupport.RestoreState(JsonUtility.FromJson<PrototypeGame.SaveData>(supportJson));
            Assert(restoredSupport.Find(supporter.Id).SupportActive && restoredSupport.Find(supporter.Id).SupportRecipientId == recipient.Id, "Support assignments survive save and load");

            FormationState target = supportGame.Formations.First(candidate => candidate.Side != recipient.Side && !candidate.IsDestroyed);
            target.Position = recipient.Position;
            target.Ratings.Defense = 20;
            ContactState contact = supportGame.ContactFor(recipient.Side, target.Id);
            if (contact == null) { contact = new ContactState { Owner = recipient.Side, TargetId = target.Id }; supportGame.Contacts.Add(contact); }
            contact.LastKnownPosition = target.Position; contact.Location = LocationQuality.High; contact.Identity = IdentityQuality.Identified; contact.Age = 0;
            PrototypeGame.SaveData supportState = supportGame.CaptureState(); supportState.ActiveFormationId = recipient.Id; supportGame.RestoreState(supportState);
            supporter = supportGame.Find(supporter.Id); recipient = supportGame.Find(recipient.Id); target = supportGame.Find(target.Id);
            Assert(supportGame.Strike(recipient, target, Salvo.Light, Reaction.Hold, out CombatResult supportedStrike, out string strikeMessage), "Recipient can spend Strike Support: " + strikeMessage);
            Assert(!supporter.SupportActive && supportedStrike.Attack == recipient.EffectiveStrike + 1 + Rules.TargetingModifier(supportGame.ContactFor(recipient.Side, target.Id), supportGame.AgeTwoTargetingPenalty), "Matching Support adds +1 and is consumed without stacking");

            var blockedGame = new PrototypeGame(1978);
            FormationState blocked = blockedGame.Active;
            blocked.ActiveEffectCardIds.Add("F-08"); blocked.Friction = true;
            HexCoord highTempoDestination = Enumerable.Range(0, blockedGame.Area.Width).SelectMany(q => Enumerable.Range(0, blockedGame.Area.Height).Select(r => new HexCoord(q, r)))
                .First(hex => blockedGame.Area.Contains(hex) && blockedGame.Area.TerrainAt(hex) != OperationalTerrain.Land && HexCoord.Distance(blocked.Position, hex) == 1);
            Assert(blockedGame.Move(blocked, highTempoDestination, MoveMode.HighTempo, out _) && blocked.SupportBlockedUntilRecover, "F-08 blocks later Support after High Tempo until Recover");
            Debug.Log("Patrol / Screen and Support tests passed: timing, geometry, persistence, interception, modifiers, cards, and save/load.");
        }

        public static void RunReactionTests()
        {
            PrototypeGame defendGame = ReactionFixture(out FormationState defendAttacker, out FormationState defendTarget);
            int defenderReadyBefore = defendTarget.ReadyTime;
            Assert(defendGame.AvailableReactions(defendAttacker, defendTarget).Count == 4, "A coherent ready defender with targeting data may choose all four Reactions");
            Assert(defendGame.Strike(defendAttacker, defendTarget, Salvo.Light, Reaction.Defend, null, out CombatResult defendResult, out string defendMessage), "Defend reaction resolves: " + defendMessage);
            Assert(defendResult.Reaction == Reaction.Defend && defendTarget.HasReacted && defendTarget.ReadyTime == defenderReadyBefore, "Defend grants a reaction without changing defender Ready Time");
            Assert(defendGame.AvailableReactions(defendAttacker, defendTarget).SequenceEqual(new[] { Reaction.None }), "A spent Formation cannot react again before its own Action");

            string reactedSave = JsonUtility.ToJson(defendGame.CaptureState());
            var reactedRestore = new PrototypeGame(1978);
            reactedRestore.RestoreState(JsonUtility.FromJson<PrototypeGame.SaveData>(reactedSave));
            Assert(reactedRestore.Find(defendTarget.Id).HasReacted, "Spent Reaction state survives save and load");

            int refreshGuard = 0;
            while (defendGame.Active != defendTarget && defendGame.Active != null && refreshGuard++ < 40) Assert(defendGame.Hold(defendGame.Active, out string cadenceMessage), "Advance to defender Action: " + cadenceMessage);
            Assert(defendGame.Active == defendTarget, "The reacted defender eventually becomes the active Formation");
            Assert(defendGame.Hold(defendTarget, out string refreshMessage) && !defendTarget.HasReacted, "A Formation refreshes its Reaction after completing its own Action: " + refreshMessage);

            PrototypeGame evadeGame = ReactionFixture(out FormationState evadeAttacker, out FormationState evadeTarget);
            HexCoord evadeStart = evadeTarget.Position;
            IReadOnlyList<HexCoord> oneHexEvades = evadeGame.LegalEvadeDestinations(evadeAttacker, evadeTarget, 1);
            Assert(oneHexEvades.Count > 0, "The reaction fixture exposes at least one legal one-hex Evade destination");
            Assert(evadeGame.Strike(evadeAttacker, evadeTarget, Salvo.Light, Reaction.Evade, oneHexEvades[0], out CombatResult evadeResult, out string evadeMessage), "Evade reaction resolves with a legal destination: " + evadeMessage);
            Assert(evadeResult.Reaction == Reaction.Evade && evadeResult.Withdrew && HexCoord.Distance(evadeStart, evadeTarget.Position) == 1 && evadeTarget.HasReacted, "Ordinary Evade gains Defense and moves exactly one legal hex after combat");

            PrototypeGame holdGame = ReactionFixture(out FormationState holdAttacker, out FormationState holdTarget);
            HexCoord holdPosition = holdTarget.Position;
            int holdBaseDefense = holdTarget.EffectiveDefense;
            Assert(holdGame.Strike(holdAttacker, holdTarget, Salvo.Light, Reaction.Hold, null, out CombatResult holdResult, out string holdReactionMessage), "Hold reaction resolves: " + holdReactionMessage);
            Assert(holdResult.Defense == holdBaseDefense + holdResult.MissileDefenseModifier && holdTarget.Position.Equals(holdPosition) && holdTarget.HasReacted, "Hold preserves position, adds no reaction Defense, and spends the Reaction");

            PrototypeGame counterGame = ReactionFixture(out FormationState counterAttacker, out FormationState counterTarget);
            Assert(counterGame.Strike(counterAttacker, counterTarget, Salvo.Light, Reaction.Counterattack, null, out CombatResult counterResult, out string counterMessage), "Counterattack reaction resolves: " + counterMessage);
            Assert(counterResult.Counterattacked && counterResult.CounterattackRoll >= 1 && counterResult.CounterattackRoll <= 6 && counterTarget.HasReacted, "Counterattack makes one bounded Light return strike and spends the Reaction");

            PrototypeGame restrictedGame = ReactionFixture(out FormationState restrictedAttacker, out FormationState restrictedTarget);
            restrictedTarget.Friction = true;
            restrictedTarget.Disruption = true;
            Assert(!restrictedGame.AvailableReactions(restrictedAttacker, restrictedTarget).Contains(Reaction.Counterattack), "Disrupted formations cannot Counterattack");
            restrictedTarget.Friction = false;
            restrictedTarget.Disruption = false;
            restrictedTarget.Damage = DamageState.Crippled;
            Assert(restrictedGame.AvailableReactions(restrictedAttacker, restrictedTarget).SequenceEqual(new[] { Reaction.Defend, Reaction.Hold }), "Crippled formations are limited to Defend or Hold");
            restrictedTarget.Replenishing = true;
            Assert(restrictedGame.AvailableReactions(restrictedAttacker, restrictedTarget).SequenceEqual(new[] { Reaction.None }), "Replenishing formations cannot react");

            var lightGame = new PrototypeGame(1978);
            FormationState lightFormation = lightGame.Active;
            int undamagedDefense = lightFormation.EffectiveDefense;
            lightFormation.Damage = DamageState.Light;
            lightFormation.LightDamageExpiresAfterAction = lightFormation.CompletedActions + 1;
            Assert(lightFormation.EffectiveDefense == undamagedDefense - 1, "Light damage applies -1 Defense");
            Assert(lightGame.Hold(lightFormation, out string lightMessage) && lightFormation.Damage == DamageState.None, "Light damage clears after the Formation completes its next own Action: " + lightMessage);
            Assert(Rules.CombineDamage(DamageState.Light, DamageState.Light) == DamageState.Heavy && Rules.CombineDamage(DamageState.Heavy, DamageState.Heavy) == DamageState.Crippled && Rules.CombineDamage(DamageState.Crippled, DamageState.Crippled) == DamageState.Destroyed, "Equal repeated damage escalates one step");
            Assert(Rules.CombineDamage(DamageState.Heavy, DamageState.Light) == DamageState.Heavy && Rules.CombineDamage(DamageState.Light, DamageState.Crippled) == DamageState.Crippled, "Lower repeated damage is ignored and higher incoming damage replaces current damage");

            PrototypeGame staleGame = ReactionFixture(out FormationState staleAttacker, out FormationState staleTarget);
            ContactState staleContact = staleGame.ContactFor(staleAttacker.Side, staleTarget.Id);
            staleContact.Location = LocationQuality.Medium;
            HexCoord wrongAim = staleGame.ContactPossibleHexes(staleContact).First(hex => !hex.Equals(staleTarget.Position) && HexCoord.Distance(staleAttacker.Position, hex) <= Rules.StrikeRange(staleAttacker.Kind, Salvo.Light));
            DamageState damageBeforeMiss = staleTarget.Damage;
            Assert(staleGame.StrikeContact(staleAttacker, staleContact, wrongAim, Salvo.Light, Reaction.None, null, out CombatResult missedResult, out string missedMessage), "A stale-location area Strike legally commits: " + missedMessage);
            Assert(missedResult.Damage == DamageState.None && staleTarget.Damage == damageBeforeMiss && missedMessage.Contains("no confirmed effect") && !missedMessage.Contains(staleTarget.Name), "A wrong aim hex consumes the Strike without revealing target existence or identity");

            PrototypeGame unresolvedHitGame = ReactionFixture(out FormationState unresolvedAttacker, out FormationState unresolvedTarget);
            ContactState unresolvedContact = unresolvedHitGame.ContactFor(unresolvedAttacker.Side, unresolvedTarget.Id);
            unresolvedContact.Identity = IdentityQuality.General;
            Assert(unresolvedHitGame.StrikeContact(unresolvedAttacker, unresolvedContact, unresolvedTarget.Position, Salvo.Light, Reaction.Hold, null, out _, out string unresolvedHitMessage), "An uncertain Contact may be hit at its actual hidden hex: " + unresolvedHitMessage);
            Assert(!unresolvedHitMessage.Contains(unresolvedTarget.Name) && unresolvedHitMessage.Contains("Contact area"), "A successful uncertain Strike log does not disclose an unidentified target name");

            PrototypeGame aiReactionGame = ReactionFixture(out FormationState aiAttacker, out FormationState aiTarget);
            aiTarget.OrderlyWithdrawalReady = true;
            Reaction aiReaction = PrototypeAiCommander.ChooseReaction(aiReactionGame, aiAttacker, aiTarget);
            HexCoord? aiEvade = PrototypeAiCommander.ChooseEvadeDestination(aiReactionGame, aiAttacker, aiTarget);
            Assert(aiReaction == Reaction.Evade && aiEvade.HasValue && aiReactionGame.LegalEvadeDestinations(aiAttacker, aiTarget, 2).Contains(aiEvade.Value), "AI selects the prepared legal Evade without consulting hidden enemy state");
            Assert(PrototypeGame.ReactionController(OperationMode.SoloVsAi, Side.Blue, Side.Blue) == ReactionControl.HumanDirect && PrototypeGame.ReactionController(OperationMode.SoloVsAi, Side.Blue, Side.Red) == ReactionControl.Ai && PrototypeGame.ReactionController(OperationMode.LocalHotseat, Side.Blue, Side.Red) == ReactionControl.HumanHandoff, "Solo and Local Hotseat route Reaction control to the correct chooser");

            PrototypeGame blindReactionGame = ReactionFixture(out FormationState blindAttacker, out FormationState blindTarget);
            blindReactionGame.Contacts.RemoveAll(contact => contact.Owner == blindTarget.Side && contact.TargetId == blindAttacker.Id);
            Assert(!blindReactionGame.AvailableReactions(blindAttacker, blindTarget).Contains(Reaction.Counterattack), "A defender cannot infer or Counterattack an attacker for which it has no usable Contact");
            Assert(blindReactionGame.LegalEvadeDestinations(blindAttacker, blindTarget, 1).Count > 0, "A defender without attacker location data can still Evade without receiving a hidden exact-position clue");
            Debug.Log("Player-selected reaction and damage tests passed: timing, refresh, Defend, Evade, Counterattack, Hold, Crippled limits, Light impairment, repeated damage, uncertain-area misses, save/load, and AI choice.");
        }

        private static PrototypeGame ReactionFixture(out FormationState attacker, out FormationState defender)
        {
            var game = new PrototypeGame(1978);
            attacker = game.Active;
            Side attackingSide = attacker.Side;
            defender = game.Formations.First(formation => formation.Side != attackingSide && formation.Kind == FormationKind.Submarine);
            defender.Position = new HexCoord(1, 7);
            defender.Ratings.Defense = 20;
            attacker.Ratings.Defense = 20;
            ContactState attackContact = game.ContactFor(attacker.Side, defender.Id);
            if (attackContact == null)
            {
                attackContact = new ContactState { Owner = attacker.Side, TargetId = defender.Id };
                game.Contacts.Add(attackContact);
            }
            attackContact.LastKnownPosition = defender.Position;
            attackContact.Location = LocationQuality.High;
            attackContact.Identity = IdentityQuality.Identified;
            attackContact.Age = 0;
            ContactState returnContact = game.ContactFor(defender.Side, attacker.Id);
            if (returnContact == null)
            {
                returnContact = new ContactState { Owner = defender.Side, TargetId = attacker.Id };
                game.Contacts.Add(returnContact);
            }
            returnContact.LastKnownPosition = attacker.Position;
            returnContact.Location = LocationQuality.High;
            returnContact.Identity = IdentityQuality.Identified;
            returnContact.Age = 0;
            return game;
        }

        public static void RunCoreSmokeTests()
        {
            RunUnitTestSuite();
            RunIntegrationTestSuite();
            RunPresentationTestSuite();
            Debug.Log("Sea of Uncertainty composed core smoke suites passed.");
        }

        [MenuItem("Sea of Uncertainty/Tests/Run Unit Suite")]
        public static void RunUnitTestSuite()
        {
            EnsureRuntimePanelSettings();
            Ensure3DMaterials();
            ThemeStyleSheet runtimeTheme = Resources.Load<ThemeStyleSheet>("UI/UnityDefaultRuntimeTheme");
            Assert(runtimeTheme != null, "UI Toolkit runtime theme is packaged");
            PanelSettings runtimePanel = AssetDatabase.LoadAssetAtPath<PanelSettings>(RuntimePanelPath);
            Assert(runtimePanel != null && runtimePanel.themeStyleSheet == runtimeTheme, "Runtime panel has its packaged theme assigned");
            Assert(Rules.MoveDistance(MoveMode.Cautious) == 1, "Cautious movement");
            Assert(Rules.MoveDistance(MoveMode.HighTempo) == 3, "High Tempo movement");
            Assert(Rules.SearchTarget(0) == 5 && Rules.SearchTarget(5) == 1, "Search bands");
            Assert(Rules.BandFor(-2) == CombatBand.Poor, "Poor combat boundary");
            Assert(Rules.BandFor(-1) == CombatBand.Even, "Even combat boundary");
            Assert(Rules.BandFor(2) == CombatBand.Favorable, "Favorable combat boundary");
            Assert(Rules.BandFor(4) == CombatBand.Dominant, "Dominant combat boundary");
            Assert(Rules.DamageFor(CombatBand.Dominant, 6) == DamageState.Destroyed, "Dominant maximum result");
            Assert(HexCoord.Distance(new HexCoord(0, 0), new HexCoord(0, 1)) == 1, "Adjacent hex distance");
            Assert(Rules.MoveDistance(FormationKind.AirGroup, MoveMode.Cautious) == 4, "Air mission cautious radius");
            Assert(Rules.MoveDistance(FormationKind.AirGroup, MoveMode.HighTempo) == 8, "Air mission high-tempo radius");
            Assert(Rules.SearchRange(SearchMode.Passive) == 8 && Rules.SearchRange(SearchMode.Focused) == 12, "20 nm Search boundaries");
            Assert(Rules.SearchAreaRadius == 1, "Search covers the selected hex and its six adjacent hexes");
            Assert(Rules.StrikeRange(FormationKind.SurfaceGroup, Salvo.Standard) == 6, "Surface standard Strike boundary");
            Assert(Rules.StrikeRange(FormationKind.AirGroup, Salvo.Heavy) == 14, "Air heavy Strike boundary");
            Assert(Rules.InterceptionRange == 1, "Interception range is one 20 nm hex");
            Assert(EntropyEffectCatalog.All.Count == 36 && EntropyEffectCatalog.For(EntropySource.Friction).Count() == 12 && EntropyEffectCatalog.For(EntropySource.Disruption).Count() == 12 && EntropyEffectCatalog.For(EntropySource.Destruction).Count() == 12, "All 36 prototype Entropy effect cards are structured game data");
            Assert(CommandResponseCatalog.All.Count == 24 && CommandResponseCatalog.All.Select(card => card.Id).Distinct().Count() == 24, "All 24 Command Response cards are structured game data");

            var responseGame = new PrototypeGame(1978);
            Assert(responseGame.CommandResponseDecks.Count == 2 && responseGame.CommandResponseDecks.All(deck => deck.Hand.Count == 3 && deck.DrawPile.Count == 21), "Each side begins with a deterministic three-card Response hand");
            CommandResponseDeckState blueResponses = responseGame.CommandResponseDecks.First(deck => deck.Side == Side.Blue);
            blueResponses.DrawPile.Remove("C-22");
            if (!blueResponses.Hand.Contains("C-22")) blueResponses.Hand.Add("C-22");
            FormationState responseFormation = responseGame.Active.Side == Side.Blue ? responseGame.Active : responseGame.Formations.First(formation => formation.Side == Side.Blue);
            int baseCommand = responseFormation.EffectiveCommand;
            Assert(responseGame.PlayCommandResponse(Side.Blue, "C-22", responseFormation, null, null, out string responseMessage) && responseFormation.EffectiveCommand == baseCommand + 1, "A supported Command Response resolves and discards: " + responseMessage);
            Assert(!blueResponses.Hand.Contains("C-22") && blueResponses.DiscardPile.Contains("C-22"), "Played Command Response leaves the hand for the discard pile");

            var cardGame = new PrototypeGame(1978);
            FormationState cardFormation = cardGame.Active;
            int frictionDrawCount = cardGame.EntropyDecks.First(deck => deck.Source == EntropySource.Friction).DrawPile.Count;
            EntropyEffectDefinition frictionCard = cardGame.MarkEntropy(cardFormation, EntropySource.Friction);
            Assert(frictionCard != null && cardFormation.Friction && cardFormation.ActiveEffectCardIds.Contains(frictionCard.Id), "New Friction draws and attaches one matching effect card");
            Assert(cardGame.PendingEntropyEffectFor(cardFormation.Side)?.Id == frictionCard.Id && cardGame.PendingEntropyFormationFor(cardFormation.Side) == cardFormation, "A card pull queues a reveal for its owning side");
            EntropyEffectDefinition stackedFrictionCard = cardGame.MarkEntropy(cardFormation, EntropySource.Friction);
            Assert(stackedFrictionCard != null && cardFormation.ActiveEffectCardIds.Count == 2, "A repeated Entropy event draws and stacks another physical card");
            Assert(cardGame.EntropyDecks.First(deck => deck.Source == EntropySource.Friction).DrawPile.Count == frictionDrawCount - 2, "Each pull advances the deterministic source deck");
            FormationState opposingFormation = cardGame.Formations.First(formation => formation.Side != cardFormation.Side);
            EntropyEffectDefinition opposingCard = cardGame.MarkEntropy(opposingFormation, EntropySource.Friction);
            Assert(opposingCard != null && cardGame.PendingEntropyEffectFor(opposingFormation.Side)?.Id == opposingCard.Id, "The opposing side receives its own independent reveal");
            cardGame.ConsumePendingEntropyEffect(opposingFormation.Side);
            Assert(cardGame.PendingEntropyEffectFor(cardFormation.Side)?.Id == frictionCard.Id, "Acknowledging an opposing draw cannot overwrite or consume the player's queued reveal");
            string queuedSave = JsonUtility.ToJson(cardGame.CaptureState());
            var restoredCardGame = new PrototypeGame(1978);
            restoredCardGame.RestoreState(JsonUtility.FromJson<PrototypeGame.SaveData>(queuedSave));
            Assert(restoredCardGame.PendingEntropyEffectFor(cardFormation.Side)?.Id == frictionCard.Id, "Pending card reveals survive save and load");
            cardGame.ConsumePendingEntropyEffect(cardFormation.Side);
            Assert(cardGame.Recover(cardFormation, out string cardRecoveryMessage) && cardFormation.Friction && cardFormation.ActiveEffectCardIds.Count == 1, "Recover discards one stacked card and retains the marked source while another remains: " + cardRecoveryMessage);
            var modifierProbe = new FormationState { Kind = FormationKind.CarrierGroup, Ratings = new Ratings { Move = 3, Search = 3, Strike = 3, Defense = 3, Command = 3 }, ActiveEffectCardIds = new List<string> { "X-01", "X-02", "X-03", "X-04", "X-05", "X-06" } };
            Assert(modifierProbe.EffectiveMove == 2 && modifierProbe.EffectiveSearch == 2 && modifierProbe.EffectiveStrike == 2 && modifierProbe.EffectiveDefense == 2 && modifierProbe.EffectiveCommand == 2 && !modifierProbe.CanHeavySalvo, "Supported Destruction cards modify formation capabilities");

            var exactTie = new List<FormationState>
            {
                new FormationState { Id = "B-TEST", Side = Side.Blue, ReadyTime = 0, Ratings = new Ratings { Command = 9 } },
                new FormationState { Id = "R-TEST", Side = Side.Red, ReadyTime = 0, Ratings = new Ratings { Command = 0 } }
            };
            Assert(Rules.NextReady(exactTie).Side == Side.Blue, "Unseeded exact tie uses formation quality and stable ID");
            Assert(Rules.NextReady(exactTie, Side.Blue).Side == Side.Red, "Cross-side exact tie passes priority away from the most recent acting side");
            Debug.Log("Sea of Uncertainty unit suite passed.");
        }

        [MenuItem("Sea of Uncertainty/Tests/Run Integration Suite")]
        public static void RunIntegrationTestSuite()
        {
            var game = new PrototypeGame();
            Assert(game.Formations.Exists(f => f.Side == Side.Blue && f.ReadyTime == 0) && game.Formations.Exists(f => f.Side == Side.Red && f.ReadyTime == 0), "Both sides have formations in the opening Ready-Time cohort");
            Assert(game.Formations.Exists(f => f.Side == Side.Blue && f.ReadyTime == 1) && game.Formations.Exists(f => f.Side == Side.Red && f.ReadyTime == 1), "Both sides have interleaved follow-on readiness");
            var cadenceGame = new PrototypeGame();
            var cadence = new List<Side>();
            for (int i = 0; i < 8; i++)
            {
                cadence.Add(cadenceGame.Active.Side);
                Assert(cadenceGame.Hold(cadenceGame.Active, out string cadenceMessage), "Continuous activation cadence action: " + cadenceMessage);
            }
            for (int i = 1; i < cadence.Count; i++) Assert(cadence[i] != cadence[i - 1], "Opening Ready-Time cadence alternates only where both sides are tied");
            Assert(game.Area.Width == 12 && game.Area.Height == 10, "Data-driven Meridian Veil dimensions");
            Assert(game.Area.NauticalMilesPerHex == 20, "Operational scale is 20 nautical miles per hex");
            Assert(game.Area.NauticalMiles(new HexCoord(0, 0), new HexCoord(0, 3)) == 60, "Hex range converts to nautical miles");
            Assert(game.Area.ReadyTimeHours == 2f, "Ready-Time point converts to two hours");
            Assert(OperationalDataValidator.Validate(game.Scenario).Count == 0, "Operational-area and scenario validation");
            Assert(ScenarioCatalog.All().Count == 2, "Scenario catalog exposes both MVP theaters");
            ScenarioDefinition luzon = ScenarioCatalog.LuzonStrait();
            ScenarioDefinition migrationSample = JsonUtility.FromJson<ScenarioDefinition>(JsonUtility.ToJson(luzon));
            migrationSample.SchemaVersion = 1; migrationSample.Area.SchemaVersion = 1; migrationSample.Area.RestrictedAreas = null;
            OperationalDataMigration.Migrate(migrationSample);
            Assert(migrationSample.SchemaVersion == OperationalDataMigration.CurrentSchemaVersion && migrationSample.Area.SchemaVersion == OperationalDataMigration.CurrentSchemaVersion && migrationSample.Area.RestrictedAreas != null, "Operational-area and scenario v1 data migrate to the current schema");
            Assert(luzon.Area.Width == 24 && luzon.Area.Height == 20 && luzon.Area.NauticalMilesPerHex == 20, "Luzon pilot dimensions and scale");
            Assert(luzon.Horizon == 24 && Mathf.Approximately(luzon.Horizon * luzon.Area.ReadyTimeHours, 48f), "Luzon scenario is a 48-hour operation");
            Assert(OperationalDataValidator.Validate(luzon).Count == 0, "Luzon operational-area and scenario validation");
            ScenarioDefinition invalidData = JsonUtility.FromJson<ScenarioDefinition>(JsonUtility.ToJson(luzon));
            invalidData.Area.Presentation.TerrainAssetSet = string.Empty;
            invalidData.Area.Locations.Add(invalidData.Area.Locations[0]);
            invalidData.DeploymentRegions.Add(new OperationalRegionDefinition { Id = "unreachable", Name = "Unreachable", Hexes = new System.Collections.Generic.List<HexCoord> { new HexCoord(0, 0) } });
            string validationReport = string.Join(" | ", OperationalDataValidator.Validate(invalidData));
            Assert(validationReport.Contains("Presentation assets") && validationReport.Contains("duplicated") && validationReport.Contains("cannot reach"), "Validator reports missing assets, duplicate IDs, and unreachable setup zones");
            OperationalLocationDefinition basco = luzon.Area.Locations.Find(location => location.Id == "basco");
            OperationalLocationDefinition aparri = luzon.Area.Locations.Find(location => location.Id == "aparri");
            double geographicNm = Geodesy.NauticalMiles(basco.Geographic, aparri.Geographic);
            int logicalNm = luzon.Area.NauticalMiles(basco.Hex, aparri.Hex);
            Assert(System.Math.Abs(geographicNm - logicalNm) / geographicNm < .2, "Luzon geographic and hex distances agree within 20 percent");
            OperationalLocationDefinition bashi = luzon.Area.Locations.Find(location => location.Id == "bashi-channel");
            OperationalLocationDefinition balintang = luzon.Area.Locations.Find(location => location.Id == "balintang-channel");
            Assert(System.Math.Abs(Geodesy.NauticalMiles(bashi.Geographic, balintang.Geographic) - luzon.Area.NauticalMiles(bashi.Hex, balintang.Hex)) / Geodesy.NauticalMiles(bashi.Geographic, balintang.Geographic) < .2, "North-south channel distance agrees within 20 percent");
            Assert(System.Math.Abs(Geodesy.NauticalMiles(bashi.Geographic, basco.Geographic) - luzon.Area.NauticalMiles(bashi.Hex, basco.Hex)) / Geodesy.NauticalMiles(bashi.Geographic, basco.Geographic) < .2, "Bashi-to-Basco distance agrees within 20 percent");
            using (var luzonMap = new OperationalMap3D(luzon.Area, 480, 270))
            {
                Assert(luzonMap.CoastlinePolygonCount >= 50, "Luzon renderer uses the local Natural Earth polygon coastline library");
                Assert(luzonMap.CoastlineSurfaceVertexCount >= 1000, "Curved land surfaces are tessellated densely enough to follow the globe without ocean cut-through");
                Assert(luzonMap.CoastlineMaximumTriangleEdge <= 1.26f, "Curved land triangles remain below the globe-safe maximum span");
                Assert(luzonMap.OffMapCoastlineVertexCount >= 100, "Authentic Natural Earth land geometry continues beyond the playable projection to the table boundary");
                Assert(luzonMap.HasDirectionalSun, "3D theater has a directional maritime sun");
                Assert(Mathf.Approximately(luzonMap.SunSourceAzimuthDegrees, 67.5f), "Maritime sun illuminates the theater from east-northeast");
                Assert(luzonMap.UsesProceduralSurfaceTextures, "Land, littoral, and ocean use procedural surface textures");
                Assert(luzonMap.WaterSurfaceVertexCount >= 4000, "Ocean surface has enough geometry for restrained wave relief");
                Assert(luzonMap.UsesGeographicElevation, "Geographic theaters render measured elevation instead of procedural landmass ridges");
                Assert(luzonMap.ElevationSampleCount == 72800 && luzonMap.TerrainMeshTileCount >= 4, "Luzon terrain uses the complete tiled two-arc-minute ETOPO subset");
                Assert(luzonMap.MaximumTerrainElevationMetres >= 2500, "Measured terrain preserves the theater's major mountain elevations");
                Assert(Mathf.Approximately(luzonMap.TerrainVerticalExaggeration, 4f), "Operational-scale terrain uses the configured restrained vertical exaggeration");
                Assert(luzonMap.HasShallowWaterDetail && luzonMap.HasCoastalFoam && luzonMap.HasCoastlineDrivenShelf, "Coastline-driven shelves, bathymetric contours, and coastal foam enrich the sea-land transition");
                Assert(luzonMap.UsesGeographicBathymetry, "Geographic ocean coloration is driven by measured ETOPO seafloor depth");
                Assert(luzonMap.HasOceanCurrentBands && luzonMap.OceanColorLuminanceRange > .08f, "Ocean texture combines readable broad color variation with restrained current bands");
                Assert(luzonMap.HasAtmosphericHaze && luzonMap.CloudShadowCount >= 2 && luzonMap.WeatherPreset == "Haze", "Data-driven haze and moving cloud-shadow layers establish maritime atmosphere");
                Assert(luzonMap.GeographicLabelCount == luzon.Area.Locations.Count, "Ports, airfields, straits, and objectives receive map-space geographic labels");
                float renderLuminance = luzonMap.ProbeRenderLuminance();
                Assert(renderLuminance > .01f && renderLuminance < .95f, "The integrated ocean, terrain, atmosphere, and label camera produces a valid non-black render");
                Vector3 edge = luzonMap.HexToWorld(new HexCoord(23, 19));
                Assert(edge.x > 0f && edge.z > 0f, "Floating-origin world coordinates center the theater");
                Assert(luzonMap.UsesEarthCurvature && luzonMap.EarthCurvatureRadiusWorld > 250f && luzonMap.EarthCurvatureRadiusWorld < 350f, "Operational theater uses the physical Earth radius at the scenario's 20-NM hex scale");
                Assert(luzonMap.TheaterEdgeDrop > .35f, "Large operational areas visibly fall away from the local tangent plane toward the horizon");
                Assert(luzonMap.UsesTraditionalFlatTopHexes, "Operational grid uses traditional flat-top hex geometry aligned with its column spacing");
                Assert(luzonMap.TryWorldToHex(luzonMap.HexToWorld(new HexCoord(11, 9)), out HexCoord roundTrip) && roundTrip.Equals(new HexCoord(11, 9)), "Hex/world conversion round trip");
                Assert(luzonMap.TryWorldToHex(luzonMap.HexToWorld(new HexCoord(23, 19)), out HexCoord edgeRoundTrip) && edgeRoundTrip.Equals(new HexCoord(23, 19)), "Edge hex selection round trip");
                var pickRect = new Rect(0f, 0f, 480f, 270f);
                Vector2 objectiveScreen = luzonMap.Project(luzon.Area.Objective, pickRect);
                Assert(luzonMap.TryPickHex(objectiveScreen, pickRect, out HexCoord curvedPick) && curvedPick.Equals(luzon.Area.Objective), "Screen picking intersects the curved globe surface at the projected objective");
                luzonMap.Pan(new Vector2(100000f, -100000f));
                Assert(luzonMap.FocusWithinBounds, "Camera pan remains inside theater bounds");
            }
            using (var operationalMap = new OperationalMap3D(game.Area, 320, 180))
            {
                Vector3 adjacentA = operationalMap.HexToWorld(new HexCoord(0, 0));
                Vector3 adjacentB = operationalMap.HexToWorld(new HexCoord(0, 1));
                float adjacentWorldDistance = Vector2.Distance(new Vector2(adjacentA.x, adjacentA.z), new Vector2(adjacentB.x, adjacentB.z));
                Assert(Mathf.Approximately(adjacentWorldDistance, Mathf.Sqrt(3f)), "3D world coordinates preserve adjacent hex spacing");
                operationalMap.SetState(game, ToolkitActionMode.Move, MoveMode.Normal, SearchMode.Passive, Salvo.Standard);
                operationalMap.SetHoverHex(new HexCoord(2, 3));
                operationalMap.SetState(game, ToolkitActionMode.Search, MoveMode.Normal, SearchMode.Active, Salvo.Standard);
                int baselineContactCount = game.Contacts.Count;
                string enemyId = game.Formations.Find(formation => formation.Side != game.Active.Side).Id;
                game.Contacts.Add(new ContactState { Owner = game.Active.Side, TargetId = enemyId, LastKnownPosition = new HexCoord(4, 3), Location = LocationQuality.High, Identity = IdentityQuality.Identified, Age = 0 });
                game.Contacts.Add(new ContactState { Owner = game.Active.Side, TargetId = enemyId, LastKnownPosition = new HexCoord(5, 3), Location = LocationQuality.Low, Identity = IdentityQuality.Unknown, Age = 2 });
                game.Contacts.Add(new ContactState { Owner = game.Active.Side, TargetId = enemyId, LastKnownPosition = new HexCoord(6, 3), Location = LocationQuality.Medium, Identity = IdentityQuality.General, Age = 3 });
                game.Contacts.Add(new ContactState { Owner = game.Active.Side, TargetId = enemyId, LastKnownPosition = new HexCoord(7, 3), Location = LocationQuality.Low, Identity = IdentityQuality.Unknown, Age = 3, IsFalse = true });
                game.Contacts.Add(new ContactState { Owner = game.Active.Side, TargetId = enemyId, LastKnownPosition = new HexCoord(8, 3), Location = LocationQuality.Low, Identity = IdentityQuality.Unknown, Age = 4, IsLost = true });
                operationalMap.SetState(game, ToolkitActionMode.Strike, MoveMode.Normal, SearchMode.Active, Salvo.Standard);
                Assert(operationalMap.HasActiveFormationPulse, "The active formation has a dedicated pulse ring");
                Assert(operationalMap.VisibleFormationCount == game.Formations.FindAll(formation => formation.Side == game.Active.Side && !formation.IsDestroyed).Count, "3D view instantiates only the active side's friendly formations");
                Assert(operationalMap.FormationMeshVariantCount >= 2 && operationalMap.ContainsRenderedName("Tapered Hull"), "Close formation models use reusable tapered naval meshes instead of stretched-cube hull blockouts");
                Assert(operationalMap.ContainsRenderedName("Swept Wing") && operationalMap.ContainsRenderedName("Hydrodynamic Pressure Hull"), "Air-group and submarine silhouettes remain recognizable by geometry");
                Assert(operationalMap.ContainsRenderedName("Production Formation Model") && operationalMap.ContainsRenderedName("Side Recognition Marking"), "Production formation set includes explicit recognition markings");
                Assert(operationalMap.ContainsRenderedName("CarrierGroup Distant Operational Symbol") && operationalMap.ContainsRenderedName("Submarine Distant Operational Symbol"), "Formation kinds retain distinct distant-symbol geometry");
                int expectedWakes = game.Formations.Count(formation => formation.Side == game.Active.Side && !formation.IsDestroyed && formation.Kind != FormationKind.Submarine) * 2;
                Assert(operationalMap.PersistentWakeCount == expectedWakes, "Visible surface and air formations receive paired persistent wakes or contrails while submarines do not");
                int expectedContrails = game.Formations.Count(formation => formation.Side == game.Active.Side && !formation.IsDestroyed && formation.Kind == FormationKind.AirGroup) * 2;
                Assert(operationalMap.AircraftContrailCount == expectedContrails && operationalMap.PersistentTrailsUseFormationSpace, "Aircraft use paired contrails and every persistent trail rotates in Formation-local space");
                Assert(operationalMap.VisibleContactCount == game.Contacts.FindAll(contact => contact.Owner == game.Active.Side && !contact.IsLost).Count, "3D view instantiates only the active side's Contacts");
                FormationState hiddenEnemy = game.Formations.Find(formation => formation.Side != game.Active.Side);
                Assert(!operationalMap.ContainsRenderedName(hiddenEnemy.Name) && !operationalMap.ContainsRenderedName(hiddenEnemy.Id), "3D scene hierarchy does not expose a hidden enemy identity");
                int secureFormationCount = operationalMap.VisibleFormationCount, secureContactCount = operationalMap.VisibleContactCount;
                operationalMap.Zoom(-1f); operationalMap.Zoom(-1f); operationalMap.Zoom(1f);
                Assert(operationalMap.VisibleFormationCount == secureFormationCount && operationalMap.VisibleContactCount == secureContactCount && !operationalMap.ContainsRenderedName(hiddenEnemy.Name), "Zoom level cannot change or reveal authoritative information");
                foreach (OperationalEffectKind kind in System.Enum.GetValues(typeof(OperationalEffectKind))) operationalMap.TriggerEffect(kind, game.Active.Position);
                Assert(operationalMap.ActiveEffectCount >= 7, "Restrained prototype effect library covers all MVP effect categories");
                operationalMap.SetState(game, ToolkitActionMode.None, MoveMode.Normal, SearchMode.Passive, Salvo.Standard, true);
                Assert(operationalMap.ActiveEffectCount == 0 && operationalMap.PooledEffectCount >= 7, "Transient effects return to the object pool");
                Assert(!operationalMap.PermanentGridVisible, "Permanent hex grid is optional");
                operationalMap.SetState(game, ToolkitActionMode.None, MoveMode.Normal, SearchMode.Passive, Salvo.Standard, false, true);
                Assert(operationalMap.PermanentGridVisible, "Optional permanent hex grid can be enabled");
                operationalMap.SetState(game, ToolkitActionMode.None, MoveMode.Normal, SearchMode.Passive, Salvo.Standard, false, false);
                Assert(!operationalMap.PermanentGridVisible, "Optional permanent hex grid can be disabled");
                game.Contacts.Add(new ContactState { Owner = game.Active.Side, TargetId = "FALSE-TEST", LastKnownPosition = new HexCoord(4, 4), Location = LocationQuality.Low, Identity = IdentityQuality.Unknown, Age = 3, IsFalse = true });
                operationalMap.SetState(game, ToolkitActionMode.Search, MoveMode.Normal, SearchMode.Focused, Salvo.Standard);
                Assert(!operationalMap.ContainsRenderedName("False Contact"), "Undisproved False Contacts use the same map presentation as real Contacts");
                game.Contacts.RemoveAll(contact => contact.TargetId == "FALSE-TEST");
                if (game.Contacts.Count > baselineContactCount) game.Contacts.RemoveRange(baselineContactCount, game.Contacts.Count - baselineContactCount);
                operationalMap.SetState(game, ToolkitActionMode.None, MoveMode.Normal, SearchMode.Passive, Salvo.Standard);
                Assert(operationalMap.PooledMarkerCount >= 1, "Formation and Contact marker roots return to the object pool");
                operationalMap.Rotate(1f);
                Assert(Mathf.Approximately(operationalMap.Heading, 30f), "3D command camera uses stepped rotation");
                float orbitPitch = operationalMap.CameraPitch;
                operationalMap.Orbit(new Vector2(45f, 30f));
                Assert(!Mathf.Approximately(operationalMap.Heading, 30f) && operationalMap.CameraPitch < orbitPitch, "Right-drag camera supports continuous orbit and tilt");
                float flyDistance = operationalMap.CameraDistance;
                operationalMap.FlyCamera(1f, 1f, 1f, 1f, -1f, .25f, true);
                Assert(operationalMap.FocusWithinBounds && operationalMap.CameraDistance < flyDistance, "Keyboard camera movement, rotation, tilt, and zoom remain bounded");
                operationalMap.SaveCameraView();
                float bookmarkedHeading = operationalMap.Heading;
                operationalMap.FlyCamera(0f, 0f, 1f, 0f, 0f, .4f);
                Assert(!Mathf.Approximately(operationalMap.Heading, bookmarkedHeading) && operationalMap.RecallCameraView() && Mathf.Approximately(operationalMap.Heading, bookmarkedHeading), "A command-camera view can be saved and recalled");
                operationalMap.FocusHex(game.Area.Objective);
                Assert(operationalMap.FocusWithinBounds, "Active/objective focus commands remain inside theater bounds");
                operationalMap.Pan(new Vector2(20f, -15f));
                operationalMap.Zoom(1f);
                operationalMap.ResetCamera();
                Assert(Mathf.Approximately(operationalMap.Heading, 0f), "3D command camera reset");
            }
            var cameraInputProbe = new TacticalMapElement();
            Assert(cameraInputProbe.SetCameraKey(KeyCode.W, true) && cameraInputProbe.CameraInputActive, "WASD camera input can be routed from the operation screen without map focus");
            cameraInputProbe.SetCameraKey(KeyCode.W, false);
            Assert(!cameraInputProbe.CameraInputActive, "Released global camera keys do not leave movement stuck active");
            cameraInputProbe.SetEdgeScroll(true);
            Assert(cameraInputProbe.EdgeScrollEnabled, "Optional edge scrolling can be enabled independently of WASD movement");
            FormationState actor = game.Active;
            int originalTime = game.Time;
            var destination = new HexCoord(actor.Position.Q, actor.Position.R > 0 ? actor.Position.R - 1 : actor.Position.R + 1);
            Assert(game.Move(actor, destination, MoveMode.Cautious, out string message), "Legal move: " + message);
            Assert(actor.Position.Equals(destination), "Move updates position");
            Assert(actor.ReadyTime > originalTime, "Move schedules readiness");

            var emptySearchGame = new PrototypeGame();
            emptySearchGame.Contacts.Clear();
            FormationState emptySearcher = emptySearchGame.Active;
            int emptySearchReadyBefore = emptySearcher.ReadyTime;
            Assert(emptySearchGame.SearchArea(emptySearcher, emptySearcher.Position, SearchMode.Passive, out string emptySearchMessage), "An empty area is a legal Search: " + emptySearchMessage);
            Assert(emptySearchMessage.Contains("no detections"), "Empty Search reports no detections without exposing hidden formations");
            Assert(!emptySearchMessage.Contains("Tempest") && !emptySearchMessage.Contains("Ember") && !emptySearchMessage.Contains("Hunter"), "Failed Search does not leak enemy identities");
            Side emptyEnemySide = emptySearcher.Side == Side.Blue ? Side.Red : Side.Blue;
            Assert(emptySearchGame.VisibleLog(emptySearcher.Side).Any(entry => entry.Contains("searched area")) && !emptySearchGame.VisibleLog(emptyEnemySide).Any(entry => entry.Contains(emptySearcher.Name) || entry.Contains("searched area")), "Search outcomes and exact areas remain private to the searching side");
            Assert(emptySearcher.ReadyTime > emptySearchReadyBefore && emptySearchGame.Active != emptySearcher, "Empty Search consumes time and advances to the next Ready formation");

            var discoveryGame = new PrototypeGame();
            discoveryGame.Contacts.Clear();
            FormationState discoverySearcher = discoveryGame.Active;
            FormationState discoveryTarget = discoveryGame.Formations.Find(formation => formation.Side != discoverySearcher.Side);
            discoveryTarget.Position = new HexCoord(discoverySearcher.Position.Q + 1, discoverySearcher.Position.R);
            discoveryTarget.Ratings.Signature = 20;
            Assert(discoveryGame.SearchArea(discoverySearcher, discoverySearcher.Position, SearchMode.Passive, out string discoveryMessage), "Area Search resolves against a hidden formation: " + discoveryMessage);
            Assert(discoveryGame.ContactFor(discoverySearcher.Side, discoveryTarget.Id) != null, "Successful area Search creates a Contact for a target in an adjacent footprint hex");

            var rejectedSearchGame = new PrototypeGame();
            FormationState rejectedSearcher = rejectedSearchGame.Active;
            int rejectedReadyBefore = rejectedSearcher.ReadyTime;
            int focusedCommandBefore = rejectedSearchGame.Sides[rejectedSearcher.Side].CommandSlots;
            Assert(!rejectedSearchGame.SearchArea(rejectedSearcher, new HexCoord(rejectedSearchGame.Area.Width - 1, rejectedSearchGame.Area.Height - 1), SearchMode.Passive, out string rejectedSearchMessage), "Out-of-range area Search is rejected");
            Assert(rejectedSearcher.ReadyTime == rejectedReadyBefore && rejectedSearchGame.Sides[rejectedSearcher.Side].CommandSlots == focusedCommandBefore, "Rejected Search consumes neither time nor Command");

            var focusedSearchGame = new PrototypeGame();
            FormationState focusedSearcher = focusedSearchGame.Active;
            int commandBefore = focusedSearchGame.Sides[focusedSearcher.Side].CommandSlots;
            Assert(focusedSearchGame.SearchArea(focusedSearcher, focusedSearcher.Position, SearchMode.Focused, out string focusedMessage), "Focused area Search resolves: " + focusedMessage);
            Assert(focusedSearchGame.Sides[focusedSearcher.Side].CommandSlots == commandBefore, "Focused Search releases its temporary Command Slot after resolution");

            var prioritySearchGame = new PrototypeGame();
            prioritySearchGame.Contacts.Clear();
            FormationState prioritySearcher = prioritySearchGame.Active;
            FormationState priorityTarget = prioritySearchGame.Formations.First(formation => formation.Side != prioritySearcher.Side);
            priorityTarget.Position = new HexCoord(prioritySearcher.Position.Q + 1, prioritySearcher.Position.R);
            priorityTarget.Ratings.Signature = 20;
            var priorityContact = new ContactState { Owner = prioritySearcher.Side, TargetId = priorityTarget.Id, LastKnownPosition = priorityTarget.Position, Location = LocationQuality.Low, Identity = IdentityQuality.Unknown };
            prioritySearchGame.Contacts.Add(priorityContact);
            Assert(prioritySearchGame.SearchArea(prioritySearcher, priorityTarget.Position, SearchMode.Passive, SearchPriority.Identity, out string priorityMessage), "Search resolves with a declared improvement priority: " + priorityMessage);
            Assert(priorityContact.Location == LocationQuality.Low && priorityContact.Identity == IdentityQuality.General, "A successful Search improves the chosen axis by exactly one step even with excess success");

            var falseSearchGame = new PrototypeGame();
            falseSearchGame.Contacts.Clear();
            FormationState falseSearcher = falseSearchGame.Active;
            falseSearcher.Ratings.Search = 20;
            var falseContact = new ContactState { Owner = falseSearcher.Side, TargetId = "FALSE-TEST", LastKnownPosition = falseSearcher.Position, Location = LocationQuality.Low, Identity = IdentityQuality.Unknown, IsFalse = true };
            falseSearchGame.Contacts.Add(falseContact);
            Assert(falseSearchGame.SearchArea(falseSearcher, falseSearcher.Position, SearchMode.Passive, SearchPriority.Location, out string falseSearchMessage) && falseContact.IsLost && falseSearchMessage.Contains("disproved"), "A successful Search of a False Contact's area disproves it");

            var geometryGame = new PrototypeGame();
            var highContact = new ContactState { Owner = Side.Blue, TargetId = "GEOMETRY", LastKnownPosition = geometryGame.Area.Objective, Location = LocationQuality.High, Identity = IdentityQuality.Unknown };
            Assert(Rules.ContactUncertaintyRadius(highContact) == 0 && geometryGame.ContactPossibleHexes(highContact).Count == 1, "High Location at Age 0 is one exact hex");
            highContact.Location = LocationQuality.Medium;
            Assert(Rules.ContactUncertaintyRadius(highContact) == 1 && geometryGame.ContactPossibleHexes(highContact).Count == 7, "Medium Location at Age 0 is a radius-one seven-hex area");
            highContact.Location = LocationQuality.Low;
            Assert(Rules.ContactUncertaintyRadius(highContact) == 2 && geometryGame.ContactPossibleHexes(highContact).Count == 19, "Low Location at Age 0 is a radius-two nineteen-hex area");
            highContact.Location = LocationQuality.High;
            highContact.Age = 1;
            Assert(Rules.ContactUncertaintyRadius(highContact) == 1, "Elapsed Contact Age expands uncertainty by one ring");
            highContact.Age = 0;
            highContact.MovementUncertainty = 1;
            Assert(Rules.ContactUncertaintyRadius(highContact) == 1, "Observed target movement expands uncertainty without exposing direction or distance");

            var movementContactGame = new PrototypeGame();
            FormationState trackedMover = movementContactGame.Active;
            var trackingContact = new ContactState { Owner = trackedMover.Side == Side.Blue ? Side.Red : Side.Blue, TargetId = trackedMover.Id, LastKnownPosition = trackedMover.Position, Location = LocationQuality.High };
            movementContactGame.Contacts.Add(trackingContact);
            Assert(movementContactGame.Move(trackedMover, movementContactGame.LegalMoveDestinations(trackedMover, MoveMode.Cautious).First(), MoveMode.Cautious, out _) && trackingContact.MovementUncertainty == 1, "A tracked Formation's Move expands the opposing Contact by one anonymous ring");

            var agingGame = new PrototypeGame();
            agingGame.Contacts.Clear();
            FormationState agingActor = agingGame.Active;
            foreach (FormationState formation in agingGame.Formations) formation.ReadyTime = formation == agingActor ? 0 : 10;
            var agingContact = new ContactState { Owner = agingActor.Side, TargetId = "AGING", LastKnownPosition = agingGame.Area.Objective, Location = LocationQuality.Medium, Identity = IdentityQuality.Unknown, Age = 2 };
            agingGame.Contacts.Add(agingContact);
            Assert(agingGame.Hold(agingActor, out _) && agingContact.Age == 3 && agingContact.Location == LocationQuality.Low, "Contact Age advances only with operational Time and degrades once on reaching Age 3");
            Assert(agingGame.Hold(agingActor, out _) && agingContact.Age == 4 && agingContact.IsLost, "Each further elapsed Time degrades again and Low Location becomes Lost");

            FormationState surfaceSensor = prioritySearchGame.Formations.First(formation => formation.Side == prioritySearcher.Side && formation.Kind == FormationKind.SurfaceGroup);
            Assert(prioritySearchGame.SearchRangeFor(surfaceSensor, SearchMode.Passive) == 7 && prioritySearchGame.SearchRangeFor(surfaceSensor, SearchMode.Focused) == 11, "Surface sensor profile supplies scenario-specific Search ranges");
            FormationState airSensor = prioritySearchGame.Formations.First(formation => formation.Side == prioritySearcher.Side && formation.Kind == FormationKind.AirGroup);
            Assert(prioritySearchGame.SearchRangeFor(airSensor, SearchMode.Focused) == 14, "Air sensor profile has a distinct scenario-specific maximum range");

            string saveJson = JsonUtility.ToJson(game.CaptureState());
            var saveData = JsonUtility.FromJson<PrototypeGame.SaveData>(saveJson);
            var restored = new PrototypeGame();
            restored.RestoreState(saveData);
            Assert(restored.Time == game.Time, "Save restores operational Time");
            Assert(restored.Active != null && restored.Active.Id == game.Active.Id, "Save restores active formation");
            Assert(restored.Formations.Count == game.Formations.Count, "Save restores formations");
            Assert(restored.Contacts.Count == game.Contacts.Count, "Save restores Contacts");
            Assert(saveData.Version == 11 && saveData.ScenarioId == "meridian-veil" && saveData.OperationalAreaId == "meridian-veil-archipelago", "Version 11 save preserves stable IDs, Entropy decks, response windows, Response hands, synchronized strikes and scenario events, Command architectures, magazines, Standing Missions, weather, uncertainty, damage duration, and private logs");
            Assert(restored.LogEntries.Count == game.LogEntries.Count, "Save restores side-scoped operational log visibility");
            Assert(restored.EntropyDecks.Count == 3 && restored.EntropyDecks.Sum(deck => deck.DrawPile.Count + deck.DiscardPile.Count) == game.EntropyDecks.Sum(deck => deck.DrawPile.Count + deck.DiscardPile.Count), "Save restores Entropy deck state");
            Assert(restored.CommandResponseDecks.Count == 2 && restored.CommandResponseDecks.All(deck => deck.Hand.Count == 3), "Save restores both private Command Response hands");
            Assert(saveData.HasLastActingSide && restored.LastActingSide == saveData.LastActingSide, "Save restores continuous-activation tie priority");
            saveData.Version = 1; saveData.ScenarioId = null; saveData.OperationalAreaId = null;
            new PrototypeGame().RestoreState(saveData);
            bool mismatchRejected = false;
            try { new PrototypeGame(1978, luzon).RestoreState(JsonUtility.FromJson<PrototypeGame.SaveData>(saveJson)); }
            catch (System.ArgumentException exception) { mismatchRejected = exception.Message.Contains("does not match"); }
            Assert(mismatchRejected, "Incompatible scenario saves fail with a clear message");

            var completeScenario = new PrototypeGame();
            int completionGuard = 0;
            while (completeScenario.Time < completeScenario.Scenario.Horizon && completeScenario.Active != null && completionGuard++ < 200)
                Assert(completeScenario.Hold(completeScenario.Active, out string holdMessage), "Grid-free scenario simulation action: " + holdMessage);
            Assert(completeScenario.Time >= completeScenario.Scenario.Horizon, "A complete scenario can resolve without a permanent grid");

            var aiGame = new PrototypeGame(1978, luzon);
            int aiGuard = 0;
            while (aiGame.Time < aiGame.Scenario.Horizon && aiGame.Active != null && aiGuard++ < 300)
            {
                FormationState aiActor = aiGame.Active;
                AiDecision decision = PrototypeAiCommander.Choose(aiGame);
                Assert(decision != null, "AI produces a decision for every Ready formation");
                Assert(PrototypeAiCommander.Execute(aiGame, decision, out string aiMessage), $"AI decision is legal ({decision?.Action}): {aiMessage}");
                Assert(aiActor.ReadyTime > aiGame.Time || aiGame.Active != aiActor, "AI action schedules or advances the Ready formation");
            }
            Assert(aiGame.Time >= aiGame.Scenario.Horizon, "AI commander can complete a full 20 nm scenario without stalling");

            var extendedStrikeGame = new PrototypeGame();
            FormationState extendedStrikeActor = extendedStrikeGame.Active;
            FormationState extendedStrikeTarget = extendedStrikeGame.Formations.Find(formation => formation.Side != extendedStrikeActor.Side);
            extendedStrikeGame.Contacts.Clear();
            HexCoord extendedFix = extendedStrikeActor.Position;
            bool foundExtendedFix = false;
            for (int q = 0; q < extendedStrikeGame.Area.Width && !foundExtendedFix; q++) for (int r = 0; r < extendedStrikeGame.Area.Height && !foundExtendedFix; r++)
            {
                var candidate = new HexCoord(q, r);
                int candidateRange = HexCoord.Distance(extendedStrikeActor.Position, candidate);
                if (candidateRange > Rules.StrikeRange(extendedStrikeActor.Kind, Salvo.Standard) && candidateRange <= Rules.StrikeRange(extendedStrikeActor.Kind, Salvo.Heavy)) { extendedFix = candidate; foundExtendedFix = true; }
            }
            extendedStrikeGame.Contacts.Add(new ContactState { Owner = extendedStrikeActor.Side, TargetId = extendedStrikeTarget.Id, LastKnownPosition = extendedFix, Location = LocationQuality.High, Identity = IdentityQuality.Identified, Age = 0 });
            AiDecision extendedStrike = PrototypeAiCommander.Choose(extendedStrikeGame);
            int extendedRange = HexCoord.Distance(extendedStrikeActor.Position, extendedFix);
            Assert(foundExtendedFix && extendedRange > Rules.StrikeRange(extendedStrikeActor.Kind, Salvo.Standard) && extendedRange <= Rules.StrikeRange(extendedStrikeActor.Kind, Salvo.Heavy) && extendedStrike.Action == ActionKind.Strike && extendedStrike.Salvo == Salvo.Heavy, "AI recognizes a legitimate high-quality Contact in extended Heavy-only range");

            var terrainGame = new PrototypeGame(1978, luzon);
            FormationState surface = terrainGame.Active;
            surface.Position = new HexCoord(0, 5);
            Assert(!terrainGame.Move(surface, new HexCoord(0, 3), MoveMode.HighTempo, out string landMessage) && landMessage.Contains("Land"), "Non-air movement cannot end on land");

            var routeGame = new PrototypeGame();
            FormationState routeMover = routeGame.Active;
            routeMover.Position = new HexCoord(3, 3);
            routeGame.Area.RestrictedAreas.Add(new OperationalRegionDefinition
            {
                Id = "route-test-barrier",
                Name = "Route Test Barrier",
                Hexes = Enumerable.Range(0, routeGame.Area.Height).Select(r => new HexCoord(4, r)).ToList()
            });
            Assert(!routeGame.Move(routeMover, new HexCoord(5, 3), MoveMode.HighTempo, out string routeMessage) && routeMessage.Contains("No legal route"), "Movement validates the complete route rather than only its destination");

            var straitGame = new PrototypeGame();
            FormationState straitMover = straitGame.Active;
            straitMover.Position = new HexCoord(3, 3);
            HexCoord straitDestination = new HexCoord(5, 3);
            List<HexCoord> straitGate = Enumerable.Range(0, straitGame.Area.Width)
                .SelectMany(q => Enumerable.Range(0, straitGame.Area.Height).Select(r => new HexCoord(q, r)))
                .Where(hex => HexCoord.Distance(straitMover.Position, hex) == 1)
                .ToList();
            foreach (HexCoord hex in straitGate)
            {
                straitGame.Area.Terrain.RemoveAll(item => item.Hex.Equals(hex));
                straitGame.Area.Terrain.Add(new TerrainHexDefinition { Q = hex.Q, R = hex.R, Terrain = OperationalTerrain.Strait, Name = "Test Strait" });
            }
            Assert(straitGate.Count > 0 && !straitGame.Move(straitMover, straitDestination, MoveMode.HighTempo, out string straitMessage) && straitMessage.Contains("intervening Straits"), "A naval Formation entering a Strait must end its Move there");

            var occupancyGame = new PrototypeGame();
            FormationState occupancyMover = occupancyGame.Active;
            HexCoord occupiedHex = Enumerable.Range(0, occupancyGame.Area.Width)
                .SelectMany(q => Enumerable.Range(0, occupancyGame.Area.Height).Select(r => new HexCoord(q, r)))
                .First(hex => occupancyGame.Area.Contains(hex) && occupancyGame.Area.TerrainAt(hex) != OperationalTerrain.Land && HexCoord.Distance(occupancyMover.Position, hex) == 1);
            FormationState friendlyOccupant = occupancyGame.Formations.First(candidate => candidate.Side == occupancyMover.Side && candidate != occupancyMover);
            friendlyOccupant.Position = occupiedHex;
            Assert(!occupancyGame.Move(occupancyMover, occupiedHex, MoveMode.Cautious, out string stackingMessage) && stackingMessage.Contains("one friendly Formation"), "Friendly Formations may pass through but may not end stacked in one hex");
            friendlyOccupant.Position = new HexCoord(0, 9);
            FormationState hiddenEnemyOccupant = occupancyGame.Formations.First(candidate => candidate.Side != occupancyMover.Side);
            hiddenEnemyOccupant.Position = occupiedHex;
            int baseSignature = occupancyMover.EffectiveSignature;
            Assert(occupancyGame.Move(occupancyMover, occupiedHex, MoveMode.Cautious, out string coexistMessage) && occupancyMover.Position.Equals(occupiedHex), "Opposing Formations may coexist in a 20 nm hex without hidden occupancy blocking movement: " + coexistMessage);
            Assert(occupancyMover.EffectiveSignature == baseSignature - 1 && occupancyMover.MovementSignatureModifier == -1, "Cautious Signature -1 persists after movement");
            int signatureGuard = 0;
            while (occupancyGame.Active != occupancyMover && signatureGuard++ < 20) Assert(occupancyGame.Hold(occupancyGame.Active, out _), "Advance to the cautious mover's next Action");
            Assert(occupancyGame.Active == occupancyMover && occupancyGame.Hold(occupancyMover, out _) && occupancyMover.MovementSignatureModifier == 0, "Cautious Signature expires when the Formation completes its next Action");

            var loudGame = new PrototypeGame();
            FormationState loudMover = loudGame.Active;
            HexCoord loudDestination = loudGame.LegalMoveDestinations(loudMover, MoveMode.HighTempo).First();
            Assert(loudGame.Move(loudMover, loudDestination, MoveMode.HighTempo, out _) && loudMover.Loud, "High Tempo remains Loud after movement");
            int loudGuard = 0;
            while (loudGame.Active != loudMover && loudGuard++ < 60) Assert(loudGame.Hold(loudGame.Active, out _), "Advance to the High Tempo mover's next Action");
            Assert(loudGame.Active == loudMover, $"High Tempo mover becomes Ready again (guard {loudGuard}, Time {loudGame.Time})");
            Assert(loudGame.Hold(loudMover, out string loudHoldMessage), "High Tempo mover may complete its next Action: " + loudHoldMessage);
            Assert(!loudMover.Loud, "High Tempo Loud expires when the Formation completes its next Action");

            var controlGame = new PrototypeGame();
            foreach (FormationState formation in controlGame.Formations) formation.Position = formation.Side == Side.Blue ? new HexCoord(0, 8) : new HexCoord(11, 1);
            FormationState blueController = controlGame.Formations.First(formation => formation.Side == Side.Blue && formation.Kind == FormationKind.SurfaceGroup);
            FormationState redController = controlGame.Formations.First(formation => formation.Side == Side.Red && formation.Kind == FormationKind.SurfaceGroup);
            FormationState redAir = controlGame.Formations.First(formation => formation.Side == Side.Red && formation.Kind == FormationKind.AirGroup);
            blueController.Position = controlGame.Area.Objective;
            redAir.Position = controlGame.Area.Objective;
            Assert(controlGame.Controls(Side.Blue, controlGame.Area.Objective), "Air Groups do not establish or contest operational objective control");
            redController.Position = controlGame.Area.Objective;
            Assert(!controlGame.Controls(Side.Blue, controlGame.Area.Objective) && !controlGame.Controls(Side.Red, controlGame.Area.Objective), "Opposing combat-capable naval presence contests control");
            redController.Damage = DamageState.Crippled;
            Assert(controlGame.Controls(Side.Blue, controlGame.Area.Objective), "Crippled Formations do not establish or contest control");

            var recorder = new PlaytestRecorder();
            recorder.StartNew(restored);
            FormationState telemetryActor = restored.Active;
            PlaytestRecorder.Observation observation = recorder.Observe(restored, telemetryActor);
            Assert(restored.Hold(telemetryActor, out string telemetryOutcome), "Telemetry test action");
            recorder.RecordAction(restored, observation, telemetryActor, "Hold", "Quiet", "", "Move; Search; Strike; Hold", 1.25f, "Base Time 1", telemetryOutcome);
            Assert(recorder.SerializeJson().Contains("ActionResolved"), "Telemetry JSON serialization");
            Assert(recorder.SerializeCsv().Contains("DecisionSeconds"), "Telemetry CSV serialization");
            var tacticalMap = new TacticalMapElement();
            tacticalMap.SetState(restored, ToolkitActionMode.Move, MoveMode.Normal, SearchMode.Passive, Salvo.Standard);
            Assert(tacticalMap.focusable, "UI Toolkit tactical map keyboard focus");
            MultiplayerFoundationTests.Run();
            Debug.Log("Sea of Uncertainty integration suite passed.");
        }

        [MenuItem("Sea of Uncertainty/Tests/Run Presentation Suite")]
        public static void RunPresentationTestSuite()
        {
            RunUiAccessibilityTests();
            UiInteractionTests.Run();
            Debug.Log("Sea of Uncertainty presentation suite passed.");
        }

        [MenuItem("Sea of Uncertainty/Tests/Run Build Validation Suite")]
        public static void RunBuildValidationSuite()
        {
            BuildWindows();
            string executable = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Windows/Sea of Uncertainty.exe"));
            string dataDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Windows/Sea of Uncertainty_Data"));
            Assert(File.Exists(executable), "Windows build emits the player executable");
            Assert(Directory.Exists(dataDirectory) && File.Exists(Path.Combine(dataDirectory, "globalgamemanagers")), "Windows build emits its player data and global managers");
            Assert(EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path == "Assets/Scenes/Prototype.unity"), "Build validation uses the authoritative prototype scene");
            Debug.Log("Sea of Uncertainty build-validation suite passed.");
        }

        public static void RunUiAccessibilityTests()
        {
            var descriptions = new HashSet<string>();
            foreach (ActionKind kind in System.Enum.GetValues(typeof(ActionKind)))
            {
                string description = OperationalMissionAudio.Description(kind);
                Assert(!string.IsNullOrWhiteSpace(description), kind + " has a text description for its audio cue");
                Assert(descriptions.Add(description), kind + " has a distinct audio description");
            }
            Assert(descriptions.Count == 8, "All eight action cues have distinct text descriptions");

            string theme = File.ReadAllText(Path.Combine(Application.dataPath, "Resources/UI/SeaTheme.uss"));
            Assert(theme.Contains(".large-text") && theme.Contains(".extra-large-text"), "Theme includes scalable text presentations");
            Assert(theme.Contains(".audio-description"), "Theme includes a visible audio-description status region");

            string input = File.ReadAllText(Path.Combine(Application.dataPath, "../ProjectSettings/InputManager.asset"));
            Assert(input.Contains("m_Name: ControllerHorizontal") && input.Contains("m_Name: ControllerVertical"), "Dedicated controller UI axes are configured independently of keyboard arrows");

            string controller = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/Prototype/SeaUIToolkitController.cs"));
            Assert(controller.Contains("controllerAxisReleased") && controller.Contains("ControllerDeadZone"), "Controller navigation requires stick recentering and honors the configured dead zone");
            Assert(controller.Contains("focusBeforeOverlay") && controller.Contains("CollectFocusables"), "Dialogs restore focus and all interactive UI can participate in focus traversal");
            Assert(controller.Contains("AccessibleId") && controller.Contains("audio-description-status"), "Runtime interface exposes stable semantic element names");
            Debug.Log("Sea of Uncertainty UI/accessibility tests passed.");
        }

        public static void RunAiEvaluationTests()
        {
            var movementGame = new PrototypeGame(3101);
            movementGame.Contacts.Clear();
            FormationState mover = movementGame.Active;
            AiDecision movement = PrototypeAiCommander.Choose(movementGame, AiStrategyProfile.Baseline);
            Assert(movement.Action == ActionKind.Move && HexCoord.Distance(movement.Hex, movementGame.Area.Objective) < HexCoord.Distance(mover.Position, movementGame.Area.Objective), "AI objective-movement fixture advances toward the public objective");

            var recoveryGame = new PrototypeGame(3102);
            recoveryGame.Contacts.Clear();
            recoveryGame.Active.Friction = true;
            Assert(PrototypeAiCommander.Choose(recoveryGame, AiStrategyProfile.Cautious).Action == ActionKind.Recover, "Cautious AI entropy-recovery fixture");

            var prosecutionGame = new PrototypeGame(3103);
            FormationState attacker = prosecutionGame.Active;
            FormationState target = prosecutionGame.Formations.First(item => item.Side != attacker.Side && !item.IsDestroyed);
            target.Position = prosecutionGame.LegalMoveDestinations(attacker, MoveMode.Cautious).First();
            prosecutionGame.Contacts.RemoveAll(item => item.Owner == attacker.Side);
            prosecutionGame.Contacts.Add(new ContactState { Owner = attacker.Side, TargetId = target.Id, LastKnownPosition = target.Position, Location = LocationQuality.High, Identity = IdentityQuality.Identified });
            AiDecision prosecution = PrototypeAiCommander.Choose(prosecutionGame, AiStrategyProfile.Aggressive);
            Assert(prosecution.Action == ActionKind.Strike && prosecution.TargetId == target.Id, "AI Contact-prosecution fixture selects a legal known target");

            var heavyGame = new PrototypeGame(3104);
            FormationState heavyAttacker = heavyGame.Active;
            FormationState heavyTarget = heavyGame.Formations.First(item => item.Side != heavyAttacker.Side && !item.IsDestroyed);
            int heavyDistance = Rules.StrikeRange(heavyAttacker.Kind, Salvo.Standard) + 1;
            HexCoord heavyHex = Enumerable.Range(0, heavyGame.Area.Width).SelectMany(q => Enumerable.Range(0, heavyGame.Area.Height).Select(r => new HexCoord(q, r)))
                .Where(heavyGame.Area.Contains).OrderBy(hex => Mathf.Abs(HexCoord.Distance(heavyAttacker.Position, hex) - heavyDistance)).ThenBy(hex => hex.Q).ThenBy(hex => hex.R).First();
            heavyTarget.Position = heavyHex;
            heavyGame.Contacts.RemoveAll(item => item.Owner == heavyAttacker.Side);
            heavyGame.Contacts.Add(new ContactState { Owner = heavyAttacker.Side, TargetId = heavyTarget.Id, LastKnownPosition = heavyHex, Location = LocationQuality.High, Identity = IdentityQuality.Identified });
            AiDecision heavy = PrototypeAiCommander.Choose(heavyGame, AiStrategyProfile.Aggressive);
            Assert(heavy.Action == ActionKind.Strike && heavy.Salvo == Salvo.Heavy, "Aggressive AI weapon-commitment fixture uses Heavy capability for a Heavy-only opportunity");

            var reactionGame = new PrototypeGame(3105);
            FormationState reactionAttacker = reactionGame.Active;
            FormationState reactionDefender = reactionGame.Formations.First(item => item.Side != reactionAttacker.Side && !item.IsDestroyed);
            reactionDefender.Damage = DamageState.Heavy;
            reactionDefender.Position = reactionGame.LegalMoveDestinations(reactionAttacker, MoveMode.Cautious).First();
            Assert(PrototypeAiCommander.ChooseReaction(reactionGame, reactionAttacker, reactionDefender, AiStrategyProfile.Cautious) == Reaction.Evade, "Cautious AI disengagement fixture evades with a heavily damaged formation");

            var cardGame = new PrototypeGame(3106);
            FormationState cardActor = cardGame.Active;
            CommandResponseDeckState cardDeck = cardGame.CommandResponseDecks.First(deck => deck.Side == cardActor.Side);
            cardDeck.DrawPile.Remove("C-01");
            if (!cardDeck.Hand.Contains("C-01")) cardDeck.Hand.Add("C-01");
            EntropyEffectDefinition attachedFriction = cardGame.MarkEntropy(cardActor, EntropySource.Friction);
            bool cardFixturePassed = attachedFriction != null && PrototypeAiCommander.TryPlayUsefulResponse(cardGame, out _);
            Assert(cardFixturePassed, "AI response-card fixture plays a useful deterministic Friction response");

            AiMetrics baseline = SimulateAiProfile(AiStrategyProfile.Baseline);
            AiMetrics candidate = SimulateAiProfile(AiStrategyProfile.Adaptive);
            Assert(baseline.Illegal == 0 && candidate.Illegal == 0, "Baseline and candidate simulations complete without illegal decisions");
            Assert(candidate.Profiles.Count >= 3 && candidate.Utility > baseline.Utility, "Candidate improves the measured strategic-variety utility while remaining legal");
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/AI"));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "baseline-vs-adaptive.csv"), "Profile,Decisions,Illegal,Holds,DistinctActions,DistinctModes,DistinctProfiles,Utility\n" + baseline.Csv("Baseline") + "\n" + candidate.Csv("Adaptive") + "\n");
            Debug.Log($"Sea of Uncertainty AI evaluation passed. Baseline utility {baseline.Utility}; adaptive utility {candidate.Utility}; report {directory}");
        }

        public static void RunContentSimulationFoundationTests()
        {
            ScenarioDefinition scenario = ScenarioCatalog.MeridianVeil();
            Assert(OperationalDataValidator.Validate(scenario).Count == 0, "Content foundation uses a valid scenario definition");

            FormationDefinition carrierDefinition = scenario.Formations.First(item => item.Kind == FormationKind.CarrierGroup);
            FormationDefinition surfaceDefinition = scenario.Formations.First(item => item.Kind == FormationKind.SurfaceGroup);
            FormationDefinition submarineDefinition = scenario.Formations.First(item => item.Kind == FormationKind.Submarine);
            FormationDefinition airDefinition = scenario.Formations.First(item => item.Kind == FormationKind.AirGroup);
            Assert(surfaceDefinition.Ratings.Signature > submarineDefinition.Ratings.Signature, "Surface and submarine roles retain distinct Signature profiles");
            Assert(Rules.StrikeRange(FormationKind.SurfaceGroup, Salvo.Light) == 3 && Rules.StrikeRange(FormationKind.SurfaceGroup, Salvo.Standard) == 6 && Rules.StrikeRange(FormationKind.SurfaceGroup, Salvo.Heavy) == 9, "Surface groups use their documented 3/6/9 weapon envelope");
            Assert(Rules.StrikeRange(FormationKind.CarrierGroup, Salvo.Heavy) > Rules.StrikeRange(FormationKind.SurfaceGroup, Salvo.Heavy), "Carrier force projection exceeds the surface-group Heavy envelope");
            Assert(Rules.MoveDistance(FormationKind.AirGroup, MoveMode.Normal) > Rules.MoveDistance(FormationKind.SurfaceGroup, MoveMode.Normal), "Air mission packages use a distinct operational radius");

            var game = new PrototypeGame(5101, scenario);
            FormationState carrier = game.Find(carrierDefinition.Id);
            FormationState surface = game.Find(surfaceDefinition.Id);
            FormationState air = game.Find(airDefinition.Id);
            Assert(carrier.Mission == ActionKind.Strike && surface.Mission == ActionKind.Move && air.Mission == ActionKind.Search, "Initial standing missions encode carrier projection, surface maneuver, and air surveillance roles");
            Assert(game.LogisticsFacilitiesFor(carrier).All(item => item.Kind == LocationKind.Port || item.Kind == LocationKind.Anchorage), "Carrier groups use naval logistics facilities");
            Assert(game.LogisticsFacilitiesFor(air).All(item => item.Kind == LocationKind.Airfield), "Air mission packages use airfields");

            SensorRangeDefinition carrierSensors = scenario.SensorRanges.Single(item => item.Kind == FormationKind.CarrierGroup);
            SensorRangeDefinition surfaceSensors = scenario.SensorRanges.Single(item => item.Kind == FormationKind.SurfaceGroup);
            SensorRangeDefinition submarineSensors = scenario.SensorRanges.Single(item => item.Kind == FormationKind.Submarine);
            SensorRangeDefinition airSensors = scenario.SensorRanges.Single(item => item.Kind == FormationKind.AirGroup);
            Assert(surfaceSensors.Passive == 7 && surfaceSensors.Active == 9 && surfaceSensors.Focused == 11, "Surface sensor doctrine uses the documented 7/9/11 envelope");
            Assert(submarineSensors.Passive < surfaceSensors.Passive && carrierSensors.Focused < airSensors.Focused, "Scenario data differentiates submarine, surface, carrier, and air sensor reach");
            Assert(Rules.SearchTarget(airDefinition.Ratings.Search + Rules.SearchModifier(SearchMode.Active) + surfaceDefinition.Ratings.Signature - 2) <=
                   Rules.SearchTarget(airDefinition.Ratings.Search + Rules.SearchModifier(SearchMode.Active) + submarineDefinition.Ratings.Signature - 2), "Higher target Signature is never harder to detect at equal range");

            HexCoord nearbyLand = scenario.Area.Terrain.Where(item => item.Terrain == OperationalTerrain.Land && HexCoord.Distance(air.Position, item.Hex) <= Rules.MoveDistance(FormationKind.AirGroup, MoveMode.Normal)).Select(item => item.Hex).First();
            Assert(game.LegalMoveDestinations(air, MoveMode.Normal).Contains(nearbyLand), "Air mission packages may operate across Land while respecting the shared map boundary");
            Assert(!game.LegalMoveDestinations(surface, MoveMode.HighTempo).Any(hex => scenario.Area.TerrainAt(hex) == OperationalTerrain.Land), "Surface movement remains confined to navigable terrain");

            string foundation = File.ReadAllText(Path.Combine(Application.dataPath, "../../design/CONTENT_SIMULATION_FOUNDATION.md"));
            Assert(foundation.Contains("A separate submarine Contact/combat procedure is not yet earned"), "Submarine procedure remains an explicit evidence gate");
            Assert(foundation.Contains("transfer record") && foundation.Contains("Reinforcement region") && foundation.Contains("stable axial order"), "Connected-theater movement has a deterministic gameplay contract");
            Debug.Log("Sea of Uncertainty content/simulation foundation tests passed.");
        }

        public static void RunAdvancedContentSimulationTests()
        {
            ScenarioDefinition authored = ScenarioCatalog.Find("meridian-veil");
            Assert(authored.Formations.Count == 10 && authored.SpecialRules.Contains("finite magazines"), "External JSON catalog supplies scenario metadata and all formation definitions");
            Assert(authored.Formations.All(item => item.Ratings != null && item.Weapons != null), "Every authored formation has ratings and a finite weapon inventory");

            var combatGame = new PrototypeGame(6201, authored);
            FormationState attacker = combatGame.Active;
            FormationState target = combatGame.Formations.First(item => item.Side != attacker.Side && item.Kind == FormationKind.CarrierGroup);
            target.Position = combatGame.LegalMoveDestinations(attacker, MoveMode.Cautious).First();
            combatGame.Contacts.RemoveAll(item => item.Owner == attacker.Side);
            combatGame.Contacts.Add(new ContactState { Owner = attacker.Side, TargetId = target.Id, LastKnownPosition = target.Position, Location = LocationQuality.High, Identity = IdentityQuality.Identified });
            int lightBefore = attacker.Weapons.Light;
            Assert(combatGame.Strike(attacker, target, Salvo.Light, Reaction.Hold, out CombatResult layeredCombat, out string combatMessage), "Finite-inventory Strike resolves: " + combatMessage);
            Assert(attacker.Weapons.Light == lightBefore - 1, "A Strike expends exactly one matching salvo");
            Assert(layeredCombat.MissileDefenseModifier >= 3 && layeredCombat.MissileDefenseLayers.Contains("Outer"), "Light attacks resolve outer, area, point, and EW defensive layers");

            var submarineGame = new PrototypeGame(6202, authored);
            FormationState aswAttacker = submarineGame.Active;
            FormationState submarine = submarineGame.Formations.First(item => item.Side != aswAttacker.Side && item.Kind == FormationKind.Submarine);
            submarine.Position = submarineGame.LegalMoveDestinations(aswAttacker, MoveMode.Cautious).First();
            submarineGame.Contacts.RemoveAll(item => item.Owner == aswAttacker.Side);
            ContactState datum = new ContactState { Owner = aswAttacker.Side, TargetId = submarine.Id, LastKnownPosition = submarine.Position, Location = LocationQuality.High, Identity = IdentityQuality.General, Domain = ContactDomain.Subsurface };
            submarineGame.Contacts.Add(datum);
            Assert(!submarineGame.Strike(aswAttacker, submarine, Salvo.Standard, Reaction.Hold, out _, out string datumMessage) && datumMessage.Contains("Identified ASW datum"), "Submarine Standard/Heavy attack requires an identified datum");
            int deepSignature = submarine.EffectiveSignature;
            submarine.SubmarineDepth = SubmarineDepthState.Shallow;
            Assert(submarine.EffectiveSignature == deepSignature + 1, "Shallow submarine operations increase detectable Signature");

            var logisticsGame = new PrototypeGame(6203, authored);
            FormationState carrier = logisticsGame.Formations.First(item => item.Side == Side.Blue && item.Kind == FormationKind.CarrierGroup);
            FormationState fleetTrain = logisticsGame.Formations.First(item => item.Side == Side.Blue && item.Kind == FormationKind.LogisticsGroup);
            Assert(HexCoord.Distance(carrier.Position, fleetTrain.Position) <= 1 && logisticsGame.HasLogisticsAccess(carrier), "A combat-capable fleet train provides mobile logistics access within one hex");
            Assert(fleetTrain.Weapons.MaxStandard == 0 && fleetTrain.Weapons.MaxHeavy == 0, "Logistics formations have restricted defensive weapon inventories");

            ScenarioDefinition northern = ScenarioCatalog.Find("northern-gateway");
            var eventGame = new PrototypeGame(6204, northern);
            Assert(eventGame.Sides[Side.Blue].Architecture == CommandArchitecture.Distributed && eventGame.CommandSlotsFor(Side.Blue).Count == 2, "Distributed command uses two decentralized attention slots");
            Assert(eventGame.Sides[Side.Red].Architecture == CommandArchitecture.Centralized && eventGame.CommandSlotsFor(Side.Red).Count == 4, "Centralized command uses four attention slots");
            Assert(eventGame.SearchModifierFor(eventGame.Active, SearchMode.Active) == Rules.SearchModifier(SearchMode.Active) - 1, "Maritime haze applies its scenario weather penalty to Search");
            int initialFormationCount = eventGame.Formations.Count;
            PrototypeGame.SaveData eventState = eventGame.CaptureState();
            eventState.Time = 7;
            foreach (FormationState formation in eventState.Formations) formation.ReadyTime = 8;
            eventState.ActiveFormationId = eventState.Formations[0].Id;
            eventState.Formations[0].ReadyTime = 7;
            eventGame.RestoreState(eventState);
            Assert(eventGame.Hold(eventGame.Active, out _), "Timeline fixture advances through the reinforcement event");
            Assert(eventGame.Formations.Count == initialFormationCount + 1 && eventGame.Find("B-SG-R1") != null && eventGame.ResolvedScheduledEventIds.Contains("blue-reinforcement"), "Scheduled reinforcement enters deterministically through its named region");

            PrototypeGame.SaveData weatherState = eventGame.CaptureState();
            weatherState.Time = 11;
            foreach (FormationState formation in weatherState.Formations) formation.ReadyTime = 12;
            weatherState.ActiveFormationId = weatherState.Formations[0].Id;
            weatherState.Formations[0].ReadyTime = 11;
            eventGame.RestoreState(weatherState);
            Assert(eventGame.Hold(eventGame.Active, out _) && eventGame.RuntimeWeatherSeverity == 0 && eventGame.RuntimeWeather == "Clear", "Scheduled weather change updates authoritative environmental rules");
            string roundTrip = JsonUtility.ToJson(eventGame.CaptureState());
            var restored = new PrototypeGame(6204, northern);
            restored.RestoreState(JsonUtility.FromJson<PrototypeGame.SaveData>(roundTrip));
            Assert(restored.RuntimeWeather == "Clear" && restored.ResolvedScheduledEventIds.Contains("weather-clears") && restored.Find("B-SG-R1")?.Weapons != null, "Weather, events, reinforcements, command architecture, and magazines survive save/load");

            string editorSource = File.ReadAllText(Path.Combine(Application.dataPath, "Editor/ScenarioCatalogEditorWindow.cs"));
            Assert(editorSource.Contains("Scenario Catalog Editor") && editorSource.Contains("Save JSON") && editorSource.Contains("ValidateCatalog"), "Scenario editor supports catalog selection, validation, formation editing, and JSON save");
            Debug.Log("Sea of Uncertainty advanced content/simulation tests passed.");
        }

        private sealed class AiMetrics
        {
            public int Decisions;
            public int Illegal;
            public int Holds;
            public readonly HashSet<ActionKind> Actions = new HashSet<ActionKind>();
            public readonly HashSet<string> Modes = new HashSet<string>();
            public readonly HashSet<AiStrategyProfile> Profiles = new HashSet<AiStrategyProfile>();
            public int Utility => Actions.Count * 4 + Modes.Count * 2 + Profiles.Count * 3 - Illegal * 100 - Holds;
            public string Csv(string name) => $"{name},{Decisions},{Illegal},{Holds},{Actions.Count},{Modes.Count},{Profiles.Count},{Utility}";
        }

        private static AiMetrics SimulateAiProfile(AiStrategyProfile profile)
        {
            var metrics = new AiMetrics();
            for (int seed = 4200; seed < 4205; seed++)
            {
                var game = new PrototypeGame(seed);
                int guard = 0;
                while (game.Time < game.Scenario.Horizon && guard++ < 600)
                {
                    AiDecision decision = PrototypeAiCommander.Choose(game, profile);
                    metrics.Decisions++;
                    if (decision == null || !PrototypeAiCommander.Execute(game, decision, out _))
                    {
                        metrics.Illegal++;
                        if (!game.Hold(game.Active, out _)) break;
                        continue;
                    }
                    metrics.Actions.Add(decision.Action);
                    metrics.Modes.Add(decision.ModeName);
                    metrics.Profiles.Add(decision.Profile);
                    if (decision.Action == ActionKind.Hold) metrics.Holds++;
                }
                Assert(guard < 600, profile + " AI simulation reaches the scenario horizon without stalling");
            }
            return metrics;
        }

        public static void CaptureMapPreview()
        {
            ScenarioDefinition scenario = ScenarioCatalog.LuzonStrait();
            var game = new PrototypeGame(1978, scenario);
            using (var map = new OperationalMap3D(scenario.Area, 1280, 720))
            {
                map.SetState(game, ToolkitActionMode.None, MoveMode.Normal, SearchMode.Passive, Salvo.Standard, false, true);
                map.Orbit(new Vector2(0f, -1000f));
                map.ProbeRenderLuminance();
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = map.Texture;
                var image = new Texture2D(map.Texture.width, map.Texture.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, map.Texture.width, map.Texture.height), 0, 0, false);
                image.Apply(false, false);
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Windows/3d-map-preview.png"));
                File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                RenderTexture.active = previous;
                Debug.Log("3D map preview captured: " + path);
            }
        }

        private static void EnsureRuntimePanelSettings()
        {
            ThemeStyleSheet runtimeTheme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/Resources/UI/UnityDefaultRuntimeTheme.tss");
            if (runtimeTheme == null) throw new System.Exception("Default runtime theme asset is missing.");

            PanelSettings panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(RuntimePanelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                panel.name = "Sea Runtime Panel";
                AssetDatabase.CreateAsset(panel, RuntimePanelPath);
            }

            panel.themeStyleSheet = runtimeTheme;
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = .5f;
            panel.sortingOrder = 10;
            EditorUtility.SetDirty(panel);
            AssetDatabase.SaveAssets();
        }

        private static void Ensure3DMaterials()
        {
            const string folder = "Assets/Resources/Materials/3D";
            System.IO.Directory.CreateDirectory(folder);
            EnsureMaterial(folder + "/CommandLine.mat", "Sprites/Default", new Color(.2f, .74f, .82f, .38f));
            EnsureMaterial(folder + "/CommandWater.mat", "Standard", new Color(.018f, .13f, .19f, 1f));
            EnsureMaterial(folder + "/CommandLand.mat", "Standard", new Color(.22f, .31f, .22f, 1f));
            EnsureMaterial(folder + "/CommandHighland.mat", "Standard", new Color(.29f, .31f, .16f, 1f));
            EnsureMaterial(folder + "/CommandLittoral.mat", "Standard", new Color(.09f, .34f, .34f, 1f));
            EnsureMaterial(folder + "/CommandBlue.mat", "Standard", new Color(.08f, .72f, .95f, 1f));
            EnsureMaterial(folder + "/CommandRed.mat", "Standard", new Color(.94f, .25f, .18f, 1f));
            EnsureMaterial(folder + "/CommandContact.mat", "Standard", new Color(1f, .48f, .13f, 1f));
            EnsureMaterial(folder + "/CommandWarning.mat", "Standard", new Color(1f, .72f, .12f, 1f));
            AssetDatabase.SaveAssets();
        }

        private static void EnsureMaterial(string path, string shaderName, Color color)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new System.Exception("Required shader is unavailable: " + shaderName);
            if (material == null)
            {
                material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.color = color;
            EditorUtility.SetDirty(material);
        }

        private static void Assert(bool condition, string label)
        {
            if (!condition) throw new System.Exception("Core smoke test failed: " + label);
        }
    }
}
#endif
