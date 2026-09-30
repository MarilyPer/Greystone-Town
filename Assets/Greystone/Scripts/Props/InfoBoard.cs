// =====================================================================================
//  InfoBoard.cs — Πινακίδα πληροφόρησης
//  ---------------------------------------------------------------------------------
//  Ξύλινη πινακίδα σε δύο στύλους με τίτλο, σύντομο κείμενο και την κύρια πηγή, γραμμένα
//  απευθείας πάνω της (3D κείμενο). Με [E] ανοίγει στο HUD το πλήρες κείμενο με όλες τις
//  πηγές (Lore.cs). Έτσι οι πηγές του περιεχομένου αναφέρονται ΜΕΣΑ στον εικονικό κόσμο.
//  Η πινακίδα κοιτά προς το τοπικό +Z.
// =====================================================================================
using UnityEngine;

namespace Greystone
{
    public class InfoBoard : MonoBehaviour
    {
        public string loreKey;

        public void Build(string key)
        {
            loreKey = key;
            LoreEntry e = Lore.Get(key);
            Material wood = Mats.Get("wood_weathered");
            Material dark = Mats.Get("timber");
            const float w = 1.7f, h = 1.15f, y = 1.55f;

            Geo.Box(transform, new Vector3(0.1f, 2.2f, 0.1f), new Vector3(-w * 0.5f + 0.05f, 1.1f, -0.05f), dark, default(Vector3), true);
            Geo.Box(transform, new Vector3(0.1f, 2.2f, 0.1f), new Vector3(w * 0.5f - 0.05f, 1.1f, -0.05f), dark, default(Vector3), true);
            Geo.Box(transform, new Vector3(w, h, 0.05f), new Vector3(0, y, 0), Mats.Get("plaster"));
            // Κορνίζα
            Geo.Box(transform, new Vector3(w + 0.1f, 0.07f, 0.08f), new Vector3(0, y + h * 0.5f, 0), wood);
            Geo.Box(transform, new Vector3(w + 0.1f, 0.07f, 0.08f), new Vector3(0, y - h * 0.5f, 0), wood);
            // Μικρό στέγαστρο που προστατεύει από τη βροχή
            Geo.PrismPart(transform, new Vector3(0.45f, 0.18f, w + 0.35f), new Vector3(0, y + h * 0.5f + 0.13f, -0.02f), Mats.Get("roof_slate"), new Vector3(0, 90, 0));

            if (e == null) return;
            float left = w * 0.5f - 0.08f;   // το κείμενο είναι στραμμένο 180°, άρα «αριστερά» του θεατή = +x της πινακίδας
            float top = y + h * 0.5f - 0.07f;
            Color ink = new Color(0.14f, 0.09f, 0.05f);
            TextUtil.Create(transform, TextUtil.Wrap(e.title, 34), new Vector3(left, top, 0.03f), 0.085f, ink, TextAnchor.UpperLeft, FontStyle.Bold)
                    .transform.localRotation = Quaternion.Euler(0, 180, 0);
            TextUtil.Create(transform, TextUtil.Wrap(e.shortText, 50), new Vector3(left, top - 0.2f, 0.03f), 0.058f, ink)
                    .transform.localRotation = Quaternion.Euler(0, 180, 0);
            string src = e.sources.Length > 0 ? "Πηγή: " + e.sources[0].Split(':')[0] : "";
            TextUtil.Create(transform, TextUtil.Wrap(src, 60), new Vector3(left, y - h * 0.5f + 0.2f, 0.03f), 0.045f, new Color(0.3f, 0.2f, 0.12f), TextAnchor.UpperLeft, FontStyle.Italic)
                    .transform.localRotation = Quaternion.Euler(0, 180, 0);
            TextUtil.Create(transform, "[E] Διαβάστε περισσότερα", new Vector3(left, y - h * 0.5f + 0.1f, 0.03f), 0.045f, new Color(0.45f, 0.12f, 0.08f))
                    .transform.localRotation = Quaternion.Euler(0, 180, 0);

            Interactable it = Interactable.Create(transform, new Vector3(0, y, 0.2f), new Vector3(w + 0.2f, h + 0.4f, 0.5f), "Διαβάστε: " + e.title);
            it.Interacted += p => { if (Hud.Instance != null) Hud.Instance.ShowLore(e); };
        }
    }
}
