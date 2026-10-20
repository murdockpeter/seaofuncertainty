#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using SeaOfUncertainty.Core;
using SeaOfUncertainty.Prototype;
using UnityEngine;

namespace SeaOfUncertainty.Editor
{
    public static class OperationalAudioTests
    {
        public static void Run()
        {
            TestOutputListener();
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../TestResults/audio"));
            Directory.CreateDirectory(directory);
            var previews = new List<KeyValuePair<string, float[]>>();
            foreach (ActionKind action in Enum.GetValues(typeof(ActionKind)))
            {
                foreach (FormationKind formation in Enum.GetValues(typeof(FormationKind)))
                {
                    float[] first = null;
                    for (int variant = 0; variant < 3; variant++)
                    {
                        float[] data = OperationalAudioSynthesis.Action(action, formation, variant);
                        Validate(data, 1, action + "/" + formation + "/" + variant, false);
                        Check(Mathf.Abs(data[0]) < .00001f && Mathf.Abs(data[data.Length - 1]) < .00001f, "Cue boundaries fade to zero");
                        if (variant == 0) first = data;
                        else Check(!first.SequenceEqual(data), "Repeated cues have actual waveform variation");
                    }
                    if (formation == FormationKind.SurfaceGroup)
                    {
                        string name = action.ToString().ToLowerInvariant();
                        WriteWave(Path.Combine(directory, name + ".wav"), first, 1);
                        previews.Add(new KeyValuePair<string, float[]>(name, first));
                    }
                }
            }
            foreach (FormationKind kind in new[] { FormationKind.Submarine, FormationKind.AirGroup, FormationKind.BallisticMissileBattery })
            {
                float[] data = OperationalAudioSynthesis.Action(ActionKind.Move, kind);
                string name = "move-" + kind.ToString().ToLowerInvariant();
                WriteWave(Path.Combine(directory, name + ".wav"), data, 1);
                previews.Add(new KeyValuePair<string, float[]>(name, data));
            }
            float[] airSearch = OperationalAudioSynthesis.Action(ActionKind.Search, FormationKind.AirGroup);
            WriteWave(Path.Combine(directory, "search-air-console.wav"), airSearch, 1);
            previews.Add(new KeyValuePair<string, float[]>("search-air-console", airSearch));
            float[] sea = OperationalAudioSynthesis.Ambience();
            Validate(sea, 2, "Ambience", true);
            int frames = sea.Length / 2;
            double derivative = 0, cross = 0, energyL = 0, energyR = 0;
            for (int i = 2; i < sea.Length; i += 2)
            {
                derivative += Math.Pow(sea[i] - sea[i - 2], 2);
                cross += sea[i] * sea[i + 1]; energyL += sea[i] * sea[i]; energyR += sea[i + 1] * sea[i + 1];
            }
            double derivativeRms = Math.Sqrt(derivative / (frames - 1));
            Check(Math.Abs(sea[0] - sea[sea.Length - 2]) < derivativeRms * 6 && Math.Abs(sea[1] - sea[sea.Length - 1]) < derivativeRms * 6,
                "Stereo loop boundary has no exceptional discontinuity");
            Check(Math.Abs(cross / Math.Sqrt(energyL * energyR)) < .95, "Sea ambience has stereo width rather than duplicated mono");
            WriteWave(Path.Combine(directory, "open-sea-ambience.wav"), sea, 2);
            WriteShowcase(directory, sea, previews);
            TestRandomIsolation();
            TestPrivacy();
            TestChecks.ThrowIfAny("Operational audio");
            Debug.Log("Operational audio passed: 144 action/type/variant renders, bounded peaks/DC, stereo loop continuity, privacy and isolated randomness. WAV previews: " + directory);
        }

        private static void Validate(float[] data, int channels, string name, bool continuous)
        {
            double energy = 0, sum = 0;
            float peak = 0;
            bool finite = true;
            foreach (float value in data)
            {
                finite &= !float.IsNaN(value) && !float.IsInfinity(value);
                peak = Mathf.Max(peak, Mathf.Abs(value)); energy += value * value; sum += value;
            }
            double rms = Math.Sqrt(energy / data.Length);
            Check(finite && peak <= .78001f && rms > .01 && Math.Abs(sum / data.Length) < .003, name + " has finite, audible samples with headroom and negligible DC");
            if (continuous)
                for (int second = 0; second < OperationalAudioSynthesis.AmbienceSeconds; second++)
                {
                    double windowEnergy = 0;
                    for (int i = second * OperationalAudioSynthesis.SampleRate * channels; i < (second + 1) * OperationalAudioSynthesis.SampleRate * channels; i++) windowEnergy += data[i] * data[i];
                    Check(Math.Sqrt(windowEnergy / (OperationalAudioSynthesis.SampleRate * channels)) > .025, "Sea loop never fades into a silent gap");
                }
        }

