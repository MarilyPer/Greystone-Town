// =====================================================================================
//  NightLight.cs — Φως που ανάβει τη νύχτα (πυρσοί, φανάρια, κεριά)
//  ---------------------------------------------------------------------------------
//  Προστίθεται σε οποιοδήποτε prefab έχει Light ή/και ParticleSystem (π.χ. οι πυρσοί του
//  Viking Village). Όταν νυχτώνει ανάβει τα φώτα και τη φωτιά, όταν ξημερώνει τα σβήνει.
//
//  ΒΕΛΤΙΣΤΟΠΟΙΗΣΗ:
//   * Δεν ελέγχει την ώρα σε κάθε frame: εγγράφεται στο event DayNightCycle.NightChanged.
//   * «LOD φώτων»: κάθε σημειακό φως στο URP Forward προσθέτει κόστος σε κάθε pixel που
//     φωτίζει. Τέσσερις φορές το δευτερόλεπτο ελέγχουμε την απόσταση από την κάμερα και
//     σβήνουμε τα φώτα που απέχουν περισσότερο από maxLightDistance (η φλόγα των σωματιδίων
//     μένει, ώστε από μακριά ο πυρσός να φαίνεται αναμμένος).
//   * Το τρεμόπαιγμα (flicker) υπολογίζεται μόνο για τα φώτα που είναι ενεργά.
// =====================================================================================
using UnityEngine;

namespace Greystone
{
    public class NightLight : MonoBehaviour
    {
        [Tooltip("Ανάβει μόνο τη νύχτα (αλλιώς πάντα αναμμένο, π.χ. τζάκι)")]
        public bool onlyAtNight = true;
        [Tooltip("Πέρα από αυτή την απόσταση από την κάμερα το φως σβήνει (η φωτιά συνεχίζει)")]
        public float maxLightDistance = 45f;
        [Range(0f, 1f)] public float flicker = 0.25f;

        Light[] _lights;
        float[] _baseIntensity;
        ParticleSystem[] _particles;
        bool _lit;
        float _timer, _seed;

        void Awake()
        {
            _lights = GetComponentsInChildren<Light>(true);
            _baseIntensity = new float[_lights.Length];
            for (int i = 0; i < _lights.Length; i++)
            {
                _baseIntensity[i] = _lights[i].intensity;
                _lights[i].shadows = LightShadows.None;   // οι πυρσοί δεν ρίχνουν σκιές (πολύ ακριβό)
            }
            _particles = GetComponentsInChildren<ParticleSystem>(true);
            _seed = Random.value * 100f;
            _timer = Random.value * 0.25f;   // διασπορά ελέγχων ανάμεσα στα frames
        }

        void OnEnable()
        {
            DayNightCycle dn = DayNightCycle.Instance != null ? DayNightCycle.Instance : Find.First<DayNightCycle>();
            if (dn != null)
            {
                dn.NightChanged += SetLit;
                SetLit(!onlyAtNight || dn.IsNight);
            }
            else SetLit(true);
        }

        void OnDisable()
        {
            if (DayNightCycle.Instance != null) DayNightCycle.Instance.NightChanged -= SetLit;
        }

        void SetLit(bool night)
        {
            _lit = !onlyAtNight || night;
            foreach (ParticleSystem p in _particles)
            {
                if (_lit) p.Play(true);
                else p.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            if (!_lit) foreach (Light l in _lights) l.enabled = false;
            _timer = 0f;
        }

        void Update()
        {
            if (!_lit) return;

            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                _timer = 0.25f;
                Camera cam = Camera.main;
                bool near = cam == null || (cam.transform.position - transform.position).sqrMagnitude < maxLightDistance * maxLightDistance;
                foreach (Light l in _lights) if (l.enabled != near) l.enabled = near;
            }

            // Τρεμόπαιγμα με θόρυβο Perlin (ομαλό, όχι «νευρικό» όπως το Random)
            float n = Mathf.PerlinNoise(_seed, Time.time * 6f);
            for (int i = 0; i < _lights.Length; i++)
                if (_lights[i].enabled) _lights[i].intensity = _baseIntensity[i] * (1f - flicker * 0.5f + flicker * n);
        }
    }
}
