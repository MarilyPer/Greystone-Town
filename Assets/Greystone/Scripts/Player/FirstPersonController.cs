// =====================================================================================
//  FirstPersonController.cs — Παίκτης πρώτου προσώπου
//  ---------------------------------------------------------------------------------
//  * Κίνηση με CharacterController: περπάτημα, τρέξιμο (Shift), άλμα (Space), βαρύτητα 9.81 m/s².
//  * Ματιά με το ποντίκι (περιορισμός ±85°).
//  * Αλληλεπίδραση [E]: ακτίνα (raycast) από το κέντρο της οθόνης· αν χτυπήσει αντικείμενο
//    που υλοποιεί το IInteractable (π.χ. κάτοικο), καλείται η Interact().
//  * Φανάρι χειρός [F] για τις νύχτες, ταχύτητα χρόνου [T].
//
//  Χρησιμοποιεί το ΝΕΟ Input System (το project έχει ενεργό μόνο αυτό) διαβάζοντας απευθείας
//  Keyboard.current / Mouse.current — δεν χρειάζεται αρχείο Input Actions.
//  Τα πλήκτρα αντιστοιχούν σε φυσικές θέσεις, άρα δουλεύουν και με ελληνικό πληκτρολόγιο.
// =====================================================================================
using UnityEngine;
using UnityEngine.InputSystem;

namespace Greystone
{
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        [Header("Κίνηση")]
        public float walkSpeed = 2.4f;
        public float runSpeed = 5.5f;
        public float jumpHeight = 0.9f;
        public float gravity = 9.81f;

        [Header("Ματιά")]
        public Transform cameraPivot;
        public float mouseSensitivity = 0.12f;

        [Header("Αλληλεπίδραση")]
        public float interactDistance = 3.2f;
        public Light lantern;

        public IInteractable Focus { get; private set; }
        public bool InputEnabled = true;

        CharacterController _cc;
        float _pitch, _yaw, _vy;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (cameraPivot == null && Camera.main != null) cameraPivot = Camera.main.transform;
            _yaw = transform.eulerAngles.y;
            if (lantern != null) lantern.enabled = false;
        }

        void OnEnable() { LockCursor(true); }

        public static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void Update()
        {
            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (kb == null) return;

            // Esc: απελευθέρωση/κλείδωμα δρομέα
            if (kb.escapeKey.wasPressedThisFrame) LockCursor(Cursor.lockState != CursorLockMode.Locked);
            bool active = InputEnabled && Cursor.lockState == CursorLockMode.Locked;

            // ---------------- Ματιά ----------------
            if (active && mouse != null)
            {
                Vector2 md = mouse.delta.ReadValue() * mouseSensitivity;
                _yaw += md.x;
                _pitch = Mathf.Clamp(_pitch - md.y, -85f, 85f);
            }
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);

            // ---------------- Κίνηση ----------------
            Vector2 input = Vector2.zero;
            if (active)
            {
                if (kb.wKey.isPressed) input.y += 1f;
                if (kb.sKey.isPressed) input.y -= 1f;
                if (kb.dKey.isPressed) input.x += 1f;
                if (kb.aKey.isPressed) input.x -= 1f;
            }
            if (input.sqrMagnitude > 1f) input.Normalize();
            float speed = active && kb.leftShiftKey.isPressed ? runSpeed : walkSpeed;
            Vector3 move = (transform.forward * input.y + transform.right * input.x) * speed;

            if (_cc.isGrounded)
            {
                _vy = -1f;
                // v = √(2·g·h): η ταχύτητα που χρειάζεται για άλμα ύψους h
                if (active && kb.spaceKey.wasPressedThisFrame) _vy = Mathf.Sqrt(2f * gravity * jumpHeight);
            }
            else _vy -= gravity * Time.deltaTime;
            move.y = _vy;
            _cc.Move(move * Time.deltaTime);

            // ---------------- Αλληλεπίδραση ----------------
            Focus = null;
            Transform cam = cameraPivot != null ? cameraPivot : transform;
            RaycastHit hit;
            if (Physics.Raycast(cam.position, cam.forward, out hit, interactDistance, ~(1 << 2), QueryTriggerInteraction.Collide))
                Focus = hit.collider.GetComponentInParent<IInteractable>();
            if (active && Focus != null && kb.eKey.wasPressedThisFrame) Focus.Interact(transform);

            // ---------------- Λοιπά πλήκτρα ----------------
            if (active && kb.fKey.wasPressedThisFrame && lantern != null) lantern.enabled = !lantern.enabled;
            if (active && kb.tKey.wasPressedThisFrame && DayNightCycle.Instance != null)
            {
                DayNightCycle.Instance.CycleSpeed();
                VillageHud.Notify("Ταχύτητα χρόνου ×" + DayNightCycle.Instance.SpeedMultiplier);
            }

            // Ασφάλεια: αν πέσει κάτω από τον κόσμο
            if (transform.position.y < -50f)
            {
                _cc.enabled = false;
                transform.position = new Vector3(0f, 30f, 0f);
                _cc.enabled = true;
            }
        }
    }
}
