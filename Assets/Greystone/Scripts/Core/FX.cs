// =====================================================================================
//  FX.cs — Εργοστάσιο συστημάτων σωματιδίων (Factory pattern)
//  ---------------------------------------------------------------------------------
//  Δημιουργεί με κώδικα τα ParticleSystems του κόσμου: καπνό καμινάδας, φωτιά, σπίθες
//  αμονιού, βροχή, σταγόνες κουβά. Όλα χρησιμοποιούν τον shader Greystone/Particle
//  (μαλακός κύκλος χωρίς υφή) και κοινά υλικά.
//
//  ΒΕΛΤΙΣΤΟΠΟΙΗΣΗ: μικρά maxParticles, simulationSpace ανάλογα με την περίπτωση, χωρίς
//  σκιές και χωρίς light probes, «culling» όταν δεν είναι ορατά (ParticleSystemCullingMode.Pause).
// =====================================================================================
using UnityEngine;
using UnityEngine.Rendering;

namespace Greystone
{
    public static class FX
    {
        static ParticleSystem Base(Transform parent, string name, Vector3 localPos, Material mat, int max)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.maxParticles = max;
            main.playOnAwake = false;
            main.cullingMode = ParticleSystemCullingMode.Pause;   // σταματά όταν δεν φαίνεται
            ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return ps;
        }

        static ParticleSystem.MinMaxGradient FadeGradient(Color a, Color b, float peakAlpha)
        {
            Gradient g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peakAlpha, 0.15f), new GradientAlphaKey(peakAlpha * 0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
            return new ParticleSystem.MinMaxGradient(g);
        }

        /// <summary>Καπνός καμινάδας: αργά ανερχόμενες, μεγαλώνουσες γκρίζες τολύπες που παρασύρει ο άνεμος.</summary>
        public static ParticleSystem Smoke(Transform parent, Vector3 localPos, float scale = 1f)
        {
            ParticleSystem ps = Base(parent, "Smoke", localPos, Mats.Particle("smoke", Color.white, false, 0.9f), 40);
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f * scale, 0.8f * scale);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = 4f;
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 8f;
            sh.radius = 0.15f;
            sh.rotation = new Vector3(-90f, 0f, 0f);
            ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 3.2f));
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = FadeGradient(new Color(0.55f, 0.53f, 0.5f), new Color(0.72f, 0.72f, 0.72f), 0.45f);
            // Ο άνεμος (WeatherSystem) ρυθμίζει δυναμικά την force-over-lifetime σε όλους τους καπνούς.
            ParticleSystem.ForceOverLifetimeModule f = ps.forceOverLifetime;
            f.enabled = true;
            f.space = ParticleSystemSimulationSpace.World;
            f.x = new ParticleSystem.MinMaxCurve(0.3f);
            f.z = new ParticleSystem.MinMaxCurve(0.1f);
            ParticleSystem.NoiseModule nz = ps.noise;
            nz.enabled = true;
            nz.strength = 0.25f;
            nz.frequency = 0.4f;
            WeatherSystem.RegisterSmoke(ps);
            return ps;
        }

        /// <summary>Φωτιά: additive πορτοκαλί σωματίδια που ανεβαίνουν γρήγορα και μικραίνουν.</summary>
        public static ParticleSystem Fire(Transform parent, Vector3 localPos, float scale = 1f)
        {
            ParticleSystem ps = Base(parent, "Fire", localPos, Mats.Particle("fire", Color.white, true, 0.8f), 60);
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f * scale, 1.2f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f * scale, 0.45f * scale);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = 35f;
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 0.15f * scale;
            ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.85f, 0.4f), 0f), new GradientColorKey(new Color(1f, 0.4f, 0.08f), 0.5f), new GradientColorKey(new Color(0.5f, 0.1f, 0.02f), 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.1f), new GradientAlphaKey(0.5f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            ParticleSystem.NoiseModule nz = ps.noise;
            nz.enabled = true;
            nz.strength = 0.3f * scale;
            nz.frequency = 1.5f;
            ps.Play();
            return ps;
        }

        /// <summary>Σπίθες αμονιού: «έκρηξη» φωτεινών κουκκίδων με βαρύτητα (παίζεται με Emit).</summary>
        public static ParticleSystem Sparks(Transform parent, Vector3 localPos)
        {
            ParticleSystem ps = Base(parent, "Sparks", localPos, Mats.Particle("spark", new Color(1f, 0.7f, 0.3f), true, 0.5f), 80);
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
            main.gravityModifier = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = 0f;
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Hemisphere;
            sh.radius = 0.05f;
            sh.rotation = new Vector3(-90f, 0f, 0f);
            ps.Play();
            return ps;
        }

        /// <summary>Βροχή: επιμηκυμένες (Stretch) σταγόνες σε κουτί πάνω από την κάμερα.</summary>
        public static ParticleSystem Rain(Transform parent)
        {
            ParticleSystem ps = Base(parent, "Rain", new Vector3(0f, 14f, 0f), Mats.Particle("rain", new Color(0.75f, 0.8f, 0.88f, 0.45f), false, 0.6f, true), 3500);
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = 1.3f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(16f, 20f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.035f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = 0f;
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(36f, 1f, 36f);
            sh.rotation = new Vector3(90f, 0f, 0f);   // εκπομπή προς τα κάτω
            ParticleSystemRenderer r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.035f;
            r.lengthScale = 1f;
            ParticleSystem.CollisionModule c = ps.collision;   // οι σταγόνες «σβήνουν» στο έδαφος/στέγες
            c.enabled = true;
            c.type = ParticleSystemCollisionType.World;
            c.mode = ParticleSystemCollisionMode.Collision3D;
            c.quality = ParticleSystemCollisionQuality.Low;   // φθηνός (προσεγγιστικός) έλεγχος
            c.lifetimeLoss = 1f;
            c.collidesWith = 1 << Geo.LayerDefault;
            ps.Play();
            return ps;
        }

        /// <summary>Σταγόνες νερού (π.χ. από τον κουβά του πηγαδιού) — παίζεται με Emit.</summary>
        public static ParticleSystem Drips(Transform parent, Vector3 localPos)
        {
            ParticleSystem ps = Base(parent, "Drips", localPos, Mats.Particle("drip", new Color(0.7f, 0.8f, 0.9f, 0.7f), false, 0.5f), 40);
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = 0.8f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.05f);
            main.gravityModifier = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = 0f;
            ps.Play();
            return ps;
        }

        /// <summary>Σημειακό φως (π.χ. φανάρι, φωτιά) χωρίς σκιές.</summary>
        public static Light PointLight(Transform parent, Vector3 localPos, Color c, float range, float intensity)
        {
            GameObject go = new GameObject("PointLight");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.Auto;
            return l;
        }
    }
}
