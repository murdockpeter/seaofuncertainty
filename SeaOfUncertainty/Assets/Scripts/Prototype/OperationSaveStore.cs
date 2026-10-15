using System;
using System.IO;
using System.Linq;
using System.Text;
using SeaOfUncertainty.Core;
using UnityEngine;

namespace SeaOfUncertainty.Prototype
{
    [Serializable]
    public sealed class OperationSave
    {
        public int Version = 4;
        public OperationMode OperationMode;
        public Side HumanSide = Side.Blue;
        public PrototypeGame.SaveData Game;
        public PlaytestSession Playtest;
        public PlaytestFeedback Feedback;
    }

    public sealed class RestoredOperation
    {
        public PrototypeGame Game;
        public OperationSave Save;
    }

    // Filesystem replacement is kept separate from decoding so failures can be exercised
    // without touching a player's real saves. Never fall back to delete-and-copy.
    public interface IOperationSaveFiles
    {
        bool Exists(string path);
        string Read(string path);
        void WriteFlushed(string path, string json);
        void Replace(string temporary, string primary, string backup);
        void Move(string temporary, string primary);
        void Delete(string path);
    }

    public sealed class OperationSaveFiles : IOperationSaveFiles
    {
        public bool Exists(string path) => File.Exists(path);
        public string Read(string path) => File.ReadAllText(path);
        public void WriteFlushed(string path, string json)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(json);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }
        public void Replace(string temporary, string primary, string backup) => File.Replace(temporary, primary, backup);
        public void Move(string temporary, string primary) => File.Move(temporary, primary);
        public void Delete(string path) => File.Delete(path);
    }

    public sealed class OperationSaveStore
    {
        [Serializable]
        private sealed class VersionHeader
        {
            public int Version;
            public GameVersionHeader Game;
        }

        [Serializable]
        private sealed class GameVersionHeader { public int Version; }

        private readonly IOperationSaveFiles files;
        public string PrimaryPath { get; }
        public string BackupPath => PrimaryPath + ".bak";
        public bool HasSave => files.Exists(PrimaryPath) || files.Exists(BackupPath);

        public OperationSaveStore(string path, IOperationSaveFiles files = null)
        {
            PrimaryPath = path;
            this.files = files ?? new OperationSaveFiles();
        }

        public RestoredOperation Load(bool backup = false) => Decode(files.Read(backup ? BackupPath : PrimaryPath));

        public bool HasValidBackup()
        {
            try { Load(true); return true; }
            catch (Exception) { return false; }
        }

        public void Save(OperationSave save)
        {
            string json = JsonUtility.ToJson(save, true);
            Decode(json); // Reject bad state before creating any file.
            string temporary = PrimaryPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                files.WriteFlushed(temporary, json);
                Decode(files.Read(temporary));
                if (!files.Exists(PrimaryPath)) files.Move(temporary, PrimaryPath);
                else
                {
                    // An unreadable primary is not proven corrupt: fail rather than
                    // replacing it without a backup when access or IO fails.
                    string previous = files.Read(PrimaryPath);
                    bool validPrimary;
                    try { Decode(previous); validPrimary = true; }
                    catch (Exception) { validPrimary = false; }
                    // A corrupt primary must never displace the existing good backup.
                    files.Replace(temporary, PrimaryPath, validPrimary ? BackupPath : null);
                }
            }
            finally
            {
                try { if (files.Exists(temporary)) files.Delete(temporary); }
                catch (IOException) { /* A stranded temp is never treated as a save. */ }
                catch (UnauthorizedAccessException) { }
            }
        }

        public static RestoredOperation Decode(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{", StringComparison.Ordinal)
                || !json.TrimEnd().EndsWith("}", StringComparison.Ordinal))
                throw new InvalidDataException("The save is empty or truncated.");
            VersionHeader header = JsonUtility.FromJson<VersionHeader>(json);
            if (header == null || header.Version < 1) throw new InvalidDataException("The save has no supported format version.");
            OperationSave operation;
            PrototypeGame.SaveData data;
            // JsonUtility may create an empty nested object for an absent Game field.
            // A nonzero nested version identifies an envelope; malformed empty
            // envelopes then fail the raw required-state checks below.
            if (header.Game != null && header.Game.Version != 0)
            {
                if (header.Version > 4) throw new InvalidDataException("This operation save requires a newer game version.");
                if (header.Game.Version < 1 || header.Game.Version > 13) throw new InvalidDataException("Unsupported game save version; a newer game may be required.");
                operation = JsonUtility.FromJson<OperationSave>(json);
                data = operation.Game;
            }
            else
            {
                // Documented compatibility path: pre-envelope raw SaveData v1-v13.
                if (header.Version > 13) throw new InvalidDataException("This save requires a newer game version.");
                data = JsonUtility.FromJson<PrototypeGame.SaveData>(json);
                operation = new OperationSave { Version = 1, Game = data };
            }
            if (data == null || data.Formations == null || data.Formations.Count == 0 || data.Time < 0)
                throw new InvalidDataException("The save is missing required game state.");
            if (data.Formations.Any(item => item == null || string.IsNullOrWhiteSpace(item.Id) || !Enum.IsDefined(typeof(Side), item.Side))
                || data.Formations.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != data.Formations.Count)
                throw new InvalidDataException("The save contains invalid or duplicate formations.");
            if (operation.Version >= 4 && (!Enum.IsDefined(typeof(OperationMode), operation.OperationMode) || !Enum.IsDefined(typeof(Side), operation.HumanSide)))
                throw new InvalidDataException("The save has an invalid play mode or player side.");
            if (data.Version >= 12 && data.Formations.Any(item => item.BallisticWeapons == null || !item.BallisticWeapons.IsValid))
                throw new InvalidDataException("The save contains an invalid ballistic profile or magazine.");
            if (data.Version >= 13 && data.Formations.Any(item => item.BallisticDefense == null || !item.BallisticDefense.IsValid))
                throw new InvalidDataException("The save contains invalid ballistic defense or interceptor stocks.");
            string scenarioId = data.ScenarioId;
            if (string.IsNullOrEmpty(scenarioId))
            {
                if (data.Version != 1) throw new InvalidDataException("The save is missing its scenario ID.");
                scenarioId = "meridian-veil";
            }
            ScenarioDefinition scenario;
            try { scenario = ScenarioCatalog.Require(scenarioId); }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException($"Scenario '{scenarioId}' could not be loaded. Restore the matching scenario catalog before retrying. {exception.Message}", exception);
            }
            if (!string.IsNullOrEmpty(data.OperationalAreaId) && data.OperationalAreaId != scenario.Area.Id)
                throw new InvalidDataException("The save's theater does not match its scenario.");
            var restored = new PrototypeGame(1978, scenario);
            restored.RestoreState(data);
            if (restored.Active == null && restored.Time < scenario.Horizon)
                throw new InvalidDataException("The save has no playable Ready formation.");
            return new RestoredOperation { Game = restored, Save = operation };
        }
    }
}
