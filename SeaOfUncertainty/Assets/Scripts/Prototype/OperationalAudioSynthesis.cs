using SeaOfUncertainty.Core;
using UnityEngine;

namespace SeaOfUncertainty.Prototype
{
    // Original procedural foley, driven only by the action and acting formation kind.
    // This local noise generator never touches simulation or UnityEngine.Random state.
    public static class OperationalAudioSynthesis
    {
        public const int SampleRate = 32000;
        public const int AmbienceSeconds = 20;
        private const float Tau = 2f * Mathf.PI;

        private struct Noise
        {
            private uint state;
            private float low, middle, high;
            public Noise(uint seed) { state = seed; low = middle = high = 0f; }
            public void Next(out float bass, out float body, out float air)
            {
                state = unchecked(state * 1664525u + 1013904223u);
                float white = (state >> 8) / 8388607.5f - 1f;
                low += .018f * (white - low);
                middle += .14f * (white - middle);
                high += .48f * (white - high);
                bass = low; body = middle - low; air = high - middle;
            }
        }

        public static FormationKind Style(ActionKind action, FormationKind formation)
            => action == ActionKind.Move ? formation : action == ActionKind.Search &&
                (formation == FormationKind.AirGroup || formation == FormationKind.BallisticMissileBattery)
                ? FormationKind.AirGroup : FormationKind.SurfaceGroup;

