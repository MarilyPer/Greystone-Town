// =====================================================================================
//  Forge.cs — Σιδηρουργείο
//  ---------------------------------------------------------------------------------
//  Ανοιχτό υπόστεγο με καμίνι, καπνοδόχο, αμόνι, φυσερό και βαρέλι νερού.
//  * Η φωτιά του καμινιού είναι πάντα αναμμένη (σωματίδια + τρεμάμενο φως + ήχος).
//  * ΦΥΣΕΡΟ (αλληλεπίδραση): συμπιέζεται (animation κλίμακας), η φωτιά δυναμώνει για λίγα
//    δευτερόλεπτα (περισσότερα σωματίδια, ισχυρότερο φως) και σιγά σιγά επανέρχεται.
//  * Όταν ο σιδεράς (Villager με ρόλο Smith) δουλεύει, καλεί Strike(): σπίθες + ήχος αμονιού.
// =====================================================================================
using System.Collections;
using UnityEngine;

namespace Greystone
{
    public class Forge : MonoBehaviour
    {
        ParticleSystem _fire, _sparks;
        Light _light;
        Transform _bellowsTop;
        float _boost;           // 0..1 ενίσχυση από το φυσερό
        float _baseRate = 35f;
        bool _pumping;

        public Vector3 AnvilWorkSpot { get { return transform.TransformPoint(new Vector3(0.9f, 0, 1.3f)); } }
        public Vector3 AnvilWorld { get { return transform.TransformPoint(new Vector3(0.9f, 0.85f, 2.1f)); } }

