using System.Collections.Generic;
using MergeLegion.Audio;
using MergeLegion.Core;
using MergeLegion.Data;
using UnityEngine;

namespace MergeLegion.Battle
{
    /// <summary>
    /// Renders a <see cref="BattleSim"/>: pooled unit models interpolated between fixed ticks, projectiles,
    /// telegraphs and the juice for every sim event. No allocations in the per-frame paths.
    /// </summary>
    public sealed class BattleView : MonoBehaviour
    {
        private struct Dying { public UnitVisual Visual; public float Time; public Vector3 Scale; }
        private struct Telegraph { public Transform Ring; public float Time; public float Duration; public float Radius; }

        private GameDatabase _db;
        private BattleSim _sim;
        private BattleConfig _cfg;
        private DamageNumbers _numbers;
        private readonly List<UnitVisual> _views = new List<UnitVisual>(256);
        private readonly List<float> _pulse = new List<float>(256);
        private readonly List<Dying> _dying = new List<Dying>(64);
        private readonly List<Telegraph> _telegraphs = new List<Telegraph>(8);
        private readonly Stack<Transform> _ringPool = new Stack<Transform>(8);
        private readonly Dictionary<int, Transform> _projectiles = new Dictionary<int, Transform>(128);
        private readonly Stack<Transform> _projectilePool = new Stack<Transform>(128);
        private Material _redDisc;

        public System.Action<SimEvent> EventHook;

        public void Init(GameDatabase db, BattleConfig cfg, DamageNumbers numbers)
        {
            _db = db;
            _cfg = cfg;
            _numbers = numbers;
            _redDisc = MaterialLibrary.Lit(new Color(1f, 0.15f, 0.1f, 1f));
        }

        public bool TryGetWorldPosition(int unitId, out Vector3 pos)
        {
            if (unitId > 0 && unitId <= _views.Count && _views[unitId - 1] != null)
            {
                pos = _views[unitId - 1].transform.position;
                return true;
            }
            pos = Vector3.zero;
            return false;
        }

        public float HeadHeight(int unitId)
        {
            var u = _sim.GetUnit(unitId);
            return u == null ? 1f : 1.1f * u.Scale + (u.IsFlying ? 1.1f : 0f) + 0.3f;
        }

        /// <summary>Drops every view and adopts a new simulation; its pending Spawn events create the views.</summary>
        public void Rebind(BattleSim sim)
        {
            Clear();
            _sim = sim;
            ProcessEvents();
            Sync(0f);
        }

        public void Clear()
        {
            for (int i = 0; i < _views.Count; i++)
            {
                UnitVisualFactory.Release(_views[i]);
                _views[i] = null;
            }
            _views.Clear();
            _pulse.Clear();
            for (int i = 0; i < _dying.Count; i++) UnitVisualFactory.Release(_dying[i].Visual);
            _dying.Clear();
            foreach (var kv in _projectiles) ReleaseProjectile(kv.Value);
            _projectiles.Clear();
            for (int i = 0; i < _telegraphs.Count; i++) ReleaseRing(_telegraphs[i].Ring);
            _telegraphs.Clear();
        }

        private void OnDestroy() => Clear();

        // ------------------------------------------------------------------ per frame

        /// <param name="alpha">Fraction (0..1) of the way to the next fixed tick, for interpolation.</param>
        public void Sync(float alpha)
        {
            if (_sim == null) return;
            float dt = Time.unscaledDeltaTime;
            var units = _sim.Units;
            int n = units.Count;
            for (int i = 0; i < n && i < _views.Count; i++)
            {
                var v = _views[i];
                if (v == null) continue;
                var u = units[i];
                float x = u.Prev.x + (u.Pos.x - u.Prev.x) * alpha;
                float z = u.Prev.y + (u.Pos.y - u.Prev.y) * alpha;
                var t = v.transform;
                t.position = new Vector3(x, v.HoverHeight, z);

                var target = u.TargetId > 0 ? units[u.TargetId - 1] : null;
                if (target != null && target.Alive)
                {
                    float dx = target.Pos.x - u.Pos.x, dz = target.Pos.y - u.Pos.y;
                    if (dx * dx + dz * dz > 0.0001f) t.rotation = Quaternion.Slerp(t.rotation, Quaternion.LookRotation(new Vector3(dx, 0f, dz)), 0.35f);
                }

                float p = _pulse[i];
                float s = u.IsBoss ? u.Scale : v.BaseScale;
                if (p > 0f)
                {
                    p -= dt;
                    _pulse[i] = p;
                    float k = 1f + 0.22f * Mathf.Sin(Mathf.Clamp01(p / 0.14f) * Mathf.PI);
                    t.localScale = new Vector3(s * k, s * (2f - k), s * k);
                }
                else t.localScale = new Vector3(s, s, s);
            }

            SyncProjectiles();
            SyncDying(dt);
            SyncTelegraphs(dt);
        }

