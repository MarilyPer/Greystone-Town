// =====================================================================================
//  GInput.cs — Ενιαίο στρώμα εισόδου (Adapter pattern)
//  ---------------------------------------------------------------------------------
//  Το Unity έχει δύο συστήματα εισόδου: το παλιό Input Manager και το νέο Input System
//  (package). Τα νέα projects του Unity 6 έχουν ενεργό το νέο. Για να λειτουργεί το project
//  σε κάθε περίπτωση, όλος ο υπόλοιπος κώδικας ρωτά ΜΟΝΟ την GInput, η οποία με
//  conditional compilation (#if) χρησιμοποιεί όποιο σύστημα είναι διαθέσιμο.
//
//  Χρησιμοποιούμε φυσικά πλήκτρα (θέσεις πλήκτρων), άρα τα W/A/S/D δουλεύουν και με
//  ελληνική διάταξη πληκτρολογίου.
// =====================================================================================
using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace Greystone
{
    /// <summary>Λογικά πλήκτρα του παιχνιδιού (ανεξάρτητα από το σύστημα εισόδου).</summary>
    public enum GKey
    {
        Forward, Back, Left, Right, Run, Jump, Interact, Lantern,
        Map, Help, Perf, ToggleLod, ToggleCullDist, ToggleThrottle, TimeSpeed, Weather, Pause, Crouch
    }

    public static class GInput
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        // ---------------------------------------------------------- Νέο Input System
        static Key Map(GKey k)
        {
            switch (k)
            {
                case GKey.Forward: return Key.W;
                case GKey.Back: return Key.S;
                case GKey.Left: return Key.A;
                case GKey.Right: return Key.D;
                case GKey.Run: return Key.LeftShift;
                case GKey.Jump: return Key.Space;
                case GKey.Interact: return Key.E;
                case GKey.Lantern: return Key.F;
                case GKey.Map: return Key.M;
                case GKey.Help: return Key.H;
                case GKey.Perf: return Key.F3;
                case GKey.ToggleLod: return Key.F4;
                case GKey.ToggleCullDist: return Key.F5;
                case GKey.ToggleThrottle: return Key.F6;
                case GKey.TimeSpeed: return Key.T;
                case GKey.Weather: return Key.R;
                case GKey.Pause: return Key.Escape;
                case GKey.Crouch: return Key.LeftCtrl;
            }
            return Key.None;
        }

        public static bool Held(GKey k) { return Keyboard.current != null && Keyboard.current[Map(k)].isPressed; }
        public static bool Down(GKey k) { return Keyboard.current != null && Keyboard.current[Map(k)].wasPressedThisFrame; }
        public static bool KeyDown(int digit)
        {
            if (Keyboard.current == null) return false;
            Key key = (Key)((int)Key.Digit1 + digit - 1);
            return Keyboard.current[key].wasPressedThisFrame;
        }

        public static Vector2 MouseDelta
        {
            // Το νέο σύστημα δίνει pixels· κλιμακώνουμε ώστε να ταιριάζει με το παλιό (Mouse X/Y).
            get { return Mouse.current != null ? Mouse.current.delta.ReadValue() * 0.05f : Vector2.zero; }
        }

        public static float Scroll
        {
            get { return Mouse.current != null ? Mathf.Sign(Mouse.current.scroll.ReadValue().y) * (Mathf.Abs(Mouse.current.scroll.ReadValue().y) > 0.01f ? 1f : 0f) : 0f; }
        }

        public static bool MouseLeftDown { get { return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame; } }
#else
        // ---------------------------------------------------------- Παλιό Input Manager
        static KeyCode Map(GKey k)
        {
            switch (k)
            {
                case GKey.Forward: return KeyCode.W;
                case GKey.Back: return KeyCode.S;
                case GKey.Left: return KeyCode.A;
                case GKey.Right: return KeyCode.D;
                case GKey.Run: return KeyCode.LeftShift;
                case GKey.Jump: return KeyCode.Space;
                case GKey.Interact: return KeyCode.E;
                case GKey.Lantern: return KeyCode.F;
                case GKey.Map: return KeyCode.M;
                case GKey.Help: return KeyCode.H;
                case GKey.Perf: return KeyCode.F3;
                case GKey.ToggleLod: return KeyCode.F4;
                case GKey.ToggleCullDist: return KeyCode.F5;
                case GKey.ToggleThrottle: return KeyCode.F6;
                case GKey.TimeSpeed: return KeyCode.T;
                case GKey.Weather: return KeyCode.R;
                case GKey.Pause: return KeyCode.Escape;
                case GKey.Crouch: return KeyCode.LeftControl;
            }
            return KeyCode.None;
        }

        public static bool Held(GKey k) { return Input.GetKey(Map(k)); }
        public static bool Down(GKey k) { return Input.GetKeyDown(Map(k)); }
        public static bool KeyDown(int digit) { return Input.GetKeyDown(KeyCode.Alpha0 + digit); }

        public static Vector2 MouseDelta
        {
            get { return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")); }
        }

        public static float Scroll
        {
            get
            {
                float y = Input.mouseScrollDelta.y;
                return Mathf.Abs(y) > 0.01f ? Mathf.Sign(y) : 0f;
            }
        }

        public static bool MouseLeftDown { get { return Input.GetMouseButtonDown(0); } }
#endif
        public static Vector2 MoveAxis
        {
            get
            {
                Vector2 v = Vector2.zero;
                if (Held(GKey.Forward)) v.y += 1f;
                if (Held(GKey.Back)) v.y -= 1f;
                if (Held(GKey.Right)) v.x += 1f;
                if (Held(GKey.Left)) v.x -= 1f;
                return v.sqrMagnitude > 1f ? v.normalized : v;
            }
        }
    }
}
