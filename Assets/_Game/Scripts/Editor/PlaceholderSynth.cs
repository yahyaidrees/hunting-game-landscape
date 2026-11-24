using System;
using System.IO;
using Game.Core.Surfaces;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Makes the placeholder sounds (PH_*.wav) from shaped noise and tones: a rifle shot with its echo, bolt and
    /// trigger clicks, bullet impacts and footsteps per surface. They only stand in, so the game is never silent,
    /// until the generated sounds from plan section 14 replace them in their sound cues.
    /// </summary>
    static class Synth
    {
        public enum ImpactKind { Dirt, Rock, Wood, Water, Leaves }

        const int Rate = 44100;

        public static float[] RifleShot(int seed)
        {
            var rng = new System.Random(seed * 7919);
            var x = new float[(int)(Rate * 1.8f)];
            var crack = Noise(x.Length, rng); HighPass(crack, 0.75f); Env(crack, 0.0003f, 0.005f);
            var boom = Noise(x.Length, rng); LowPass(boom, 0.05f); Env(boom, 0.001f, 0.13f);
            var thump = Tone(x.Length, 58f + seed * 3f); Env(thump, 0.002f, 0.11f);
            Mix(x, crack, 0.7f); Mix(x, boom, 3.2f); Mix(x, thump, 0.7f);
            // echo off the far hillside, twice, duller each time
            var echo = (float[])boom.Clone(); LowPass(echo, 0.15f);
            Mix(x, echo, 1.1f, (int)(Rate * (0.33f + seed * 0.02f)));
            LowPass(echo, 0.3f);
            Mix(x, echo, 0.6f, (int)(Rate * (0.78f + seed * 0.03f)));
            return Normalize(x, 0.95f);
        }

        public static float[] Bolt(int seed)
        {
            var rng = new System.Random(seed * 131);
            var x = new float[(int)(Rate * 1.0f)];
            foreach (float at in new[] { 0f, 0.22f + seed * 0.01f, 0.48f, 0.62f })
                Mix(x, Click(rng, 2000f + (float)rng.NextDouble() * 900f, 0.015f), 1f, (int)(Rate * at));
            return Normalize(x, 0.8f);
        }

        public static float[] DryFire(int seed)
        {
            var rng = new System.Random(seed * 17);
            var x = new float[(int)(Rate * 0.3f)];
            Mix(x, Click(rng, 3200f, 0.01f), 1f);
            return Normalize(x, 0.7f);
        }

        public static float[] Reload(int seed)
        {
            var rng = new System.Random(seed * 997);
            var x = new float[(int)(Rate * 2.4f)];
            foreach (float at in new[] { 0.25f, 0.95f, 1.65f })
            {
                Mix(x, Click(rng, 1800f, 0.02f), 1f, (int)(Rate * at));
                Mix(x, Click(rng, 2600f, 0.012f), 0.7f, (int)(Rate * (at + 0.09f)));
            }
            return Normalize(x, 0.75f);
        }

        public static float[] Impact(int seed, ImpactKind kind)
        {
            var rng = new System.Random(seed * 31 + (int)kind * 1000);
            var x = new float[(int)(Rate * 0.6f)];
            switch (kind)
            {
                case ImpactKind.Dirt:
                {
                    var thud = Noise(x.Length, rng); LowPass(thud, 0.1f); Env(thud, 0.001f, 0.06f);
                    var spray = Noise(x.Length, rng); LowPass(spray, 0.35f); Env(spray, 0.01f, 0.16f);
                    Mix(x, thud, 2.5f); Mix(x, spray, 0.35f);
                    break;
                }
                case ImpactKind.Rock:
                {
                    var crack = Noise(x.Length, rng); HighPass(crack, 0.6f); Env(crack, 0.0002f, 0.008f);
                    var ring = Tone(x.Length, 2700f + seed * 150f); Env(ring, 0.0005f, 0.05f);
                    var ring2 = Tone(x.Length, 4300f); Env(ring2, 0.0005f, 0.03f);
                    Mix(x, crack, 1f); Mix(x, ring, 0.35f); Mix(x, ring2, 0.15f);
                    break;
                }
                case ImpactKind.Wood:
                {
                    var thud = Noise(x.Length, rng); LowPass(thud, 0.08f); Env(thud, 0.001f, 0.05f);
                    var knock = Tone(x.Length, 380f + seed * 30f); Env(knock, 0.001f, 0.06f);
                    var splinter = Noise(x.Length, rng); HighPass(splinter, 0.5f); Env(splinter, 0.002f, 0.035f);
                    Mix(x, thud, 2f); Mix(x, knock, 0.6f); Mix(x, splinter, 0.45f);
                    break;
                }
                case ImpactKind.Water:
                {
                    var splash = Noise(x.Length, rng); LowPass(splash, 0.25f); Env(splash, 0.003f, 0.17f);
                    var plop = Sweep(x.Length, 500f, 1300f, 0.09f); Env(plop, 0.002f, 0.07f);
                    Mix(x, splash, 1f); Mix(x, plop, 0.4f);
                    break;
                }
                default:
                {
                    var crackle = Crackle(x.Length, rng, 0.012f); Env(crackle, 0.001f, 0.12f);
                    var body = Noise(x.Length, rng); LowPass(body, 0.12f); Env(body, 0.001f, 0.05f);
                    Mix(x, crackle, 1f); Mix(x, body, 1.5f);
                    break;
                }
            }
            return Normalize(x, 0.9f);
        }

        public static float[] Step(int seed, SurfaceKind kind)
        {
            var rng = new System.Random(seed * 53 + (int)kind * 101);
            var x = new float[(int)(Rate * 0.4f)];
            float j = 0.85f + (float)rng.NextDouble() * 0.3f;   // each version a little different
            switch (kind)
            {
                case SurfaceKind.Grass:
                {
                    var body = Noise(x.Length, rng); LowPass(body, 0.14f * j); Env(body, 0.008f, 0.06f);
                    var swish = Noise(x.Length, rng); HighPass(swish, 0.3f); Env(swish, 0.02f, 0.05f);
                    Mix(x, body, 2f); Mix(x, swish, 0.25f);
                    break;
                }
                case SurfaceKind.DryLeaves:
                {
                    var crackle = Crackle(x.Length, rng, 0.02f); Env(crackle, 0.004f, 0.09f * j);
                    var body = Noise(x.Length, rng); LowPass(body, 0.15f); Env(body, 0.006f, 0.05f);
                    Mix(x, crackle, 1f); Mix(x, body, 1f);
                    break;
                }
                case SurfaceKind.Mud:
                {
                    var body = Noise(x.Length, rng); LowPass(body, 0.04f * j); Env(body, 0.01f, 0.09f);
                    for (int i = 0; i < body.Length; i++) body[i] *= 0.6f + 0.4f * Mathf.Sin(i / (float)Rate * Mathf.PI * 2f * 22f);
                    Mix(x, body, 3.5f);
                    break;
                }
                case SurfaceKind.Rock:
                {
                    var tap = Noise(x.Length, rng); HighPass(tap, 0.4f); Env(tap, 0.0004f, 0.012f);
                    var gravel = Crackle(x.Length, rng, 0.006f); Env(gravel, 0.002f, 0.05f * j);
                    Mix(x, tap, 1f); Mix(x, gravel, 0.5f);
                    break;
                }
                case SurfaceKind.ShallowWater:
                {
                    var splash = Noise(x.Length, rng); LowPass(splash, 0.3f); Env(splash, 0.01f, 0.2f * j);
                    var slosh = Noise(x.Length, rng); LowPass(slosh, 0.04f); Env(slosh, 0.005f, 0.12f);
                    Mix(x, splash, 0.8f); Mix(x, slosh, 2f);
                    break;
                }
                default:
                {
                    var knock = Tone(x.Length, 170f * j); Env(knock, 0.001f, 0.05f);
                    var body = Noise(x.Length, rng); LowPass(body, 0.15f); Env(body, 0.001f, 0.03f);
                    Mix(x, knock, 1f); Mix(x, body, 1.2f);
                    break;
                }
            }
            return Normalize(x, 0.8f);
        }

        // ---------- building blocks ----------

        static float[] Noise(int n, System.Random rng)
        {
            var x = new float[n];
            for (int i = 0; i < n; i++) x[i] = (float)(rng.NextDouble() * 2.0 - 1.0);
            return x;
        }

        static float[] Tone(int n, float hz)
        {
            var x = new float[n];
            for (int i = 0; i < n; i++) x[i] = Mathf.Sin(2f * Mathf.PI * hz * i / Rate);
            return x;
        }

        static float[] Sweep(int n, float fromHz, float toHz, float seconds)
        {
            var x = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = Mathf.Clamp01(i / (Rate * seconds));
                phase += 2f * Mathf.PI * Mathf.Lerp(fromHz, toHz, t) / Rate;
                x[i] = Mathf.Sin(phase);
            }
            return x;
        }

        // sparse random snaps: leaves and twigs, gravel
        static float[] Crackle(int n, System.Random rng, float density)
        {
            var x = new float[n];
            for (int i = 0; i < n; i++)
            {
                if (rng.NextDouble() > density) continue;
                float amp = (float)(rng.NextDouble() * 0.8 + 0.2) * (rng.NextDouble() < 0.5 ? -1f : 1f);
                int len = 20 + rng.Next(60);
                for (int k = 0; k < len && i + k < n; k++) x[i + k] += amp * Mathf.Exp(-k / 8f) * (float)(rng.NextDouble() * 2 - 1);
            }
            return x;
        }

        static float[] Click(System.Random rng, float ringHz, float decay)
        {
            int n = (int)(Rate * 0.08f);
            var snap = Noise(n, rng); HighPass(snap, 0.6f); Env(snap, 0.0002f, 0.004f);
            var ring = Tone(n, ringHz); Env(ring, 0.0003f, decay);
            Mix(snap, ring, 0.35f);
            return snap;
        }

        static void LowPass(float[] x, float a)
        {
            float y = 0f;
            for (int i = 0; i < x.Length; i++) { y += a * (x[i] - y); x[i] = y; }
        }

        static void HighPass(float[] x, float a)
        {
            float low = 0f;
            for (int i = 0; i < x.Length; i++) { low += (1f - a) * (x[i] - low); x[i] -= low; }
        }

        static void Env(float[] x, float attack, float decay)
        {
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)Rate;
                x[i] *= (1f - Mathf.Exp(-t / attack)) * Mathf.Exp(-t / decay);
            }
        }

        static void Mix(float[] dst, float[] src, float gain, int offset = 0)
        {
            for (int i = 0; i < src.Length && i + offset < dst.Length; i++) dst[i + offset] += src[i] * gain;
        }

        static float[] Normalize(float[] x, float peak)
        {
            float max = 1e-6f;
            foreach (float v in x) max = Mathf.Max(max, Mathf.Abs(v));
            for (int i = 0; i < x.Length; i++) x[i] *= peak / max;
            // a short fade at the end so nothing clicks
            int fade = Mathf.Min(x.Length, Rate / 100);
            for (int i = 0; i < fade; i++) x[x.Length - 1 - i] *= i / (float)fade;
            return x;
        }

        /// <summary>16-bit mono PCM .wav.</summary>
        public static byte[] Wav(float[] x) => Wav(x, Rate);

        public static byte[] Wav(float[] x, int rate)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            int bytes = x.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + bytes); w.Write(new[] { 'W', 'A', 'V', 'E' });
            w.Write(new[] { 'f', 'm', 't', ' ' }); w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(bytes);
            foreach (float v in x) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(v * 32767f), -32768, 32767));
            w.Flush();
            return ms.ToArray();
        }
    }
}
