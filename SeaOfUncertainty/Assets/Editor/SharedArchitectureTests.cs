using System;
using System.IO;
using System.Linq;
using SeaOfUncertainty.Core;
using UnityEditor;
using UnityEngine;

namespace SeaOfUncertainty.Editor
{
    public static class SharedArchitectureTests
    {
        [MenuItem("Sea of Uncertainty/Run Shared Architecture Tests")]
        public static void Run()
        {
            var game = new PrototypeGame(8675309, ScenarioCatalog.Find("meridian-veil"));
            var document = new AuthoritativeReplayDocument();
            for (int index = 0; index < 45 && game.Time < game.Scenario.Horizon; index++)
            {
                AiDecision decision = PrototypeAiCommander.Choose(game, AiStrategyProfile.Adaptive);
                Assert(decision != null, "AI provides an authoritative replay fixture command");
                AuthoritativeCommand command = AuthoritativeCommand.FromAiDecision(game, decision);
                AuthoritativeEvent resolved = AuthoritativeReplay.Record(document, game, command);
                Assert(resolved.Accepted, $"Recorded command {index} is legal: {resolved.Outcome}");
                Assert(resolved.Sequence == index && command.Sequence == index, "Command and event sequences remain aligned");
                Assert(resolved.StateBefore != resolved.StateAfter, "Accepted action changes authoritative state");
            }
            Assert(document.Commands.Count >= 20, "Replay fixture spans a meaningful operational sequence");
            Assert(document.ScenarioId == "meridian-veil" && document.Seed == 8675309, "Replay header preserves scenario and random seed");

            string json = AuthoritativeReplay.ToJson(document);
            AuthoritativeReplayDocument roundTrip = AuthoritativeReplay.FromJson(json);
            AuthoritativeReplayResult replay = AuthoritativeReplay.Replay(roundTrip);
            Assert(replay.Success && replay.CommandsApplied == document.Commands.Count, replay.Message);
            Assert(AuthoritativeReplay.StateDigest(replay.Game) == document.Events.Last().StateAfter, "Replay reaches the recorded final state digest");

            AuthoritativeReplayDocument tampered = AuthoritativeReplay.FromJson(json);
            tampered.Commands[0].ActorId = "INVALID-FORMATION";
            AuthoritativeReplayResult rejected = AuthoritativeReplay.Replay(tampered);
            Assert(!rejected.Success && rejected.FailedSequence == 0 && rejected.Message.Contains("diverged"), "Replay detects a changed command before it can silently desynchronize");

            FormationState wrongSideActor = replay.Game.Active;
            var wrongSide = new AuthoritativeCommand { Kind = AuthoritativeCommandKind.Action, Side = wrongSideActor.Side == Side.Blue ? Side.Red : Side.Blue, ActorId = wrongSideActor.Id, Action = ActionKind.Hold };
            Assert(!AuthoritativeCommandProcessor.Execute(replay.Game, wrongSide, out string ownership) && ownership.Contains("does not own"), "Command validation rejects side/Formation ownership mismatches");

            PrintAndPlayGenerator.Generate();
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../generated/print-and-play"));
            string manifest = File.ReadAllText(Path.Combine(output, "manifest.json"));
            Assert(manifest.Contains("\"EntropyCardCount\": 36") && manifest.Contains("\"ResponseCardCount\": 24"), "Print manifest contains the complete structured decks");
            Assert(File.ReadAllText(Path.Combine(output, "formation-cards.html")).Contains("B-CV") && File.ReadAllText(Path.Combine(output, "formation-cards.html")).Contains("WEAPONS"), "Formation cards use scenario formation and weapon data");
            Assert(File.ReadAllText(Path.Combine(output, "counters-and-contacts.html")).Contains("FALSE CONTACT"), "Counter sheet includes formation and Contact markers");
            Assert(File.ReadAllText(Path.Combine(output, "tracks.html")).Contains("READY TIME") && File.ReadAllText(Path.Combine(output, "tracks.html")).Contains("ENTROPY"), "Operational tracks include Ready, Command, Endurance, weapon, and Entropy state");
            Assert(File.ReadAllText(Path.Combine(output, "scenario-aids.html")).Contains("Action Aid") && File.ReadAllText(Path.Combine(output, "scenario-aids.html")).Contains("Horizon"), "Scenario setup sheets and player aid derive from structured rules data");
            Assert(document.Commands.Any(item => item.Kind == AuthoritativeCommandKind.Action && item.Action == ActionKind.Search), "Replay fixture includes seeded Search resolution");
            Debug.Log($"Sea of Uncertainty shared architecture tests passed: {document.Commands.Count} commands, final digest {document.Events.Last().StateAfter}.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Shared architecture test failed: " + message);
        }
    }
}
