// =====================================================================================
//  Church.cs — Ενοριακός ναός με καμπαναριό, ρολόι και καμπάνα
//  ---------------------------------------------------------------------------------
//  * Κλίτος (nave) με απότομη στέγη και ψηλά στενά παράθυρα με βιτρό.
//  * Πύργος με ανοιχτό καμπαναριό (4 γωνιακοί πεσσοί) και πυραμιδοειδές κωδωνοστάσιο.
//  * ΡΟΛΟΪ: οι δείκτες ακολουθούν την ώρα του κόσμου (animation συνεχούς τιμής).
//  * ΚΑΜΠΑΝΑ: φυσικό εκκρεμές με απόσβεση. Κάθε χτύπημα δίνει «ώθηση» στη γωνιακή ταχύτητα·
//    η επιτάχυνση είναι -(g/L)·sin(θ) (νόμος του εκκρεμούς), άρα η ταλάντωση είναι ρεαλιστική.
//    Χτυπά αυτόματα τις ώρες (07:00–21:00, τόσες φορές όσες η ώρα σε 12ωρη μορφή) και όταν
//    ο χρήστης τραβήξει το σχοινί.
// =====================================================================================
using System.Collections;
using UnityEngine;

namespace Greystone
{
    public class Church : MonoBehaviour
    {
        Transform _bellPivot, _hourHand, _minuteHand, _rope;
        float _angle, _angVel;
        bool _ringing;
        const float PendulumLength = 0.9f;

        public Transform Static { get; private set; }

        public void Build()
        {
            Static = Geo.Child(transform, "Static", Vector3.zero);
            Material stone = Mats.Get("stone");
            Material grey = Mats.Get("stone_grey");
            Material roof = Mats.Get("roof_slate");

            // ---------------- Κλίτος (z: 3..25 τοπικά, πρόσοψη προς -z) ----------------
            const float nw = 10f, nl = 22f, nh = 8f;
            float nz = 3f + nl * 0.5f;
            Geo.Box(Static, new Vector3(nw, nh + 1f, nl), new Vector3(0, (nh - 1f) * 0.5f, nz), stone, default(Vector3), true);
            Geo.Box(Static, new Vector3(nw + 0.2f, 0.6f, nl + 0.2f), new Vector3(0, 0.05f, nz), Mats.Get("stone_rubble"));
            float roofH = 5.5f;
            Geo.PrismPart(Static, new Vector3(nw, roofH, nl), new Vector3(0, nh + roofH * 0.5f, nz), stone);
            float theta = Mathf.Atan2(roofH, nw * 0.5f) * Mathf.Rad2Deg;
            float L = Mathf.Sqrt(25f + roofH * roofH);
            float s = Mathf.Sin(theta * Mathf.Deg2Rad), c = Mathf.Cos(theta * Mathf.Deg2Rad);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 mid = new Vector3(side * nw * 0.25f, nh + roofH * 0.5f, nz);
                Vector3 eave = new Vector3(side * c, -s, 0);
                Vector3 n = new Vector3(side * s, c, 0);
                Geo.Box(Static, new Vector3(L + 0.5f, 0.22f, nl + 0.6f), mid + eave * 0.25f + n * 0.11f, roof, new Vector3(0, 0, -side * theta));
            }
            // Αντηρίδες (buttresses) κατά μήκος του κλίτους
            for (float z = 5f; z < 24f; z += 4.5f)
                for (int side = -1; side <= 1; side += 2)
                    Geo.Box(Static, new Vector3(0.7f, 4.5f, 0.9f), new Vector3(side * (nw * 0.5f + 0.3f), 2f, z), grey, new Vector3(0, 0, side * 6f));
            // Ψηλά παράθυρα με βιτρό
            Material stained = Mats.Get("stained_glass");
            for (float z = 7.2f; z < 24f; z += 4.5f)
                for (int side = -1; side <= 1; side += 2)
                {
                    Geo.Box(Static, new Vector3(0.08f, 3.2f, 1.1f), new Vector3(side * (nw * 0.5f + 0.02f), 4.4f, z), stained);
                    Geo.Box(Static, new Vector3(0.15f, 0.25f, 1.4f), new Vector3(side * (nw * 0.5f + 0.05f), 6.1f, z), grey);
                }
            // Ανατολικό παράθυρο στο ιερό (πίσω τοίχος)
            Geo.Box(Static, new Vector3(2.4f, 4f, 0.08f), new Vector3(0, 5f, nz + nl * 0.5f + 0.02f), stained);

