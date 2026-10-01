// =====================================================================================
//  HumanoidWalk.cs — Διαδικαστικό animation για humanoid χαρακτήρες
//  ---------------------------------------------------------------------------------
//  Οι χαρακτήρες του project είναι Humanoid (έχουν Avatar) αλλά δεν υπάρχουν animation
//  clips. Αντί να γλιστρούν σε T-pose, κινούμε τα κόκαλα με κώδικα:
//    * Βάδισμα: οι μηροί ταλαντώνονται μπρος-πίσω, τα γόνατα λυγίζουν στη φάση αιώρησης,
//      τα χέρια κινούνται αντίθετα από τα πόδια, η λεκάνη ανεβοκατεβαίνει ελαφρά.
//      Η συχνότητα των βημάτων εξαρτάται από την ταχύτητα (μήκος διασκελισμού ~1.4 m),
//      άρα τα πόδια δεν «γλιστρούν» στο έδαφος.
//    * Στάση: ανάσα (ελαφριά κίνηση κορμού) και τα χέρια χαλαρά στο πλάι.
//    * Εργασία / ομιλία: χειρονομίες (π.χ. σφυρηλάτηση για τον σιδερά).
//
//  ΑΝΕΞΑΡΤΗΤΟ ΑΠΟ ΤΟΝ ΣΚΕΛΕΤΟ: δεν υποθέτουμε άξονες κοκάλων. Η αρχική (rest) περιστροφή κάθε
//  κοκάλου αποθηκεύεται στον χώρο του χαρακτήρα και οι κινήσεις εφαρμόζονται γύρω από τον
//  άξονα «δεξιά» του ΧΑΡΑΚΤΗΡΑ. Έτσι το script δουλεύει με οποιονδήποτε humanoid σκελετό.
//  Αν το rest pose είναι T-pose, τα χέρια «κατεβαίνουν» αυτόματα (υπολογισμός διόρθωσης).
//
//  Αν ο Animator έχει AnimatorController (π.χ. προσθέσετε animations από Mixamo), το script
//  απλώς στέλνει την παράμετρο «Speed» και αφήνει τα πραγματικά animations να παίξουν.
// =====================================================================================
using UnityEngine;
using UnityEngine.AI;

namespace Greystone
{
    public enum Gesture { None, Talk, Hammer, Sell, Pray }

    public class HumanoidWalk : MonoBehaviour
    {
        [Tooltip("Μήκος διασκελισμού σε μέτρα (πόση απόσταση ανά πλήρη κύκλο βημάτων)")]
        public float strideLength = 1.4f;
        public float legSwing = 28f;
        public float kneeBend = 45f;
        public float armSwing = 18f;

        /// <summary>Τρέχουσα χειρονομία (ορίζεται από τον VillagerAI).</summary>
        [HideInInspector] public Gesture gesture;
        /// <summary>Αν δεν υπάρχει NavMeshAgent, η ταχύτητα δίνεται από έξω.</summary>
        [HideInInspector] public float externalSpeed;

        Animator _animator;
        NavMeshAgent _agent;
        bool _useController;
        static readonly int SpeedHash = Animator.StringToHash("Speed");

        // Κόκαλα
        Transform _hips, _spine, _head;
        Transform _lUpLeg, _lLeg, _rUpLeg, _rLeg;
        Transform _lUpArm, _lArm, _rUpArm, _rArm;
        // Αρχικές περιστροφές στον χώρο του χαρακτήρα
        Quaternion _rHips, _rSpine, _rHead, _rLUpLeg, _rLLeg, _rRUpLeg, _rRLeg, _rLUpArm, _rLArm, _rRUpArm, _rRArm;
        Quaternion _lArmFix = Quaternion.identity, _rArmFix = Quaternion.identity;
        Vector3 _hipsLocalPos;
        Renderer _visibilityRenderer;   // για παράλειψη του animation όταν ο χαρακτήρας δεν φαίνεται
        float _phase, _blend, _gestureBlend, _seed;
        bool _ok;

