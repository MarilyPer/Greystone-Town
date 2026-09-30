// =====================================================================================
//  SkySystem.cs — Κύκλος ημέρας / νύχτας
//  ---------------------------------------------------------------------------------
//  Σε κάθε frame, με βάση την ώρα (TimeManager) και τον καιρό (WeatherSystem):
//   * περιστρέφει το φως του ήλιου και της σελήνης (DirectionalLights),
//   * αλλάζει ένταση/χρώμα του φωτός (πορτοκαλί στην ανατολή/δύση, λευκό το μεσημέρι),
//   * υπολογίζει τα χρώματα του ουρανού και τα στέλνει στον skybox shader,
//   * ρυθμίζει τον περιβάλλοντα φωτισμό (Trilight) και την ομίχλη,
//   * ορίζει την καθολική τιμή _GS_NightGlow (ανάβουν παράθυρα/φανάρια).
//
//  ΒΕΛΤΙΣΤΟΠΟΙΗΣΗ: μόνο ΕΝΑ φως ρίχνει σκιές κάθε στιγμή (ο ήλιος τη μέρα, κανένα τη
//  νύχτα). Ο περιβάλλων φωτισμός είναι «Trilight» (3 χρώματα) αντί για επαναϋπολογισμό
//  του environment lighting από τον skybox (DynamicGI.UpdateEnvironment), που είναι ακριβός.
// =====================================================================================
using UnityEngine;
using UnityEngine.Rendering;

namespace Greystone
{
    public class SkySystem : MonoBehaviour
    {
        public static SkySystem Instance { get; private set; }

        Light _sun, _moon;
        Material _sky;
        Vector2 _cloudOffset;

        // Χρώματα ουρανού σε τρεις καταστάσεις (νύχτα / λυκόφως / μέρα)
        static readonly Color ZenithNight = new Color(0.01f, 0.015f, 0.045f);
        static readonly Color HorizonNight = new Color(0.035f, 0.045f, 0.09f);
        static readonly Color ZenithTwilight = new Color(0.13f, 0.17f, 0.36f);
        static readonly Color HorizonTwilight = new Color(0.95f, 0.52f, 0.3f);
        static readonly Color ZenithDay = new Color(0.2f, 0.42f, 0.82f);
        static readonly Color HorizonDay = new Color(0.66f, 0.78f, 0.9f);
        static readonly Color Overcast = new Color(0.55f, 0.58f, 0.62f);

        public Light Sun { get { return _sun; } }

        public void Build()
        {
            Instance = this;
            _sun = CreateDirectional("Sun", new Color(1f, 0.95f, 0.85f), 1.2f, true);
            _moon = CreateDirectional("Moon", new Color(0.6f, 0.7f, 1f), 0.15f, false);

            _sky = new Material(Mats.FindShader("Greystone/Sky"));
            RenderSettings.skybox = _sky;
            RenderSettings.sun = _sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0035f;

            QualitySettings.shadowDistance = 160f;
            QualitySettings.shadowCascades = 4;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.pixelLightCount = 6;
            Update();
        }

        Light CreateDirectional(string name, Color c, float intensity, bool shadows)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            Light l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = c;
            l.intensity = intensity;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            l.shadowStrength = 0.85f;
            l.shadowBias = 0.05f;
            l.shadowNormalBias = 0.4f;
            return l;
        }

