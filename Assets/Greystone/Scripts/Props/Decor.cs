// =====================================================================================
//  Decor.cs — Διακοσμητικά στοιχεία του χωριού
//  ---------------------------------------------------------------------------------
//  Στατικά αντικείμενα που δίνουν πληρότητα και αληθοφάνεια: σταυρός της αγοράς, παγκάκια,
//  θημωνιές, σκιάχτρο, ξερολιθιές (dry-stone walls) του περιβόλου της εκκλησίας,
//  ταφόπλακες, φράχτης του μαντριού, ξυλοστοίβες, ale-stake της ταβέρνας.
//
//  ΒΕΛΤΙΣΤΟΠΟΙΗΣΗ: επαναλαμβανόμενα στοιχεία (πάσσαλοι φράχτη, πέτρες τοίχου, ταφόπλακες)
//  ενώνονται σε ΕΝΑ mesh με Geo.Combine, ενώ οι colliders είναι λίγα απλά κουτιά.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Greystone
{
    public static class Decor
    {
        /// <summary>Λίθινος σταυρός της αγοράς (market cross) σε κλιμακωτή βάση.</summary>
        public static void MarketCross(Transform parent, Vector3 pos)
        {
            Transform t = Geo.Child(parent, "MarketCross", pos);
            Material s = Mats.Get("stone_grey");
            Geo.Box(t, new Vector3(3.0f, 0.3f, 3.0f), new Vector3(0, 0.1f, 0), s, default(Vector3), true);
            Geo.Box(t, new Vector3(2.2f, 0.3f, 2.2f), new Vector3(0, 0.4f, 0), s, default(Vector3), true);
            Geo.Box(t, new Vector3(1.4f, 0.3f, 1.4f), new Vector3(0, 0.7f, 0), s, default(Vector3), true);
            Geo.Box(t, new Vector3(0.6f, 0.6f, 0.6f), new Vector3(0, 1.15f, 0), s);
            Geo.Cyl(t, 0.18f, 3.2f, new Vector3(0, 3.0f, 0), s, 0.8f, 8, default(Vector3), true);
            Geo.Box(t, new Vector3(0.9f, 0.18f, 0.18f), new Vector3(0, 4.3f, 0), s);
            Geo.Box(t, new Vector3(0.18f, 0.7f, 0.18f), new Vector3(0, 4.45f, 0), s);
        }

        public static void Bench(Transform parent, Vector3 pos, float yaw)
        {
            Transform t = Geo.Child(parent, "Bench", pos, new Vector3(0, yaw, 0));
            Material w = Mats.Get("wood_weathered");
            Geo.Box(t, new Vector3(1.8f, 0.07f, 0.4f), new Vector3(0, 0.45f, 0), w, default(Vector3), true);
            Geo.Box(t, new Vector3(0.1f, 0.42f, 0.35f), new Vector3(-0.75f, 0.21f, 0), Mats.Get("timber"));
            Geo.Box(t, new Vector3(0.1f, 0.42f, 0.35f), new Vector3(0.75f, 0.21f, 0), Mats.Get("timber"));
        }

        /// <summary>Θημωνιά (κωνική στοίβα σανού).</summary>
        public static void Haystack(Transform parent, Vector3 pos, float scale)
        {
            Transform t = Geo.Child(parent, "Haystack", pos);
            Geo.Cyl(t, 1.3f * scale, 1.6f * scale, new Vector3(0, 0.8f * scale, 0), Mats.Get("straw"), 0.9f, 14, default(Vector3), true);
            Geo.Cyl(t, 1.2f * scale, 1.4f * scale, new Vector3(0, 2.3f * scale, 0), Mats.Get("straw"), 0f, 14);
        }

        public static void Scarecrow(Transform parent, Vector3 pos)
        {
            Transform t = Geo.Child(parent, "Scarecrow", pos, new Vector3(0, 30, 0));
            Material w = Mats.Get("wood_weathered");
            Geo.Box(t, new Vector3(0.08f, 2.4f, 0.08f), new Vector3(0, 1.2f, 0), w, default(Vector3), true);
            Geo.Box(t, new Vector3(1.6f, 0.07f, 0.07f), new Vector3(0, 1.75f, 0), w);
            Geo.Cyl(t, 0.3f, 0.7f, new Vector3(0, 1.5f, 0), Mats.Solid(new Color(0.42f, 0.3f, 0.2f)), 0.8f, 8);   // παλιό σακάκι
            Geo.Ball(t, Vector3.one * 0.32f, new Vector3(0, 2.05f, 0), Mats.Get("straw"));
            Geo.Cyl(t, 0.3f, 0.04f, new Vector3(0, 2.2f, 0), Mats.Get("straw"), 1f, 10);                         // γείσο καπέλου
            Geo.Cyl(t, 0.16f, 0.2f, new Vector3(0, 2.3f, 0), Mats.Get("straw"), 0.8f, 10);
            Geo.Box(t, new Vector3(0.4f, 0.12f, 0.12f), new Vector3(-0.85f, 1.75f, 0), Mats.Get("straw"), new Vector3(0, 0, 20));
            Geo.Box(t, new Vector3(0.4f, 0.12f, 0.12f), new Vector3(0.85f, 1.75f, 0), Mats.Get("straw"), new Vector3(0, 0, -20));
        }

        public static void Woodpile(Transform parent, Vector3 pos, float yaw)
        {
            Transform t = Geo.Child(parent, "Woodpile", pos, new Vector3(0, yaw, 0));
            List<CombineInstance> logs = new List<CombineInstance>();
            Mesh cyl = Geo.Cylinder(1f, 7);
            for (int row = 0; row < 3; row++)
                for (int i = 0; i < 5 - row; i++)
                    logs.Add(Geo.CI(cyl, new Vector3(-0.8f + i * 0.34f + row * 0.17f, 0.15f + row * 0.29f, 0), new Vector3(0.3f, 1.1f, 0.3f), new Vector3(90, 0, 0)));
            GameObject g = Geo.Part(t, "Logs", Geo.Combine("Woodpile", logs), Mats.Get("bark"), Vector3.zero, Vector3.one);
            g.layer = Geo.LayerSmallProps;
            BoxCollider bc = t.gameObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, 0.45f, 0);
            bc.size = new Vector3(1.8f, 0.9f, 1.1f);
        }

        /// <summary>Ale-stake: κοντάρι με «θάμνο» έξω από την ταβέρνα (σήμα φρέσκιας μπύρας).</summary>
        public static void AleStake(Transform parent, Vector3 localPos, float yaw)
        {
            Transform t = Geo.Child(parent, "AleStake", localPos, new Vector3(0, yaw, 0));
            Geo.Box(t, new Vector3(0.08f, 0.08f, 1.6f), new Vector3(0, 0, 0.8f), Mats.Get("timber"), new Vector3(-12, 0, 0));
            Geo.Ball(t, new Vector3(0.7f, 0.6f, 0.7f), new Vector3(0, -0.45f, 1.55f), Mats.Get("leaves"), 8, 6);
            Geo.Beam(t, new Vector3(0, 0.3f, 1.55f), new Vector3(0, -0.2f, 1.55f), 0.01f, Mats.Get("rope"));
        }

        /// <summary>
        /// Ξερολιθιά κατά μήκος πολυγωνικής γραμμής (λίστα σημείων στον κόσμο). Κάθε τμήμα
        /// ακολουθεί το έδαφος. Τα οπτικά τμήματα ενώνονται σε ένα mesh· collider ανά τμήμα.
        /// </summary>
        public static void DryStoneWall(Transform parent, Vector2[] pts, float height)
        {
            Transform t = Geo.Child(parent, "DryStoneWall", Vector3.zero);
            List<CombineInstance> parts = new List<CombineInstance>();
            for (int i = 0; i < pts.Length - 1; i++)
            {
                Vector2 a = pts[i], b = pts[i + 1];
                float len = Vector2.Distance(a, b);
                int segs = Mathf.Max(1, Mathf.CeilToInt(len / 3f));
                for (int k = 0; k < segs; k++)
                {
                    Vector2 p0 = Vector2.Lerp(a, b, k / (float)segs), p1 = Vector2.Lerp(a, b, (k + 1) / (float)segs);
                    Vector2 m = (p0 + p1) * 0.5f;
                    float y = WorldLayout.HeightAt(m.x, m.y);
                    float yaw = Mathf.Atan2(p1.x - p0.x, p1.y - p0.y) * Mathf.Rad2Deg;
                    Vector3 c = new Vector3(m.x, y + height * 0.5f - 0.15f, m.y);
                    Vector3 size = new Vector3(0.55f, height + 0.3f, Vector2.Distance(p0, p1) + 0.05f);
                    parts.Add(Geo.CI(Geo.Cube, c, size, new Vector3(0, yaw, 0)));
                    parts.Add(Geo.CI(Geo.Cube, c + Vector3.up * (height * 0.5f + 0.12f), new Vector3(0.65f, 0.14f, size.z), new Vector3(0, yaw, 0)));   // «κορυφή» (coping)
                    GameObject col = new GameObject("WallCol");
                    col.transform.SetParent(t, false);
                    col.transform.position = c;
                    col.transform.rotation = Quaternion.Euler(0, yaw, 0);
                    col.AddComponent<BoxCollider>().size = size;
                }
            }
            Geo.Part(t, "WallMesh", Geo.Combine("DryStoneWall", parts), Mats.Get("stone_rubble"), Vector3.zero, Vector3.one);
        }

        /// <summary>Ταφόπλακες στον περίβολο (ένα συνενωμένο mesh).</summary>
        public static void Gravestones(Transform parent, Rect area, int count, int seed)
        {
            System.Random r = new System.Random(seed);
            List<CombineInstance> stones = new List<CombineInstance>();
            for (int i = 0; i < count; i++)
            {
                float x = area.xMin + (float)r.NextDouble() * area.width;
                float z = area.yMin + (float)r.NextDouble() * area.height;
                float y = WorldLayout.HeightAt(x, z);
                float tilt = ((float)r.NextDouble() - 0.5f) * 10f;   // παλιές πλάκες, ελαφρώς γερμένες
                float h = 0.6f + (float)r.NextDouble() * 0.5f;
                stones.Add(Geo.CI(Geo.Cube, new Vector3(x, y + h * 0.4f, z), new Vector3(0.55f, h, 0.12f), new Vector3(tilt, (float)r.NextDouble() * 10f - 5f, tilt * 0.5f)));
                stones.Add(Geo.CI(Geo.Cylinder(1f, 10), new Vector3(x, y + h * 0.9f, z), new Vector3(0.55f, 0.12f, 0.55f), new Vector3(90, 0, 0)));
            }
            GameObject g = Geo.Part(parent, "Gravestones", Geo.Combine("Gravestones", stones), Mats.Get("stone_grey"), Vector3.zero, Vector3.one);
            g.layer = Geo.LayerSmallProps;
        }

        /// <summary>Ξύλινος φράχτης (πάσσαλοι + 2 οριζόντια ξύλα) γύρω από ορθογώνιο με άνοιγμα-πύλη.</summary>
        public static void Fence(Transform parent, Rect r, float gateCenterZ, float gateWidth)
        {
            Transform t = Geo.Child(parent, "Fence", Vector3.zero);
            List<CombineInstance> parts = new List<CombineInstance>();
            Vector2[] corners = { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax), new Vector2(r.xMin, r.yMin) };
            for (int side = 0; side < 4; side++)
            {
                Vector2 a = corners[side], b = corners[side + 1];
                float len = Vector2.Distance(a, b);
                int posts = Mathf.CeilToInt(len / 2.2f);
                for (int k = 0; k < posts; k++)
                {
                    Vector2 p0 = Vector2.Lerp(a, b, k / (float)posts), p1 = Vector2.Lerp(a, b, (k + 1) / (float)posts);
                    Vector2 mid = (p0 + p1) * 0.5f;
                    // Άνοιγμα πύλης στη δυτική πλευρά (side 3: από (xMin,yMax) προς (xMin,yMin))
                    if (side == 3 && Mathf.Abs(mid.y - gateCenterZ) < gateWidth * 0.5f) continue;
                    float y0 = WorldLayout.HeightAt(p0.x, p0.y);
                    float ym = WorldLayout.HeightAt(mid.x, mid.y);
                    float yaw = Mathf.Atan2(p1.x - p0.x, p1.y - p0.y) * Mathf.Rad2Deg;
                    float seg = Vector2.Distance(p0, p1);
                    parts.Add(Geo.CI(Geo.Cube, new Vector3(p0.x, y0 + 0.55f, p0.y), new Vector3(0.14f, 1.3f, 0.14f)));
                    parts.Add(Geo.CI(Geo.Cube, new Vector3(mid.x, ym + 0.45f, mid.y), new Vector3(0.06f, 0.1f, seg), new Vector3(0, yaw, 0)));
                    parts.Add(Geo.CI(Geo.Cube, new Vector3(mid.x, ym + 0.95f, mid.y), new Vector3(0.06f, 0.1f, seg), new Vector3(0, yaw, 0)));
                    GameObject col = new GameObject("FenceCol");
                    col.transform.SetParent(t, false);
                    col.transform.position = new Vector3(mid.x, ym + 0.6f, mid.y);
                    col.transform.rotation = Quaternion.Euler(0, yaw, 0);
                    col.AddComponent<BoxCollider>().size = new Vector3(0.2f, 1.2f, seg);
                }
            }
            Geo.Part(t, "FenceMesh", Geo.Combine("Fence", parts), Mats.Get("wood_weathered"), Vector3.zero, Vector3.one);
        }
    }
}
