// =====================================================================================
//  Outfit.cs — Ενδυμασία κατοίκου (για τους modular χαρακτήρες SopaDopa)
//  ---------------------------------------------------------------------------------
//  Οι χαρακτήρες «Constructor» περιέχουν δεκάδες κομμάτια ρούχων/πανοπλίας ως παιδιά-meshes.
//  Ανάλογα με τον ρόλο του κατοίκου ενεργοποιούμε ένα ταιριαστό σύνολο και κρύβουμε τα υπόλοιπα.
//  Έτσι ΕΝΑ prefab δίνει πολλούς διαφορετικούς, αναγνωρίσιμους χαρακτήρες.
//  Τα σώματα (Body_*) και ο σκελετός (game.rig / DEF-*) δεν πειράζονται ποτέ.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Greystone
{
    public enum VillagerRole { Peasant, Farmer, Merchant, Blacksmith, Innkeeper, Elder, Guard, Hunter }

    public class Outfit : MonoBehaviour
    {
        public VillagerRole role;
        [Tooltip("Σπόρος τυχαιότητας για παραλλαγές (π.χ. κουκούλα, κάπα)")]
        public int seed;

        static readonly Dictionary<VillagerRole, string[]> Sets = new Dictionary<VillagerRole, string[]>
        {
            { VillagerRole.Peasant,    new[] { "Cloth_tunic", "Cloth_pants", "Cloth_belt", "Lether_boot(left)", "Lether_boot(right)" } },
            { VillagerRole.Farmer,     new[] { "Cloth_tunic", "Cloth_pants", "Cloth_belt_cloth", "Feet_bandages(left)", "Feet_bandages(right)", "Hood_Cloth_1" } },
            { VillagerRole.Merchant,   new[] { "Lether_tunic", "Lether_pants", "Lether_belt", "Lether_boot(left)", "Lether_boot(right)", "Cloth_mantle" } },
            { VillagerRole.Blacksmith, new[] { "Lether_tunic", "Cloth_pants", "Lether_wide_belt", "Lether_boot(left)", "Lether_boot(right)", "Lether_bracer_1(left)", "Lether_bracer_1(right)" } },
            { VillagerRole.Innkeeper,  new[] { "Cloth_tunic", "Lether_pants", "CLoth_wide_Belt_1", "Lether_boot(left)", "Lether_boot(right)", "Scarf" } },
            { VillagerRole.Elder,      new[] { "Robe", "Cloth_belt", "Hood_down", "Feet_bandages(left)", "Feet_bandages(right)" } },
            { VillagerRole.Guard,      new[] { "Chain_Mail_Chest", "Chain_Mail_Pants", "Steel_cuirass_1", "Steel_boot(left)", "Steel_boot(right)", "Steel_Healm_1", "Steel_pauldron_1(left)", "Steel_pauldron_1(right)", "Lether_belt", "Cape_1" } },
            { VillagerRole.Hunter,     new[] { "Feathered_jacket", "Lether_pants", "Lether_boot(left)", "Lether_boot(right)", "Hood_cloth_2", "Leather_chest_belt_2" } },
        };

        // Προαιρετικά αξεσουάρ που προστίθενται τυχαία για ποικιλία
        static readonly string[] Extras = { "Scarf", "Hood_down", "Cape_2", "Leather_arms_strap_1(left)", "Cloth_bracer(left)", "Cloth_bracer(right)" };

        void Awake() { Apply(); }

        public void Apply()
        {
            HashSet<string> wanted = new HashSet<string>(Sets[role]);
            System.Random r = new System.Random(seed);
            if (role != VillagerRole.Guard && r.NextDouble() < 0.4) wanted.Add(Extras[r.Next(Extras.Length)]);

            // Όλα τα κομμάτια-meshes (και τα ανενεργά), όπου κι αν βρίσκονται στην ιεραρχία
            foreach (Renderer rend in GetComponentsInChildren<Renderer>(true))
            {
                string n = rend.gameObject.name;
                if (n.StartsWith("Body_")) { rend.gameObject.SetActive(true); continue; }   // το σώμα μένει πάντα ορατό
                rend.gameObject.SetActive(wanted.Contains(n));
            }
        }
    }
}
