// =====================================================================================
//  RoadNetwork.cs — Γράφος δρόμων και αναζήτηση διαδρομής A*
//  ---------------------------------------------------------------------------------
//  Οι χωρικοί δεν περπατούν «ευθεία» μέσα από σπίτια: κινούνται πάνω στους δρόμους.
//
//  1. Κατασκευή γράφου: κάθε δρόμος (WorldLayout.Roads) δειγματοληπτείται ανά ~7 m σε
//     κόμβους· διαδοχικοί κόμβοι ενώνονται με ακμές. Κόμβοι που πέφτουν πολύ κοντά
//     (διασταυρώσεις) συγχωνεύονται. Προαιρετικά «σημεία» (πόρτες σπιτιών, θέσεις εργασίας)
//     συνδέονται στον πλησιέστερο κόμβο.
//  2. A*: βρίσκει τη συντομότερη διαδρομή με ευρετική την ευκλείδεια απόσταση (αποδεκτή
//     ευρετική -> βέλτιστο αποτέλεσμα). Χρησιμοποιεί δυαδικό σωρό (binary heap) ως ουρά
//     προτεραιότητας: O(E log V) αντί για O(V²) της απλοϊκής υλοποίησης.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Greystone
{
    public class RoadNetwork
    {
        public static RoadNetwork Instance { get; private set; }

        readonly List<Vector2> _nodes = new List<Vector2>();
        readonly List<List<int>> _edges = new List<List<int>>();
        const float SampleStep = 7f;
        const float MergeDistance = 3.5f;
        const float Extent = 120f;   // ο γράφος καλύπτει μόνο την περιοχή του χωριού

        public int NodeCount { get { return _nodes.Count; } }

        public RoadNetwork()
        {
            Instance = this;
            foreach (Vector4 r in WorldLayout.Roads)
            {
                Vector2 a = new Vector2(r.x, r.y), b = new Vector2(r.z, r.w);
                // Περικοπή του τμήματος στην περιοχή του χωριού
                ClipToExtent(ref a, ref b);
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / SampleStep));
                int prev = -1;
                for (int i = 0; i <= steps; i++)
                {
                    Vector2 p = Vector2.Lerp(a, b, i / (float)steps);
                    int id = AddOrMerge(p);
                    if (prev >= 0 && prev != id) Connect(prev, id);
                    prev = id;
                }
            }
            // Κόμβοι στην περίμετρο της πλατείας (οι χωρικοί κάνουν κύκλο γύρω από τα εμπόδια της)
            int first = -1, last = -1;
            for (int i = 0; i < 12; i++)
            {
                float ang = i / 12f * Mathf.PI * 2f;
                int id = AddOrMerge(WorldLayout.PlazaCenter + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 14.5f);
                if (last >= 0) Connect(last, id); else first = id;
                last = id;
                ConnectToNearest(id, 9f);
            }
            Connect(last, first);
        }

        static void ClipToExtent(ref Vector2 a, ref Vector2 b)
        {
            Vector2 d = b - a;
            float t0 = 0f, t1 = 1f;
            for (int axis = 0; axis < 2; axis++)
            {
                float p = axis == 0 ? a.x : a.y;
                float dd = axis == 0 ? d.x : d.y;
                if (Mathf.Abs(dd) < 1e-5f) continue;
                float ta = (-Extent - p) / dd, tb = (Extent - p) / dd;
                if (ta > tb) { float tmp = ta; ta = tb; tb = tmp; }
                t0 = Mathf.Max(t0, ta);
                t1 = Mathf.Min(t1, tb);
            }
            Vector2 na = a + d * t0, nb = a + d * t1;
            a = na;
            b = nb;
        }

        int AddOrMerge(Vector2 p)
        {
            for (int i = 0; i < _nodes.Count; i++)
                if ((_nodes[i] - p).sqrMagnitude < MergeDistance * MergeDistance) return i;
            _nodes.Add(p);
            _edges.Add(new List<int>());
            return _nodes.Count - 1;
        }

        void Connect(int a, int b)
        {
            if (a == b) return;
            if (!_edges[a].Contains(b)) _edges[a].Add(b);
            if (!_edges[b].Contains(a)) _edges[b].Add(a);
        }

        void ConnectToNearest(int id, float maxDist)
        {
            for (int i = 0; i < _nodes.Count; i++)
                if (i != id && Vector2.Distance(_nodes[i], _nodes[id]) < maxDist) Connect(i, id);
        }

        public int Nearest(Vector2 p)
        {
            int best = 0;
            float bd = float.MaxValue;
            for (int i = 0; i < _nodes.Count; i++)
            {
                float d = (_nodes[i] - p).sqrMagnitude;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        /// <summary>
        /// Διαδρομή από το from στο to (θέσεις στον κόσμο). Επιστρέφει λίστα σημείων στο έδαφος:
        /// from -> κόμβοι δρόμου (A*) -> to.
        /// </summary>
        public List<Vector3> FindPath(Vector3 from, Vector3 to)
        {
            List<Vector3> result = new List<Vector3>();
            Vector2 f2 = new Vector2(from.x, from.z), t2 = new Vector2(to.x, to.z);
            int start = Nearest(f2), goal = Nearest(t2);
            List<int> nodes = AStar(start, goal);

            result.Add(from);
            // Αν ο προορισμός είναι πιο κοντά από τον πρώτο κόμβο, πάμε κατευθείαν.
            if (Vector2.Distance(f2, t2) < Vector2.Distance(f2, _nodes[start]) + 2f || nodes == null)
            {
                result.Add(to);
                return result;
            }
            foreach (int n in nodes) result.Add(WorldLayout.OnGround(_nodes[n].x, _nodes[n].y));
            result.Add(to);
            return result;
        }

        // ---------------------------------------------------------------- A*
        List<int> AStar(int start, int goal)
        {
            int n = _nodes.Count;
            float[] g = new float[n];
            int[] came = new int[n];
            bool[] closed = new bool[n];
            for (int i = 0; i < n; i++) { g[i] = float.MaxValue; came[i] = -1; }
            g[start] = 0f;

            MinHeap open = new MinHeap(n);
            open.Push(start, Heuristic(start, goal));

            while (open.Count > 0)
            {
                int cur = open.Pop();
                if (cur == goal) break;
                if (closed[cur]) continue;       // «παλιά» εγγραφή στον σωρό (lazy deletion)
                closed[cur] = true;
                foreach (int nb in _edges[cur])
                {
                    if (closed[nb]) continue;
                    float tentative = g[cur] + Vector2.Distance(_nodes[cur], _nodes[nb]);
                    if (tentative < g[nb])
                    {
                        g[nb] = tentative;
                        came[nb] = cur;
                        open.Push(nb, tentative + Heuristic(nb, goal));
                    }
                }
            }
            if (start != goal && came[goal] < 0) return null;

            // Ανασύνθεση διαδρομής από το τέλος προς την αρχή
            List<int> path = new List<int>();
            for (int c = goal; c >= 0; c = came[c]) path.Add(c);
            path.Reverse();
            return path;
        }

        float Heuristic(int a, int b) { return Vector2.Distance(_nodes[a], _nodes[b]); }

        /// <summary>Δυαδικός σωρός ελαχίστου (ουρά προτεραιότητας) για το A*.</summary>
        class MinHeap
        {
            readonly List<int> _items;
            readonly List<float> _keys;
            public int Count { get { return _items.Count; } }

            public MinHeap(int capacity)
            {
                _items = new List<int>(capacity);
                _keys = new List<float>(capacity);
            }

            public void Push(int item, float key)
            {
                _items.Add(item);
                _keys.Add(key);
                int i = _items.Count - 1;
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (_keys[parent] <= _keys[i]) break;
                    Swap(i, parent);
                    i = parent;
                }
            }

            public int Pop()
            {
                int top = _items[0];
                int last = _items.Count - 1;
                Swap(0, last);
                _items.RemoveAt(last);
                _keys.RemoveAt(last);
                int i = 0;
                while (true)
                {
                    int l = i * 2 + 1, r = l + 1, m = i;
                    if (l < _items.Count && _keys[l] < _keys[m]) m = l;
                    if (r < _items.Count && _keys[r] < _keys[m]) m = r;
                    if (m == i) break;
                    Swap(i, m);
                    i = m;
                }
                return top;
            }

            void Swap(int a, int b)
            {
                int ti = _items[a]; _items[a] = _items[b]; _items[b] = ti;
                float tk = _keys[a]; _keys[a] = _keys[b]; _keys[b] = tk;
            }
        }

        /// <summary>Οι κόμβοι (για οπτικοποίηση στον χάρτη).</summary>
        public IList<Vector2> Nodes { get { return _nodes; } }
    }
}
