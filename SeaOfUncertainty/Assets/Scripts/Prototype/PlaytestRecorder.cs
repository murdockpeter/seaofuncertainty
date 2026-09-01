using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SeaOfUncertainty.Core;
using UnityEngine;

namespace SeaOfUncertainty.Prototype
{
    [Serializable]
    public sealed class PlaytestEvent
    {
        public int Sequence;
        public string RecordedUtc;
        public float SessionSeconds;
        public int OperationalTime;
        public string Category;
        public string Side;
        public string FormationId;
        public string Action;
        public string Mode;
        public string TargetId;
        public string LegalAlternatives;
        public float DecisionSeconds;
        public string Calculation;
        public string Outcome;
        public int ReadyBefore;
        public int ReadyAfter;
        public int CommandBefore;
        public int CommandAfter;
        public string ContactChanges;
        public string EntropyChanges;
        public int BlueObjectiveRange;
        public int RedObjectiveRange;
        public int ConsecutiveSideActivations;
    }

    [Serializable]
    public sealed class PlaytestFeedback
    {
        public int ReadyTimeClarity = 3;
        public int InformationImpact = 3;
        public int CombatFairness = 3;
        public int EntropyInterest = 3;
        public bool ObviousChoiceOccurred;
        public bool RepeatedRuleLookup;
        public bool UnfairFeelingRoll;
        public bool InactivePlayerLull;
        public string BestDecision = string.Empty;
        public string MostConfusingMoment = string.Empty;
        public string RulesAdjustment = string.Empty;
    }

    [Serializable]
    public sealed class PlaytestSession
    {
        public int Version = 1;
        public string SessionId;
        public string Scenario = "Meridian Veil";
        public string StartedUtc;
        public string EndedUtc;
        public string Build = "P0 Instrumentation";
        public List<PlaytestEvent> Events = new List<PlaytestEvent>();
        public PlaytestFeedback Feedback = new PlaytestFeedback();
    }

    public sealed class PlaytestRecorder
    {
        public sealed class Observation
        {
            public int Time;
            public int Ready;
            public int Command;
            public Dictionary<string, string> Contacts;
            public Dictionary<string, string> Entropy;
        }

        private PlaytestSession session;
        private float sessionStarted;
        private readonly Dictionary<string, int> entropyMarkedAt = new Dictionary<string, int>();
        private Side? previousActingSide;
        private int consecutiveSideActivations;

        public PlaytestSession Session => session;
        public string LastExportDirectory { get; private set; }
        public string SerializeJson() => JsonUtility.ToJson(session, true);
        public string SerializeCsv() => ToCsv();

        public void StartNew(PrototypeGame game)
        {
            sessionStarted = Time.realtimeSinceStartup;
            session = new PlaytestSession
            {
                SessionId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture),
                StartedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                Scenario = game.Scenario.DisplayName,
                Build = "3D Operational Map Foundation"
            };
            previousActingSide = null;
            consecutiveSideActivations = 0;
            entropyMarkedAt.Clear();
            Record(game, "Session", null, null, null, null, "", 0, "", game.Scenario.DisplayName + " initialized.", null);
        }

        public void Restore(PlaytestSession restored)
        {
            session = restored ?? new PlaytestSession();
            if (string.IsNullOrEmpty(session.SessionId)) session.SessionId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            sessionStarted = Time.realtimeSinceStartup;
        }

        public Observation Observe(PrototypeGame game, FormationState actor)
        {
            return new Observation
            {
                Time = game.Time,
                Ready = actor?.ReadyTime ?? -1,
                Command = actor == null ? -1 : game.Sides[actor.Side].CommandSlots,
                Contacts = game.Contacts.ToDictionary(ContactKey, ContactValue),
                Entropy = game.Formations.ToDictionary(f => f.Id, EntropyValue)
            };
        }

        public void RecordChoice(PrototypeGame game, FormationState actor, string category, string action, string mode, string legalAlternatives)
        {
            Record(game, category, actor, action, mode, null, legalAlternatives, 0, "", "Selection changed before commitment.", null);
        }

        public void RecordRejected(PrototypeGame game, FormationState actor, string action, string mode, string targetId, string reason, float decisionSeconds)
        {
            Record(game, "ActionRejected", actor, action, mode, targetId, "", decisionSeconds, "", reason, null);
        }

