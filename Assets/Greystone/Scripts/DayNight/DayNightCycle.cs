// =====================================================================================
//  DayNightCycle.cs — Κύκλος μέρας / νύχτας
//  ---------------------------------------------------------------------------------
//  Κρατά την ώρα του κόσμου και σε κάθε frame:
//   * τοποθετεί τον ΗΛΙΟ με αστρονομικό τύπο (γεωγραφικό πλάτος / ηλιακή απόκλιση), ώστε η
//     ανατολή, η δύση και το ύψος του μεσημεριού να είναι ρεαλιστικά,
//   * τοποθετεί τη ΣΕΛΗΝΗ απέναντι από τον ήλιο,
//   * αλλάζει ένταση/χρώμα των φώτων (πορτοκαλί στην ανατολή/δύση, λευκό το μεσημέρι),
//   * ενημερώνει τον ουρανό (skybox), τον περιβάλλοντα φωτισμό και την ομίχλη,
//   * ειδοποιεί τα υπόλοιπα συστήματα όταν νυχτώνει/ξημερώνει (event NightChanged).
//
//  Μοτίβο Observer: οι πυρσοί (NightLight) και οι κάτοικοι (VillagerAI) εγγράφονται στα
//  events, αντί να ελέγχουν την ώρα σε κάθε frame (λιγότερη δουλειά για τη CPU).
//
//  Όλες οι παράμετροι ρυθμίζονται από τον Inspector.
// =====================================================================================
using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Greystone
{
    public class DayNightCycle : MonoBehaviour
    {
        public static DayNightCycle Instance { get; private set; }

        [Header("Χρόνος")]
        [Tooltip("Ώρα της ημέρας (0–24)")]
        [Range(0f, 24f)] public float timeOfDay = 8.5f;
        [Tooltip("Πόσα πραγματικά λεπτά διαρκεί μία ολόκληρη μέρα του παιχνιδιού")]
        public float dayLengthMinutes = 16f;
        [Tooltip("Πολλαπλασιαστές ταχύτητας που εναλλάσσονται με το πλήκτρο T")]
        public float[] speedPresets = { 1f, 10f, 60f };
        public int day = 1;

        [Header("Φώτα")]
        public Light sun;
        public Light moon;
        public float sunMaxIntensity = 1.6f;
        public float moonMaxIntensity = 0.25f;
        public Gradient sunColor;

        [Header("Ουρανός & ατμόσφαιρα")]
        [Tooltip("Υλικό ουρανού με τον shader Greystone/Sky (αστέρια, σελήνη, σύννεφα). Αν μείνει κενό χρησιμοποιείται το skybox της σκηνής.")]
        public Material skyMaterial;
        public bool controlFog = true;
        public bool controlAmbient = true;

        [Header("Αστρονομία")]
        [Tooltip("Γεωγραφικό πλάτος σε μοίρες (π.χ. 52 για τη βόρεια Ευρώπη)")]
        public float latitude = 52f;
        [Tooltip("Ηλιακή απόκλιση σε μοίρες (+23 θερινό ηλιοστάσιο, 0 ισημερία, −23 χειμερινό)")]
        public float declination = 12f;
        [Tooltip("Περιστροφή του ορίζοντα (αζιμούθιο) ώστε να ταιριάζει με τη διάταξη του χωριού")]
        public float northOffset = 0f;

        /// <summary>Ειδοποίηση αλλαγής ώρας (ακέραια ώρα).</summary>
        public event Action<int> HourChanged;
        /// <summary>Ειδοποίηση όταν νυχτώνει (true) ή ξημερώνει (false).</summary>
        public event Action<bool> NightChanged;

        public bool IsNight { get; private set; }
        public int SpeedIndex { get; private set; }
        public float SpeedMultiplier { get { return speedPresets[Mathf.Clamp(SpeedIndex, 0, speedPresets.Length - 1)]; } }

        /// <summary>Φως ημέρας 0..1 (ομαλή μετάβαση στο λυκόφως).</summary>
        public float Daylight { get; private set; }

        // Χρώματα ουρανού: νύχτα / λυκόφως / μέρα
        static readonly Color ZenithNight = new Color(0.01f, 0.015f, 0.045f);
        static readonly Color HorizonNight = new Color(0.04f, 0.05f, 0.1f);
        static readonly Color ZenithDay = new Color(0.2f, 0.42f, 0.82f);
        static readonly Color HorizonDay = new Color(0.66f, 0.78f, 0.9f);
        static readonly Color HorizonDusk = new Color(0.95f, 0.5f, 0.28f);

        Vector2 _cloudOffset;

        void Reset()
        {
            sunColor = DefaultSunGradient();
        }

        void Awake()
        {
            Instance = this;
            if (sunColor == null || sunColor.colorKeys.Length < 2) sunColor = DefaultSunGradient();
            IsNight = SunDirection(timeOfDay).y < 0.03f;
            if (skyMaterial != null) RenderSettings.skybox = skyMaterial;
            if (controlAmbient) RenderSettings.ambientMode = AmbientMode.Trilight;
            Apply();
        }

        static Gradient DefaultSunGradient()
        {
            Gradient g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.45f, 0.2f), 0f), new GradientColorKey(new Color(1f, 0.75f, 0.5f), 0.25f), new GradientColorKey(new Color(1f, 0.96f, 0.9f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        void Update()
        {
            int prevHour = (int)timeOfDay;
            // 24 ώρες παιχνιδιού σε dayLengthMinutes πραγματικά λεπτά (επί τον πολλαπλασιαστή)
            timeOfDay += Time.deltaTime * SpeedMultiplier * 24f / (dayLengthMinutes * 60f);
            if (timeOfDay >= 24f) { timeOfDay -= 24f; day++; }

            int h = (int)timeOfDay;
            if (h != prevHour && HourChanged != null) HourChanged(h);
            Apply();
        }

        public void CycleSpeed() { SpeedIndex = (SpeedIndex + 1) % speedPresets.Length; }
        public void SetTime(float hours) { timeOfDay = Mathf.Repeat(hours, 24f); Apply(); }

        public string ClockText
        {
            get
            {
                int h = (int)timeOfDay;
                int m = (int)((timeOfDay - h) * 60f);
                return string.Format("{0:00}:{1:00}", h, m);
            }
        }

        /// <summary>
        /// Κατεύθυνση προς τον ήλιο. Τύπος ηλιακού ύψους: sin(a) = sinφ·sinδ + cosφ·cosδ·cosH,
        /// με H = ωριαία γωνία (15° ανά ώρα από το μεσημέρι). Σύστημα: +X ανατολή, +Z βορράς.
        /// </summary>
        public Vector3 SunDirection(float hours)
        {
            float phi = latitude * Mathf.Deg2Rad;
            float dec = declination * Mathf.Deg2Rad;
            float H = (hours - 12f) * 15f * Mathf.Deg2Rad;
            float east = -Mathf.Cos(dec) * Mathf.Sin(H);
            float north = Mathf.Cos(phi) * Mathf.Sin(dec) - Mathf.Sin(phi) * Mathf.Cos(dec) * Mathf.Cos(H);
            float up = Mathf.Sin(phi) * Mathf.Sin(dec) + Mathf.Cos(phi) * Mathf.Cos(dec) * Mathf.Cos(H);
            Vector3 d = new Vector3(east, up, north).normalized;
            return Quaternion.Euler(0f, northOffset, 0f) * d;
        }

        void Apply()
        {
            Vector3 sunDir = SunDirection(timeOfDay);
            Vector3 moonDir = (-sunDir + new Vector3(0.2f, 0.35f, 0.15f)).normalized;
            float e = sunDir.y;   // «ύψος» του ήλιου, -1..1

            Daylight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.1f, 0.2f, e));
            float twilight = Mathf.Clamp01(1f - Mathf.Abs(e - 0.02f) / 0.2f);
            float night = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.02f, -0.18f, e));

            // ---------------- Φώτα ----------------
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.LookRotation(-sunDir, Vector3.up);
                float si = Mathf.Clamp01(e * 5f + 0.05f) * sunMaxIntensity;
                sun.intensity = si;
                sun.color = sunColor.Evaluate(Mathf.Clamp01(e * 2.5f));
                sun.enabled = si > 0.01f;
                sun.shadows = LightShadows.Soft;
            }
            if (moon != null)
            {
                moon.transform.rotation = Quaternion.LookRotation(-moonDir, Vector3.up);
                float mi = night * moonMaxIntensity * Mathf.Clamp01(moonDir.y * 4f);
                moon.intensity = mi;
                moon.enabled = mi > 0.005f && (sun == null || !sun.enabled);
                // Μόνο ένα φως με σκιές κάθε στιγμή (ο ήλιος τη μέρα, η σελήνη τη νύχτα): λιγότερο κόστος.
                moon.shadows = LightShadows.Soft;
            }
            if (sun != null) RenderSettings.sun = sun.enabled || moon == null ? sun : moon;

            // ---------------- Χρώματα ουρανού ----------------
            Color zenith = Color.Lerp(ZenithNight, ZenithDay, Daylight);
            Color horizon = Color.Lerp(HorizonNight, HorizonDay, Daylight);
            horizon = Color.Lerp(horizon, HorizonDusk, twilight * 0.8f);

            if (skyMaterial != null)
            {
                _cloudOffset += new Vector2(1f, 0.3f) * Time.deltaTime * 0.003f;
                skyMaterial.SetColor("_ZenithColor", zenith);
                skyMaterial.SetColor("_HorizonColor", horizon);
                skyMaterial.SetColor("_GroundColor", horizon * 0.45f);
                skyMaterial.SetVector("_SunDir", sunDir);
                skyMaterial.SetVector("_MoonDir", moonDir);
                skyMaterial.SetColor("_SunColor", sun != null ? sun.color : Color.white);
                skyMaterial.SetFloat("_StarIntensity", night);
                skyMaterial.SetColor("_CloudColor", Color.Lerp(new Color(0.08f, 0.09f, 0.12f), Color.Lerp(Color.white, new Color(1f, 0.7f, 0.5f), twilight * 0.6f), Mathf.Max(Daylight, 0.05f)));
                skyMaterial.SetVector("_CloudOffset", _cloudOffset);
            }
            else if (RenderSettings.skybox != null && RenderSettings.skybox.HasProperty("_Exposure"))
            {
                // Το ενσωματωμένο Skybox/Procedural: απλώς σκοτεινιάζουμε τη νύχτα.
                RenderSettings.skybox.SetFloat("_Exposure", Mathf.Lerp(0.08f, 1.2f, Daylight));
            }

            // ---------------- Περιβάλλων φωτισμός & ομίχλη ----------------
            if (controlAmbient)
            {
                RenderSettings.ambientSkyColor = zenith * Mathf.Lerp(1.4f, 1.1f, Daylight) + new Color(0.03f, 0.035f, 0.07f);
                RenderSettings.ambientEquatorColor = horizon * 0.7f + new Color(0.02f, 0.02f, 0.04f);
                RenderSettings.ambientGroundColor = new Color(0.15f, 0.13f, 0.1f) * Mathf.Lerp(0.25f, 1f, Daylight);
            }
            if (controlFog)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogDensity = Mathf.Lerp(0.006f, 0.0035f, Daylight);
                RenderSettings.fogColor = horizon * 0.9f;
            }

            // ---------------- Νύχτα; ----------------
            bool isNight = e < 0.03f;   // λίγο πριν τη δύση ανάβουν τα φώτα
            if (isNight != IsNight)
            {
                IsNight = isNight;
                if (NightChanged != null) NightChanged(isNight);
            }
        }
    }
}
