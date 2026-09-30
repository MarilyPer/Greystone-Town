// =====================================================================================
//  Hud.cs — Διεπαφή χρήστη (IMGUI)
//  ---------------------------------------------------------------------------------
//  Χρησιμοποιούμε το Immediate Mode GUI του Unity (OnGUI) ώστε όλη η διεπαφή να είναι
//  κώδικας, χωρίς prefabs/canvases. Περιέχει:
//   * οθόνη φόρτωσης,                         * σταυρόνημα και οδηγία αλληλεπίδρασης,
//   * ρολόι/ημέρα/καιρό/ταχύτητα χρόνου,        * πυξίδα,
//   * ειδοποιήσεις που σβήνουν σταδιακά,         * παράθυρο πληροφοριών με ΠΗΓΕΣ (κλικ = άνοιγμα),
//   * χάρτη με τηλεμεταφορά σε σημεία ενδιαφέροντος (ο χάρτης «ζωγραφίζεται» ΜΙΑ φορά σε υφή),
//   * overlay απόδοσης (F3) με μετρήσεις και κατάσταση βελτιστοποιήσεων,
//   * μενού παύσης με ρυθμίσεις (ευαισθησία ποντικιού, ένταση, ώρα, καιρός).
//
//  Κλίμακα: όλα σχεδιάζονται σε «εικονική» ανάλυση 1920x1080 και κλιμακώνονται με GUI.matrix,
//  ώστε η διεπαφή να έχει ίδιο μέγεθος σε κάθε οθόνη.
// =====================================================================================
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace Greystone
{
    public class Hud : MonoBehaviour
    {
        public static Hud Instance { get; private set; }

        public struct Poi { public string name; public Vector3 pos; public float yaw; }
        public readonly List<Poi> Pois = new List<Poi>();

        // ---------------- Κατάσταση ----------------
        public bool Loading = true;
        float _loadProgress;
        string _loadText = "";
        LoreEntry _lore;
        bool _map, _help, _perf, _pause;
        Vector2 _loreScroll;

        class Note { public string text; public float time; public float duration; }
        readonly List<Note> _notes = new List<Note>();

        // ---------------- Στυλ ----------------
        GUIStyle _panel, _title, _text, _small, _prompt, _center, _button, _link, _big;
        Texture2D _bg, _bgDark, _white, _mapTex;
        bool _stylesReady;
        const float RefW = 1920f, RefH = 1080f;

        // ---------------- Απόδοση ----------------
        ProfilerRecorder _drawCalls, _batches, _setPass, _tris;
        float _fps, _fpsTimer;
        int _frames;

        public bool IsModal { get { return Loading || _lore != null || _map || _pause; } }

        void Awake()
        {
            Instance = this;
            _perf = false;
        }

        void OnEnable()
        {
            _drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            _batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            _setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            _tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
        }

        void OnDisable()
        {
            _drawCalls.Dispose();
            _batches.Dispose();
            _setPass.Dispose();
            _tris.Dispose();
        }

        // ================================================================== Δημόσιο API
        public void SetLoading(float progress, string text)
        {
            _loadProgress = progress;
            _loadText = text;
        }

        public void Notify(string text, float duration = 4.5f)
        {
            _notes.Add(new Note { text = text, time = Time.unscaledTime, duration = duration });
            if (_notes.Count > 5) _notes.RemoveAt(0);
        }

        public void ShowLore(LoreEntry e)
        {
            _lore = e;
            _loreScroll = Vector2.zero;
            if (SoundBank.Instance != null) SoundBank.Instance.PlayUI("ui");
        }

        public void ToggleMap() { if (!Loading) { _map = !_map; _lore = null; } }
        public void ToggleHelp() { _help = !_help; }
        public void TogglePerf() { _perf = !_perf; }

        /// <summary>Esc: κλείνει ό,τι είναι ανοιχτό, αλλιώς ανοίγει/κλείνει το μενού παύσης.</summary>
        public void Escape()
        {
            if (_lore != null) { _lore = null; return; }
            if (_map) { _map = false; return; }
            if (_help) { _help = false; return; }
            _pause = !_pause;
        }

        public void CloseLore() { _lore = null; }

        void Update()
        {
            // Μέτρηση FPS (μέσος όρος ανά 0.5 s)
            _frames++;
            _fpsTimer += Time.unscaledDeltaTime;
            if (_fpsTimer >= 0.5f)
            {
                _fps = _frames / _fpsTimer;
                _frames = 0;
                _fpsTimer = 0f;
            }
            Time.timeScale = _pause ? 0f : 1f;
            // Ο δρομέας εμφανίζεται μόνο όταν υπάρχει ανοιχτό «modal» παράθυρο.
            bool modal = IsModal;
            Cursor.lockState = modal ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = modal;
            if (PlayerController.Instance != null) PlayerController.Instance.InputEnabled = !modal;
        }

        // ================================================================== Στυλ
        static Texture2D Tex(Color c)
        {
            Texture2D t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        void InitStyles()
        {
            _bg = Tex(new Color(0.09f, 0.075f, 0.06f, 0.82f));
            _bgDark = Tex(new Color(0.04f, 0.035f, 0.03f, 0.94f));
            _white = Tex(Color.white);
            _panel = new GUIStyle(GUI.skin.box) { padding = new RectOffset(22, 22, 18, 18) };
            _panel.normal.background = _bg;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, wordWrap = true };
            _title.normal.textColor = new Color(0.96f, 0.84f, 0.55f);
            _big = new GUIStyle(_title) { fontSize = 56, alignment = TextAnchor.MiddleCenter };
            _text = new GUIStyle(GUI.skin.label) { fontSize = 22, wordWrap = true, richText = true };
            _text.normal.textColor = new Color(0.93f, 0.9f, 0.82f);
            _small = new GUIStyle(_text) { fontSize = 18 };
            _link = new GUIStyle(_small);
            _link.normal.textColor = new Color(0.62f, 0.8f, 1f);
            _link.hover.textColor = new Color(0.85f, 0.93f, 1f);
            _prompt = new GUIStyle(_text) { fontSize = 24, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _center = new GUIStyle(_text) { alignment = TextAnchor.MiddleCenter };
            _button = new GUIStyle(GUI.skin.button) { fontSize = 20, padding = new RectOffset(12, 12, 8, 8) };
            _stylesReady = true;
        }

        // ================================================================== Σχεδίαση
        void OnGUI()
        {
            if (!_stylesReady) InitStyles();
            float s = Screen.height / RefH;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
            float W = Screen.width / s;   // πλάτος σε εικονικές μονάδες (για οθόνες διαφορετικής αναλογίας)

            if (Loading) { DrawLoading(W); return; }

            DrawClock();
            DrawCompass(W);
            DrawNotes();
            if (!IsModal) DrawCrosshairAndPrompt(W);
            if (_help) DrawHelp(W);
            if (_perf) DrawPerf(W);
            if (_lore != null) DrawLore(W);
            if (_map) DrawMap(W);
            if (_pause) DrawPause(W);
        }

        void DrawLoading(float W)
        {
            GUI.DrawTexture(new Rect(0, 0, W, RefH), _bgDark);
            GUI.Label(new Rect(0, 330, W, 90), "Greystone", _big);
            GUI.Label(new Rect(0, 420, W, 40), "Ένα χωριό των Cotswolds, περίπου 1400 μ.Χ.", _center);
            Rect bar = new Rect(W * 0.5f - 400, 540, 800, 18);
            GUI.DrawTexture(bar, Tex2(0.2f));
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(_loadProgress), bar.height), Tex2(0.85f));
            GUI.Label(new Rect(0, 575, W, 40), _loadText, _center);
            GUI.Label(new Rect(0, 900, W, 40), "Όλος ο κόσμος (γεωμετρία, υφές, ήχοι, μουσική) δημιουργείται διαδικαστικά από κώδικα.", _center);
        }

        Texture2D _grey20, _grey85;
        Texture2D Tex2(float g)
        {
            if (g < 0.5f) { if (_grey20 == null) _grey20 = Tex(new Color(0.25f, 0.2f, 0.15f)); return _grey20; }
            if (_grey85 == null) _grey85 = Tex(new Color(0.9f, 0.75f, 0.45f));
            return _grey85;
        }

        void DrawClock()
        {
            TimeManager tm = TimeManager.Instance;
            WeatherSystem w = WeatherSystem.Instance;
            GUILayout.BeginArea(new Rect(24, 20, 460, 150), _panel);
            GUILayout.Label("Ημέρα " + tm.day + "  ·  " + tm.ClockText + "  ·  " + tm.PartOfDay, _text);
            GUILayout.Label("Καιρός: " + (w != null ? w.StateName : "-") + "   ·   Χρόνος ×" + (int)tm.TimeScale + "  [T]", _small);
            GUILayout.Label("H: βοήθεια   M: χάρτης   F3: απόδοση   Esc: μενού", _small);
            GUILayout.EndArea();
        }

        void DrawCompass(float W)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            float yaw = cam.transform.eulerAngles.y;
            Rect r = new Rect(W * 0.5f - 300, 20, 600, 44);
            GUI.Box(r, GUIContent.none, _panel);
            string[] names = { "Β", "ΒΑ", "Α", "ΝΑ", "Ν", "ΝΔ", "Δ", "ΒΔ" };
            for (int i = 0; i < 8; i++)
            {
                float a = Mathf.DeltaAngle(yaw, i * 45f);
                if (Mathf.Abs(a) > 75f) continue;
                float x = r.center.x + a / 75f * 280f;
                GUIStyle st = i % 2 == 0 ? _prompt : _small;
                GUI.Label(new Rect(x - 30, r.y + 6, 60, 32), names[i], st);
            }
            GUI.DrawTexture(new Rect(r.center.x - 1, r.y + 2, 2, 8), _white);
        }

        void DrawCrosshairAndPrompt(float W)
        {
            GUI.DrawTexture(new Rect(W * 0.5f - 3, RefH * 0.5f - 3, 6, 6), _white);
            PlayerController p = PlayerController.Instance;
            if (p != null && p.Focus != null)
            {
                string text = "[E]  " + p.Focus.Prompt;
                Vector2 size = _prompt.CalcSize(new GUIContent(text));
                Rect r = new Rect(W * 0.5f - size.x * 0.5f - 20, RefH * 0.5f + 40, size.x + 40, 48);
                GUI.Box(r, GUIContent.none, _panel);
                GUI.Label(r, text, _prompt);
            }
        }

        void DrawNotes()
        {
            float y = RefH - 90;
            for (int i = _notes.Count - 1; i >= 0; i--)
            {
                Note n = _notes[i];
                float age = Time.unscaledTime - n.time;
                if (age > n.duration) { _notes.RemoveAt(i); continue; }
                float alpha = Mathf.Clamp01((n.duration - age) / 0.8f);
                GUI.color = new Color(1, 1, 1, alpha);
                float h = _text.CalcHeight(new GUIContent(n.text), 820) + 24;
                Rect r = new Rect(24, y - h, 860, h);
                GUI.Box(r, GUIContent.none, _panel);
                GUI.Label(new Rect(r.x + 18, r.y + 12, 820, h), n.text, _text);
                y -= h + 8;
                GUI.color = Color.white;
            }
        }

        void DrawHelp(float W)
        {
            GUILayout.BeginArea(new Rect(W - 560, 90, 536, 640), _panel);
            GUILayout.Label("Χειρισμός", _title);
            GUILayout.Label(
                "W A S D — κίνηση\nShift — τρέξιμο   ·   Space — άλμα\nΠοντίκι — ματιά\n" +
                "E — αλληλεπίδραση (πόρτες, μοχλός, καμπάνα, πηγάδι, φυσερό, πινακίδες, χωρικοί)\n" +
                "Ροδέλα — ένταση λύρας (όταν την κοιτάτε)\nF — φανάρι χειρός\n" +
                "M — χάρτης & τηλεμεταφορά\nT — ταχύτητα χρόνου   ·   R — αλλαγή καιρού\n" +
                "F3 — δείκτες απόδοσης\nF4 — βελτιστοποίηση βλάστησης on/off\nF5 — αποκοπή μικρών αντικειμένων on/off\n" +
                "F6 — αραίωση ενημερώσεων AI on/off\nEsc — μενού / κλείσιμο παραθύρων", _small);
            GUILayout.EndArea();
        }

        void DrawPerf(float W)
        {
            GUILayout.BeginArea(new Rect(24, 190, 520, 470), _panel);
            GUILayout.Label("Απόδοση", _title);
            GUILayout.Label(string.Format("FPS: {0:0}   ({1:0.0} ms/frame)", _fps, 1000f / Mathf.Max(_fps, 0.01f)), _text);
            GUILayout.Label("Draw calls: " + Val(_drawCalls) + "   ·   Batches: " + Val(_batches) + "   ·   SetPass: " + Val(_setPass), _small);
            GUILayout.Label("Τρίγωνα: " + Val(_tris), _small);
            GUILayout.Label("Βλάστηση: " + Vegetation.InstancesDrawn + " / " + Vegetation.TotalInstances + " στιγμιότυπα σε " + Vegetation.BatchesDrawn + " instanced κλήσεις", _small);
            GUILayout.Label("Agents: " + UpdateScheduler.AgentCount + "   ·   AgentUpdate/δευτ.: " + UpdateScheduler.UpdatesPerSecond, _small);
            GUILayout.Label("Σημειακά φώτα ενεργά: " + LightLod.ActiveCount + " / " + LightLod.TotalCount, _small);
            GUILayout.Space(8);
            GUILayout.Label("[F4] Frustum culling + LOD βλάστησης: " + OnOff(Vegetation.OptimizationsEnabled), _small);
            GUILayout.Label("[F5] Απόσταση αποκοπής μικρών αντικειμένων: " + OnOff(GameBootstrap.LayerCullingEnabled), _small);
            GUILayout.Label("[F6] Αραίωση ενημερώσεων (time-slicing): " + OnOff(UpdateScheduler.ThrottlingEnabled), _small);
            GUILayout.Label("Static batching & LOD Groups κτιρίων: πάντα ενεργά", _small);
            GUILayout.EndArea();
        }

        static string Val(ProfilerRecorder r)
        {
            return r.Valid ? r.LastValue.ToString() : "n/a (μόνο σε Editor/Development build)";
        }

        static string OnOff(bool b) { return b ? "<color=#9f9>ΕΝΕΡΓΟ</color>" : "<color=#f88>ΑΝΕΝΕΡΓΟ</color>"; }

        void DrawLore(float W)
        {
            Rect r = new Rect(W * 0.5f - 520, 110, 1040, 820);
            GUI.Box(r, GUIContent.none, _panel);
            GUILayout.BeginArea(new Rect(r.x + 30, r.y + 24, r.width - 60, r.height - 48));
            GUILayout.Label(_lore.title, _title);
            GUILayout.Space(10);
            _loreScroll = GUILayout.BeginScrollView(_loreScroll, GUILayout.Height(600));
            GUILayout.Label(_lore.body, _text);
            GUILayout.Space(16);
            GUILayout.Label("Πηγές (κλικ για άνοιγμα):", _small);
            foreach (string src in _lore.sources)
            {
                if (GUILayout.Button("• " + src, _link))
                {
                    int idx = src.IndexOf("http");
                    if (idx >= 0) Application.OpenURL(src.Substring(idx).Trim());
                }
            }
            GUILayout.EndScrollView();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Κλείσιμο (Esc)", _button, GUILayout.Width(240))) _lore = null;
            GUILayout.EndArea();
        }

        // ------------------------------------------------------------------ Χάρτης
        /// <summary>
        /// Δημιουργεί ΜΙΑ φορά μια υφή 256x256 με την κάτοψη του κόσμου (ύψος, νερό, δρόμοι,
        /// χωράφια, κτίρια). Ο χάρτης δεν ξαναζωγραφίζεται σε κάθε frame (βελτιστοποίηση).
        /// </summary>
        public void BuildMapTexture()
        {
            const int N = 256;
            _mapTex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            _mapTex.wrapMode = TextureWrapMode.Clamp;
            Color[] px = new Color[N * N];
            for (int j = 0; j < N; j++)
                for (int i = 0; i < N; i++)
                {
                    float x = -WorldLayout.Half + (i + 0.5f) / N * WorldLayout.Size;
                    float z = -WorldLayout.Half + (j + 0.5f) / N * WorldLayout.Size;
                    Vector2 p = new Vector2(x, z);
                    float h = WorldLayout.HeightAt(x, z);
                    Color c = Color.Lerp(new Color(0.42f, 0.52f, 0.28f), new Color(0.3f, 0.36f, 0.22f), Mathf.InverseLerp(0f, 40f, h));
                    // Σκίαση ανάγλυφου (hillshade) από τη διαφορά ύψους
                    float hx = WorldLayout.HeightAt(x + 2f, z) - h;
                    c *= Mathf.Clamp(1f - hx * 0.25f, 0.6f, 1.25f);
                    if (WorldLayout.InField(p)) c = new Color(0.75f, 0.63f, 0.35f);
                    if (WorldLayout.DistanceToRoads(p) < WorldLayout.RoadHalfWidth + 0.5f || Vector2.Distance(p, WorldLayout.PlazaCenter) < WorldLayout.PlazaRadius) c = new Color(0.72f, 0.64f, 0.5f);
                    if (h < WorldLayout.WaterLevel) c = new Color(0.2f, 0.38f, 0.55f);
                    if (WorldLayout.InFootprint(p, -0.3f)) c = new Color(0.45f, 0.3f, 0.22f);
                    c.a = 1f;
                    px[j * N + i] = c;
                }
            _mapTex.SetPixels(px);
            _mapTex.Apply();
        }

        void DrawMap(float W)
        {
            Rect r = new Rect(W * 0.5f - 700, 80, 1400, 920);
            GUI.Box(r, GUIContent.none, _panel);
            Rect mapRect = new Rect(r.x + 30, r.y + 30, 860, 860);
            if (_mapTex != null) GUI.DrawTexture(mapRect, _mapTex);

            // Σημεία ενδιαφέροντος και θέση παίκτη (το +z του κόσμου = πάνω στον χάρτη)
            foreach (Poi poi in Pois)
            {
                Vector2 mp = WorldToMap(poi.pos, mapRect);
                GUI.DrawTexture(new Rect(mp.x - 4, mp.y - 4, 8, 8), Tex2(0.85f));
            }
            PlayerController pl = PlayerController.Instance;
            if (pl != null)
            {
                Vector2 mp = WorldToMap(pl.transform.position, mapRect);
                Matrix4x4 old = GUI.matrix;
                GUIUtility.RotateAroundPivot(pl.transform.eulerAngles.y, mp * (Screen.height / RefH));
                GUI.Label(new Rect(mp.x - 20, mp.y - 22, 40, 40), "▲", _prompt);
                GUI.matrix = old;
            }

            GUILayout.BeginArea(new Rect(r.x + 920, r.y + 30, 450, 860));
            GUILayout.Label("Χάρτης", _title);
            GUILayout.Label("Επιλέξτε προορισμό για τηλεμεταφορά:", _small);
            foreach (Poi poi in Pois)
            {
                if (GUILayout.Button(poi.name, _button))
                {
                    if (pl != null) pl.Teleport(poi.pos, poi.yaw);
                    _map = false;
                }
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Κλείσιμο (M)", _button)) _map = false;
            GUILayout.EndArea();
        }

        static Vector2 WorldToMap(Vector3 w, Rect r)
        {
            float u = (w.x + WorldLayout.Half) / WorldLayout.Size;
            float v = (w.z + WorldLayout.Half) / WorldLayout.Size;
            return new Vector2(r.x + u * r.width, r.y + (1f - v) * r.height);
        }

        // ------------------------------------------------------------------ Παύση / ρυθμίσεις
        void DrawPause(float W)
        {
            Rect r = new Rect(W * 0.5f - 380, 170, 760, 740);
            GUI.Box(r, GUIContent.none, _panel);
            GUILayout.BeginArea(new Rect(r.x + 36, r.y + 26, r.width - 72, r.height - 52));
            GUILayout.Label("Παύση — Ρυθμίσεις", _title);
            GUILayout.Space(10);

            GUILayout.Label("Ευαισθησία ποντικιού: " + PlayerController.MouseSensitivity.ToString("0.0"), _small);
            float ms = GUILayout.HorizontalSlider(PlayerController.MouseSensitivity, 0.5f, 6f);
            if (!Mathf.Approximately(ms, PlayerController.MouseSensitivity))
            {
                PlayerController.MouseSensitivity = ms;
                PlayerPrefs.SetFloat("gs_mouse", ms);
            }
            GUILayout.Space(12);
            GUILayout.Label("Ένταση ήχου: " + Mathf.RoundToInt(AudioListener.volume * 100) + "%", _small);
            AudioListener.volume = GUILayout.HorizontalSlider(AudioListener.volume, 0f, 1f);
            GUILayout.Space(16);

            GUILayout.Label("Ώρα της ημέρας:", _small);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Αυγή 05:30", _button)) TimeManager.Instance.SetHour(5.5f);
            if (GUILayout.Button("Πρωί 09:00", _button)) TimeManager.Instance.SetHour(9f);
            if (GUILayout.Button("Δύση 18:40", _button)) TimeManager.Instance.SetHour(18.66f);
            if (GUILayout.Button("Νύχτα 22:30", _button)) TimeManager.Instance.SetHour(22.5f);
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
            GUILayout.Label("Καιρός:", _small);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Αίθριος", _button)) WeatherSystem.Instance.SetState(WeatherState.Clear);
            if (GUILayout.Button("Συννεφιά", _button)) WeatherSystem.Instance.SetState(WeatherState.Cloudy);
            if (GUILayout.Button("Βροχή", _button)) WeatherSystem.Instance.SetState(WeatherState.Rain);
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
            if (GUILayout.Button("Πηγές & Συντελεστές", _button)) { _pause = false; ShowLore(Lore.Get("credits")); }

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Συνέχεια", _button)) _pause = false;
            if (GUILayout.Button("Έξοδος", _button)) Application.Quit();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