        private void SyncProjectiles()
        {
            var list = _sim.Projectiles;
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                if (_projectiles.TryGetValue(p.Id, out var tr)) tr.position = new Vector3(p.Pos.x, 1.0f, p.Pos.y);
            }
        }

        private void SyncDying(float dt)
        {
            for (int i = _dying.Count - 1; i >= 0; i--)
            {
                var d = _dying[i];
                d.Time += Time.deltaTime;
                float k = Mathf.Clamp01(d.Time / 0.35f);
                d.Visual.transform.localScale = d.Scale * (1f - k);
                if (k >= 1f)
                {
                    UnitVisualFactory.Release(d.Visual);
                    int last = _dying.Count - 1;
                    _dying[i] = _dying[last];
                    _dying.RemoveAt(last);
                }
                else _dying[i] = d;
            }
        }

        private void SyncTelegraphs(float dt)
        {
            for (int i = _telegraphs.Count - 1; i >= 0; i--)
            {
                var t = _telegraphs[i];
                t.Time += dt;
                float k = Mathf.Clamp01(t.Time / t.Duration);
                float pulse = 1f + 0.06f * Mathf.Sin(t.Time * 18f);
                float d = t.Radius * 2f * (0.55f + 0.45f * k) * pulse;
                t.Ring.localScale = new Vector3(d, 0.02f, d);
                if (k >= 1f)
                {
                    ReleaseRing(t.Ring);
                    int last = _telegraphs.Count - 1;
                    _telegraphs[i] = _telegraphs[last];
                    _telegraphs.RemoveAt(last);
                }
                else _telegraphs[i] = t;
            }
        }

        // ------------------------------------------------------------------ events

