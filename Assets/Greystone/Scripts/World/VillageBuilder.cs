// =====================================================================================
//  VillageBuilder.cs — Σύνθεση του χωριού
//  ---------------------------------------------------------------------------------
//  Τοποθετεί όλα τα κτίρια, τα αντικείμενα με λειτουργικότητα, τις πινακίδες, τους
//  κατοίκους και τα ζώα, σύμφωνα με το σχέδιο της WorldLayout. Στο τέλος εφαρμόζει
//  STATIC BATCHING σε όλη τη στατική γεωμετρία: τα meshes με ίδιο υλικό ενώνονται σε
//  μεγάλα batches, οπότε εκατοντάδες «κουτιά» σχεδιάζονται με λίγα draw calls.
//
//  Μοτίβο Builder: η σειρά κατασκευής έχει σημασία (π.χ. πρώτα τα κτίρια ώστε να
//  καταχωρηθούν τα αποτυπώματα πριν φυτευτεί η βλάστηση· πρώτα ο γράφος δρόμων και
//  μετά οι χωρικοί που τον χρησιμοποιούν).
// =====================================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Greystone
{
    public class VillageBuilder : MonoBehaviour
    {
        readonly List<House> _houses = new List<House>();
        readonly List<Transform> _staticRoots = new List<Transform>();
        Forge _forge;
        Well _well;
        MarketStall _stallA, _stallB;
        Windmill _mill;
        Church _church;
        House _tavern;

        struct HouseDef
        {
            public float x, z, yaw, w, d;
            public int floors;
            public WallStyle walls;
            public RoofStyle roof;
            public HouseDef(float x, float z, float yaw, float w, float d, int floors, WallStyle walls, RoofStyle roof)
            {
                this.x = x; this.z = z; this.yaw = yaw; this.w = w; this.d = d; this.floors = floors; this.walls = walls; this.roof = roof;
            }
        }

        static readonly HouseDef[] Houses =
        {
            // Βόρεια πλευρά κεντρικού δρόμου (κοιτούν νότια)
            new HouseDef(-64f,  13f, 180f, 8f, 6f, 2, WallStyle.Stone,     RoofStyle.Slate),   // 0
            new HouseDef(-50f,  13f, 180f, 9f, 6f, 2, WallStyle.Plaster,   RoofStyle.Thatch),  // 1
            new HouseDef(-36f,  13f, 180f, 7f, 6f, 1, WallStyle.Stone,     RoofStyle.Thatch),  // 2
            new HouseDef( 24f,  13f, 180f, 9f, 6.5f, 2, WallStyle.StoneGrey, RoofStyle.Slate), // 3
            new HouseDef( 38f,  13f, 180f, 8f, 6f, 2, WallStyle.Stone,     RoofStyle.Slate),   // 4
            new HouseDef( 52f,  13f, 180f, 7f, 6f, 1, WallStyle.Plaster,   RoofStyle.Thatch),  // 5
            // Νότια πλευρά (κοιτούν βόρεια)
            new HouseDef(-62f, -13f,   0f, 8f, 6f, 2, WallStyle.StoneGrey, RoofStyle.Slate),   // 6
            new HouseDef(-48f, -13f,   0f, 7f, 6f, 1, WallStyle.Stone,     RoofStyle.Thatch),  // 7
            new HouseDef( 44f, -13f,   0f, 7f, 6f, 2, WallStyle.Plaster,   RoofStyle.Slate),   // 8
            // Κατά μήκος του νότιου δρόμου
            new HouseDef(-13f, -26f,  90f, 8f, 6f, 2, WallStyle.Stone,     RoofStyle.Slate),   // 9
            new HouseDef(-13f, -40f,  90f, 7f, 6f, 1, WallStyle.Plaster,   RoofStyle.Thatch),  // 10
            new HouseDef( 13f, -40f, 270f, 8f, 6f, 2, WallStyle.Stone,     RoofStyle.Slate),   // 11
            new HouseDef( 13f, -22f, 270f, 7f, 6f, 1, WallStyle.StoneGrey, RoofStyle.Thatch),  // 12
        };

        public IEnumerator Build(Hud hud)
        {
            // ---------------- Κατοικίες ----------------
            for (int i = 0; i < Houses.Length; i++)
            {
                HouseDef h = Houses[i];
                HouseSpec s = new HouseSpec { width = h.w, depth = h.d, floors = h.floors, walls = h.walls, roof = h.roof, name = "Σπίτι " + (i + 1) };
                _houses.Add(PlaceHouse(s, h.x, h.z, h.yaw));
            }
            yield return null;

            // ---------------- Ταβέρνα (μπορείτε να μπείτε) ----------------
            _tavern = PlaceHouse(new HouseSpec { name = "Ταβέρνα", width = 11f, depth = 8f, floors = 1, walls = WallStyle.Stone, roof = RoofStyle.Slate, enterable = true, doorOffset = -1.5f }, 26f, -15f, 0f);
            Decor.AleStake(_tavern.transform, new Vector3(2.5f, 2.3f, 4.05f), 0f);

            // ---------------- Εκκλησία ----------------
            _church = Place<Church>("Church", 0f, 33f, 0f);
            _church.Build();
            _staticRoots.Add(_church.Static);
            WorldLayout.Footprints.Add(new Rect(-6f, 30.5f, 12f, 28f));
            Decor.DryStoneWall(transform, new[] { new Vector2(-3f, 27f), new Vector2(-13f, 27f), new Vector2(-13f, 66f), new Vector2(13f, 66f), new Vector2(13f, 27f), new Vector2(3f, 27f) }, 1.1f);
            Decor.Gravestones(transform, new Rect(-11.5f, 30f, 4.5f, 30f), 12, 11);
            Decor.Gravestones(transform, new Rect(7f, 30f, 4.5f, 30f), 12, 12);
            yield return null;

            // ---------------- Πλατεία ----------------
            _well = Place<Well>("Well", -8f, 8f, 0f);
            _well.Build();
            WorldLayout.Footprints.Add(new Rect(-9.5f, 6.5f, 3f, 3f));
            Decor.MarketCross(transform, WorldLayout.OnGround(8f, 8f));
            _stallA = Place<MarketStall>("StallApples", -9f, -8f, Mathf.Atan2(9f, 8f) * Mathf.Rad2Deg);
            _stallA.Build("cloth_red", "apple");
            _stallB = Place<MarketStall>("StallBread", 9f, -8f, Mathf.Atan2(-9f, 8f) * Mathf.Rad2Deg);
            _stallB.Build("cloth_blue", "bread");
            _staticRoots.Add(_stallA.transform.Find("Static"));
            _staticRoots.Add(_stallB.transform.Find("Static"));
            HurdyGurdy hg = Place<HurdyGurdy>("HurdyGurdy", -4.5f, -11.5f, 200f);
            hg.Build();
            Vector2[] torches = { new Vector2(-12f, 5f), new Vector2(12f, 5f), new Vector2(-12f, -5f), new Vector2(12f, -5f) };
            foreach (Vector2 t in torches) Place<WallLantern>("Torch", t.x, t.y, 0f).BuildTorch();
            Decor.Bench(transform, WorldLayout.OnGround(-12.5f, -10f), 45f);
            Decor.Bench(transform, WorldLayout.OnGround(12.5f, 10.5f), 225f);
            yield return null;

            // ---------------- Σιδηρουργείο ----------------
            _forge = Place<Forge>("Forge", -28f, -15f, 0f);
            _forge.Build();
            _staticRoots.Add(_forge.transform.Find("Static"));
            WorldLayout.Footprints.Add(new Rect(-31.5f, -17.5f, 7f, 5f));

            // ---------------- Ανεμόμυλος ----------------
            _mill = Place<Windmill>("Windmill", WorldLayout.MillPos.x, WorldLayout.MillPos.y, 0f);
            _mill.Build();
            WorldLayout.Footprints.Add(new Rect(WorldLayout.MillPos.x - 4.5f, WorldLayout.MillPos.y - 4.5f, 9f, 9f));

            // ---------------- Γέφυρα και διόδια ----------------
            float rx = WorldLayout.RiverX(0f);
            TollGate gate = Place<TollGate>("TollGate", rx - 17f, 0f, 0f);
            gate.transform.position = new Vector3(rx - 17f, 0f, 0f);
            gate.BuildBridge(rx - 16f, rx + 16f);
            gate.BuildGate();
            Place<WallLantern>("BridgeLantern", rx - 16.5f, 3.6f, 0f).BuildPostLantern();
            Place<WallLantern>("BridgeLantern", rx + 16.5f, -3.6f, 180f).BuildPostLantern();
            yield return null;

            // ---------------- Αγροτική ζώνη ----------------
            Decor.Fence(transform, WorldLayout.SheepPen, WorldLayout.SheepPen.center.y, 2.6f);
            Decor.Scarecrow(transform, WorldLayout.OnGround(-40f, -70f));
            Decor.Haystack(transform, WorldLayout.OnGround(-80f, -52f), 1f);
            Decor.Haystack(transform, WorldLayout.OnGround(-80f, -58f), 0.85f);
            Decor.Haystack(transform, WorldLayout.OnGround(68f, -60f), 1.1f);
            Decor.Woodpile(transform, WorldLayout.OnGround(-57.3f, 13f), 90f);
            Decor.Woodpile(transform, WorldLayout.OnGround(31f, 13.5f), 90f);
            Decor.Woodpile(transform, WorldLayout.OnGround(-24f, -18.5f), 0f);

            // ---------------- Πινακίδες πληροφοριών ----------------
            Board("welcome", 4.5f, -18.5f, 180f);
            Board("credits", 8.4f, -17.2f, 205f);
            Board("sky", -8.4f, -17.6f, 160f);
            Board("hurdy", -7.2f, -12.4f, 190f);
            Board("well", -11.8f, 10.8f, 135f);
            Board("market", 12.6f, -10.5f, 225f);
            Board("church", 4.2f, 28.2f, 180f);
            Board("clock", -4.2f, 28.2f, 180f);
            Board("forge", -22.5f, -9.2f, 0f);
            Board("windmill", -48.5f, 44.5f, 135f);
            Board("fields", -5.8f, -50f, 90f);
            Board("sheep", 30f, -26.6f, 180f);
            Board("bridge", rx - 20.5f, 4.2f, 180f);
            Board("tavern", 18.8f, -9.4f, 0f);
            Board("night", -18f, 4.2f, 180f);
            yield return null;

            // ---------------- Δίκτυο δρόμων (A*) και κάτοικοι ----------------
            new RoadNetwork();
            BuildVillagers();
            BuildAnimals();
            yield return null;

            // ---------------- Static batching ----------------
            foreach (House h in _houses) _staticRoots.Add(h.Static);
            _staticRoots.Add(_tavern.Static);
            _staticRoots.Add(gate.transform.Find("BridgeStatic"));
            List<GameObject> batch = new List<GameObject>();
            foreach (Transform root in _staticRoots)
            {
                if (root == null) continue;
                foreach (MeshRenderer mr in root.GetComponentsInChildren<MeshRenderer>())
                {
                    mr.gameObject.isStatic = true;
                    batch.Add(mr.gameObject);
                }
            }
            StaticBatchingUtility.Combine(batch.ToArray(), gameObject);

            // ---------------- Σημεία ενδιαφέροντος για τον χάρτη ----------------
            AddPoi(hud, "Πλατεία της αγοράς", 0f, -22f, 0f);
            AddPoi(hud, "Εκκλησία & καμπαναριό", 0f, 22f, 0f);
            AddPoi(hud, "Ταβέρνα", 24.5f, -5f, 180f);
            AddPoi(hud, "Σιδηρουργείο", -28f, -5f, 180f);
            AddPoi(hud, "Ανεμόμυλος", -46f, 42f, -45f);
            AddPoi(hud, "Γέφυρα & διόδια", rx - 24f, -1.5f, 90f);
            AddPoi(hud, "Μαντρί προβάτων", 28f, -29f, 110f);
            AddPoi(hud, "Χωράφια & σκιάχτρο", -4f, -58f, 250f);
            AddPoi(hud, "Οπωρώνας", -52f, -34f, 250f);
            AddPoi(hud, "Θέα από τον λόφο", -150f, 125f, 130f);
        }

        void AddPoi(Hud hud, string name, float x, float z, float yaw)
        {
            hud.Pois.Add(new Hud.Poi { name = name, pos = WorldLayout.OnGround(x, z, 0.3f), yaw = yaw });
        }

        T Place<T>(string name, float x, float z, float yaw) where T : Component
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = WorldLayout.OnGround(x, z);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            return go.AddComponent<T>();
        }

        House PlaceHouse(HouseSpec s, float x, float z, float yaw)
        {
            House h = Place<House>(s.name, x, z, yaw);
            h.Build(s);
            return h;
        }

        void Board(string key, float x, float z, float yaw)
        {
            Place<InfoBoard>("Board_" + key, x, z, yaw).Build(key);
        }

        // ------------------------------------------------------------------ Κάτοικοι
        Villager AddVillager(string name, Role role, bool female, House home, Vector3 work, float workYaw)
        {
            GameObject go = new GameObject("Villager_" + name);
            go.transform.SetParent(transform, false);
            go.transform.position = home.DoorstepWorld;
            Villager v = go.AddComponent<Villager>();
            v.displayName = name;
            v.role = role;
            v.female = female;
            v.home = home;
            v.workSpot = work;
            v.workYaw = workYaw;
            v.forge = _forge;
            v.well = _well;
            return v;
        }

        void BuildVillagers()
        {
            // Χρώματα από φυτικές βαφές της εποχής: ριζάρι (κόκκινο), λουλάκι/woad (μπλε), weld (κίτρινο)
            Color black = new Color(0.08f, 0.08f, 0.09f);
            Color hose = new Color(0.25f, 0.2f, 0.15f);

            List<Villager> all = new List<Villager>();
            all.Add(AddVillager("Thomas", Role.Smith, false, _houses[7], _forge.AnvilWorkSpot, 0f));
            all.Add(AddVillager("Alys", Role.Vendor, true, _houses[3], _stallA.VendorSpotWorld, _stallA.transform.eulerAngles.y));
            all.Add(AddVillager("John", Role.Vendor, false, _houses[4], _stallB.VendorSpotWorld, _stallB.transform.eulerAngles.y));
            Villager walter = AddVillager("Walter", Role.Farmer, false, _houses[9], WorldLayout.OnGround(-30f, -52f), 180f);
            walter.wanderArea = new Rect(-62f, -84f, 36f, 30f);
            all.Add(walter);
            Villager maud = AddVillager("Maud", Role.Farmer, true, _houses[11], WorldLayout.OnGround(30f, -52f), 180f);
            maud.wanderArea = new Rect(20f, -84f, 36f, 30f);
            all.Add(maud);
            all.Add(AddVillager("Edmund", Role.Priest, false, _houses[1], WorldLayout.OnGround(1.5f, 28.8f), 180f));
            Villager hugh = AddVillager("Hugh", Role.Shepherd, false, _houses[12], WorldLayout.OnGround(36.5f, -31f), 90f);
            hugh.wanderArea = new Rect(WorldLayout.SheepPen.xMin + 1.5f, WorldLayout.SheepPen.yMin + 1.5f, WorldLayout.SheepPen.width - 3f, WorldLayout.SheepPen.height - 3f);
            all.Add(hugh);
            all.Add(AddVillager("Agnes", Role.WaterCarrier, true, _houses[0], _well.DrawSpotWorld, 0f));
            Vector3 millDoor = _mill.transform.position + Quaternion.Euler(0, 135f, 0) * new Vector3(0, 0, 5.2f);
            all.Add(AddVillager("Robert", Role.Miller, false, _houses[5], WorldLayout.OnGround(millDoor.x, millDoor.z), 135f));
            Villager geoff = AddVillager("Geoffrey", Role.Watchman, false, _houses[8], Vector3.zero, 0f);
            geoff.patrol = new List<Vector3>
            {
                WorldLayout.OnGround(-45f, -1.5f), WorldLayout.OnGround(0f, -14f), WorldLayout.OnGround(45f, -1.5f),
                WorldLayout.OnGround(45f, 1.5f), WorldLayout.OnGround(0f, 24f), WorldLayout.OnGround(-45f, 1.5f),
            };
            all.Add(geoff);
            Villager joan = AddVillager("Joan", Role.Innkeeper, true, _tavern, _tavern.DoorstepWorld, 0f);
            // Εσωτερική διαδρομή: πίσω από την πόρτα -> κατά μήκος του βόρειου τοίχου -> πίσω από τον πάγκο
            joan.interiorPath = new List<Vector3>
            {
                _tavern.InsideWorld(new Vector3(-1.5f, 0.1f, 3f)),
                _tavern.InsideWorld(new Vector3(4.85f, 0.1f, 2.2f)),
                _tavern.InsideWorld(new Vector3(4.85f, 0.1f, -0.9f)),
            };
            joan.extraSpot = joan.interiorPath[joan.interiorPath.Count - 1];
            joan.workYaw = 270f;
            all.Add(joan);

            // Build μετά την απόδοση όλων των πεδίων (το Build ελέγχει αν είναι ώρα εργασίας)
            for (int i = 0; i < all.Count; i++)
            {
                Villager v = all[i];
                v.Build(TunicOf(v), v.role == Role.Priest ? black : hose, 100 + i);
            }
        }

        static Color TunicOf(Villager v)
        {
            switch (v.role)
            {
                case Role.Smith: return new Color(0.42f, 0.26f, 0.16f);
                case Role.Vendor: return v.female ? new Color(0.52f, 0.17f, 0.12f) : new Color(0.18f, 0.27f, 0.47f);
                case Role.Farmer: return v.female ? new Color(0.62f, 0.55f, 0.25f) : new Color(0.6f, 0.55f, 0.45f);
                case Role.Priest: return new Color(0.08f, 0.08f, 0.09f);
                case Role.Shepherd: return new Color(0.25f, 0.36f, 0.2f);
                case Role.WaterCarrier: return new Color(0.2f, 0.32f, 0.5f);
                case Role.Miller: return new Color(0.72f, 0.7f, 0.64f);
                case Role.Watchman: return new Color(0.35f, 0.22f, 0.15f);
                default: return new Color(0.3f, 0.4f, 0.24f);
            }
        }

        void BuildAnimals()
        {
            Rect pen = WorldLayout.SheepPen;
            for (int i = 0; i < 9; i++)
            {
                GameObject go = new GameObject("Sheep_" + i);
                go.transform.SetParent(transform, false);
                float x = Random.Range(pen.xMin + 2f, pen.xMax - 2f), z = Random.Range(pen.yMin + 2f, pen.yMax - 2f);
                go.transform.position = WorldLayout.OnGround(x, z);
                go.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                go.AddComponent<Sheep>().Build(pen, 200 + i);
            }

            GameObject flock = new GameObject("BirdFlock");
            flock.transform.SetParent(transform, false);
            flock.AddComponent<BirdFlock>().Build();

            GameObject cart = new GameObject("HorseCart");
            cart.transform.SetParent(transform, false);
            cart.AddComponent<Cart>().Build();
        }
    }
}
