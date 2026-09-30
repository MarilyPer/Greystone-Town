// =====================================================================================
//  PlayerController.cs — Χειριστής πρώτου προσώπου
//  ---------------------------------------------------------------------------------
//  * Κίνηση με CharacterController (περπάτημα, τρέξιμο, άλμα, βαρύτητα 9.81 m/s²,
//    ρεαλιστικές ταχύτητες: γρήγορο περπάτημα ~2.2 m/s, τρέξιμο ~5.2 m/s).
//  * Ματιά με το ποντίκι (περιορισμός κλίσης ±85°), «κούνημα κεφαλιού» (head bob) στο βάδισμα.
//  * Βήματα με ήχο ανάλογα με την επιφάνεια (γρασίδι / πέτρα / ξύλο / νερό).
//  * Αλληλεπίδραση: μία ακτίνα (raycast) από το κέντρο της οθόνης. Χρησιμοποιούμε μάσκα
//    layers (Default + Interact) ώστε να ελέγχονται μόνο τα σχετικά colliders και οι τοίχοι
//    να «κρύβουν» αντικείμενα πίσω τους.
//  * Φανάρι χειρός (F) για τις νύχτες.
//  * Στο νερό η κίνηση επιβραδύνεται (αληθοφάνεια).
// =====================================================================================
using UnityEngine;

