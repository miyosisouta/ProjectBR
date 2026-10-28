using System.Collections;
using UnityEngine;

namespace BomberRally
{
    /// <summary>
    /// 打ち返せずに爆弾が爆発したときのエフェクト。ゲームの山場なので思い切り派手にする。
    /// 閃光 → 火球 → 衝撃波と火花 → 遅れて小爆発 → 煙 の順に重ね、
    /// ヒットストップ・画面フラッシュ・強い画面揺れ・光源の明滅で手応えを出す。
    /// HitEffect と同じく、ParticleSystem は最初に作って Emit するだけ。
    /// 数値は ExplosionEffectSettings にあり、再生のたびに読み直すので再生中でも調整できる。
    /// </summary>
    public class ExplosionEffect : MonoBehaviour
    {
        [Tooltip("未設定なら Resources/" + ExplosionEffectSettings.ResourcePath + " を使う")]
        [SerializeField] private ExplosionEffectSettings settings;

        private ParticleSystem flashSystem;
        private ParticleSystem fireSystem;
        private ParticleSystem smokeSystem;
        private ParticleSystem sparkSystem;
        private ParticleSystem ringSystem;
        private ParticleSystem groundRingSystem;

        private Light flashLight;
        private float lightStart = -1f;

        private bool hitStopping;
        private float timeScaleBeforeStop = 1f;
        private float screenFlashStart = -1f;

        private Transform shakeTarget;
        private Vector3 shakeOrigin;
        private float shakeStart = -1f;
        private float shakeSeed;

        public ExplosionEffectSettings Settings
        {
            get
            {
                if (settings == null) settings = Resources.Load<ExplosionEffectSettings>(ExplosionEffectSettings.ResourcePath);
                // アセットが見つからなくても既定値で動かす
                if (settings == null) settings = ScriptableObject.CreateInstance<ExplosionEffectSettings>();
                return settings;
            }
            set => settings = value;
        }

        private void Awake()
        {
            flashSystem = EffectParticles.Create(transform, "ExplosionFlash", EffectAssets.SoftDot, ParticleSystemRenderMode.Billboard, 64);
            fireSystem = EffectParticles.Create(transform, "ExplosionFire", EffectAssets.SoftDot, ParticleSystemRenderMode.Billboard, 1000);
            smokeSystem = EffectParticles.Create(transform, "ExplosionSmoke", EffectAssets.Smoke, ParticleSystemRenderMode.Billboard, 500);
            sparkSystem = EffectParticles.Create(transform, "ExplosionSparks", EffectAssets.SoftDot, ParticleSystemRenderMode.Stretch, 1000);
            ringSystem = EffectParticles.Create(transform, "ExplosionRing", EffectAssets.Ring, ParticleSystemRenderMode.Billboard, 64);
            groundRingSystem = EffectParticles.Create(transform, "ExplosionGroundRing", EffectAssets.Ring, ParticleSystemRenderMode.HorizontalBillboard, 64);

            // 煙は火より奥に描く
            smokeSystem.GetComponent<ParticleSystemRenderer>().sortingFudge = 40f;
            var smokeRotation = smokeSystem.rotationOverLifetime;
            smokeRotation.enabled = true;

            var lightGo = new GameObject("ExplosionLight");
            lightGo.transform.SetParent(transform, false);
            flashLight = lightGo.AddComponent<Light>();
            flashLight.type = LightType.Point;
            flashLight.shadows = LightShadows.None;
            flashLight.intensity = 0f;
            flashLight.enabled = false;
        }

        /// <summary>設定アセットの値をパーティクルと光源に反映する。値を変えてもすぐ効くよう、再生のたびに呼ぶ。</summary>
        private void ApplySettings(ExplosionEffectSettings s)
        {
            EffectParticles.SetOverLifetime(flashSystem, s.fade, s.flashSizeCurve);
            EffectParticles.SetOverLifetime(fireSystem, s.fireGradient, s.fireSizeCurve);
            EffectParticles.SetOverLifetime(smokeSystem, s.smokeGradient, s.smokeSizeCurve);
            EffectParticles.SetOverLifetime(sparkSystem, s.fade, s.sparkSizeCurve);
            EffectParticles.SetOverLifetime(ringSystem, s.fade, s.ringSizeCurve);
            EffectParticles.SetOverLifetime(groundRingSystem, s.fade, s.ringSizeCurve);

            EffectParticles.SetDrag(fireSystem, s.fireDrag);
            EffectParticles.SetDrag(smokeSystem, s.smokeDrag);
            EffectParticles.SetDrag(sparkSystem, s.sparkDrag);

            var smokeMain = smokeSystem.main;
            smokeMain.gravityModifier = -s.smokeRise;
            var smokeRotation = smokeSystem.rotationOverLifetime;
            smokeRotation.z = new ParticleSystem.MinMaxCurve(-s.smokeSpin, s.smokeSpin);

            var sparkMain = sparkSystem.main;
            sparkMain.gravityModifier = s.sparkGravity;
            var sparkRenderer = sparkSystem.GetComponent<ParticleSystemRenderer>();
            sparkRenderer.velocityScale = s.sparkVelocityStretch;
            sparkRenderer.lengthScale = s.sparkLengthStretch;

            flashLight.color = s.lightColor;
            flashLight.range = s.lightRange * s.scale;
        }

