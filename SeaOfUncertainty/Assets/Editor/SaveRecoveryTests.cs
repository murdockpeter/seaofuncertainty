#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using SeaOfUncertainty.Core;
using SeaOfUncertainty.Prototype;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeaOfUncertainty.Editor
{
    public static class SaveRecoveryTests
    {
        public static void Run()
        {
            bool oldRunInBackground = Application.runInBackground;
            string directory = Path.Combine(Application.temporaryCachePath, "SaveRecovery-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var host = new GameObject("Save Recovery Tests");
            try
            {
                string path = Path.Combine(directory, "operation.json");
                var files = new FailingFiles();
                var store = new OperationSaveStore(path, files);
                var first = new OperationSave { Game = new PrototypeGame().CaptureState(), OperationMode = OperationMode.SoloVsAi, HumanSide = Side.Red };
                first.Game.ActiveFormationId = first.Game.Formations.First(item => item.Side == Side.Red).Id;
                string firstJson = JsonUtility.ToJson(first, true);
                store.Save(first);
                Check(File.ReadAllText(path) == firstJson && !File.Exists(store.BackupPath), "First save publishes the flushed temporary file without inventing a backup");
                var nextGame = new PrototypeGame();
                nextGame.Hold(nextGame.Active, out _);
                var next = new OperationSave { Game = nextGame.CaptureState() };
                store.Save(next);
                Check(File.ReadAllText(store.BackupPath) == firstJson && store.Load().Game.Active.Id == nextGame.Active.Id, "Replacing a save retains the prior valid operation and restores current readiness");
                string primary = File.ReadAllText(path), backup = File.ReadAllText(store.BackupPath);
                foreach (string stage in new[] { "write", "verify", "read", "replace" })
                {
                    files.Failure = stage;
                    Reject(() => store.Save(first), "Injected " + stage + " failure is reported");
                    Check(File.ReadAllText(path) == primary && File.ReadAllText(store.BackupPath) == backup, stage + " failure preserves primary and backup byte-for-byte");
                    Check(!Directory.GetFiles(directory, "*.tmp").Any(), stage + " failure cleans its own temporary file");
                }
                files.Failure = null;
                File.WriteAllText(path, "{truncated");
                Check(store.HasValidBackup(), "A corrupt primary still permits validated backup recovery");
                Check(store.Load(true).Save.HumanSide == Side.Red && store.Load(true).Save.OperationMode == OperationMode.SoloVsAi, "Backup retains player side and Solo mode");
                store.Save(next);
                Check(File.ReadAllText(store.BackupPath) == backup, "Saving over a corrupt primary does not rotate corruption into the good backup");
                File.Delete(path);
                Check(store.HasSave && store.HasValidBackup(), "A missing primary does not hide a recoverable backup");
                store.Save(next);
                Check(File.ReadAllText(store.BackupPath) == backup, "Publishing a new primary preserves an existing backup");

                Reject(() => OperationSaveStore.Decode("{}"), "Empty objects are not playable saves");
                Reject(() => OperationSaveStore.Decode(firstJson.Substring(0, firstJson.Length / 2)), "Truncated saves fail validation");
                Reject(() => OperationSaveStore.Decode("{bad json}"), "Malformed JSON fails validation");
                first.Version = 6;
                Reject(() => OperationSaveStore.Decode(JsonUtility.ToJson(first)), "Newer operation envelope is rejected");
                first.Version = 4; first.Game.Version = 15;
                Reject(() => OperationSaveStore.Decode(JsonUtility.ToJson(first)), "Newer nested game format is rejected");
                first.Game.Version = 11; first.Game.ScenarioId = "missing-authored-operation";
                Reject(() => OperationSaveStore.Decode(JsonUtility.ToJson(first)), "Unknown authored scenario never silently loads Meridian Veil");
                first.Game.ScenarioId = "meridian-veil"; first.Game.Version = 1; first.Game.ScenarioId = null; first.Game.OperationalAreaId = null;
                Check(OperationSaveStore.Decode(JsonUtility.ToJson(first.Game)).Game.Scenario.Id == "meridian-veil", "Legacy raw v1 saves retain the explicit Meridian Veil compatibility path");
                first.Game = new PrototypeGame().CaptureState();
                Check(OperationSaveStore.Decode(JsonUtility.ToJson(first.Game)).Game.Scenario.Id == "meridian-veil", "Raw current SaveData remains compatible");

                var backend = host.AddComponent<SeaPrototypeController>();
                backend.EditorSavePathOverride = path;
                backend.EditorEnsureInitializedForTests();
                var ui = host.AddComponent<SeaUIToolkitController>();
                File.WriteAllText(path, "{bad json}");
                File.WriteAllText(store.BackupPath, firstJson);
                ui.EditorEnsureInitializedForTests();
                PrototypeGame running = backend.Game;
                PlaytestSession session = backend.Telemetry.Session;
                Check(ui.EditorInvokeButton("CONTINUE SAVED OPERATION") && ui.EditorView == "Main" && ui.EditorRoot.Q<VisualElement>("dialog-loadfailed") != null,
                    "Failed load stays on the current screen with a clear error dialog");
                Check(ReferenceEquals(running, backend.Game) && ReferenceEquals(session, backend.Telemetry.Session), "Failed load leaves running game and telemetry untouched");
                Check(ui.EditorInvokeButton("CANCEL") && ReferenceEquals(running, backend.Game), "Cancelling recovery leaves the current operation intact");
                ui.EditorInvokeButton("CONTINUE SAVED OPERATION");
                Check(ui.EditorInvokeButton("LOAD PREVIOUS BACKUP") && backend.SelectedMode == OperationMode.SoloVsAi && backend.HumanSide == Side.Red,
                    "Explicit backup button restores saved play mode and player side");
                Check(File.ReadAllText(path) == "{bad json}" && File.ReadAllText(store.BackupPath) == firstJson, "Recovery reads leave both save files unchanged");
                File.WriteAllText(store.BackupPath, "{}");
                Check(!backend.HasRecoverableBackup, "An invalid backup is never offered as valid recovery");
                TestChecks.ThrowIfAny("Save recovery");
                Debug.Log("Sea of Uncertainty save recovery tests passed.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                Application.runInBackground = oldRunInBackground;
                foreach (string temporary in Directory.GetFiles(directory)) File.Delete(temporary);
                Directory.Delete(directory);
            }
        }

        private sealed class FailingFiles : IOperationSaveFiles
        {
            private readonly OperationSaveFiles real = new OperationSaveFiles();
            public string Failure;
            public bool Exists(string path) => real.Exists(path);
            public string Read(string path)
            {
                if (Failure == "read" && path.EndsWith(".json", StringComparison.Ordinal)) throw new IOException("Injected primary read failure");
                return real.Read(path);
            }
            public void WriteFlushed(string path, string json)
            {
                if (Failure == "write") { File.WriteAllText(path, "partial"); throw new IOException("Injected write failure"); }
                if (Failure == "verify") { real.WriteFlushed(path, "{}"); return; }
                real.WriteFlushed(path, json);
            }
            public void Replace(string temporary, string primary, string backup)
            {
                if (Failure == "replace") throw new IOException("Injected replace failure");
                real.Replace(temporary, primary, backup);
            }
            public void Move(string temporary, string primary) => real.Move(temporary, primary);
            public void Delete(string path) => real.Delete(path);
        }

        private static void Reject(Action action, string label)
        {
            try { action(); Check(false, label); }
            catch (Exception exception) when (exception is ArgumentException || exception is IOException || exception is InvalidDataException) { Check(true, label); }
        }
        private static void Check(bool condition, string label) => TestChecks.Check(condition, label);
    }
}
#endif