        void Start()
        {
            _agent = GetComponentInParent<NavMeshAgent>();
            _animator = GetComponentInChildren<Animator>();
            _seed = Random.value * 10f;
            if (_animator == null || !_animator.isHuman) { enabled = false; return; }

            _useController = _animator.runtimeAnimatorController != null;
            if (_useController) return;

            _hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
            _spine = _animator.GetBoneTransform(HumanBodyBones.Spine);
            _head = _animator.GetBoneTransform(HumanBodyBones.Head);
            _lUpLeg = _animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            _lLeg = _animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            _rUpLeg = _animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            _rLeg = _animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            _lUpArm = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            _lArm = _animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            _rUpArm = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            _rArm = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            _ok = _hips && _spine && _lUpLeg && _lLeg && _rUpLeg && _rLeg && _lUpArm && _lArm && _rUpArm && _rArm;
            if (!_ok) { enabled = false; return; }

            // Ο Animator δεν έχει controller: τον απενεργοποιούμε ώστε να μη γράφει ποτέ στα κόκαλα.
            _animator.enabled = false;

            Quaternion inv = Quaternion.Inverse(transform.rotation);
            _rHips = inv * _hips.rotation;
            _rSpine = inv * _spine.rotation;
            if (_head) _rHead = inv * _head.rotation;
            _rLUpLeg = inv * _lUpLeg.rotation; _rLLeg = inv * _lLeg.rotation;
            _rRUpLeg = inv * _rUpLeg.rotation; _rRLeg = inv * _rLeg.rotation;
            _rLUpArm = inv * _lUpArm.rotation; _rLArm = inv * _lArm.rotation;
            _rRUpArm = inv * _rUpArm.rotation; _rRArm = inv * _rArm.rotation;
            _hipsLocalPos = _hips.localPosition;
            _visibilityRenderer = GetComponentInChildren<SkinnedMeshRenderer>();

            // Διόρθωση T-pose: στρέφουμε κάθε χέρι ώστε να δείχνει προς τα κάτω, ελαφρώς στο πλάι.
            _lArmFix = ArmDownFix(_lUpArm, _lArm, inv);
            _rArmFix = ArmDownFix(_rUpArm, _rArm, inv);
        }

        static Quaternion ArmDownFix(Transform upper, Transform lower, Quaternion inv)
        {
            Vector3 dir = inv * (lower.position - upper.position).normalized;   // κατεύθυνση χεριού στον χώρο του χαρακτήρα
            Vector3 want = new Vector3(Mathf.Sign(dir.x) * 0.2f, -1f, 0.04f).normalized;
            return Quaternion.FromToRotation(dir, want);
        }

        float CurrentSpeed()
        {
            if (_agent != null && _agent.enabled) return _agent.velocity.magnitude;
            return externalSpeed;
        }

