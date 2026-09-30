// =====================================================================================
//  TimeManager.cs
//  ---------------------------------------------------------------------------------
//  Το ρολόι του κόσμου. Κρατά την ώρα της ημέρας, τη μέρα και την ταχύτητα ροής του
//  χρόνου, και υπολογίζει τη ΘΕΣΗ ΤΟΥ ΗΛΙΟΥ με αστρονομικό τύπο για το γεωγραφικό
//  πλάτος των Cotswolds (≈51.8° Β). Έτσι η ανατολή/δύση και το ύψος του ήλιου το
//  μεσημέρι είναι ρεαλιστικά (αληθοφάνεια).
//
//  Μοτίβο σχεδίασης: Singleton + Observer. Τα υπόλοιπα συστήματα εγγράφονται στα
//  events (HourChanged, NightChanged) αντί να ελέγχουν την ώρα σε κάθε frame —
//  λιγότερη δουλειά ανά frame (βελτιστοποίηση).
// =====================================================================================
using System;
using UnityEngine;

namespace Greystone
{
    public class TimeManager : MonoBehaviour
    {
        public static TimeManager Instance { get; private set; }

        /// <summary>Πόσα δευτερόλεπτα παιχνιδιού περνούν σε 1 πραγματικό δευτερόλεπτο.</summary>
        public static readonly float[] SpeedPresets = { 60f, 360f, 1800f };

        [Tooltip("Ώρα της ημέρας σε ώρες (0..24)")]
        public float hours = 8.5f;
        public int day = 1;
        public int speedIndex = 0;

        // Αστρονομικές παράμετροι
        const float LatitudeDeg = 51.8f;      // Cotswolds
        const float DeclinationDeg = 12f;     // ηλιακή απόκλιση (αρχές Μαΐου / μέσα Αυγούστου)

        public event Action<int> HourChanged;      // νέα ακέραια ώρα
        public event Action<bool> NightChanged;    // true = νύχτωσε

        public bool IsNight { get; private set; }
        public float TimeScale { get { return SpeedPresets[speedIndex]; } }

        void Awake()
        {
            Instance = this;
            IsNight = ComputeSunDirection(hours).y < 0.03f;
        }

        void Update()
        {
            int prevHour = (int)hours;
            hours += Time.deltaTime * TimeScale / 3600f;
            if (hours >= 24f)
            {
                hours -= 24f;
                day++;
            }

            int h = (int)hours;
            if (h != prevHour && HourChanged != null) HourChanged(h);

            bool night = SunDirection.y < 0.03f;   // λίγο πριν δύσει εντελώς ανάβουν τα φανάρια
            if (night != IsNight)
            {
                IsNight = night;
                if (NightChanged != null) NightChanged(night);
            }
        }

        public void CycleSpeed()
        {
            speedIndex = (speedIndex + 1) % SpeedPresets.Length;
        }

        /// <summary>Μεταπήδηση σε συγκεκριμένη ώρα (χρησιμοποιείται από το μενού).</summary>
        public void SetHour(float h)
        {
            hours = Mathf.Repeat(h, 24f);
        }

        public Vector3 SunDirection { get { return ComputeSunDirection(hours); } }

        /// <summary>
        /// Κατεύθυνση προς τον ήλιο (μοναδιαίο διάνυσμα) με τον κλασικό τύπο θέσης ηλίου:
        ///   ύψος:  sin(a) = sinφ·sinδ + cosφ·cosδ·cosH
        /// όπου φ = γεωγρ. πλάτος, δ = απόκλιση, H = ωριαία γωνία (15° ανά ώρα από το μεσημέρι).
        /// Συνιστώσες σε σύστημα Ανατολή/Βορράς/Πάνω -> (x, z, y) του Unity.
        /// </summary>
        public static Vector3 ComputeSunDirection(float h)
        {
            float phi = LatitudeDeg * Mathf.Deg2Rad;
            float dec = DeclinationDeg * Mathf.Deg2Rad;
            float H = (h - 12f) * 15f * Mathf.Deg2Rad;
            float east = -Mathf.Cos(dec) * Mathf.Sin(H);
            float north = Mathf.Cos(phi) * Mathf.Sin(dec) - Mathf.Sin(phi) * Mathf.Cos(dec) * Mathf.Cos(H);
            float up = Mathf.Sin(phi) * Mathf.Sin(dec) + Mathf.Cos(phi) * Mathf.Cos(dec) * Mathf.Cos(H);
            return new Vector3(east, up, north).normalized;
        }

        /// <summary>Φως ημέρας 0..1 (ομαλή μετάβαση στο λυκόφως).</summary>
        public float Daylight
        {
            get { return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.12f, 0.2f, SunDirection.y)); }
        }

        public string ClockText
        {
            get
            {
                int h = (int)hours;
                int m = (int)((hours - h) * 60f);
                return string.Format("{0:00}:{1:00}", h, m);
            }
        }

        /// <summary>Λεκτική περιγραφή του τμήματος της ημέρας (για το HUD και τους διαλόγους).</summary>
        public string PartOfDay
        {
            get
            {
                if (hours < 5f) return "Νύχτα";
                if (hours < 8f) return "Αυγή";
                if (hours < 12f) return "Πρωί";
                if (hours < 14f) return "Μεσημέρι";
                if (hours < 18f) return "Απόγευμα";
                if (hours < 21f) return "Σούρουπο";
                return "Νύχτα";
            }
        }
    }
}