            // ---------------- Πύργος (τοπικό z: -2.5..2.5 γύρω από το z=0.5) ----------------
            const float tw = 5f, baseH = 11f, belfryH = 4.5f;
            float tz = 0.5f;
            Geo.Box(Static, new Vector3(tw, baseH + 1f, tw), new Vector3(0, (baseH - 1f) * 0.5f, tz), stone, default(Vector3), true);
            Geo.Box(Static, new Vector3(tw + 0.3f, 0.3f, tw + 0.3f), new Vector3(0, baseH, tz), grey);     // κορνίζα
            // Καμπαναριό: 4 γωνιακοί πεσσοί, ανοιχτές πλευρές
            for (int ix = -1; ix <= 1; ix += 2)
                for (int iz = -1; iz <= 1; iz += 2)
                    Geo.Box(Static, new Vector3(1.1f, belfryH, 1.1f), new Vector3(ix * (tw * 0.5f - 0.55f), baseH + belfryH * 0.5f, tz + iz * (tw * 0.5f - 0.55f)), stone);
            Geo.Box(Static, new Vector3(tw + 0.2f, 0.6f, tw + 0.2f), new Vector3(0, baseH + belfryH + 0.3f, tz), grey);
            // Επάλξεις (battlements)
            for (int i = 0; i < 4; i++)
            {
                float a = -tw * 0.5f + 0.4f + i * (tw - 0.8f) / 3f;
                Geo.Box(Static, new Vector3(0.6f, 0.6f, 0.4f), new Vector3(a, baseH + belfryH + 0.9f, tz - tw * 0.5f), grey);
                Geo.Box(Static, new Vector3(0.6f, 0.6f, 0.4f), new Vector3(a, baseH + belfryH + 0.9f, tz + tw * 0.5f), grey);
                Geo.Box(Static, new Vector3(0.4f, 0.6f, 0.6f), new Vector3(-tw * 0.5f, baseH + belfryH + 0.9f, tz + a), grey);
                Geo.Box(Static, new Vector3(0.4f, 0.6f, 0.6f), new Vector3(tw * 0.5f, baseH + belfryH + 0.9f, tz + a), grey);
            }
            // Πυραμιδοειδές κωδωνοστάσιο (κώνος 4 πλευρών, στραμμένος 45°)
            Geo.Cyl(Static, tw * 0.62f, 7f, new Vector3(0, baseH + belfryH + 0.6f + 3.5f, tz), roof, 0f, 4, new Vector3(0, 45, 0));
            Geo.Box(Static, new Vector3(0.08f, 1.2f, 0.08f), new Vector3(0, baseH + belfryH + 8.6f, tz), Mats.Get("iron"));
            Geo.Box(Static, new Vector3(0.6f, 0.08f, 0.08f), new Vector3(0, baseH + belfryH + 8.8f, tz), Mats.Get("iron"));

