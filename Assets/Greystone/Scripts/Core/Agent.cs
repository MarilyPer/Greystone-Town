// =====================================================================================
//  Agent.cs
//  ---------------------------------------------------------------------------------
//  Βασική κλάση για κάθε «ζωντανό» στοιχείο με λογική (χωρικοί, πρόβατα, κάρο, πουλιά).
//
//  ΒΕΛΤΙΣΤΟΠΟΙΗΣΗ: Οι Agents ΔΕΝ έχουν δική τους μέθοδο Update(). Τους ενημερώνει κεντρικά
//  ο UpdateScheduler (μοτίβο "Update Manager"), ο οποίος επιπλέον αραιώνει τις ενημερώσεις
//  όσων βρίσκονται μακριά από την κάμερα (time-slicing / LOD λογικής). Το dt που λαμβάνει
//  ο agent είναι ο συνολικός χρόνος από την προηγούμενη ενημέρωσή του, άρα η κίνηση μένει
//  σωστή σε ταχύτητα ακόμη κι αν ενημερώνεται σπανιότερα.
// =====================================================================================
using UnityEngine;

namespace Greystone
{
    public abstract class Agent : MonoBehaviour
    {
        [HideInInspector] public float tickAccum;
        [HideInInspector] public int tickOffset;

        protected virtual void OnEnable()
        {
            tickOffset = Random.Range(0, 64);
            UpdateScheduler.Register(this);
        }

        protected virtual void OnDisable()
        {
            UpdateScheduler.Unregister(this);
        }

        /// <summary>Καλείται από τον UpdateScheduler. dt = χρόνος από την τελευταία κλήση.</summary>
        public abstract void AgentUpdate(float dt);
    }
}
