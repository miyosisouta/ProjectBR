using UnityEngine;

namespace BomberRally
{
    /// <summary>
    /// 打ち返したときのエフェクト。通常は控えめな輪と火花、スマッシュは少し大きく暖色にして閃光と小さな画面揺れを足す。
    /// 毎回出るものなので、パーティクルは都度生成せず、最初に作った ParticleSystem から Emit するだけ。
    /// 数値は HitEffectSettings にあり、再生のたびに読み直すので再生中でも調整できる。
    /// </summary>
    public class HitEffect : MonoBehaviour
    {
        [Tooltip("未設定なら Resources/" + HitEffectSettings.ResourcePath + " を使う")]
        [SerializeField] private HitEffectSettings settings;

        private ParticleSystem ringSystem;
        private ParticleSystem sparkSystem;
        private ParticleSystem flashSystem;

        private Transform shakeTarget;
        private Vector3 shakeOrigin;
        private float shakeEnd = -1f;

        public HitEffectSettings Settings
        {
            get
            {
                if (settings == null) settings = Resources.Load<HitEffectSettings>(HitEffectSettings.ResourcePath);
                // アセットが見つからなくても既定値で動かす
                if (settings == null) settings = ScriptableObject.CreateInstance<HitEffectSettings>();
                return settings;
            }
            set => settings = value;
        }

        private void Awake()
        {
            ringSystem = EffectParticles.Create(transform, "HitRing", EffectAssets.Ring, ParticleSystemRenderMode.Billboard, 64);
            sparkSystem = EffectParticles.Create(transform, "HitSparks", EffectAssets.SoftDot, ParticleSystemRenderMode.Stretch, 64);
            flashSystem = EffectParticles.Create(transform, "HitFlash", EffectAssets.SoftDot, ParticleSystemRenderMode.Billboard, 64);
        }

        /// <summary>設定アセットの値をパーティクルに反映する。値を変えてもすぐ効くよう、再生のたびに呼ぶ。</summary>
        private void ApplySettings(HitEffectSettings s)
        {
            EffectParticles.SetOverLifetime(ringSystem, s.fade, s.ringSizeCurve);
            EffectParticles.SetOverLifetime(sparkSystem, s.fade, s.sparkSizeCurve);
            EffectParticles.SetOverLifetime(flashSystem, s.fade, s.flashSizeCurve);
            EffectParticles.SetDrag(sparkSystem, s.sparkDrag);

            var sparkRenderer = sparkSystem.GetComponent<ParticleSystemRenderer>();
            sparkRenderer.velocityScale = s.sparkVelocityStretch;
            sparkRenderer.lengthScale = s.sparkLengthStretch;
        }

        /// <param name="hitPoint">打った位置</param>
        /// <param name="towards">打ち返した先（火花の向きに使う）</param>
        public void Play(Vector3 hitPoint, Vector3 towards, bool isSmash)
        {
            var s = Settings;
            ApplySettings(s);

            var style = isSmash ? s.smash : s.normal;
            var dir = towards - hitPoint;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;

            EffectParticles.Emit(ringSystem, hitPoint, Vector3.zero, style.ringSize, style.ringLifetime, style.color);

            // 内側の輪は少し遅れて広がるように見せるため、長めに残す
            if (style.secondRingScale > 0f)
            {
                EffectParticles.Emit(ringSystem, hitPoint, Vector3.zero,
                    style.ringSize * style.secondRingScale, style.ringLifetime * style.secondRingLifetimeScale, style.color);
            }

            if (style.burstCount > 0) EmitBurst(hitPoint, style);

            if (style.flashSize > 0f)
            {
                EffectParticles.Emit(flashSystem, hitPoint, Vector3.zero, style.flashSize, style.flashLifetime, style.color);
            }

            var sparkColor = style.color;
            sparkColor.a = Mathf.Min(1f, sparkColor.a + style.sparkAlphaBoost);
            for (int i = 0; i < style.sparkCount; i++)
            {
                var sparkDir = Quaternion.AngleAxis(Random.Range(-style.sparkSpread, style.sparkSpread), Vector3.up) * dir;
                sparkDir.y = Random.Range(s.sparkVerticalRange.x, s.sparkVerticalRange.y);
                EffectParticles.Emit(sparkSystem,
                    hitPoint + dir * 0.2f,
                    sparkDir.normalized * style.sparkSpeed * Random.Range(0.6f, 1f),
                    style.sparkSize,
                    style.sparkLifetime * Random.Range(0.7f, 1f),
                    sparkColor);
            }

            if (isSmash) StartShake(s);
        }

        /// <summary>画面に対して全方向へ短い線を放射する（漫画の集中線のような打撃の印）。</summary>
        private void EmitBurst(Vector3 hitPoint, HitEffectSettings.Style style)
        {
            var cam = Camera.main;
            var right = cam != null ? cam.transform.right : Vector3.right;
            var up = cam != null ? cam.transform.up : Vector3.up;
            float offset = Random.Range(0f, 360f);

            for (int i = 0; i < style.burstCount; i++)
            {
                float angle = (offset + 360f * i / style.burstCount + Random.Range(-8f, 8f)) * Mathf.Deg2Rad;
                var dir = right * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                EffectParticles.Emit(sparkSystem,
                    hitPoint + dir * 0.25f,
                    dir * style.burstSpeed * Random.Range(0.85f, 1f),
                    style.burstSize,
                    style.burstLifetime,
                    style.burstColor);
            }
        }

        private void StartShake(HitEffectSettings s)
        {
            if (s.shakeAmplitude <= 0f || s.shakeDuration <= 0f) return;
            var cam = Camera.main;
            if (cam == null) return;

            // 揺れている最中に重なったときは元の位置を取り直さない
            if (shakeEnd < 0f || shakeTarget != cam.transform)
            {
                shakeTarget = cam.transform;
                shakeOrigin = shakeTarget.position;
            }
            shakeEnd = Time.time + s.shakeDuration;
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

            var s = Settings;
            float k = (shakeEnd - Time.time) / Mathf.Max(0.0001f, s.shakeDuration);
            var offset = Random.insideUnitCircle * s.shakeAmplitude * Mathf.Clamp01(k);
            shakeTarget.position = shakeOrigin + shakeTarget.right * offset.x + shakeTarget.up * offset.y;
        }

        private void OnDisable()
        {
            if (shakeEnd >= 0f && shakeTarget != null) shakeTarget.position = shakeOrigin;
            shakeEnd = -1f;
        }
    }
}
