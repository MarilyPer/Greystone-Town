// =====================================================================================
//  Windmill.cs — Πυργόμυλος (tower mill)
//  ---------------------------------------------------------------------------------
//  * Κωνικός λίθινος πύργος με ξύλινο περιστρεφόμενο τρούλο (cap).
//  * Ο ΤΡΟΥΛΟΣ ΣΤΡΕΦΕΤΑΙ ΑΡΓΑ ΠΡΟΣ ΤΟΝ ΑΝΕΜΟ (όπως οι πραγματικοί tower mills με fantail).
//  * ΠΤΕΡΥΓΙΑ: η γωνιακή ταχύτητα-στόχος είναι ανάλογη της έντασης του ανέμου· η πραγματική
//    ταχύτητα την πλησιάζει σταδιακά (αδράνεια) — επιταχύνουν/επιβραδύνουν ομαλά.
//  * ΜΟΧΛΟΣ-ΦΡΕΝΟ: ο χρήστης σταματά/ξεκινά τον μύλο (Lever -> event Toggled).
//  * Ήχος μηχανισμού με ένταση και τόνο (pitch) ανάλογα της ταχύτητας.
// =====================================================================================
using UnityEngine;

namespace Greystone
{
    public class Windmill : MonoBehaviour
    {
        Transform _cap, _sails;
        float _speed;          // rad/s
        bool _brake;
        AudioSource _works;

        public void Build()
        {
            Material stone = Mats.Get("stone");
            Material wood = Mats.Get("wood_weathered");
            Material dark = Mats.Get("timber");
            const float towerH = 11f;

            Geo.Cyl(transform, 3.4f, towerH, new Vector3(0, towerH * 0.5f - 0.5f, 0), stone, 0.74f, 20, default(Vector3), true);
            Geo.Cyl(transform, 3.6f, 0.6f, new Vector3(0, 0f, 0), Mats.Get("stone_rubble"), 1f, 20);
            // Εξώστης (gallery) γύρω από τον πύργο
            Geo.Cyl(transform, 3.4f, 0.15f, new Vector3(0, 6.2f, 0), dark, 1f, 20);
            // Παράθυρα
            for (int i = 0; i < 3; i++)
            {
                float a = (i * 120f + 60f) * Mathf.Deg2Rad;
                float y = 3.5f + i * 1.8f;
                float r = 3.4f - 0.884f * (y + 0.5f) / towerH + 0.03f;   // ακτίνα του κωνικού πύργου στο ύψος y
                Transform w = Geo.Child(transform, "Window", new Vector3(Mathf.Sin(a) * r, y, Mathf.Cos(a) * r), new Vector3(-4f, a * Mathf.Rad2Deg, 0));
                Geo.Box(w, new Vector3(0.6f, 0.8f, 0.1f), new Vector3(0, 0, 0.05f), Mats.Get("window"));
                Geo.Box(w, new Vector3(0.8f, 0.12f, 0.16f), new Vector3(0, 0.46f, 0.05f), Mats.Get("stone_grey"));
            }

            // Πόρτα (κλειδωμένη) προς τον νότο-ανατολή, προς το χωριό
            GameObject dg = new GameObject("MillDoor");
            dg.transform.SetParent(transform, false);
            float doorAngle = 135f;
            dg.transform.localRotation = Quaternion.Euler(0, doorAngle, 0);
            dg.transform.localPosition = Quaternion.Euler(0, doorAngle, 0) * new Vector3(0, 0.05f, 3.42f);
            Door d = dg.AddComponent<Door>();
            d.locked = true;
            d.lockedMessage = "Ο μυλωνάς δεν επιτρέπει σε ξένους να ανέβουν στον μύλο — οι μυλόπετρες είναι επικίνδυνες.";
            d.Build(1.1f, 2.1f);

            // ---------------- Τρούλος (περιστρέφεται γύρω από τον κατακόρυφο άξονα) ----------------
            _cap = Geo.Child(transform, "Cap", new Vector3(0, towerH - 0.5f, 0));
            Geo.Cyl(_cap, 2.75f, 0.4f, new Vector3(0, 0.2f, 0), dark, 1f, 20);
            Geo.Ball(_cap, new Vector3(5.2f, 3.6f, 6.2f), new Vector3(0, 0.6f, 0), wood, 16, 10);
            Geo.Box(_cap, new Vector3(0.25f, 0.25f, 3.2f), new Vector3(0, 1.1f, 2.4f), dark);       // άξονας (windshaft)
            // Fantail: μικρή «ουρά» στο πίσω μέρος
            Transform tail = Geo.Child(_cap, "Fantail", new Vector3(0, 1.2f, -3.6f));
            Geo.Box(tail, new Vector3(0.12f, 0.12f, 1.6f), new Vector3(0, 0, 0.6f), dark);
            for (int i = 0; i < 6; i++)
                Geo.Box(tail, new Vector3(0.05f, 0.9f, 0.3f), Vector3.zero, wood, new Vector3(0, 0, i * 30f));

            // ---------------- Πτερύγια (4 βραχίονες με πλέγμα και πανί) ----------------
            _sails = Geo.Child(_cap, "Sails", new Vector3(0, 1.1f, 4.05f));
            Geo.Cyl(_sails, 0.35f, 0.5f, Vector3.zero, Mats.Get("iron"), 1f, 10, new Vector3(90, 0, 0));
            Material cloth = Mats.Solid(new Color(0.86f, 0.82f, 0.72f), 0.02f);
            for (int i = 0; i < 4; i++)
            {
                Transform arm = Geo.Child(_sails, "Arm", Vector3.zero, new Vector3(0, 0, i * 90f));
                Geo.Box(arm, new Vector3(0.22f, 7.2f, 0.18f), new Vector3(0, 3.8f, 0), dark);           // στύλος (stock)
                Geo.Box(arm, new Vector3(1.8f, 5.6f, 0.04f), new Vector3(0.95f, 4.4f, 0.06f), cloth);  // πανί
                for (int k = 0; k < 7; k++)                                                          // εγκάρσιες ράβδοι
                    Geo.Box(arm, new Vector3(1.9f, 0.05f, 0.06f), new Vector3(0.95f, 1.8f + k * 0.85f, 0f), dark);
                Geo.Box(arm, new Vector3(0.06f, 5.6f, 0.06f), new Vector3(1.85f, 4.4f, 0f), dark);
            }

            // ---------------- Μοχλός φρένου ----------------
            GameObject lv = new GameObject("BrakeLever");
            lv.transform.SetParent(transform, false);
            lv.transform.localPosition = Quaternion.Euler(0, doorAngle - 28f, 0) * new Vector3(0, 0, 4.2f);
            lv.transform.localRotation = Quaternion.Euler(0, doorAngle - 28f, 0);
            Lever lever = lv.AddComponent<Lever>();
            lever.onText = "Λύστε το φρένο του μύλου";
            lever.offText = "Βάλτε φρένο στον μύλο";
            lever.Build(false);
            lever.Toggled += on =>
            {
                _brake = on;
                if (Hud.Instance != null) Hud.Instance.Notify(on ? "Το φρένο έπιασε: τα πτερύγια επιβραδύνουν." : "Το φρένο λύθηκε: ο άνεμος παίρνει τα πτερύγια.");
            };

            _works = SoundBank.Instance.CreateLoop(transform, new Vector3(0, 8f, 0), "millworks", 0f, 3f, 45f);
        }

