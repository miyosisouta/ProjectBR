using UnityEngine;

namespace BomberRally
{
    /// <summary>
    /// 給油機。爆発に巻き込まれるとびくっと膨らんで焦げ、そのまま炎が燃え上がる。
    /// 炎は Ignite で最大になり、Extinguish（店員の消火器）で少しずつ小さくなって消える。誰も消さなければゆっくり自然に消える。
    /// 焦げと炎は試合が始まると元に戻る（ResetState）。
    /// </summary>
    public class FuelPump : MonoBehaviour
    {
        [Tooltip("爆発と炎の中心。未設定ならこの位置の少し上")]
        [SerializeField] private Transform blastPoint;
        [Tooltip("焦がす（暗くする）レンダラー")]
        [SerializeField] private Renderer[] scorchRenderers = new Renderer[0];
        [Tooltip("1 回の爆発で暗くなる割合")]
        [SerializeField, Range(0f, 1f)] private float scorchPerBlast = 0.3f;
        [SerializeField, Range(0f, 1f)] private float maxScorch = 0.7f;

        [Header("炎")]
        [Tooltip("1 秒あたりの炎の粒の数（炎が最大のとき）")]
        [SerializeField] private float flamesPerSecond = 70f;
        [Tooltip("1 秒あたりの煙の数（炎が最大のとき）")]
        [SerializeField] private float smokePerSecond = 9f;
        [Tooltip("炎の広がり（半径）")]
        [SerializeField] private float fireRadius = 0.7f;
        [Tooltip("炎の高さの目安（上へ飛ぶ速さ）")]
        [SerializeField] private Vector2 flameRiseSpeed = new Vector2(2.5f, 4.5f);
        [SerializeField] private Vector2 flameSize = new Vector2(0.8f, 1.5f);
        [Tooltip("火がついた直後に燃え上がる強さ（炎の大きさに足す倍率）")]
        [SerializeField] private float flareBoost = 1.2f;
        [Tooltip("燃え上がりが落ち着くまでの秒数")]
        [SerializeField] private float flareDuration = 1.2f;
        [Tooltip("誰も消さなかったときに自然に消えるまでの秒数")]
        [SerializeField] private float selfExtinguishSeconds = 20f;
        [SerializeField] private float lightIntensity = 7f;
        [SerializeField] private float lightRange = 9f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock block;
        private Color[] baseColors;
        private float scorch;
        private Vector3 baseScale;
        private float joltStart = -10f;

        private ParticleSystem flames;
        private ParticleSystem smoke;
        private Light fireLight;
        private float fire;
        private float flareStart = -10f;
        private float flameTimer;
        private float smokeTimer;

        public Vector3 BlastPoint => blastPoint != null ? blastPoint.position : transform.position + Vector3.up * 1.2f;

        /// <summary>今の炎の強さ（0 で消えている、1 で最大）。</summary>
        public float Fire => fire;
        public bool IsBurning => fire > 0f;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            baseScale = transform.localScale;
            baseColors = new Color[scorchRenderers.Length];
            for (int i = 0; i < scorchRenderers.Length; i++)
            {
                var mat = scorchRenderers[i] != null ? scorchRenderers[i].sharedMaterial : null;
                baseColors[i] = mat != null && mat.HasProperty(BaseColorId) ? mat.GetColor(BaseColorId) : Color.white;
            }

