// =====================================================================================
//  VillageSceneBuilder.cs — Αυτόματη κατασκευή της σκηνής του χωριού (Editor)
//  ---------------------------------------------------------------------------------
//  Μενού: Greystone → 2. Δημιουργία σκηνής χωριού
//
//  Χτίζει και ΑΠΟΘΗΚΕΥΕΙ μια κανονική σκηνή (Assets/Greystone/Scenes/GreystoneVillage.unity)
//  χρησιμοποιώντας τα assets του project. Μετά μπορείτε να μετακινήσετε/περιστρέψετε/
//  προσθέσετε ελεύθερα αντικείμενα στον Editor — η σκηνή είναι δική σας.
//
//  Βήματα:
//    1. Έδαφος (Terrain): πεδιάδα για το χωριό, λόφοι γύρω, υφές από το TerrainDemoScene_URP
//       (γρασίδι, χώμα στους δρόμους, βότσαλα στην πλατεία, βράχος στις πλαγιές) και δάσος
//       με τα δέντρα SpeedTree του ίδιου πακέτου.
//    2. Φως & ουρανός: ήλιος, σελήνη, υλικό ουρανού (Greystone/Sky), DayNightCycle.
//    3. Χωριό: πλατεία με τον κέλτικο σταυρό και πυρσούς, δύο δακτύλιοι κτιρίων (Viking Village
//       + 3DForge), παλισάδα με πύλη και σκοπιές, αγορά, διακοσμητικά.
//    4. Σημεία ενδιαφέροντος (VillagePoint) για τους κατοίκους.
//    5. Κάτοικοι (SopaDopa χαρακτήρες) με NavMeshAgent, ενδυμασία ανά ρόλο, βάδισμα, AI.
//    6. Παίκτης και HUD.
//    7. NavMesh (bake) — αποθηκεύεται ως asset.
//
//  ΒΕΛΤΙΣΤΟΠΟΙΗΣΕΙΣ που ρυθμίζονται εδώ:
//    * Terrain: GPU instanced σχεδίαση, δέντρα με billboards από απόσταση (LOD).
//    * Κτίρια Viking: LOD Group με τα έτοιμα «_low» μοντέλα του πακέτου.
//    * Static flags (Batching/Occluder/Occludee) σε όλη τη στατική γεωμετρία → static batching
//      και δυνατότητα Occlusion Culling (Window → Rendering → Occlusion Culling → Bake).
// =====================================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Greystone.EditorTools
{
    public static class VillageSceneBuilder
    {
        const string RootFolder = "Assets/Greystone";
        const string GenFolder = "Assets/Greystone/Generated";
        const string ScenePath = "Assets/Greystone/Scenes/GreystoneVillage.unity";

        const float TerrainHeight = 60f;
        const float VillageBase = 8f;          // υψόμετρο της πεδιάδας του χωριού (m)
        const float PlazaRadius = 11f;
        static float GateHalfAngle = 9f;       // άνοιγμα πύλης (μοίρες) στον νότο — προσαρμόζεται στο πλάτος της πύλης

        // Οι ακτίνες υπολογίζονται από τις ΠΡΑΓΜΑΤΙΚΕΣ διαστάσεις των μοντέλων (MeasureLayout),
        // ώστε τα κτίρια να μην επικαλύπτονται, όποιο κι αν είναι το μέγεθός τους.
        static float Ring1 = 27f;              // πρώτος δακτύλιος σπιτιών
        static float Ring2 = 50f;              // δεύτερος δακτύλιος
        static float WallRadius = 68f;         // παλισάδα
        static float Road1 = 18f, Road2 = 40f; // κυκλικοί δρόμοι ανάμεσα στους δακτυλίους
        static float TerrainSize = 320f;

        const string VIK = "Assets/Viking Village/Prefabs/";
        const string FORGE = "Assets/3DForge/FantasyExteriors/Village & Towns/Prefabs/";
        const string COBRA = "Assets/CobraGamesAssets/Stylized_Dungeon_Props_Pack/Prefabs/Props/";
        const string TDS = "Assets/TerrainDemoScene_URP/";

        static System.Random _rng;
        static Terrain _terrain;
        static Transform _village, _points;

        // ==================================================================== Μενού
        [MenuItem("Greystone/2. Δημιουργία σκηνής χωριού")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!EditorUtility.DisplayDialog("Greystone",
                "Θα δημιουργηθεί (ή θα αντικατασταθεί) η σκηνή\n" + ScenePath + "\n\nΣυνέχεια;", "Ναι", "Άκυρο")) return;

            _rng = new System.Random(1400);
            EnsureFolder(RootFolder, "Scenes");
            EnsureFolder(RootFolder, "Generated");

            try
            {
                Progress("Μετατροπή υλικών σε URP…", 0.02f);
                MaterialToURP.ConvertAll();

                Progress("Νέα σκηνή…", 0.05f);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                _village = new GameObject("Village").transform;
                _points = new GameObject("VillagePoints").transform;

                Progress("Μέτρηση κτιρίων…", 0.08f);
                MeasureLayout();
                Progress("Έδαφος…", 0.1f);
                BuildTerrain();
                Progress("Φως & ουρανός…", 0.3f);
                BuildLighting();
                Progress("Πλατεία…", 0.35f);
                BuildPlaza();
                Progress("Κτίρια…", 0.45f);
                List<Transform> homes = BuildBuildings();
                Progress("Παλισάδα & πύλη…", 0.6f);
                BuildWalls();
                Progress("Διακόσμηση…", 0.65f);
                BuildProps(homes);
                Progress("Δάσος…", 0.7f);
                PlantForest();
                Progress("Κάτοικοι…", 0.78f);
                BuildVillagers();
                Progress("Παίκτης…", 0.85f);
                BuildPlayer();
                Progress("NavMesh (bake)…", 0.9f);
                BakeNavMesh();

                Progress("Αποθήκευση…", 0.98f);
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
                AddSceneToBuild();
                Debug.Log("[Greystone] Η σκηνή του χωριού δημιουργήθηκε: " + ScenePath + " — πατήστε Play!");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        static void Progress(string msg, float p) { EditorUtility.DisplayProgressBar("Greystone — χτίσιμο χωριού", msg, p); }

        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }

        static void AddSceneToBuild()
        {
            List<EditorBuildSettingsScene> list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            list.RemoveAll(s => s.path == ScenePath);
            list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        // ==================================================================== Βοηθητικά
        static float Rand() { return (float)_rng.NextDouble(); }
        static float Rand(float a, float b) { return a + (b - a) * (float)_rng.NextDouble(); }

        /// <summary>Φόρτωση prefab από διαδρομή· αν δεν βρεθεί, αναζήτηση με το όνομα του αρχείου.</summary>
        static GameObject Prefab(string path)
        {
            GameObject g = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (g != null) return g;
            string name = Path.GetFileNameWithoutExtension(path);
            foreach (string guid in AssetDatabase.FindAssets(name + " t:Prefab"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(p) == name) return AssetDatabase.LoadAssetAtPath<GameObject>(p);
            }
            Debug.LogWarning("[Greystone] Δεν βρέθηκε το prefab: " + path);
            return null;
        }

        /// <summary>Αναζήτηση prefab που το όνομά του περιέχει ένα κομμάτι (π.χ. «eltic» για τον σταυρό).</summary>
        static GameObject PrefabContaining(string part)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(p).Contains(part)) return AssetDatabase.LoadAssetAtPath<GameObject>(p);
            }
            return null;
        }

        static float Ground(float x, float z)
        {
            return _terrain != null ? _terrain.SampleHeight(new Vector3(x, 0f, z)) + _terrain.transform.position.y : VillageBase;
        }

        static GameObject Place(GameObject prefab, Vector3 pos, float yaw, Transform parent, float scale = 1f)
        {
            if (prefab == null) return null;
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * go.transform.rotation;
            if (!Mathf.Approximately(scale, 1f)) go.transform.localScale *= scale;
            return go;
        }

        static Bounds WorldBounds(GameObject go)
        {
            Renderer[] rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static void MarkStatic(GameObject go)
        {
            StaticEditorFlags f = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            {
                // Τα αντικείμενα με σωματίδια/φώτα (πυρσοί) δεν γίνονται static batched
                if (t.GetComponent<ParticleSystem>() != null || t.GetComponent<Light>() != null) continue;
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, f);
            }
        }

        static VillagePoint Point(PointType type, Vector3 pos, Vector3 forward, int capacity = 2)
        {
            GameObject g = new GameObject("Point_" + type);
            g.transform.SetParent(_points, false);
            g.transform.position = pos;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.001f) g.transform.rotation = Quaternion.LookRotation(forward);
            VillagePoint vp = g.AddComponent<VillagePoint>();
            vp.type = type;
            vp.capacity = capacity;
            return vp;
        }

        /// <summary>Γωνία (μοίρες) από τον +Z (βορράς) προς τον +X (ανατολή).</summary>
        static Vector3 OnCircle(float radius, float angleDeg)
        {
            float a = angleDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a) * radius, 0f, Mathf.Cos(a) * radius);
        }

        static Vector3 Grounded(Vector3 p) { p.y = Ground(p.x, p.z); return p; }

        // ==================================================================== 1. Έδαφος
        /// <summary>Αναλυτικό ύψος: επίπεδη πεδιάδα χωριού, απαλοί λόφοι προς τα έξω.</summary>
        static float HeightAt(float x, float z)
        {
            float r = Mathf.Sqrt(x * x + z * z);
            float hills = Mathf.PerlinNoise(x * 0.012f + 31.7f, z * 0.012f + 12.3f);
            float detail = Mathf.PerlinNoise(x * 0.06f + 3.1f, z * 0.06f + 9.4f) - 0.5f;
            float ring = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(WallRadius + 12f, TerrainSize * 0.5f - 15f, r));
            float h = VillageBase + ring * (8f + hills * 26f) + detail * Mathf.Lerp(0.5f, 2.5f, ring);
            // Ο δρόμος προς τον νότο μένει ομαλός (κοιλάδα)
            float roadDist = Mathf.Abs(x);
            if (z < 0f) h = Mathf.Lerp(VillageBase + detail * 0.4f, h, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(4f, 22f, roadDist)) * 0.85f + 0.15f * ring);
            return h;
        }

        static void BuildTerrain()
        {
            TerrainData td = new TerrainData();
            td.heightmapResolution = 257;
            td.alphamapResolution = 256;
            td.baseMapResolution = 512;
            td.size = new Vector3(TerrainSize, TerrainHeight, TerrainSize);

            int res = td.heightmapResolution;
            float[,] h = new float[res, res];
            for (int j = 0; j < res; j++)
                for (int i = 0; i < res; i++)
                {
                    float x = -TerrainSize * 0.5f + i / (float)(res - 1) * TerrainSize;
                    float z = -TerrainSize * 0.5f + j / (float)(res - 1) * TerrainSize;
                    h[j, i] = HeightAt(x, z) / TerrainHeight;   // [y, x] στη σύμβαση του Unity
                }
            td.SetHeights(0, 0, h);

            // ---------------- Υφές (Terrain Layers) ----------------
            string L = TDS + "Terrain/Layers/";
            TerrainLayer grass = AssetDatabase.LoadAssetAtPath<TerrainLayer>(L + "Grass_A.terrainlayer");
            TerrainLayer soil = AssetDatabase.LoadAssetAtPath<TerrainLayer>(L + "Grass_Soil_A.terrainlayer");
            TerrainLayer pebbles = AssetDatabase.LoadAssetAtPath<TerrainLayer>(L + "Pebbles_B.terrainlayer");
            TerrainLayer cliff = AssetDatabase.LoadAssetAtPath<TerrainLayer>(L + "Cliff_Mossy_E.terrainlayer");
            TerrainLayer moss = AssetDatabase.LoadAssetAtPath<TerrainLayer>(L + "Grass_Moss_A.terrainlayer");
            List<TerrainLayer> layers = new List<TerrainLayer>();
            foreach (TerrainLayer tl in new[] { grass, soil, pebbles, cliff, moss }) if (tl != null) layers.Add(tl);
            td.terrainLayers = layers.ToArray();
            if (layers.Count == 5) PaintTerrain(td);

            // ---------------- Δέντρα (SpeedTree) ----------------
            List<TreePrototype> protos = new List<TreePrototype>();
            foreach (string t in new[] { "Pines/Pine_A/Pine_A", "Pines/Pine_B/Pine_B", "Pines/Pine_C/Pine_C", "Pines/Pine_D/Pine_D", "Conifer/Conifer" })
            {
                GameObject p = AssetDatabase.LoadAssetAtPath<GameObject>(TDS + "Prefabs/Trees/" + t + ".prefab");
                if (p != null) protos.Add(new TreePrototype { prefab = p });
            }
            td.treePrototypes = protos.ToArray();

            AssetDatabase.CreateAsset(td, GenFolder + "/VillageTerrain.asset");

            GameObject tgo = Terrain.CreateTerrainGameObject(td);
            tgo.name = "Terrain";
            tgo.transform.position = new Vector3(-TerrainSize * 0.5f, 0f, -TerrainSize * 0.5f);
            _terrain = tgo.GetComponent<Terrain>();
            // ΒΕΛΤΙΣΤΟΠΟΙΗΣΗ: instanced σχεδίαση εδάφους, billboards για τα μακρινά δέντρα
            _terrain.drawInstanced = true;
            _terrain.treeBillboardDistance = 110f;
            _terrain.treeDistance = 600f;
            _terrain.basemapDistance = 220f;
            _terrain.heightmapPixelError = 6f;
        }

        /// <summary>Βάφει το έδαφος: γρασίδι, χώμα στους δρόμους, βότσαλα στην πλατεία, βράχο στις πλαγιές.</summary>
        static void PaintTerrain(TerrainData td)
        {
            int res = td.alphamapResolution;
            float[,,] a = new float[res, res, 5];
            for (int j = 0; j < res; j++)
                for (int i = 0; i < res; i++)
                {
                    float u = i / (float)(res - 1), v = j / (float)(res - 1);
                    float x = -TerrainSize * 0.5f + u * TerrainSize, z = -TerrainSize * 0.5f + v * TerrainSize;
                    float r = Mathf.Sqrt(x * x + z * z);
                    float steep = td.GetSteepness(u, v);
                    float n = Mathf.PerlinNoise(x * 0.08f, z * 0.08f);

                    // Δρόμοι: κυκλικοί δρόμοι στους δακτυλίους + κεντρικός δρόμος προς τον νότο (πύλη)
                    float roadRing1 = Mathf.Abs(r - Road1);
                    float roadRing2 = Mathf.Abs(r - Road2);
                    float mainRoad = z < PlazaRadius ? Mathf.Abs(x) : 999f;
                    float road = Mathf.Min(roadRing1, Mathf.Min(roadRing2, mainRoad));
                    float roadW = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.6f + n, 3.2f + n, road));
                    float plazaW = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(PlazaRadius - 1f, PlazaRadius + 1.5f, r + (n - 0.5f) * 2f));
                    float cliffW = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(22f, 34f, steep));
                    float mossW = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(WallRadius + 10f, WallRadius + 40f, r)) * (0.4f + n * 0.6f);

                    float wGrass = Mathf.Max(0f, 1f - roadW - plazaW - cliffW - mossW);
                    float[] w = { wGrass, roadW * (1f - plazaW), plazaW, cliffW, mossW * (1f - cliffW) };
                    float sum = 0f;
                    foreach (float f in w) sum += f;
                    for (int k = 0; k < 5; k++) a[j, i, k] = w[k] / Mathf.Max(sum, 0.0001f);
                }
            td.SetAlphamaps(0, 0, a);
        }

        // ==================================================================== 2. Φως & ουρανός
        static void BuildLighting()
        {
            GameObject dnGo = new GameObject("DayNightCycle");
            DayNightCycle dn = dnGo.AddComponent<DayNightCycle>();

            Light sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.intensity = 1.5f;
            sun.transform.SetParent(dnGo.transform, false);
            Light moon = new GameObject("Moon").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.6f, 0.7f, 1f);
            moon.shadows = LightShadows.Soft;
            moon.transform.SetParent(dnGo.transform, false);

            Shader skyShader = Shader.Find("Greystone/Sky");
            Material sky = null;
            if (skyShader != null)
            {
                sky = new Material(skyShader);
                AssetDatabase.CreateAsset(sky, GenFolder + "/GreystoneSky.mat");
                RenderSettings.skybox = sky;
            }
            dn.sun = sun;
            dn.moon = moon;
            dn.skyMaterial = sky;
            dn.timeOfDay = 9f;
            RenderSettings.sun = sun;
        }

        // ==================================================================== 3. Πλατεία
        static void BuildPlaza()
        {
            Transform plaza = new GameObject("Plaza").transform;
            plaza.SetParent(_village, false);
            Vector3 c = Grounded(Vector3.zero);

            GameObject cross = PrefabContaining("eltic");
            GameObject crossGo = Place(cross, c, 180f, plaza);
            if (crossGo != null)
            {
                // Κλιμάκωση ώστε ο σταυρός να έχει ύψος ~4 m (ανεξάρτητα από τις μονάδες του μοντέλου)
                Bounds b = WorldBounds(crossGo);
                if (b.size.y > 0.01f) crossGo.transform.localScale *= 4f / b.size.y;
                if (crossGo.GetComponentInChildren<Collider>() == null) crossGo.AddComponent<BoxCollider>();
                MarkStatic(crossGo);
            }
            Point(PointType.Temple, c + new Vector3(0f, 0f, -2.5f), Vector3.forward, 4);

            // Πυρσοί γύρω από την πλατεία (ανάβουν τη νύχτα)
            GameObject torch = Prefab(VIK + "Props/pf_torch_stick_01.prefab");
            for (int i = 0; i < 6; i++)
            {
                float ang = 30f + i * 60f;
                GameObject t = Place(torch, Grounded(OnCircle(PlazaRadius - 1f, ang)), ang + 180f, plaza);
                if (t != null) t.AddComponent<NightLight>();
            }

            // Πλατεία: σημεία συνάθροισης
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = Grounded(OnCircle(6f, 45f + i * 90f));
                Point(PointType.Plaza, p, -p, 4);
            }

            // Αγορά (ανατολική πλευρά της πλατείας): σκαμπό, κιβώτια, βαρέλια, πιθάρια
            string[] goods = { "Market_Stool_A", "Crate_Large", "Barrel", "Pot_Large", "Market_Stool_B", "Crate_Small", "Pot_Small" };
            for (int s = 0; s < 3; s++)
            {
                float ang = 70f + s * 20f;
                Vector3 stall = Grounded(OnCircle(PlazaRadius - 3.5f, ang));
                for (int k = 0; k < 3; k++)
                {
                    GameObject g = Prefab(COBRA + goods[(s * 3 + k) % goods.Length] + ".prefab");
                    Vector3 offset = Quaternion.Euler(0f, ang, 0f) * new Vector3((k - 1) * 0.9f, 0f, 0.4f);
                    GameObject go = Place(g, Grounded(stall + offset), ang + Rand(-20f, 20f), plaza);
                    if (go != null) MarkStatic(go);
                }
                Point(PointType.Market, Grounded(OnCircle(PlazaRadius - 5.2f, ang)), stall - OnCircle(PlazaRadius - 5.2f, ang), 2);
            }
        }

        // ==================================================================== 4. Κτίρια
        class Def
        {
            public string path; public PointType work; public bool isHome; public float yawOffset;
            public Def(string p, PointType w = PointType.Home, bool home = true, float yaw = 0f) { path = p; work = w; isHome = home; yawOffset = yaw; }
        }

        // Πρώτος δακτύλιος: τα «δημόσια» κτίρια και μερικά σπίτια
        static readonly Def[] Ring1Defs =
            {
                new Def(VIK + "Buildings/pf_build_bighouse_01.prefab"),                                         // μέγαρο του γέροντα
                new Def("Assets/3DForge/Blueprints/PremiumBlueprints/PB_VIK_VEK/Sword&ShieldInn/BLUEPRINTS/SwordShieldInn.prefab", PointType.Tavern, false),
                new Def(VIK + "Buildings/pf_build_small_house_01.prefab"),
                new Def(VIK + "Buildings/pf_build_blacksmith_01.prefab", PointType.Smithy, false),
                new Def(VIK + "Buildings/pf_build_small_house_tall_roof_01.prefab"),
                new Def(FORGE + "Buildings/ForestVillage/Smithy.prefab", PointType.Smithy, false),
                new Def(VIK + "Buildings/pf_build_small_house_straw_roof_01.prefab"),
                new Def(VIK + "Buildings/pf_build_storage_01.prefab", PointType.Market, false),
            };
        // Δεύτερος δακτύλιος: κατοικίες, αποθήκες, στάβλος, στρατώνας
        static readonly Def[] Ring2Defs =
            {
                new Def(VIK + "Buildings/pf_build_small_house_01.prefab"),
                new Def(VIK + "Buildings/pf_build_bighouse_02.prefab"),
                new Def(FORGE + "Buildings/Farm/Stables/Stable_2horse.prefab", PointType.Stable, false),
                new Def(VIK + "Buildings/pf_build_small_house_straw_roof_01.prefab"),
                new Def(VIK + "Buildings/pf_build_barracks_single_01.prefab", PointType.Watchtower, true),
                new Def(FORGE + "Base/Templates/BasicHouse.prefab"),
                new Def(VIK + "Buildings/pf_build_small_house_tall_roof_01.prefab"),
                new Def(VIK + "Buildings/pf_build_big_storage_01.prefab", PointType.Stable, false),
                new Def(FORGE + "Buildings/ForestVillage/ForestVillage02.prefab"),
                new Def(VIK + "Buildings/pf_build_small_house_01.prefab"),
            };

        /// <summary>Διαστάσεις (στο επίπεδο XZ) ενός prefab: x = μεγαλύτερη πλευρά, y = μικρότερη.</summary>
        static Vector2 Measure(string path)
        {
            GameObject pf = Prefab(path);
            if (pf == null) return Vector2.zero;
            GameObject tmp = (GameObject)PrefabUtility.InstantiatePrefab(pf);
            Bounds b = WorldBounds(tmp);
            Object.DestroyImmediate(tmp);
            return new Vector2(Mathf.Max(b.size.x, b.size.z), Mathf.Min(b.size.x, b.size.z));
        }

        /// <summary>
        /// Υπολογίζει ακτίνες ώστε: (α) σε κάθε δακτύλιο να χωρούν τα κτίρια με κενά ανάμεσα,
        /// (β) οι δακτύλιοι να μην επικαλύπτονται μεταξύ τους ή με την πλατεία, (γ) το τείχος
        /// και το έδαφος να περικλείουν όλο το χωριό.
        /// </summary>
        static void MeasureLayout()
        {
            float sum1 = 0f, max1 = 0f, sum2 = 0f, max2 = 0f;
            foreach (Def d in Ring1Defs) { Vector2 m = Measure(d.path); sum1 += m.x; max1 = Mathf.Max(max1, m.x); }
            foreach (Def d in Ring2Defs) { Vector2 m = Measure(d.path); sum2 += m.x; max2 = Mathf.Max(max2, m.x); }
            const float usable = Mathf.PI * 2f * 0.85f;   // αφήνουμε ~15% για τον δρόμο και τα κενά
            Ring1 = Mathf.Max(27f, sum1 * 1.15f / usable, PlazaRadius + 7f + max1 * 0.5f);
            Ring2 = Mathf.Max(50f, sum2 * 1.15f / usable, Ring1 + max1 * 0.5f + max2 * 0.5f + 9f);
            WallRadius = Ring2 + max2 * 0.5f + 14f;
            Road1 = (PlazaRadius + Ring1 - max1 * 0.5f) * 0.5f;
            Road2 = (Ring1 + max1 * 0.5f + Ring2 - max2 * 0.5f) * 0.5f;
            TerrainSize = Mathf.Max(320f, Mathf.Ceil((WallRadius + 90f) * 2f / 32f) * 32f);
            Debug.Log(string.Format("[Greystone] Διάταξη: δακτύλιος1 {0:0} m, δακτύλιος2 {1:0} m, τείχος {2:0} m, έδαφος {3:0} m", Ring1, Ring2, WallRadius, TerrainSize));
        }

        static List<Transform> BuildBuildings()
        {
            Transform root = new GameObject("Buildings").transform;
            root.SetParent(_village, false);
            List<Transform> homes = new List<Transform>();
            PlaceRing(Ring1Defs, Ring1, root, homes);
            PlaceRing(Ring2Defs, Ring2, root, homes);
            return homes;
        }

        /// <summary>
        /// Τοποθετεί κτίρια γύρω από την πλατεία σε κύκλο. Η απόσταση ανάμεσά τους υπολογίζεται
        /// από τις πραγματικές διαστάσεις (bounds) κάθε μοντέλου, ώστε να μην επικαλύπτονται,
        /// αφήνοντας ελεύθερο το άνοιγμα του κεντρικού δρόμου στον νότο.
        /// </summary>
        static void PlaceRing(Def[] defs, float radius, Transform root, List<Transform> homes)
        {
            // Πρώτα δημιουργούμε τα κτίρια για να μάθουμε το πλάτος τους
            List<GameObject> gos = new List<GameObject>();
            List<float> widths = new List<float>();
            foreach (Def d in defs)
            {
                GameObject go = InstantiateWithLod(d.path, root);
                gos.Add(go);
                if (go == null) { widths.Add(0f); continue; }
                Bounds b = WorldBounds(go);
                widths.Add(Mathf.Max(b.size.x, b.size.z));
            }
            // Διαθέσιμο τόξο (χωρίς το άνοιγμα του δρόμου)
            float roadGap = 2f * (12f / radius) * Mathf.Rad2Deg;   // ~12 m ελεύθερα δεξιά-αριστερά του δρόμου
            float arc = 360f - roadGap;
            float total = 0f;
            foreach (float w in widths) total += w;
            float spare = Mathf.Max(0f, arc * Mathf.Deg2Rad * radius - total) / Mathf.Max(1, defs.Length);   // ίσα κενά
            float angle = 180f + roadGap * 0.5f;   // ξεκινάμε δίπλα στον δρόμο (νότος) και πάμε δεξιόστροφα

            for (int i = 0; i < defs.Length; i++)
            {
                GameObject go = gos[i];
                float w = widths[i];
                float half = (w * 0.5f + spare * 0.5f) / radius * Mathf.Rad2Deg;
                angle += half;
                if (go != null)
                {
                    Vector3 pos = OnCircle(radius, angle);
                    // Κοιτάζει προς το κέντρο του χωριού (+ προαιρετική διόρθωση ανά μοντέλο)
                    float yaw = Mathf.Atan2(-pos.x, -pos.z) * Mathf.Rad2Deg + defs[i].yawOffset;
                    pos = Grounded(pos);
                    go.transform.position = pos;
                    go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                    MarkStatic(go);

                    // Σημείο «πόρτας»: προς την πλευρά της πλατείας, έξω από τον όγκο του κτιρίου
                    Bounds b = WorldBounds(go);
                    float depth = Mathf.Min(b.extents.x, b.extents.z);
                    Vector3 toCenter = (-pos).normalized;
                    toCenter.y = 0f;
                    Vector3 door = Grounded(pos + toCenter * (depth + 1.5f));
                    if (defs[i].isHome) homes.Add(Point(PointType.Home, door, toCenter, 2).transform);
                    if (defs[i].work != PointType.Home) Point(defs[i].work, Grounded(pos + toCenter * (depth + 2.5f)), -toCenter, 3);
                }
                angle += half;
            }
        }

        /// <summary>
        /// Δημιουργεί κτίριο. Αν υπάρχει η «_low» εκδοχή του (Viking Village), φτιάχνει LOD Group:
        /// κοντά το λεπτομερές μοντέλο, μακριά το απλό, πολύ μακριά τίποτα.
        /// </summary>
        static GameObject InstantiateWithLod(string path, Transform parent)
        {
            GameObject high = Prefab(path);
            if (high == null) return null;
            GameObject lowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace(".prefab", "_low.prefab"));

            GameObject holder = new GameObject(high.name);
            holder.transform.SetParent(parent, false);
            GameObject hi = (GameObject)PrefabUtility.InstantiatePrefab(high);
            hi.transform.SetParent(holder.transform, false);
            if (lowPrefab == null) return holder;

            GameObject lo = (GameObject)PrefabUtility.InstantiatePrefab(lowPrefab);
            lo.transform.SetParent(holder.transform, false);
            // Το απλό μοντέλο δεν χρειάζεται colliders (τους έχει ήδη το λεπτομερές)
            foreach (Collider col in lo.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col);

            LODGroup lg = holder.AddComponent<LODGroup>();
            lg.SetLODs(new[]
            {
                new LOD(0.18f, hi.GetComponentsInChildren<Renderer>()),
                new LOD(0.015f, lo.GetComponentsInChildren<Renderer>()),
            });
            lg.RecalculateBounds();
            return holder;
        }

        // ==================================================================== 5. Παλισάδα & πύλη
        static void BuildWalls()
        {
            Transform root = new GameObject("Palisade").transform;
            root.SetParent(_village, false);

            // Πύλη στον νότο
            GameObject gatePrefab = Prefab(VIK + "Buildings/pf_build_gate_01.prefab");
            if (gatePrefab != null)
            {
                // Ο μακρύς άξονας της πύλης πρέπει να είναι κάθετος στον δρόμο (δηλ. κατά τον άξονα x στον νότο)
                GameObject probe = (GameObject)PrefabUtility.InstantiatePrefab(gatePrefab);
                Bounds gb = WorldBounds(probe);
                Object.DestroyImmediate(probe);
                float gateYaw = gb.size.x >= gb.size.z ? 0f : 90f;
                // Το άνοιγμα στο τείχος ισούται με το πλάτος της πύλης (ώστε να μη μένουν κενά)
                GateHalfAngle = Mathf.Max(2f, (Mathf.Max(gb.size.x, gb.size.z) * 0.5f + 0.3f) / WallRadius * Mathf.Rad2Deg);
                GameObject gate = Place(gatePrefab, Grounded(OnCircle(WallRadius, 180f)), gateYaw, root);
                if (gate != null) MarkStatic(gate);
            }
            Point(PointType.Gate, Grounded(OnCircle(WallRadius - 5f, 180f)), Vector3.back, 3);

            // Τμήματα τείχους κατά μήκος του κύκλου: το μήκος τους μετριέται από το μοντέλο
            GameObject panel = Prefab(VIK + "Buildings/pf_build_wall_panel_01.prefab");
            if (panel != null)
            {
                GameObject probe = (GameObject)PrefabUtility.InstantiatePrefab(panel);
                Bounds pb = WorldBounds(probe);
                bool longX = pb.size.x >= pb.size.z;
                float len = Mathf.Max(2f, Mathf.Max(pb.size.x, pb.size.z) * 0.95f);
                Object.DestroyImmediate(probe);

                float step = len / WallRadius * Mathf.Rad2Deg;
                int count = 0;
                for (float a = 180f + GateHalfAngle + step * 0.5f; a < 540f - GateHalfAngle - step * 0.5f && count < 160; a += step, count++)
                {
                    Vector3 p = Grounded(OnCircle(WallRadius, a));
                    // Ο μακρύς άξονας του τμήματος εφάπτεται στον κύκλο
                    float yaw = a + (longX ? 0f : 90f);
                    GameObject w = Place(panel, p, yaw, root);
                    if (w != null) MarkStatic(w);
                }
            }

            // Σκοπιές σε τέσσερα σημεία, λίγο μέσα από το τείχος
            GameObject tower = Prefab(VIK + "Buildings/pf_build_tower_01.prefab");
            foreach (float a in new[] { 150f, 210f, 45f, 315f })
            {
                Vector3 p = Grounded(OnCircle(WallRadius - 7f, a));
                GameObject t = Place(tower, p, a + 180f, root);
                if (t != null) MarkStatic(t);
                Point(PointType.Watchtower, Grounded(OnCircle(WallRadius - 11f, a)), -p, 1);
            }

            // Σημεία περιπολίας (για τους φρουρούς τη νύχτα), κατά μήκος του εσωτερικού δρόμου
            for (int i = 0; i < 10; i++)
                Point(PointType.Patrol, Grounded(OnCircle(Road2, i * 36f)), Vector3.forward, 1);

            // Πυρσοί στην πύλη και στον κεντρικό δρόμο
            GameObject torch = Prefab(VIK + "Props/pf_torch_stick_01.prefab");
            foreach (Vector3 p in new[] { new Vector3(-4f, 0f, -WallRadius + 3f), new Vector3(4f, 0f, -WallRadius + 3f), new Vector3(-3f, 0f, -Road2), new Vector3(3f, 0f, -Road1) })
            {
                GameObject t = Place(torch, Grounded(p), 0f, root);
                if (t != null) t.AddComponent<NightLight>();
            }
        }

        // ==================================================================== 6. Διακόσμηση
        static void BuildProps(List<Transform> homes)
        {
            Transform root = new GameObject("Props").transform;
            root.SetParent(_village, false);
            string[] props =
            {
                VIK + "Props/pf_barrels_01.prefab", VIK + "Props/pf_logpile_01.prefab", VIK + "Props/pf_buckets_01.prefab",
                VIK + "Props/pf_plankpile_01.prefab", VIK + "Props/pf_barrel_01.prefab", VIK + "Props/pf_shed_01.prefab",
                VIK + "Props/pf_fish_stick_01.prefab", VIK + "Props/pf_fence_01.prefab",
            };
            // Ένα-δύο αντικείμενα δίπλα σε κάθε σπίτι
            foreach (Transform h in homes)
            {
                int n = 1 + _rng.Next(2);
                for (int k = 0; k < n; k++)
                {
                    Vector3 side = Vector3.Cross(Vector3.up, h.forward).normalized * (k == 0 ? 3.2f : -3.2f);
                    GameObject g = Place(Prefab(props[_rng.Next(props.Length)]), Grounded(h.position + side - h.forward * 1.2f), Rand(0f, 360f), root);
                    if (g != null) MarkStatic(g);
                }
            }
            // Ογκόλιθοι/βράχια έξω από το τείχος
            string[] rocks = { "Rock_A_01", "Rock_B_01", "Rock_C_01", "Rock_Overgrown_A", "Rock_Overgrown_B", "Rock_D" };
            for (int i = 0; i < 30; i++)
            {
                float ang = Rand(0f, 360f);
                if (Mathf.Abs(Mathf.DeltaAngle(ang, 180f)) < 15f) continue;   // όχι πάνω στον δρόμο
                float r = Rand(WallRadius + 8f, TerrainSize * 0.45f);
                GameObject g = Place(Prefab(TDS + "Prefabs/Rocks/" + rocks[_rng.Next(rocks.Length)] + ".prefab"), Grounded(OnCircle(r, ang)) - Vector3.up * 0.3f, Rand(0f, 360f), root, Rand(0.6f, 1.6f));
                if (g != null) MarkStatic(g);
            }
        }

        // ==================================================================== 7. Δάσος
        static void PlantForest()
        {
            if (_terrain == null || _terrain.terrainData.treePrototypes.Length == 0) return;
            TerrainData td = _terrain.terrainData;
            List<TreeInstance> trees = new List<TreeInstance>();
            // Απλός έλεγχος ελάχιστης απόστασης σε πλέγμα (τύπου Poisson) για φυσική κατανομή
            HashSet<Vector2Int> used = new HashSet<Vector2Int>();
            int attempts = 0;
            int target = Mathf.RoundToInt(1100f * (TerrainSize * TerrainSize) / (320f * 320f));
            while (trees.Count < target && attempts < target * 20)
            {
                attempts++;
                float x = Rand(-TerrainSize * 0.5f + 4f, TerrainSize * 0.5f - 4f);
                float z = Rand(-TerrainSize * 0.5f + 4f, TerrainSize * 0.5f - 4f);
                float r = Mathf.Sqrt(x * x + z * z);
                if (r < WallRadius + 9f) continue;                     // όχι μέσα/δίπλα στο τείχος
                if (z < 0f && Mathf.Abs(x) < 9f) continue;              // ο δρόμος μένει ελεύθερος
                if (Mathf.PerlinNoise(x * 0.02f + 7f, z * 0.02f + 3f) < 0.32f) continue;   // ξέφωτα
                Vector2Int cell = new Vector2Int(Mathf.FloorToInt(x / 4.5f), Mathf.FloorToInt(z / 4.5f));
                if (used.Contains(cell)) continue;
                used.Add(cell);

                TreeInstance t = new TreeInstance();
                t.prototypeIndex = _rng.Next(td.treePrototypes.Length);
                float u = (x + TerrainSize * 0.5f) / TerrainSize, v = (z + TerrainSize * 0.5f) / TerrainSize;
                // Το y είναι κανονικοποιημένο ύψος (0..1) — το δέντρο «πατά» πάνω στο έδαφος
                t.position = new Vector3(u, td.GetInterpolatedHeight(u, v) / td.size.y, v);
                float s = Rand(0.8f, 1.3f);
                t.widthScale = s;
                t.heightScale = s * Rand(0.9f, 1.15f);
                t.rotation = Rand(0f, Mathf.PI * 2f);
                t.color = Color.white;
                t.lightmapColor = Color.white;
                trees.Add(t);
            }
            td.treeInstances = trees.ToArray();
            _terrain.Flush();
        }

        // ==================================================================== 8. Κάτοικοι
        struct Npc
        {
            public string name; public VillagerRole role; public bool female; public float wake, sleep;
            public Npc(string n, VillagerRole r, bool f, float w, float s) { name = n; role = r; female = f; wake = w; sleep = s; }
        }

        static void BuildVillagers()
        {
            GameObject male = Prefab("Assets/SopaDopa/URP_Medieval_Fantasy/Constructor_URP.prefab");
            GameObject femalePrefab = Prefab("Assets/SopaDopa/URP_Medieval_Fantasy/Constructor_fem_1.prefab");
            if (male == null) { Debug.LogWarning("[Greystone] Δεν βρέθηκαν οι χαρακτήρες SopaDopa — παραλείπονται οι κάτοικοι."); return; }

            Npc[] roster =
            {
                new Npc("Μπιόρν", VillagerRole.Blacksmith, false, 6.0f, 19.0f),
                new Npc("Ίνγκριντ", VillagerRole.Innkeeper, true, 7.0f, 23.5f),
                new Npc("Χάκον", VillagerRole.Elder, false, 8.0f, 20.0f),
                new Npc("Σίγκριντ", VillagerRole.Merchant, true, 7.5f, 18.5f),
                new Npc("Λέιφ", VillagerRole.Merchant, false, 7.0f, 18.0f),
                new Npc("Γκούναρ", VillagerRole.Guard, false, 0f, 24f),
                new Npc("Ούλφ", VillagerRole.Guard, false, 0f, 24f),
                new Npc("Τόρβαλντ", VillagerRole.Guard, false, 0f, 24f),
                new Npc("Άστριντ", VillagerRole.Farmer, true, 5.5f, 19.0f),
                new Npc("Έρικ", VillagerRole.Farmer, false, 5.5f, 19.5f),
                new Npc("Φρέγια", VillagerRole.Peasant, true, 7.0f, 21.0f),
                new Npc("Ρούνα", VillagerRole.Peasant, true, 8.0f, 20.5f),
                new Npc("Σβεν", VillagerRole.Peasant, false, 6.5f, 21.5f),
                new Npc("Χέλγκα", VillagerRole.Hunter, true, 5.0f, 18.0f),
            };

            Transform root = new GameObject("Villagers").transform;
            List<VillagePoint> homes = new List<VillagePoint>();
            foreach (VillagePoint p in _points.GetComponentsInChildren<VillagePoint>()) if (p.type == PointType.Home) homes.Add(p);

            for (int i = 0; i < roster.Length; i++)
            {
                Npc n = roster[i];
                GameObject prefab = n.female && femalePrefab != null ? femalePrefab : male;
                VillagePoint home = homes.Count > 0 ? homes[i % homes.Count] : null;
                Vector3 pos = home != null ? home.transform.position : Grounded(OnCircle(5f, i * 25f));

                GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.name = "NPC_" + n.name;
                go.transform.SetParent(root, false);
                go.transform.position = pos;
                go.transform.rotation = Quaternion.Euler(0f, Rand(0f, 360f), 0f);
                GameObjectUtility.SetStaticEditorFlags(go, (StaticEditorFlags)0);   // οι κάτοικοι κινούνται: όχι static

                Outfit outfit = go.AddComponent<Outfit>();
                outfit.role = n.role;
                outfit.seed = i * 7 + 3;
                outfit.Apply();

                NavMeshAgent agent = go.AddComponent<NavMeshAgent>();
                agent.radius = 0.3f;
                agent.height = 1.8f;
                agent.speed = n.role == VillagerRole.Guard ? 1.5f : Rand(1.1f, 1.4f);
                agent.angularSpeed = 360f;
                agent.acceleration = 6f;
                agent.stoppingDistance = 0.4f;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
                agent.avoidancePriority = 30 + _rng.Next(40);

                // Collider ανίχνευσης για το [E]. Layer 8: δεν επηρεάζει το bake του NavMesh.
                CapsuleCollider cap = go.AddComponent<CapsuleCollider>();
                cap.isTrigger = true;
                cap.center = new Vector3(0f, 0.9f, 0f);
                cap.height = 1.8f;
                cap.radius = 0.4f;
                go.layer = 8;

                go.AddComponent<HumanoidWalk>();
                VillagerAI ai = go.AddComponent<VillagerAI>();
                ai.displayName = n.name;
                ai.role = n.role;
                ai.female = n.female;
                ai.home = home;
                ai.wakeHour = n.wake + Rand(-0.3f, 0.3f);
                ai.homeHour = n.sleep + Rand(-0.3f, 0.3f);

                if (n.role == VillagerRole.Guard)
                {
                    // Ο φρουρός κρατά πυρσό τη νύχτα: σημειακό φως στο ύψος του χεριού
                    Light l = new GameObject("CarriedTorchLight").AddComponent<Light>();
                    l.type = LightType.Point;
                    l.color = new Color(1f, 0.65f, 0.35f);
                    l.range = 9f;
                    l.intensity = 2f;
                    l.shadows = LightShadows.None;
                    l.enabled = false;
                    l.transform.SetParent(go.transform, false);
                    l.transform.localPosition = new Vector3(0.35f, 1.5f, 0.3f);
                    ai.carriedLight = l;
                }
            }
        }

        // ==================================================================== 9. Παίκτης
        static void BuildPlayer()
        {
            GameObject player = new GameObject("Player");
            player.layer = 2;   // Ignore Raycast: δεν «χτυπά» τη δική του ακτίνα, δεν μπαίνει στο NavMesh
            player.transform.position = Grounded(new Vector3(0f, 0f, -PlazaRadius - 6f)) + Vector3.up * 0.1f;
            CharacterController cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.slopeLimit = 50f;
            cc.stepOffset = 0.4f;

            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(player.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            Camera cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 900f;
            cam.fieldOfView = 68f;
            camGo.AddComponent<AudioListener>();

            Light lantern = new GameObject("Lantern").AddComponent<Light>();
            lantern.type = LightType.Point;
            lantern.color = new Color(1f, 0.72f, 0.42f);
            lantern.range = 10f;
            lantern.intensity = 1.6f;
            lantern.shadows = LightShadows.None;
            lantern.transform.SetParent(camGo.transform, false);
            lantern.transform.localPosition = new Vector3(0.3f, -0.3f, 0.3f);

            FirstPersonController fpc = player.AddComponent<FirstPersonController>();
            fpc.cameraPivot = camGo.transform;
            fpc.lantern = lantern;

            GameObject hud = new GameObject("HUD");
            hud.AddComponent<VillageHud>().player = fpc;
        }

        // ==================================================================== 10. NavMesh
        static void BakeNavMesh()
        {
            GameObject nav = new GameObject("Navigation");
            NavMeshSurface surface = nav.AddComponent<NavMeshSurface>();
            VillageNavigation.Configure(surface);
            surface.BuildNavMesh();
            if (surface.navMeshData != null)
            {
                string path = GenFolder + "/VillageNavMesh.asset";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(surface.navMeshData, path);
                EditorUtility.SetDirty(surface);
            }
            nav.AddComponent<VillageNavigation>();
        }
    }
}
#endif
