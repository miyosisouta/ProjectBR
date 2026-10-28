using UnityEngine;

namespace BomberRally
{
    /// <summary>
    /// 爆弾の導火線から散る火花と、先端のちらつく光と、先端が通った跡の光の軌跡。
    /// どれも爆弾の子として導火線の位置に付くので、爆弾が回ると一緒に回る。
    /// ラリーが速くなる（飛行時間が短くなる）ほど火花が増えて激しくなり、スマッシュ中はさらに増える。
    /// Bomb が必要になったときに付ける。数値は BombEffectSettings にあり、インスペクターで変えた瞬間に反映される。
    /// </summary>
    public class BombFuseEffect : MonoBehaviour
    {
        private Bomb bomb;
        private ParticleSystem sparks;
        private ParticleSystem glow;
        private TrailRenderer trail;

        private float heat;
        private bool smash;
        private BombEffectSettings appliedSettings;
        private int appliedVersion = -1;

        public void Setup(Bomb owner)
        {
            bomb = owner;
        }

        /// <param name="flightTime">今回の飛行時間。短いほど激しくなる</param>
        /// <param name="isSmash">スマッシュで飛んでいる間は火花を増やす</param>
        public void SetHeat(float flightTime, bool isSmash)
        {
            var s = bomb.Settings;
            heat = s.Heat(flightTime);
            smash = isSmash;
            EnsureSystems();
            Apply(s);
        }

        /// <summary>サーブ待ちなど、飛んでいないときのおとなしい状態にする。前の場所に残った火花も消す。</summary>
        public void Calm()
        {
            heat = 0f;
            smash = false;
            EnsureSystems();
            sparks.Clear();
            glow.Clear();
            trail.Clear();
            Apply(bomb.Settings);
        }

        /// <summary>爆弾が離れた場所へ移ったときに、軌跡が前の場所とつながらないよう消す。</summary>
        public void ClearTrail()
        {
            if (trail != null) trail.Clear();
        }

        private void Update()
        {
            if (bomb == null || sparks == null) return;
            // 回転で導火線の先が動く速さだけを火花に渡す。
            // 位置の変化から求めると、爆弾が瞬間移動したときにとんでもない速さになって画面を横切る線になる
            var tip = sparks.transform.position - bomb.transform.position;
            var main = sparks.main;
            main.emitterVelocity = Vector3.Cross(bomb.AngularVelocity * Mathf.Deg2Rad, tip);
        }

        private void LateUpdate()
        {
            if (bomb == null || sparks == null) return;
            var s = bomb.Settings;
            if (s != appliedSettings || s.Version != appliedVersion) Apply(s);
        }

        // 爆弾は非アクティブのまま使われ始めることがあるので Awake ではなく初回に作る
        private void EnsureSystems()
        {
            if (sparks != null) return;

            sparks = EffectParticles.Create(transform, "FuseSparks", EffectAssets.SoftDot, ParticleSystemRenderMode.Stretch, 500);
            glow = EffectParticles.Create(transform, "FuseGlow", EffectAssets.SoftDot, ParticleSystemRenderMode.Billboard, 32);

            foreach (var ps in new[] { sparks, glow })
            {
                var main = ps.main;
                // 爆弾が消えて出てきたときに自動で再開させる
                main.playOnAwake = true;
                // 爆弾の大きさに火花の大きさや速さを引っ張られないようにする
                main.scalingMode = ParticleSystemScalingMode.Shape;
                var emission = ps.emission;
                emission.enabled = true;
            }

            // 導火線の先から上に向かって円錐状に散らす
            var shape = sparks.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.radius = 0.02f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            // 火花は回転の勢いを受け継いで、振り回されて散る（速さは Update で毎フレーム渡す）
            var sparksMain = sparks.main;
            sparksMain.emitterVelocityMode = ParticleSystemEmitterVelocityMode.Custom;
            var inherit = sparks.inheritVelocity;
            inherit.enabled = true;
            inherit.mode = ParticleSystemInheritVelocityMode.Initial;

            // 光は導火線の先に貼り付いたまま一緒に回る
            var glowMain = glow.main;
            glowMain.simulationSpace = ParticleSystemSimulationSpace.Local;

            var trailGo = new GameObject("FuseTrail");
            trailGo.transform.SetParent(transform, false);
            trail = trailGo.AddComponent<TrailRenderer>();
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.minVertexDistance = 0.03f;
            trail.sharedMaterial = EffectAssets.SoftLine;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
        }