        void Update()
        {
            WeatherSystem w = WeatherSystem.Instance;
            float wind = w != null ? w.WindStrength : 0.4f;
            Vector2 dir = w != null ? w.WindDirection : new Vector2(1, 0);

            // Ο τρούλος κοιτά ΠΡΟΣ τον άνεμο (τα πτερύγια δέχονται τον άνεμο από μπροστά)
            float targetYaw = Mathf.Atan2(-dir.x, -dir.y) * Mathf.Rad2Deg;
            float yaw = Mathf.MoveTowardsAngle(_cap.localEulerAngles.y, targetYaw, Time.deltaTime * 3f);
            _cap.localRotation = Quaternion.Euler(0, yaw, 0);

            // Αδράνεια πτερυγίων: η ταχύτητα προσεγγίζει τον στόχο εκθετικά
            float target = _brake ? 0f : Mathf.Clamp(wind, 0.1f, 1.3f) * 1.3f;
            float rate = _brake ? 0.6f : 0.15f;   // το φρένο δρα γρηγορότερα από την επιτάχυνση
            _speed = Mathf.Lerp(_speed, target, 1f - Mathf.Exp(-Time.deltaTime * rate));
            _sails.Rotate(0f, 0f, -_speed * Mathf.Rad2Deg * Time.deltaTime, Space.Self);

            _works.volume = Mathf.Clamp01(_speed / 1.3f) * 0.7f;
            _works.pitch = 0.7f + _speed * 0.3f;
        }
    }
}
