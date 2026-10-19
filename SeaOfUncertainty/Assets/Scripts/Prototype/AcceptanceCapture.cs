#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SeaOfUncertainty.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeaOfUncertainty.Prototype
{
    public sealed partial class SeaUIToolkitController
    {
        // Explicit development-build entry point; exercises the production screens.
        // Captures are evidence for review, never a substitute for observed play.
        private IEnumerator RunAcceptanceCapture()
        {
            var pending = new Stack<IEnumerator>();
            pending.Push(CaptureAcceptance());
            while (pending.Count > 0)
            {
                bool moved = false, failed = false;
                object next = null;
                try { moved = pending.Peek().MoveNext(); if (moved) next = pending.Peek().Current; }
                catch (Exception exception) { Debug.LogException(exception); failed = true; }
                if (failed)
                {
                    while (pending.Count > 0) (pending.Pop() as IDisposable)?.Dispose();
                    Application.Quit(1);
                    yield break;
                }
                if (!moved) { (pending.Pop() as IDisposable)?.Dispose(); continue; }
                if (next is IEnumerator nested) pending.Push(nested);
                else yield return next;
            }
        }

        private IEnumerator CaptureAcceptance()
        {
            Application.runInBackground = true;
            string directory = Environment.GetCommandLineArgs().FirstOrDefault(arg => arg.StartsWith("-acceptanceOutput=", StringComparison.Ordinal))?.Substring("-acceptanceOutput=".Length)
                ?? Path.Combine(Application.persistentDataPath, "AcceptanceCaptures");
            Directory.CreateDirectory(directory);
            var notes = new List<string> { "Automated production UI captures; no human input, DPI or controller acceptance claimed.", SystemInfo.operatingSystem, SystemInfo.graphicsDeviceName };
            bool oldAge = backend.AgeTwoPenalty, oldDescriptions = backend.AudioDescriptions, oldContrast = backend.HighContrast, oldMotion = backend.ReducedMotion, oldGrid = backend.HexGridVisible, oldController = backend.ControllerNavigation;
            float oldMaster = backend.MasterVolume, oldEffects = backend.EffectsVolume, oldAmbient = backend.AmbientVolume, oldScale = backend.TextScale, oldDeadZone = backend.ControllerDeadZone;
            bool success = false;
            try
            {
                foreach (string id in new[] { "meridian-veil", "northern-gateway", "narrows-shield" })
                {
                    foreach (int width in new[] { 1280, 1920 })
                    {
                        Screen.SetResolution(width, width == 1280 ? 720 : 1080, FullScreenMode.Windowed);
                        yield return new WaitForSecondsRealtime(.4f);
                        foreach (float scale in new[] { 1f, 1.5f })
                        {
                            backend.ToolkitSetSettings(oldAge, 0, oldEffects, oldAmbient, true, oldContrast, true, true, scale, oldController, oldDeadZone);
                            backend.ToolkitNewScenario(id);
                            backend.ToolkitSetOperationMode(OperationMode.LocalHotseat);
                            guidePending = false;
                            string prefix = id + "-" + width + "-" + (int)(scale * 100);
                            ShowBriefing();
                            yield return CaptureFrame(directory, prefix + "-briefing", notes);
                            ShowSetup();
                            yield return CaptureFrame(directory, prefix + "-setup", notes);
                            ShowGame(); map.FocusObjective();
                            yield return CaptureFrame(directory, prefix + "-objective", notes);
                            ShowFormationOrders(OrderAnchor());
                            yield return CaptureFrame(directory, prefix + "-orders", notes);
                            CloseOverlay(); ShowInspector();
                            yield return CaptureFrame(directory, prefix + "-inspector", notes);
                            CloseOverlay(); ShowInspector(backend.Game.Contacts.FirstOrDefault(item => item.Owner == backend.Game.Active.Side && !item.IsLost));
                            yield return CaptureFrame(directory, prefix + "-contact", notes);
                            CloseOverlay(); ShowResponseHand();
                            yield return CaptureFrame(directory, prefix + "-cards", notes);
                            CloseOverlay(); ShowReadyQueue();
                            yield return CaptureFrame(directory, prefix + "-ready", notes);
                            CloseOverlay(); ShowMapGuide();
                            yield return CaptureFrame(directory, prefix + "-guide", notes);
                            CloseOverlay(); ShowRules();
                            yield return CaptureFrame(directory, prefix + "-manual", notes);
                            CloseOverlay(); ShowHandoff();
                            yield return CaptureFrame(directory, prefix + "-handoff", notes);

                            FormationState actor = backend.Game.Formations.First(item => item.Side == Side.Red && item.BallisticWeapons.Capacity > 0);
                            PrototypeGame.SaveData state = backend.Game.CaptureState(); state.ActiveFormationId = actor.Id; backend.Game.RestoreState(state);
                            ContactState report = backend.Game.Contacts.First(item => item.Owner == Side.Red && backend.Game.Find(item.TargetId)?.Kind == FormationKind.CarrierGroup);
                            pendingStrikeContact = report; pendingStrikeAim = report.LastKnownPosition; weaponDelivery = WeaponDelivery.Ballistic;
                            BeginStrikeReaction(backend.Game.Find(report.TargetId), Salvo.Standard);
                            yield return CaptureFrame(directory, prefix + "-reaction-handoff", notes);
                            ShowReactionChoice();
                            yield return CaptureFrame(directory, prefix + "-reaction", notes);
                        }
                    }
                }
                Screen.SetResolution(1920, 1080, FullScreenMode.FullScreenWindow);
                yield return new WaitForSecondsRealtime(.5f);
                backend.ToolkitNewScenario("meridian-veil"); ShowGame();
                yield return CaptureFrame(directory, "fullscreen-map", notes);
                foreach (string id in new[] { "meridian-veil", "northern-gateway", "narrows-shield" })
                    yield return CaptureSession(directory, id, notes, OperationMode.SoloVsAi);
                yield return CaptureSession(directory, "meridian-veil", notes, OperationMode.LocalHotseat);
                success = true;
            }
            finally
            {
                backend.ToolkitSetSettings(oldAge, oldMaster, oldEffects, oldAmbient, oldDescriptions, oldContrast, oldMotion, oldGrid, oldScale, oldController, oldDeadZone);
                File.WriteAllLines(Path.Combine(directory, "capture-report.txt"), notes);
                Application.Quit(success ? 0 : 1);
            }

        }

        private IEnumerator CaptureFrame(string directory, string name, List<string> notes)
        {
            yield return new WaitForSecondsRealtime(.18f);
            yield return new WaitForEndOfFrame();
            Texture2D capture = ScreenCapture.CaptureScreenshotAsTexture();
            bool visible = false;
            for (int x = 1; x < 10; x++)
                for (int y = 1; y < 10; y++)
                {
                    Color sample = capture.GetPixel(capture.width * x / 10, capture.height * y / 10);
                    if (sample.r + sample.g + sample.b > .02f) visible = true;
                }
            if (!visible) { Destroy(capture); throw new InvalidOperationException("Black capture; rerun with a visible player window."); }
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), capture.EncodeToPNG());
            Destroy(capture);
            notes.Add($"{name}: {Screen.width}x{Screen.height}, {Screen.fullScreenMode}, scale {backend.TextScale}, panel {root.worldBound}, managed memory {GC.GetTotalMemory(false)}");
            foreach (Button button in root.Query<Button>().ToList().Where(item => item.enabledInHierarchy))
            {
                Rect bounds = button.worldBound;
                Rect viewport = root.worldBound;
                if (bounds.height > 0 && (bounds.yMax < viewport.yMin || bounds.yMin > viewport.yMax) && button.GetFirstAncestorOfType<ScrollView>() == null)
                    notes.Add("LAYOUT REVIEW " + name + ": offscreen button " + button.text);
            }
        }

        private IEnumerator CaptureSession(string directory, string id, List<string> notes, OperationMode mode)
        {
            backend.ToolkitNewScenario(id);
            backend.ToolkitSetOperationMode(mode);
            string prefix = id + "-" + mode.ToString().ToLowerInvariant();
            int actions = 0;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            while (!backend.IsComplete && actions++ < 600)
            {
                backend.ToolkitBeginDecision();
                AiDecision decision = PrototypeAiCommander.Choose(backend.Game);
                backend.ToolkitExecuteAutomatedPlaytestDecision(decision);
                if (!backend.LastToolkitActionSucceeded) throw new InvalidOperationException("Automated session failed: " + id + ": " + backend.LastMessage);
                if (backend.SelectedMode == OperationMode.LocalHotseat) ShowHandoff(); else if (backend.IsAiTurn) ShowAiPlanning(); else ShowGame();
                yield return null;
                if (actions % 20 == 0) yield return CaptureFrame(directory, prefix + "-session-" + actions, notes);
            }
            notes.Add($"AUTOMATED SESSION {id} {mode}: actions {actions}, T{backend.Game.Time}, completed {backend.IsComplete}, elapsed {timer.Elapsed.TotalSeconds:F1}s. Both sides selected by AI; no human balance acceptance.");
            backend.Telemetry.Export(backend.Feedback);
            File.WriteAllText(Path.Combine(directory, prefix + "-session.json"), JsonUtility.ToJson(backend.Telemetry.Session, true));
            ShowResults();
            yield return CaptureFrame(directory, prefix + "-results", notes);
        }
    }
}
#endif