        public void Build()
        {
            Transform st = Geo.Child(transform, "Static", Vector3.zero);
            Material wood = Mats.Get("timber");
            Material stone = Mats.Get("stone_rubble");
            // Πίσω τοίχος και πλαϊνός
            Geo.Box(st, new Vector3(6f, 3.4f, 0.4f), new Vector3(0, 1.4f, -1.8f), stone, default(Vector3), true);
            Geo.Box(st, new Vector3(0.4f, 3.4f, 3.8f), new Vector3(-2.8f, 1.4f, 0), stone, default(Vector3), true);
            // Στύλοι και στέγη μονόρριχτη
            Geo.Box(st, new Vector3(0.22f, 3.0f, 0.22f), new Vector3(2.8f, 1.5f, 1.8f), wood, default(Vector3), true);
            Geo.Box(st, new Vector3(0.22f, 3.0f, 0.22f), new Vector3(-2.7f, 1.5f, 1.8f), wood, default(Vector3), true);
            Geo.Box(st, new Vector3(6.6f, 0.2f, 4.6f), new Vector3(0, 3.35f, 0.1f), Mats.Get("roof_slate"), new Vector3(-10f, 0, 0));
            // Καμίνι
            Geo.Box(st, new Vector3(1.8f, 0.9f, 1.3f), new Vector3(-1.4f, 0.45f, -1.0f), stone, default(Vector3), true);
            Geo.Box(st, new Vector3(1.1f, 0.12f, 0.7f), new Vector3(-1.4f, 0.93f, -0.95f), Mats.Get("ember"));
            Geo.Cyl(st, 0.9f, 1.3f, new Vector3(-1.4f, 2.3f, -1.1f), stone, 0.45f, 10);          // χοάνη καπνού
            Geo.Box(st, new Vector3(0.6f, 2.2f, 0.6f), new Vector3(-1.4f, 3.9f, -1.3f), stone);  // καπνοδόχος
            _fire = FX.Fire(transform, new Vector3(-1.4f, 1.0f, -0.95f), 1.2f);
            FX.Smoke(transform, new Vector3(-1.4f, 5.1f, -1.3f), 1.2f).Play();
            _light = FX.PointLight(transform, new Vector3(-1.2f, 1.5f, 0f), new Color(1f, 0.5f, 0.2f), 8f, 2f);
            Flicker fl = gameObject.AddComponent<Flicker>();
            fl.target = _light;
            SoundBank.Instance.CreateLoop(transform, new Vector3(-1.4f, 1f, -1f), "fire", 0.45f, 1f, 16f);

            // Αμόνι σε κορμό
            Transform anvil = Geo.Child(transform, "Anvil", new Vector3(0.9f, 0, 2.1f));
            Geo.Cyl(anvil, 0.35f, 0.55f, new Vector3(0, 0.27f, 0), Mats.Get("bark"), 1f, 10, default(Vector3), true);
            Material iron = Mats.Get("iron");
            Geo.Box(anvil, new Vector3(0.3f, 0.18f, 0.25f), new Vector3(0, 0.63f, 0), iron);
            Geo.Box(anvil, new Vector3(0.6f, 0.14f, 0.22f), new Vector3(0, 0.78f, 0), iron);
            Geo.Cyl(anvil, 0.08f, 0.3f, new Vector3(0.42f, 0.78f, 0), iron, 0f, 8, new Vector3(0, 0, -90));   // κέρας
            _sparks = FX.Sparks(transform, new Vector3(0.9f, 0.88f, 2.1f));

            // Φυσερό (δερμάτινο) δίπλα στο καμίνι
            Transform bellows = Geo.Child(transform, "Bellows", new Vector3(-2.35f, 0.7f, -0.2f), new Vector3(0, 90, 0));
            Geo.Box(bellows, new Vector3(0.7f, 0.06f, 1.0f), Vector3.zero, Mats.Get("wood"));
            _bellowsTop = Geo.Child(bellows, "Top", new Vector3(0, 0.3f, 0));
            Geo.Box(_bellowsTop, new Vector3(0.7f, 0.06f, 1.0f), Vector3.zero, Mats.Get("wood"));
            Geo.Box(_bellowsTop, new Vector3(0.06f, 0.06f, 0.6f), new Vector3(0, 0.05f, -0.75f), Mats.Get("wood"));
            Geo.Box(bellows, new Vector3(0.62f, 0.28f, 0.9f), new Vector3(0, 0.15f, 0), Mats.Get("leather"));
            Geo.Cyl(bellows, 0.06f, 0.6f, new Vector3(0, 0.12f, 0.75f), iron, 0.6f, 6, new Vector3(90, 0, 0));
            Interactable it = Interactable.Create(bellows, new Vector3(0, 0.3f, 0), new Vector3(1.2f, 1f, 1.6f), "");
            it.PromptProvider = () => _pumping ? "Το φυσερό δουλεύει…" : "Πιέστε το φυσερό";
            it.Interacted += p => { if (!_pumping) StartCoroutine(Pump()); };

            // Βαρέλι νερού για «βάψιμο» και εργαλεία στον τοίχο
            House.Barrel(transform, new Vector3(2.2f, 0.5f, -1.1f));
            Geo.Cyl(transform, 0.34f, 0.02f, new Vector3(2.2f, 0.95f, -1.1f), Mats.Get("well_water"), 1f, 12);
            for (int i = 0; i < 4; i++)
                Geo.Box(transform, new Vector3(0.05f, 0.7f, 0.05f), new Vector3(0.2f + i * 0.35f, 2.0f, -1.55f), iron, new Vector3(0, 0, 8f * (i - 1.5f)));
        }

        IEnumerator Pump()
        {
            _pumping = true;
            for (int i = 0; i < 2; i++)
            {
                SoundBank.Instance.PlayAt("bellows", transform.position, 0.8f, 1f, 20f);
                yield return Anim.MoveLocal(_bellowsTop, new Vector3(0, 0.05f, 0), 0.5f);
                _boost = 1f;
                yield return Anim.MoveLocal(_bellowsTop, new Vector3(0, 0.3f, 0), 0.6f);
            }
            if (Hud.Instance != null) Hud.Instance.Notify("Η φωτιά φουντώνει: τα κάρβουνα γίνονται κιτρινόλευκα.");
            _pumping = false;
        }

        /// <summary>Χτύπημα σφυριού στο αμόνι (καλείται από τον σιδερά).</summary>
        public void Strike()
        {
            _sparks.Emit(Random.Range(10, 22));
            SoundBank.Instance.PlayAt("clang", AnvilWorld, 0.8f, Random.Range(0.95f, 1.05f), 60f);
        }

        void Update()
        {
            _boost = Mathf.MoveTowards(_boost, 0f, Time.deltaTime * 0.12f);
            ParticleSystem.EmissionModule em = _fire.emission;
            em.rateOverTime = _baseRate * (1f + _boost * 1.5f);
            ParticleSystem.MainModule main = _fire.main;
            main.startSpeedMultiplier = 1f + _boost;
            _light.range = 8f + _boost * 4f;
        }
    }
}
