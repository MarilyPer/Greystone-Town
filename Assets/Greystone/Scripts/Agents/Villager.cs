// =====================================================================================
//  Villager.cs — Κάτοικος του χωριού
//  ---------------------------------------------------------------------------------
//  Κάθε χωρικός έχει ρόλο (σιδεράς, μανάβης, αγρότης, ιερέας…), σπίτι και θέση εργασίας.
//
//  ΗΜΕΡΗΣΙΟ ΠΡΟΓΡΑΜΜΑ: ξυπνά (μέσα στο σπίτι), βγαίνει από την πόρτα, περπατά στους
//  δρόμους (διαδρομή A* από το RoadNetwork) μέχρι τη δουλειά του, εργάζεται (με ανάλογο
//  animation), και το σούρουπο επιστρέφει σπίτι και «χάνεται» μέσα. Ο νυχτοφύλακας κάνει
//  το αντίστροφο: περιπολεί τη νύχτα κρατώντας φανάρι.
//
//  ΜΗΧΑΝΗ ΚΑΤΑΣΤΑΣΕΩΝ (FSM): AtHome -> Walking -> Working -> Walking -> AtHome,
//  και Talking (όταν ο χρήστης του μιλήσει με [E]).
//
//  ANIMATION: διαδικαστικό — τα πόδια/χέρια ταλαντώνονται με ημίτονο ανάλογο της
//  απόστασης που διανύθηκε (άρα δεν «γλιστρούν»), και ειδικές κινήσεις ανά εργασία
//  (σφυρηλάτηση, σκάλισμα, προσευχή).
//
//  Κληρονομεί από Agent: ενημερώνεται από τον UpdateScheduler με αραίωση ανάλογα με την απόσταση.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Greystone
{
    public enum Role { Smith, Vendor, Farmer, Priest, Shepherd, WaterCarrier, Miller, Watchman, Innkeeper }

    public class Villager : Agent
    {
        enum State { AtHome, Walking, Working, Talking }

        // ---------------- Ρυθμίσεις (ορίζονται από τον VillageBuilder) ----------------
        public string displayName;
        public Role role;
        public bool female;
        public House home;
        public Vector3 workSpot;
        public float workYaw;
        public Vector3 extraSpot;              // τελική θέση μέσα στην ταβέρνα
        public List<Vector3> interiorPath;     // διαδρομή από την είσοδο ως το extraSpot (αποφεύγει τραπέζια/πάγκο)
        public Forge forge;
        public Well well;
        public List<Vector3> patrol;           // διαδρομή νυχτοφύλακα
        public Rect wanderArea;                // περιοχή περιπλάνησης (αγρότες, βοσκός)

        // ---------------- Κατάσταση ----------------
        State _state = State.AtHome;
        State _resumeState;
        List<Vector3> _path;
        int _pathIndex;
        bool _goingHome;
        float _leaveHour, _returnHour;
        float _timer, _walkPhase, _workPhase;
        float _talkTimer;
        int _patrolIndex;
        bool _visible = true;
        bool _indoorTarget;

        // ---------------- Σκελετός (rig) ----------------
        Transform _rig, _legL, _legR, _armL, _armR, _torso, _head;
        Light _lantern;
        Renderer[] _renderers;
        const float WalkSpeed = 1.35f;

        public string RoleName
        {
            get
            {
                switch (role)
                {
                    case Role.Smith: return "σιδεράς";
                    case Role.Vendor: return female ? "πωλήτρια" : "πωλητής";
                    case Role.Farmer: return female ? "αγρότισσα" : "αγρότης";
                    case Role.Priest: return "εφημέριος";
                    case Role.Shepherd: return "βοσκός";
                    case Role.WaterCarrier: return "νερουλού";
                    case Role.Miller: return "μυλωνάς";
                    case Role.Watchman: return "νυχτοφύλακας";
                    default: return "ταβερνιάρισσα";
                }
            }
        }

        // ================================================================== Κατασκευή
        public void Build(Color tunic, Color legs, int seed)
        {
            System.Random r = new System.Random(seed);
            _leaveHour = 6f + (float)r.NextDouble() * 1.8f;
            _returnHour = 17.3f + (float)r.NextDouble() * 1.8f;
            if (role == Role.Innkeeper) { _leaveHour = 7f; _returnHour = 23.5f; }
            if (role == Role.Priest) { _leaveHour = 6.5f; _returnHour = 20f; }

            _rig = Geo.Child(transform, "Rig", Vector3.zero);
            Material tunicMat = Mats.Solid(tunic, 0.1f);
            Material legMat = Mats.Solid(legs, 0.1f);
            Material skin = Mats.Get("skin");
            Material shoe = Mats.Get("leather");
            bool robe = role == Role.Priest || female;

            // Πόδια
            _legL = Geo.Child(_rig, "LegL", new Vector3(-0.1f, 0.9f, 0));
            _legR = Geo.Child(_rig, "LegR", new Vector3(0.1f, 0.9f, 0));
            foreach (Transform leg in new[] { _legL, _legR })
            {
                Geo.Cyl(leg, 0.075f, 0.88f, new Vector3(0, -0.44f, 0), legMat, 0.8f, 8);
                Geo.Box(leg, new Vector3(0.12f, 0.08f, 0.24f), new Vector3(0, -0.86f, 0.05f), shoe);
            }
            // Κορμός: χιτώνας ή μακρύ φόρεμα/ράσο
            _torso = Geo.Child(_rig, "Torso", new Vector3(0, 0.9f, 0));
            if (robe) Geo.Cyl(_torso, 0.34f, 1.05f, new Vector3(0, 0.22f, 0), tunicMat, 0.6f, 12);
            else Geo.Cyl(_torso, 0.26f, 0.78f, new Vector3(0, 0.38f, 0), tunicMat, 0.78f, 12);
            Geo.Cyl(_torso, 0.24f, 0.07f, new Vector3(0, 0.3f, 0), shoe, 1f, 12);                    // ζώνη
            if (role == Role.Smith) Geo.Box(_torso, new Vector3(0.4f, 0.7f, 0.04f), new Vector3(0, 0.25f, 0.23f), shoe);   // ποδιά
            if (role == Role.Priest) Geo.Cyl(_torso, 0.12f, 0.05f, new Vector3(0, 0.74f, 0), Mats.Get("plaster"), 1f, 10); // κολάρο
            // Κεφάλι
            _head = Geo.Child(_rig, "Head", new Vector3(0, 1.72f, 0));
            Geo.Ball(_head, new Vector3(0.23f, 0.26f, 0.24f), Vector3.zero, skin, 12, 8);
            Geo.Ball(_head, new Vector3(0.05f, 0.06f, 0.05f), new Vector3(0, -0.01f, 0.12f), skin, 6, 4);   // μύτη
            Material hair = Mats.Get("hair_dark");
            BuildHeadwear(_head, hair, r);
            // Χέρια
            _armL = Geo.Child(_rig, "ArmL", new Vector3(-0.27f, 1.52f, 0));
            _armR = Geo.Child(_rig, "ArmR", new Vector3(0.27f, 1.52f, 0));
            foreach (Transform arm in new[] { _armL, _armR })
            {
                Geo.Cyl(arm, 0.06f, 0.6f, new Vector3(0, -0.29f, 0), tunicMat, 0.85f, 8);
                Geo.Ball(arm, Vector3.one * 0.1f, new Vector3(0, -0.63f, 0), skin, 8, 6);
            }
            BuildTool(r);

            // Ζώνη αλληλεπίδρασης (μιλήστε μου)
            Interactable it = Interactable.Create(transform, new Vector3(0, 1f, 0), new Vector3(0.8f, 2f, 0.8f), "");
            it.PromptProvider = () => "Μιλήστε με τον/την " + displayName + " (" + RoleName + ")";
            it.Interacted += Talk;

            _renderers = GetComponentsInChildren<Renderer>();
            SetVisible(false);
            _state = State.AtHome;
            // Αν η εκκίνηση γίνεται μέρα, οι εργαζόμενοι βρίσκονται ήδη στη δουλειά τους.
            if (WantsToWork(TimeManager.Instance.hours) && role != Role.Watchman)
            {
                Vector3 spot = role == Role.Innkeeper ? extraSpot : workSpot;
                transform.position = spot;
                transform.rotation = Quaternion.Euler(0, workYaw, 0);
                SetVisible(true);
                _indoorTarget = role == Role.Innkeeper;
                _state = State.Working;
            }
        }

        void BuildHeadwear(Transform head, Material hair, System.Random r)
        {
            switch (role)
            {
                case Role.Farmer:   // ψάθινο καπέλο
                    Geo.Cyl(head, 0.3f, 0.03f, new Vector3(0, 0.1f, 0), Mats.Get("straw"), 1f, 12);
                    Geo.Cyl(head, 0.14f, 0.14f, new Vector3(0, 0.17f, 0), Mats.Get("straw"), 0.8f, 10);
                    break;
                case Role.Priest:
                case Role.Watchman:
                case Role.Shepherd:  // κουκούλα (hood)
                    Geo.Ball(head, new Vector3(0.28f, 0.3f, 0.3f), new Vector3(0, 0.03f, -0.03f), Mats.Solid(role == Role.Priest ? new Color(0.1f, 0.1f, 0.12f) : new Color(0.3f, 0.26f, 0.2f)), 10, 8);
                    break;
                case Role.Miller:    // λευκός σκούφος (αλευρωμένος)
                    Geo.Ball(head, new Vector3(0.26f, 0.18f, 0.26f), new Vector3(0, 0.08f, 0), Mats.Get("plaster"), 10, 6);
                    break;
                default:
                    if (female) Geo.Ball(head, new Vector3(0.27f, 0.25f, 0.28f), new Vector3(0, 0.05f, -0.02f), Mats.Get("plaster"), 10, 6);   // λευκή μαντίλα (coif)
                    else Geo.Ball(head, new Vector3(0.25f, 0.18f, 0.26f), new Vector3(0, 0.07f, -0.02f), hair, 10, 6);
                    break;
            }
        }

        void BuildTool(System.Random r)
        {
            Transform hand = Geo.Child(_armR, "Hand", new Vector3(0, -0.63f, 0.02f));
            Material wood = Mats.Get("wood");
            switch (role)
            {
                case Role.Smith:
                    Geo.Box(hand, new Vector3(0.04f, 0.04f, 0.4f), new Vector3(0, 0, 0.18f), wood);
                    Geo.Box(hand, new Vector3(0.08f, 0.14f, 0.08f), new Vector3(0, 0, 0.38f), Mats.Get("iron"));
                    break;
                case Role.Farmer:
                    Geo.Box(hand, new Vector3(0.04f, 1.4f, 0.04f), new Vector3(0, 0.3f, 0.05f), wood);
                    Geo.Box(hand, new Vector3(0.2f, 0.05f, 0.15f), new Vector3(0, -0.4f, 0.12f), Mats.Get("iron"));
                    break;
                case Role.Shepherd:
                    Geo.Box(hand, new Vector3(0.04f, 1.7f, 0.04f), new Vector3(0, 0.45f, 0.05f), wood);
                    Geo.Cyl(hand, 0.1f, 0.03f, new Vector3(0, 1.32f, 0.12f), wood, 1f, 8, new Vector3(0, 0, 90));
                    break;
                case Role.Watchman:
                    Geo.Box(hand, new Vector3(0.02f, 0.2f, 0.02f), new Vector3(0, -0.1f, 0), Mats.Get("iron"));
                    Geo.Box(hand, new Vector3(0.16f, 0.22f, 0.16f), new Vector3(0, -0.3f, 0), Mats.Get("lantern_glass"));
                    _lantern = FX.PointLight(hand, new Vector3(0, -0.3f, 0.1f), new Color(1f, 0.7f, 0.4f), 8f, 1.3f);
                    LightLod.Register(_lantern);
                    Geo.Box(_armL, new Vector3(0.04f, 1.9f, 0.04f), new Vector3(0, -0.4f, 0.1f), wood);   // ραβδί
                    break;
            }
        }

        // ================================================================== Πρόγραμμα
        bool WantsToWork(float h)
        {
            if (role == Role.Watchman) return h >= 20.5f || h < 5f;
            return h >= _leaveHour && h < _returnHour;
        }

        public override void AgentUpdate(float dt)
        {
            if (dt <= 0f) return;
            float h = TimeManager.Instance.hours;
            bool work = WantsToWork(h);

            switch (_state)
            {
                case State.AtHome:
                    if (work) LeaveHome();
                    break;
                case State.Working:
                    if (!work) GoHome();
                    else DoWork(dt);
                    break;
                case State.Walking:
                    // Αν άλλαξε το πρόγραμμα ενώ περπατά (π.χ. γρήγορη ροή χρόνου), αλλάζει προορισμό.
                    if (_goingHome && work) GoToWork();
                    else if (!_goingHome && !work) GoHome();
                    Walk(dt);
                    break;
                case State.Talking:
                    _talkTimer -= dt;
                    FacePlayer(dt);
                    if (_talkTimer <= 0f) _state = _resumeState;
                    break;
            }
            Animate(dt);
        }

        void LeaveHome()
        {
            transform.position = home.DoorstepWorld;
            home.door.OpenBriefly();
            SetVisible(true);
            if (_lantern != null) LightLod.SetWanted(_lantern, true);
            GoToWork();
        }

        void GoToWork()
        {
            _goingHome = false;
            Vector3 target = workSpot;
            if (role == Role.Watchman && patrol != null && patrol.Count > 0) target = patrol[_patrolIndex];
            _path = RoadNetwork.Instance.FindPath(transform.position, target);
            if (role == Role.Innkeeper && interiorPath != null) _path.AddRange(interiorPath);
            _indoorTarget = role == Role.Innkeeper;
            _pathIndex = 1;
            _state = State.Walking;
        }

        void GoHome()
        {
            _goingHome = true;
            Vector3 from = transform.position;
            _path = new List<Vector3>();
            if (_indoorTarget && interiorPath != null)
            {
                // Πρώτα βγαίνει από την ταβέρνα ακολουθώντας ανάποδα την εσωτερική διαδρομή
                home.door.OpenBriefly(8f);
                _path.Add(from);
                for (int i = interiorPath.Count - 2; i >= 0; i--) _path.Add(interiorPath[i]);
                from = home.DoorstepWorld;
            }
            _path.AddRange(RoadNetwork.Instance.FindPath(from, home.DoorstepWorld));
            _pathIndex = 1;
            _indoorTarget = false;
            _state = State.Walking;
        }

        void Walk(float dt)
        {
            if (_path == null || _pathIndex >= _path.Count)
            {
                Arrive();
                return;
            }
            Vector3 target = _path[_pathIndex];
            Vector3 pos = transform.position;
            Vector3 to = target - pos;
            to.y = 0f;
            float step = WalkSpeed * dt;
            if (to.magnitude <= step)
            {
                pos.x = target.x;
                pos.z = target.z;
                _pathIndex++;
            }
            else
            {
                pos += to.normalized * step;
                Quaternion look = Quaternion.LookRotation(to.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-dt * 8f));
            }
            pos.y = WorldLayout.HeightAt(pos.x, pos.z) + (WorldLayout.InFootprint(new Vector2(pos.x, pos.z), -0.6f) ? 0.08f : 0f);
            transform.position = pos;
            _walkPhase += step * 2.4f;
        }

        void Arrive()
        {
            if (_goingHome)
            {
                home.door.OpenBriefly();
                SetVisible(false);
                if (_lantern != null) LightLod.SetWanted(_lantern, false);
                _state = State.AtHome;
                return;
            }
            if (role == Role.Watchman && patrol != null && patrol.Count > 0)
            {
                // Επόμενο σημείο περιπολίας (κυκλικά)
                _patrolIndex = (_patrolIndex + 1) % patrol.Count;
                _timer = 3f;
                _state = State.Working;
                return;
            }
            transform.rotation = Quaternion.Euler(0, workYaw, 0);
            _state = State.Working;
            _timer = Random.Range(4f, 10f);
        }

        void DoWork(float dt)
        {
            _timer -= dt;
            switch (role)
            {
                case Role.Watchman:
                    if (_timer <= 0f) GoToWork();   // συνέχεια περιπολίας
                    break;
                case Role.Farmer:
                case Role.Shepherd:
                    // Περιπλάνηση σε τυχαία σημεία της περιοχής εργασίας (ευθεία, χωρίς A*)
                    if (_timer <= 0f)
                    {
                        Vector3 p = new Vector3(Random.Range(wanderArea.xMin, wanderArea.xMax), 0, Random.Range(wanderArea.yMin, wanderArea.yMax));
                        _path = new List<Vector3> { transform.position, WorldLayout.OnGround(p.x, p.z) };
                        _pathIndex = 1;
                        _timer = Random.Range(8f, 16f);
                    }
                    if (_path != null && _pathIndex < _path.Count)
                    {
                        Vector3 before = transform.position;
                        Walk(dt);
                        if (_pathIndex >= _path.Count) transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                        if ((transform.position - before).sqrMagnitude > 0f) return;
                    }
                    break;
                case Role.WaterCarrier:
                    if (_timer <= 0f && well != null)
                    {
                        well.Draw(false);
                        _timer = Random.Range(20f, 35f);
                    }
                    break;
            }
        }

        // ================================================================== Διάλογος
        void Talk(PlayerController p)
        {
            if (_state == State.AtHome) return;
            if (_state != State.Talking) _resumeState = _state;
            _state = State.Talking;
            _talkTimer = 5f;
            if (Hud.Instance != null) Hud.Instance.Notify(displayName + ": «" + Dialogue() + "»", 6f);
        }

        void FacePlayer(float dt)
        {
            if (PlayerController.Instance == null) return;
            Vector3 d = PlayerController.Instance.transform.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), 1f - Mathf.Exp(-dt * 6f));
        }

        string Dialogue()
        {
            WeatherSystem w = WeatherSystem.Instance;
            bool rain = w != null && w.State == WeatherState.Rain;
            string[] lines;
            switch (role)
            {
                case Role.Smith:
                    lines = new[] { "Το σίδερο το σφυρηλατείς όσο είναι κόκκινο — κρύο δεν λυγίζει.", "Πάτα το φυσερό αν θες να δεις τη φωτιά να φουντώνει.", "Φτιάχνω πέταλα, καρφιά και μεντεσέδες για όλο το χωριό." };
                    break;
                case Role.Vendor:
                    lines = new[] { "Φρέσκα μήλα από τον οπωρώνα! Μισή πένα η ντουζίνα.", "Η αγορά γίνεται με άδεια του βασιλιά, με χάρτη σφραγισμένο.", rain ? "Με τέτοια βροχή, ποιος θα ψωνίσει σήμερα;" : "Καλή μέρα για αγορά, ο ουρανός είναι καθαρός." };
                    break;
                case Role.Farmer:
                    lines = new[] { "Φέτος σιτάρι εδώ, του χρόνου κριθάρι, και μετά ο αγρός ξεκουράζεται.", "Κάθε οικογένεια καλλιεργεί τη δική της λωρίδα.", rain ? "Η βροχή κάνει καλό στα στάχυα." : "Τα στάχυα θα είναι έτοιμα για θερισμό σε λίγες εβδομάδες." };
                    break;
                case Role.Priest:
                    lines = new[] { "Η καμπάνα μετρά τις ώρες για όλους μας.", "Το ρολόι του πύργου είναι το καμάρι του χωριού.", "Ο ναός χτίστηκε με τα χρήματα από το μαλλί." };
                    break;
                case Role.Shepherd:
                    lines = new[] { "Αυτά είναι Cotswold — τα λένε «λιοντάρια» για το μαλλί τους.", "Μην τρέχεις κοντά στα πρόβατα, τρομάζουν.", "Το μαλλί μας φτάνει ως τη Φλάνδρα." };
                    break;
                case Role.WaterCarrier:
                    lines = new[] { "Το νερό του πηγαδιού είναι το πιο καθαρό του τόπου.", "Γύρνα τη μανιβέλα αργά, αλλιώς χύνεται ο κουβάς." };
                    break;
                case Role.Miller:
                    lines = new[] { "Ο τρούλος γυρίζει μόνος του προς τον αέρα.", "Όταν δυναμώνει ο άνεμος, βάζω φρένο για να μη σπάσουν τα πανιά." };
                    break;
                case Role.Watchman:
                    lines = new[] { "Όλα ήσυχα. Κρατάω το φανάρι να βλέπουν όλοι τον δρόμο.", "Από τον καιρό του Καταστατικού του Winchester φυλάμε σκοπιά κάθε νύχτα." };
                    break;
                default:
                    lines = new[] { "Καλώς όρισες! Έχουμε φρέσκια μπύρα — το είδες το ale-stake έξω;", "Κάθισε δίπλα στο τζάκι να ζεσταθείς." };
                    break;
            }
            return lines[Random.Range(0, lines.Length)];
        }

        // ================================================================== Animation
        void Animate(float dt)
        {
            bool walking = _state == State.Walking || ((_state == State.Working) && _path != null && _pathIndex < _path.Count && (role == Role.Farmer || role == Role.Shepherd));
            float swing = walking ? Mathf.Sin(_walkPhase) : 0f;
            float legA = swing * 32f;
            _legL.localRotation = Quaternion.Euler(legA, 0, 0);
            _legR.localRotation = Quaternion.Euler(-legA, 0, 0);
            _rig.localPosition = new Vector3(0, walking ? Mathf.Abs(Mathf.Cos(_walkPhase)) * 0.035f : 0f, 0);

            float armL = -legA * 0.8f, armR = legA * 0.8f;
            float torsoPitch = 0f;
            if (_state == State.Working && !walking)
            {
                _workPhase += dt;
                switch (role)
                {
                    case Role.Smith:
                        // Σφυρηλάτηση: ο βραχίονας σηκώνεται αργά και πέφτει γρήγορα
                        float cycle = Mathf.Repeat(_workPhase, 1.1f) / 1.1f;
                        float prev = Mathf.Repeat(_workPhase - dt, 1.1f) / 1.1f;
                        armR = cycle < 0.7f ? Mathf.Lerp(-10f, -130f, cycle / 0.7f) : Mathf.Lerp(-130f, -10f, (cycle - 0.7f) / 0.3f);
                        armL = -35f;
                        torsoPitch = 8f;
                        if (prev > cycle && forge != null && _visible) forge.Strike();   // στιγμή κρούσης
                        break;
                    case Role.Farmer:
                        armR = -50f + Mathf.Sin(_workPhase * 3f) * 30f;
                        armL = -60f + Mathf.Sin(_workPhase * 3f) * 30f;
                        torsoPitch = 18f + Mathf.Sin(_workPhase * 3f) * 8f;
                        break;
                    case Role.Priest:
                        armR = -55f; armL = -55f; torsoPitch = 6f;
                        break;
                    case Role.Vendor:
                    case Role.Innkeeper:
                        armR = -20f + Mathf.Sin(_workPhase * 1.3f) * 20f;
                        armL = -15f;
                        break;
                    default:
                        armR = Mathf.Sin(_workPhase * 0.8f) * 5f;
                        armL = -armR;
                        break;
                }
            }
            if (_state == State.Talking) { armR = -30f + Mathf.Sin(Time.time * 4f) * 15f; armL = 0f; }
            if (role == Role.Watchman) armR = Mathf.Min(armR, -40f);   // κρατά το φανάρι μπροστά
            _armL.localRotation = Quaternion.Euler(armL, 0, 0);
            _armR.localRotation = Quaternion.Euler(armR, 0, 0);
            _torso.localRotation = Quaternion.Euler(torsoPitch, 0, 0);
            _head.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 0.5f + tickOffset) * 4f, Mathf.Sin(Time.time * 0.3f + tickOffset) * 12f, 0);
        }

        void SetVisible(bool v)
        {
            _visible = v;
            for (int i = 0; i < _renderers.Length; i++) _renderers[i].enabled = v;
            Collider[] cols = GetComponentsInChildren<Collider>();
            for (int i = 0; i < cols.Length; i++) cols[i].enabled = v;
        }
    }
}
