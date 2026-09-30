// =====================================================================================
//  Well.cs — Πηγάδι με βαρούλκο
//  ---------------------------------------------------------------------------------
//  Αλληλουχία κίνησης (coroutine) όταν ο χρήστης αντλεί νερό:
//    1. η μανιβέλα γυρίζει και ο κουβάς κατεβαίνει μέσα στο πηγάδι (το σχοινί μακραίνει),
//    2. πλατσούρισμα όταν φτάνει στο νερό,
//    3. η μανιβέλα γυρίζει ανάποδα και ο κουβάς ανεβαίνει ΓΕΜΑΤΟΣ (φαίνεται το νερό),
//       ενώ στάζουν σταγόνες (σωματίδια).
//  Η περιστροφή του τυμπάνου είναι συνδεδεμένη με το μήκος του σχοινιού (γωνία = μήκος / ακτίνα),
//  όπως σε ένα πραγματικό βαρούλκο.
// =====================================================================================
using System.Collections;
using UnityEngine;

namespace Greystone
{
    public class Well : MonoBehaviour
    {
        Transform _drum, _bucket, _rope;
        GameObject _water;
        ParticleSystem _drips;
        bool _busy;
        const float DrumY = 2.05f, DrumRadius = 0.13f, BucketTop = 1.2f, BucketBottom = -2.6f;
        public Vector3 DrawSpotWorld { get { return transform.TransformPoint(new Vector3(0, 0, -1.9f)); } }

        public void Build()
        {
            Material stone = Mats.Get("stone_rubble");
            Material wood = Mats.Get("timber");
            Geo.Cyl(transform, 1.15f, 0.95f, new Vector3(0, 0.45f, 0), stone, 1f, 18, default(Vector3), true);
            Geo.Cyl(transform, 1.22f, 0.12f, new Vector3(0, 0.95f, 0), Mats.Get("stone_grey"), 1f, 18);
            Geo.Cyl(transform, 0.9f, 0.02f, new Vector3(0, 0.97f, 0), Mats.Get("dark_interior"), 1f, 18);   // σκοτεινό άνοιγμα
            // Στύλοι, τύμπανο και μανιβέλα
            Geo.Box(transform, new Vector3(0.16f, 2.4f, 0.16f), new Vector3(-1.05f, 1.2f, 0), wood, default(Vector3), true);
            Geo.Box(transform, new Vector3(0.16f, 2.4f, 0.16f), new Vector3(1.05f, 1.2f, 0), wood, default(Vector3), true);
            _drum = Geo.Child(transform, "Drum", new Vector3(0, DrumY, 0));
            Geo.Cyl(_drum, DrumRadius, 1.9f, Vector3.zero, Mats.Get("wood"), 1f, 10, new Vector3(0, 0, 90));
            Geo.Cyl(_drum, 0.16f, 0.25f, new Vector3(0, 0, 0), Mats.Get("rope"), 1f, 10, new Vector3(0, 0, 90));
            Geo.Box(_drum, new Vector3(0.06f, 0.45f, 0.06f), new Vector3(1.2f, 0.2f, 0), Mats.Get("iron"));
            Geo.Box(_drum, new Vector3(0.25f, 0.05f, 0.05f), new Vector3(1.3f, 0.42f, 0), Mats.Get("wood"));
            // Μικρή στέγη
            Geo.PrismPart(transform, new Vector3(1.8f, 0.8f, 2.6f), new Vector3(0, 2.75f, 0), Mats.Get("roof_slate"), new Vector3(0, 90, 0));
            // Κουβάς και σχοινί
            _rope = Geo.Child(transform, "Rope", new Vector3(0, DrumY, 0));
            Geo.Cyl(_rope, 0.012f, 1f, new Vector3(0, -0.5f, 0), Mats.Get("rope"), 1f, 5);
            _bucket = Geo.Child(transform, "Bucket", new Vector3(0, BucketTop, 0));
            Geo.Cyl(_bucket, 0.2f, 0.32f, Vector3.zero, Mats.Get("wood"), 1.15f, 12);
            Geo.Cyl(_bucket, 0.21f, 0.04f, new Vector3(0, 0.1f, 0), Mats.Get("iron"), 1.1f, 12);
            Geo.Beam(_bucket, new Vector3(-0.2f, 0.16f, 0), new Vector3(0, 0.4f, 0), 0.01f, Mats.Get("iron"));
            Geo.Beam(_bucket, new Vector3(0.2f, 0.16f, 0), new Vector3(0, 0.4f, 0), 0.01f, Mats.Get("iron"));
            _water = Geo.Cyl(_bucket, 0.19f, 0.02f, new Vector3(0, 0.12f, 0), Mats.Get("well_water"), 1f, 12);
            _water.SetActive(false);
            _drips = FX.Drips(_bucket, Vector3.zero);
            UpdateRope();

            Interactable it = Interactable.Create(transform, new Vector3(0, 1.2f, 0), new Vector3(2.6f, 2.4f, 2.6f), "");
            it.PromptProvider = () => _busy ? "Ο κουβάς κινείται…" : "Αντλήστε νερό (γυρίστε τη μανιβέλα)";
            it.Interacted += p => Draw(true);
        }

        /// <summary>Άντληση νερού. byPlayer=false όταν αντλεί κάποιος χωρικός (χωρίς μηνύματα).</summary>
        public void Draw(bool byPlayer)
        {
            if (!_busy) StartCoroutine(DrawRoutine(byPlayer));
        }

        IEnumerator DrawRoutine(bool byPlayer)
        {
            _busy = true;
            SoundBank.Instance.PlayAt("ratchet", transform.position + Vector3.up * 2f, 0.7f, 1f, 25f);
            yield return MoveBucket(BucketTop, BucketBottom, 2.2f);
            SoundBank.Instance.PlayAt("splash", transform.position, 0.6f, 1.1f, 20f);
            yield return new WaitForSeconds(0.8f);
            _water.SetActive(true);
            SoundBank.Instance.PlayAt("ratchet", transform.position + Vector3.up * 2f, 0.7f, 0.85f, 25f);
            yield return MoveBucket(BucketBottom, BucketTop, 3.0f);
            _drips.Emit(25);
            if (byPlayer && Hud.Instance != null) Hud.Instance.Notify("Ο κουβάς ανέβηκε γεμάτος με καθαρό, κρύο νερό.");
            yield return new WaitForSeconds(4f);
            _water.SetActive(false);
            _busy = false;
        }

        IEnumerator MoveBucket(float from, float to, float duration)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                float y = Mathf.Lerp(from, to, Anim.Smooth(t));
                _bucket.localPosition = new Vector3(0, y, 0);
                UpdateRope();
                yield return null;
            }
        }

        void UpdateRope()
        {
            float len = DrumY - (_bucket.localPosition.y + 0.4f);
            _rope.localScale = new Vector3(1f, Mathf.Max(0.05f, len), 1f);
            // Περιστροφή τυμπάνου = μήκος σχοινιού / ακτίνα (σε ακτίνια)
            _drum.localRotation = Quaternion.Euler(len / DrumRadius * Mathf.Rad2Deg, 0, 0);
        }
    }
}
