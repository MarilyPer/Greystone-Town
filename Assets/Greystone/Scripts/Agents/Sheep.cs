// =====================================================================================
//  Sheep.cs — Πρόβατο (μηχανή καταστάσεων)
//  ---------------------------------------------------------------------------------
//  Καταστάσεις: Idle (στέκεται), Graze (βόσκει με σκυμμένο κεφάλι), Walk (πηγαίνει σε τυχαίο
//  σημείο του μαντριού), Flee (απομακρύνεται από τον παίκτη αν πλησιάσει τρέχοντας ή πολύ κοντά).
//  Οι μεταβάσεις είναι πιθανοτικές με τυχαίες διάρκειες — έτσι το κοπάδι δεν κινείται
//  «συγχρονισμένα». Το βέλασμα ακούγεται σε τυχαία διαστήματα (μόνο όταν ο παίκτης είναι κοντά).
//  Όλες οι κινήσεις περιορίζονται μέσα στο μαντρί (WorldLayout.SheepPen).
// =====================================================================================
using UnityEngine;

namespace Greystone
{
    public class Sheep : Agent
    {
        enum State { Idle, Graze, Walk, Flee }
        State _state;
        float _timer, _baaTimer, _phase;
        Vector3 _target;
        Transform _headPivot;
        Transform[] _legs = new Transform[4];
        Rect _pen;

        public void Build(Rect pen, int seed)
        {
            _pen = pen;
            System.Random rnd = new System.Random(seed);
            Material wool = Mats.Get("wool");
            Material face = Mats.Get("sheep_face");
            float s = 0.9f + (float)rnd.NextDouble() * 0.2f;
            transform.localScale = Vector3.one * s;
            Geo.Ball(transform, new Vector3(0.75f, 0.62f, 1.15f), new Vector3(0, 0.78f, 0), wool, 12, 8);
            Geo.Ball(transform, new Vector3(0.55f, 0.35f, 0.6f), new Vector3(0, 1.02f, -0.1f), wool, 10, 6);   // «φούσκωμα» πλάτης
            _headPivot = Geo.Child(transform, "HeadPivot", new Vector3(0, 0.95f, 0.5f));
            Geo.Ball(_headPivot, new Vector3(0.36f, 0.34f, 0.36f), new Vector3(0, 0.05f, 0.08f), wool, 8, 6);   // μαλλί στο μέτωπο (Cotswold)
            Geo.Ball(_headPivot, new Vector3(0.24f, 0.26f, 0.4f), new Vector3(0, -0.05f, 0.25f), face, 10, 6);
            Geo.Ball(_headPivot, new Vector3(0.18f, 0.06f, 0.1f), new Vector3(-0.17f, 0.02f, 0.12f), face, 6, 4);
            Geo.Ball(_headPivot, new Vector3(0.18f, 0.06f, 0.1f), new Vector3(0.17f, 0.02f, 0.12f), face, 6, 4);
            Vector3[] legPos = { new Vector3(-0.2f, 0.55f, 0.35f), new Vector3(0.2f, 0.55f, 0.35f), new Vector3(-0.2f, 0.55f, -0.35f), new Vector3(0.2f, 0.55f, -0.35f) };
            for (int i = 0; i < 4; i++)
            {
                _legs[i] = Geo.Child(transform, "Leg", legPos[i]);
                Geo.Cyl(_legs[i], 0.05f, 0.55f, new Vector3(0, -0.27f, 0), face, 0.8f, 6);
            }
            Interactable it = Interactable.Create(transform, new Vector3(0, 0.8f, 0), new Vector3(0.9f, 1.2f, 1.4f), "Χαϊδέψτε το πρόβατο");
            it.Interacted += p =>
            {
                SoundBank.Instance.PlayAt(Random.value < 0.5f ? "baa" : "baa_high", transform.position + Vector3.up, 0.9f, Random.Range(0.9f, 1.1f), 30f);
                if (Hud.Instance != null) Hud.Instance.Notify("Το μαλλί του είναι μακρύ και σγουρό — ένα αληθινό «Cotswold Lion».");
                _state = State.Idle;
                _timer = 3f;
            };
            _baaTimer = Random.Range(5f, 25f);
            PickState();
        }

