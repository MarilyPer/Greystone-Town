// =====================================================================================
//  Synth.cs — Διαδικαστική σύνθεση ήχου
//  ---------------------------------------------------------------------------------
//  Όλοι οι ήχοι του Greystone (καμπάνα, αμόνι, πρόβατα, πουλιά, ποτάμι, βροχή, φωτιά,
//  μουσική της λύρας…) ΣΥΝΤΙΘΕΝΤΑΙ με κώδικα κατά τη φόρτωση. Δεν υπάρχουν αρχεία ήχου.
//
//  Τεχνικές που χρησιμοποιούνται:
//   * Προσθετική σύνθεση (additive): άθροισμα ημιτονοειδών «μερικών» (partials) με
//     διαφορετική απόσβεση — π.χ. η καμπάνα έχει τα χαρακτηριστικά μερικά hum, prime,
//     tierce (έλασσονα τρίτη), quint, nominal (βλ. Wikipedia «Strike tone»/«Bell»).
//   * Αφαιρετική σύνθεση (subtractive): λευκός θόρυβος -> φίλτρα χαμηλοπερατά (one-pole)
//     για ποτάμι, άνεμο, βροχή, φωτιά.
//   * Φορμάντες: τα αρμονικά του βελάσματος ενισχύονται γύρω από συχνότητες φωνηέντων.
//   * Ομαλή επανάληψη (seamless loop): διασταυρούμενο σβήσιμο (crossfade) τέλους/αρχής.
//  Όλοι οι ήχοι είναι μονοφωνικοί 22.05 kHz· η χωρικότητα (3D) προκύπτει από το AudioSource.
// =====================================================================================
using System;
using UnityEngine;

namespace Greystone
{
    public static class Synth
    {
        public const int Rate = 22050;
        const float TwoPi = Mathf.PI * 2f;

        static System.Random _rng = new System.Random(1400);
        static float Rand() { return (float)_rng.NextDouble(); }
        static float Noise() { return (float)_rng.NextDouble() * 2f - 1f; }

        // ----------------------------------------------------------------- Βοηθητικά
        static float[] Buffer(float seconds) { return new float[(int)(seconds * Rate)]; }

        static void Normalize(float[] s, float peak)
        {
            float max = 0.0001f;
            for (int i = 0; i < s.Length; i++) max = Mathf.Max(max, Mathf.Abs(s[i]));
            float g = peak / max;
            for (int i = 0; i < s.Length; i++) s[i] *= g;
        }

        /// <summary>Φτιάχνει ομαλό loop: τα τελευταία fade δείγματα «λιώνουν» στην αρχή.</summary>
        static float[] MakeLoop(float[] s, float fadeSeconds)
        {
            int f = (int)(fadeSeconds * Rate);
            int n = s.Length - f;
            float[] o = new float[n];
            for (int i = 0; i < n; i++)
            {
                if (i < f)
                {
                    float t = i / (float)f;
                    o[i] = s[i] * t + s[n + i] * (1f - t);
                }
                else o[i] = s[i];
            }
            return o;
        }

        /// <summary>Χαμηλοπερατό φίλτρο πρώτης τάξης (one-pole) επί τόπου. a κοντά στο 1 = πιο «σκοτεινό».</summary>
        static void LowPass(float[] s, float a)
        {
            float y = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                y = y * a + s[i] * (1f - a);
                s[i] = y;
            }
        }

        static float Env(float t, float attack, float decayRate)
        {
            return (t < attack ? t / attack : 1f) * Mathf.Exp(-(t - attack) * decayRate);
        }

        public static AudioClip ToClip(string name, float[] samples)
        {
            AudioClip c = AudioClip.Create(name, samples.Length, 1, Rate, false);
            c.SetData(samples, 0);
            return c;
        }

