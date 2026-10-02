using UnityEngine;

namespace MergeLegion.Battle
{
    public enum VfxType { Pop, Hit, Death, Heal, Glow, Coin, Explosion }

    /// <summary>
    /// Allocation-free particle effects: one shared ParticleSystem per effect type, emitted with per-call
    /// position and colour. Nothing is instantiated while a battle runs.
    /// </summary>
    public sealed class Vfx : MonoBehaviour
    {
        private static Vfx _instance;
        private ParticleSystem[] _systems;

        public static Vfx Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[Vfx]");
                    _instance = go.AddComponent<Vfx>();
                    _instance.Build();
                }
                return _instance;
            }
        }

        public static void Emit(VfxType type, Vector3 position, Color color, int count = 10)
        {
            var ps = Instance._systems[(int)type];
            var ep = new ParticleSystem.EmitParams
            {
                position = position,
                startColor = color,
                applyShapeToPosition = true
            };
            ps.Emit(ep, count);
        }

        private void Build()
        {
            _systems = new ParticleSystem[7];
            //                              life  vMin vMax  sMin  sMax  grav   radius
            _systems[(int)VfxType.Pop]       = Make("Pop",       0.55f, 2.0f, 5.0f, 0.12f, 0.28f, 0.6f, 0.15f);
            _systems[(int)VfxType.Hit]       = Make("Hit",       0.30f, 1.5f, 3.5f, 0.08f, 0.16f, 0.2f, 0.10f);
            _systems[(int)VfxType.Death]     = Make("Death",     0.70f, 2.0f, 4.5f, 0.15f, 0.35f, 1.2f, 0.25f);
            _systems[(int)VfxType.Heal]      = Make("Heal",      0.90f, 0.5f, 1.5f, 0.10f, 0.20f, -0.6f, 0.45f);
            _systems[(int)VfxType.Glow]      = Make("Glow",      0.80f, 0.3f, 1.2f, 0.30f, 0.60f, -0.2f, 0.30f);
            _systems[(int)VfxType.Coin]      = Make("Coin",      0.80f, 3.0f, 6.0f, 0.15f, 0.25f, 2.0f, 0.10f);
            _systems[(int)VfxType.Explosion] = Make("Explosion", 0.90f, 3.0f, 9.0f, 0.30f, 0.80f, 0.3f, 0.40f);
        }

        private ParticleSystem Make(string label, float life, float vMin, float vMax, float sMin, float sMax, float gravity, float radius)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.7f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(vMin, vMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sMin, sMax);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 1500;

            var emission = ps.emission;
            emission.enabled = false;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) });
            col.color = gradient;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = MaterialLibrary.Particle;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            ps.Play();
            return ps;
        }
    }
}