namespace Greystone
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }

        public float walkSpeed = 2.2f;
        public float runSpeed = 5.2f;
        public float jumpSpeed = 4.2f;            // -> ύψος άλματος ≈ v²/2g ≈ 0.9 m
        public float gravity = 9.81f;
        public float eyeHeight = 1.65f;
        public float interactDistance = 3.2f;

        public static float MouseSensitivity = 2.2f;

        public Camera Cam { get; private set; }
        public bool InputEnabled = true;
        public bool Indoors { get; set; }
        public Interactable Focus { get; private set; }
        public bool InWater { get; private set; }

        CharacterController _cc;
        Transform _head;
        float _pitch, _yaw;
        float _vy;
        float _bobPhase, _stepDistance;
        Light _lantern;
        int _interactMask;
        Vector3 _lastPos;

        public void Init(Vector3 pos, float yaw)
        {
            Instance = this;
            gameObject.layer = Geo.LayerPlayer;
            _cc = GetComponent<CharacterController>();
            _cc.height = 1.8f;
            _cc.radius = 0.32f;
            _cc.center = new Vector3(0f, 0.9f, 0f);
            _cc.stepOffset = 0.4f;
            _cc.slopeLimit = 48f;
            _cc.skinWidth = 0.04f;

            _head = Geo.Child(transform, "Head", new Vector3(0f, eyeHeight, 0f));
            GameObject camGo = new GameObject("PlayerCamera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(_head, false);
            Cam = camGo.AddComponent<Camera>();
            Cam.nearClipPlane = 0.08f;
            Cam.farClipPlane = 1400f;
            Cam.fieldOfView = 70f;
            Cam.clearFlags = CameraClearFlags.Skybox;
            camGo.AddComponent<AudioListener>();
            ApplyLayerCullDistances(true);

            // Φανάρι χειρός: θερμό σημειακό φως λίγο δεξιά/κάτω από την κάμερα.
            _lantern = FX.PointLight(_head, new Vector3(0.35f, -0.35f, 0.3f), new Color(1f, 0.72f, 0.4f), 9f, 1.4f);
            _lantern.enabled = false;

            _interactMask = (1 << Geo.LayerDefault) | (1 << Geo.LayerInteract);
            MouseSensitivity = PlayerPrefs.GetFloat("gs_mouse", 2.2f);
            Teleport(pos, yaw);
        }

        /// <summary>
        /// ΒΕΛΤΙΣΤΟΠΟΙΗΣΗ: απόσταση αποκοπής ανά layer. Τα μικρά αντικείμενα (layer SmallProps:
        /// βαρέλια, κασόνια, φρούτα, ταφόπλακες…) δεν σχεδιάζονται πέρα από 70 m, όπου
        /// ούτως ή άλλως καταλαμβάνουν ελάχιστα pixels. Το υπόλοιπο σκηνικό έχει far plane 1400 m.
        /// </summary>
        public void ApplyLayerCullDistances(bool enabled)
        {
            float[] d = new float[32];
            if (enabled) d[Geo.LayerSmallProps] = 70f;
            Cam.layerCullDistances = d;
            Cam.layerCullSpherical = true;
        }

        public void Teleport(Vector3 pos, float yaw)
        {
            _cc.enabled = false;
            transform.position = pos;
            _cc.enabled = true;
            _yaw = yaw;
            _pitch = 0f;
            _vy = 0f;
            _lastPos = pos;
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        }

        public void ToggleLantern()
        {
            _lantern.enabled = !_lantern.enabled;
            SoundBank.Instance.PlayUI("clunk", 0.3f);
        }

        public bool LanternOn { get { return _lantern != null && _lantern.enabled; } }

        void Update()
        {
            if (_cc == null) return;
            float dt = Time.deltaTime;

            // ---------------- Ματιά ----------------
            if (InputEnabled)
            {
                Vector2 md = GInput.MouseDelta * MouseSensitivity;
                _yaw += md.x;
                _pitch = Mathf.Clamp(_pitch - md.y, -85f, 85f);
            }
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);

            // ---------------- Κίνηση ----------------
            InWater = transform.position.y < WorldLayout.WaterLevel - 0.25f;
            Vector2 input = InputEnabled ? GInput.MoveAxis : Vector2.zero;
            bool running = InputEnabled && GInput.Held(GKey.Run);
            float speed = running ? runSpeed : walkSpeed;
            if (InWater) speed *= 0.45f;
            Vector3 move = (transform.forward * input.y + transform.right * input.x) * speed;

            if (_cc.isGrounded)
            {
                _vy = -1f;   // μικρή προς τα κάτω ταχύτητα κρατά τον παίκτη «κολλημένο» στις κατηφόρες
                if (InputEnabled && GInput.Down(GKey.Jump) && !InWater) _vy = jumpSpeed;
            }
            else
            {
                _vy -= gravity * dt;
            }
            move.y = _vy;
            _cc.Move(move * dt);

            // ---------------- Head bob & βήματα ----------------
            Vector3 delta = transform.position - _lastPos;
            delta.y = 0f;
            float moved = delta.magnitude;
            _lastPos = transform.position;
            if (_cc.isGrounded && moved > 0.0005f)
            {
                _bobPhase += moved * (running ? 1.9f : 2.6f);
                _stepDistance += moved;
                float stride = running ? 1.5f : 0.8f;
                if (_stepDistance > stride)
                {
                    _stepDistance = 0f;
                    PlayFootstep(running);
                }
            }
            float bob = Mathf.Sin(_bobPhase * Mathf.PI) * 0.035f * Mathf.Clamp01(moved / (dt * walkSpeed + 0.0001f));
            _head.localPosition = new Vector3(0f, eyeHeight + bob, 0f);
            _head.localRotation = Quaternion.Euler(_pitch, 0f, 0f);

            // Ασφάλεια: αν πέσει κάτω από τον κόσμο, επιστροφή στην αρχή.
            if (transform.position.y < -40f) Teleport(WorldLayout.OnGround(WorldLayout.Spawn.x, WorldLayout.Spawn.z, 0.5f), 0f);

            // ---------------- Αλληλεπίδραση ----------------
            UpdateFocus();
            if (InputEnabled && Focus != null)
            {
                if (GInput.Down(GKey.Interact)) Focus.Interact(this);
                float sc = GInput.Scroll;
                if (sc != 0f) Focus.Scroll((int)sc);
            }
            if (InputEnabled && GInput.Down(GKey.Lantern)) ToggleLantern();
        }

        void UpdateFocus()
        {
            Focus = null;
            RaycastHit hit;
            Ray ray = new Ray(Cam.transform.position, Cam.transform.forward);
            if (Physics.Raycast(ray, out hit, interactDistance, _interactMask, QueryTriggerInteraction.Collide))
            {
                Interactable it = hit.collider.GetComponent<Interactable>();
                if (it != null && it.interactionEnabled) Focus = it;
            }
        }

        void PlayFootstep(bool running)
        {
            Vector3 p = transform.position;
            string clip;
            if (InWater) clip = "splash";
            else if (Indoors) clip = "step_wood";
            else
            {
                Vector2 p2 = new Vector2(p.x, p.z);
                bool paved = Vector2.Distance(p2, WorldLayout.PlazaCenter) < WorldLayout.PlazaRadius
                             || (Mathf.Abs(p.x - WorldLayout.RiverX(p.z)) < 16f && Mathf.Abs(p.z) < 3.5f);   // γέφυρα
                bool road = WorldLayout.DistanceToRoads(p2) < WorldLayout.RoadHalfWidth;
                clip = paved ? "step_stone" : (road ? "step_stone" : "step_grass");
            }
            float vol = (running ? 0.4f : 0.25f) * (clip == "splash" ? 0.5f : 1f);
            SoundBank.Instance.PlayAt(clip, p, vol, Random.Range(0.85f, 1.15f), 15f);
        }
    }
}
