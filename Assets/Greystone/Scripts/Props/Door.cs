// =====================================================================================
//  Door.cs — Πόρτα με μεντεσέ
//  ---------------------------------------------------------------------------------
//  Ξύλινη πόρτα με σιδερένιες λάμες που περιστρέφεται γύρω από τον μεντεσέ (pivot στο
//  αριστερό άκρο). Καταστάσεις: κλειδωμένη (χτύπημα + μήνυμα) ή ελεύθερη (άνοιγμα/κλείσιμο
//  με τρίξιμο). Οι χωρικοί την ανοίγουν στιγμιαία όταν μπαίνουν/βγαίνουν από το σπίτι τους.
//  Ο collider της πόρτας κινείται μαζί της (kinematic Rigidbody), οπότε ο παίκτης
//  πράγματι εμποδίζεται από την κλειστή πόρτα και περνά μόνο όταν είναι ανοιχτή.
// =====================================================================================
using System.Collections;
using UnityEngine;

namespace Greystone
{
    public class Door : MonoBehaviour
    {
        public bool locked = true;
        public string lockedMessage = "Η πόρτα είναι κλειδωμένη. Οι ένοικοι δεν περιμένουν επισκέψεις.";
        public bool IsOpen { get; private set; }

        Transform _pivot;
        Coroutine _anim;
        float _openAngle = -95f;

        /// <summary>Κατασκευή πόρτας πλάτους w, ύψους h. Η πόρτα κοιτά προς το τοπικό +Z.</summary>
        public void Build(float w, float h)
        {
            _pivot = Geo.Child(transform, "Hinge", new Vector3(-w * 0.5f, 0f, 0f));
            Material wood = Mats.Get("wood");
            Material iron = Mats.Get("iron");
            GameObject panel = Geo.Box(_pivot, new Vector3(w, h, 0.08f), new Vector3(w * 0.5f, h * 0.5f, 0f), wood, default(Vector3), true);
            Rigidbody rb = panel.AddComponent<Rigidbody>();
            rb.isKinematic = true;   // κινούμενος collider: δηλώνεται kinematic για σωστή/φθηνή φυσική
            // Σιδερένιες λάμες και χερούλι-κρίκος
            Geo.Box(_pivot, new Vector3(w * 0.85f, 0.06f, 0.02f), new Vector3(w * 0.45f, h * 0.22f, 0.05f), iron);
            Geo.Box(_pivot, new Vector3(w * 0.85f, 0.06f, 0.02f), new Vector3(w * 0.45f, h * 0.78f, 0.05f), iron);
            Geo.Cyl(_pivot, 0.06f, 0.015f, new Vector3(w * 0.82f, h * 0.48f, 0.06f), iron, 1f, 8, new Vector3(90, 0, 0));
            // Κάθετες σανίδες (λεπτές γραμμές για λεπτομέρεια)
            for (int i = 1; i < 4; i++)
                Geo.Box(_pivot, new Vector3(0.012f, h * 0.98f, 0.01f), new Vector3(w * i / 4f, h * 0.5f, 0.045f), Mats.Get("timber"));

            Interactable it = Interactable.Create(transform, new Vector3(0f, h * 0.5f, 0.3f), new Vector3(w + 0.2f, h, 0.7f), "");
            it.PromptProvider = () => locked ? "Χτυπήστε την πόρτα" : (IsOpen ? "Κλείστε την πόρτα" : "Ανοίξτε την πόρτα");
            it.Interacted += OnInteract;
        }

        void OnInteract(PlayerController p)
        {
            if (locked)
            {
                SoundBank.Instance.PlayAt("knock", transform.position + Vector3.up, 0.8f);
                if (Hud.Instance != null) Hud.Instance.Notify(lockedMessage);
                return;
            }
            SetOpen(!IsOpen);
        }

        public void SetOpen(bool open)
        {
            if (open == IsOpen) return;
            IsOpen = open;
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(Anim.RotateLocal(_pivot, Quaternion.Euler(0f, open ? _openAngle : 0f, 0f), 1.1f));
            SoundBank.Instance.PlayAt("creak", transform.position + Vector3.up, 0.5f, Random.Range(0.9f, 1.1f), 20f);
        }

        /// <summary>Άνοιγμα για λίγα δευτερόλεπτα (χρησιμοποιείται από τους χωρικούς).</summary>
        public void OpenBriefly(float seconds = 2.5f)
        {
            if (!isActiveAndEnabled) return;
            StartCoroutine(BriefRoutine(seconds));
        }

        IEnumerator BriefRoutine(float s)
        {
            bool wasOpen = IsOpen;
            SetOpen(true);
            yield return new WaitForSeconds(s);
            if (!wasOpen) SetOpen(false);
        }
    }
}
