// =====================================================================================
//  Vegetation.cs — Δάσος, θάμνοι, βράχια, γρασίδι, σιτάρι
//  ---------------------------------------------------------------------------------
//  ΤΟ ΚΥΡΙΟ ΠΑΡΑΔΕΙΓΜΑ ΒΕΛΤΙΣΤΟΠΟΙΗΣΗΣ ΤΟΥ PROJECT
//
//  Ο κόσμος έχει δεκάδες χιλιάδες φυτά. Αν το καθένα ήταν GameObject με MeshRenderer:
//    - θα είχαμε δεκάδες χιλιάδες draw calls και τεράστιο κόστος CPU (culling, transforms),
//    - η δημιουργία τους θα διαρκούσε πολύ και θα κατανάλωνε μνήμη.
//
//  Αντί γι' αυτό:
//   1. GPU INSTANCING: κάθε είδος φυτού έχει ΕΝΑ mesh και ΕΝΑ υλικό· αποθηκεύουμε μόνο πίνακες
//      μετασχηματισμού (Matrix4x4) και ζωγραφίζουμε έως 1023 στιγμιότυπα με ΜΙΑ κλήση
//      (Graphics.DrawMeshInstanced).
//   2. ΧΩΡΙΚΗ ΔΙΑΜΕΡΙΣΗ (chunks): τα στιγμιότυπα ομαδοποιούνται σε τετράγωνα κελιά. Κάθε
//      κελί έχει bounding box.
//   3. FRUSTUM CULLING ανά κελί: υπολογίζουμε τα 6 επίπεδα του κώνου όρασης της κάμερας
//      (GeometryUtility.CalculateFrustumPlanes) και απορρίπτουμε ολόκληρα κελιά εκτός οπτικού πεδίου.
//   4. LOD ΑΠΟΣΤΑΣΗΣ: κοντινά κελιά -> λεπτομερές mesh με σκιές· μακρινά -> απλό mesh χωρίς
//      σκιές· πέρα από τη μέγιστη απόσταση -> τίποτα (π.χ. το γρασίδι μόνο σε 45 m).
//   5. Colliders κορμών: απλά CapsuleColliders χωρίς renderer (η φυσική δεν χρειάζεται mesh).
//
//  Με F4 οι βελτιστοποιήσεις 3 και 4 απενεργοποιούνται για σύγκριση (overlay F3).
// =====================================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Greystone
{
    public class Vegetation : MonoBehaviour
    {
        public static Vegetation Instance { get; private set; }
        public static bool OptimizationsEnabled = true;

        // ------------------------------------------------------------------ Δομές δεδομένων
        /// <summary>Ένα «είδος» φυτού με δύο επίπεδα λεπτομέρειας.</summary>
        class Species
        {
            public string name;
            public Mesh near, far;           // far μπορεί να είναι null (π.χ. γρασίδι)
            public Material[] materials;     // ένα ανά submesh
            public float nearDistance;       // έως εδώ: λεπτομερές mesh
            public float maxDistance;        // πέρα από εδώ: καθόλου
            public bool nearShadows;
            public float chunkSize;
            public Dictionary<Vector2Int, Chunk> chunks = new Dictionary<Vector2Int, Chunk>();
        }

        class Chunk
        {
            public Bounds bounds;
            public List<Matrix4x4> list = new List<Matrix4x4>();
            public Matrix4x4[][] batches;    // έτοιμοι πίνακες έως 1023 στοιχείων
            public int[] counts;
        }

        readonly List<Species> _species = new List<Species>();
        readonly Plane[] _frustum = new Plane[6];
        Transform _colliderRoot;
        System.Random _rng = new System.Random(1400);

        // Στατιστικά για το overlay απόδοσης
        public static int InstancesDrawn { get; private set; }
        public static int BatchesDrawn { get; private set; }
        public static int TotalInstances { get; private set; }

        float Rand() { return (float)_rng.NextDouble(); }
        float Rand(float a, float b) { return a + (b - a) * (float)_rng.NextDouble(); }

        // ================================================================== Κατασκευή
        public IEnumerator Build()
        {
            Instance = this;
            _colliderRoot = Geo.Child(transform, "TreeColliders", Vector3.zero);

            Species oak = AddSpecies("Oak", BuildOak(true), BuildOak(false), new[] { Mats.Get("bark"), Mats.Get("leaves") }, 110f, 480f, true, 60f);
            Species pine = AddSpecies("Pine", BuildPine(true), BuildPine(false), new[] { Mats.Get("bark"), Mats.Get("conifer") }, 110f, 480f, true, 60f);
            Species apple = AddSpecies("AppleTree", BuildAppleTree(), null, new[] { Mats.Get("bark"), Mats.Get("orchard_leaves"), Mats.Get("apple") }, 140f, 140f, true, 40f);
            Species bush = AddSpecies("Bush", BuildBush(), null, new[] { Mats.Get("leaves") }, 130f, 130f, true, 40f);
            Species rock = AddSpecies("Rock", BuildRock(), null, new[] { Mats.Get("rock") }, 160f, 300f, true, 60f);
            Species grass = AddSpecies("Grass", BuildGrassTuft(), null, new[] { Mats.Get("grass") }, 45f, 45f, false, 24f);
            Species wheat = AddSpecies("Wheat", BuildWheat(1.0f), null, new[] { Mats.Get("wheat") }, 150f, 150f, false, 24f);
            Species barley = AddSpecies("Barley", BuildWheat(0.8f), null, new[] { Mats.Get("barley") }, 150f, 150f, false, 24f);
            yield return null;

            // ---------------- Δάσος στους λόφους ----------------
            int row = 0;
            for (float z = -WorldLayout.Half + 4f; z < WorldLayout.Half - 4f; z += 6.5f)
            {
                for (float x = -WorldLayout.Half + 4f; x < WorldLayout.Half - 4f; x += 6.5f)
                {
                    float px = x + Rand(-3f, 3f), pz = z + Rand(-3f, 3f);
                    Vector2 p = new Vector2(px, pz);
                    float r = p.magnitude;
                    // Πυκνότητα: σχεδόν μηδενική μέσα στο χωριό, αυξάνεται προς τους λόφους,
                    // διαμορφωμένη από θόρυβο (ξέφωτα και συστάδες).
                    float density = Mathf.InverseLerp(88f, 130f, r) * (Mathf.PerlinNoise(px * 0.012f + 50f, pz * 0.012f + 50f) * 1.6f - 0.15f);
                    if (Rand() > density) continue;
                    if (!WorldLayout.IsFreeGround(p, 3f, 14f, false)) continue;
                    float h = WorldLayout.HeightAt(px, pz);
                    bool conifer = Rand() < Mathf.InverseLerp(5f, 30f, h) * 0.85f + 0.1f;
                    float s = Rand(0.8f, 1.35f);
                    AddInstance(conifer ? pine : oak, new Vector3(px, h - 0.2f, pz), Rand(0f, 360f), new Vector3(s, s * Rand(0.9f, 1.15f), s));
                    AddTreeCollider(new Vector3(px, h, pz), conifer ? 0.35f * s : 0.4f * s, 5f);
                    if (Rand() < 0.25f)
                    {
                        Vector2 b = p + new Vector2(Rand(-4f, 4f), Rand(-4f, 4f));
                        if (WorldLayout.IsFreeGround(b, 2f, 12f, false))
                            AddInstance(bush, WorldLayout.OnGround(b.x, b.y, -0.1f), Rand(0, 360), Vector3.one * Rand(0.7f, 1.3f));
                    }
                }
                if (++row % 10 == 0) yield return null;   // μοίρασμα σε frames
            }

            // ---------------- Μεμονωμένα δέντρα μέσα στο χωριό ----------------
            Vector2[] villageTrees =
            {
                new Vector2(-22f, 26f), new Vector2(20f, 28f), new Vector2(-40f, 30f), new Vector2(32f, 22f),
                new Vector2(-20f, -32f), new Vector2(60f, -18f), new Vector2(66f, 20f), new Vector2(-75f, 18f),
                new Vector2(-8f, 60f), new Vector2(9f, 62f), new Vector2(26f, -46f), new Vector2(-30f, 44f),
            };
            foreach (Vector2 v in villageTrees)
            {
                if (!WorldLayout.IsFreeGround(v, 2f, 12f, false)) continue;
                bool conifer = v.y > 55f;   // «ίταμοι» (yews) στον περίβολο της εκκλησίας
                float s = Rand(1.0f, 1.3f);
                AddInstance(conifer ? pine : oak, WorldLayout.OnGround(v.x, v.y, -0.2f), Rand(0, 360), new Vector3(s, s, s));
                AddTreeCollider(WorldLayout.OnGround(v.x, v.y), 0.45f * s, 5f);
            }

            // ---------------- Οπωρώνας (μηλιές σε κάναβο) ----------------
            Rect o = WorldLayout.Orchard;
            for (float x = o.xMin + 3f; x < o.xMax - 1f; x += 6f)
                for (float z = o.yMin + 3f; z < o.yMax - 1f; z += 6f)
                {
                    float s = Rand(0.85f, 1.1f);
                    AddInstance(apple, WorldLayout.OnGround(x, z, -0.1f), Rand(0, 360), Vector3.one * s);
                    AddTreeCollider(WorldLayout.OnGround(x, z), 0.2f, 2.5f);
                }

            // ---------------- Φράχτες από θάμνους (hedgerows) γύρω από τα χωράφια ----------------
            foreach (Rect f in WorldLayout.Fields)
            {
                for (float x = f.xMin; x <= f.xMax; x += 2.2f)
                {
                    TryHedge(bush, new Vector2(x, f.yMin - 2.5f));
                }
                for (float z = f.yMin; z <= f.yMax; z += 2.2f)
                {
                    TryHedge(bush, new Vector2(f.xMin - 2.5f, z));
                    TryHedge(bush, new Vector2(f.xMax + 2.5f, z));
                }
            }
            yield return null;

            // ---------------- Βράχια ----------------
            for (int i = 0; i < 700; i++)
            {
                Vector2 p = new Vector2(Rand(-WorldLayout.Half + 5, WorldLayout.Half - 5), Rand(-WorldLayout.Half + 5, WorldLayout.Half - 5));
                if (p.magnitude < 70f && Rand() > 0.15f) continue;
                if (!WorldLayout.IsFreeGround(p, 2f, 8f, false)) continue;
                float s = Rand(0.4f, 1.8f);
                if (p.magnitude > 130f) s *= Rand(1f, 2.2f);
                AddInstance(rock, WorldLayout.OnGround(p.x, p.y, -0.25f * s), Rand(0, 360), new Vector3(s * Rand(0.8f, 1.4f), s * Rand(0.5f, 0.9f), s));
            }
            yield return null;

            // ---------------- Γρασίδι γύρω από το χωριό ----------------
            for (int i = 0; i < 42000; i++)
            {
                float a = Rand(0f, Mathf.PI * 2f);
                float r = Mathf.Sqrt(Rand()) * 118f;       // ομοιόμορφη κατανομή σε δίσκο
                Vector2 p = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
                if (!WorldLayout.IsFreeGround(p, 0.6f, 9f, false)) continue;
                // Λιγότερο γρασίδι όπου ο θόρυβος είναι χαμηλός (φυσικά «μπαλώματα»)
                if (Mathf.PerlinNoise(p.x * 0.08f, p.y * 0.08f) < 0.35f) continue;
                float s = Rand(0.7f, 1.3f);
                AddInstance(grass, WorldLayout.OnGround(p.x, p.y, -0.03f), Rand(0, 360), new Vector3(s, s * Rand(0.8f, 1.3f), s));
                if (i % 6000 == 0) yield return null;
            }

            // ---------------- Σιτάρι και κριθάρι ----------------
            for (int fi = 0; fi < WorldLayout.Fields.Length; fi++)
            {
                Rect f = WorldLayout.Fields[fi];
                Species crop = fi == 0 ? wheat : barley;
                for (float x = f.xMin + 0.8f; x < f.xMax - 0.5f; x += 0.62f)
                {
                    // Κάθε 6η λωρίδα (open-field strips) αφήνεται ένα στενό αυλάκι-μονοπάτι.
                    float strip = Mathf.Repeat(x - f.xMin, 6f);
                    if (strip < 0.5f) continue;
                    for (float z = f.yMin + 0.8f; z < f.yMax - 0.5f; z += 0.62f)
                    {
                        float px = x + Rand(-0.2f, 0.2f), pz = z + Rand(-0.2f, 0.2f);
                        if (Vector2.Distance(new Vector2(px, pz), new Vector2(-40f, -70f)) < 2.5f) continue;   // σκιάχτρο
                        float s = Rand(0.85f, 1.15f);
                        AddInstance(crop, WorldLayout.OnGround(px, pz, -0.02f), Rand(0, 360), new Vector3(s, s * Rand(0.9f, 1.1f), s));
                    }
                }
                yield return null;
            }

            FinalizeChunks();
        }

        void TryHedge(Species bush, Vector2 p)
        {
            if (!WorldLayout.IsFreeGround(p, 1.5f, 10f, true)) return;
            if (WorldLayout.InField(p, 0.5f)) return;
            AddInstance(bush, WorldLayout.OnGround(p.x, p.y, -0.15f), Rand(0, 360), new Vector3(Rand(0.8f, 1.2f), Rand(0.7f, 1.0f), Rand(0.8f, 1.2f)));
        }

        Species AddSpecies(string name, Mesh near, Mesh far, Material[] mats, float nearD, float maxD, bool shadows, float chunk)
        {
            Species s = new Species { name = name, near = near, far = far, materials = mats, nearDistance = nearD, maxDistance = maxD, nearShadows = shadows, chunkSize = chunk };
            _species.Add(s);
            return s;
        }

        void AddInstance(Species s, Vector3 pos, float yaw, Vector3 scale)
        {
            Vector2Int key = new Vector2Int(Mathf.FloorToInt(pos.x / s.chunkSize), Mathf.FloorToInt(pos.z / s.chunkSize));
            Chunk c;
            if (!s.chunks.TryGetValue(key, out c))
            {
                c = new Chunk();
                c.bounds = new Bounds(pos, Vector3.zero);
                s.chunks[key] = c;
            }
            c.list.Add(Matrix4x4.TRS(pos, Quaternion.Euler(0f, yaw, 0f), scale));
            // Το bounding box του κελιού περιλαμβάνει και το μέγεθος του φυτού
            Bounds mb = s.near.bounds;
            c.bounds.Encapsulate(pos + Vector3.Scale(mb.max, scale) + Vector3.one);
            c.bounds.Encapsulate(pos + Vector3.Scale(mb.min, scale) - Vector3.one);
        }

        void AddTreeCollider(Vector3 basePos, float radius, float height)
        {
            GameObject go = new GameObject("TreeCol");
            go.transform.SetParent(_colliderRoot, false);
            go.transform.position = basePos + Vector3.up * height * 0.5f;
            CapsuleCollider cc = go.AddComponent<CapsuleCollider>();
            cc.radius = radius;
            cc.height = height;
            go.isStatic = true;
        }

        /// <summary>Προετοιμάζει έτοιμους πίνακες των 1023 (όριο του DrawMeshInstanced).</summary>
        void FinalizeChunks()
        {
            int total = 0;
            foreach (Species s in _species)
            {
                foreach (Chunk c in s.chunks.Values)
                {
                    int n = c.list.Count;
                    total += n;
                    int batchCount = (n + 1022) / 1023;
                    c.batches = new Matrix4x4[batchCount][];
                    c.counts = new int[batchCount];
                    for (int b = 0; b < batchCount; b++)
                    {
                        int cnt = Mathf.Min(1023, n - b * 1023);
                        c.batches[b] = c.list.GetRange(b * 1023, cnt).ToArray();
                        c.counts[b] = cnt;
                    }
                    c.list = null;   // αποδέσμευση μνήμης
                }
            }
            TotalInstances = total;
        }

        // ================================================================== Σχεδίαση ανά frame
        void Update()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            GeometryUtility.CalculateFrustumPlanes(cam, _frustum);
            Vector3 cp = cam.transform.position;
            int inst = 0, batches = 0;

            for (int si = 0; si < _species.Count; si++)
            {
                Species s = _species[si];
                float near2 = s.nearDistance * s.nearDistance;
                float max2 = s.maxDistance * s.maxDistance;
                foreach (Chunk c in s.chunks.Values)
                {
                    if (c.batches == null) continue;
                    Mesh mesh = s.near;
                    bool shadows = s.nearShadows;
                    if (OptimizationsEnabled)
                    {
                        // 1) Frustum culling ολόκληρου κελιού
                        if (!GeometryUtility.TestPlanesAABB(_frustum, c.bounds)) continue;
                        // 2) LOD με βάση την απόσταση από το πλησιέστερο σημείο του κελιού
                        float d2 = c.bounds.SqrDistance(cp);
                        if (d2 > max2) continue;
                        if (d2 > near2)
                        {
                            if (s.far == null) continue;
                            mesh = s.far;
                            shadows = false;
                        }
                    }
                    ShadowCastingMode sc = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    for (int b = 0; b < c.batches.Length; b++)
                    {
                        for (int sub = 0; sub < mesh.subMeshCount && sub < s.materials.Length; sub++)
                        {
                            Graphics.DrawMeshInstanced(mesh, sub, s.materials[sub], c.batches[b], c.counts[b], null, sc, true, 0, null, LightProbeUsage.Off);
                            batches++;
                        }
                        inst += c.counts[b];
                    }
                }
            }
            InstancesDrawn = inst;
            BatchesDrawn = batches;
        }

        // ================================================================== Meshes φυτών
        Mesh BuildOak(bool detailed)
        {
            List<CombineInstance> trunk = new List<CombineInstance>();
            List<CombineInstance> leaves = new List<CombineInstance>();
            if (detailed)
            {
                trunk.Add(Geo.CI(Geo.Cylinder(0.6f, 8), new Vector3(0, 2.2f, 0), new Vector3(0.75f, 4.4f, 0.75f)));
                trunk.Add(Geo.CI(Geo.Cylinder(0.4f, 6), new Vector3(0.7f, 4.4f, 0.2f), new Vector3(0.3f, 2.2f, 0.3f), new Vector3(0, 0, -35)));
                trunk.Add(Geo.CI(Geo.Cylinder(0.4f, 6), new Vector3(-0.6f, 4.2f, -0.3f), new Vector3(0.3f, 2f, 0.3f), new Vector3(20, 0, 35)));
                Mesh sp = Geo.Sphere(10, 7);
                leaves.Add(Geo.CI(sp, new Vector3(0, 5.8f, 0), new Vector3(5.4f, 4.2f, 5.4f)));
                leaves.Add(Geo.CI(sp, new Vector3(1.6f, 6.4f, 0.8f), new Vector3(3.6f, 3.2f, 3.6f)));
                leaves.Add(Geo.CI(sp, new Vector3(-1.5f, 6.1f, -0.9f), new Vector3(3.8f, 3.3f, 3.8f)));
                leaves.Add(Geo.CI(sp, new Vector3(0.3f, 7.6f, -0.3f), new Vector3(3.2f, 2.8f, 3.2f)));
                leaves.Add(Geo.CI(sp, new Vector3(-0.4f, 6.8f, 1.7f), new Vector3(3.0f, 2.6f, 3.0f)));
            }
            else
            {
                trunk.Add(Geo.CI(Geo.Cylinder(0.6f, 5), new Vector3(0, 2.2f, 0), new Vector3(0.75f, 4.4f, 0.75f)));
                leaves.Add(Geo.CI(Geo.Sphere(6, 4), new Vector3(0, 6.2f, 0), new Vector3(6.2f, 5f, 6.2f)));
            }
            return Geo.Combine(detailed ? "OakNear" : "OakFar", trunk, leaves);
        }

        Mesh BuildPine(bool detailed)
        {
            List<CombineInstance> trunk = new List<CombineInstance>();
            List<CombineInstance> leaves = new List<CombineInstance>();
            trunk.Add(Geo.CI(Geo.Cylinder(0.5f, detailed ? 7 : 4), new Vector3(0, 1.8f, 0), new Vector3(0.5f, 3.6f, 0.5f)));
            if (detailed)
            {
                Mesh cone = Geo.Cylinder(0f, 10);
                leaves.Add(Geo.CI(cone, new Vector3(0, 4.2f, 0), new Vector3(4.6f, 4.4f, 4.6f)));
                leaves.Add(Geo.CI(cone, new Vector3(0, 6.6f, 0), new Vector3(3.7f, 4.0f, 3.7f)));
                leaves.Add(Geo.CI(cone, new Vector3(0, 8.9f, 0), new Vector3(2.7f, 3.6f, 2.7f)));
                leaves.Add(Geo.CI(cone, new Vector3(0, 10.8f, 0), new Vector3(1.6f, 2.8f, 1.6f)));
            }
            else
            {
                leaves.Add(Geo.CI(Geo.Cylinder(0f, 5), new Vector3(0, 7f, 0), new Vector3(4.6f, 10f, 4.6f)));
            }
            return Geo.Combine(detailed ? "PineNear" : "PineFar", trunk, leaves);
        }

        Mesh BuildAppleTree()
        {
            List<CombineInstance> trunk = new List<CombineInstance>();
            List<CombineInstance> leaves = new List<CombineInstance>();
            List<CombineInstance> apples = new List<CombineInstance>();
            trunk.Add(Geo.CI(Geo.Cylinder(0.6f, 6), new Vector3(0, 0.9f, 0), new Vector3(0.28f, 1.8f, 0.28f)));
            Mesh sp = Geo.Sphere(9, 6);
            leaves.Add(Geo.CI(sp, new Vector3(0, 2.6f, 0), new Vector3(3.2f, 2.4f, 3.2f)));
            leaves.Add(Geo.CI(sp, new Vector3(0.8f, 2.9f, 0.4f), new Vector3(2f, 1.8f, 2f)));
            System.Random r = new System.Random(7);
            Mesh small = Geo.Sphere(6, 4);
            for (int i = 0; i < 14; i++)
            {
                float a = (float)r.NextDouble() * Mathf.PI * 2f;
                float y = 1.8f + (float)r.NextDouble() * 1.4f;
                float rad = 1.45f - Mathf.Abs(y - 2.6f) * 0.4f;
                apples.Add(Geo.CI(small, new Vector3(Mathf.Cos(a) * rad, y, Mathf.Sin(a) * rad), Vector3.one * 0.12f));
            }
            return Geo.Combine("AppleTree", trunk, leaves, apples);
        }

        Mesh BuildBush()
        {
            List<CombineInstance> l = new List<CombineInstance>();
            Mesh sp = Geo.Sphere(8, 5);
            l.Add(Geo.CI(sp, new Vector3(0, 0.55f, 0), new Vector3(1.8f, 1.2f, 1.6f)));
            l.Add(Geo.CI(sp, new Vector3(0.6f, 0.45f, 0.3f), new Vector3(1.2f, 0.9f, 1.2f)));
            l.Add(Geo.CI(sp, new Vector3(-0.5f, 0.4f, -0.4f), new Vector3(1.3f, 0.9f, 1.1f)));
            return Geo.Combine("Bush", l);
        }

        /// <summary>Βράχος: σφαίρα με «θορυβώδη» μετατόπιση κορυφών (ίδια μετατόπιση για ίδιες θέσεις -> χωρίς ρωγμές).</summary>
        Mesh BuildRock()
        {
            Mesh src = Geo.Sphere(9, 6);
            Vector3[] v = src.vertices;
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 p = v[i];
                float n = Mathf.PerlinNoise(p.x * 3f + 10f, p.z * 3f + p.y * 2f + 10f);
                p *= 0.75f + n * 0.55f;
                if (p.y < -0.1f) p.y = -0.1f;   // επίπεδη βάση
                v[i] = p;
            }
            Mesh m = new Mesh();
            m.name = "Rock";
            m.vertices = v;
            m.triangles = src.triangles;
            m.uv = src.uv;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Τούφα γρασιδιού: 7 λεπίδες-τρίγωνα με τυχαία κλίση και στροφή.</summary>
        Mesh BuildGrassTuft()
        {
            List<Vector3> v = new List<Vector3>();
            List<Vector3> n = new List<Vector3>();
            List<int> t = new List<int>();
            System.Random r = new System.Random(3);
            for (int b = 0; b < 7; b++)
            {
                float a = (float)r.NextDouble() * Mathf.PI * 2f;
                float h = 0.3f + (float)r.NextDouble() * 0.22f;
                float w = 0.05f + (float)r.NextDouble() * 0.03f;
                Vector3 off = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (float)r.NextDouble() * 0.12f;
                Vector3 side = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a)) * w;
                Vector3 lean = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (0.05f + (float)r.NextDouble() * 0.1f);
                int s = v.Count;
                v.Add(off - side); v.Add(off + side); v.Add(off + lean + Vector3.up * h);
                n.Add(Vector3.up); n.Add(Vector3.up); n.Add(Vector3.up);
                t.Add(s); t.Add(s + 1); t.Add(s + 2);
            }
            Mesh m = new Mesh();
            m.name = "GrassTuft";
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Θύσανος σιταριού/κριθαριού: 8 λεπτά στελέχη με πιο φαρδύ στάχυ στην κορυφή.</summary>
        Mesh BuildWheat(float height)
        {
            List<Vector3> v = new List<Vector3>();
            List<Vector3> n = new List<Vector3>();
            List<int> t = new List<int>();
            System.Random r = new System.Random(5);
            for (int k = 0; k < 8; k++)
            {
                float a = (float)r.NextDouble() * Mathf.PI;
                Vector3 side = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                Vector3 off = new Vector3((float)r.NextDouble() - 0.5f, 0, (float)r.NextDouble() - 0.5f) * 0.3f;
                float h = height * (0.85f + (float)r.NextDouble() * 0.25f);
                Vector3 lean = new Vector3((float)r.NextDouble() - 0.5f, 0, (float)r.NextDouble() - 0.5f) * 0.12f;
                // Στέλεχος
                AddQuadStrip(v, n, t, off, off + lean + Vector3.up * (h - 0.16f), side * 0.012f);
                // Στάχυ (πιο φαρδύ)
                AddQuadStrip(v, n, t, off + lean + Vector3.up * (h - 0.17f), off + lean * 1.15f + Vector3.up * h, side * 0.03f);
            }
            Mesh m = new Mesh();
            m.name = "Wheat";
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }

        static void AddQuadStrip(List<Vector3> v, List<Vector3> n, List<int> t, Vector3 a, Vector3 b, Vector3 halfWidth)
        {
            int s = v.Count;
            v.Add(a - halfWidth); v.Add(a + halfWidth); v.Add(b + halfWidth); v.Add(b - halfWidth);
            for (int i = 0; i < 4; i++) n.Add(Vector3.up);
            t.Add(s); t.Add(s + 1); t.Add(s + 2);
            t.Add(s); t.Add(s + 2); t.Add(s + 3);
        }
    }
}
