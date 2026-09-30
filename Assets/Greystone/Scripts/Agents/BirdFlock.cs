// =====================================================================================
//  BirdFlock.cs — Σμήνος πουλιών (Boids) με GPU instancing
//  ---------------------------------------------------------------------------------
//  Υλοποίηση των τριών κανόνων του Craig Reynolds (1987):
//    1. Διαχωρισμός (separation): αποφυγή πολύ κοντινών γειτόνων,
//    2. Ευθυγράμμιση (alignment): ταίριασμα της κατεύθυνσης με τους γείτονες,
//    3. Συνοχή (cohesion): κίνηση προς το κέντρο του σμήνους,
//  συν μια δύναμη «έλξης» προς έναν στόχο που κάνει αργούς κύκλους πάνω από το χωριό.
//
//  Όλα τα πουλιά σχεδιάζονται με ΜΙΑ κλήση Graphics.DrawMeshInstanced· το φτερούγισμα
//  γίνεται στη GPU (Bird shader). Τη νύχτα τα πουλιά «κουρνιάζουν» (δεν σχεδιάζονται).
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Greystone
{
    public class BirdFlock : MonoBehaviour
    {
        const int Count = 26;
        Vector3[] _pos = new Vector3[Count];
        Vector3[] _vel = new Vector3[Count];
        Matrix4x4[] _matrices = new Matrix4x4[Count];
        Mesh _mesh;
        Material _mat;
        Vector3 _center = new Vector3(0, 32, 0);

        public void Build()
        {
            _mesh = BuildBirdMesh();
            _mat = Mats.Get("bird");
            for (int i = 0; i < Count; i++)
            {
                _pos[i] = _center + Random.insideUnitSphere * 12f;
                _vel[i] = Random.onUnitSphere * 5f;
                _vel[i].y *= 0.2f;
            }
        }

        static Mesh BuildBirdMesh()
        {
            // Σώμα (λεπτό ρόμβος) + δύο φτερά-τρίγωνα που εκτείνονται στον άξονα x
            Vector3[] v =
            {
                new Vector3(0, 0, 0.28f), new Vector3(0.05f, 0, 0), new Vector3(0, 0, -0.22f), new Vector3(-0.05f, 0, 0), new Vector3(0, 0.05f, 0),
                new Vector3(0.04f, 0, 0.08f), new Vector3(0.45f, 0, -0.02f), new Vector3(0.04f, 0, -0.08f),
                new Vector3(-0.04f, 0, 0.08f), new Vector3(-0.45f, 0, -0.02f), new Vector3(-0.04f, 0, -0.08f),
            };
            int[] t = { 0, 4, 1, 1, 4, 2, 2, 4, 3, 3, 4, 0, 5, 6, 7, 8, 10, 9 };
            Mesh m = new Mesh();
            m.name = "Bird";
            m.vertices = v;
            m.triangles = t;
            m.RecalculateNormals();
            m.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            return m;
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            // Τη νύχτα τα πουλιά δεν πετούν
            if (TimeManager.Instance != null && TimeManager.Instance.Daylight < 0.25f) return;

            // Στόχος: αργός κύκλος (Lissajous) πάνω από το χωριό
            float t = Time.time * 0.05f;
            Vector3 goal = new Vector3(Mathf.Sin(t) * 55f, 30f + Mathf.Sin(t * 2.3f) * 8f, Mathf.Cos(t * 0.8f) * 45f);

            Vector3 center = Vector3.zero, avgVel = Vector3.zero;
            for (int i = 0; i < Count; i++) { center += _pos[i]; avgVel += _vel[i]; }
            center /= Count;
            avgVel /= Count;

            for (int i = 0; i < Count; i++)
            {
                Vector3 sep = Vector3.zero;
                for (int j = 0; j < Count; j++)
                {
                    if (i == j) continue;
                    Vector3 d = _pos[i] - _pos[j];
                    float m2 = d.sqrMagnitude;
                    if (m2 < 4f && m2 > 0.0001f) sep += d / m2;      // ισχυρότερη απώθηση σε μικρότερη απόσταση
                }
                Vector3 acc = sep * 3f
                            + (center - _pos[i]) * 0.25f          // συνοχή
                            + (avgVel - _vel[i]) * 0.6f           // ευθυγράμμιση
                            + (goal - _pos[i]).normalized * 2.2f;  // στόχος
                // Να μην πέφτουν χαμηλά (έδαφος/λόφοι)
                float ground = WorldLayout.HeightAt(_pos[i].x, _pos[i].z);
                if (_pos[i].y < ground + 15f) acc.y += 6f;

                _vel[i] += acc * dt;
                float sp = _vel[i].magnitude;
                _vel[i] = _vel[i] / Mathf.Max(sp, 0.001f) * Mathf.Clamp(sp, 5f, 11f);
                _pos[i] += _vel[i] * dt;
                _matrices[i] = Matrix4x4.TRS(_pos[i], Quaternion.LookRotation(_vel[i]), Vector3.one * 1.4f);
            }
            Graphics.DrawMeshInstanced(_mesh, 0, _mat, _matrices, Count, null, ShadowCastingMode.Off, false, 0, null, LightProbeUsage.Off);
        }
    }
}
