// =====================================================================================
//  Geo.cs — Διαδικαστική γεωμετρία
//  ---------------------------------------------------------------------------------
//  Όλη η γεωμετρία του κόσμου δημιουργείται από κώδικα (δεν υπάρχουν εξωτερικά μοντέλα).
//
//  Βασική ιδέα: φτιάχνουμε ΜΟΝΑΔΙΑΙΑ meshes (κύβος 1x1x1, κύλινδρος ύψους 1 κ.λπ.) μία
//  φορά, τα αποθηκεύουμε σε cache και τα επαναχρησιμοποιούμε κλιμακώνοντας το Transform.
//  Έτσι π.χ. όλοι οι «κύβοι» του χωριού μοιράζονται ΕΝΑ Mesh αντικείμενο στη μνήμη.
//  Οι shaders μας χρωματίζουν σε world space, οπότε η κλιμάκωση δεν παραμορφώνει υφές.
//
//  Επιπλέον: βοηθητικές μέθοδοι για τοποθέτηση «κομματιών» (parts) με ή χωρίς collider,
//  και συνένωση meshes (CombineMeshes) για τα φυτά του instancing.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Greystone
{
    public static class Geo
    {
        static readonly Dictionary<string, Mesh> MeshCache = new Dictionary<string, Mesh>();

        // Layers που χρησιμοποιεί το project (αριθμοί — δεν απαιτούν ρύθμιση στο TagManager).
        public const int LayerDefault = 0;
        public const int LayerIgnoreRaycast = 2;
        public const int LayerInteract = 8;     // περιοχές αλληλεπίδρασης (triggers)
        public const int LayerSmallProps = 9;   // μικρά αντικείμενα: κόβονται νωρίτερα (layerCullDistances)
        public const int LayerPlayer = 10;

        // =============================================================== Μονάδιαία meshes
        public static Mesh Cube
        {
            get
            {
                Mesh m;
                if (MeshCache.TryGetValue("cube", out m)) return m;
                m = BuildCube();
                MeshCache["cube"] = m;
                return m;
            }
        }

        /// <summary>Κύλινδρος/κόλουρος κώνος ύψους 1, ακτίνα βάσης 0.5, ακτίνα κορυφής 0.5·taper.</summary>
        public static Mesh Cylinder(float taper = 1f, int segments = 12)
        {
            string key = "cyl_" + taper.ToString("0.000") + "_" + segments;
            Mesh m;
            if (MeshCache.TryGetValue(key, out m)) return m;
            m = BuildCylinder(0.5f, 0.5f * taper, segments);
            MeshCache[key] = m;
            return m;
        }

        public static Mesh Sphere(int segments = 12, int rings = 8)
        {
            string key = "sph_" + segments + "_" + rings;
            Mesh m;
            if (MeshCache.TryGetValue(key, out m)) return m;
            m = BuildSphere(segments, rings);
            MeshCache[key] = m;
            return m;
        }

        /// <summary>Τριγωνικό πρίσμα (αέτωμα στέγης). Τομή στο XY, κορυφή στο (0, 0.5), κορφιάς κατά Z.</summary>
        public static Mesh Prism
        {
            get
            {
                Mesh m;
                if (MeshCache.TryGetValue("prism", out m)) return m;
                m = BuildPrism();
                MeshCache["prism"] = m;
                return m;
            }
        }

        /// <summary>Επίπεδο πλέγμα 1x1 στο XZ με υποδιαιρέσεις (για νερό, τέντες).</summary>
        public static Mesh PlaneGrid(int sx, int sz)
        {
            string key = "plane_" + sx + "_" + sz;
            Mesh m;
            if (MeshCache.TryGetValue(key, out m)) return m;
            m = BuildPlane(sx, sz);
            MeshCache[key] = m;
            return m;
        }

        // =============================================================== Τοποθέτηση κομματιών
        public static Transform Child(Transform parent, string name, Vector3 localPos, Vector3 euler = default(Vector3))
        {
            GameObject go = new GameObject(name);
            Transform t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            t.localEulerAngles = euler;
            return t;
        }

        /// <summary>
        /// Δημιουργεί ένα ορατό κομμάτι (MeshFilter + MeshRenderer) και προαιρετικά BoxCollider.
        /// Ο BoxCollider ταιριάζει αυτόματα στα όρια του μοναδιαίου mesh και κλιμακώνεται με το Transform.
        /// </summary>
        public static GameObject Part(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos, Vector3 scale,
                                      Vector3 euler = default(Vector3), bool collider = false, bool castShadows = true)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = euler;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            // Τα light probes / reflection probes δεν χρησιμοποιούνται: απενεργοποίηση = λιγότερη δουλειά CPU.
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            if (collider) go.AddComponent<BoxCollider>();
            return go;
        }

        public static GameObject Box(Transform parent, Vector3 size, Vector3 pos, Material mat, Vector3 euler = default(Vector3), bool collider = false)
        {
            return Part(parent, "Box", Cube, mat, pos, size, euler, collider);
        }

        /// <summary>Κύλινδρος: pos = κέντρο, height κατά τον τοπικό άξονα Y.</summary>
        public static GameObject Cyl(Transform parent, float radius, float height, Vector3 pos, Material mat,
                                     float taper = 1f, int segments = 12, Vector3 euler = default(Vector3), bool collider = false)
        {
            return Part(parent, "Cylinder", Cylinder(taper, segments), mat, pos, new Vector3(radius * 2f, height, radius * 2f), euler, collider);
        }

        public static GameObject Ball(Transform parent, Vector3 size, Vector3 pos, Material mat, int segments = 12, int rings = 8)
        {
            return Part(parent, "Sphere", Sphere(segments, rings), mat, pos, size);
        }

        public static GameObject PrismPart(Transform parent, Vector3 size, Vector3 pos, Material mat, Vector3 euler = default(Vector3))
        {
            return Part(parent, "Prism", Prism, mat, pos, size, euler);
        }

        /// <summary>Κύλινδρος που ενώνει δύο σημεία (τοπικές συντεταγμένες) — π.χ. σχοινί ή δοκάρι.</summary>
        public static GameObject Beam(Transform parent, Vector3 a, Vector3 b, float radius, Material mat, int segments = 6)
        {
            Vector3 d = b - a;
            GameObject go = Part(parent, "Beam", Cylinder(1f, segments), mat, (a + b) * 0.5f, new Vector3(radius * 2f, d.magnitude, radius * 2f));
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            return go;
        }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
        }

        // =============================================================== Συνένωση meshes
        /// <summary>
        /// Ενώνει πολλά (mesh, πίνακας μετασχηματισμού) σε ΕΝΑ mesh. Κάθε λίστα του groups γίνεται
        /// ξεχωριστό submesh (π.χ. 0 = κορμός, 1 = φύλλωμα) ώστε να έχει δικό του υλικό.
        /// </summary>
        public static Mesh Combine(string name, params List<CombineInstance>[] groups)
        {
            List<CombineInstance> sub = new List<CombineInstance>();
            foreach (List<CombineInstance> g in groups)
            {
                Mesh part = new Mesh();
                part.indexFormat = IndexFormat.UInt32;
                part.CombineMeshes(g.ToArray(), true, true);
                CombineInstance ci = new CombineInstance();
                ci.mesh = part;
                ci.transform = Matrix4x4.identity;
                sub.Add(ci);
            }
            Mesh result = new Mesh();
            result.name = name;
            result.indexFormat = IndexFormat.UInt32;
            result.CombineMeshes(sub.ToArray(), false, false);
            result.RecalculateBounds();
            return result;
        }

        public static CombineInstance CI(Mesh mesh, Vector3 pos, Vector3 scale, Vector3 euler = default(Vector3))
        {
            CombineInstance ci = new CombineInstance();
            ci.mesh = mesh;
            ci.transform = Matrix4x4.TRS(pos, Quaternion.Euler(euler), scale);
            return ci;
        }

        // =============================================================== Κατασκευαστές meshes
        static Mesh BuildCube()
        {
            Vector3[] n = { Vector3.up, Vector3.down, Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
            List<Vector3> v = new List<Vector3>();
            List<Vector3> nn = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>();
            List<int> tri = new List<int>();
            foreach (Vector3 face in n)
            {
                // Δύο κάθετα διανύσματα πάνω στην έδρα.
                Vector3 a = face.y != 0 ? Vector3.right : Vector3.up;
                Vector3 b = Vector3.Cross(face, a);
                int start = v.Count;
                v.Add((face - a - b) * 0.5f);
                v.Add((face - a + b) * 0.5f);
                v.Add((face + a + b) * 0.5f);
                v.Add((face + a - b) * 0.5f);
                for (int i = 0; i < 4; i++) nn.Add(face);
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(0, 1)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(1, 0));
                // Φορά τριγώνων ώστε η εμπρόσθια όψη να κοιτά προς «face» (clockwise στο Unity).
                tri.Add(start); tri.Add(start + 1); tri.Add(start + 2);
                tri.Add(start); tri.Add(start + 2); tri.Add(start + 3);
            }
            Mesh m = new Mesh();
            m.name = "GS_Cube";
            m.SetVertices(v);
            m.SetNormals(nn);
            m.SetUVs(0, uv);
            m.SetTriangles(tri, 0);
            FixWinding(m);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }

        static Mesh BuildCylinder(float rBottom, float rTop, int seg)
        {
            List<Vector3> v = new List<Vector3>();
            List<Vector3> nn = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>();
            List<int> tri = new List<int>();
            float slope = (rBottom - rTop);   // για σωστές κάθετους σε κόλουρο κώνο
            // Πλευρική επιφάνεια
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                Vector3 normal = new Vector3(c, slope, s).normalized;
                v.Add(new Vector3(c * rBottom, -0.5f, s * rBottom)); nn.Add(normal); uv.Add(new Vector2(i / (float)seg, 0));
                v.Add(new Vector3(c * rTop, 0.5f, s * rTop)); nn.Add(normal); uv.Add(new Vector2(i / (float)seg, 1));
            }
            for (int i = 0; i < seg; i++)
            {
                int b = i * 2;
                tri.Add(b); tri.Add(b + 1); tri.Add(b + 2);
                tri.Add(b + 1); tri.Add(b + 3); tri.Add(b + 2);
            }
            // Καπάκια (πάνω/κάτω)
            AddCap(v, nn, uv, tri, seg, rTop, 0.5f, Vector3.up);
            AddCap(v, nn, uv, tri, seg, rBottom, -0.5f, Vector3.down);
            Mesh m = new Mesh();
            m.name = "GS_Cylinder";
            m.SetVertices(v);
            m.SetNormals(nn);
            m.SetUVs(0, uv);
            m.SetTriangles(tri, 0);
            FixWinding(m);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }

        static void AddCap(List<Vector3> v, List<Vector3> nn, List<Vector2> uv, List<int> tri, int seg, float r, float y, Vector3 normal)
        {
            if (r <= 0.0001f) return;
            int center = v.Count;
            v.Add(new Vector3(0, y, 0)); nn.Add(normal); uv.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                v.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r)); nn.Add(normal);
                uv.Add(new Vector2(Mathf.Cos(a) * 0.5f + 0.5f, Mathf.Sin(a) * 0.5f + 0.5f));
            }
            for (int i = 0; i < seg; i++)
            {
                tri.Add(center); tri.Add(center + 1 + i); tri.Add(center + 2 + i);
            }
        }

        static Mesh BuildSphere(int seg, int rings)
        {
            List<Vector3> v = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>();
            List<int> tri = new List<int>();
            for (int r = 0; r <= rings; r++)
            {
                float phi = r / (float)rings * Mathf.PI;
                for (int s = 0; s <= seg; s++)
                {
                    float th = s / (float)seg * Mathf.PI * 2f;
                    v.Add(new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th)) * 0.5f);
                    uv.Add(new Vector2(s / (float)seg, 1f - r / (float)rings));
                }
            }
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < seg; s++)
                {
                    int a = r * (seg + 1) + s;
                    int b = a + seg + 1;
                    tri.Add(a); tri.Add(a + 1); tri.Add(b);
                    tri.Add(a + 1); tri.Add(b + 1); tri.Add(b);
                }
            Mesh m = new Mesh();
            m.name = "GS_Sphere";
            m.SetVertices(v);
            List<Vector3> nn = new List<Vector3>();
            foreach (Vector3 p in v) nn.Add(p.normalized);
            m.SetNormals(nn);
            m.SetUVs(0, uv);
            m.SetTriangles(tri, 0);
            FixWinding(m);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }

        static Mesh BuildPrism()
        {
            // Κορυφές τριγωνικής τομής
            Vector3 L = new Vector3(-0.5f, -0.5f, 0), R = new Vector3(0.5f, -0.5f, 0), T = new Vector3(0, 0.5f, 0);
            Vector3 f = new Vector3(0, 0, 0.5f), b = new Vector3(0, 0, -0.5f);
            List<Vector3> v = new List<Vector3>();
            List<int> tri = new List<int>();
            // Κάθε έδρα με δικές της κορυφές (flat shading)
            AddQuad(v, tri, L + b, T + b, T + f, L + f);      // αριστερή κεκλιμένη
            AddQuad(v, tri, T + b, R + b, R + f, T + f);      // δεξιά κεκλιμένη
            AddQuad(v, tri, L + f, R + f, R + b, L + b);      // βάση
            int s = v.Count;                                   // μπροστινό τρίγωνο
            v.Add(L + f); v.Add(T + f); v.Add(R + f);
            tri.Add(s); tri.Add(s + 1); tri.Add(s + 2);
            s = v.Count;                                       // πίσω τρίγωνο
            v.Add(L + b); v.Add(R + b); v.Add(T + b);
            tri.Add(s); tri.Add(s + 1); tri.Add(s + 2);
            Mesh m = new Mesh();
            m.name = "GS_Prism";
            m.SetVertices(v);
            m.SetTriangles(tri, 0);
            m.RecalculateNormals();
            FixWinding(m, true);
            List<Vector2> uv = new List<Vector2>();
            foreach (Vector3 p in v) uv.Add(new Vector2(p.x + 0.5f, p.y + 0.5f));
            m.SetUVs(0, uv);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }

        static void AddQuad(List<Vector3> v, List<int> tri, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int s = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            tri.Add(s); tri.Add(s + 1); tri.Add(s + 2);
            tri.Add(s); tri.Add(s + 2); tri.Add(s + 3);
        }

        static Mesh BuildPlane(int sx, int sz)
        {
            List<Vector3> v = new List<Vector3>();
            List<Vector3> nn = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>();
            List<int> tri = new List<int>();
            for (int z = 0; z <= sz; z++)
                for (int x = 0; x <= sx; x++)
                {
                    v.Add(new Vector3(x / (float)sx - 0.5f, 0, z / (float)sz - 0.5f));
                    nn.Add(Vector3.up);
                    uv.Add(new Vector2(x / (float)sx, z / (float)sz));
                }
            for (int z = 0; z < sz; z++)
                for (int x = 0; x < sx; x++)
                {
                    int a = z * (sx + 1) + x;
                    int c = a + sx + 1;
                    // Clockwise όπως φαίνεται από πάνω (+Y) -> εμπρόσθια όψη προς τα πάνω
                    tri.Add(a); tri.Add(c); tri.Add(a + 1);
                    tri.Add(a + 1); tri.Add(c); tri.Add(c + 1);
                }
            Mesh m = new Mesh();
            m.name = "GS_Plane";
            m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(v);
            m.SetNormals(nn);
            m.SetUVs(0, uv);
            m.SetTriangles(tri, 0);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }

        /// <summary>
        /// Διασφαλίζει ότι κάθε τρίγωνο «κοιτά» προς την κατεύθυνση της κάθετού του (ή, για
        /// κλειστά κυρτά σχήματα με fromCenter=true, προς τα έξω). Έτσι δεν χρειάζεται να
        /// θυμόμαστε τη φορά (clockwise) του Unity σε κάθε κατασκευαστή — λιγότερα λάθη.
        /// </summary>
        static void FixWinding(Mesh m, bool fromCenter = false)
        {
            Vector3[] v = m.vertices;
            Vector3[] n = m.normals;
            int[] t = m.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                // Στο Unity (αριστερόστροφο σύστημα) η εμπρόσθια κάθετος είναι Cross(b-a, c-a).
                Vector3 faceN = Vector3.Cross(b - a, c - a);
                Vector3 want = fromCenter ? (a + b + c) / 3f : (n[t[i]] + n[t[i + 1]] + n[t[i + 2]]);
                if (Vector3.Dot(faceN, want) < 0f)
                {
                    int tmp = t[i + 1];
                    t[i + 1] = t[i + 2];
                    t[i + 2] = tmp;
                }
            }
            m.triangles = t;
            if (fromCenter) m.RecalculateNormals();
        }
    }
}