        public void ProcessEvents()
        {
            if (_sim == null) return;
            var events = _sim.Events;
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                Handle(e);
                EventHook?.Invoke(e);
            }
            events.Clear();
        }

        private void Handle(SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.Spawn:
                case SimEventType.Revive:
                    CreateView(e.A);
                    if (e.Type == SimEventType.Revive) Vfx.Emit(VfxType.Heal, new Vector3(e.X, 0.6f, e.Y), new Color(0.5f, 1f, 0.7f), 12);
                    break;

                case SimEventType.Attack:
                    SetPulse(e.A);
                    break;

                case SimEventType.ProjectileLaunch:
                    var tr = _projectilePool.Count > 0 ? _projectilePool.Pop() : CreateProjectile();
                    tr.gameObject.SetActive(true);
                    tr.position = new Vector3(e.X, 1f, e.Y);
                    _projectiles[e.A] = tr;
                    break;

                case SimEventType.ProjectileEnd:
                    if (_projectiles.TryGetValue(e.A, out var ptr))
                    {
                        ReleaseProjectile(ptr);
                        _projectiles.Remove(e.A);
                    }
                    break;

                case SimEventType.Hit:
                    if (e.Value >= _cfg.damageNumberMinValue)
                    {
                        var at = new Vector3(e.X, 1.2f, e.Y);
                        Vfx.Emit(VfxType.Hit, at, e.Team == 0 ? new Color(1f, 0.5f, 0.4f) : Color.white, 3);
                        _numbers.Show(at, e.Value, e.Team == 0 ? new Color(1f, 0.45f, 0.4f) : Color.white);
                        Sfx.Play(SfxId.Hit, 0.5f);
                    }
                    break;

                case SimEventType.Heal:
                    _numbers.Show(new Vector3(e.X, 1.4f, e.Y), e.Value, new Color(0.4f, 1f, 0.55f));
                    Vfx.Emit(VfxType.Heal, new Vector3(e.X, 0.5f, e.Y), new Color(0.5f, 1f, 0.7f), 2);
                    break;

                case SimEventType.Death:
                    OnDeath(e);
                    break;

                case SimEventType.Telegraph:
                    AddTelegraph(e);
                    break;

                case SimEventType.Slam:
                    Vfx.Emit(VfxType.Explosion, new Vector3(e.X, 0.4f, e.Y), new Color(1f, 0.55f, 0.2f), 40);
                    if (ArenaCamera.Instance != null) ArenaCamera.Instance.Shake(0.35f, 0.3f);
                    Haptics.Heavy();
                    break;

                case SimEventType.SkillCast:
                    if (e.A < 0)
                    {
                        Vfx.Emit(VfxType.Explosion, new Vector3(e.X, 0.4f, e.Y), new Color(1f, 0.4f, 0.1f), 60);
                        if (ArenaCamera.Instance != null) ArenaCamera.Instance.Shake(0.3f, 0.25f);
                        Haptics.Heavy();
                    }
                    break;

                case SimEventType.SkillBolt:
                    Vfx.Emit(VfxType.Glow, new Vector3(e.X, 1f, e.Y), new Color(0.7f, 0.6f, 1f), 8);
                    break;

                case SimEventType.BossEnrage:
                    if (TryGetWorldPosition(e.A, out var bp)) Vfx.Emit(VfxType.Explosion, bp, new Color(1f, 0.1f, 0.1f), 30);
                    break;
            }
        }

        private void CreateView(int unitId)
        {
            var u = _sim.GetUnit(unitId);
            if (u == null) return;
            while (_views.Count < unitId) { _views.Add(null); _pulse.Add(0f); }
            if (_views[unitId - 1] != null) return;

            var line = _db.GetLine((UnitLineId)u.Line);
            int level = Mathf.Clamp(u.Level, 1, line.MaxLevel);
            var v = UnitVisualFactory.Get(line, level, u.Team == Team.Enemy, transform);
            v.BaseScale = u.Scale;
            v.transform.position = new Vector3(u.Pos.x, v.HoverHeight, u.Pos.y);
            v.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0f, u.Team == Team.Player ? 1f : -1f));
            v.transform.localScale = Vector3.one * u.Scale;
            v.HideLabel();
            _views[unitId - 1] = v;
            _pulse[unitId - 1] = 0f;
        }

        private void SetPulse(int unitId)
        {
            if (unitId > 0 && unitId <= _pulse.Count) _pulse[unitId - 1] = 0.14f;
        }

        private void OnDeath(SimEvent e)
        {
            int idx = e.A - 1;
            if (idx < 0 || idx >= _views.Count || _views[idx] == null) return;
            var v = _views[idx];
            _views[idx] = null;
            _dying.Add(new Dying { Visual = v, Time = 0f, Scale = v.transform.localScale });
            Vfx.Emit(VfxType.Death, new Vector3(e.X, 0.6f, e.Y), v.Tint, 14);
            Haptics.Light();
            Sfx.Play(SfxId.Death, 0.6f);
        }

        private void AddTelegraph(SimEvent e)
        {
            var ring = _ringPool.Count > 0 ? _ringPool.Pop() : CreateRing();
            ring.gameObject.SetActive(true);
            ring.position = new Vector3(e.X, 0.16f, e.Y);
            _telegraphs.Add(new Telegraph { Ring = ring, Time = 0f, Duration = e.A < 0 ? 0.7f : 0.9f, Radius = e.Value });
        }

        // ------------------------------------------------------------------ small pooled props

        private Transform CreateProjectile()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * 0.3f;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = MaterialLibrary.Lit(new Color(1f, 0.9f, 0.4f));
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        private void ReleaseProjectile(Transform t)
        {
            t.gameObject.SetActive(false);
            _projectilePool.Push(t);
        }

        private Transform CreateRing()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = _redDisc;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        private void ReleaseRing(Transform t)
        {
            t.gameObject.SetActive(false);
            _ringPool.Push(t);
        }
    }
}
