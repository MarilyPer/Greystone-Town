// =====================================================================================
//  Interactable.cs
//  ---------------------------------------------------------------------------------
//  Γενικό συστατικό αλληλεπίδρασης. Κάθε αντικείμενο με το οποίο ο χρήστης μπορεί να
//  αλληλεπιδράσει (πόρτα, μοχλός, καμπάνα, πινακίδα, χωρικός…) έχει ένα παιδί-αντικείμενο
//  με Interactable + Trigger Collider στο layer "Interact".
//
//  Σχεδιαστικό μοτίβο: Σύνθεση (composition) αντί για κληρονομικότητα. Το Interactable
//  δεν ξέρει τι κάνει το αντικείμενο· απλώς εκθέτει events (Interacted, Scrolled) στα
//  οποία εγγράφεται το εκάστοτε script (Door, Lever...). Το κείμενο οδηγίας (prompt)
//  μπορεί να είναι δυναμικό μέσω delegate (π.χ. «Άνοιξε» / «Κλείσε»).
// =====================================================================================
using System;
using UnityEngine;

namespace Greystone
{
    public class Interactable : MonoBehaviour
    {
        public event Action<PlayerController> Interacted;
        public event Action<int> Scrolled;

        /// <summary>Σταθερό κείμενο οδηγίας, αν δεν οριστεί PromptProvider.</summary>
        public string prompt = "Αλληλεπίδραση";
        /// <summary>Δυναμικό κείμενο οδηγίας (έχει προτεραιότητα).</summary>
        public Func<string> PromptProvider;
        /// <summary>Αν δέχεται ροδέλα ποντικιού (π.χ. ένταση μουσικής).</summary>
        public bool acceptsScroll;
        public bool interactionEnabled = true;

        public string Prompt { get { return PromptProvider != null ? PromptProvider() : prompt; } }

        public void Interact(PlayerController by)
        {
            if (interactionEnabled && Interacted != null) Interacted(by);
        }

        public void Scroll(int step)
        {
            if (acceptsScroll && Scrolled != null) Scrolled(step);
        }

        /// <summary>
        /// Εργοστασιακή μέθοδος (Factory): δημιουργεί ζώνη αλληλεπίδρασης (trigger box) ως παιδί του parent.
        /// </summary>
        public static Interactable Create(Transform parent, Vector3 localCenter, Vector3 size, string promptText)
        {
            GameObject go = new GameObject("Interact");
            go.layer = Geo.LayerInteract;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCenter;
            BoxCollider bc = go.AddComponent<BoxCollider>();
            bc.size = size;
            bc.isTrigger = true;
            Interactable it = go.AddComponent<Interactable>();
            it.prompt = promptText;
            return it;
        }
    }
}
