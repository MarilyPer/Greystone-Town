// =====================================================================================
//  LightLod.cs — «LOD» σημειακών φώτων
//  ---------------------------------------------------------------------------------
//  Στο Forward rendering του Built-in pipeline, κάθε σημειακό φως (per-pixel) που αγγίζει
//  ένα αντικείμενο προσθέτει ΕΝΑ ΑΚΟΜΗ ΠΕΡΑΣΜΑ σχεδίασης (draw call) για αυτό. Τα φανάρια
//  του χωριού είναι πολλά· όσα είναι μακριά από τον παίκτη συνεισφέρουν ελάχιστα οπτικά.
//
//  Γι' αυτό, 4 φορές το δευτερόλεπτο (όχι σε κάθε frame), ενεργοποιούμε μόνο τα φώτα που
//  βρίσκονται σε απόσταση < MaxDistance από την κάμερα και είναι «αναμμένα» από το
//  συστατικό τους (π.χ. τη νύχτα). Τα υπόλοιπα κρατούν μόνο την εκπομπή του υλικού τους,
//  άρα από μακριά εξακολουθούν να «λάμπουν».
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Greystone
{
    public class LightLod : MonoBehaviour
    {
        class Entry { public Light light; public bool wanted; public float baseRange; }

        static readonly List<Entry> Entries = new List<Entry>();
        public static float MaxDistance = 55f;
        public static int ActiveCount { get; private set; }
        public static int TotalCount { get { return Entries.Count; } }

        float _timer;

        public static void Register(Light l)
        {
            Entries.Add(new Entry { light = l, wanted = false, baseRange = l.range });
            l.enabled = false;
        }

        /// <summary>Το συστατικό δηλώνει αν «θέλει» το φως αναμμένο (π.χ. νύχτα).</summary>
        public static void SetWanted(Light l, bool wanted)
        {
            for (int i = 0; i < Entries.Count; i++)
                if (Entries[i].light == l) { Entries[i].wanted = wanted; return; }
        }

        void OnDestroy() { Entries.Clear(); }

        void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = 0.25f;

            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 c = cam.transform.position;
            float max2 = MaxDistance * MaxDistance;
            int active = 0;
            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                Entry e = Entries[i];
                if (e.light == null) { Entries.RemoveAt(i); continue; }
                bool on = e.wanted && (e.light.transform.position - c).sqrMagnitude < max2;
                if (e.light.enabled != on) e.light.enabled = on;
                if (on) active++;
            }
            ActiveCount = active;
        }
    }
}