        void PickState()
        {
            float r = Random.value;
            if (r < 0.45f) { _state = State.Graze; _timer = Random.Range(5f, 14f); }
            else if (r < 0.7f) { _state = State.Idle; _timer = Random.Range(2f, 6f); }
            else
            {
                _state = State.Walk;
                _target = new Vector3(Random.Range(_pen.xMin + 1.5f, _pen.xMax - 1.5f), 0, Random.Range(_pen.yMin + 1.5f, _pen.yMax - 1.5f));
                _timer = 15f;
            }
        }

        public override void AgentUpdate(float dt)
        {
            PlayerController p = PlayerController.Instance;
            Vector3 pos = transform.position;
            float speed = 0f;

            // Αντίδραση στον παίκτη: φυγή αν είναι πολύ κοντά ή αν τρέχει κοντά
            if (p != null && _state != State.Flee)
            {
                float d = Vector3.Distance(p.transform.position, pos);
                bool running = GInput.Held(GKey.Run) && GInput.MoveAxis.sqrMagnitude > 0.1f;
                if (d < 2.2f || (running && d < 7f))
                {
                    Vector3 away = (pos - p.transform.position);
                    away.y = 0f;
                    _target = pos + away.normalized * 5f;
                    _state = State.Flee;
                    _timer = 3f;
                    if (Random.value < 0.5f) SoundBank.Instance.PlayAt("baa_high", pos + Vector3.up, 0.8f, Random.Range(1f, 1.2f), 30f);
                }
            }

            _timer -= dt;
            switch (_state)
            {
                case State.Walk: speed = 0.7f; break;
                case State.Flee: speed = 2.4f; break;
            }
            if (speed > 0f)
            {
                // Περιορισμός του στόχου μέσα στο μαντρί
                _target.x = Mathf.Clamp(_target.x, _pen.xMin + 1f, _pen.xMax - 1f);
                _target.z = Mathf.Clamp(_target.z, _pen.yMin + 1f, _pen.yMax - 1f);
                Vector3 to = _target - pos;
                to.y = 0f;
                if (to.magnitude < 0.3f) _timer = 0f;
                else
                {
                    Vector3 dir = to.normalized;
                    pos += dir * speed * dt;
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-dt * 4f));
                    _phase += speed * dt * 7f;
                }
            }
            if (_timer <= 0f) PickState();

            pos.y = WorldLayout.HeightAt(pos.x, pos.z);
            transform.position = pos;

            // Animation: βάδισμα διαγώνιων ζευγών ποδιών, κεφάλι χαμηλά όταν βόσκει
            float swing = speed > 0f ? Mathf.Sin(_phase) * 28f : 0f;
            _legs[0].localRotation = _legs[3].localRotation = Quaternion.Euler(swing, 0, 0);
            _legs[1].localRotation = _legs[2].localRotation = Quaternion.Euler(-swing, 0, 0);
            float headPitch = _state == State.Graze ? 55f + Mathf.Sin(Time.time * 3f + tickOffset) * 6f : Mathf.Sin(Time.time * 0.7f + tickOffset) * 5f;
            _headPivot.localRotation = Quaternion.Slerp(_headPivot.localRotation, Quaternion.Euler(headPitch, 0, 0), 1f - Mathf.Exp(-dt * 3f));

            // Βέλασμα σε τυχαία διαστήματα (μόνο αν ο παίκτης είναι σχετικά κοντά)
            _baaTimer -= dt;
            if (_baaTimer <= 0f)
            {
                _baaTimer = Random.Range(10f, 30f);
                if (p != null && Vector3.Distance(p.transform.position, pos) < 45f)
                    SoundBank.Instance.PlayAt(Random.value < 0.6f ? "baa" : "baa_high", pos + Vector3.up, 0.7f, Random.Range(0.85f, 1.15f), 45f);
            }
        }
    }
}
