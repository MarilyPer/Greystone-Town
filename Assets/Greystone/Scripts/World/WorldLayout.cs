// =====================================================================================
//  WorldLayout.cs
//  ---------------------------------------------------------------------------------
//  Το «πολεοδομικό σχέδιο» του Greystone: ΜΙΑ στατική πηγή αλήθειας για τη γεωγραφία.
//
//  Όλα τα υπόλοιπα συστήματα (έδαφος, shader εδάφους, βλάστηση, κτίρια, δίκτυο δρόμων
//  για το A*, τοποθέτηση NPC) διαβάζουν από εδώ. Έτσι, αν μετακινηθεί ένας δρόμος, το
//  έδαφος ισοπεδώνεται, ο shader τον ζωγραφίζει, τα δέντρα τον αποφεύγουν και οι
//  χωρικοί περπατούν πάνω του — χωρίς ασυνέπειες ανάμεσα στα συστήματα.
//
//  Σύστημα συντεταγμένων (Unity): +X = Ανατολή, +Z = Βορράς, +Y = πάνω. 1 μονάδα = 1 m.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Greystone
{
    public static class WorldLayout
    {
        // ------------------------------------------------------------------ Διαστάσεις
        public const float Size = 480f;            // πλευρά εδάφους (m)
        public const float Half = Size * 0.5f;
        public const float Step = 2f;              // απόσταση κορυφών πλέγματος (m)
        public const int Verts = 241;              // Size / Step + 1
        public const float FlatRadius = 100f;      // ακτίνα της πεδιάδας του χωριού
        public const float WaterLevel = -1.2f;     // στάθμη ποταμού
        public const float RiverBed = -2.6f;       // πυθμένας ποταμού
        public const float RoadHalfWidth = 2.6f;
        public const float PlayBoundary = 228f;    // αόρατα όρια περιήγησης

        // ------------------------------------------------------------------ Σημεία ενδιαφέροντος
        public static readonly Vector2 PlazaCenter = Vector2.zero;
        public const float PlazaRadius = 16f;
        public static readonly Vector2 MillPos = new Vector2(-58f, 56f);
        public static readonly Vector3 Spawn = new Vector3(0f, 0f, -24f);

        /// <summary>Δρόμοι ως ευθύγραμμα τμήματα (x1, z1, x2, z2). Ίδια δεδομένα πάνε στον shader.</summary>
        public static readonly Vector4[] Roads =
        {
            new Vector4(-236f,   0f, 236f,    0f),   // κύριος δρόμος Δύσης–Ανατολής
            new Vector4(   0f,   0f,   0f, -236f),   // νότιος δρόμος προς τα χωράφια
            new Vector4(   0f,   0f,   0f,   31f),   // προς την εκκλησία
            new Vector4( -10f,  10f, -52f,   50f),   // μονοπάτι προς τον ανεμόμυλο
            new Vector4(   0f, -30f,  34f,  -30f),   // προς το μαντρί
        };

        /// <summary>Χωράφια (open-field system) ως ορθογώνια (xmin, zmin, πλάτος, βάθος).</summary>
        public static readonly Rect[] Fields =
        {
            new Rect(-72f, -94f, 58f, 46f),          // σιτάρι
            new Rect( 14f, -94f, 48f, 46f),          // κριθάρι
        };

        public static readonly Rect SheepPen = new Rect(34f, -38f, 20f, 14f);
        public static readonly Rect Orchard = new Rect(-86f, -44f, 30f, 18f);
        public static readonly Rect Churchyard = new Rect(-13f, 27f, 26f, 39f);

        /// <summary>Αποτυπώματα κτιρίων (καταχωρούνται από τον VillageBuilder) — εκεί δεν φυτρώνει τίποτα.</summary>
        public static readonly List<Rect> Footprints = new List<Rect>();

        // ------------------------------------------------------------------ Θόρυβος
        // Χρησιμοποιούμε Mathf.PerlinNoise (ντετερμινιστικό) με μετατοπίσεις «σπόρου».
        const float HillSeedX = 1400.37f, HillSeedZ = 912.11f;
        const float DetailSeedX = 311.9f, DetailSeedZ = 77.3f;

        static float[] _heightCache;   // γεμίζει από το TerrainBuilder για γρήγορες αναζητήσεις

        /// <summary>fBm (άθροισμα οκτάβων) Perlin, επιστρέφει περίπου -1..1.</summary>
        static float Fbm(float x, float z, float freq, int octaves, float sx, float sz)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += (Mathf.PerlinNoise(x * freq + sx, z * freq + sz) * 2f - 1f) * amp;
                norm += amp;
                amp *= 0.5f;
                freq *= 2.03f;
            }
            return sum / norm;
        }

        /// <summary>Κέντρο της κοίτης του ποταμού (x) για δεδομένο z — ελικοειδής διαδρομή.</summary>
        public static float RiverX(float z)
        {
            return 80f + 7f * Mathf.Sin(z * 0.025f) + 3f * Mathf.Sin(z * 0.07f + 1f);
        }

        /// <summary>Απόσταση σημείου από ευθύγραμμο τμήμα ab.</summary>
        public static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Vector2.Dot(ab, ab));
            return (p - (a + ab * t)).magnitude;
        }

        public static float DistanceToRoads(Vector2 p)
        {
            float d = float.MaxValue;
            for (int i = 0; i < Roads.Length; i++)
            {
                Vector4 r = Roads[i];
                d = Mathf.Min(d, SegmentDistance(p, new Vector2(r.x, r.y), new Vector2(r.z, r.w)));
            }
            return d;
        }

        /// <summary>
        /// Αναλυτική συνάρτηση ύψους εδάφους. Συνθέτει:
        ///  1. Δακτύλιο λόφων γύρω από την πεδιάδα (μάσκα ακτίνας) που σβήνει στα άκρα του χάρτη,
        ///  2. μικρές κυματώσεις στην πεδιάδα, 3. τον λοφίσκο του ανεμόμυλου (Gaussian),
        ///  4. ισοπέδωση δρόμων/πλατείας, 5. σκάψιμο της κοίτης του ποταμού.
        /// </summary>
        public static float ComputeHeight(float x, float z)
        {
            Vector2 p = new Vector2(x, z);
            float r = p.magnitude;
            float hills = Fbm(x, z, 0.0065f, 4, HillSeedX, HillSeedZ);        // -1..1
            float detail = Fbm(x, z, 0.05f, 2, DetailSeedX, DetailSeedZ);     // -1..1

            float hillMask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(FlatRadius, 185f, r));
            float edge = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
            float edgeFade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(200f, 238f, edge));

            float baseH = (14f + 26f * (hills * 0.5f + 0.5f)) * hillMask * edgeFade;
            float dm = Vector2.Distance(p, MillPos);
            baseH += 4.5f * Mathf.Exp(-dm * dm / (2f * 13f * 13f));

            float h = baseH + detail * Mathf.Lerp(0.45f, 3f, hillMask);

            // Ισοπέδωση δρόμων και πλατείας (μόνο στην πεδιάδα — στους λόφους ο δρόμος ακολουθεί το έδαφος)
            float roadM = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(RoadHalfWidth + 0.5f, RoadHalfWidth + 5f, DistanceToRoads(p)));
            float plazaM = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(PlazaRadius, PlazaRadius + 6f, Vector2.Distance(p, PlazaCenter)));
            float flatM = Mathf.Max(roadM, plazaM) * (1f - hillMask);
            h = Mathf.Lerp(h, baseH, flatM);
            h = Mathf.Max(h, -0.5f);   // κανένα βαθούλωμα δεν πέφτει κάτω από το νερό εκτός κοίτης

            // Κοίτη ποταμού: επίπεδος πυθμένας 5 m, ομαλές όχθες (πλατύτερη κοιλάδα στους λόφους)
            float dr = Mathf.Abs(x - RiverX(z));
            float bank = 13f + 22f * hillMask;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(5f, bank, dr));
            return Mathf.Lerp(RiverBed, h, t);
        }

        /// <summary>Αποθήκευση του πίνακα υψών που υπολογίστηκε για το έδαφος (Verts x Verts).</summary>
        public static void SetHeightCache(float[] heights) { _heightCache = heights; }

        /// <summary>
        /// Γρήγορη αναζήτηση ύψους με διγραμμική παρεμβολή στον αποθηκευμένο πίνακα.
        /// Είναι πολύ φθηνότερη από το ComputeHeight και συμφωνεί με το ορατό πλέγμα.
        /// </summary>
        public static float HeightAt(float x, float z)
        {
            if (_heightCache == null) return ComputeHeight(x, z);
            float fx = Mathf.Clamp((x + Half) / Step, 0f, Verts - 1.001f);
            float fz = Mathf.Clamp((z + Half) / Step, 0f, Verts - 1.001f);
            int ix = (int)fx, iz = (int)fz;
            float tx = fx - ix, tz = fz - iz;
            float h00 = _heightCache[iz * Verts + ix];
            float h10 = _heightCache[iz * Verts + ix + 1];
            float h01 = _heightCache[(iz + 1) * Verts + ix];
            float h11 = _heightCache[(iz + 1) * Verts + ix + 1];
            return Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), tz);
        }

        public static Vector3 OnGround(float x, float z, float yOffset = 0f)
        {
            return new Vector3(x, HeightAt(x, z) + yOffset, z);
        }

        // ------------------------------------------------------------------ Ερωτήματα περιοχών
        public static bool InField(Vector2 p, float margin = 0f)
        {
            for (int i = 0; i < Fields.Length; i++)
            {
                Rect f = Fields[i];
                if (p.x > f.xMin - margin && p.x < f.xMax + margin && p.y > f.yMin - margin && p.y < f.yMax + margin) return true;
            }
            return false;
        }

        public static bool InRect(Rect r, Vector2 p, float margin)
        {
            return p.x > r.xMin - margin && p.x < r.xMax + margin && p.y > r.yMin - margin && p.y < r.yMax + margin;
        }

        public static bool InFootprint(Vector2 p, float margin)
        {
            for (int i = 0; i < Footprints.Count; i++)
                if (InRect(Footprints[i], p, margin)) return true;
            return false;
        }

        public static bool NearRiver(Vector2 p, float margin)
        {
            return Mathf.Abs(p.x - RiverX(p.y)) < margin;
        }

        /// <summary>Είναι το σημείο «ελεύθερο» για βλάστηση; (όχι δρόμος, νερό, κτίριο, χωράφι, πλατεία)</summary>
        public static bool IsFreeGround(Vector2 p, float roadMargin, float riverMargin, bool allowFields)
        {
            if (DistanceToRoads(p) < RoadHalfWidth + roadMargin) return false;
            if (NearRiver(p, riverMargin)) return false;
            if (Vector2.Distance(p, PlazaCenter) < PlazaRadius + 1.5f) return false;
            if (!allowFields && InField(p, 1f)) return false;
            if (InRect(SheepPen, p, 1f)) return false;
            if (InRect(Churchyard, p, 0.5f)) return false;
            if (InFootprint(p, 1.2f)) return false;
            return true;
        }
    }
}
