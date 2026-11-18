using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BomberRally
{
    /// <summary>
    /// ガソリンスタンドの店員。普段は ScaredBystander で青ざめて見守り、給油機が燃えると消火器を持って駆けつける。
    /// 中央のプレイヤーの間は通らず、ステージの外周を回り込んで向かう。消火器を吹きかけている間だけ炎が少しずつ小さくなり、消えたら持ち場へ戻る。燃えている給油機が複数あれば順番に消す。
    /// </summary>
    public class GasStationStaff : MonoBehaviour
    {
        [Tooltip("爆発してから走り出すまでの秒数（一瞬すくむ）")]
        [SerializeField] private float reactionDelay = 0.5f;
        [SerializeField] private float runSpeed = 7f;
        [Tooltip("給油機のどれくらい手前で止まるか")]
        [SerializeField] private float stopDistance = 2f;
        [Tooltip("最大の炎を消し切るまでの秒数")]
        [SerializeField] private float secondsToExtinguish = 3.5f;
        [Tooltip("消火器の噴射口")]
        [SerializeField] private Transform nozzle;

        [Header("回り道（ステージ中央のプレイヤーを避ける）")]
        [Tooltip("ステージ中央からこの半径の円に沿って外側を回る")]
        [SerializeField] private float detourRadius = 9.2f;
        [Tooltip("まっすぐ向かう線がこの半径より中央に近づくなら回り道する")]
        [SerializeField] private float safeRadius = 8f;
        [Tooltip("これより手前（道路側）には出ない（ステージ基準の z）")]
        [SerializeField] private float frontLimit = -6.6f;
        [Tooltip("回り道の円を何度刻みで進むか")]
        [SerializeField] private float detourStepDegrees = 10f;
        [Tooltip("向きを変える速さ（度/秒）")]
        [SerializeField] private float turnSpeed = 900f;

        private readonly List<FuelPump> queue = new List<FuelPump>();
        private Vector3 home;
        private Quaternion homeRotation;
        private ParticleSystem spray;
        private Coroutine work;

        private void Awake()
        {
            home = transform.position;
            homeRotation = transform.rotation;
            spray = EffectParticles.Create(transform, "ExtinguisherSpray", EffectAssets.Smoke, ParticleSystemRenderMode.Billboard, 300);
            EffectParticles.SetOverLifetime(spray, EffectParticles.PlainFade(), AnimationCurve.Linear(0f, 0.6f, 1f, 2.2f));
            EffectParticles.SetDrag(spray, 3f);
        }

        /// <summary>燃えている給油機を消しに行く。</summary>
        public void Extinguish(FuelPump pump)
        {
            if (pump == null) return;
            if (!queue.Contains(pump)) queue.Add(pump);
            if (work == null) work = StartCoroutine(Work());
        }

        /// <summary>すぐに持ち場へ戻す（試合のやり直しなど）。</summary>
        public void ReturnHome()
        {
            if (work != null) StopCoroutine(work);
            work = null;
            queue.Clear();
            transform.SetPositionAndRotation(home, homeRotation);
        }

        private IEnumerator Work()
        {
            yield return new WaitForSeconds(reactionDelay);
            while (true)
            {
                yield return ExtinguishQueued();
                // 持ち場へ戻る途中で別の給油機が燃えたら、戻るのをやめてすぐそちらへ向かう
                bool interrupted = false;
                foreach (var point in PlanRoute(transform.position, home))
                {
                    while (!interrupted && (transform.position - point).sqrMagnitude > 0.01f)
                    {
                        if (queue.Count > 0) { interrupted = true; break; }
                        StepTowards(point);
                        yield return null;
                    }
                    if (interrupted) break;
                }
                if (!interrupted) break;
            }
            transform.rotation = homeRotation;
            work = null;
        }

        /// <summary>燃えた順に給油機を消して回る。</summary>
        private IEnumerator ExtinguishQueued()
        {
            while (queue.Count > 0)
            {
                var pump = queue[0];
                queue.RemoveAt(0);
                if (pump == null || !pump.IsBurning) continue;

                var fire = pump.transform.position;
                fire.y = home.y;
                yield return RunAlong(PlanRoute(transform.position, StandPoint(fire)));

                while (pump.IsBurning)
                {
                    Face(fire);
                    EmitSpray(pump.BlastPoint);
                    pump.Extinguish(Time.deltaTime / Mathf.Max(0.1f, secondsToExtinguish));
                    yield return null;
                }
                // 消えたあとも少しだけ吹きかけて念押しする
                for (float t = 0f; t < 0.4f; t += Time.deltaTime)
                {
                    EmitSpray(pump.BlastPoint);
                    yield return null;
                }
            }
        }

        private void EmitSpray(Vector3 target)
        {
            for (int i = 0; i < 2; i++)
            {
                var from = nozzle != null ? nozzle.position : transform.position + Vector3.up;
                var dir = (target - from).normalized;
                var vel = (dir + Random.insideUnitSphere * 0.2f) * Random.Range(6f, 9f);
                EffectParticles.Emit(spray, from, vel, Random.Range(0.4f, 0.7f), 0.7f, new Color(0.95f, 0.95f, 0.95f, 0.8f));
            }
        }

        /// <summary>
        /// 消火するときに立つ場所。給油機の外側（プレイヤーと反対側）に立つ。
        /// 外側が道路になる手前の給油機だけは、店員の持ち場に近い側の横に立つ。
        /// </summary>
        private Vector3 StandPoint(Vector3 fireWorld)
        {
            var fire = ToStage(fireWorld);
            var outward = fire.sqrMagnitude > 0.01f ? fire.normalized : Vector3.back;
            var stand = fire + outward * stopDistance;
            if (stand.z < frontLimit)
            {
                var side = new Vector3(outward.z, 0f, -outward.x);
                if (Vector3.Dot(side, ToStage(home) - fire) < 0f) side = -side;
                stand = fire + side * stopDistance;
            }
            return ToWorld(stand);
        }

        /// <summary>
        /// from から to までの道順。まっすぐ行くとプレイヤーのいる中央に近づく場合は、
        /// ステージの外周（detourRadius の円）に沿って回り込む。道路側には出ない。
        /// </summary>
        private List<Vector3> PlanRoute(Vector3 fromWorld, Vector3 toWorld)
        {
            var route = new List<Vector3>();
            var from = ToStage(fromWorld);
            var to = ToStage(toWorld);
            if (DistanceFromCenter(from, to) >= safeRadius)
            {
                route.Add(toWorld);
                return route;
            }

            float a0 = Mathf.Atan2(from.z, from.x) * Mathf.Rad2Deg;
            float a1 = Mathf.Atan2(to.z, to.x) * Mathf.Rad2Deg;
            float delta = Mathf.DeltaAngle(a0, a1);
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(delta) / Mathf.Max(1f, detourStepDegrees)));
            for (int i = 0; i <= steps; i++)
            {
                float a = (a0 + delta * i / steps) * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * detourRadius;
                p.z = Mathf.Max(p.z, frontLimit);
                route.Add(ToWorld(p));
            }
            route.Add(toWorld);
            return route;
        }

        /// <summary>線分 a-b がステージ中央にいちばん近づく距離。</summary>
        private static float DistanceFromCenter(Vector3 a, Vector3 b)
        {
            var ab = b - a;
            float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(-Vector3.Dot(a, ab) / ab.sqrMagnitude) : 0f;
            return (a + ab * t).magnitude;
        }

        // ステージの中心（親）を原点にした水平な座標との変換
        private Vector3 ToStage(Vector3 world)
        {
            var local = transform.parent != null ? transform.parent.InverseTransformPoint(world) : world;
            local.y = 0f;
            return local;
        }

        private Vector3 ToWorld(Vector3 stage)
        {
            var world = transform.parent != null ? transform.parent.TransformPoint(stage) : stage;
            world.y = home.y;
            return world;
        }

        private IEnumerator RunAlong(List<Vector3> route)
        {
            foreach (var point in route) yield return RunTo(point);
        }

        private IEnumerator RunTo(Vector3 goal)
        {
            while ((transform.position - goal).sqrMagnitude > 0.01f)
            {
                StepTowards(goal);
                yield return null;
            }
        }

        private void StepTowards(Vector3 goal)
        {
            var dir = goal - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), turnSpeed * Time.deltaTime);
            }
            transform.position = Vector3.MoveTowards(transform.position, goal, runSpeed * Time.deltaTime);
        }

        private void Face(Vector3 point)
        {
            var dir = point - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir);
        }
    }
}
