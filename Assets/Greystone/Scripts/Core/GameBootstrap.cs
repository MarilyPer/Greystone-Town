// =====================================================================================
//  GameBootstrap.cs — Σημείο εκκίνησης της εφαρμογής
//  ---------------------------------------------------------------------------------
//  Χτίζει ΟΛΟ τον κόσμο με κώδικα, σε διαδοχικά βήματα (coroutine) με οθόνη φόρτωσης:
//    1. διαχειριστές (χρόνος, ενημερώσεις, ήχος, φώτα, HUD),
//    2. σύνθεση ήχων,  3. έδαφος,  4. ουρανός/καιρός,  5. ποτάμι,
//    6. χωριό (κτίρια, αντικείμενα, κάτοικοι, ζώα),  7. βλάστηση,  8. παίκτης.
//
//  Αυτόματη εκκίνηση: με [RuntimeInitializeOnLoadMethod] ο bootstrap δημιουργείται μόνος
//  του όταν πατηθεί Play σε ΟΠΟΙΑΔΗΠΟΤΕ σκηνή (ακόμη και άδεια), αν δεν υπάρχει ήδη.
//  Έτσι το project δεν εξαρτάται από χειροποίητα αρχεία σκηνής.
//
//  Επίσης χειρίζεται τα «καθολικά» πλήκτρα (χάρτης, βοήθεια, απόδοση, καιρός, χρόνος).
// =====================================================================================
using System.Collections;
using UnityEngine;

namespace Greystone
{
    public class GameBootstrap : MonoBehaviour
    {
        public static bool LayerCullingEnabled = true;
        public static GameBootstrap Instance { get; private set; }

        Hud _hud;
        PlayerController _player;
        bool _ready;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
#if UNITY_2023_1_OR_NEWER
            if (FindFirstObjectByType<GameBootstrap>() != null) return;
#else
            if (FindObjectOfType<GameBootstrap>() != null) return;
#endif
            new GameObject("Greystone").AddComponent<GameBootstrap>();
        }

        void Awake()
        {
            Instance = this;
            ResetStatics();
            // Αφαίρεση προεπιλεγμένης κάμερας/φωτός της σκηνής (π.χ. SampleScene) — ο κόσμος τα δημιουργεί μόνος του.
#if UNITY_2023_1_OR_NEWER
            foreach (Camera c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) Destroy(c.gameObject);
            foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None)) Destroy(l.gameObject);
            foreach (AudioListener al in FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) Destroy(al);
#else
            foreach (Camera c in FindObjectsOfType<Camera>()) Destroy(c.gameObject);
            foreach (Light l in FindObjectsOfType<Light>()) Destroy(l.gameObject);
            foreach (AudioListener al in FindObjectsOfType<AudioListener>()) Destroy(al);
