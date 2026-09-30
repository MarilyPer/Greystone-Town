// =====================================================================================
//  TextUtil.cs — Κείμενο μέσα στον τρισδιάστατο κόσμο
//  ---------------------------------------------------------------------------------
//  Δημιουργεί TextMesh (3D κείμενο) με την ενσωματωμένη δυναμική γραμματοσειρά του Unity.
//  Η δυναμική γραμματοσειρά χρησιμοποιεί τις γραμματοσειρές του λειτουργικού ως εφεδρεία,
//  οπότε αποδίδονται σωστά οι ελληνικοί χαρακτήρες.
//
//  Το TextMesh δεν αναδιπλώνει αυτόματα τις γραμμές, γι' αυτό υπάρχει η Wrap(). Επίσης, η
//  υφή της δυναμικής γραμματοσειράς ξαναφτιάχνεται όταν προστίθενται νέοι χαρακτήρες —
//  ακούμε το Font.textureRebuilt ώστε το υλικό μας να δείχνει πάντα στη σωστή υφή.
// =====================================================================================
using System.Text;
using UnityEngine;

namespace Greystone
{
    public static class TextUtil
    {
        static Font _font;
        static Material _mat;

        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
                // Unity 2022.2+: "LegacyRuntime.ttf", παλαιότερες εκδόσεις: "Arial.ttf"
                string v = Application.unityVersion;
                int major = 0, minor = 0;
                string[] parts = v.Split('.');
                if (parts.Length > 1) { int.TryParse(parts[0], out major); int.TryParse(parts[1], out minor); }
                bool legacyRuntime = major > 2022 || (major == 2022 && minor >= 2);   // π.χ. "6000.0.x" (Unity 6) -> major = 6000
                _font = Resources.GetBuiltinResource<Font>(legacyRuntime ? "LegacyRuntime.ttf" : "Arial.ttf");
                if (_font == null) _font = Resources.GetBuiltinResource<Font>(legacyRuntime ? "Arial.ttf" : "LegacyRuntime.ttf");
                Font.textureRebuilt += OnRebuilt;
                return _font;
            }
        }

        static void OnRebuilt(Font f)
        {
            if (f == _font && _mat != null) _mat.mainTexture = f.material.mainTexture;
        }

        static Material Material
        {
            get
            {
                if (_mat == null)
                {
                    _mat = Mats.Get("text");
                    _mat.mainTexture = Font.material.mainTexture;
                }
                return _mat;
            }
        }

        /// <summary>Δημιουργεί 3D κείμενο. height = ύψος γραμμής σε μέτρα.</summary>
        public static TextMesh Create(Transform parent, string text, Vector3 localPos, float height, Color color, TextAnchor anchor = TextAnchor.UpperLeft, FontStyle style = FontStyle.Normal)
        {
            GameObject go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.layer = Geo.LayerSmallProps;
            TextMesh tm = go.AddComponent<TextMesh>();
            tm.font = Font;
            tm.fontSize = 64;
            tm.characterSize = height * 10f / 64f;   // characterSize: μέγεθος σε μονάδες ανά «10 pixel» γραμματοσειράς
            tm.anchor = anchor;
            tm.alignment = anchor == TextAnchor.UpperCenter || anchor == TextAnchor.MiddleCenter ? TextAlignment.Center : TextAlignment.Left;
            tm.fontStyle = style;
            tm.color = color;
            tm.text = text;
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = Material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return tm;
        }

        /// <summary>Αναδίπλωση κειμένου σε γραμμές το πολύ maxChars χαρακτήρων (σε όρια λέξεων).</summary>
        public static string Wrap(string text, int maxChars)
        {
            StringBuilder sb = new StringBuilder();
            foreach (string paragraph in text.Split('\n'))
            {
                int line = 0;
                foreach (string word in paragraph.Split(' '))
                {
                    if (line > 0 && line + word.Length + 1 > maxChars)
                    {
                        sb.Append('\n');
                        line = 0;
                    }
                    if (line > 0) { sb.Append(' '); line++; }
                    sb.Append(word);
                    line += word.Length;
                }
                sb.Append('\n');
            }
            return sb.ToString().TrimEnd('\n');
        }
    }
}
