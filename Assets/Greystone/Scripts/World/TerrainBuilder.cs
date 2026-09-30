// =====================================================================================
//  TerrainBuilder.cs — Διαδικαστικό έδαφος σε τμήματα (chunks)
//  ---------------------------------------------------------------------------------
//  1. Υπολογίζει ένα πλέγμα υψών 241x241 (ανά 2 m) από τη WorldLayout.ComputeHeight και
//     το αποθηκεύει (cache) για γρήγορες αναζητήσεις ύψους από τα υπόλοιπα συστήματα.
//  2. Χωρίζει το έδαφος σε 6x6 τμήματα των 80 m. ΒΕΛΤΙΣΤΟΠΟΙΗΣΗ: κάθε τμήμα έχει δικό του
//     bounding box, οπότε το Unity εφαρμόζει frustum culling ανά τμήμα — δεν ζωγραφίζεται
//     το έδαφος που βρίσκεται πίσω από την κάμερα.
//  3. Κάθε τμήμα έχει MeshCollider (το ίδιο mesh) για φυσική/περπάτημα.
//  4. Γύρω από τον χάρτη τοποθετεί «μακρινή πεδιάδα» και αόρατα τοιχώματα-όρια.
// =====================================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Greystone
{
    public class TerrainBuilder : MonoBehaviour
    {
        const int ChunksPerSide = 6;
        const int CellsPerChunk = (WorldLayout.Verts - 1) / ChunksPerSide;   // 40

        float[] _h;

        public IEnumerator Build()
        {
            int V = WorldLayout.Verts;
            _h = new float[V * V];
            for (int j = 0; j < V; j++)
            {
                float z = -WorldLayout.Half + j * WorldLayout.Step;
                for (int i = 0; i < V; i++)
                    _h[j * V + i] = WorldLayout.ComputeHeight(-WorldLayout.Half + i * WorldLayout.Step, z);
                if (j % 60 == 0) yield return null;   // μοίρασμα της δουλειάς σε frames
            }
            WorldLayout.SetHeightCache(_h);

            Material mat = Mats.Get("terrain");
            ConfigureTerrainMaterial(mat);

            for (int cz = 0; cz < ChunksPerSide; cz++)
            {
                for (int cx = 0; cx < ChunksPerSide; cx++)
                    BuildChunk(cx, cz, mat);
                yield return null;
            }

            BuildFarGround(mat);
            BuildBoundaries();
        }

        /// <summary>Στέλνει τα δεδομένα διάταξης (δρόμοι, πλατεία, χωράφια) στον shader εδάφους.</summary>
        public static void ConfigureTerrainMaterial(Material mat)
        {
            Vector4[] roads = new Vector4[8];
            for (int i = 0; i < WorldLayout.Roads.Length && i < 8; i++) roads[i] = WorldLayout.Roads[i];
            mat.SetVectorArray("_Roads", roads);
            mat.SetFloat("_RoadCount", Mathf.Min(8, WorldLayout.Roads.Length));
            mat.SetVector("_Plaza", new Vector4(WorldLayout.PlazaCenter.x, WorldLayout.PlazaCenter.y, WorldLayout.PlazaRadius, 0));
            Vector4[] fields = new Vector4[4];
            for (int i = 0; i < WorldLayout.Fields.Length && i < 4; i++)
            {
                Rect f = WorldLayout.Fields[i];
                fields[i] = new Vector4(f.xMin, f.yMin, f.xMax, f.yMax);
            }
            mat.SetVectorArray("_Fields", fields);
            mat.SetFloat("_FieldCount", Mathf.Min(4, WorldLayout.Fields.Length));
            mat.SetFloat("_WaterLevel", WorldLayout.WaterLevel);
            mat.SetFloat("_RoadHalfWidth", WorldLayout.RoadHalfWidth);
        }

        void BuildChunk(int cx, int cz, Material mat)
        {
            int V = WorldLayout.Verts;
            int n = CellsPerChunk + 1;
            Vector3[] verts = new Vector3[n * n];
            Vector3[] normals = new Vector3[n * n];
            Vector2[] uvs = new Vector2[n * n];
            int[] tris = new int[CellsPerChunk * CellsPerChunk * 6];

            int i0 = cx * CellsPerChunk, j0 = cz * CellsPerChunk;
            // Το τμήμα τοποθετείται στο κέντρο του, ώστε το Transform να έχει νόημα (π.χ. για το LOD)
            Vector3 origin = new Vector3(-WorldLayout.Half + (i0 + CellsPerChunk * 0.5f) * WorldLayout.Step, 0,
                                         -WorldLayout.Half + (j0 + CellsPerChunk * 0.5f) * WorldLayout.Step);

            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    int gi = i0 + i, gj = j0 + j;
                    float x = -WorldLayout.Half + gi * WorldLayout.Step;
                    float z = -WorldLayout.Half + gj * WorldLayout.Step;
                    int k = j * n + i;
                    verts[k] = new Vector3(x, _h[gj * V + gi], z) - origin;
                    // Κάθετος με κεντρικές διαφορές στο καθολικό πλέγμα (χωρίς «ραφές» ανάμεσα στα τμήματα)
                    float hl = _h[gj * V + Mathf.Max(gi - 1, 0)];
                    float hr = _h[gj * V + Mathf.Min(gi + 1, V - 1)];
                    float hd = _h[Mathf.Max(gj - 1, 0) * V + gi];
                    float hu = _h[Mathf.Min(gj + 1, V - 1) * V + gi];
                    normals[k] = new Vector3(hl - hr, 2f * WorldLayout.Step, hd - hu).normalized;
                    uvs[k] = new Vector2(x, z) * 0.1f;
                }

            int t = 0;
            for (int j = 0; j < CellsPerChunk; j++)
                for (int i = 0; i < CellsPerChunk; i++)
                {
                    int a = j * n + i;
                    int c = a + n;
                    // Clockwise από πάνω -> εμπρόσθια όψη προς τα πάνω
                    tris[t++] = a; tris[t++] = c; tris[t++] = a + 1;
                    tris[t++] = a + 1; tris[t++] = c; tris[t++] = c + 1;
                }

            Mesh m = new Mesh();
            m.name = "TerrainChunk_" + cx + "_" + cz;
            m.indexFormat = IndexFormat.UInt32;
            m.vertices = verts;
            m.normals = normals;
            m.uv = uvs;
            m.triangles = tris;
            m.RecalculateBounds();

            GameObject go = new GameObject(m.name);
            go.transform.SetParent(transform, false);
            go.transform.position = origin;
            go.isStatic = true;
            go.AddComponent<MeshFilter>().sharedMesh = m;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            MeshCollider mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = m;
        }

        /// <summary>Τέσσερις μεγάλες λωρίδες «πεδιάδας» έξω από τον χάρτη (για ορίζοντα χωρίς κενά).</summary>
        void BuildFarGround(Material mat)
        {
            const float far = 2200f;
            float h = WorldLayout.Half;
            Vector4[] strips =
            {
                new Vector4(0, h + far * 0.5f, far * 2f + h * 2f, far),    // βόρεια
                new Vector4(0, -h - far * 0.5f, far * 2f + h * 2f, far),   // νότια
                new Vector4(h + far * 0.5f, 0, far, h * 2f),               // ανατολικά
                new Vector4(-h - far * 0.5f, 0, far, h * 2f),              // δυτικά
            };
            foreach (Vector4 s in strips)
            {
                GameObject g = Geo.Part(transform, "FarGround", Geo.PlaneGrid(1, 1), mat, new Vector3(s.x, 0.2f, s.y), new Vector3(s.z, 1f, s.w));
                g.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                g.isStatic = true;
            }
        }

        /// <summary>Αόρατα τοιχώματα που κρατούν τον παίκτη μέσα στην περιοχή περιήγησης.</summary>
        void BuildBoundaries()
        {
            float b = WorldLayout.PlayBoundary;
            Vector3[] centers = { new Vector3(0, 50, b + 1), new Vector3(0, 50, -b - 1), new Vector3(b + 1, 50, 0), new Vector3(-b - 1, 50, 0) };
            Vector3[] sizes = { new Vector3(b * 2 + 4, 200, 2), new Vector3(b * 2 + 4, 200, 2), new Vector3(2, 200, b * 2 + 4), new Vector3(2, 200, b * 2 + 4) };
            for (int i = 0; i < 4; i++)
            {
                GameObject w = new GameObject("Boundary");
                w.transform.SetParent(transform, false);
                w.transform.position = centers[i];
                w.AddComponent<BoxCollider>().size = sizes[i];
            }
        }
    }
}