        public static float[] Action(ActionKind action, FormationKind formation, int variant = 0)
        {
            formation = Style(action, formation);
            float duration = action == ActionKind.Move ? 1.65f : action == ActionKind.Strike ? 1.8f : action == ActionKind.Replenish ? 1.7f
                : action == ActionKind.Search ? 1.45f : action == ActionKind.Patrol ? 1.1f : action == ActionKind.Hold ? .55f : .95f;
            int count = Mathf.CeilToInt(duration * SampleRate);
            var result = new float[count];
            var noise = new Noise(unchecked(0x5EA01978u + (uint)action * 7919u + (uint)formation * 1013u + (uint)variant * 104729u));
            float variation = 1f + (variant % 3 - 1) * .025f;
            float enginePhase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float progress = t / duration;
                noise.Next(out float bass, out float body, out float air);
                float sample;
                switch (action)
                {
                    case ActionKind.Move:
                        float rpm = (formation == FormationKind.BallisticMissileBattery ? 43f : 57f) * variation * (1f + .12f * Mathf.Sin(Mathf.PI * progress));
                        enginePhase += Tau * rpm / SampleRate;
                        float machinery = Mathf.Sin(enginePhase) + .28f * Mathf.Sin(2f * enginePhase + .3f) + .12f * Mathf.Sin(3f * enginePhase);
                        float blades = .76f + .24f * Mathf.Sin(Tau * 6.2f * variation * t);
                        if (formation == FormationKind.AirGroup)
                            sample = (body * 1.1f + air * .65f + bass * .6f + .035f * Mathf.Sin(enginePhase * 13f)) * Fade(t, duration, .22f, .45f);
                        else if (formation == FormationKind.Submarine)
                            sample = (bass * 1.6f + machinery * .1f * blades + body * .07f) * Fade(t, duration, .23f, .5f);
                        else if (formation == FormationKind.BallisticMissileBattery)
                            sample = (machinery * .12f * blades + bass + body * (.35f + .25f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Tau * 12f * t)), 4f))) * Fade(t, duration, .14f, .35f);
                        else sample = (machinery * .11f * blades + bass * 1.3f + body * (.32f + .12f * Mathf.Sin(Tau * 1.3f * t))) * Fade(t, duration, .2f, .42f);
                        break;
                    case ActionKind.Search:
                        sample = formation == FormationKind.AirGroup
                            ? Radio(t, body, air, 0f, .32f) + Click(t, body, .04f) + .35f * Radio(t, body, air, .64f, .2f)
                            : Ping(t, 1180f * variation, .08f) + .23f * Ping(t, 1180f * variation, .37f) + .09f * Ping(t, 1180f * variation, .73f) + body * .025f * Fade(t, duration, .05f, .5f);
                        break;
                    case ActionKind.Strike:
                        float local = Mathf.Max(0f, t - .055f);
                        float pressure = Mathf.Sin(Tau * (43f * local + 22f * (1f - Mathf.Exp(-local * 9f)) / 9f));
                        sample = Click(t, air, .015f) * .12f +
                            (bass * 1.8f + body * .8f + pressure * .16f) * Mathf.Exp(-local * 4.5f) * Smooth(local / .014f) +
                            (body * .7f + air * .38f) * Fade(t, duration, .13f, .9f) * Mathf.Exp(-t * 1.7f);
                        break;
                    case ActionKind.Recover:
                        sample = .6f * Click(t, air, .03f) + .4f * Click(t, body, .22f) + .7f * Click(t, air, .48f)
                            + (bass * .6f + .055f * Mathf.Sin(Tau * (110f * t - 18f * t * t))) * Fade(t, duration, .06f, .45f);
                        break;
                    case ActionKind.Hold:
                        sample = Radio(t, body, air, .04f, .18f) * .7f + .4f * Click(t, air, .035f) + .55f * Click(t, body, .29f);
                        break;
                    case ActionKind.Patrol:
                        sample = .44f * Ping(t, 890f * variation, .11f) + .12f * Ping(t, 890f * variation, .4f)
                            + .45f * Click(t, body, .65f) + .3f * Click(t, air, .81f);
                        break;
                    case ActionKind.Support:
                        sample = Radio(t, body, air, .035f, .22f) + .8f * Radio(t, body, air, .43f, .27f)
                            + .5f * Click(t, air, .02f) + .4f * Click(t, body, .76f);
                        break;
                    case ActionKind.Replenish:
                        float ratchet = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Tau * 13f * variation * t)), 10f);
                        sample = (bass * .85f + body * (.35f + ratchet) + .075f * Mathf.Sin(Tau * 74f * variation * t)) * Fade(t, duration, .14f, .65f)
                            + air * .18f * Fade(t - .75f, .65f, .06f, .3f) + .6f * Click(t, body, 1.38f);
                        break;
                    default: sample = 0f; break;
                }
                result[i] = sample;
            }
            if (action == ActionKind.Search || action == ActionKind.Strike)
                Reflections(result, action == ActionKind.Strike ? .12f : .055f);
            Normalize(result, 1, action == ActionKind.Strike ? .18f : action == ActionKind.Hold ? .09f : .14f, .012f);
            return result;
        }

        public static float[] Ambience()
        {
            int frames = AmbienceSeconds * SampleRate, overlap = 2 * SampleRate;
            var raw = new float[(frames + overlap) * 2];
            var left = new Noise(0x5EA0BEEFu);
            var right = new Noise(0x714ECAFEu);
            for (int i = 0; i < frames + overlap; i++)
            {
                float t = i / (float)SampleRate;
                float swell = .46f + .22f * Mathf.Sin(Tau * t / 10f) + .1f * Mathf.Sin(Tau * t / 4f + .7f);
                left.Next(out float lowL, out float bodyL, out float airL);
                right.Next(out float lowR, out float bodyR, out float airR);
                raw[i * 2] = lowL * .55f + (bodyL * .65f + airL * .12f) * swell;
                raw[i * 2 + 1] = lowR * .55f + (bodyR * .65f + airR * .12f) * (.92f * swell + .04f);
            }
            var result = new float[frames * 2];
            System.Array.Copy(raw, result, result.Length);
            // At the loop boundary adjacent samples follow the continuing noise
            // stream; an equal-power overlap avoids the old fade to silence.
            for (int i = 0; i < overlap; i++)
            {
                float theta = i / (float)(overlap - 1) * Mathf.PI * .5f;
                for (int channel = 0; channel < 2; channel++)
                    result[i * 2 + channel] = raw[(frames + i) * 2 + channel] * Mathf.Cos(theta) + raw[i * 2 + channel] * Mathf.Sin(theta);
            }
            Normalize(result, 2, .07f, 0f);
            return result;
        }

        private static float Smooth(float value) { value = Mathf.Clamp01(value); return value * value * (3f - 2f * value); }
        private static float Fade(float t, float duration, float attack, float release)
            => t < 0f || t > duration ? 0f : Smooth(t / attack) * Smooth((duration - t) / release);
        private static float Ping(float t, float frequency, float delay)
        {
            t -= delay;
            return t < 0f ? 0f : (Mathf.Sin(Tau * frequency * t) + .12f * Mathf.Sin(Tau * frequency * 1.48f * t)) * Mathf.Exp(-t * 10f) * Smooth(t / .004f) * .4f;
        }
        private static float Click(float t, float noise, float delay)
        {
            t -= delay;
            return t < 0f ? 0f : (noise + .17f * Mathf.Sin(Tau * 1680f * t)) * Mathf.Exp(-t * 95f) * Smooth(t / .0008f);
        }
        private static float Radio(float t, float body, float air, float delay, float duration)
            => (body * .8f + air * .24f) * Fade(t - delay, duration, .014f, .055f);
        private static void Reflections(float[] data, float wet)
        {
            int delay = (int)(SampleRate * .071f);
            // Feed-forward taps have no feedback accumulation or unbounded tail.
            for (int i = data.Length - 1; i >= delay; i--)
                data[i] += data[i - delay] * wet + (i >= delay * 2 ? data[i - delay * 2] * wet * .45f : 0f);
        }
        private static void Normalize(float[] data, int channels, float rms, float fade)
        {
            for (int channel = 0; channel < channels; channel++)
            {
                double sum = 0;
                for (int i = channel; i < data.Length; i += channels) sum += data[i];
                float mean = (float)(sum / (data.Length / channels));
                for (int i = channel; i < data.Length; i += channels) data[i] -= mean;
            }
            double energy = 0;
            float peak = 0f;
            int frames = data.Length / channels;
            for (int i = 0; i < data.Length; i++)
            {
                if (fade > 0f) data[i] *= Fade(i / channels / (float)SampleRate, (frames - 1f) / SampleRate, fade, fade);
                energy += data[i] * data[i]; peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            }
            float gain = Mathf.Min(8f, rms / Mathf.Max(.000001f, Mathf.Sqrt((float)(energy / data.Length))), .78f / Mathf.Max(.000001f, peak));
            for (int i = 0; i < data.Length; i++) data[i] *= gain;
        }
    }
}
