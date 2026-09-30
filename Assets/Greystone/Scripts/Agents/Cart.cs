// =====================================================================================
//  Cart.cs — Κάρο με άλογο σε προκαθορισμένη διαδρομή
//  ---------------------------------------------------------------------------------
//  Κινείται σε κλειστή διαδρομή κατά μήκος του κεντρικού δρόμου (πηγαινέλα με στροφή σε
//  ημικύκλιο στα άκρα). Κάνει στάσεις στα άκρα («παράδοση εμπορευμάτων»), δουλεύει μόνο
//  την ημέρα, και ΣΤΑΜΑΤΑ αν ο παίκτης βρεθεί μπροστά του (απλή αποφυγή σύγκρουσης).
//  Οι τροχοί περιστρέφονται κατά γωνία = απόσταση / ακτίνα (κύλιση χωρίς ολίσθηση) και τα
//  πόδια του αλόγου κινούνται ανάλογα με την ταχύτητα. Η κλίση ακολουθεί το έδαφος.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Greystone
{
    public class Cart : Agent
    {
        readonly List<Vector3> _route = new List<Vector3>();
        float[] _cum;           // σωρευτικό μήκος διαδρομής
        float _dist, _speed, _wait;
        int _lastEnd;           // 1 = ανατολή, -1 = δύση: μία στάση σε κάθε άκρο
        Transform _wheelL, _wheelR, _horse;
        Transform[] _horseLegs = new Transform[4];
        AudioSource _audio;
        const float MaxSpeed = 1.7f, WheelRadius = 0.55f;
        public const float WestEnd = -92f, EastEnd = 58f;

        public void Build()
        {
            // ---------------- Διαδρομή ----------------
            const float lane = 1.5f, r = 3.2f;
            for (float x = WestEnd; x <= EastEnd; x += 4f) _route.Add(new Vector3(x, 0, -lane));
            for (int i = 1; i < 12; i++) { float a = -Mathf.PI * 0.5f + i / 12f * Mathf.PI; _route.Add(new Vector3(EastEnd + Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r)); }
            for (float x = EastEnd; x >= WestEnd; x -= 4f) _route.Add(new Vector3(x, 0, lane));
            for (int i = 1; i < 12; i++) { float a = Mathf.PI * 0.5f + i / 12f * Mathf.PI; _route.Add(new Vector3(WestEnd + Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r)); }
            _cum = new float[_route.Count + 1];
            for (int i = 0; i < _route.Count; i++) _cum[i + 1] = _cum[i] + Vector3.Distance(_route[i], _route[(i + 1) % _route.Count]);

            // ---------------- Κάρο ----------------
            Material wood = Mats.Get("wood");
            Material dark = Mats.Get("timber");
            Transform body = Geo.Child(transform, "CartBody", new Vector3(0, 0, -1.2f));
            Geo.Box(body, new Vector3(1.5f, 0.1f, 2.4f), new Vector3(0, 0.75f, 0), wood);
            Geo.Box(body, new Vector3(0.06f, 0.4f, 2.4f), new Vector3(-0.72f, 0.98f, 0), wood);
            Geo.Box(body, new Vector3(0.06f, 0.4f, 2.4f), new Vector3(0.72f, 0.98f, 0), wood);
            Geo.Box(body, new Vector3(1.5f, 0.4f, 0.06f), new Vector3(0, 0.98f, -1.18f), wood);
            Geo.Box(body, new Vector3(0.08f, 0.08f, 2.2f), new Vector3(-0.45f, 0.62f, 1.9f), dark);   // τιμόνι (shafts)
            Geo.Box(body, new Vector3(0.08f, 0.08f, 2.2f), new Vector3(0.45f, 0.62f, 1.9f), dark);
            Geo.Cyl(body, 0.06f, 1.7f, new Vector3(0, WheelRadius, 0), Mats.Get("iron"), 1f, 6, new Vector3(0, 0, 90));   // άξονας
            // Φορτίο: σακιά και βαρέλι
            Geo.Ball(body, new Vector3(0.6f, 0.45f, 0.8f), new Vector3(-0.3f, 1.05f, 0.4f), Mats.Get("straw"), 8, 6);
            Geo.Ball(body, new Vector3(0.6f, 0.45f, 0.8f), new Vector3(0.3f, 1.05f, -0.3f), Mats.Get("straw"), 8, 6);
            House.Barrel(body, new Vector3(0.2f, 1.3f, 0.6f));
            _wheelL = Wheel(body, -0.82f);
            _wheelR = Wheel(body, 0.82f);

            // ---------------- Άλογο ----------------
            _horse = Geo.Child(transform, "Horse", new Vector3(0, 0, 1.6f));
            Material hide = Mats.Get("horse");
            Geo.Ball(_horse, new Vector3(0.65f, 0.75f, 1.7f), new Vector3(0, 1.35f, 0), hide, 12, 8);
            Geo.Cyl(_horse, 0.22f, 0.9f, new Vector3(0, 1.8f, 0.85f), hide, 0.8f, 10, new Vector3(40, 0, 0));   // λαιμός
            Geo.Ball(_horse, new Vector3(0.28f, 0.3f, 0.65f), new Vector3(0, 2.1f, 1.3f), hide, 10, 6);          // κεφάλι
            Geo.Box(_horse, new Vector3(0.06f, 0.35f, 0.7f), new Vector3(0, 2.05f, 0.75f), Mats.Get("hair_dark"), new Vector3(40, 0, 0));   // χαίτη
            Geo.Cyl(_horse, 0.06f, 0.7f, new Vector3(0, 1.3f, -0.95f), Mats.Get("hair_dark"), 0.5f, 6, new Vector3(-30, 0, 0));           // ουρά
            Geo.Cyl(_horse, 0.4f, 0.12f, new Vector3(0, 1.72f, 0.62f), Mats.Get("leather"), 1f, 12, new Vector3(60, 0, 0));               // περιλαίμιο
            Vector3[] lp = { new Vector3(-0.2f, 1.05f, 0.55f), new Vector3(0.2f, 1.05f, 0.55f), new Vector3(-0.2f, 1.05f, -0.55f), new Vector3(0.2f, 1.05f, -0.55f) };
            for (int i = 0; i < 4; i++)
            {
                _horseLegs[i] = Geo.Child(_horse, "Leg", lp[i]);
                Geo.Cyl(_horseLegs[i], 0.08f, 1.0f, new Vector3(0, -0.5f, 0), hide, 0.7f, 8);
                Geo.Cyl(_horseLegs[i], 0.07f, 0.1f, new Vector3(0, -1.0f, 0), Mats.Get("hair_dark"), 1f, 8);
            }
            // Collider (ο παίκτης δεν περνά μέσα από το κάρο)
            BoxCollider bc = gameObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, 1f, 0);
            bc.size = new Vector3(1.6f, 2f, 5.4f);
            Rigidbody rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            _audio = SoundBank.Instance.CreateLoop(transform, new Vector3(0, 0.8f, 0), "cart", 0f, 2f, 28f);
            _dist = 0f;
            Place();
        }

        Transform Wheel(Transform parent, float x)
        {
            Transform w = Geo.Child(parent, "Wheel", new Vector3(x, WheelRadius, 0));
            Geo.Cyl(w, WheelRadius, 0.08f, Vector3.zero, Mats.Get("wood_weathered"), 1f, 16, new Vector3(0, 0, 90));
            Geo.Cyl(w, WheelRadius + 0.01f, 0.05f, Vector3.zero, Mats.Get("iron"), 1f, 16, new Vector3(0, 0, 90));
            for (int i = 0; i < 4; i++) Geo.Box(w, new Vector3(0.05f, WheelRadius * 1.9f, 0.05f), Vector3.zero, Mats.Get("timber"), new Vector3(i * 45f, 0, 0));
            return w;
        }

        Vector3 PointAt(float d)
        {
            float total = _cum[_cum.Length - 1];
            d = Mathf.Repeat(d, total);
            int i = 0;
            while (i < _route.Count - 1 && _cum[i + 1] < d) i++;
            float seg = _cum[i + 1] - _cum[i];
            float t = seg > 0f ? (d - _cum[i]) / seg : 0f;
            return Vector3.Lerp(_route[i], _route[(i + 1) % _route.Count], t);
        }

        void Place()
        {
            Vector3 p = PointAt(_dist);
            Vector3 ahead = PointAt(_dist + 1.5f);
            Vector3 back = PointAt(_dist - 1.5f);
            p.y = WorldLayout.HeightAt(p.x, p.z);
            ahead.y = WorldLayout.HeightAt(ahead.x, ahead.z);
            back.y = WorldLayout.HeightAt(back.x, back.z);
            Vector3 fwd = ahead - back;
            transform.position = p;
            if (fwd.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
        }

        public override void AgentUpdate(float dt)
        {
            float h = TimeManager.Instance.hours;
            bool daytime = h >= 7.5f && h < 18.5f;
            float target = MaxSpeed;

            // Στάση στα άκρα της διαδρομής
            Vector3 p = transform.position;
            if (_wait > 0f) { _wait -= dt; target = 0f; }
            else if (Mathf.Abs(p.x - EastEnd) < 0.6f && _lastEnd != 1) { _wait = 12f; _lastEnd = 1; }
            else if (Mathf.Abs(p.x - WestEnd) < 0.6f && _lastEnd != -1) { _wait = 12f; _lastEnd = -1; }
            // Τη νύχτα σταθμεύει στο δυτικό άκρο
            if (!daytime && Mathf.Abs(p.x - WestEnd) < 3f) target = 0f;
            // Αποφυγή παίκτη: αν βρίσκεται μπροστά, σε απόσταση < 4.5 m, σταματά
            PlayerController pl = PlayerController.Instance;
            if (pl != null)
            {
                Vector3 toP = pl.transform.position - (transform.position + transform.forward * 2.5f);
                if (toP.magnitude < 3.2f) target = 0f;
            }

            _speed = Mathf.MoveTowards(_speed, target, dt * 0.8f);
            float moved = _speed * dt;
            _dist += moved;
            Place();

            // Κύλιση τροχών και βήμα αλόγου
            float wheelDeg = moved / WheelRadius * Mathf.Rad2Deg;
            _wheelL.Rotate(wheelDeg, 0, 0, Space.Self);
            _wheelR.Rotate(wheelDeg, 0, 0, Space.Self);
            float phase = _dist * 3.2f;
            float amp = Mathf.Clamp01(_speed / MaxSpeed) * 25f;
            _horseLegs[0].localRotation = _horseLegs[3].localRotation = Quaternion.Euler(Mathf.Sin(phase) * amp, 0, 0);
            _horseLegs[1].localRotation = _horseLegs[2].localRotation = Quaternion.Euler(-Mathf.Sin(phase) * amp, 0, 0);
            _horse.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(phase)) * 0.03f * amp / 25f, 1.6f);
            _audio.volume = Mathf.Clamp01(_speed / MaxSpeed) * 0.6f;
        }
    }
}
