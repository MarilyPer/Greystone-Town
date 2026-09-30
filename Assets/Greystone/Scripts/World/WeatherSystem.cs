// =====================================================================================
//  WeatherSystem.cs — Καιρός & ηχητική ατμόσφαιρα
//  ---------------------------------------------------------------------------------
//  Ο καιρός είναι μια ΜΑΡΚΟΒΙΑΝΗ ΑΛΥΣΙΔΑ τριών καταστάσεων (Αίθριος, Συννεφιά, Βροχή):
//  σε κάθε αλλαγή ώρας του παιχνιδιού υπάρχει πιθανότητα μετάβασης σε γειτονική κατάσταση.
//  Οι τιμές (κάλυψη νεφών, βροχή, άνεμος) δεν αλλάζουν απότομα αλλά «κυλούν» ομαλά προς
//  τους στόχους της νέας κατάστασης (εκθετική εξομάλυνση).
//
//  Επιδράσεις: σύννεφα/χρώματα ουρανού (SkySystem), σωματίδια βροχής γύρω από την
//  κάμερα, «βρεγμένες» επιφάνειες (καθολικό _GS_Wetness), ένταση ανέμου στη βλάστηση
//  (καθολικό _GS_WindStrength), κατεύθυνση καπνών, ταχύτητα ανεμόμυλου, ήχοι.
//  Ο χρήστης μπορεί να αλλάξει καιρό με το πλήκτρο R.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Greystone
{
    public enum WeatherState { Clear, Cloudy, Rain }

    public class WeatherSystem : MonoBehaviour
    {
        public static WeatherSystem Instance { get; private set; }

        public WeatherState State { get; private set; }
        public float CloudCover { get; private set; }
        public float RainAmount { get; private set; }
        public float WindStrength { get; private set; }
        public Vector2 WindDirection { get; private set; }
        public float Wetness { get; private set; }

        float _targetCloud, _targetRain, _targetWind;
        float _windAngle, _targetWindAngle;
        ParticleSystem _rain;
        AudioSource _rainAudio, _windAudio, _birdAudio, _cricketAudio;
        static readonly List<ParticleSystem> Smokes = new List<ParticleSystem>();
        float _smokeTimer;

        public static void RegisterSmoke(ParticleSystem ps) { Smokes.Add(ps); }

        public string StateName
        {
            get
            {
                switch (State)
                {
                    case WeatherState.Clear: return "Αίθριος";
                    case WeatherState.Cloudy: return "Συννεφιά";
                    default: return "Βροχή";
                }
            }
        }

        public void Build()
        {
            Instance = this;
            _windAngle = _targetWindAngle = 20f;
            SetState(WeatherState.Clear, true);

            _rain = FX.Rain(transform);
            SoundBank sb = SoundBank.Instance;
            _rainAudio = Make2DLoop("rain");
            _windAudio = Make2DLoop("wind");
            _birdAudio = Make2DLoop("birdsong");
            _cricketAudio = Make2DLoop("crickets");
            TimeManager.Instance.HourChanged += OnHourChanged;
        }

        AudioSource Make2DLoop(string clip)
        {
            AudioSource a = SoundBank.Instance.CreateLoop(transform, Vector3.zero, clip, 0f, 1f, 10f);
            a.spatialBlend = 0f;   // ατμόσφαιρα: χωρίς χωρικότητα
            return a;
        }

        void OnDestroy()
        {
            if (TimeManager.Instance != null) TimeManager.Instance.HourChanged -= OnHourChanged;
            Smokes.Clear();
        }

        /// <summary>Μαρκοβιανή μετάβαση: από κάθε κατάσταση πηγαίνουμε μόνο σε «γειτονική».</summary>
        void OnHourChanged(int hour)
        {
            float r = Random.value;
            switch (State)
            {
                case WeatherState.Clear: if (r < 0.15f) SetState(WeatherState.Cloudy); break;
                case WeatherState.Cloudy: if (r < 0.2f) SetState(WeatherState.Rain); else if (r < 0.45f) SetState(WeatherState.Clear); break;
                case WeatherState.Rain: if (r < 0.35f) SetState(WeatherState.Cloudy); break;
            }
            // Ο άνεμος αλλάζει σιγά σιγά κατεύθυνση
            _targetWindAngle += Random.Range(-35f, 35f);
        }

        public void SetState(WeatherState s, bool instant = false)
        {
            State = s;
            switch (s)
            {
                case WeatherState.Clear: _targetCloud = 0.3f; _targetRain = 0f; _targetWind = 0.35f; break;
                case WeatherState.Cloudy: _targetCloud = 0.7f; _targetRain = 0f; _targetWind = 0.65f; break;
                case WeatherState.Rain: _targetCloud = 0.95f; _targetRain = 1f; _targetWind = 1.0f; break;
            }
            if (instant)
            {
                CloudCover = _targetCloud;
                RainAmount = _targetRain;
                WindStrength = _targetWind;
            }
        }

        public void CycleWeather()
        {
            SetState((WeatherState)(((int)State + 1) % 3));
        }

        void Update()
        {
            float dt = Time.deltaTime;
            // Εκθετική εξομάλυνση προς τους στόχους (ανεξάρτητη από το frame rate)
            float k = 1f - Mathf.Exp(-dt * 0.25f);
            CloudCover = Mathf.Lerp(CloudCover, _targetCloud, k);
            RainAmount = Mathf.Lerp(RainAmount, _targetRain, k * 1.5f);
            WindStrength = Mathf.Lerp(WindStrength, _targetWind + Mathf.Sin(Time.time * 0.37f) * 0.08f, k);
            _windAngle = Mathf.LerpAngle(_windAngle, _targetWindAngle, 1f - Mathf.Exp(-dt * 0.05f));
            WindDirection = new Vector2(Mathf.Cos(_windAngle * Mathf.Deg2Rad), Mathf.Sin(_windAngle * Mathf.Deg2Rad));

            // Υγρασία: βρέχεται γρήγορα, στεγνώνει αργά
            Wetness = Mathf.MoveTowards(Wetness, RainAmount > 0.3f ? 1f : 0f, dt * (RainAmount > 0.3f ? 0.08f : 0.015f));

            // Καθολικές τιμές shader: μία κλήση επηρεάζει όλα τα υλικά του κόσμου.
            Shader.SetGlobalFloat(Mats.GlobalWetness, Wetness);
            Shader.SetGlobalFloat(Mats.GlobalWindStrength, WindStrength);
            Shader.SetGlobalVector(Mats.GlobalWindDir, new Vector4(WindDirection.x, WindDirection.y, 0, 0));

            // ---------------- Βροχή γύρω από την κάμερα ----------------
            Camera cam = Camera.main;
            bool indoors = PlayerController.Instance != null && PlayerController.Instance.Indoors;
            if (cam != null) transform.position = cam.transform.position;
            ParticleSystem.EmissionModule em = _rain.emission;
            em.rateOverTime = indoors ? 0f : RainAmount * 2400f;

            // ---------------- Ήχοι ατμόσφαιρας ----------------
            float daylight = TimeManager.Instance.Daylight;
            float muffle = indoors ? 0.35f : 1f;
            _rainAudio.volume = RainAmount * 0.55f * muffle;
            _windAudio.volume = Mathf.Clamp01(WindStrength * 0.35f) * muffle;
            _birdAudio.volume = daylight * (1f - RainAmount) * 0.35f * muffle;
            float h = TimeManager.Instance.hours;
            _cricketAudio.volume = (1f - daylight) * (1f - RainAmount) * 0.25f * muffle * ((h > 20f || h < 4f) ? 1f : 0.3f);

            // ---------------- Καπνοί: ο άνεμος τους παρασύρει (ενημέρωση 2 φορές/δευτ.) ----------------
            _smokeTimer -= dt;
            if (_smokeTimer <= 0f)
            {
                _smokeTimer = 0.5f;
                Vector2 f = WindDirection * WindStrength * 0.6f;
                for (int i = 0; i < Smokes.Count; i++)
                {
                    if (Smokes[i] == null) continue;
                    ParticleSystem.ForceOverLifetimeModule fo = Smokes[i].forceOverLifetime;
                    fo.x = new ParticleSystem.MinMaxCurve(f.x);
                    fo.z = new ParticleSystem.MinMaxCurve(f.y);
                }
            }
        }
    }
}