        void LateUpdate()
        {
            float speed = CurrentSpeed();
            if (_useController)
            {
                _animator.SetFloat(SpeedHash, speed);
                return;
            }

            // ΒΕΛΤΙΣΤΟΠΟΙΗΣΗ: αν ο χαρακτήρας είναι εκτός κάμερας (ή κρυμμένος στο σπίτι του),
            // δεν υπολογίζουμε τα κόκαλα — κανείς δεν θα δει τη διαφορά.
            if (_visibilityRenderer != null && (!_visibilityRenderer.enabled || !_visibilityRenderer.isVisible)) return;

            float dt = Time.deltaTime;
            // Φάση βημάτων: ένας πλήρης κύκλος (δύο βήματα) ανά strideLength μέτρα
            _phase += speed / strideLength * Mathf.PI * 2f * dt;
            _blend = Mathf.MoveTowards(_blend, Mathf.Clamp01(speed / 1.1f), dt * 4f);
            _gestureBlend = Mathf.MoveTowards(_gestureBlend, gesture != Gesture.None && speed < 0.2f ? 1f : 0f, dt * 3f);

            Quaternion root = transform.rotation;
            Vector3 right = Vector3.right;   // άξονας περιστροφής στον χώρο του χαρακτήρα
            float s = Mathf.Sin(_phase), c = Mathf.Cos(_phase);

            // Σειρά: πρώτα οι γονείς (λεκάνη, κορμός) και μετά τα παιδιά (πόδια, χέρια),
            // γιατί η αλλαγή ενός γονέα μετακινεί και όλα τα παιδιά του.
            // ---------------- Λεκάνη & κορμός ----------------
            float bob = Mathf.Abs(c) * 0.03f * _blend;
            _hips.localPosition = _hipsLocalPos + _hips.parent.InverseTransformVector(Vector3.up * bob);
            float breathe = Mathf.Sin(Time.time * 1.6f + _seed) * 1.2f * (1f - _blend);
            float lean = 4f * _blend;
            _hips.rotation = root * Quaternion.AngleAxis(s * 4f * _blend, Vector3.up) * _rHips;
            _spine.rotation = root * Quaternion.AngleAxis(lean + breathe, right) * Quaternion.AngleAxis(-s * 6f * _blend, Vector3.up) * _rSpine;

            // ---------------- Πόδια ----------------
            // Αρνητική γωνία γύρω από το «δεξιά» = το πόδι πάει μπροστά
            float lThigh = -s * legSwing * _blend;
            float rThigh = s * legSwing * _blend;
            float lKnee = Mathf.Max(0f, c) * kneeBend * _blend;     // λυγίζει όταν το πόδι αιωρείται προς τα εμπρός
            float rKnee = Mathf.Max(0f, -c) * kneeBend * _blend;
            _lUpLeg.rotation = root * Quaternion.AngleAxis(lThigh, right) * _rLUpLeg;
            _lLeg.rotation = root * Quaternion.AngleAxis(lThigh + lKnee, right) * _rLLeg;
            _rUpLeg.rotation = root * Quaternion.AngleAxis(rThigh, right) * _rRUpLeg;
            _rLeg.rotation = root * Quaternion.AngleAxis(rThigh + rKnee, right) * _rRLeg;

            // ---------------- Χέρια ----------------
            float lArm = s * armSwing * _blend;
            float rArm = -s * armSwing * _blend;
            float lElbow = -12f, rElbow = -12f;
            ApplyGesture(ref lArm, ref rArm, ref lElbow, ref rElbow);
            _lUpArm.rotation = root * Quaternion.AngleAxis(lArm, right) * _lArmFix * _rLUpArm;
            _lArm.rotation = root * Quaternion.AngleAxis(lArm + lElbow, right) * _lArmFix * _rLArm;
            _rUpArm.rotation = root * Quaternion.AngleAxis(rArm, right) * _rArmFix * _rRUpArm;
            _rArm.rotation = root * Quaternion.AngleAxis(rArm + rElbow, right) * _rArmFix * _rRArm;

            // ---------------- Κεφάλι: ήπια «ματιά» γύρω ----------------
            if (_head)
            {
                float look = Mathf.Sin(Time.time * 0.35f + _seed) * 15f * (1f - _blend);
                _head.rotation = root * Quaternion.AngleAxis(look, Vector3.up) * _rHead;
            }
        }

        /// <summary>Χειρονομίες εργασίας/ομιλίας: αναμειγνύονται ομαλά με το βάδισμα.</summary>
        void ApplyGesture(ref float lArm, ref float rArm, ref float lElbow, ref float rElbow)
        {
            if (_gestureBlend <= 0f) return;
            float t = Time.time + _seed;
            float gl = 0f, gr = 0f, gle = -12f, gre = -12f;
            switch (gesture)
            {
                case Gesture.Talk:     // ζωηρή κίνηση του δεξιού χεριού
                    gr = -35f + Mathf.Sin(t * 3.1f) * 15f; gre = -45f;
                    gl = -10f + Mathf.Sin(t * 1.7f) * 6f;
                    break;
                case Gesture.Hammer:   // σήκωμα αργά, κατέβασμα γρήγορα
                    float cyc = Mathf.Repeat(t, 1.2f) / 1.2f;
                    gr = cyc < 0.7f ? Mathf.Lerp(-20f, -120f, cyc / 0.7f) : Mathf.Lerp(-120f, -20f, (cyc - 0.7f) / 0.3f);
                    gre = -60f; gl = -40f; gle = -50f;
                    break;
                case Gesture.Sell:     // δείχνει την πραμάτεια
                    gr = -50f + Mathf.Sin(t * 1.2f) * 20f; gre = -30f;
                    gl = -25f; gle = -50f;
                    break;
                case Gesture.Pray:
                    gl = gr = -45f; gle = gre = -70f;
                    break;
            }
            lArm = Mathf.Lerp(lArm, gl, _gestureBlend);
            rArm = Mathf.Lerp(rArm, gr, _gestureBlend);
            lElbow = Mathf.Lerp(lElbow, gle, _gestureBlend);
            rElbow = Mathf.Lerp(rElbow, gre, _gestureBlend);
        }
    }
}
