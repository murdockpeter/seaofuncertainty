using System.Collections.Generic;
using SeaOfUncertainty.Core;
using UnityEngine;

namespace SeaOfUncertainty.Prototype
{
    public sealed class OperationalMissionAudio : MonoBehaviour
    {
        private readonly Dictionary<int, AudioClip[]> clips = new Dictionary<int, AudioClip[]>();
        private readonly Dictionary<int, int> nextVariants = new Dictionary<int, int>();
        private readonly AudioSource[] voices = new AudioSource[2];
        private AudioSource ambienceSource;
        private AudioClip ambienceClip;
        private float ambienceVolume;
        private float nextCueTime;

        private void Awake()
        {
            for (int i = 0; i < voices.Length; i++) voices[i] = CreateSource(false);
            ambienceSource = CreateSource(true);
            ambienceClip = CreateClip("Open sea - wind and water", OperationalAudioSynthesis.Ambience(), 2);
            ambienceSource.clip = ambienceClip;
            Configure(.8f, .7f, .35f);
            ambienceSource.Play();
        }

        private AudioSource CreateSource(bool loop)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            // Command-map audio is never positioned at an unseen enemy's location.
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.priority = loop ? 200 : 100;
            return source;
        }

        public void Configure(float master, float effects, float ambience)
        {
            AudioListener.volume = Mathf.Clamp01(master);
            foreach (AudioSource voice in voices)
                if (voice != null) voice.volume = Mathf.Clamp01(effects) * .4f;
            ambienceVolume = Mathf.Clamp01(ambience) * .4f;
            if (ambienceSource != null) ambienceSource.volume = ambienceVolume;
        }

        private void Update()
        {
            if (ambienceSource == null) return;
            bool active = false;
            foreach (AudioSource voice in voices) if (voice != null && voice.isPlaying) active = true;
            ambienceSource.volume = Mathf.MoveTowards(ambienceSource.volume, ambienceVolume * (active ? .6f : 1f), Time.unscaledDeltaTime * .3f);
        }

        public void Play(ActionKind mission, FormationKind formation = FormationKind.SurfaceGroup)
        {
            if (Time.unscaledTime < nextCueTime) return;
            AudioSource available = null;
            foreach (AudioSource voice in voices) if (voice != null && !voice.isPlaying) { available = voice; break; }
            // Bound overlapping cues without cutting a playing waveform mid-transient.
            if (available == null) return;
            FormationKind style = OperationalAudioSynthesis.Style(mission, formation);
            int key = (int)mission * 16 + (int)style;
            if (!clips.TryGetValue(key, out AudioClip[] variants))
            {
                variants = new AudioClip[3]; clips[key] = variants; nextVariants[key] = 0;
            }
            int index = nextVariants[key];
            if (variants[index] == null)
                variants[index] = CreateClip($"{mission} - {style} - {index + 1}", OperationalAudioSynthesis.Action(mission, style, index), 1);
            nextVariants[key] = (index + 1) % variants.Length;
            available.clip = variants[index]; available.Play();
            nextCueTime = Time.unscaledTime + .08f;
        }

        private static AudioClip CreateClip(string name, float[] samples, int channels)
        {
            AudioClip clip = AudioClip.Create(name, samples.Length / channels, channels, OperationalAudioSynthesis.SampleRate, false);
            clip.SetData(samples, 0); return clip;
        }

        private void OnDestroy()
        {
            foreach (AudioSource voice in voices) if (voice != null) voice.Stop();
            if (ambienceSource != null) ambienceSource.Stop();
            foreach (AudioClip[] variants in clips.Values)
                foreach (AudioClip clip in variants) if (clip != null) Destroy(clip);
            clips.Clear();
            if (ambienceClip != null) Destroy(ambienceClip);
        }

        public static string Description(ActionKind kind, FormationKind formation = FormationKind.SurfaceGroup)
        {
            switch (kind)
            {
                case ActionKind.Move:
                    switch (formation)
                    {
                        case FormationKind.AirGroup: return "Movement complete: turbine rush and airflow.";
                        case FormationKind.Submarine: return "Movement complete: muffled propulsion and low water wash.";
                        case FormationKind.BallisticMissileBattery: return "Movement complete: diesel rumble and running gear.";
                        default: return "Movement complete: marine engine, propeller rhythm and wake wash.";
                    }
                case ActionKind.Search: return formation == FormationKind.AirGroup || formation == FormationKind.BallisticMissileBattery
                    ? "Search complete: sensor-console relay and radio squelch; no detection information."
                    : "Search complete: damped sonar ping and fading echoes; no detection information.";
                case ActionKind.Strike: return "Strike resolved: weapon-launch rush and pressure thump; no hit confirmation.";
                case ActionKind.Recover: return "Recovery complete: equipment relays and a short motor spin-down.";
                case ActionKind.Hold: return "Hold complete: brief radio squelch and receiver click.";
                case ActionKind.Patrol: return "Patrol established: quiet watch ping and relay clicks.";
                case ActionKind.Support: return "Support established: paired radio bursts and key clicks.";
                case ActionKind.Replenish: return "Replenishment complete: winch, hydraulic hiss and coupling latch.";
                default: return "Operational audio cue.";
            }
        }
    }
}
