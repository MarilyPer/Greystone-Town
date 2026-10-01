// =====================================================================================
//  VillagerAI.cs — Συμπεριφορά κατοίκου (NPC)
//  ---------------------------------------------------------------------------------
//  ΜΗΧΑΝΗ ΚΑΤΑΣΤΑΣΕΩΝ (Finite State Machine):
//
//      AtHome ──(ώρα εργασίας)──▶ Walking ──(άφιξη)──▶ Working ──(βράδυ)──▶ Walking ──▶ AtHome
//                                     ▲                    │
//                                     └──(νέο σημείο)──────┘          + Talking (όταν του μιλήσετε)
//
//  * ΗΜΕΡΗΣΙΟ ΠΡΟΓΡΑΜΜΑ: κάθε κάτοικος έχει ώρα που ξυπνά και ώρα που γυρίζει σπίτι (με τυχαία
//    διαφορά ώστε να μη μετακινούνται όλοι ταυτόχρονα). Τη νύχτα «μπαίνουν» στα σπίτια τους
//    (κρύβονται). Οι φρουροί αντίθετα περιπολούν τη νύχτα κρατώντας πυρσό.
//  * ΠΛΟΗΓΗΣΗ: NavMeshAgent — η μηχανή βρίσκει διαδρομή (A*) πάνω στο NavMesh, αποφεύγει κτίρια
//    και άλλους κατοίκους (RVO avoidance).
//  * ΕΡΓΑΣΙΑ: στον χώρο εργασίας τους μετακινούνται σε κοντινά σημεία και κάνουν χειρονομίες
//    ανάλογα με τον ρόλο (σφυρηλάτηση, πώληση, προσευχή).
//  * ΒΕΛΤΙΣΤΟΠΟΙΗΣΗ (time-slicing): η «σκέψη» (αποφάσεις) τρέχει μόνο κάθε ~0.4 s και με
//    τυχαία μετατόπιση ανά κάτοικο, ώστε το κόστος να μοιράζεται ανάμεσα στα frames. Η κίνηση
//    καθαυτή γίνεται ομαλά από τον NavMeshAgent σε κάθε frame.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Greystone
{
    public interface IInteractable
    {
        string Prompt { get; }
        void Interact(Transform player);
    }

    [RequireComponent(typeof(NavMeshAgent))]
    public class VillagerAI : MonoBehaviour, IInteractable
    {
        enum State { AtHome, Walking, Working, Talking }

        [Header("Ταυτότητα")]
        public string displayName = "Κάτοικος";
        public VillagerRole role = VillagerRole.Peasant;
        public bool female;

        [Header("Πρόγραμμα")]
        public VillagePoint home;
        [Range(0f, 24f)] public float wakeHour = 7f;
        [Range(0f, 24f)] public float homeHour = 19.5f;
        [Tooltip("Ακτίνα μέσα στην οποία κινείται όταν εργάζεται")]
        public float workRadius = 5f;

        [Header("Φρουροί")]
        [Tooltip("Φως που κρατά ο φρουρός τη νύχτα (προαιρετικό)")]
        public Light carriedLight;

        State _state = State.AtHome;
        State _resume;
        NavMeshAgent _agent;
        HumanoidWalk _walk;
        Renderer[] _renderers;
        Collider[] _colliders;
        VillagePoint _target;
        readonly List<VillagePoint> _workPoints = new List<VillagePoint>();
        readonly List<VillagePoint> _patrol = new List<VillagePoint>();
        int _patrolIndex;
        float _thinkTimer, _idleTimer, _talkTimer;
        bool _visible = true;
        bool _initialized;

        public string Prompt { get { return "Μιλήστε με " + displayName + " (" + RoleName + ")"; } }

        public string RoleName
        {
            get
            {
                switch (role)
                {
                    case VillagerRole.Farmer: return female ? "αγρότισσα" : "αγρότης";
                    case VillagerRole.Merchant: return female ? "έμπορος" : "έμπορος";
                    case VillagerRole.Blacksmith: return "σιδεράς";
                    case VillagerRole.Innkeeper: return female ? "ταβερνιάρισσα" : "ταβερνιάρης";
                    case VillagerRole.Elder: return "γέροντας του χωριού";
                    case VillagerRole.Guard: return "φρουρός";
                    case VillagerRole.Hunter: return female ? "κυνηγός" : "κυνηγός";
                    default: return female ? "χωρική" : "χωρικός";
                }
            }
        }

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _walk = GetComponent<HumanoidWalk>();
            _agent.enabled = false;   // ενεργοποιείται όταν το NavMesh είναι έτοιμο
            _thinkTimer = Random.Range(0f, 0.4f);
        }

        void Start()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _colliders = GetComponentsInChildren<Collider>(true);
            CollectPoints();
            if (DayNightCycle.Instance != null) DayNightCycle.Instance.NightChanged += OnNightChanged;
        }

        void OnDestroy()
        {
            if (DayNightCycle.Instance != null) DayNightCycle.Instance.NightChanged -= OnNightChanged;
        }

        /// <summary>Βρίσκει τα σημεία εργασίας που αντιστοιχούν στον ρόλο του.</summary>
        void CollectPoints()
        {
            PointType[] wanted;
            switch (role)
            {
                case VillagerRole.Merchant: wanted = new[] { PointType.Market }; break;
                case VillagerRole.Blacksmith: wanted = new[] { PointType.Smithy }; break;
                case VillagerRole.Innkeeper: wanted = new[] { PointType.Tavern }; break;
                case VillagerRole.Elder: wanted = new[] { PointType.Temple, PointType.Plaza }; break;
                case VillagerRole.Farmer: wanted = new[] { PointType.Stable, PointType.Market }; break;
                case VillagerRole.Guard: wanted = new[] { PointType.Gate, PointType.Watchtower }; break;
                case VillagerRole.Hunter: wanted = new[] { PointType.Gate, PointType.Plaza }; break;
                default: wanted = new[] { PointType.Plaza, PointType.Market, PointType.Tavern }; break;
            }
            foreach (VillagePoint p in Find.All<VillagePoint>())
            {
                if (System.Array.IndexOf(wanted, p.type) >= 0) _workPoints.Add(p);
                if (p.type == PointType.Patrol) _patrol.Add(p);
            }
            // Σταθερή σειρά περιπολίας (κατά γωνία γύρω από το κέντρο του χωριού)
            _patrol.Sort((a, b) => Mathf.Atan2(a.transform.position.x, a.transform.position.z).CompareTo(Mathf.Atan2(b.transform.position.x, b.transform.position.z)));
        }

        // ================================================================== Πρόγραμμα
        bool WantsToBeOut()
        {
            DayNightCycle dn = DayNightCycle.Instance;
            if (dn == null) return true;
            float h = dn.timeOfDay;
            if (role == VillagerRole.Guard) return true;   // οι φρουροί είναι πάντα σε υπηρεσία (μέρα: πύλη, νύχτα: περιπολία)
            return wakeHour < homeHour ? (h >= wakeHour && h < homeHour) : (h >= wakeHour || h < homeHour);
        }

        bool NightPatrol { get { return role == VillagerRole.Guard && DayNightCycle.Instance != null && DayNightCycle.Instance.IsNight && _patrol.Count > 0; } }

        void OnNightChanged(bool night)
        {
            if (carriedLight != null) carriedLight.enabled = night && _visible;
            if (role == VillagerRole.Guard && _state != State.AtHome && _state != State.Talking) PickNextTarget();
        }

        void Update()
        {
            if (!VillageNavigation.Ready) return;
            if (!_initialized) Initialize();

            if (_state == State.Talking)
            {
                _talkTimer -= Time.deltaTime;
                FacePlayer();
                if (_talkTimer <= 0f) EndTalk();
                return;
            }

            // «Σκέψη» αραιά: κάθε 0.4 s (time-slicing)
            _thinkTimer -= Time.deltaTime;
            if (_thinkTimer > 0f) return;
            _thinkTimer = 0.4f;
            Think(0.4f);
        }

        void Initialize()
        {
            _initialized = true;
            // Τοποθέτηση πάνω στο NavMesh (αν ο κάτοικος στήθηκε λίγο έξω από αυτό)
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 5f, NavMesh.AllAreas))
                transform.position = hit.position;
            _agent.enabled = true;
            _agent.Warp(transform.position);

            if (WantsToBeOut())
            {
                // Ξεκινάμε ήδη στη δουλειά, ώστε το χωριό να είναι «ζωντανό» από την αρχή
                SetVisible(true);
                VillagePoint p = PickWorkPoint();
                if (p != null)
                {
                    Vector3 spot = RandomNear(p.transform.position, 2f);
                    _agent.Warp(spot);
                }
                _state = State.Working;
                _idleTimer = Random.Range(3f, 10f);
            }
            else
            {
                GoInside();
            }
        }

        void Think(float dt)
        {
            bool out_ = WantsToBeOut();
            switch (_state)
            {
                case State.AtHome:
                    if (out_) LeaveHome();
                    break;

                case State.Walking:
                    if (!out_ && _target != home) { GoHome(); break; }
                    if (Arrived())
                    {
                        if (_target == home && !out_) { GoInside(); break; }
                        _state = State.Working;
                        _idleTimer = NightPatrol ? Random.Range(2f, 5f) : Random.Range(6f, 18f);
                        FaceTarget();
                    }
                    break;

                case State.Working:
                    if (!out_) { GoHome(); break; }
                    _idleTimer -= dt;
                    if (_idleTimer <= 0f) PickNextTarget();
                    break;
            }
            UpdateGesture();
        }

        bool Arrived()
        {
            return !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.3f;
        }

        void LeaveHome()
        {
            SetVisible(true);
            _agent.enabled = true;
            if (home != null) _agent.Warp(home.transform.position);
            PickNextTarget();
        }

        void GoHome()
        {
            if (home == null) { GoInside(); return; }
            _target = home;
            _agent.SetDestination(home.transform.position);
            _state = State.Walking;
        }

        void GoInside()
        {
            SetVisible(false);
            _state = State.AtHome;
            _target = null;
        }

        void PickNextTarget()
        {
            if (NightPatrol)
            {
                // Επόμενο σημείο περιπολίας κυκλικά
                _patrolIndex = (_patrolIndex + 1) % _patrol.Count;
                _target = _patrol[_patrolIndex];
                _agent.SetDestination(_target.transform.position);
                _state = State.Walking;
                return;
            }
            VillagePoint p = PickWorkPoint();
            if (p == null) { _idleTimer = 5f; return; }
            _target = p;
            _agent.SetDestination(RandomNear(p.transform.position, workRadius));
            _state = State.Walking;
        }

        VillagePoint PickWorkPoint()
        {
            if (_workPoints.Count == 0) return null;
            return _workPoints[Random.Range(0, _workPoints.Count)];
        }

        /// <summary>Τυχαίο σημείο του NavMesh κοντά σε ένα κέντρο.</summary>
        static Vector3 RandomNear(Vector3 center, float radius)
        {
            for (int i = 0; i < 6; i++)
            {
                Vector2 r = Random.insideUnitCircle * radius;
                NavMeshHit hit;
                if (NavMesh.SamplePosition(center + new Vector3(r.x, 0f, r.y), out hit, 2f, NavMesh.AllAreas))
                    return hit.position;
            }
            return center;
        }

        void FaceTarget()
        {
            if (_target == null) return;
            Vector3 f = _target.transform.forward;
            f.y = 0f;
            if (f.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(f);
        }

        void UpdateGesture()
        {
            if (_walk == null) return;
            Gesture g = Gesture.None;
            if (_state == State.Working && !NightPatrol)
            {
                switch (role)
                {
                    case VillagerRole.Blacksmith: g = Gesture.Hammer; break;
                    case VillagerRole.Merchant: g = Gesture.Sell; break;
                    case VillagerRole.Elder: g = _target != null && _target.type == PointType.Temple ? Gesture.Pray : Gesture.None; break;
                }
            }
            if (_state == State.Talking) g = Gesture.Talk;
            _walk.gesture = g;
        }

        void SetVisible(bool v)
        {
            _visible = v;
            foreach (Renderer r in _renderers) if (r != null) r.enabled = v;
            foreach (Collider c in _colliders) if (c != null) c.enabled = v;
            if (!v && _agent.enabled) _agent.enabled = false;
            if (carriedLight != null) carriedLight.enabled = v && DayNightCycle.Instance != null && DayNightCycle.Instance.IsNight;
        }

        // ================================================================== Διάλογος
        public void Interact(Transform player)
        {
            if (_state == State.AtHome) return;
            if (_state != State.Talking)
            {
                _resume = _state;
                if (_agent.enabled) _agent.isStopped = true;
            }
            _state = State.Talking;
            _talkTimer = 5f;
            UpdateGesture();
            VillageHud.Say(displayName, Line());
        }

        void EndTalk()
        {
            if (_agent.enabled) _agent.isStopped = false;
            _state = _resume;
            UpdateGesture();
        }

        void FacePlayer()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 d = cam.transform.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), Time.deltaTime * 5f);
        }

        string Line()
        {
            DayNightCycle dn = DayNightCycle.Instance;
            bool night = dn != null && dn.IsNight;
            string[] lines;
            switch (role)
            {
                case VillagerRole.Blacksmith:
                    lines = new[] { "Το σίδερο το χτυπάς όσο είναι κόκκινο.", "Φτιάχνω πέταλα, καρφιά και λεπίδες για όλο το χωριό.", "Η φωτιά του καμινιού δεν σβήνει ποτέ πριν το σούρουπο." };
                    break;
                case VillagerRole.Merchant:
                    lines = new[] { "Καλή πραμάτεια, φρέσκια από το πρωί!", "Αλάτι, ύφασμα και ψάρι — ό,τι χρειαστείς.", "Οι έμποροι από τον νότο φέρνουν μπαχαρικά δύο φορές τον χρόνο." };
                    break;
                case VillagerRole.Innkeeper:
                    lines = night ? new[] { "Μείνε για ένα ποτήρι μπύρα, η νύχτα είναι κρύα." } : new[] { "Η ταβέρνα ανοίγει νωρίς και κλείνει αργά.", "Έχουμε ψωμί, τυρί και ζεστό στιφάδο." };
                    break;
                case VillagerRole.Elder:
                    lines = new[] { "Αυτό το χωριό στέκει εδώ εδώ και τρεις γενιές.", "Ο πέτρινος σταυρός στην πλατεία είναι παλαιότερος από όλα τα σπίτια.", "Σεβάσου τον τόπο και θα σε σεβαστεί κι αυτός." };
                    break;
                case VillagerRole.Guard:
                    lines = night ? new[] { "Περιπολώ ως την αυγή. Κράτα τον δρόμο φωτισμένο.", "Τη νύχτα οι πύλες κλείνουν για τους ξένους." } : new[] { "Η πύλη είναι ανοιχτή όσο κρατά το φως.", "Από τη σκοπιά βλέπουμε ως το δάσος." };
                    break;
                case VillagerRole.Hunter:
                    lines = new[] { "Το δάσος βόρεια έχει ελάφια — και λύκους.", "Γυρνάω πάντα πριν πέσει το σκοτάδι." };
                    break;
                case VillagerRole.Farmer:
                    lines = new[] { "Τα ζώα θέλουν φροντίδα πρωί και βράδυ.", "Αν βρέξει απόψε, η σοδειά θα είναι καλή." };
                    break;
                default:
                    lines = new[] { "Καλημέρα, ξένε! Πρώτη φορά στο χωριό μας;", "Στην πλατεία θα βρεις ό,τι χρειάζεσαι.", "Ο σιδεράς δουλεύει από το χάραμα." };
                    break;
            }
            return lines[Random.Range(0, lines.Length)];
        }
    }
}
