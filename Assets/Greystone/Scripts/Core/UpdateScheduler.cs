// =====================================================================================
//  UpdateScheduler.cs — Κεντρικός διαχειριστής ενημερώσεων (Update Manager)
//  ---------------------------------------------------------------------------------
//  Δύο τεχνικές βελτιστοποίησης σε ένα:
//
//  1) Update Manager: Το Unity καλεί κάθε μέθοδο Update() μέσω της μηχανής (native ->
//     managed), κάτι που έχει κόστος ανά αντικείμενο. Μία μόνο Update() που καλεί
//     απευθείας (managed -> managed) τις AgentUpdate() όλων είναι φθηνότερη.
//
//  2) LOD λογικής / Time-slicing: Οι agents κοντά στον παίκτη ενημερώνονται σε κάθε
//     frame. Όσο απομακρύνονται, ενημερώνονται κάθε 3 ή κάθε 10 frames. Τα offsets
//     (tickOffset) μοιράζουν το φορτίο ομοιόμορφα ανάμεσα στα frames.
//
//  Η βελτιστοποίηση μπορεί να απενεργοποιηθεί (F6) για σύγκριση στο overlay απόδοσης.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Greystone
{
    public class UpdateScheduler : MonoBehaviour
    {
        static readonly List<Agent> Agents = new List<Agent>(64);

        public static bool ThrottlingEnabled = true;
        public const float NearDistance = 35f;
        public const float MidDistance = 90f;

        /// <summary>Πόσες κλήσεις AgentUpdate έγιναν στο τελευταίο δευτερόλεπτο (για το overlay).</summary>
        public static int UpdatesPerSecond { get; private set; }
        public static int AgentCount { get { return Agents.Count; } }

        int _frame;
        int _counter;
        float _counterTimer;

        public static void Register(Agent a) { if (!Agents.Contains(a)) Agents.Add(a); }
        public static void Unregister(Agent a) { Agents.Remove(a); }
        public static void Clear() { Agents.Clear(); }

        void Update()
        {
            _frame++;
            float dt = Time.deltaTime;
            Camera cam = Camera.main;
            Vector3 camPos = cam != null ? cam.transform.position : Vector3.zero;
            const float near2 = NearDistance * NearDistance;
            const float mid2 = MidDistance * MidDistance;

            // Αντίστροφη διάσχιση: ασφαλής αν κάποιος agent απενεργοποιηθεί κατά την ενημέρωση.
            for (int i = Agents.Count - 1; i >= 0; i--)
            {
                if (i >= Agents.Count) continue;
                Agent a = Agents[i];
                a.tickAccum += dt;

                int interval = 1;
                if (ThrottlingEnabled && cam != null)
                {
                    float d2 = (a.transform.position - camPos).sqrMagnitude;
                    interval = d2 < near2 ? 1 : (d2 < mid2 ? 3 : 10);
                }

                if ((_frame + a.tickOffset) % interval == 0)
                {
                    float acc = a.tickAccum;
                    a.tickAccum = 0f;
                    a.AgentUpdate(acc);
                    _counter++;
                }
            }

            _counterTimer += Time.unscaledDeltaTime;
            if (_counterTimer >= 1f)
            {
                UpdatesPerSecond = _counter;
                _counter = 0;
                _counterTimer -= 1f;
            }
        }
    }
}
