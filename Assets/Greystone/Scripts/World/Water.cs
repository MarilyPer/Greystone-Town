// =====================================================================================
//  Water.cs — Ποτάμι
//  ---------------------------------------------------------------------------------
//  Ένα επίμηκες επίπεδο (λεπτομερές πλέγμα για τα κύματα του vertex shader) στη στάθμη
//  του νερού. Το έδαφος έχει ήδη «σκαφτεί» κατά μήκος της κοίτης (WorldLayout), οπότε το
//  νερό φαίνεται μόνο εκεί. Κατά μήκος της κοίτης τοποθετούνται πηγές ήχου ροής
//  με μικρή εμβέλεια: το AudioSource εκτός εμβέλειας δεν ακούγεται (και το Unity το
//  «εικονικοποιεί» — virtual voice — χωρίς κόστος μίξης).
// =====================================================================================
using UnityEngine;
using UnityEngine.Rendering;

namespace Greystone
{
    public class Water : MonoBehaviour
    {
        public void Build()
        {
            Material mat = Mats.Get("water");
            // Τμήματα των 60 m ώστε να γίνεται frustum culling ανά τμήμα
            for (float z = -WorldLayout.Half; z < WorldLayout.Half; z += 60f)
            {
                float zc = z + 30f;
                float x = WorldLayout.RiverX(zc);
                GameObject g = Geo.Part(transform, "River", Geo.PlaneGrid(16, 40), mat,
                                        new Vector3(x, WorldLayout.WaterLevel, zc), new Vector3(46f, 1f, 60f));
                MeshRenderer mr = g.GetComponent<MeshRenderer>();
                mr.shadowCastingMode = ShadowCastingMode.Off;
            }

            // Ήχος ροής ανά 40 m κατά μήκος του ποταμού (μόνο μέσα στην περιοχή περιήγησης)
            for (float z = -200f; z <= 200f; z += 40f)
            {
                Vector3 p = new Vector3(WorldLayout.RiverX(z), WorldLayout.WaterLevel + 0.3f, z);
                AudioSource a = SoundBank.Instance.CreateLoop(transform, Vector3.zero, "river", 0.5f, 4f, 32f);
                a.transform.position = p;
            }
        }
    }
}
