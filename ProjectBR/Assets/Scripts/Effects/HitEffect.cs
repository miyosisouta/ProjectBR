using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace BomberRally
{
    /// <summary>
    /// 打ち返したときのエフェクト。通常は控えめな輪と火花、スマッシュは少し大きく暖色にして閃光と小さな画面揺れを足す。
    /// 毎回出るものなので、パーティクルは都度生成せず、最初に作った ParticleSystem から Emit するだけ。
    /// </summary>
    public class HitEffect : MonoBehaviour
    {
        [Serializable]
        public struct Style
        {
            [Tooltip("輪と閃光の色（火花は少し濃くなる）")]
            public Color color;
            [Tooltip("輪が広がりきったときの大きさ")]
            public float ringSize;
            public float ringLifetime;
            public int sparkCount;
            public float sparkSpeed;
            public float sparkSize;
            public float sparkLifetime;
            [Tooltip("打った方向から左右に火花が散る角度")]
            public float sparkSpread;
            [Tooltip("中心の閃光の大きさ（0 なら出さない）")]
            public float flashSize;
            [Tooltip("内側にもう一つ出す輪の大きさの倍率（0 なら出さない）")]
            public float secondRingScale;
            [Tooltip("全方向に放射状に伸びる線の数（0 なら出さない）")]
            public int burstCount;
            public float burstSpeed;
            public Color burstColor;
        }

        [SerializeField]
        private Style normal = new Style
        {
            color = new Color(0.8f, 0.92f, 1f, 0.35f),
            ringSize = 0.9f,
            ringLifetime = 0.14f,
            sparkCount = 3,
            sparkSpeed = 4f,
            sparkSize = 0.05f,
            sparkLifetime = 0.14f,
            sparkSpread = 50f,
            flashSize = 0f,
            secondRingScale = 0f,
            burstCount = 0,
        };

        [SerializeField]
        private Style smash = new Style
        {
            color = new Color(1f, 0.72f, 0.3f, 0.8f),
            ringSize = 1.9f,
            ringLifetime = 0.22f,
            sparkCount = 12,
            sparkSpeed = 9f,
            sparkSize = 0.09f,
            sparkLifetime = 0.24f,
            sparkSpread = 35f,
            flashSize = 1.5f,
            secondRingScale = 0.55f,
            burstCount = 8,
            burstSpeed = 16f,
            burstColor = new Color(1f, 0.95f, 0.75f, 0.9f),
        };

        [Header("スマッシュの画面揺れ（0 で揺らさない）")]
        [SerializeField] private float shakeAmplitude = 0.05f;
        [SerializeField] private float shakeDuration = 0.1f;

        private ParticleSystem ringSystem;
        private ParticleSystem sparkSystem;
        private ParticleSystem flashSystem;

        private Transform shakeTarget;
        private Vector3 shakeOrigin;
        private float shakeEnd = -1f;

        private void Awake()
        {
            // 輪は勢いよく広がってから止まる
            var ringGrow = new AnimationCurve(new Keyframe(0f, 0.35f, 2.5f, 2.5f), new Keyframe(1f, 1f, 0f, 0f));
            var shrink = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            var flashPop = new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.8f));

            ringSystem = CreateSystem("HitRing", EffectAssets.Ring, ringGrow, ParticleSystemRenderMode.Billboard);
            sparkSystem = CreateSystem("HitSparks", EffectAssets.SoftDot, shrink, ParticleSystemRenderMode.Stretch);
            flashSystem = CreateSystem("HitFlash", EffectAssets.SoftDot, flashPop, ParticleSystemRenderMode.Billboard);

            // 火花は飛び出してすぐ減速させ、遠くまで飛ばない
            var limit = sparkSystem.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 100f;
            limit.drag = 10f;
            limit.multiplyDragByParticleSize = false;
            limit.multiplyDragByParticleVelocity = false;

            var sparkRenderer = sparkSystem.GetComponent<ParticleSystemRenderer>();
            sparkRenderer.velocityScale = 0.035f;
            sparkRenderer.lengthScale = 1.5f;
        }

        /// <param name="hitPoint">打った位置</param>
        /// <param name="towards">打ち返した先（火花の向きに使う）</param>
        public void Play(Vector3 hitPoint, Vector3 towards, bool isSmash)
        {
            var style = isSmash ? smash : normal;
            var dir = towards - hitPoint;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;

            ringSystem.Emit(new ParticleSystem.EmitParams
            {
                position = hitPoint,
                velocity = Vector3.zero,
                startSize = style.ringSize,
                startLifetime = style.ringLifetime,
                startColor = style.color,
                applyShapeToPosition = false,
            }, 1);

            // 内側の輪は少し遅れて広がるように見せるため、長めに残す
            if (style.secondRingScale > 0f)
            {
                ringSystem.Emit(new ParticleSystem.EmitParams
                {
                    position = hitPoint,
                    velocity = Vector3.zero,
                    startSize = style.ringSize * style.secondRingScale,
                    startLifetime = style.ringLifetime * 1.3f,
                    startColor = style.color,
                    applyShapeToPosition = false,
                }, 1);
            }

            if (style.burstCount > 0) EmitBurst(hitPoint, style);

            if (style.flashSize > 0f)
            {
                flashSystem.Emit(new ParticleSystem.EmitParams
                {
                    position = hitPoint,
                    velocity = Vector3.zero,
                    startSize = style.flashSize,
                    startLifetime = style.ringLifetime * 0.5f,
                    startColor = style.color,
                    applyShapeToPosition = false,
                }, 1);
            }

            var sparkColor = style.color;
            sparkColor.a = Mathf.Min(1f, sparkColor.a + 0.3f);
            for (int i = 0; i < style.sparkCount; i++)
            {
                var sparkDir = Quaternion.AngleAxis(UnityEngine.Random.Range(-style.sparkSpread, style.sparkSpread), Vector3.up) * dir;
                sparkDir.y = UnityEngine.Random.Range(-0.2f, 0.5f);
                sparkSystem.Emit(new ParticleSystem.EmitParams
                {
                    position = hitPoint + dir * 0.2f,
                    velocity = sparkDir.normalized * style.sparkSpeed * UnityEngine.Random.Range(0.6f, 1f),
                    startSize = style.sparkSize,
                    startLifetime = style.sparkLifetime * UnityEngine.Random.Range(0.7f, 1f),
                    startColor = sparkColor,
                    applyShapeToPosition = false,
                }, 1);
            }

            if (isSmash) StartShake();
        }

        /// <summary>画面に対して全方向へ短い線を放射する（漫画の集中線のような打撃の印）。</summary>
        private void EmitBurst(Vector3 hitPoint, Style style)
        {
            var cam = Camera.main;
            var right = cam != null ? cam.transform.right : Vector3.right;
            var up = cam != null ? cam.transform.up : Vector3.up;
            float offset = UnityEngine.Random.Range(0f, 360f);

            for (int i = 0; i < style.burstCount; i++)
            {
                float angle = (offset + 360f * i / style.burstCount + UnityEngine.Random.Range(-8f, 8f)) * Mathf.Deg2Rad;
                var dir = right * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                sparkSystem.Emit(new ParticleSystem.EmitParams
                {
                    position = hitPoint + dir * 0.25f,
                    velocity = dir * style.burstSpeed * UnityEngine.Random.Range(0.85f, 1f),
                    startSize = 0.07f,
                    startLifetime = 0.12f,
                    startColor = style.burstColor,
                    applyShapeToPosition = false,
                }, 1);
            }
        }

        private void StartShake()
        {
            if (shakeAmplitude <= 0f || shakeDuration <= 0f) return;
            var cam = Camera.main;
            if (cam == null) return;

            // 揺れている最中に重なったときは元の位置を取り直さない
            if (shakeEnd < 0f || shakeTarget != cam.transform)
            {
                shakeTarget = cam.transform;
                shakeOrigin = shakeTarget.position;
            }
            shakeEnd = Time.time + shakeDuration;
        }

        private void LateUpdate()
        {
            if (shakeEnd < 0f || shakeTarget == null) return;
            if (Time.time >= shakeEnd)
            {
                shakeTarget.position = shakeOrigin;
                shakeEnd = -1f;
                return;
            }

            float k = (shakeEnd - Time.time) / shakeDuration;
            var offset = UnityEngine.Random.insideUnitCircle * shakeAmplitude * k;
            shakeTarget.position = shakeOrigin + shakeTarget.right * offset.x + shakeTarget.up * offset.y;
        }

        private void OnDisable()
        {
            if (shakeEnd >= 0f && shakeTarget != null) shakeTarget.position = shakeOrigin;
            shakeEnd = -1f;
        }

        /// <summary>自分では出さず、Emit されたものだけを動かす ParticleSystem。</summary>
        private ParticleSystem CreateSystem(string systemName, Material material, AnimationCurve size, ParticleSystemRenderMode mode)
        {
            var go = new GameObject(systemName);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.gravityModifier = 0f;
            main.maxParticles = 64;

            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;

            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.3f), new GradientAlphaKey(0f, 1f) });
            var color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = fade;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, size);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = mode;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            ps.Play();
            return ps;
        }
    }
}