            // ---------------- Είσοδος (στη νότια όψη του πύργου, τοπικό -z) ----------------
            GameObject doorGo = new GameObject("ChurchDoor");
            doorGo.transform.SetParent(transform, false);
            doorGo.transform.localPosition = new Vector3(0, 0.05f, tz - tw * 0.5f - 0.03f);
            doorGo.transform.localRotation = Quaternion.Euler(0, 180, 0);
            Door door = doorGo.AddComponent<Door>();
            door.locked = true;
            door.lockedMessage = "Ο ναός είναι κλειστός. Η λειτουργία γίνεται την Κυριακή το πρωί.";
            door.Build(1.4f, 2.8f);
            // Οξυκόρυφο πλαίσιο εισόδου
            Geo.Box(Static, new Vector3(2.2f, 0.3f, 0.3f), new Vector3(0, 3.05f, tz - tw * 0.5f - 0.1f), grey);
            Geo.Box(Static, new Vector3(0.3f, 3.1f, 0.3f), new Vector3(-0.95f, 1.5f, tz - tw * 0.5f - 0.1f), grey);
            Geo.Box(Static, new Vector3(0.3f, 3.1f, 0.3f), new Vector3(0.95f, 1.5f, tz - tw * 0.5f - 0.1f), grey);

            BuildClock(new Vector3(0, 8.3f, tz - tw * 0.5f - 0.02f));
            BuildBell(new Vector3(0, baseH + belfryH - 0.5f, tz));
            BuildRope(new Vector3(1.9f, 0, tz - tw * 0.5f - 0.15f));

            TimeManager.Instance.HourChanged += OnHour;
        }

        void OnDestroy()
        {
            if (TimeManager.Instance != null) TimeManager.Instance.HourChanged -= OnHour;
        }

