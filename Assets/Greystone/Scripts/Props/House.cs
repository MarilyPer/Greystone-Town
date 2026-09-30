// =====================================================================================
//  House.cs — Παραμετρικό σπίτι
//  ---------------------------------------------------------------------------------
//  Ένα σπίτι περιγράφεται από λίγες παραμέτρους (HouseSpec) και κατασκευάζεται με κώδικα:
//  τοίχοι (λιθοδομή ή σοβάς με ξυλοδεσιά), αετώματα, δίρριχτη στέγη (πέτρινες πλάκες ή
//  άχυρο), παράθυρα με λίθινα πλαίσια και μεσόστυλα (χαρακτηριστικό των Cotswolds),
//  καμινάδα με καπνό, πόρτα και φανάρι.
//
//  Τα σπίτια «ζουν»: ο καπνός βγαίνει τις ώρες μαγειρέματος (πρωί/βράδυ) και τα παράθυρα
//  φωτίζονται τη νύχτα. Η ταβέρνα είναι κούφια με εσωτερικό (τζάκι, τραπέζια, βαρέλια).
//
//  ΒΕΛΤΙΣΤΟΠΟΙΗΣΗ:
//   * Τα στατικά μέρη μπαίνουν κάτω από το παιδί «Static» ώστε ο VillageBuilder να τα
//     ενώσει με static batching (λιγότερα draw calls).
//   * LOD Group: από μακριά (μικρό ποσοστό της οθόνης) εμφανίζεται μόνο το «κέλυφος»
//     (τοίχοι, στέγη, καμινάδα)· οι λεπτομέρειες (πλαίσια, λάμες, φανάρια) κρύβονται.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Greystone
{
    public enum WallStyle { Stone, StoneGrey, Plaster }
    public enum RoofStyle { Slate, Thatch }

    public class HouseSpec
    {
        public string name = "Σπίτι";
        public float width = 8f, depth = 6f;
        public int floors = 2;
        public WallStyle walls = WallStyle.Stone;
        public RoofStyle roof = RoofStyle.Slate;
        public bool enterable;
        public bool chimney = true;
        public bool lantern = true;
        public float doorOffset;   // μετατόπιση πόρτας κατά x
    }

    public class House : MonoBehaviour
    {
        public HouseSpec spec;
        public Door door;
        public Transform Static { get; private set; }
        public Transform Details { get; private set; }

        /// <summary>Σημείο μπροστά στην πόρτα (για τους χωρικούς), σε συντεταγμένες κόσμου.</summary>
        public Vector3 DoorstepWorld
        {
            get { return transform.TransformPoint(new Vector3(spec.doorOffset, 0f, spec.depth * 0.5f + 1.2f)); }
        }

        /// <summary>Σημείο μέσα στο σπίτι (για τους χωρικούς της ταβέρνας).</summary>
        public Vector3 InsideWorld(Vector3 local) { return transform.TransformPoint(local); }

        const float FloorHeight = 2.8f;
        ParticleSystem _smoke;
        readonly List<Renderer> _shell = new List<Renderer>();

        public void Build(HouseSpec s)
        {
            spec = s;
            Static = Geo.Child(transform, "Static", Vector3.zero);
            Details = Geo.Child(transform, "Details", Vector3.zero);
            float w = s.width, d = s.depth, H = s.floors * FloorHeight;
            float roofH = d * (s.roof == RoofStyle.Thatch ? 0.62f : 0.55f);   // απότομη κλίση (~50°), όπως στις πέτρινες στέγες

            Material wallMat = s.walls == WallStyle.Plaster ? Mats.Get("plaster") : Mats.Get(s.walls == WallStyle.StoneGrey ? "stone_grey" : "stone");
            Material trim = Mats.Get(s.walls == WallStyle.StoneGrey ? "stone" : "stone_grey");

            // ---------------- Τοίχοι ----------------
            if (s.enterable) BuildHollowWalls(w, d, H, wallMat);
            else Shell(Geo.Box(Static, new Vector3(w, H + 1f, d), new Vector3(0, (H - 1f) * 0.5f, 0), wallMat, default(Vector3), true));

            // Βάση/θεμέλιο από πιο σκούρα πέτρα (plinth)
            if (!s.enterable) Geo.Box(Static, new Vector3(w + 0.12f, 0.45f, d + 0.12f), new Vector3(0, 0.0f, 0), Mats.Get("stone_rubble"));

            // ---------------- Αετώματα & στέγη ----------------
            Shell(Geo.PrismPart(Static, new Vector3(d, roofH, w), new Vector3(0, H + roofH * 0.5f, 0), wallMat, new Vector3(0, 90, 0)));
            BuildRoof(w, d, H, roofH, s.roof);

            // ---------------- Ξυλοδεσιά (μόνο στα σοβατισμένα) ----------------
            if (s.walls == WallStyle.Plaster) BuildTimberFrame(w, d, H);

            // ---------------- Παράθυρα ----------------
            BuildWindows(w, d, H, trim, s);

            // ---------------- Πόρτα ----------------
            float doorW = 1.05f, doorH = 2.1f;
            Transform frame = Details;
            Geo.Box(frame, new Vector3(0.18f, doorH + 0.15f, 0.12f), new Vector3(s.doorOffset - doorW * 0.5f - 0.09f, doorH * 0.5f, d * 0.5f + 0.04f), trim);
            Geo.Box(frame, new Vector3(0.18f, doorH + 0.15f, 0.12f), new Vector3(s.doorOffset + doorW * 0.5f + 0.09f, doorH * 0.5f, d * 0.5f + 0.04f), trim);
            Geo.Box(frame, new Vector3(doorW + 0.5f, 0.22f, 0.14f), new Vector3(s.doorOffset, doorH + 0.1f, d * 0.5f + 0.05f), trim);
            Geo.Box(Static, new Vector3(doorW + 0.5f, 0.12f, 0.5f), new Vector3(s.doorOffset, 0.02f, d * 0.5f + 0.25f), Mats.Get("stone_grey"), default(Vector3), true);   // κατώφλι

            GameObject doorGo = new GameObject("Door");
            doorGo.transform.SetParent(transform, false);
            doorGo.transform.localPosition = new Vector3(s.doorOffset, 0.05f, d * 0.5f + (s.enterable ? -0.15f : 0.03f));
            door = doorGo.AddComponent<Door>();
            door.locked = !s.enterable;
            door.Build(doorW, doorH);

            // ---------------- Καμινάδα & καπνός ----------------
            if (s.chimney)
            {
                float cx = w * 0.5f - 0.55f;
                float top = H + roofH + 0.9f;
                Shell(Geo.Box(Static, new Vector3(0.9f, top - H + 1f, 0.9f), new Vector3(cx, H - 1f + (top - H + 1f) * 0.5f, 0), trim));
                Geo.Box(Details, new Vector3(1.05f, 0.15f, 1.05f), new Vector3(cx, top, 0), trim);
                Geo.Cyl(Details, 0.15f, 0.35f, new Vector3(cx, top + 0.25f, 0), Mats.Get("roof_slate"), 1f, 8);   // πήλινο «καπέλο» καπνοδόχου
                _smoke = FX.Smoke(transform, new Vector3(cx, top + 0.5f, 0), 1f);
                TimeManager.Instance.HourChanged += OnHour;
                OnHour((int)TimeManager.Instance.hours);
            }

            // ---------------- Φανάρι ----------------
            if (s.lantern)
            {
                GameObject l = new GameObject("WallLantern");
                l.transform.SetParent(transform, false);
                l.transform.localPosition = new Vector3(s.doorOffset + doorW * 0.5f + 0.55f, 2.25f, d * 0.5f);
                l.AddComponent<WallLantern>().BuildWallLantern();
            }

            // Καταχώρηση αποτυπώματος (για να μη φυτρώνει γρασίδι/δέντρα μέσα στο σπίτι)
            Vector3 c = transform.position;
            bool rotated = Mathf.Abs(Mathf.Sin(transform.eulerAngles.y * Mathf.Deg2Rad)) > 0.7f;
            float fw = rotated ? d : w, fd = rotated ? w : d;
            WorldLayout.Footprints.Add(new Rect(c.x - fw * 0.5f - 0.5f, c.z - fd * 0.5f - 0.5f, fw + 1f, fd + 1f));

            if (s.enterable) BuildInterior(w, d, H);
            SetupLod();
        }

        void Shell(GameObject go) { _shell.Add(go.GetComponent<Renderer>()); }

        void OnDestroy()
        {
            if (TimeManager.Instance != null) TimeManager.Instance.HourChanged -= OnHour;
        }

        /// <summary>Καπνός τις ώρες μαγειρέματος/θέρμανσης (η ταβέρνα καπνίζει πάντα).</summary>
        void OnHour(int h)
        {
            if (_smoke == null) return;
            bool cooking = spec.enterable || (h >= 6 && h < 10) || (h >= 17 && h < 23) || Random.value < 0.15f;
            if (cooking && !_smoke.isPlaying) _smoke.Play();
            else if (!cooking && _smoke.isPlaying) _smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        // ------------------------------------------------------------------ Στέγη
        void BuildRoof(float w, float d, float H, float roofH, RoofStyle style)
        {
            Material mat = Mats.Get(style == RoofStyle.Thatch ? "roof_thatch" : "roof_slate");
            float thick = style == RoofStyle.Thatch ? 0.45f : 0.18f;
            float overhang = style == RoofStyle.Thatch ? 0.6f : 0.35f;
            float halfSpan = d * 0.5f;
            float theta = Mathf.Atan2(roofH, halfSpan) * Mathf.Rad2Deg;
            float L = Mathf.Sqrt(halfSpan * halfSpan + roofH * roofH);
            float sin = Mathf.Sin(theta * Mathf.Deg2Rad), cos = Mathf.Cos(theta * Mathf.Deg2Rad);

            for (int side = -1; side <= 1; side += 2)
            {
                // Κέντρο της κλίσης, μετατοπισμένο προς τη μαρφίζα (overhang) και προς τα έξω (πάχος)
                Vector3 mid = new Vector3(0, H + roofH * 0.5f, side * halfSpan * 0.5f);
                Vector3 eaveDir = new Vector3(0, -sin, side * cos);
                Vector3 normal = new Vector3(0, cos, side * sin);
                Vector3 pos = mid + eaveDir * (overhang * 0.5f) + normal * (thick * 0.5f);
                Shell(Geo.Box(Static, new Vector3(w + 0.5f, thick, L + overhang), pos, mat, new Vector3(side * theta, 0, 0)));
            }
            // Κορφιάς (ridge)
            Geo.Box(Static, new Vector3(w + 0.55f, thick * 1.3f, thick * 1.6f), new Vector3(0, H + roofH + thick * 0.55f, 0), mat);
        }

        // ------------------------------------------------------------------ Κούφιοι τοίχοι (ταβέρνα)
        void BuildHollowWalls(float w, float d, float H, Material m)
        {
            const float t = 0.35f;
            float doorW = 1.25f, doorH = 2.3f;
            float x0 = spec.doorOffset;
            Shell(Geo.Box(Static, new Vector3(w, H + 1f, t), new Vector3(0, (H - 1f) * 0.5f, -d * 0.5f + t * 0.5f), m, default(Vector3), true));
            Shell(Geo.Box(Static, new Vector3(t, H + 1f, d), new Vector3(-w * 0.5f + t * 0.5f, (H - 1f) * 0.5f, 0), m, default(Vector3), true));
            Shell(Geo.Box(Static, new Vector3(t, H + 1f, d), new Vector3(w * 0.5f - t * 0.5f, (H - 1f) * 0.5f, 0), m, default(Vector3), true));
            float leftW = (x0 - doorW * 0.5f) + w * 0.5f;
            float rightW = w * 0.5f - (x0 + doorW * 0.5f);
            Shell(Geo.Box(Static, new Vector3(leftW, H + 1f, t), new Vector3(-w * 0.5f + leftW * 0.5f, (H - 1f) * 0.5f, d * 0.5f - t * 0.5f), m, default(Vector3), true));
            Shell(Geo.Box(Static, new Vector3(rightW, H + 1f, t), new Vector3(w * 0.5f - rightW * 0.5f, (H - 1f) * 0.5f, d * 0.5f - t * 0.5f), m, default(Vector3), true));
            Shell(Geo.Box(Static, new Vector3(doorW, H - doorH, t), new Vector3(x0, doorH + (H - doorH) * 0.5f, d * 0.5f - t * 0.5f), m, default(Vector3), true));
            // Πάτωμα (σανίδες) και ταβάνι
            Geo.Box(Static, new Vector3(w - 2 * t, 0.12f, d - 2 * t), new Vector3(0, 0.02f, 0), Mats.Get("wood"), default(Vector3), true);
            Shell(Geo.Box(Static, new Vector3(w, 0.2f, d), new Vector3(0, H + 0.1f, 0), Mats.Get("timber")));
            // Δοκάρια ταβανιού
            for (float x = -w * 0.5f + 1.5f; x < w * 0.5f - 1f; x += 1.8f)
                Geo.Box(Details, new Vector3(0.22f, 0.25f, d - 2 * t), new Vector3(x, H - 0.12f, 0), Mats.Get("timber"));
        }

        // ------------------------------------------------------------------ Ξυλοδεσιά
        void BuildTimberFrame(float w, float d, float H)
        {
            Material tm = Mats.Get("timber");
            float z = d * 0.5f + 0.03f;
            for (int face = -1; face <= 1; face += 2)
            {
                float fz = face * z;
                // Κάθετοι στύλοι
                for (float x = -w * 0.5f + 0.1f; x <= w * 0.5f; x += w / 4f - 0.001f)
                    Geo.Box(Details, new Vector3(0.18f, H, 0.08f), new Vector3(Mathf.Min(x, w * 0.5f - 0.1f), H * 0.5f, fz), tm);
                // Οριζόντια δοκάρια ανά όροφο
                for (int f = 1; f <= spec.floors; f++)
                    Geo.Box(Details, new Vector3(w + 0.04f, 0.2f, 0.08f), new Vector3(0, f * FloorHeight - 0.1f, fz), tm);
                // Διαγώνιες αντηρίδες στον επάνω όροφο
                if (spec.floors > 1)
                {
                    Geo.Box(Details, new Vector3(0.14f, 1.9f, 0.07f), new Vector3(-w * 0.5f + 0.7f, FloorHeight * 1.5f, fz), tm, new Vector3(0, 0, 35));
                    Geo.Box(Details, new Vector3(0.14f, 1.9f, 0.07f), new Vector3(w * 0.5f - 0.7f, FloorHeight * 1.5f, fz), tm, new Vector3(0, 0, -35));
                }
            }
        }

        // ------------------------------------------------------------------ Παράθυρα
        void BuildWindows(float w, float d, float H, Material trim, HouseSpec s)
        {
            Material glass = Mats.Get("window");
            int count = Mathf.Max(1, Mathf.FloorToInt((w - 1.5f) / 2.6f));
            for (int floor = 0; floor < s.floors; floor++)
            {
                float y = floor * FloorHeight + 1.55f;
                for (int face = -1; face <= 1; face += 2)
                {
                    for (int i = 0; i < count; i++)
                    {
                        float x = -w * 0.5f + (i + 0.5f) * (w / count);
                        // Στο ισόγειο της πρόσοψης αφήνουμε χώρο για την πόρτα
                        if (face == 1 && floor == 0 && Mathf.Abs(x - s.doorOffset) < 1.4f) continue;
                        Window(new Vector3(x, y, face * (d * 0.5f)), face > 0 ? 0f : 180f, glass, trim, floor == 0 ? 1.0f : 0.85f);
                        if (s.enterable) Window(new Vector3(x, y, face * (d * 0.5f - 0.36f)), face > 0 ? 180f : 0f, Mats.Get("window"), trim, 1.0f, true);
                    }
                }
                // Ένα παράθυρο σε κάθε πλαϊνό τοίχο (αέτωμα) στον επάνω όροφο
                if (floor == s.floors - 1 && d > 5f)
                {
                    Window(new Vector3(w * 0.5f, y, 0), 90f, glass, trim, 0.7f);
                    Window(new Vector3(-w * 0.5f, y, 0), -90f, glass, trim, 0.7f);
                }
            }
        }

        /// <summary>Παράθυρο με λίθινο πλαίσιο, πρέκι (hood mould), περβάζι και μεσόστυλο.</summary>
        void Window(Vector3 wallPoint, float yaw, Material glass, Material trim, float scale, bool interior = false)
        {
            Transform t = Geo.Child(Details, "Window", wallPoint, new Vector3(0, yaw, 0));
            float ww = 1.0f * scale, wh = 1.15f * scale;
            Geo.Box(t, new Vector3(ww, wh, 0.06f), new Vector3(0, 0, 0.02f), glass);
            if (interior) return;
            Geo.Box(t, new Vector3(ww + 0.3f, 0.14f, 0.16f), new Vector3(0, wh * 0.5f + 0.1f, 0.05f), trim);   // πρέκι
            Geo.Box(t, new Vector3(ww + 0.24f, 0.1f, 0.2f), new Vector3(0, -wh * 0.5f - 0.06f, 0.07f), trim);  // περβάζι
            Geo.Box(t, new Vector3(0.09f, wh, 0.1f), new Vector3(-ww * 0.5f - 0.03f, 0, 0.04f), trim);
            Geo.Box(t, new Vector3(0.09f, wh, 0.1f), new Vector3(ww * 0.5f + 0.03f, 0, 0.04f), trim);
            Geo.Box(t, new Vector3(0.08f, wh, 0.09f), new Vector3(0, 0, 0.05f), trim);                         // μεσόστυλο (mullion)
            Geo.Box(t, new Vector3(ww, 0.025f, 0.07f), new Vector3(0, wh * 0.15f, 0.05f), Mats.Get("iron"));   // μολύβδινες λάμες
        }

        // ------------------------------------------------------------------ Εσωτερικό ταβέρνας
        void BuildInterior(float w, float d, float H)
        {
            Transform inside = Geo.Child(transform, "Interior", Vector3.zero);
            Material wood = Mats.Get("wood"), dark = Mats.Get("timber"), stone = Mats.Get("stone_grey");

            // Τζάκι στον πίσω τοίχο
            float bz = -d * 0.5f + 0.35f;
            Geo.Box(inside, new Vector3(2.4f, 1.6f, 0.8f), new Vector3(0, 0.8f, bz + 0.4f), stone, default(Vector3), true);
            Geo.Box(inside, new Vector3(1.5f, 1.0f, 0.3f), new Vector3(0, 0.55f, bz + 0.68f), Mats.Get("dark_interior"));
            Geo.Box(inside, new Vector3(2.7f, 0.18f, 1.0f), new Vector3(0, 1.65f, bz + 0.45f), dark);
            Geo.Box(inside, new Vector3(1.6f, H - 1.7f, 0.7f), new Vector3(0, 1.7f + (H - 1.7f) * 0.5f, bz + 0.35f), stone);
            Geo.Box(inside, new Vector3(0.9f, 0.12f, 0.3f), new Vector3(0, 0.12f, bz + 0.75f), Mats.Get("ember"));
            FX.Fire(inside, new Vector3(0, 0.25f, bz + 0.72f), 1.1f);
            Light fire = FX.PointLight(inside, new Vector3(0, 0.9f, bz + 1.4f), new Color(1f, 0.55f, 0.25f), 9f, 2.2f);
            gameObject.AddComponent<Flicker>().target = fire;
            SoundBank.Instance.CreateLoop(inside, new Vector3(0, 0.6f, bz + 1f), "fire", 0.5f, 1f, 12f);

            // Τραπέζια με πάγκους
            for (int i = 0; i < 3; i++)
            {
                float x = -w * 0.5f + 1.8f + i * ((w - 3.6f) / 2f);
                float z = 0.6f;
                Transform table = Geo.Child(inside, "Table", new Vector3(x, 0, z));
                Geo.Box(table, new Vector3(1.6f, 0.08f, 0.8f), new Vector3(0, 0.76f, 0), wood, default(Vector3), true);
                for (int lx = -1; lx <= 1; lx += 2)
                    for (int lz = -1; lz <= 1; lz += 2)
                        Geo.Box(table, new Vector3(0.08f, 0.72f, 0.08f), new Vector3(lx * 0.7f, 0.36f, lz * 0.32f), dark);
                Geo.Box(table, new Vector3(1.5f, 0.06f, 0.3f), new Vector3(0, 0.44f, 0.7f), wood, default(Vector3), true);
                Geo.Box(table, new Vector3(1.5f, 0.06f, 0.3f), new Vector3(0, 0.44f, -0.7f), wood, default(Vector3), true);
                Geo.Box(table, new Vector3(0.06f, 0.42f, 0.25f), new Vector3(-0.65f, 0.21f, 0.7f), dark);
                Geo.Box(table, new Vector3(0.06f, 0.42f, 0.25f), new Vector3(0.65f, 0.21f, 0.7f), dark);
                Geo.Box(table, new Vector3(0.06f, 0.42f, 0.25f), new Vector3(-0.65f, 0.21f, -0.7f), dark);
                Geo.Box(table, new Vector3(0.06f, 0.42f, 0.25f), new Vector3(0.65f, 0.21f, -0.7f), dark);
                // Κύπελλα και κερί
                Geo.Cyl(table, 0.05f, 0.12f, new Vector3(-0.3f, 0.86f, 0.1f), Mats.Get("wood_weathered"), 0.9f, 8);
                Geo.Cyl(table, 0.05f, 0.12f, new Vector3(0.35f, 0.86f, -0.15f), Mats.Get("wood_weathered"), 0.9f, 8);
                Geo.Cyl(table, 0.025f, 0.15f, new Vector3(0, 0.87f, 0), Mats.Get("plaster"), 1f, 6);
            }

            // Πάγκος σερβιρίσματος και βαρέλια
            Geo.Box(inside, new Vector3(0.7f, 1.1f, 2.6f), new Vector3(w * 0.5f - 1.3f, 0.55f, -0.9f), dark, default(Vector3), true);
            Geo.Box(inside, new Vector3(0.85f, 0.06f, 2.7f), new Vector3(w * 0.5f - 1.3f, 1.12f, -0.9f), wood);
            for (int i = 0; i < 3; i++)
                Barrel(inside, new Vector3(w * 0.5f - 0.75f, 0.5f, -d * 0.5f + 0.9f + i * 0.85f));

            // Ζώνη εσωτερικού: ο παίκτης θεωρείται «μέσα» (σταματά η βροχή, πνίγονται οι ήχοι έξω)
            ProximityZone zone = ProximityZone.Create(transform, new Vector3(0, H * 0.5f, 0), new Vector3(w - 0.5f, H, d - 0.5f));
            zone.Entered += () => { if (PlayerController.Instance != null) PlayerController.Instance.Indoors = true; };
            zone.Exited += () => { if (PlayerController.Instance != null) PlayerController.Instance.Indoors = false; };
        }

        public static void Barrel(Transform parent, Vector3 pos)
        {
            GameObject b = Geo.Cyl(parent, 0.35f, 1.0f, pos, Mats.Get("wood"), 1f, 12, default(Vector3), true);
            b.layer = Geo.LayerSmallProps;
            Geo.Cyl(parent, 0.365f, 0.06f, pos + new Vector3(0, 0.3f, 0), Mats.Get("iron"), 1f, 12).layer = Geo.LayerSmallProps;
            Geo.Cyl(parent, 0.365f, 0.06f, pos + new Vector3(0, -0.3f, 0), Mats.Get("iron"), 1f, 12).layer = Geo.LayerSmallProps;
        }

        // ------------------------------------------------------------------ LOD
        void SetupLod()
        {
            LODGroup lg = gameObject.AddComponent<LODGroup>();
            Renderer[] all = GetComponentsInChildren<Renderer>();
            List<Renderer> lod0 = new List<Renderer>();
            foreach (Renderer r in all)
                if (!(r is ParticleSystemRenderer)) lod0.Add(r);
            LOD[] lods =
            {
                new LOD(0.07f, lod0.ToArray()),          // κοντά: όλα
                new LOD(0.008f, _shell.ToArray()),       // μακριά: μόνο το κέλυφος
            };
            lg.SetLODs(lods);
            lg.fadeMode = LODFadeMode.None;
            lg.RecalculateBounds();
        }
    }

    /// <summary>Τρεμόπαιγμα έντασης φωτός (φωτιά τζακιού/καμινιού).</summary>
    public class Flicker : MonoBehaviour
    {
        public Light target;
        public float amount = 0.35f;
        float _base = -1f, _seed;

        void Start() { _seed = Random.value * 50f; }

        void Update()
        {
            if (target == null) return;
            if (_base < 0f) _base = target.intensity;
            target.intensity = _base * (1f - amount * 0.5f + amount * Mathf.PerlinNoise(_seed, Time.time * 6f));
        }
    }
}
