// =====================================================================================
//  WallLantern.cs — Φανάρι τοίχου / δαυλός
//  ---------------------------------------------------------------------------------
//  Ανάβει αυτόματα όταν νυχτώνει (event TimeManager.NightChanged — Observer) και σβήνει
//  την αυγή. Το γυαλί του φαναριού χρησιμοποιεί το κοινό υλικό "lantern_glass" του οποίου η
//  εκπομπή ελέγχεται από την καθολική μεταβλητή _GS_NightGlow, άρα δεν χρειάζεται καμία
//  αλλαγή υλικού ανά φανάρι. Το σημειακό φως (Light) διαχειρίζεται το LightLod, ενώ ένα
//  ελαφρύ τρεμόπαιγμα της έντασης μιμείται τη φλόγα του κεριού.
// =====================================================================================
using UnityEngine;

namespace Greystone
{
    public class WallLantern : MonoBehaviour
    {
        Light _light;
        float _baseIntensity;
        float _seed;
        ParticleSystem _flame;
        bool _isTorch;

        /// <summary>Φανάρι σε μεταλλικό βραχίονα (για τοίχους σπιτιών).</summary>
        public void BuildWallLantern()
        {
            Material iron = Mats.Get("iron");
            Geo.Box(transform, new Vector3(0.05f, 0.05f, 0.35f), new Vector3(0, 0.25f, 0.17f), iron);
            Geo.Box(transform, new Vector3(0.05f, 0.25f, 0.05f), new Vector3(0, 0.15f, 0.02f), iron);
            Transform lamp = Geo.Child(transform, "Lamp", new Vector3(0, 0.02f, 0.33f));
            Geo.Box(lamp, new Vector3(0.2f, 0.28f, 0.2f), Vector3.zero, Mats.Get("lantern_glass"));
            Geo.Box(lamp, new Vector3(0.24f, 0.03f, 0.24f), new Vector3(0, 0.15f, 0), iron);
            Geo.Box(lamp, new Vector3(0.24f, 0.03f, 0.24f), new Vector3(0, -0.15f, 0), iron);
            Geo.Cyl(lamp, 0.1f, 0.12f, new Vector3(0, 0.22f, 0), iron, 0f, 6);
            SetupLight(new Vector3(0, 0.02f, 0.33f), 9f, 1.5f);
        }

        /// <summary>Ξύλινος στύλος με φανάρι στην κορυφή (πλατεία, γέφυρα).</summary>
        public void BuildPostLantern()
        {
            Material wood = Mats.Get("timber");
            Geo.Cyl(transform, 0.09f, 3f, new Vector3(0, 1.5f, 0), wood, 0.8f, 8, default(Vector3), true);
            Geo.Box(transform, new Vector3(0.6f, 0.07f, 0.07f), new Vector3(0.25f, 2.9f, 0), wood);
            Transform lamp = Geo.Child(transform, "Lamp", new Vector3(0.48f, 2.62f, 0));
            Material iron = Mats.Get("iron");
            Geo.Box(lamp, new Vector3(0.22f, 0.3f, 0.22f), Vector3.zero, Mats.Get("lantern_glass"));
            Geo.Box(lamp, new Vector3(0.26f, 0.03f, 0.26f), new Vector3(0, 0.16f, 0), iron);
            Geo.Box(lamp, new Vector3(0.26f, 0.03f, 0.26f), new Vector3(0, -0.16f, 0), iron);
            Geo.Cyl(lamp, 0.11f, 0.12f, new Vector3(0, 0.23f, 0), iron, 0f, 6);
            SetupLight(new Vector3(0.48f, 2.55f, 0), 11f, 1.6f);
        }

        /// <summary>Δαυλός σε σιδερένια βάση με πραγματική φλόγα (σωματίδια).</summary>
        public void BuildTorch()
        {
            _isTorch = true;
            Material iron = Mats.Get("iron");
            Geo.Cyl(transform, 0.06f, 2.2f, new Vector3(0, 1.1f, 0), iron, 0.7f, 6, default(Vector3), true);
            Geo.Cyl(transform, 0.14f, 0.25f, new Vector3(0, 2.3f, 0), iron, 1.4f, 8);
            Geo.Cyl(transform, 0.1f, 0.2f, new Vector3(0, 2.45f, 0), Mats.Get("ember"), 1f, 8);
            _flame = FX.Fire(transform, new Vector3(0, 2.55f, 0), 0.8f);
            SetupLight(new Vector3(0, 2.8f, 0), 12f, 1.8f);
        }

        void SetupLight(Vector3 localPos, float range, float intensity)
        {
            _light = FX.PointLight(transform, localPos, new Color(1f, 0.66f, 0.34f), range, intensity);
            _baseIntensity = intensity;
            _seed = Random.value * 100f;
            LightLod.Register(_light);
            TimeManager.Instance.NightChanged += OnNight;
            OnNight(TimeManager.Instance.IsNight);
        }

        void OnDestroy()
        {
            if (TimeManager.Instance != null) TimeManager.Instance.NightChanged -= OnNight;
        }

        void OnNight(bool night)
        {
            LightLod.SetWanted(_light, night);
            if (_flame != null)
            {
                if (night) _flame.Play(); else _flame.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        void Update()
        {
            if (_light == null || !_light.enabled) return;
            // Τρεμόπαιγμα: άθροισμα θορύβου Perlin (ομαλό) — ο δαυλός τρεμοπαίζει πιο έντονα.
            float n = Mathf.PerlinNoise(_seed, Time.time * (_isTorch ? 7f : 3f));
            _light.intensity = _baseIntensity * (_isTorch ? (0.7f + 0.5f * n) : (0.85f + 0.25f * n));
        }
    }
}