        // ------------------------------------------------------------------ Ρολόι
        void BuildClock(Vector3 center)
        {
            Transform face = Geo.Child(transform, "Clock", center);
            Geo.Cyl(face, 1.15f, 0.1f, Vector3.zero, Mats.Get("clock_face"), 1f, 24, new Vector3(90, 0, 0));
            Geo.Cyl(face, 1.25f, 0.06f, new Vector3(0, 0, 0.03f), Mats.Get("gold"), 1f, 24, new Vector3(90, 0, 0));
            // Ενδείξεις ωρών (12 μικρές εγκοπές)
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * Mathf.PI * 2f;
                Vector3 p = new Vector3(Mathf.Sin(a) * 0.95f, Mathf.Cos(a) * 0.95f, -0.07f);
                Geo.Box(face, new Vector3(0.07f, i % 3 == 0 ? 0.25f : 0.14f, 0.02f), p, Mats.Get("iron"), new Vector3(0, 0, -a * Mathf.Rad2Deg));
            }
            _hourHand = Geo.Child(face, "HourHand", new Vector3(0, 0, -0.09f));
            Geo.Box(_hourHand, new Vector3(0.1f, 0.6f, 0.03f), new Vector3(0, 0.26f, 0), Mats.Get("iron"));
            _minuteHand = Geo.Child(face, "MinuteHand", new Vector3(0, 0, -0.11f));
            Geo.Box(_minuteHand, new Vector3(0.06f, 0.9f, 0.03f), new Vector3(0, 0.4f, 0), Mats.Get("iron"));
            Geo.Cyl(face, 0.08f, 0.06f, new Vector3(0, 0, -0.13f), Mats.Get("gold"), 1f, 8, new Vector3(90, 0, 0));
        }

        // ------------------------------------------------------------------ Καμπάνα
        void BuildBell(Vector3 hangPoint)
        {
            Material bronze = Mats.Get("bronze");
            Geo.Box(transform, new Vector3(4.2f, 0.3f, 0.3f), hangPoint + new Vector3(0, 0.25f, 0), Mats.Get("timber"));   // δοκός ανάρτησης
            _bellPivot = Geo.Child(transform, "BellPivot", hangPoint);
            Geo.Cyl(_bellPivot, 0.62f, 0.95f, new Vector3(0, -0.75f, 0), bronze, 0.55f, 16);
            Geo.Ball(_bellPivot, new Vector3(0.7f, 0.5f, 0.7f), new Vector3(0, -0.3f, 0), bronze, 14, 8);
            Geo.Cyl(_bellPivot, 0.66f, 0.1f, new Vector3(0, -1.2f, 0), bronze, 1f, 16);               // χείλος
            Geo.Box(_bellPivot, new Vector3(0.5f, 0.2f, 0.25f), new Vector3(0, -0.02f, 0), Mats.Get("timber"));   // «κεφαλή» (headstock)
            Geo.Cyl(_bellPivot, 0.04f, 0.9f, new Vector3(0, -0.7f, 0), Mats.Get("iron"), 1f, 6);       // γλωσσίδι
            Geo.Ball(_bellPivot, Vector3.one * 0.16f, new Vector3(0, -1.15f, 0), Mats.Get("iron"), 8, 6);
        }

        void BuildRope(Vector3 basePos)
        {
            _rope = Geo.Child(transform, "BellRope", basePos);
            Geo.Cyl(_rope, 0.025f, 3.2f, new Vector3(0, 2.6f, 0), Mats.Get("rope"), 1f, 6);
            Geo.Cyl(_rope, 0.05f, 0.45f, new Vector3(0, 1.1f, 0), Mats.Get("cloth_red"), 1f, 8);      // «sally» (χειρολαβή)
            Geo.Box(_rope, new Vector3(0.2f, 0.2f, 0.2f), new Vector3(0, 4.25f, 0), Mats.Get("iron"));  // τροχαλία
            Interactable it = Interactable.Create(_rope, new Vector3(0, 1.4f, 0), new Vector3(0.8f, 2.4f, 0.8f), "");
            it.PromptProvider = () => _ringing ? "Η καμπάνα χτυπά…" : "Τραβήξτε το σχοινί της καμπάνας";
            it.Interacted += p => { if (!_ringing) StartCoroutine(Ring(3, true)); };
        }

        void OnHour(int h)
        {
            if (h < 7 || h > 21 || _ringing) return;
            int strikes = h % 12 == 0 ? 12 : h % 12;
            StartCoroutine(Ring(strikes, false));
        }

        IEnumerator Ring(int strikes, bool byPlayer)
        {
            _ringing = true;
            if (byPlayer && Hud.Instance != null) Hud.Instance.Notify("Ο ήχος της καμπάνας αντηχεί σε όλο το χωριό.");
            for (int i = 0; i < strikes; i++)
            {
                if (byPlayer) StartCoroutine(Anim.MoveLocal(_rope, _rope.localPosition + Vector3.down * 0.35f, 0.3f));
                _angVel += 2.6f;      // ώθηση στο εκκρεμές
                yield return new WaitForSeconds(0.35f);
                SoundBank.Instance.PlayAt("bell", _bellPivot.position, 1f, 1f, 420f);
                if (byPlayer) StartCoroutine(Anim.MoveLocal(_rope, _rope.localPosition + Vector3.up * 0.35f, 0.4f));
                yield return new WaitForSeconds(1.6f);
            }
            _ringing = false;
        }

        void Update()
        {
            // ---------------- Εκκρεμές καμπάνας (ημι-άρρητη μέθοδος Euler) ----------------
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            float accel = -(9.81f / PendulumLength) * Mathf.Sin(_angle) - 0.9f * _angVel;
            _angVel += accel * dt;
            _angle += _angVel * dt;
            _bellPivot.localRotation = Quaternion.Euler(_angle * Mathf.Rad2Deg, 0, 0);

            // ---------------- Δείκτες ρολογιού ----------------
            float hrs = TimeManager.Instance.hours;
            float minutes = (hrs - Mathf.Floor(hrs)) * 60f;
            // Η όψη του ρολογιού κοιτά προς -z (ο θεατής κοιτά προς +z και έχει το +x στα δεξιά του):
            // αρνητική γωνία γύρω από τον z στρέφει τον δείκτη προς +x, δηλαδή δεξιόστροφα.
            _hourHand.localRotation = Quaternion.Euler(0, 0, -(hrs % 12f) / 12f * 360f);
            _minuteHand.localRotation = Quaternion.Euler(0, 0, -minutes / 60f * 360f);
        }
    }
}
