// =====================================================================================
//  Anim.cs — Μικρή βιβλιοθήκη «tweens» με coroutines
//  ---------------------------------------------------------------------------------
//  Ομαλές μεταβάσεις (πόρτες, μοχλοί, μπάρες) χωρίς εξωτερικά πακέτα. Χρησιμοποιείται
//  καμπύλη smoothstep (επιτάχυνση-επιβράδυνση), που δίνει φυσικότερη αίσθηση βάρους από
//  τη γραμμική παρεμβολή.
// =====================================================================================
using System.Collections;
using UnityEngine;

namespace Greystone
{
    public static class Anim
    {
        public static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        public static IEnumerator RotateLocal(Transform tr, Quaternion to, float duration)
        {
            Quaternion from = tr.localRotation;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(duration, 0.0001f);
                tr.localRotation = Quaternion.Slerp(from, to, Smooth(t));
                yield return null;
            }
        }

        public static IEnumerator MoveLocal(Transform tr, Vector3 to, float duration)
        {
            Vector3 from = tr.localPosition;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(duration, 0.0001f);
                tr.localPosition = Vector3.Lerp(from, to, Smooth(t));
                yield return null;
            }
        }
    }
}