        // ================================================================= Ήχοι
        /// <summary>Εκκλησιαστική καμπάνα (G3) — προσθετική σύνθεση με τα κλασικά μερικά.</summary>
        public static float[] Bell()
        {
            float f = 196f;
            float[] ratio = { 0.5f, 1f, 1.19f, 1.5f, 2f, 2.51f, 2.66f, 3.01f, 4.16f, 5.43f };
            float[] amp = { 0.55f, 0.7f, 0.45f, 0.25f, 0.85f, 0.25f, 0.2f, 0.18f, 0.12f, 0.07f };
            float[] dec = { 0.28f, 0.55f, 0.75f, 0.9f, 0.85f, 1.5f, 1.7f, 2.1f, 3.0f, 4.0f };
            float[] s = Buffer(5.5f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                float v = 0f;
                for (int k = 0; k < ratio.Length; k++)
                {
                    // Μικρό «διπλό» μερικό -> αργό διακρότημα (beating), χαρακτηριστικό των καμπανών.
                    float fr = f * ratio[k];
                    v += amp[k] * Mathf.Exp(-t * dec[k]) * (Mathf.Sin(TwoPi * fr * t) + 0.3f * Mathf.Sin(TwoPi * (fr + 0.7f) * t));
                }
                if (t < 0.01f) v += Noise() * (1f - t / 0.01f) * 0.5f;   // χτύπημα γλωσσιδιού
                s[i] = v * Mathf.Min(1f, t / 0.002f);
            }
            Normalize(s, 0.9f);
            return s;
        }

        /// <summary>Σφυρί σε αμόνι: ανάρμονα, γρήγορα αποσβενόμενα μεταλλικά μερικά.</summary>
        public static float[] Clang()
        {
            float[] fr = { 1180f, 2230f, 3310f, 4870f, 6150f };
            float[] dc = { 6f, 8f, 11f, 15f, 20f };
            float[] s = Buffer(0.8f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                float v = 0f;
                for (int k = 0; k < fr.Length; k++) v += Mathf.Sin(TwoPi * fr[k] * t) * Mathf.Exp(-t * dc[k]) / (k + 1);
                if (t < 0.006f) v += Noise() * 0.8f;
                s[i] = v;
            }
            Normalize(s, 0.8f);
            return s;
        }