        /// <param name="position">爆発の中心</param>
        /// <param name="groundY">地面の高さ（地面を這う衝撃波に使う）</param>
        public void Play(Vector3 position, float groundY)
        {
            var s = Settings;
            ApplySettings(s);

            EmitFlash(s, position, s.scale);
            EmitFire(s, position, s.fireCount, s.scale);
            EmitSparks(s, position);
            EmitShockwave(s, position, groundY);
            EmitSmoke(s, position);

            if (s.aftershockCount > 0) StartCoroutine(Aftershocks(s, position));

            if (s.lightIntensity > 0f && s.lightDuration > 0f)
            {
                flashLight.transform.position = position + Vector3.up * 0.5f;
                flashLight.enabled = true;
                lightStart = Time.time;
            }

            if (s.screenFlashAlpha > 0f && s.screenFlashDuration > 0f) screenFlashStart = Time.unscaledTime;
            StartShake(s);
            StartHitStop(s);
        }

        private void EmitFlash(ExplosionEffectSettings s, Vector3 position, float size)
        {
            // 大きな白い光と、それより長く残る黄色い芯
            if (s.flashSize > 0f) EffectParticles.Emit(flashSystem, position, Vector3.zero, s.flashSize * size, s.flashLifetime, s.flashColor);
            if (s.coreSize > 0f) EffectParticles.Emit(flashSystem, position, Vector3.zero, s.coreSize * size, s.coreLifetime, s.coreColor);
        }

        private void EmitFire(ExplosionEffectSettings s, Vector3 center, int count, float size)
        {
            for (int i = 0; i < count; i++)
            {
                var dir = Random.onUnitSphere;
                // 地面に潜らないよう上半分寄りに飛ばす
                dir.y = Mathf.Abs(dir.y) * 0.8f + 0.1f;
                dir.Normalize();
                EffectParticles.Emit(fireSystem,
                    center + dir * Random.Range(0f, 0.4f) * size,
                    dir * s.fireSpeed * Random.Range(0.4f, 1f) * size,
                    s.fireSize * Random.Range(0.6f, 1.2f) * size,
                    s.fireLifetime * Random.Range(0.6f, 1f),
                    Color.Lerp(s.fireColorA, s.fireColorB, Random.value),
                    Random.Range(0f, 360f));
            }
        }

        private void EmitSparks(ExplosionEffectSettings s, Vector3 center)
        {
            for (int i = 0; i < s.sparkCount; i++)
            {
                var dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 0.9f + 0.15f;
                EffectParticles.Emit(sparkSystem,
                    center,
                    dir.normalized * s.sparkSpeed * Random.Range(0.35f, 1f) * s.scale,
                    Random.Range(s.sparkSize.x, s.sparkSize.y) * s.scale,
                    Random.Range(s.sparkLifetime.x, s.sparkLifetime.y),
                    s.sparkColor);
            }

            // 漫画の集中線のように画面に対して放射状に伸びる太い線
            if (s.lineCount <= 0) return;
            var cam = Camera.main;
            var right = cam != null ? cam.transform.right : Vector3.right;
            var up = cam != null ? cam.transform.up : Vector3.up;
            float offset = Random.Range(0f, 360f);
            for (int i = 0; i < s.lineCount; i++)
            {
                float angle = (offset + 360f * i / s.lineCount + Random.Range(-6f, 6f)) * Mathf.Deg2Rad;
                var dir = right * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                EffectParticles.Emit(sparkSystem,
                    center + dir * 0.6f * s.scale,
                    dir * s.lineSpeed * Random.Range(0.8f, 1f) * s.scale,
                    s.lineSize * s.scale,
                    s.lineLifetime,
                    s.lineColor);
            }
        }

        private void EmitShockwave(ExplosionEffectSettings s, Vector3 center, float groundY)
        {
            if (s.shockwaveSize <= 0f) return;
            var ground = new Vector3(center.x, groundY + 0.05f, center.z);
            float size = s.shockwaveSize * s.scale;
            EffectParticles.Emit(groundRingSystem, ground, Vector3.zero, size, s.shockwaveLifetime, s.shockwaveColor);
            if (s.innerRingScale > 0f) EffectParticles.Emit(groundRingSystem, ground, Vector3.zero, size * s.innerRingScale, s.innerRingLifetime, s.innerRingColor);
            if (s.airRingScale > 0f) EffectParticles.Emit(ringSystem, center, Vector3.zero, size * s.airRingScale, s.airRingLifetime, s.shockwaveColor);
        }

