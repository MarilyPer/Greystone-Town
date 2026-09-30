// =====================================================================================
//  HurdyGurdy.cs — Λύρα με τροχό (hurdy-gurdy) στην πλατεία
//  ---------------------------------------------------------------------------------
//  Λειτουργικότητα «ραδιοφώνου»: [E] παίζει/σταματά τη μελωδία, η ΡΟΔΕΛΑ του ποντικιού
//  (όταν ο χρήστης κοιτά το όργανο) ρυθμίζει την ένταση (0–100%). Όσο παίζει, η μανιβέλα
//  και ο τροχός περιστρέφονται (οπτική ανάδραση). Ο ήχος είναι τρισδιάστατος: ακούγεται
//  δυνατότερα όσο πλησιάζει ο χρήστης.
// =====================================================================================
using UnityEngine;

namespace Greystone
{
    public class HurdyGurdy : MonoBehaviour
    {
        AudioSource _audio;
        Transform _crank, _wheel;
        float _volume = 0.7f;
        bool _playing;

        public void Build()
        {
            Material wood = Mats.Get("wood");
            Material dark = Mats.Get("timber");
            // Τρίποδο αναλόγιο
            Geo.Box(transform, new Vector3(0.9f, 0.06f, 0.5f), new Vector3(0, 0.95f, 0), dark, default(Vector3), true);
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f * Mathf.Deg2Rad;
                Geo.Beam(transform, new Vector3(Mathf.Sin(a) * 0.25f, 0.93f, Mathf.Cos(a) * 0.18f), new Vector3(Mathf.Sin(a) * 0.45f, 0f, Mathf.Cos(a) * 0.35f), 0.03f, dark);
            }
            // Σώμα οργάνου (ηχείο σε σχήμα «μισού αχλαδιού»)
            Transform body = Geo.Child(transform, "Body", new Vector3(0, 1.12f, 0), new Vector3(0, 0, 8));
            Geo.Ball(body, new Vector3(0.75f, 0.3f, 0.36f), Vector3.zero, wood, 14, 8);
            Geo.Box(body, new Vector3(0.55f, 0.1f, 0.14f), new Vector3(0.1f, 0.14f, 0), dark);   // κουτί πλήκτρων
            for (int k = 0; k < 8; k++)
                Geo.Box(body, new Vector3(0.025f, 0.04f, 0.05f), new Vector3(-0.12f + k * 0.06f, 0.1f, 0.1f), Mats.Get("plaster"));
            Geo.Box(body, new Vector3(0.1f, 0.12f, 0.08f), new Vector3(0.42f, 0.1f, 0), dark);   // κεφαλή κουρδίσματος
            _wheel = Geo.Child(body, "Wheel", new Vector3(-0.28f, 0.05f, 0));
            Geo.Cyl(_wheel, 0.12f, 0.03f, Vector3.zero, Mats.Get("wood_weathered"), 1f, 12, new Vector3(90, 0, 0));
            _crank = Geo.Child(body, "Crank", new Vector3(-0.42f, 0.02f, 0));
            Geo.Box(_crank, new Vector3(0.03f, 0.14f, 0.03f), new Vector3(0, 0.06f, 0), Mats.Get("iron"));
            Geo.Cyl(_crank, 0.02f, 0.08f, new Vector3(0, 0.12f, -0.04f), Mats.Get("wood"), 1f, 6, new Vector3(90, 0, 0));
            // Χορδές
            for (int s = 0; s < 3; s++)
                Geo.Beam(body, new Vector3(-0.34f, 0.12f, -0.03f + s * 0.03f), new Vector3(0.42f, 0.16f, -0.03f + s * 0.03f), 0.003f, Mats.Get("iron"));

            _audio = SoundBank.Instance.CreateLoop(transform, new Vector3(0, 1.1f, 0), "hurdy", _volume, 2f, 55f, false);

            Interactable it = Interactable.Create(transform, new Vector3(0, 1f, 0), new Vector3(1.2f, 1.6f, 1.2f), "");
            it.acceptsScroll = true;
            it.PromptProvider = () => (_playing ? "Σταματήστε τη μουσική" : "Παίξτε τη λύρα") + "   ·   Ροδέλα: ένταση " + Mathf.RoundToInt(_volume * 100f) + "%";
            it.Interacted += p => Toggle();
            it.Scrolled += step =>
            {
                _volume = Mathf.Clamp01(_volume + step * 0.1f);
                _audio.volume = _volume;
            };
        }

        void Toggle()
        {
            _playing = !_playing;
            if (_playing) { _audio.time = 0f; _audio.Play(); }
            else _audio.Stop();
        }

        void Update()
        {
            if (!_playing) return;
            float spin = 360f * 1.4f * Time.deltaTime;
            _crank.Rotate(0f, 0f, -spin, Space.Self);
            _wheel.Rotate(0f, 0f, -spin, Space.Self);   // ο τροχός έχει άξονα τον τοπικό z
        }
    }
}
