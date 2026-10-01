// =====================================================================================
//  VillageHud.cs — Απλή διεπαφή χρήστη (IMGUI)
//  ---------------------------------------------------------------------------------
//  * Ρολόι (ημέρα, ώρα, μέρα/νύχτα, ταχύτητα χρόνου).
//  * Σταυρόνημα και οδηγία αλληλεπίδρασης «[E] Μιλήστε με …».
//  * Διάλογοι κατοίκων και σύντομες ειδοποιήσεις που σβήνουν σταδιακά.
//  * Βοήθεια με τα πλήκτρα (H).
//  Σχεδιάζεται σε εικονική ανάλυση 1920x1080 και κλιμακώνεται σε κάθε οθόνη.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Greystone
{
    public class VillageHud : MonoBehaviour
    {
        static VillageHud _instance;

        public FirstPersonController player;
        public bool showHelp = true;

        class Line { public string text; public float until; }
        readonly List<Line> _lines = new List<Line>();
        GUIStyle _box, _text, _small, _prompt;
        Texture2D _bg, _white;

        void Awake()
        {
            _instance = this;
            if (player == null) player = Find.First<FirstPersonController>();
        }

        /// <summary>Φράση κατοίκου (εμφανίζεται για λίγα δευτερόλεπτα).</summary>
        public static void Say(string who, string text)
        {
            Notify("<b>" + who + ":</b> «" + text + "»", 6f);
        }

        public static void Notify(string text, float seconds = 3.5f)
        {
            if (_instance == null) return;
            _instance._lines.Add(new Line { text = text, until = Time.time + seconds });
            if (_instance._lines.Count > 4) _instance._lines.RemoveAt(0);
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame) showHelp = !showHelp;
        }

        void Styles()
        {
            _bg = new Texture2D(1, 1);
            _bg.SetPixel(0, 0, new Color(0.08f, 0.06f, 0.05f, 0.78f));
            _bg.Apply();
            _white = Texture2D.whiteTexture;
            _box = new GUIStyle(GUI.skin.box) { padding = new RectOffset(18, 18, 12, 12) };
            _box.normal.background = _bg;
            _text = new GUIStyle(GUI.skin.label) { fontSize = 24, richText = true, wordWrap = true };
            _text.normal.textColor = new Color(0.95f, 0.92f, 0.84f);
            _small = new GUIStyle(_text) { fontSize = 19 };
            _prompt = new GUIStyle(_text) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        }

        void OnGUI()
        {
            if (_box == null) Styles();
            float s = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float W = Screen.width / s;

            // ---------------- Ρολόι ----------------
            DayNightCycle dn = DayNightCycle.Instance;
            if (dn != null)
            {
                GUILayout.BeginArea(new Rect(20, 20, 420, 90), _box);
                GUILayout.Label("Ημέρα " + dn.day + "  ·  " + dn.ClockText + "  ·  " + (dn.IsNight ? "Νύχτα" : "Μέρα"), _text);
                GUILayout.Label("Χρόνος ×" + dn.SpeedMultiplier + "   [T] αλλαγή", _small);
                GUILayout.EndArea();
            }

            // ---------------- Σταυρόνημα & οδηγία ----------------
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                GUI.DrawTexture(new Rect(W * 0.5f - 3, 540 - 3, 6, 6), _white);
                if (player != null && player.Focus != null)
                {
                    string t = "[E]  " + player.Focus.Prompt;
                    Vector2 size = _prompt.CalcSize(new GUIContent(t));
                    Rect r = new Rect(W * 0.5f - size.x * 0.5f - 18, 580, size.x + 36, 46);
                    GUI.Box(r, GUIContent.none, _box);
                    GUI.Label(r, t, _prompt);
                }
            }

            // ---------------- Διάλογοι / ειδοποιήσεις ----------------
            float y = 1080 - 40;
            for (int i = _lines.Count - 1; i >= 0; i--)
            {
                if (Time.time > _lines[i].until) { _lines.RemoveAt(i); continue; }
                float h = _text.CalcHeight(new GUIContent(_lines[i].text), 760) + 24;
                y -= h;
                GUI.Box(new Rect(20, y, 800, h), GUIContent.none, _box);
                GUI.Label(new Rect(38, y + 12, 760, h), _lines[i].text, _text);
                y -= 8;
            }

            // ---------------- Βοήθεια ----------------
            if (showHelp)
            {
                GUILayout.BeginArea(new Rect(W - 440, 20, 420, 300), _box);
                GUILayout.Label("<b>Χειρισμός</b>", _text);
                GUILayout.Label("W A S D — κίνηση · Shift — τρέξιμο\nSpace — άλμα · Ποντίκι — ματιά\nE — μιλήστε με κατοίκους\nF — φανάρι · T — ταχύτητα χρόνου\nEsc — δρομέας · H — απόκρυψη βοήθειας", _small);
                GUILayout.EndArea();
            }
        }
    }
}
