// =====================================================================================
//  Lever.cs — Μοχλός δύο θέσεων
//  ---------------------------------------------------------------------------------
//  Γενικό, επαναχρησιμοποιήσιμο συστατικό: εκπέμπει το event Toggled(bool) όταν ο χρήστης
//  τον μετακινεί. Χρησιμοποιείται για το φρένο του ανεμόμυλου (μπορεί όμως να συνδεθεί με
//  οποιονδήποτε μηχανισμό — Observer pattern).
// =====================================================================================
using System;
using UnityEngine;

namespace Greystone
{
    public class Lever : MonoBehaviour
    {
        public event Action<bool> Toggled;
        public bool State { get; private set; }
        public string onText = "Απενεργοποίηση", offText = "Ενεργοποίηση";

        Transform _arm;
        Coroutine _anim;

        public void Build(bool initial)
        {
            Material wood = Mats.Get("wood_weathered");
            Material iron = Mats.Get("iron");
            Geo.Box(transform, new Vector3(0.5f, 0.3f, 0.35f), new Vector3(0, 0.15f, 0), wood, default(Vector3), true);
            Geo.Box(transform, new Vector3(0.08f, 0.14f, 0.3f), new Vector3(-0.2f, 0.36f, 0), iron);
            Geo.Box(transform, new Vector3(0.08f, 0.14f, 0.3f), new Vector3(0.2f, 0.36f, 0), iron);
            _arm = Geo.Child(transform, "Arm", new Vector3(0, 0.38f, 0));
            Geo.Cyl(_arm, 0.035f, 0.9f, new Vector3(0, 0.45f, 0), wood, 1f, 8);
            Geo.Ball(_arm, Vector3.one * 0.12f, new Vector3(0, 0.92f, 0), Mats.Get("leather"));
            State = initial;
            _arm.localRotation = Quaternion.Euler(State ? 35f : -35f, 0, 0);

            Interactable it = Interactable.Create(transform, new Vector3(0, 0.6f, 0), new Vector3(0.8f, 1.3f, 0.8f), "");
            it.PromptProvider = () => State ? onText : offText;
            it.Interacted += p => Set(!State);
        }

        public void Set(bool on)
        {
            State = on;
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(Anim.RotateLocal(_arm, Quaternion.Euler(on ? 35f : -35f, 0, 0), 0.45f));
            SoundBank.Instance.PlayAt("clunk", transform.position, 0.8f, 0.9f, 25f);
            if (Toggled != null) Toggled(on);
        }
    }
}
