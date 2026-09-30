// =====================================================================================
//  SoundBank.cs — Τράπεζα ήχων & Object Pool πηγών ήχου
//  ---------------------------------------------------------------------------------
//  * Συνθέτει ΜΙΑ φορά όλα τα AudioClips (Synth.cs) κατά τη φόρτωση, σε διαδοχικά frames
//    (coroutine), ώστε η οθόνη φόρτωσης να μην «παγώνει».
//  * OBJECT POOLING: Οι στιγμιαίοι ήχοι (βήματα, χτυπήματα, βελάσματα…) παίζονται από μια
//    δεξαμενή προκατασκευασμένων AudioSource που ανακυκλώνονται. Αποφεύγουμε έτσι τη
//    συνεχή δημιουργία/καταστροφή GameObjects (Instantiate/Destroy), που προκαλεί
//    κόστος CPU και συλλογή απορριμμάτων (GC spikes).
// =====================================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Greystone
{
    public class SoundBank : MonoBehaviour
    {
        public static SoundBank Instance { get; private set; }

        readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        AudioSource[] _pool;
        int _next;
        const int PoolSize = 24;

        /// <summary>Συνταγές: όνομα -> συνάρτηση σύνθεσης.</summary>
        static readonly KeyValuePair<string, System.Func<float[]>>[] Recipes =
        {
            R("bell", Synth.Bell),
            R("clang", Synth.Clang),
            R("creak", () => Synth.Creak()),
            R("knock", Synth.Knock),
            R("thud", () => Synth.Thud()),
            R("clunk", () => Synth.Thud(140f, 0.25f)),
            R("baa", () => Synth.Baa(1f)),
            R("baa_high", () => Synth.Baa(1.25f)),
            R("birdsong", Synth.Birdsong),
            R("crickets", Synth.Crickets),
            R("river", Synth.River),
            R("wind", Synth.Wind),
            R("rain", Synth.Rain),
            R("fire", Synth.Fire),
            R("millworks", Synth.MillWorks),
            R("step_grass", () => Synth.Step(0.8f, 0f)),
            R("step_stone", () => Synth.Step(0.35f, 0.6f)),
            R("step_wood", () => Synth.Step(0.6f, 0.3f)),
            R("splash", Synth.Splash),
            R("bellows", Synth.Bellows),
            R("ratchet", () => Synth.Ratchet()),
            R("cart", Synth.Cart),
            R("ui", Synth.UiClick),
            R("hurdy", Synth.HurdyGurdyTune),
        };

        static KeyValuePair<string, System.Func<float[]>> R(string n, System.Func<float[]> f)
        {
            return new KeyValuePair<string, System.Func<float[]>>(n, f);
        }

        public static int RecipeCount { get { return Recipes.Length; } }

        void Awake()
        {
            Instance = this;
            _pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                GameObject go = new GameObject("PooledAudio_" + i);
                go.transform.SetParent(transform, false);
                AudioSource a = go.AddComponent<AudioSource>();
                Configure3D(a, 1f, 40f);
                a.playOnAwake = false;
                _pool[i] = a;
            }
        }

        /// <summary>Σύνθεση όλων των ήχων, ένας ανά frame. progress(0..1, όνομα).</summary>
        public IEnumerator Generate(System.Action<float, string> progress)
        {
            for (int i = 0; i < Recipes.Length; i++)
            {
                if (progress != null) progress(i / (float)Recipes.Length, Recipes[i].Key);
                yield return null;
                if (!_clips.ContainsKey(Recipes[i].Key))
                    _clips[Recipes[i].Key] = Synth.ToClip(Recipes[i].Key, Recipes[i].Value());
            }
        }

        public AudioClip Clip(string name)
        {
            AudioClip c;
            return _clips.TryGetValue(name, out c) ? c : null;
        }

        public static void Configure3D(AudioSource a, float minDist, float maxDist)
        {
            a.spatialBlend = 1f;
            a.rolloffMode = AudioRolloffMode.Linear;
            a.minDistance = minDist;
            a.maxDistance = maxDist;
            a.dopplerLevel = 0f;
        }

        /// <summary>Παίζει στιγμιαίο ήχο σε θέση του κόσμου χρησιμοποιώντας την επόμενη πηγή της δεξαμενής.</summary>
        public void PlayAt(string clip, Vector3 pos, float volume = 1f, float pitch = 1f, float maxDist = 40f)
        {
            AudioClip c = Clip(clip);
            if (c == null) return;
            // Προτιμούμε ελεύθερη πηγή· αν όλες παίζουν, «κλέβουμε» την παλαιότερη (round-robin).
            AudioSource a = null;
            for (int k = 0; k < PoolSize; k++)
            {
                AudioSource cand = _pool[(_next + k) % PoolSize];
                if (!cand.isPlaying) { a = cand; _next = (_next + k + 1) % PoolSize; break; }
            }
            if (a == null) { a = _pool[_next]; _next = (_next + 1) % PoolSize; }

            a.transform.position = pos;
            a.clip = c;
            a.volume = volume;
            a.pitch = pitch;
            a.maxDistance = maxDist;
            a.spatialBlend = 1f;
            a.Play();
        }

        /// <summary>Ήχος διεπαφής (2D, χωρίς χωρικότητα).</summary>
        public void PlayUI(string clip, float volume = 0.5f)
        {
            AudioSource a = _pool[_next];
            _next = (_next + 1) % PoolSize;
            a.clip = Clip(clip);
            a.volume = volume;
            a.pitch = 1f;
            a.spatialBlend = 0f;
            if (a.clip != null) a.Play();
        }

        /// <summary>Δημιουργεί μόνιμη πηγή ήχου σε επανάληψη (loop) προσαρτημένη σε αντικείμενο.</summary>
        public AudioSource CreateLoop(Transform parent, Vector3 localPos, string clip, float volume, float minDist, float maxDist, bool play = true)
        {
            GameObject go = new GameObject("Loop_" + clip);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            AudioSource a = go.AddComponent<AudioSource>();
            Configure3D(a, minDist, maxDist);
            a.clip = Clip(clip);
            a.loop = true;
            a.volume = volume;
            a.playOnAwake = false;
            if (play && a.clip != null)
            {
                a.time = Random.Range(0f, a.clip.length * 0.9f);   // διαφορετική φάση ανά πηγή
                a.Play();
            }
            return a;
        }
    }
}
