// =====================================================================================
//  ProximityZone.cs — Ζώνη εγγύτητας (αισθητήρας)
//  ---------------------------------------------------------------------------------
//  Ενεργοποιεί events όταν ο παίκτης μπαίνει/βγαίνει από έναν όγκο (π.χ. μπάρα διοδίων,
//  εσωτερικό ταβέρνας). Αντί για physics triggers χρησιμοποιούμε απλό έλεγχο «σημείο μέσα
//  σε προσανατολισμένο κουτί» 10 φορές το δευτερόλεπτο: ελάχιστο κόστος, ντετερμινιστική
//  συμπεριφορά και καμία εξάρτηση από Rigidbodies.
// =====================================================================================
using System;
using UnityEngine;

namespace Greystone
{
    public class ProximityZone : MonoBehaviour
    {
        public Vector3 size = Vector3.one;
        public event Action Entered;
        public event Action Exited;
        public bool Inside { get; private set; }
        float _timer;

        public static ProximityZone Create(Transform parent, Vector3 localCenter, Vector3 size)
        {
            GameObject go = new GameObject("ProximityZone");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCenter;
            ProximityZone z = go.AddComponent<ProximityZone>();
            z.size = size;
            return z;
        }

        void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = 0.1f;
            PlayerController p = PlayerController.Instance;
            if (p == null) return;
            // Μετατροπή της θέσης του παίκτη στο τοπικό σύστημα της ζώνης -> έλεγχος AABB
            Vector3 local = transform.InverseTransformPoint(p.transform.position + Vector3.up * 0.9f);
            Vector3 h = size * 0.5f;
            bool inside = Mathf.Abs(local.x) < h.x && Mathf.Abs(local.y) < h.y && Mathf.Abs(local.z) < h.z;
            if (inside != Inside)
            {
                Inside = inside;
                if (inside) { if (Entered != null) Entered(); }
                else if (Exited != null) Exited();
            }
        }
    }
}
