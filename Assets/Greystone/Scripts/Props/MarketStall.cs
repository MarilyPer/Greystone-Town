// =====================================================================================
//  MarketStall.cs — Πάγκος αγοράς
//  ---------------------------------------------------------------------------------
//  Ξύλινος πάγκος με ριγέ τέντα από ύφασμα που ΚΥΜΑΤΙΖΕΙ στον άνεμο (Cloth shader) και
//  εμπορεύματα (μήλα, λάχανα, ψωμιά) — τα πολλά μικρά φρούτα ενώνονται σε ΕΝΑ mesh
//  (CombineMeshes) ώστε να κοστίζουν ένα μόνο draw call ανά πάγκο.
//  Ο πάγκος κοιτά προς το τοπικό +Z (ο πωλητής στέκεται πίσω, στο -Z).
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Greystone
{
    public class MarketStall : MonoBehaviour
    {
        public Vector3 VendorSpotWorld { get { return transform.TransformPoint(new Vector3(0, 0, -1.1f)); } }

        public void Build(string clothKey, string goodsKey)
        {
            Transform st = Geo.Child(transform, "Static", Vector3.zero);
            Material wood = Mats.Get("wood");
            Material dark = Mats.Get("timber");
            const float w = 3.0f, d = 1.4f;
            // Πάγκος
            Geo.Box(st, new Vector3(w, 0.08f, d), new Vector3(0, 0.9f, 0), wood, default(Vector3), true);
            Geo.Box(st, new Vector3(w, 0.75f, 0.05f), new Vector3(0, 0.48f, d * 0.5f - 0.03f), Mats.Get("wood_weathered"));
            // Στύλοι
            for (int ix = -1; ix <= 1; ix += 2)
            {
                Geo.Box(st, new Vector3(0.1f, 2.5f, 0.1f), new Vector3(ix * (w * 0.5f - 0.05f), 1.25f, d * 0.5f - 0.05f), dark, default(Vector3), true);
                Geo.Box(st, new Vector3(0.1f, 2.8f, 0.1f), new Vector3(ix * (w * 0.5f - 0.05f), 1.4f, -d * 0.5f + 0.05f), dark, default(Vector3), true);
            }
            // Τέντα: υποδιαιρεμένο επίπεδο (για κυμάτισμα). Στερεωμένο πίσω (uv.y=0) ελεύθερο μπροστά.
            GameObject awning = Geo.Part(transform, "Awning", Geo.PlaneGrid(8, 6), Mats.Get(clothKey),
                                         new Vector3(0, 2.65f, 0.05f), new Vector3(w + 0.4f, 1f, d + 0.9f), new Vector3(-14f, 0, 0));
            awning.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;

            // Εμπορεύματα: πολλά μικρά σχήματα -> ΕΝΑ mesh
            List<CombineInstance> goods = new List<CombineInstance>();
            System.Random r = new System.Random(goodsKey.GetHashCode());
            Mesh sphere = Geo.Sphere(8, 6);
            for (int i = 0; i < 3; i++)   // τρία καλάθια
            {
                Vector3 basket = new Vector3(-0.9f + i * 0.9f, 0.94f, 0.05f);
                Geo.Cyl(transform, 0.34f, 0.18f, basket + new Vector3(0, 0.09f, 0), Mats.Get("straw"), 1.2f, 12).layer = Geo.LayerSmallProps;
                int n = goodsKey == "bread" ? 6 : 14;
                for (int k = 0; k < n; k++)
                {
                    float a = (float)r.NextDouble() * Mathf.PI * 2f;
                    float rad = (float)r.NextDouble() * 0.25f;
                    Vector3 p = basket + new Vector3(Mathf.Cos(a) * rad, 0.2f + (float)r.NextDouble() * 0.08f, Mathf.Sin(a) * rad);
                    Vector3 s = goodsKey == "cabbage" ? Vector3.one * 0.2f : (goodsKey == "bread" ? new Vector3(0.24f, 0.1f, 0.14f) : Vector3.one * 0.09f);
                    goods.Add(Geo.CI(sphere, p, s, new Vector3(0, (float)r.NextDouble() * 360f, 0)));
                }
            }
            Mesh m = Geo.Combine("Goods_" + goodsKey, goods);
            GameObject gg = Geo.Part(transform, "Goods", m, Mats.Get(goodsKey), Vector3.zero, Vector3.one);
            gg.layer = Geo.LayerSmallProps;
            // Κασόνια δίπλα
            Geo.Box(transform, new Vector3(0.6f, 0.45f, 0.45f), new Vector3(w * 0.5f + 0.5f, 0.22f, 0.2f), Mats.Get("wood_weathered"), new Vector3(0, 12, 0), true).layer = Geo.LayerSmallProps;
            Geo.Box(transform, new Vector3(0.6f, 0.45f, 0.45f), new Vector3(w * 0.5f + 0.45f, 0.67f, 0.18f), Mats.Get("wood_weathered"), new Vector3(0, -6, 0)).layer = Geo.LayerSmallProps;

            Vector3 c = transform.position;
            WorldLayout.Footprints.Add(new Rect(c.x - 2f, c.z - 2f, 4f, 4f));
        }
    }
}
