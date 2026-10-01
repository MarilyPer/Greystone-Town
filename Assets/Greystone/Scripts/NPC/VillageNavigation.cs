// =====================================================================================
//  VillageNavigation.cs — Πλοηγήσιμη επιφάνεια (NavMesh) του χωριού
//  ---------------------------------------------------------------------------------
//  Οι κάτοικοι κινούνται με NavMeshAgent, δηλαδή βρίσκουν διαδρομές (αλγόριθμος A* της
//  μηχανής) πάνω σε ένα πλέγμα πλοήγησης που αποφεύγει κτίρια, φράχτες και δέντρα.
//
//  Το NavMesh «ψήνεται» (bake) από τον VillageSceneBuilder στον Editor και αποθηκεύεται ως
//  asset. Αν για κάποιο λόγο λείπει (π.χ. αλλάξατε τη σκηνή), ψήνεται αυτόματα κατά την
//  εκκίνηση. Χρησιμοποιούνται οι COLLIDERS ως γεωμετρία (όχι τα meshes): πολύ λιγότερα
//  τρίγωνα, άρα γρηγορότερο bake και καθαρότερο αποτέλεσμα.
// =====================================================================================
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Greystone
{
    [RequireComponent(typeof(NavMeshSurface))]
    public class VillageNavigation : MonoBehaviour
    {
        public static bool Ready { get; private set; }

        void Awake()
        {
            Ready = false;
            NavMeshSurface surface = GetComponent<NavMeshSurface>();
            if (surface.navMeshData == null)
            {
                Configure(surface);
                surface.BuildNavMesh();
            }
            Ready = true;
        }

        /// <summary>Κοινές ρυθμίσεις (χρησιμοποιούνται και από τον Editor builder).</summary>
        public static void Configure(NavMeshSurface surface)
        {
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            // Οι ζώνες αλληλεπίδρασης (triggers) και ο παίκτης δεν συμμετέχουν
            surface.layerMask = ~((1 << 2) | (1 << 8));
        }
    }
}