        private static void TestRandomIsolation()
        {
            UnityEngine.Random.State old = UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(4200); float expected = UnityEngine.Random.value;
                UnityEngine.Random.InitState(4200);
                OperationalAudioSynthesis.Action(ActionKind.Strike, FormationKind.Submarine);
                Check(UnityEngine.Random.value == expected, "Audio generation leaves Unity random state unchanged");
            }
            finally { UnityEngine.Random.state = old; }
        }

        private static void TestOutputListener()
        {
            AudioListener[] existing = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            bool[] enabled = existing.Select(listener => listener.enabled).ToArray();
            var host = new GameObject("Audio output tests");
            var secondHost = new GameObject("Second audio output tests");
            try
            {
                foreach (AudioListener listener in existing) listener.enabled = false;
                AudioListener output = OperationalMissionAudio.EnsureOutputListener(host);
                Check(output != null && output.enabled && output.gameObject.activeInHierarchy, "Audio startup supplies an enabled listener when the scene has none");
                Check(OperationalMissionAudio.EnsureOutputListener(secondHost) == output && secondHost.GetComponent<AudioListener>() == null,
                    "Another audio host reuses the enabled listener without duplicating output");
                output.enabled = false;
                Check(OperationalMissionAudio.EnsureOutputListener(host) == output && output.enabled,
                    "A disabled host listener is reenabled rather than duplicated");
                Check(host.GetComponents<AudioListener>().Length == 1, "Repeated output initialization leaves one host listener");
                host.SetActive(false);
                AudioListener replacement = OperationalMissionAudio.EnsureOutputListener(secondHost);
                Check(replacement.gameObject == secondHost && replacement.enabled,
                    "An inactive scene listener cannot suppress active audio output");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(secondHost);
                for (int i = 0; i < existing.Length; i++) if (existing[i] != null) existing[i].enabled = enabled[i];
            }
        }

        private static void TestPrivacy()
        {
            bool background = Application.runInBackground;
            float volume = AudioListener.volume;
            var host = new GameObject("Audio privacy tests");
            try
            {
                var backend = host.AddComponent<SeaPrototypeController>(); backend.EditorEnsureInitializedForTests();
                backend.ToolkitSetOperationMode(OperationMode.SoloVsAi, Side.Blue);
                MethodInfo completed = typeof(SeaPrototypeController).GetMethod("OnMissionCompleted", BindingFlags.Instance | BindingFlags.NonPublic);
                string before = backend.LastAudioDescription;
                completed.Invoke(backend, new object[] { backend.Game.Formations.First(item => item.Side == Side.Red), ActionKind.Strike });
                Check(backend.LastAudioDescription == before, "Unseen OPFOR actions do not announce themselves through audio or descriptions");
                FormationState owned = backend.Game.Formations.First(item => item.Side == Side.Blue && item.Kind == FormationKind.Submarine);
                completed.Invoke(backend, new object[] { owned, ActionKind.Move });
                Check(backend.LastAudioDescription == OperationalMissionAudio.Description(ActionKind.Move, FormationKind.Submarine), "Owned formation kind chooses matching sound and accessibility text");
                Check(OperationalMissionAudio.Description(ActionKind.Strike).Contains("no hit confirmation"), "Weapon cue does not claim a hidden hit");
            }
            finally { UnityEngine.Object.DestroyImmediate(host); Application.runInBackground = background; AudioListener.volume = volume; }
        }

        private static void WriteShowcase(string directory, float[] sea, List<KeyValuePair<string, float[]>> previews)
        {
            int rate = OperationalAudioSynthesis.SampleRate;
            int length = rate * 3 + previews.Sum(item => item.Value.Length + rate / 2);
            var stereo = new float[length * 2];
            for (int i = 0; i < length; i++) { stereo[i * 2] = sea[(i * 2) % sea.Length] * .28f; stereo[i * 2 + 1] = sea[(i * 2 + 1) % sea.Length] * .28f; }
            var order = new List<string> { "0.00s - sea ambience", "All sounds are original procedural foley, not field recordings." };
            int cursor = rate * 3;
            foreach (var item in previews)
            {
                order.Add((cursor / (float)rate).ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "s - " + item.Key);
                for (int i = 0; i < item.Value.Length; i++) { stereo[(cursor + i) * 2] += item.Value[i] * .8f; stereo[(cursor + i) * 2 + 1] += item.Value[i] * .8f; }
                cursor += item.Value.Length + rate / 2;
            }
            WriteWave(Path.Combine(directory, "audio-showcase.wav"), stereo, 2);
            File.WriteAllLines(Path.Combine(directory, "showcase-timestamps.txt"), order);
        }

        private static void WriteWave(string path, float[] samples, int channels)
        {
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                int bytes = samples.Length * 2, rate = OperationalAudioSynthesis.SampleRate;
                writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + bytes); writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16); writer.Write((short)1); writer.Write((short)channels); writer.Write(rate); writer.Write(rate * channels * 2);
                writer.Write((short)(channels * 2)); writer.Write((short)16); writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(bytes);
                foreach (float sample in samples) writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(sample, -1f, 1f) * 32767f));
            }
        }
        private static void Check(bool condition, string label) => TestChecks.Check(condition, label);
    }
}
#endif