        private void EmitSmoke(ExplosionEffectSettings s, Vector3 center)
        {
            for (int i = 0; i < s.smokeCount; i++)
            {
                var dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 0.6f + 0.25f;
                var color = s.smokeColor * Random.Range(0.8f, 1.15f);
                color.a = s.smokeColor.a;
                EffectParticles.Emit(smokeSystem,
                    center + dir * Random.Range(0.2f, 0.8f) * s.scale,
                    dir.normalized * Random.Range(s.smokeSpeed.x, s.smokeSpeed.y) * s.scale,
                    s.smokeSize * Random.Range(0.7f, 1.2f) * s.scale,
                    s.smokeLifetime * Random.Range(0.7f, 1f),
                    color,
                    Random.Range(0f, 360f));
            }
        }

        /// <summary>本体の後に周りで小さな爆発を連続させ、爆発の余韻を出す。</summary>
        private IEnumerator Aftershocks(ExplosionEffectSettings s, Vector3 center)
        {
            for (int i = 0; i < s.aftershockCount; i++)
            {
                yield return new WaitForSeconds(Random.Range(s.aftershockInterval.x, s.aftershockInterval.y));
                var offset = Random.insideUnitSphere * s.aftershockRadius * s.scale;
                offset.y = Mathf.Abs(offset.y) + 0.3f;
                var pos = center + offset;
                float size = s.scale * Random.Range(s.aftershockSize.x, s.aftershockSize.y);
                EmitFlash(s, pos, size * 0.35f);
                EmitFire(s, pos, s.fireCount / 3, size);
                if (s.airRingScale > 0f) EffectParticles.Emit(ringSystem, pos, Vector3.zero, s.shockwaveSize * 0.3f * size, s.airRingLifetime * 0.7f, s.shockwaveColor);
            }
        }

        private void StartHitStop(ExplosionEffectSettings s)
        {
            if (s.hitStopDuration <= 0f || hitStopping) return;
            StartCoroutine(HitStop(s.hitStopDuration, s.hitStopTimeScale));
        }

        private IEnumerator HitStop(float duration, float timeScale)
        {
            hitStopping = true;
            timeScaleBeforeStop = Time.timeScale;
            Time.timeScale = timeScale;
            yield return new WaitForSecondsRealtime(duration);
            Time.timeScale = timeScaleBeforeStop;
            hitStopping = false;
        }

        private void StartShake(ExplosionEffectSettings s)
        {
            if (s.shakeAmplitude <= 0f || s.shakeDuration <= 0f) return;
            var cam = Camera.main;
            if (cam == null) return;

            // 揺れている最中に重なったときは元の位置を取り直さない
            if (shakeStart < 0f || shakeTarget != cam.transform)
            {
                shakeTarget = cam.transform;
                shakeOrigin = shakeTarget.position;
            }
            shakeStart = Time.unscaledTime;
            shakeSeed = Random.Range(0f, 100f);
        }

        private void Update()
        {
            if (lightStart < 0f) return;
            var s = Settings;
            float t = (Time.time - lightStart) / Mathf.Max(0.0001f, s.lightDuration);
            if (t >= 1f)
            {
                flashLight.enabled = false;
                lightStart = -1f;
                return;
            }
            // 一瞬で最大になり、ちらつきながら消える
            float flicker = 0.8f + 0.2f * Mathf.PerlinNoise(Time.time * 40f, 0f);
            flashLight.intensity = s.lightIntensity * Mathf.Pow(1f - t, 2f) * flicker;
        }

        private void LateUpdate()
        {
            // ヒットストップ中も揺らしたいので実時間で動かす
            if (shakeStart < 0f || shakeTarget == null) return;
            var s = Settings;
            float elapsed = Time.unscaledTime - shakeStart;
            if (elapsed >= s.shakeDuration)
            {
                shakeTarget.position = shakeOrigin;
                shakeStart = -1f;
                return;
            }

            float k = 1f - elapsed / s.shakeDuration;
            float n = elapsed * s.shakeFrequency;
            var offset = new Vector2(Mathf.PerlinNoise(shakeSeed, n) - 0.5f, Mathf.PerlinNoise(shakeSeed + 50f, n) - 0.5f) * 2f;
            offset *= s.shakeAmplitude * k * k;
            shakeTarget.position = shakeOrigin + shakeTarget.right * offset.x + shakeTarget.up * offset.y;
        }

        private void OnGUI()
        {
            if (screenFlashStart < 0f || Event.current.type != EventType.Repaint) return;
            var s = Settings;
            float t = (Time.unscaledTime - screenFlashStart) / Mathf.Max(0.0001f, s.screenFlashDuration);
            if (t >= 1f)
            {
                screenFlashStart = -1f;
                return;
            }
            var prev = GUI.color;
            var c = s.screenFlashColor;
            c.a = s.screenFlashAlpha * (1f - t) * (1f - t);
            GUI.color = c;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        private void OnDisable()
        {
            if (hitStopping) Time.timeScale = timeScaleBeforeStop;
            hitStopping = false;
            if (shakeStart >= 0f && shakeTarget != null) shakeTarget.position = shakeOrigin;
            shakeStart = -1f;
            screenFlashStart = -1f;
            lightStart = -1f;
            if (flashLight != null) flashLight.enabled = false;
        }
    }
}