#endif
            Application.targetFrameRate = 120;
        }

        /// <summary>
        /// Μηδενισμός στατικής κατάστασης. Αν στο Editor είναι απενεργοποιημένο το «Domain Reload»
        /// (Enter Play Mode Options), οι static μεταβλητές διατηρούνται ανάμεσα στα Play — εδώ τις καθαρίζουμε.
        /// </summary>
        static void ResetStatics()
        {
            WorldLayout.Footprints.Clear();
            WorldLayout.SetHeightCache(null);
            UpdateScheduler.Clear();
            Vegetation.OptimizationsEnabled = true;
            UpdateScheduler.ThrottlingEnabled = true;
            LayerCullingEnabled = true;
        }

        void Start()
        {
            StartCoroutine(BuildWorld());
        }

        IEnumerator BuildWorld()
        {
            // Προσωρινή κάμερα για την οθόνη φόρτωσης
            Camera loadingCam = new GameObject("LoadingCamera").AddComponent<Camera>();
            loadingCam.clearFlags = CameraClearFlags.SolidColor;
            loadingCam.backgroundColor = Color.black;

            // ---------------- 1. Διαχειριστές ----------------
            gameObject.AddComponent<TimeManager>();
            gameObject.AddComponent<UpdateScheduler>();
            gameObject.AddComponent<LightLod>();
            gameObject.AddComponent<SoundBank>();
            _hud = gameObject.AddComponent<Hud>();
            _hud.Loading = true;
            yield return null;

            // ---------------- 2. Ήχοι ----------------
            yield return SoundBank.Instance.Generate((p, name) => _hud.SetLoading(p * 0.3f, "Σύνθεση ήχων: " + name + "…"));

            // ---------------- 3. Έδαφος ----------------
            _hud.SetLoading(0.3f, "Διαμόρφωση εδάφους και ποταμού…");
            yield return null;
            TerrainBuilder terrain = Child<TerrainBuilder>("Terrain");
            yield return terrain.Build();

            // ---------------- 4. Ουρανός & καιρός ----------------
            _hud.SetLoading(0.45f, "Ουρανός, ήλιος και καιρός…");
            yield return null;
            Child<SkySystem>("Sky").Build();
            Child<WeatherSystem>("Weather").Build();
            Child<Water>("River").Build();

            // ---------------- 5. Χωριό ----------------
            _hud.SetLoading(0.55f, "Χτίζοντας το χωριό…");
            yield return null;
            VillageBuilder village = Child<VillageBuilder>("Village");
            yield return village.Build(_hud);

            // ---------------- 6. Βλάστηση ----------------
            _hud.SetLoading(0.75f, "Φύτευση δάσους, οπωρώνα και χωραφιών…");
            yield return null;
            yield return Child<Vegetation>("Vegetation").Build();

            // ---------------- 7. Παίκτης ----------------
            _hud.SetLoading(0.95f, "Σχεδόν έτοιμο…");
            yield return null;
            _hud.BuildMapTexture();
            Destroy(loadingCam.gameObject);
            GameObject pgo = new GameObject("Player");
            pgo.AddComponent<CharacterController>();
            _player = pgo.AddComponent<PlayerController>();
            _player.Init(WorldLayout.OnGround(WorldLayout.Spawn.x, WorldLayout.Spawn.z, 0.1f), 0f);

            _hud.Loading = false;
            _ready = true;
            _hud.Notify("Καλώς ήρθατε στο Greystone! Πατήστε H για τον χειρισμό και διαβάστε τις πινακίδες για να γνωρίσετε το χωριό.", 8f);
        }

        T Child<T>(string name) where T : Component
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.AddComponent<T>();
        }

        void Update()
        {
            if (!_ready) return;
            if (GInput.Down(GKey.Pause)) _hud.Escape();
            if (GInput.Down(GKey.Map)) _hud.ToggleMap();
            if (GInput.Down(GKey.Help)) _hud.ToggleHelp();
            if (GInput.Down(GKey.Perf)) _hud.TogglePerf();
            if (_hud.IsModal) return;

            if (GInput.Down(GKey.TimeSpeed))
            {
                TimeManager.Instance.CycleSpeed();
                _hud.Notify("Ταχύτητα χρόνου: 1 δευτερόλεπτο = " + (TimeManager.Instance.TimeScale / 60f) + " λεπτά παιχνιδιού");
            }
            if (GInput.Down(GKey.Weather))
            {
                WeatherSystem.Instance.CycleWeather();
                _hud.Notify("Ο καιρός αλλάζει: " + WeatherSystem.Instance.StateName);
            }
            if (GInput.Down(GKey.ToggleLod))
            {
                Vegetation.OptimizationsEnabled = !Vegetation.OptimizationsEnabled;
                _hud.Notify("Frustum culling & LOD βλάστησης: " + (Vegetation.OptimizationsEnabled ? "ενεργά" : "ανενεργά (δείτε τη διαφορά στο F3)"));
            }
            if (GInput.Down(GKey.ToggleCullDist))
            {
                LayerCullingEnabled = !LayerCullingEnabled;
                _player.ApplyLayerCullDistances(LayerCullingEnabled);
                _hud.Notify("Απόσταση αποκοπής μικρών αντικειμένων: " + (LayerCullingEnabled ? "ενεργή" : "ανενεργή"));
            }
            if (GInput.Down(GKey.ToggleThrottle))
            {
                UpdateScheduler.ThrottlingEnabled = !UpdateScheduler.ThrottlingEnabled;
                _hud.Notify("Αραίωση ενημερώσεων AI: " + (UpdateScheduler.ThrottlingEnabled ? "ενεργή" : "ανενεργή"));
            }
        }
    }
}