        public void RecordAction(PrototypeGame game, Observation before, FormationState actor, string action, string mode, string targetId, string legalAlternatives, float decisionSeconds, string calculation, string outcome)
        {
            string contactChanges = DescribeChanges(before.Contacts, game.Contacts.ToDictionary(ContactKey, ContactValue));
            Dictionary<string, string> afterEntropy = game.Formations.ToDictionary(f => f.Id, EntropyValue);
            string entropyChanges = DescribeEntropyChanges(before.Entropy, afterEntropy, game.Time);
            int readyAfter = actor?.ReadyTime ?? -1;
            int commandAfter = actor == null ? -1 : game.Sides[actor.Side].CommandSlots;
            TrackActivation(actor?.Side);
            Record(game, "ActionResolved", actor, action, mode, targetId, legalAlternatives, decisionSeconds, calculation, outcome,
                new[] { before.Ready, readyAfter, before.Command, commandAfter }, contactChanges, entropyChanges);
        }

        public void RecordScore(PrototypeGame game, int blueScore, int redScore, string result)
        {
            Record(game, "ScenarioResult", null, "Score", null, null, "", 0, $"Blue {blueScore}; Red {redScore}", result, null);
        }

        public string Export(PlaytestFeedback feedback)
        {
            session.Feedback = feedback ?? new PlaytestFeedback();
            session.EndedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            string directory = Path.Combine(Application.persistentDataPath, "Playtests", session.SessionId);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "session.json"), SerializeJson());
            File.WriteAllText(Path.Combine(directory, "events.csv"), SerializeCsv());
            File.WriteAllText(Path.Combine(directory, "feedback.txt"), FeedbackText(session.Feedback));
            LastExportDirectory = directory;
            return directory;
        }

        private void Record(PrototypeGame game, string category, FormationState actor, string action, string mode, string targetId, string alternatives, float decisionSeconds, string calculation, string outcome, int[] state, string contactChanges = "", string entropyChanges = "")
        {
            if (session == null) StartNew(game);
            ObjectiveRanges(game, out int blueRange, out int redRange);
            session.Events.Add(new PlaytestEvent
            {
                Sequence = session.Events.Count + 1,
                RecordedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                SessionSeconds = Time.realtimeSinceStartup - sessionStarted,
                OperationalTime = game.Time,
                Category = category ?? string.Empty,
                Side = actor?.Side.ToString() ?? string.Empty,
                FormationId = actor?.Id ?? string.Empty,
                Action = action ?? string.Empty,
                Mode = mode ?? string.Empty,
                TargetId = targetId ?? string.Empty,
                LegalAlternatives = alternatives ?? string.Empty,
                DecisionSeconds = decisionSeconds,
                Calculation = calculation ?? string.Empty,
                Outcome = outcome ?? string.Empty,
                ReadyBefore = state != null ? state[0] : -1,
                ReadyAfter = state != null ? state[1] : -1,
                CommandBefore = state != null ? state[2] : -1,
                CommandAfter = state != null ? state[3] : -1,
                ContactChanges = contactChanges ?? string.Empty,
                EntropyChanges = entropyChanges ?? string.Empty,
                BlueObjectiveRange = blueRange,
                RedObjectiveRange = redRange,
                ConsecutiveSideActivations = consecutiveSideActivations
            });
        }

        private void TrackActivation(Side? actingSide)
        {
            if (!actingSide.HasValue) return;
            if (previousActingSide == actingSide) consecutiveSideActivations++;
            else { previousActingSide = actingSide; consecutiveSideActivations = 1; }
        }

        private string DescribeEntropyChanges(Dictionary<string, string> before, Dictionary<string, string> after, int currentTime)
        {
            var changes = new List<string>();
            foreach (string formationId in after.Keys.Union(before.Keys))
            {
                string oldValue = before.TryGetValue(formationId, out string oldState) ? oldState : "---";
                string newValue = after.TryGetValue(formationId, out string newState) ? newState : "---";
                if (oldValue == newValue) continue;
                string[] names = { "Friction", "Disruption", "Destruction" };
                for (int i = 0; i < 3; i++)
                {
                    char oldMark = oldValue.Length > i ? oldValue[i] : '-';
                    char newMark = newValue.Length > i ? newValue[i] : '-';
                    string key = formationId + ":" + names[i];
                    if (oldMark != '1' && newMark == '1')
                    {
                        entropyMarkedAt[key] = currentTime;
                        string cards = newValue.Length > 4 ? newValue.Substring(4) : string.Empty;
                        changes.Add($"{formationId} +{names[i]} at T{currentTime:00}{(string.IsNullOrEmpty(cards) ? string.Empty : " cards " + cards)}");
                    }
                    else if (oldMark == '1' && newMark != '1')
                    {
                        int duration = entropyMarkedAt.TryGetValue(key, out int markedAt) ? currentTime - markedAt : -1;
                        changes.Add($"{formationId} -{names[i]} duration {(duration >= 0 ? duration.ToString() : "unknown")}");
                        entropyMarkedAt.Remove(key);
                    }
                }
            }
            return string.Join(" | ", changes);
        }

        private static string DescribeChanges(Dictionary<string, string> before, Dictionary<string, string> after)
        {
            var changes = new List<string>();
            foreach (string key in before.Keys.Union(after.Keys))
            {
                string oldValue = before.TryGetValue(key, out string oldState) ? oldState : "none";
                string newValue = after.TryGetValue(key, out string newState) ? newState : "none";
                if (oldValue != newValue) changes.Add($"{key}: {oldValue} -> {newValue}");
            }
            return string.Join(" | ", changes);
        }

        private string ToCsv()
        {
            var csv = new StringBuilder();
            csv.AppendLine("Sequence,RecordedUtc,SessionSeconds,OperationalTime,Category,Side,FormationId,Action,Mode,TargetId,LegalAlternatives,DecisionSeconds,Calculation,Outcome,ReadyBefore,ReadyAfter,CommandBefore,CommandAfter,ContactChanges,EntropyChanges,BlueObjectiveRange,RedObjectiveRange,ConsecutiveSideActivations");
            foreach (PlaytestEvent e in session.Events)
            {
                string[] values = { e.Sequence.ToString(), e.RecordedUtc, e.SessionSeconds.ToString("0.000", CultureInfo.InvariantCulture), e.OperationalTime.ToString(), e.Category, e.Side, e.FormationId, e.Action, e.Mode, e.TargetId, e.LegalAlternatives, e.DecisionSeconds.ToString("0.000", CultureInfo.InvariantCulture), e.Calculation, e.Outcome, e.ReadyBefore.ToString(), e.ReadyAfter.ToString(), e.CommandBefore.ToString(), e.CommandAfter.ToString(), e.ContactChanges, e.EntropyChanges, e.BlueObjectiveRange.ToString(), e.RedObjectiveRange.ToString(), e.ConsecutiveSideActivations.ToString() };
                csv.AppendLine(string.Join(",", values.Select(Csv)));
            }
            return csv.ToString();
        }

        private static string FeedbackText(PlaytestFeedback f)
        {
            return $"Ready Time clarity (1-5): {f.ReadyTimeClarity}\nInformation impact (1-5): {f.InformationImpact}\nCombat fairness (1-5): {f.CombatFairness}\nEntropy interest (1-5): {f.EntropyInterest}\nObvious choice: {f.ObviousChoiceOccurred}\nRepeated lookup: {f.RepeatedRuleLookup}\nUnfair-feeling roll: {f.UnfairFeelingRoll}\nInactive-player lull: {f.InactivePlayerLull}\n\nBest decision:\n{f.BestDecision}\n\nMost confusing moment:\n{f.MostConfusingMoment}\n\nSuggested rules adjustment:\n{f.RulesAdjustment}\n";
        }

        private static string Csv(string value) => "\"" + (value ?? string.Empty).Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";
        private static string ContactKey(ContactState contact) => contact.Owner + ":" + contact.TargetId;
        private static string ContactValue(ContactState contact) => contact.IsLost ? "Lost" : $"{contact.Location}/{contact.Identity}/Age{contact.Age}@{contact.LastKnownPosition}";
        private static string EntropyValue(FormationState formation)
        {
            string cards = formation.ActiveEffectCardIds == null ? string.Empty : string.Join("+", formation.ActiveEffectCardIds);
            return $"{(formation.Friction ? '1' : '0')}{(formation.Disruption ? '1' : '0')}{(formation.Destruction ? '1' : '0')}:{cards}";
        }

        private static void ObjectiveRanges(PrototypeGame game, out int blueRange, out int redRange)
        {
            HexCoord objective = game.Area.Objective;
            blueRange = game.Formations.Where(f => f.Side == Side.Blue && !f.IsDestroyed).Select(f => HexCoord.Distance(f.Position, objective)).DefaultIfEmpty(99).Min();
            redRange = game.Formations.Where(f => f.Side == Side.Red && !f.IsDestroyed).Select(f => HexCoord.Distance(f.Position, objective)).DefaultIfEmpty(99).Min();
        }
    }
}