        /// <summary>設定と今の激しさを反映する。</summary>
        private void Apply(BombEffectSettings s)
        {
            appliedSettings = s;
            appliedVersion = s.Version;
            PlaceAtFuse(s);

            float boost = smash ? s.smashMultiplier : 1f;
            float speedScale = Mathf.Lerp(1f, s.wildSpeedMultiplier, heat);

            var main = sparks.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(s.sparkSpeed.x * speedScale, s.sparkSpeed.y * speedScale);
            main.startSize = new ParticleSystem.MinMaxCurve(s.sparkSize.x, s.sparkSize.y);
            main.startLifetime = new ParticleSystem.MinMaxCurve(s.sparkLifetime.x, s.sparkLifetime.y);
            main.startColor = new ParticleSystem.MinMaxGradient(s.sparkColorA, s.sparkColorB);
            main.gravityModifier = s.sparkGravity;
            var emission = sparks.emission;
            emission.rateOverTime = Mathf.Lerp(s.sparkRate.x, s.sparkRate.y, heat) * boost;
            var shape = sparks.shape;
            shape.angle = s.sparkSpread;
            var inherit = sparks.inheritVelocity;
            inherit.curve = new ParticleSystem.MinMaxCurve(s.sparkInheritVelocity);
            EffectParticles.SetOverLifetime(sparks, s.sparkGradient, s.sparkSizeCurve);
            EffectParticles.SetDrag(sparks, s.sparkDrag);
            var renderer = sparks.GetComponent<ParticleSystemRenderer>();
            renderer.velocityScale = s.sparkVelocityStretch;
            renderer.lengthScale = s.sparkLengthStretch;

            // 光は短い寿命のものを次々に出して、大きさのばらつきでちらつかせる
            float glowSize = Mathf.Lerp(s.glowSize.x, s.glowSize.y, heat) * (smash ? 1.2f : 1f);
            float flickerRate = Mathf.Max(1f, s.glowFlickerRate);
            var glowMain = glow.main;
            glowMain.startSpeed = 0f;
            glowMain.startSize = new ParticleSystem.MinMaxCurve(glowSize * 0.7f, glowSize);
            glowMain.startLifetime = 1.5f / flickerRate;
            glowMain.startColor = s.glowColor;
            var glowEmission = glow.emission;
            glowEmission.rateOverTime = glowSize > 0f ? flickerRate : 0f;
            EffectParticles.SetOverLifetime(glow, EffectParticles.PlainFade(), AnimationCurve.Constant(0f, 1f, 1f));

            trail.emitting = s.fuseTrailTime > 0f;
            trail.time = Mathf.Max(0.01f, s.fuseTrailTime);
            trail.widthMultiplier = s.fuseTrailWidth;
            trail.colorGradient = EffectParticles.Gradient(
                new[] { new GradientColorKey(s.fuseTrailColor, 0f), new GradientColorKey(s.fuseTrailColor, 1f) },
                new[] { new GradientAlphaKey(s.fuseTrailColor.a, 0f), new GradientAlphaKey(0f, 1f) });

            if (!sparks.isPlaying) sparks.Play();
            if (!glow.isPlaying) glow.Play();
        }

        private void PlaceAtFuse(BombEffectSettings s)
        {
            var fusePoint = bomb.FusePoint;
            foreach (var t in new[] { sparks.transform, glow.transform, trail.transform })
            {
                if (fusePoint != null)
                {
                    t.SetParent(fusePoint, false);
                    t.localPosition = Vector3.zero;
                    continue;
                }
                t.SetParent(transform, false);
                // 爆弾の子なので、爆弾が回れば導火線の位置も一緒に回る
                float scaleY = Mathf.Max(0.0001f, transform.lossyScale.y);
                t.localPosition = Vector3.up * (s.fuseHeight / scaleY);
            }
        }
    }
}
