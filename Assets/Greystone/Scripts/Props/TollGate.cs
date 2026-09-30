// =====================================================================================
//  TollGate.cs — Λίθινη γέφυρα με μπάρα διοδίων (pontage)
//  ---------------------------------------------------------------------------------
//  * ΓΕΦΥΡΑ: κατάστρωμα, στηθαία, βάθρα με θραυσικύματα (cutwaters) μέσα στο ποτάμι.
//  * ΜΠΑΡΑ ΜΕ ΑΙΣΘΗΤΗΡΑ ΕΓΓΥΤΗΤΑΣ: όταν ο χρήστης μπαίνει στη ζώνη ελέγχου (ProximityZone),
//    η μπάρα σηκώνεται· όταν απομακρυνθεί, κατεβαίνει μετά από λίγο. Ο collider της μπάρας
//    κινείται μαζί της, οπότε η κλειστή μπάρα εμποδίζει πραγματικά τη διέλευση.
//  * Ο εισπράκτορας ανακοινώνει το (ιστορικό) αντίτιμο την πρώτη φορά.
// =====================================================================================
using System.Collections;
using UnityEngine;

namespace Greystone
{
    public class TollGate : MonoBehaviour
    {
        Transform _boom;
        Coroutine _anim;
        bool _announced;
        float _closeDelay;

        /// <summary>Χτίζει τη γέφυρα από xStart έως xEnd (κόσμος) κατά μήκος του δρόμου z=0.</summary>
        public void BuildBridge(float xStart, float xEnd)
        {
            Transform st = Geo.Child(transform, "BridgeStatic", Vector3.zero);
            st.position = new Vector3((xStart + xEnd) * 0.5f, 0, 0);
            float len = xEnd - xStart;
            Material stone = Mats.Get("stone_grey");
            Material rubble = Mats.Get("stone_rubble");
            const float width = 5.6f;
            // Κατάστρωμα (με κεκλιμένα άκρα-ράμπες ώστε να «δένει» με τον δρόμο)
            Geo.Box(st, new Vector3(len, 0.8f, width), new Vector3(0, -0.36f, 0), stone, default(Vector3), true);
            // Στηθαία
            for (int side = -1; side <= 1; side += 2)
            {
                Geo.Box(st, new Vector3(len, 1.0f, 0.4f), new Vector3(0, 0.5f, side * (width * 0.5f - 0.2f)), rubble, default(Vector3), true);
                Geo.Box(st, new Vector3(len + 0.1f, 0.12f, 0.5f), new Vector3(0, 1.05f, side * (width * 0.5f - 0.2f)), stone);
            }
            // Βάθρα με θραυσικύματα (αιχμηρά προς τη ροή του νερού)
            float riverX = WorldLayout.RiverX(0f) - st.position.x;
            for (int k = -1; k <= 1; k += 2)
            {
                float px = riverX + k * 4.2f;
                Geo.Box(st, new Vector3(1.6f, 3.2f, width - 0.4f), new Vector3(px, -2.0f, 0), stone, default(Vector3), true);
                Geo.Cyl(st, 0.95f, 3.2f, new Vector3(px, -2.0f, width * 0.5f - 0.1f), stone, 1f, 4, new Vector3(0, 45, 0));
                Geo.Cyl(st, 0.95f, 3.2f, new Vector3(px, -2.0f, -width * 0.5f + 0.1f), stone, 1f, 4, new Vector3(0, 45, 0));
            }
            // Βάθρα στις όχθες
            Geo.Box(st, new Vector3(2.4f, 3f, width), new Vector3(-len * 0.5f + 1.2f, -1.9f, 0), rubble, default(Vector3), true);
            Geo.Box(st, new Vector3(2.4f, 3f, width), new Vector3(len * 0.5f - 1.2f, -1.9f, 0), rubble, default(Vector3), true);
        }

        /// <summary>Μπάρα διοδίων και οικίσκος. Ο κόμβος (transform) βρίσκεται στο δυτικό άκρο της γέφυρας.</summary>
        public void BuildGate()
        {
            Material wood = Mats.Get("timber");
            // Στύλοι
            Geo.Box(transform, new Vector3(0.3f, 1.4f, 0.3f), new Vector3(0, 0.7f, -2.6f), wood, default(Vector3), true);
            Geo.Box(transform, new Vector3(0.25f, 1.0f, 0.25f), new Vector3(0, 0.5f, 2.6f), wood, default(Vector3), true);
            Geo.Box(transform, new Vector3(0.3f, 0.3f, 0.3f), new Vector3(0, 1.0f, 2.6f), Mats.Get("iron"));
            // Μπάρα: περιστρέφεται γύρω από τον άξονα x στο νότιο στύλο
            _boom = Geo.Child(transform, "Boom", new Vector3(0, 1.2f, -2.6f));
            GameObject beam = Geo.Box(_boom, new Vector3(0.16f, 0.16f, 5.6f), new Vector3(0, 0, 2.8f), Mats.Get("wood"), default(Vector3), true);
            beam.AddComponent<Rigidbody>().isKinematic = true;
            for (int i = 0; i < 4; i++)   // κόκκινες λωρίδες
                Geo.Box(_boom, new Vector3(0.17f, 0.17f, 0.5f), new Vector3(0, 0, 1.0f + i * 1.3f), Mats.Get("cloth_red"));
            Geo.Box(_boom, new Vector3(0.4f, 0.4f, 0.5f), new Vector3(0, 0, -0.4f), Mats.Get("stone_grey"));   // αντίβαρο

            // Οικίσκος εισπράκτορα
            Transform booth = Geo.Child(transform, "TollBooth", new Vector3(-2.8f, 0, -5.4f));
            Geo.Box(booth, new Vector3(2.2f, 2.4f, 2.0f), new Vector3(0, 1.2f, 0), Mats.Get("plaster"), default(Vector3), true);
            Geo.Box(booth, new Vector3(0.9f, 0.8f, 0.06f), new Vector3(0, 1.4f, 1.02f), Mats.Get("window"));
            Geo.PrismPart(booth, new Vector3(2.6f, 1.1f, 2.6f), new Vector3(0, 2.95f, 0), Mats.Get("roof_thatch"));
            Geo.Box(booth, new Vector3(0.6f, 0.06f, 0.4f), new Vector3(0, 1.0f, 1.2f), Mats.Get("wood"));   // πάγκος νομισμάτων

            // Ζώνη αισθητήρα: 12 m κατά μήκος του δρόμου, και από τις δύο πλευρές της μπάρας
            ProximityZone zone = ProximityZone.Create(transform, new Vector3(0, 1f, 0), new Vector3(14f, 4f, 7f));
            zone.Entered += () =>
            {
                SetOpen(true);
                if (!_announced && Hud.Instance != null)
                {
                    _announced = true;
                    Hud.Instance.Notify("Εισπράκτορας: «Διόδια γέφυρας (pontage): μισή πένα για τους πεζούς. Για τη συντήρηση της γέφυρας!»");
                }
            };
            zone.Exited += () => _closeDelay = 2.5f;
        }

        void Update()
        {
            if (_closeDelay > 0f)
            {
                _closeDelay -= Time.deltaTime;
                if (_closeDelay <= 0f) SetOpen(false);
            }
        }

        void SetOpen(bool open)
        {
            _closeDelay = 0f;
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(Anim.RotateLocal(_boom, Quaternion.Euler(open ? -80f : 0f, 0, 0), 1.6f));
            SoundBank.Instance.PlayAt(open ? "creak" : "thud", _boom.position, 0.6f, open ? 0.8f : 1f, 25f);
        }
    }
}