            flames = EffectParticles.Create(transform, "Flames", EffectAssets.SoftDot, ParticleSystemRenderMode.Billboard, 600);
            EffectParticles.SetOverLifetime(flames,
                EffectParticles.Gradient(
                    new[]
                    {
                        new GradientColorKey(new Color(1f, 0.95f, 0.7f), 0f),
                        new GradientColorKey(new Color(1f, 0.55f, 0.12f), 0.35f),
                        new GradientColorKey(new Color(0.75f, 0.15f, 0.05f), 1f),
                    },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0.7f, 0.55f), new GradientAlphaKey(0f, 1f) }),
                new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.25f)));
            EffectParticles.SetDrag(flames, 1.5f);

            smoke = EffectParticles.Create(transform, "FireSmoke", EffectAssets.Smoke, ParticleSystemRenderMode.Billboard, 200);
            EffectParticles.SetOverLifetime(smoke,
                EffectParticles.Gradient(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) }),
                new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 2f)));
            smoke.GetComponent<ParticleSystemRenderer>().sortingFudge = 40f;

            var lightGo = new GameObject("FireLight");
            lightGo.transform.SetParent(transform, false);
            fireLight = lightGo.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1f, 0.55f, 0.2f);
            fireLight.range = lightRange;
            fireLight.shadows = LightShadows.None;
            fireLight.enabled = false;
        }

        /// <summary>爆発に巻き込まれた。膨らんで焦げる。</summary>
        public void Detonate()
        {
            scorch = Mathf.Min(maxScorch, scorch + scorchPerBlast);
            ApplyScorch();
            joltStart = Time.time;
        }

        /// <summary>炎を最大にして燃え上がらせる。</summary>
        public void Ignite()
        {
            fire = 1f;
            flareStart = Time.time;
        }

        /// <summary>炎を amount だけ弱める（1 で最大から消えるまで）。</summary>
        public void Extinguish(float amount)
        {
            fire = Mathf.Max(0f, fire - amount);
        }

        public void ResetState()
        {
            scorch = 0f;
            fire = 0f;
            ApplyScorch();
        }

        private void Update()
        {
            UpdateJolt();

            if (fire <= 0f)
            {
                fireLight.enabled = false;
                return;
            }

            fire = Mathf.Max(0f, fire - Time.deltaTime / Mathf.Max(0.1f, selfExtinguishSeconds));
            float flare = Mathf.Clamp01(1f - (Time.time - flareStart) / flareDuration);
            // 小さくなるときは見た目の勢いも一緒に落とす（弱火ほど背が低く、粒も少ない）
            float power = fire * (1f + flare * flareBoost);

            flameTimer += Time.deltaTime * flamesPerSecond * Mathf.Max(0.15f, Mathf.Sqrt(power));
            while (flameTimer >= 1f)
            {
                flameTimer -= 1f;
                EmitFlame(power);
            }

            smokeTimer += Time.deltaTime * smokePerSecond * power;
            while (smokeTimer >= 1f)
            {
                smokeTimer -= 1f;
                var p = BlastPoint + Vector3.up * (0.8f + power) + Random.insideUnitSphere * 0.4f;
                var v = Vector3.up * Random.Range(1.5f, 2.5f) + Random.insideUnitSphere * 0.4f;
                EffectParticles.Emit(smoke, p, v, Random.Range(1.2f, 2f) * (0.6f + 0.4f * power), 2.2f, new Color(0.15f, 0.13f, 0.12f, 0.6f), Random.Range(0f, 360f));
            }

            fireLight.enabled = true;
            fireLight.transform.position = BlastPoint + Vector3.up * 0.8f;
            fireLight.intensity = lightIntensity * power * (0.75f + 0.25f * Mathf.PerlinNoise(Time.time * 9f, 0.3f));
        }

        private void EmitFlame(float power)
        {
            var disc = Random.insideUnitCircle * fireRadius * (0.5f + 0.5f * Mathf.Min(1f, power));
            var pos = BlastPoint + new Vector3(disc.x, 0f, disc.y);
            float scale = 0.45f + 0.55f * power;
            var vel = Vector3.up * Random.Range(flameRiseSpeed.x, flameRiseSpeed.y) * scale
                      + new Vector3(Random.Range(-0.4f, 0.4f), 0f, Random.Range(-0.4f, 0.4f));
            EffectParticles.Emit(flames, pos, vel, Random.Range(flameSize.x, flameSize.y) * scale, Random.Range(0.55f, 0.9f), Color.white);
        }

        private void UpdateJolt()
        {
            const float duration = 0.4f;
            float t = (Time.time - joltStart) / duration;
            if (t >= 1f)
            {
                transform.localScale = baseScale;
                return;
            }
            float k = Mathf.Sin(t * Mathf.PI) * (1f - t);
            transform.localScale = baseScale * (1f + 0.35f * k);
        }

        private void ApplyScorch()
        {
            for (int i = 0; i < scorchRenderers.Length; i++)
            {
                var r = scorchRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, Color.Lerp(baseColors[i], Color.black, scorch));
                r.SetPropertyBlock(block);
            }
        }
    }
}
