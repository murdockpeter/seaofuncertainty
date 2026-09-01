using System;
using System.Collections.Generic;
using SeaOfUncertainty.Core;
using UnityEngine;

namespace SeaOfUncertainty.Prototype
{
    public sealed class OperationalMissionAudio : MonoBehaviour
    {
        private const int SampleRate = 44100;
        private readonly Dictionary<ActionKind, AudioClip> clips = new Dictionary<ActionKind, AudioClip>();
        private AudioSource source;

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.volume = .42f;
            foreach (ActionKind kind in Enum.GetValues(typeof(ActionKind))) clips[kind] = BuildClip(kind);
        }

        public void Play(ActionKind mission)
        {
            if (source == null || !clips.TryGetValue(mission, out AudioClip clip) || clip == null) return;
            source.PlayOneShot(clip);
        }

        private void OnDestroy()
        {
            foreach (AudioClip clip in clips.Values) if (clip != null) Destroy(clip);
            clips.Clear();
        }

        private static AudioClip BuildClip(ActionKind kind)
        {
            float duration = kind == ActionKind.Search || kind == ActionKind.Recover || kind == ActionKind.Replenish ? .62f : kind == ActionKind.Support ? .5f : .38f;
            int count = Mathf.CeilToInt(duration * SampleRate);
            var samples = new float[count];
            uint noise = 0x5EA01978u + (uint)kind * 7919u;
            for (int index = 0; index < count; index++)
            {
                float time = index / (float)SampleRate;
                float progress = index / (float)Mathf.Max(1, count - 1);
                float attack = Mathf.Clamp01(time / .018f);
                float release = Mathf.Clamp01((duration - time) / .11f);
                float envelope = attack * release;
                float sample;
                switch (kind)
                {
                    case ActionKind.Move:
                        sample = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(105f, 155f, progress) * time) * (.62f + .18f * Mathf.Sin(2f * Mathf.PI * 7f * time));
                        break;
                    case ActionKind.Search:
                        sample = Chirp(time, progress, 920f, 510f) + (time > .24f ? .34f * Chirp(time - .24f, progress, 760f, 430f) : 0f);
                        break;
                    case ActionKind.Strike:
                        noise = noise * 1664525u + 1013904223u;
                        float burst = ((noise >> 8) / 8388607.5f - 1f) * Mathf.Exp(-time * 18f);
                        sample = burst * .62f + Mathf.Sin(2f * Mathf.PI * 82f * time) * Mathf.Exp(-time * 8f);
                        break;
                    case ActionKind.Recover:
                        sample = DelayedBell(time, 330f, 0f) + DelayedBell(time, 440f, .18f) + DelayedBell(time, 660f, .38f);
                        break;
                    case ActionKind.Hold:
                        sample = Mathf.Sin(2f * Mathf.PI * 196f * time) * .72f + Mathf.Sin(2f * Mathf.PI * 294f * time) * .22f;
                        break;
                    case ActionKind.Patrol:
                        sample = Mathf.Sin(2f * Mathf.PI * (progress < .5f ? 280f : 350f) * time) * (.65f + .2f * Mathf.Sin(2f * Mathf.PI * 5f * time));
                        break;
                    case ActionKind.Support:
                        sample = DelayedBell(time, 523.25f, 0f) + DelayedBell(time, 659.25f, .2f);
                        break;
                    case ActionKind.Replenish:
                        sample = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(180f, 360f, progress) * time) * .72f + Mathf.Sin(2f * Mathf.PI * 90f * time) * .18f;
                        break;
                    default:
                        sample = 0f;
                        break;
                }
                samples[index] = Mathf.Clamp(sample * envelope * .42f, -.9f, .9f);
            }
            AudioClip clip = AudioClip.Create("Mission " + kind, count, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static float Chirp(float time, float progress, float high, float low)
            => Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(high, low, progress) * time) * Mathf.Exp(-time * 5.5f);

        private static float DelayedBell(float time, float frequency, float delay)
        {
            float local = time - delay;
            if (local < 0f) return 0f;
            return Mathf.Sin(2f * Mathf.PI * frequency * local) * Mathf.Exp(-local * 5.2f) * Mathf.Clamp01(local / .012f) * .52f;
        }
    }
}
