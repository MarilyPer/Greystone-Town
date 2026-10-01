// =====================================================================================
//  Find.cs — Βοηθητική αναζήτηση αντικειμένων σκηνής
//  ---------------------------------------------------------------------------------
//  Στο Unity 6 οι μέθοδοι FindObjectsOfType έχουν αντικατασταθεί από τις ταχύτερες
//  FindObjectsByType (χωρίς ταξινόμηση). Τις «τυλίγουμε» εδώ ώστε ο υπόλοιπος κώδικας να
//  μην εξαρτάται από την έκδοση της μηχανής.
//  ΠΡΟΣΟΧΗ: η αναζήτηση σε όλη τη σκηνή είναι ακριβή — καλείται μόνο κατά την εκκίνηση,
//  ποτέ μέσα σε Update().
// =====================================================================================
using UnityEngine;

namespace Greystone
{
    public static class Find
    {
        public static T[] All<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
            return Object.FindObjectsOfType<T>();
#endif
        }

        public static T First<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<T>();
#else
            return Object.FindObjectOfType<T>();
#endif
        }
    }
}
