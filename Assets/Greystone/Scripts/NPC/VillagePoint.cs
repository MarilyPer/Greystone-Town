// =====================================================================================
//  VillagePoint.cs — Σημείο ενδιαφέροντος του χωριού
//  ---------------------------------------------------------------------------------
//  Μικρός «δείκτης» στη σκηνή που λέει στους κατοίκους πού μπορούν να πάνε: σπίτια (πόρτες),
//  αγορά, σιδηρουργείο, ταβέρνα, πύλη, σκοπιά, πλατεία, σημεία περιπολίας.
//  Ο VillageSceneBuilder τα δημιουργεί αυτόματα, αλλά μπορείτε να προσθέσετε/μετακινήσετε
//  όσα θέλετε από τον Editor: οι κάτοικοι τα βρίσκουν μόνοι τους κατά την εκκίνηση.
//  Στο Scene view σχεδιάζεται ένα χρωματιστό εικονίδιο (Gizmo) για κάθε τύπο.
// =====================================================================================
using UnityEngine;

namespace Greystone
{
    public enum PointType { Home, Plaza, Market, Smithy, Tavern, Gate, Watchtower, Patrol, Stable, Temple }

    public class VillagePoint : MonoBehaviour
    {
        public PointType type = PointType.Plaza;
        [Tooltip("Για σπίτια: πόσοι κάτοικοι μένουν εδώ")]
        public int capacity = 2;
        [HideInInspector] public int occupants;

        void OnDrawGizmos()
        {
            Color c;
            switch (type)
            {
                case PointType.Home: c = new Color(0.3f, 0.8f, 0.3f); break;
                case PointType.Market: c = new Color(1f, 0.8f, 0.2f); break;
                case PointType.Smithy: c = new Color(1f, 0.4f, 0.1f); break;
                case PointType.Tavern: c = new Color(0.7f, 0.3f, 0.9f); break;
                case PointType.Gate:
                case PointType.Watchtower:
                case PointType.Patrol: c = new Color(0.9f, 0.2f, 0.2f); break;
                default: c = new Color(0.3f, 0.6f, 1f); break;
            }
            Gizmos.color = c;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, 0.5f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.2f);
        }
    }
}
