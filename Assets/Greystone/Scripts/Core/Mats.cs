// =====================================================================================
//  Mats.cs — Βιβλιοθήκη υλικών (Material Library)
//  ---------------------------------------------------------------------------------
//  Όλα τα υλικά δημιουργούνται ΜΙΑ φορά και ΜΟΙΡΑΖΟΝΤΑΙ (Flyweight pattern).
//
//  Γιατί είναι βελτιστοποίηση: Αν κάθε σπίτι είχε δικό του αντίγραφο του υλικού
//  «πέτρα», η GPU θα έπρεπε να αλλάζει κατάσταση (SetPass) για κάθε σπίτι και το
//  static batching δεν θα μπορούσε να ενώσει τα meshes. Με κοινά υλικά:
//    - το static batching ενώνει όλα τα αντικείμενα ίδιου υλικού σε λίγα batches,
//    - το GPU instancing λειτουργεί (απαιτεί ίδιο υλικό),
//    - η μνήμη μένει μικρή.
//  Για διαφοροποιήσεις ανά αντικείμενο (π.χ. χρώμα) προτιμάμε MaterialPropertyBlock
//  ή παραλλαγή στον shader με βάση τη θέση, αντί για νέα υλικά.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Greystone
{
    public static class Mats
    {
        static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

        // Ονόματα των καθολικών μεταβλητών shader (βλ. GreystoneCommon.cginc).
        // Τα IDs υπολογίζονται μία φορά — φθηνότερο από τη χρήση string σε κάθε κλήση.
        public static readonly int GlobalWetness = Shader.PropertyToID("_GS_Wetness");
        public static readonly int GlobalNightGlow = Shader.PropertyToID("_GS_NightGlow");
        public static readonly int GlobalWindStrength = Shader.PropertyToID("_GS_WindStrength");
        public static readonly int GlobalWindDir = Shader.PropertyToID("_GS_WindDir");

        public static Shader FindShader(string name)
        {
            Shader s = Shader.Find(name);
            if (s == null) s = Resources.Load<Shader>("Shaders/" + name.Replace("Greystone/", "Greystone"));
            if (s == null)
            {
                Debug.LogWarning("[Greystone] Δεν βρέθηκε ο shader " + name + " — χρήση Standard.");
                s = Shader.Find("Standard");
            }
            return s;
        }

        static Material New(string shader)
        {
            Material m = new Material(FindShader(shader));
            m.enableInstancing = true;   // απαραίτητο για Graphics.RenderMeshInstanced
            return m;
        }

        /// <summary>Γενικό υλικό Greystone/Lit.</summary>
        static Material Lit(Color c, Color c2, float noiseScale, float noiseAmount, float gloss, float metallic = 0f)
        {
            Material m = New("Greystone/Lit");
            m.SetColor("_Color", c);
            m.SetColor("_Color2", c2);
            m.SetFloat("_NoiseScale", noiseScale);
            m.SetFloat("_NoiseAmount", noiseAmount);
            m.SetFloat("_Glossiness", gloss);
            m.SetFloat("_Metallic", metallic);
            return m;
        }

        /// <summary>Απλό μονόχρωμο υλικό (cache ανά χρώμα) — για ρούχα χωρικών κ.λπ.</summary>
        public static Material Solid(Color c, float gloss = 0.15f)
        {
            string key = "solid_" + ColorUtility.ToHtmlStringRGB(c) + "_" + gloss.ToString("0.00");
            Material m;
            if (Cache.TryGetValue(key, out m)) return m;
            m = Lit(c, c * 0.85f, 3f, 0.12f, gloss);
            Cache[key] = m;
            return m;
        }

        /// <summary>Επιστρέφει (και δημιουργεί αν χρειάζεται) το κοινό υλικό με το δοσμένο όνομα.</summary>
        public static Material Get(string key)
        {
            Material m;
            if (Cache.TryGetValue(key, out m)) return m;
            m = Create(key);
            m.name = "GS_" + key;
            Cache[key] = m;
            return m;
        }

        static Material Create(string key)
        {
            Material m;
            switch (key)
            {
                // ---------------- Πέτρα (Cotswold stone) ----------------
                case "stone":
                    m = New("Greystone/Stone");
                    m.SetColor("_Color", new Color(0.76f, 0.68f, 0.52f));
                    m.SetColor("_Color2", new Color(0.66f, 0.63f, 0.57f));
                    return m;
                case "stone_grey":
                    m = New("Greystone/Stone");
                    m.SetColor("_Color", new Color(0.63f, 0.62f, 0.58f));
                    m.SetColor("_Color2", new Color(0.55f, 0.54f, 0.51f));
                    m.SetVector("_BlockSize", new Vector4(0.7f, 0.32f, 0, 0));
                    return m;
                case "stone_rubble":   // ξερολιθιά / χαμηλοί τοίχοι
                    m = New("Greystone/Stone");
                    m.SetColor("_Color", new Color(0.62f, 0.58f, 0.5f));
                    m.SetColor("_Color2", new Color(0.5f, 0.49f, 0.45f));
                    m.SetVector("_BlockSize", new Vector4(0.34f, 0.12f, 0, 0));
                    return m;
                // ---------------- Στέγες ----------------
                case "roof_slate":
                    m = New("Greystone/Roof");
                    return m;
                case "roof_thatch":
                    m = New("Greystone/Roof");
                    m.SetColor("_Color", new Color(0.62f, 0.52f, 0.32f));
                    m.SetColor("_Color2", new Color(0.45f, 0.37f, 0.22f));
                    m.SetFloat("_Thatch", 1f);
                    return m;
                // ---------------- Ξύλο / σοβάς ----------------
                case "timber":   // σκούρα δοκάρια
                    m = Lit(new Color(0.23f, 0.16f, 0.1f), new Color(0.14f, 0.09f, 0.06f), 6f, 0.5f, 0.12f);
                    m.SetVector("_Stretch", new Vector4(1f, 8f, 1f, 0));
                    return m;
                case "wood":     // φρέσκο ξύλο (πάγκοι, πόρτες)
                    m = Lit(new Color(0.47f, 0.33f, 0.2f), new Color(0.33f, 0.22f, 0.13f), 5f, 0.5f, 0.15f);
                    m.SetVector("_Stretch", new Vector4(1f, 7f, 1f, 0));
                    return m;
                case "wood_weathered":
                    m = Lit(new Color(0.42f, 0.38f, 0.32f), new Color(0.3f, 0.27f, 0.22f), 5f, 0.5f, 0.08f);
                    m.SetVector("_Stretch", new Vector4(1f, 7f, 1f, 0));
                    return m;
                case "plaster":
                    return Lit(new Color(0.9f, 0.86f, 0.77f), new Color(0.78f, 0.74f, 0.66f), 1.3f, 0.35f, 0.05f);
                case "straw":
                    return Lit(new Color(0.78f, 0.66f, 0.38f), new Color(0.6f, 0.5f, 0.28f), 8f, 0.5f, 0.05f);
                case "bark":
                    m = Lit(new Color(0.3f, 0.24f, 0.18f), new Color(0.2f, 0.16f, 0.12f), 4f, 0.6f, 0.05f);
                    m.SetVector("_Stretch", new Vector4(1f, 5f, 1f, 0));
                    return m;
                // ---------------- Μέταλλα ----------------
                case "iron":
                    return Lit(new Color(0.18f, 0.18f, 0.19f), new Color(0.1f, 0.1f, 0.1f), 8f, 0.4f, 0.45f, 0.8f);
                case "bronze":
                    return Lit(new Color(0.62f, 0.45f, 0.22f), new Color(0.35f, 0.3f, 0.2f), 3f, 0.35f, 0.6f, 0.9f);
                case "gold":
                    return Lit(new Color(0.9f, 0.72f, 0.3f), new Color(0.8f, 0.6f, 0.25f), 3f, 0.1f, 0.75f, 1f);
                // ---------------- Φως / γυαλί ----------------
                case "window":   // παράθυρα: σκούρα τη μέρα, φωτεινά (ζεστό φως κεριού) τη νύχτα
                    m = Lit(new Color(0.1f, 0.12f, 0.14f), new Color(0.15f, 0.17f, 0.2f), 4f, 0.3f, 0.85f);
                    m.SetColor("_EmissionColor", new Color(1.6f, 0.95f, 0.42f));
                    m.SetFloat("_NightGlow", 1f);
                    m.SetFloat("_WetResponse", 0f);
                    return m;
                case "stained_glass":
                    m = New("Greystone/Lit");
                    m.SetColor("_Color", new Color(0.25f, 0.12f, 0.35f));
                    m.SetColor("_Color2", new Color(0.1f, 0.3f, 0.5f));
                    m.SetFloat("_NoiseScale", 6f);
                    m.SetFloat("_NoiseAmount", 1f);
                    m.SetFloat("_Glossiness", 0.8f);
                    m.SetColor("_EmissionColor", new Color(0.5f, 0.3f, 0.2f));
                    m.SetFloat("_NightGlow", 1f);
                    return m;
                case "lantern_glass":
                    m = Lit(new Color(0.9f, 0.75f, 0.45f), new Color(0.8f, 0.6f, 0.4f), 3f, 0.2f, 0.6f);
                    m.SetColor("_EmissionColor", new Color(2.4f, 1.3f, 0.5f));
                    m.SetFloat("_NightGlow", 1f);
                    return m;
                case "ember":    // κάρβουνα καμινιού: πάντα πυρακτωμένα
                    m = Lit(new Color(0.2f, 0.05f, 0.02f), new Color(0.1f, 0.02f, 0.01f), 9f, 0.7f, 0.2f);
                    m.SetColor("_EmissionColor", new Color(2.2f, 0.6f, 0.12f));
                    return m;
                case "dark_interior":   // το σκοτεινό εσωτερικό (π.χ. πηγάδι, κούφωμα)
                    return Lit(new Color(0.02f, 0.02f, 0.02f), new Color(0.03f, 0.03f, 0.03f), 1f, 0f, 0.3f);
                case "well_water":
                    return Lit(new Color(0.05f, 0.1f, 0.12f), new Color(0.08f, 0.14f, 0.16f), 2f, 0.3f, 0.95f);
                // ---------------- Ζωντανά ----------------
                case "wool":
                    return Lit(new Color(0.9f, 0.88f, 0.82f), new Color(0.78f, 0.75f, 0.68f), 7f, 0.6f, 0.02f);
                case "sheep_face":
                    return Lit(new Color(0.15f, 0.13f, 0.12f), new Color(0.1f, 0.09f, 0.08f), 5f, 0.3f, 0.1f);
                case "skin":
                    return Lit(new Color(0.85f, 0.66f, 0.52f), new Color(0.78f, 0.58f, 0.46f), 5f, 0.2f, 0.25f);
                case "horse":
                    return Lit(new Color(0.36f, 0.23f, 0.14f), new Color(0.28f, 0.18f, 0.1f), 4f, 0.3f, 0.25f);
                case "hair_dark":
                    return Lit(new Color(0.12f, 0.08f, 0.05f), new Color(0.08f, 0.05f, 0.03f), 10f, 0.4f, 0.2f);
                case "leather":
                    return Lit(new Color(0.3f, 0.19f, 0.11f), new Color(0.22f, 0.14f, 0.08f), 6f, 0.4f, 0.3f);
                // ---------------- Διάφορα ----------------
                case "rope":
                    return Lit(new Color(0.6f, 0.52f, 0.36f), new Color(0.45f, 0.38f, 0.25f), 20f, 0.6f, 0.05f);
                case "clock_face":
                    return Lit(new Color(0.9f, 0.88f, 0.8f), new Color(0.8f, 0.78f, 0.7f), 2f, 0.2f, 0.3f);
                case "apple":
                    return Lit(new Color(0.65f, 0.1f, 0.06f), new Color(0.75f, 0.45f, 0.1f), 6f, 0.7f, 0.55f);
                case "cabbage":
                    return Lit(new Color(0.4f, 0.58f, 0.3f), new Color(0.6f, 0.72f, 0.45f), 8f, 0.6f, 0.3f);
                case "bread":
                    return Lit(new Color(0.72f, 0.5f, 0.26f), new Color(0.55f, 0.35f, 0.16f), 6f, 0.6f, 0.2f);
                case "cloth_red":
                    m = New("Greystone/Cloth");
                    m.SetColor("_Color", new Color(0.62f, 0.14f, 0.1f));
                    m.SetColor("_Color2", new Color(0.88f, 0.83f, 0.7f));
                    return m;
                case "cloth_blue":
                    m = New("Greystone/Cloth");
                    m.SetColor("_Color", new Color(0.16f, 0.26f, 0.5f));
                    m.SetColor("_Color2", new Color(0.88f, 0.83f, 0.7f));
                    return m;
                case "cloth_green":
                    m = New("Greystone/Cloth");
                    m.SetColor("_Color", new Color(0.22f, 0.4f, 0.2f));
                    m.SetColor("_Color2", new Color(0.86f, 0.8f, 0.6f));
                    return m;
                case "rock":
                    m = New("Greystone/Stone");
                    m.SetColor("_Color", new Color(0.5f, 0.49f, 0.46f));
                    m.SetColor("_Color2", new Color(0.42f, 0.41f, 0.38f));
                    m.SetColor("_Mortar", new Color(0.46f, 0.45f, 0.42f));
                    m.SetVector("_BlockSize", new Vector4(3f, 2f, 0, 0));
                    return m;
                // ---------------- Βλάστηση ----------------
                case "grass":
                    m = New("Greystone/Foliage");
                    m.SetColor("_Color", new Color(0.18f, 0.28f, 0.08f));
                    m.SetColor("_TipColor", new Color(0.45f, 0.55f, 0.2f));
                    m.SetFloat("_Height", 0.5f);
                    m.SetFloat("_Sway", 0.25f);
                    return m;
                case "wheat":
                    m = New("Greystone/Foliage");
                    m.SetColor("_Color", new Color(0.55f, 0.48f, 0.22f));
                    m.SetColor("_TipColor", new Color(0.9f, 0.76f, 0.38f));
                    m.SetFloat("_Height", 1.05f);
                    m.SetFloat("_Sway", 0.35f);
                    m.SetFloat("_Variation", 0.15f);
                    return m;
                case "barley":
                    m = New("Greystone/Foliage");
                    m.SetColor("_Color", new Color(0.4f, 0.48f, 0.22f));
                    m.SetColor("_TipColor", new Color(0.72f, 0.74f, 0.42f));
                    m.SetFloat("_Height", 0.85f);
                    m.SetFloat("_Sway", 0.35f);
                    m.SetFloat("_Variation", 0.15f);
                    return m;
                case "leaves":
                    m = New("Greystone/Foliage");
                    m.SetColor("_Color", new Color(0.13f, 0.24f, 0.08f));
                    m.SetColor("_TipColor", new Color(0.3f, 0.45f, 0.16f));
                    m.SetFloat("_Height", 9f);
                    m.SetFloat("_Sway", 0.18f);
                    m.SetFloat("_UpNormals", 0f);
                    m.SetFloat("_Variation", 0.3f);
                    return m;
                case "conifer":
                    m = New("Greystone/Foliage");
                    m.SetColor("_Color", new Color(0.07f, 0.16f, 0.09f));
                    m.SetColor("_TipColor", new Color(0.15f, 0.28f, 0.14f));
                    m.SetFloat("_Height", 12f);
                    m.SetFloat("_Sway", 0.12f);
                    m.SetFloat("_UpNormals", 0f);
                    m.SetFloat("_Variation", 0.25f);
                    return m;
                case "orchard_leaves":
                    m = New("Greystone/Foliage");
                    m.SetColor("_Color", new Color(0.2f, 0.32f, 0.1f));
                    m.SetColor("_TipColor", new Color(0.4f, 0.55f, 0.2f));
                    m.SetFloat("_Height", 5f);
                    m.SetFloat("_Sway", 0.18f);
                    m.SetFloat("_UpNormals", 0f);
                    return m;
                case "bird":
                    return New("Greystone/Bird");
                // ---------------- Ειδικά ----------------
                case "terrain":
                    return New("Greystone/Terrain");
                case "water":
                    m = New("Greystone/Water");
                    m.enableInstancing = false;
                    return m;
                case "text":
                    m = new Material(FindShader("Greystone/Text3D"));
                    return m;
            }

            Debug.LogWarning("[Greystone] Άγνωστο υλικό: " + key);
            return Solid(Color.magenta);
        }

        /// <summary>Υλικό σωματιδίων: additive=true για φωτιά/σπίθες, false για καπνό/βροχή.</summary>
        public static Material Particle(string key, Color tint, bool additive, float softness, bool streak = false)
        {
            Material m;
            if (Cache.TryGetValue("p_" + key, out m)) return m;
            m = new Material(FindShader("Greystone/Particle"));
            m.SetColor("_Color", tint);
            m.SetFloat("_Softness", softness);
            m.SetFloat("_Streak", streak ? 1f : 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.renderQueue = 3000;
            Cache["p_" + key] = m;
            return m;
        }
    }
}