        void Update()
        {
            if (_sun == null || TimeManager.Instance == null) return;
            WeatherSystem w = WeatherSystem.Instance;
            float cloud = w != null ? w.CloudCover : 0.3f;
            float rain = w != null ? w.RainAmount : 0f;

            Vector3 sunDir = TimeManager.Instance.SunDirection;           // προς τον ήλιο
            // Η σελήνη: περίπου απέναντι από τον ήλιο, λίγο μετατοπισμένη ώστε να μη «κρύβεται» στον ορίζοντα.
            Vector3 moonDir = (-sunDir + new Vector3(0.25f, 0.35f, -0.2f)).normalized;

            float e = sunDir.y;                                              // «ύψος» ήλιου (-1..1)
            float day = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.05f, 0.25f, e));
            float twilight = Mathf.Clamp01(1f - Mathf.Abs(e - 0.02f) / 0.2f);
            float night = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.02f, -0.18f, e));

            // ---------------- Φώτα ----------------
            _sun.transform.rotation = Quaternion.LookRotation(-sunDir, Vector3.up);
            _moon.transform.rotation = Quaternion.LookRotation(-moonDir, Vector3.up);
            Color warm = new Color(1f, 0.55f, 0.28f);
            Color white = new Color(1f, 0.96f, 0.88f);
            float sunI = Mathf.Clamp01(e * 5f + 0.05f) * 1.25f * (1f - cloud * 0.55f);
            _sun.color = Color.Lerp(warm, white, Mathf.Clamp01(e * 3f));
            _sun.intensity = sunI;
            _sun.enabled = sunI > 0.01f;
            _sun.shadowStrength = Mathf.Lerp(0.85f, 0.35f, cloud);
            float moonI = night * 0.18f * Mathf.Clamp01(moonDir.y * 4f) * (1f - cloud * 0.6f);
            _moon.intensity = moonI;
            _moon.enabled = moonI > 0.005f && !_sun.enabled;
            RenderSettings.sun = _sun.enabled ? _sun : _moon;

            // ---------------- Ουρανός ----------------
            Color zenith = Color.Lerp(Color.Lerp(ZenithNight, ZenithTwilight, 1f - night), ZenithDay, day);
            Color horizon = Color.Lerp(HorizonNight, HorizonDay, day);
            horizon = Color.Lerp(horizon, HorizonTwilight, twilight * (1f - cloud * 0.7f) * 0.85f);
            // Συννεφιά: αποχρωματισμός προς γκρι
            float grey = cloud * 0.8f + rain * 0.2f;
            float lum = Mathf.Lerp(0.04f, 1f, day);
            zenith = Color.Lerp(zenith, Overcast * lum, grey * 0.8f);
            horizon = Color.Lerp(horizon, Overcast * lum * 1.05f, grey * 0.8f);

            _cloudOffset += (w != null ? w.WindDirection : new Vector2(1f, 0.3f)) * Time.deltaTime * 0.004f * (0.4f + (w != null ? w.WindStrength : 0.4f));

            _sky.SetColor("_ZenithColor", zenith);
            _sky.SetColor("_HorizonColor", horizon);
            _sky.SetColor("_GroundColor", horizon * 0.45f);
            _sky.SetVector("_SunDir", sunDir);
            _sky.SetVector("_MoonDir", moonDir);
            _sky.SetColor("_SunColor", _sun.color);
            _sky.SetFloat("_StarIntensity", night);
            _sky.SetFloat("_CloudCover", cloud);
            Color cloudCol = Color.Lerp(new Color(0.08f, 0.09f, 0.12f), Color.Lerp(white, new Color(1f, 0.7f, 0.5f), twilight * 0.6f), Mathf.Max(day, 0.05f));
            cloudCol = Color.Lerp(cloudCol, cloudCol * 0.62f, rain);
            _sky.SetColor("_CloudColor", cloudCol);
            _sky.SetVector("_CloudOffset", _cloudOffset);

            // ---------------- Περιβάλλων φωτισμός και ομίχλη ----------------
            RenderSettings.ambientSkyColor = zenith * Mathf.Lerp(1.1f, 1.4f, day) + new Color(0.03f, 0.035f, 0.06f);
            RenderSettings.ambientEquatorColor = horizon * 0.75f + new Color(0.02f, 0.02f, 0.035f);
            RenderSettings.ambientGroundColor = new Color(0.16f, 0.14f, 0.1f) * Mathf.Lerp(0.2f, 1f, day);
            RenderSettings.fogColor = horizon * 0.95f;
            RenderSettings.fogDensity = Mathf.Lerp(0.0028f, 0.009f, rain) + cloud * 0.001f;

            // ---------------- Νυχτερινή λάμψη (όλα τα παράθυρα/φανάρια με μία εντολή) ----------------
            Shader.SetGlobalFloat(Mats.GlobalNightGlow, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, -0.02f, e)));
        }
    }
}
