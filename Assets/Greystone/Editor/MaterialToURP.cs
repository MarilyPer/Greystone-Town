// =====================================================================================
//  MaterialToURP.cs — Μετατροπή υλικών Built-in «Standard» σε URP «Lit»
//  ---------------------------------------------------------------------------------
//  Μερικά πακέτα (3DForge, CobraGames, living birds, barell…) είναι φτιαγμένα για το Built-in
//  pipeline: τα υλικά τους χρησιμοποιούν τον shader «Standard», που στο URP εμφανίζεται ΡΟΖ.
//  Εδώ αλλάζουμε τον shader σε «Universal Render Pipeline/Lit» και μεταφέρουμε τις ιδιότητες
//  που έχουν διαφορετικό όνομα (_MainTex→_BaseMap, _Color→_BaseColor, _Glossiness→_Smoothness)
//  καθώς και τον τρόπο απόδοσης (Opaque / Cutout / Transparent).
//
//  (Ισοδύναμο εργαλείο του Unity: Window → Rendering → Render Pipeline Converter.)
// =====================================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Greystone.EditorTools
{
    public static class MaterialToURP
    {
        [MenuItem("Greystone/1. Μετατροπή υλικών Built-in σε URP")]
        public static void ConvertAll()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
            {
                Debug.LogError("[Greystone] Δεν βρέθηκε ο shader URP/Lit — είναι εγκατεστημένο το URP;");
                return;
            }
            int converted = 0;
            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (i % 50 == 0) EditorUtility.DisplayProgressBar("Μετατροπή υλικών σε URP", path, i / (float)guids.Length);
                    Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (m == null || m.shader == null) continue;
                    string sn = m.shader.name;
                    if (sn != "Standard" && sn != "Standard (Specular setup)" && sn != "Legacy Shaders/Diffuse" && sn != "Legacy Shaders/Bumped Diffuse") continue;
                    Convert(m, lit);
                    converted++;
                }
            }
            finally { EditorUtility.ClearProgressBar(); }
            AssetDatabase.SaveAssets();
            Debug.Log("[Greystone] Μετατράπηκαν " + converted + " υλικά σε URP/Lit.");
        }

        static void Convert(Material m, Shader lit)
        {
            // Διαβάζουμε ΠΡΙΝ την αλλαγή shader (μετά χάνονται οι ιδιότητες με άλλο όνομα)
            Texture mainTex = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
            Vector2 tiling = m.HasProperty("_MainTex") ? m.GetTextureScale("_MainTex") : Vector2.one;
            Vector2 offset = m.HasProperty("_MainTex") ? m.GetTextureOffset("_MainTex") : Vector2.zero;
            Color color = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
            float gloss = m.HasProperty("_Glossiness") ? m.GetFloat("_Glossiness") : 0.3f;
            float mode = m.HasProperty("_Mode") ? m.GetFloat("_Mode") : 0f;
            float cutoff = m.HasProperty("_Cutoff") ? m.GetFloat("_Cutoff") : 0.5f;
            bool emission = m.IsKeywordEnabled("_EMISSION");

            Undo.RecordObject(m, "Convert to URP");
            m.shader = lit;
            m.SetTexture("_BaseMap", mainTex);
            m.SetTextureScale("_BaseMap", tiling);
            m.SetTextureOffset("_BaseMap", offset);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", gloss);
            if (m.GetTexture("_BumpMap") != null) m.EnableKeyword("_NORMALMAP");
            if (m.GetTexture("_MetallicGlossMap") != null) m.EnableKeyword("_METALLICSPECGLOSSMAP");
            if (m.GetTexture("_OcclusionMap") != null) m.EnableKeyword("_OCCLUSIONMAP");
            if (emission)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            if (mode == 1f)   // Cutout
            {
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", cutoff);
                m.EnableKeyword("_ALPHATEST_ON");
                m.renderQueue = (int)RenderQueue.AlphaTest;
            }
            else if (mode >= 2f)   // Fade / Transparent
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            m.enableInstancing = true;   // επιτρέπει GPU instancing των επαναλαμβανόμενων αντικειμένων
            EditorUtility.SetDirty(m);
        }
    }
}
#endif