        /// <summary>Τρίξιμο πόρτας: παλμοί «κολλάει-γλιστράει» (stick-slip) σε ξύλινο συντονισμό.</summary>
        public static float[] Creak(float seconds = 1.0f, float baseRate = 70f)
        {
            float[] s = Buffer(seconds);
            float phase = 0f;
            float res1 = 0f, res1v = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                float rate = baseRate + 45f * Mathf.Sin(TwoPi * 1.3f * t) + 15f * Mathf.Sin(TwoPi * 3.7f * t);
                phase += rate / Rate;
                float impulse = 0f;
                if (phase >= 1f) { phase -= 1f; impulse = 0.6f + Rand() * 0.4f; }
                // Απλός συντονιστής 2ης τάξης (μάζα-ελατήριο) στα ~850 Hz
                float w = TwoPi * 850f / Rate;
                res1v += -w * w * res1 - 0.02f * res1v + impulse;
                res1 += res1v;
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / seconds));
                s[i] = res1 * env;
            }
            Normalize(s, 0.6f);
            return s;
        }

        /// <summary>Χτύπημα σε ξύλινη πόρτα (3 χτυπήματα).</summary>
        public static float[] Knock()
        {
            float[] s = Buffer(0.9f);
            float[] at = { 0f, 0.22f, 0.44f };
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                float v = 0f;
                foreach (float a in at)
                {
                    float tt = t - a;
                    if (tt >= 0f) v += (Mathf.Sin(TwoPi * 110f * tt) + 0.5f * Mathf.Sin(TwoPi * 240f * tt) + Noise() * 0.3f) * Mathf.Exp(-tt * 35f);
                }
                s[i] = v;
            }
            Normalize(s, 0.7f);
            return s;
        }

        /// <summary>Γδούπος (μοχλός, μπάρα διοδίων, κουβάς).</summary>
        public static float[] Thud(float freq = 60f, float seconds = 0.5f)
        {
            float[] s = Buffer(seconds);
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                s[i] = (Mathf.Sin(TwoPi * freq * t * (1f - t * 0.4f)) + Noise() * 0.35f) * Mathf.Exp(-t * 12f);
            }
            LowPass(s, 0.6f);
            Normalize(s, 0.7f);
            return s;
        }

        /// <summary>Βέλασμα προβάτου: βίμπρατο + τρέμολο + φορμάντες φωνήεντος «ε/α».</summary>
        public static float[] Baa(float pitch = 1f)
        {
            float[] s = Buffer(0.95f);
            float phase = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                float f0 = 330f * pitch * (1f + 0.035f * Mathf.Sin(TwoPi * 6.5f * t)) * (1f - t * 0.08f);
                phase += f0 / Rate;
                float v = 0f;
                for (int k = 1; k <= 12; k++)
                {
                    float fk = f0 * k;
                    // Δύο φορμάντες (~750 Hz και ~1300 Hz) ενισχύουν τα αντίστοιχα αρμονικά
                    float form = Mathf.Exp(-Mathf.Pow((fk - 750f) / 260f, 2f)) + 0.7f * Mathf.Exp(-Mathf.Pow((fk - 1300f) / 300f, 2f)) + 0.05f;
                    v += Mathf.Sin(TwoPi * phase * k) * form;
                }
                float trem = 1f - 0.55f * (0.5f + 0.5f * Mathf.Sin(TwoPi * 13f * t));
                float env = Mathf.Clamp01(t / 0.06f) * Mathf.Clamp01((0.95f - t) / 0.3f);
                s[i] = v * trem * env + Noise() * 0.02f * env;
            }
            Normalize(s, 0.6f);
            return s;
        }

        static void AddChirp(float[] s, float start, float dur, float f0, float f1, float amp)
        {
            int a = (int)(start * Rate), n = (int)(dur * Rate);
            float ph = 0f;
            for (int i = 0; i < n && a + i < s.Length; i++)
            {
                float t = i / (float)n;
                float f = Mathf.Lerp(f0, f1, t) + 300f * Mathf.Sin(TwoPi * 40f * t * dur);
                ph += f / Rate;
                s[a + i] += Mathf.Sin(TwoPi * ph) * Mathf.Sin(Mathf.PI * t) * amp;
            }
        }

        /// <summary>Πρωινή ατμόσφαιρα: τιτιβίσματα πουλιών σε τυχαίες στιγμές + απαλός άνεμος (loop).</summary>
        public static float[] Birdsong()
        {
            float[] s = Buffer(12.5f);
            float t = 0.2f;
            while (t < 11.5f)
            {
                int notes = 2 + _rng.Next(5);
                float baseF = 2600f + Rand() * 2200f;
                float amp = 0.15f + Rand() * 0.25f;
                for (int k = 0; k < notes; k++)
                {
                    float d = 0.05f + Rand() * 0.1f;
                    AddChirp(s, t, d, baseF * (0.9f + Rand() * 0.3f), baseF * (0.8f + Rand() * 0.6f), amp);
                    t += d + 0.03f + Rand() * 0.05f;
                }
                t += 0.3f + Rand() * 1.4f;
            }
            float[] wind = Buffer(12.5f);
            for (int i = 0; i < wind.Length; i++) wind[i] = Noise();
            LowPass(wind, 0.985f);
            for (int i = 0; i < s.Length; i++) s[i] += wind[i] * 0.8f;
            Normalize(s, 0.5f);
            return MakeLoop(s, 0.5f);
        }

        /// <summary>Νυχτερινά τριζόνια: φέρουσα ~4.4 kHz σε ομάδες σύντομων παλμών.</summary>
        public static float[] Crickets()
        {
            float[] s = Buffer(6.5f);
            for (int c = 0; c < 3; c++)
            {
                float f = 4200f + c * 350f;
                float period = 0.45f + c * 0.13f;
                float offset = Rand() * period;
                for (int i = 0; i < s.Length; i++)
                {
                    float t = i / (float)Rate + offset;
                    float inPeriod = t % period;
                    float pulse = 0f;
                    for (int p = 0; p < 3; p++)
                    {
                        float pt = inPeriod - p * 0.035f;
                        if (pt > 0f && pt < 0.022f) pulse = Mathf.Sin(Mathf.PI * pt / 0.022f);
                    }
                    s[i] += Mathf.Sin(TwoPi * f * t) * pulse * (0.3f - c * 0.07f);
                }
            }
            Normalize(s, 0.35f);
            return MakeLoop(s, 0.5f);
        }

        /// <summary>Ροή ποταμού: φιλτραρισμένος θόρυβος + «γουργουρητά» (loop).</summary>
        public static float[] River()
        {
            float[] s = Buffer(6.5f);
            for (int i = 0; i < s.Length; i++) s[i] = Noise();
            LowPass(s, 0.82f);
            float[] low = (float[])s.Clone();
            LowPass(low, 0.97f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                float gurgle = 0.6f + 0.4f * Mathf.Sin(TwoPi * 0.7f * t + Mathf.Sin(TwoPi * 0.23f * t) * 3f);
                s[i] = s[i] * 0.6f * gurgle + low[i] * 3f;
            }
            Normalize(s, 0.6f);
            return MakeLoop(s, 0.5f);
        }

        /// <summary>Άνεμος: «καφέ» θόρυβος με αργή διακύμανση έντασης (ακέραιοι κύκλοι -> ομαλό loop).</summary>
        public static float[] Wind()
        {
            float[] s = Buffer(8.5f);
            float b = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                b = Mathf.Clamp(b + Noise() * 0.04f, -1f, 1f) * 0.998f;
                float t = i / (float)Rate;
                s[i] = b * (0.55f + 0.45f * Mathf.Sin(TwoPi * t / 8f) * Mathf.Sin(TwoPi * t / 2.67f));
            }
            LowPass(s, 0.7f);
            Normalize(s, 0.5f);
            return MakeLoop(s, 0.5f);
        }

        /// <summary>Βροχή: λευκός θόρυβος (υψηλές συχνότητες) + μεμονωμένες σταγόνες.</summary>
        public static float[] Rain()
        {
            float[] s = Buffer(4.5f);
            float prev = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float n = Noise();
                s[i] = (n - prev) * 0.35f;   // διαφόριση = υψιπερατό
                prev = n;
            }
            for (int d = 0; d < 180; d++)
            {
                int a = _rng.Next(s.Length - 800);
                float f = 1800f + Rand() * 2500f;
                for (int i = 0; i < 700; i++)
                {
                    float t = i / (float)Rate;
                    s[a + i] += Mathf.Sin(TwoPi * f * t) * Mathf.Exp(-t * 180f) * (0.1f + Rand() * 0.05f);
                }
            }
            Normalize(s, 0.5f);
            return MakeLoop(s, 0.5f);
        }

        /// <summary>Φωτιά: βαθύ βουητό + τριξίματα (σύντομοι απότομοι παλμοί).</summary>
        public static float[] Fire()
        {
            float[] s = Buffer(4.5f);
            for (int i = 0; i < s.Length; i++) s[i] = Noise();
            LowPass(s, 0.95f);
            for (int i = 0; i < s.Length; i++) s[i] *= 2.5f;
            for (int c = 0; c < 90; c++)
            {
                int a = _rng.Next(s.Length - 400);
                float amp = 0.2f + Rand() * 0.6f;
                for (int i = 0; i < 300; i++) s[a + i] += Noise() * amp * Mathf.Exp(-i / 40f);
            }
            Normalize(s, 0.55f);
            return MakeLoop(s, 0.4f);
        }

        /// <summary>Ξύλινος μηχανισμός ανεμόμυλου: ρυθμικό τρίξιμο + βουητό μυλόπετρας.</summary>
        public static float[] MillWorks()
        {
            float[] creak = Creak(4.4f, 40f);
            float[] s = Buffer(4.4f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                float rhythm = 0.3f + 0.7f * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(TwoPi * t / 1.1f), 3f);
                s[i] = creak[i] * rhythm * 0.6f + Mathf.Sin(TwoPi * 48f * t) * 0.15f + Noise() * 0.05f;
            }
            LowPass(s, 0.3f);
            Normalize(s, 0.5f);
            return MakeLoop(s, 0.3f);
        }

        /// <summary>Βήμα: σύντομη ριπή θορύβου με φίλτρο. soft=γρασίδι, αλλιώς πέτρα/ξύλο.</summary>
        public static float[] Step(float filter, float click)
        {
            float[] s = Buffer(0.16f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                s[i] = Noise() * Mathf.Exp(-t * 32f) + (t < 0.004f ? click * Noise() : 0f);
            }
            LowPass(s, filter);
            Normalize(s, 0.5f);
            return s;
        }

        /// <summary>Πλατσούρισμα νερού: θόρυβος + φυσαλίδες (γρήγορα ανερχόμενες ημιτονοειδείς).</summary>
        public static float[] Splash()
        {
            float[] s = Buffer(0.9f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                s[i] = Noise() * Mathf.Exp(-t * 7f) * 0.8f;
            }
            LowPass(s, 0.5f);
            for (int b = 0; b < 14; b++)
            {
                int a = _rng.Next((int)(0.5f * Rate));
                float f = 600f + Rand() * 900f;
                for (int i = 0; i < 1500 && a + i < s.Length; i++)
                {
                    float t = i / (float)Rate;
                    s[a + i] += Mathf.Sin(TwoPi * f * t * (1f + t * 6f)) * Mathf.Exp(-t * 40f) * 0.3f;
                }
            }
            Normalize(s, 0.6f);
            return s;
        }

        /// <summary>Φυσερό σιδηρουργείου: «ρουφηξιά» αέρα.</summary>
        public static float[] Bellows()
        {
            float[] s = Buffer(1.1f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                s[i] = Noise() * Mathf.Sin(Mathf.PI * t / 1.1f) * Mathf.Sin(Mathf.PI * t / 1.1f);
            }
            LowPass(s, 0.9f);
            Normalize(s, 0.6f);
            return s;
        }

        /// <summary>Καστάνια μανιβέλας: σειρά από «κλικ».</summary>
        public static float[] Ratchet(float seconds = 1.6f, float clicksPerSecond = 9f)
        {
            float[] s = Buffer(seconds);
            float period = 1f / clicksPerSecond;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                float tt = t % period;
                s[i] = (Noise() * 0.5f + Mathf.Sin(TwoPi * 2400f * tt)) * Mathf.Exp(-tt * 250f);
            }
            Normalize(s, 0.5f);
            return s;
        }

        /// <summary>Τρίξιμο/κροτάλισμα κάρου (loop): ξύλινοι κρότοι + βουητό τροχών.</summary>
        public static float[] Cart()
        {
            float[] s = Buffer(4.4f);
            for (int i = 0; i < s.Length; i++) s[i] = Noise() * 0.3f;
            LowPass(s, 0.97f);
            for (int c = 0; c < 40; c++)
            {
                int a = _rng.Next(s.Length - 900);
                float f = 300f + Rand() * 400f;
                for (int i = 0; i < 800; i++)
                {
                    float t = i / (float)Rate;
                    s[a + i] += Mathf.Sin(TwoPi * f * t) * Mathf.Exp(-t * 60f) * (0.3f + Rand() * 0.2f);
                }
            }
            Normalize(s, 0.5f);
            return MakeLoop(s, 0.3f);
        }

        /// <summary>Σύντομο «κλικ» διεπαφής.</summary>
        public static float[] UiClick()
        {
            float[] s = Buffer(0.06f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                s[i] = Mathf.Sin(TwoPi * 1400f * t) * Mathf.Exp(-t * 90f);
            }
            return s;
        }

        // ----------------------------------------------------------------- Μουσική
        /// <summary>
        /// Πρωτότυπη μελωδία σε δώριο τρόπο (Re) για λύρα με τροχό (hurdy-gurdy):
        /// μελωδία «πριονωτής» κυματομορφής με βίμπρατο, πάνω σε συνεχές μπουρδόνι Re–Λα.
        /// Κάθε νότα = (MIDI, διάρκεια σε όγδοα).
        /// </summary>
        public static float[] HurdyGurdyTune()
        {
            int[,] tune =
            {
                // Φράση Α
                {69,2},{69,1},{67,1},{65,2},{64,2}, {62,2},{64,1},{65,1},{67,4},
                {69,2},{72,2},{71,1},{69,1},{67,2}, {65,2},{64,2},{62,4},
                // Φράση Β
                {74,2},{72,1},{71,1},{69,2},{67,2}, {69,1},{71,1},{72,2},{69,4},
                {67,2},{65,1},{64,1},{65,2},{67,2}, {69,4},{69,4},
                // Φράση Α' (κατάληξη στη βάση Re)
                {69,2},{69,1},{67,1},{65,2},{64,2}, {62,2},{64,1},{65,1},{67,2},{65,2},
                {64,2},{62,1},{60,1},{62,2},{64,2}, {62,8},
            };
            const float eighth = 0.21f;
            int totalEighths = 0;
            for (int i = 0; i < tune.GetLength(0); i++) totalEighths += tune[i, 1];
            float total = totalEighths * eighth + 0.4f;
            float[] s = Buffer(total);

            // Χάρτης: σε ποια νότα βρισκόμαστε σε κάθε χρονική στιγμή
            float[] noteStart = new float[tune.GetLength(0) + 1];
            for (int i = 0; i < tune.GetLength(0); i++) noteStart[i + 1] = noteStart[i] + tune[i, 1] * eighth;

            float phM = 0f, phD1 = 0f, phD2 = 0f;
            float lpM = 0f, lpD = 0f;
            int note = 0;
            float freq = MidiToHz(tune[0, 0]);
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                while (note < tune.GetLength(0) - 1 && t >= noteStart[note + 1]) note++;
                float target = MidiToHz(tune[note, 0]);
                freq = Mathf.Lerp(freq, target, 0.02f);   // ελαφρύ glissando (portamento) ανάμεσα στις νότες
                float vib = 1f + 0.006f * Mathf.Sin(TwoPi * 5.5f * t);

                phM = (phM + freq * vib / Rate) % 1f;
                phD1 = (phD1 + 146.83f / Rate) % 1f;   // Re3
                phD2 = (phD2 + 220.0f / Rate) % 1f;    // Λα3

                float melody = phM * 2f - 1f;                          // πριονωτή
                float drone = (phD1 * 2f - 1f) * 0.5f + (phD2 * 2f - 1f) * 0.35f;
                // Ο τροχός τρίβει τις χορδές: ρυθμικό «buzz» (trompette) στους χρόνους
                float beat = (t % (eighth * 2f)) / (eighth * 2f);
                float buzz = beat < 0.12f ? (Noise() * 0.25f + 0.25f) : 0f;

                lpM = lpM * 0.55f + melody * 0.45f;
                lpD = lpD * 0.8f + drone * 0.2f;
                float env = Mathf.Clamp01(t / 0.3f) * Mathf.Clamp01((total - t) / 0.4f);
                s[i] = (lpM * 0.65f + lpD * 0.55f + buzz * lpD) * env;
            }
            Normalize(s, 0.75f);
            return s;
        }

        static float MidiToHz(int midi)
        {
            return 440f * Mathf.Pow(2f, (midi - 69) / 12f);
        }
    }
}
