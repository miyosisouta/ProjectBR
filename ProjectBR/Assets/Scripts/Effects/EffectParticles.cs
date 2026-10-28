using UnityEngine;
using UnityEngine.Rendering;

namespace BomberRally
{
    /// <summary>
    /// HitEffect / ExplosionEffect で共通の ParticleSystem まわりの処理。
    /// 見た目の数値は持たず、設定アセットの値を毎回流し込む前提。
    /// </summary>
    public static class EffectParticles
    {
        /// <summary>自分では出さず、Emit されたものだけを動かす ParticleSystem。</summary>
        public static ParticleSystem Create(Transform parent, string systemName, Material material, ParticleSystemRenderMode mode, int maxParticles)
        {
            var go = new GameObject(systemName);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.gravityModifier = 0f;
            main.maxParticles = maxParticles;

            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;

            var color = ps.colorOverLifetime;
            color.enabled = true;
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = mode;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            // 大きな火球が画面いっぱいになっても切られないようにする
            renderer.maxParticleSize = 3f;

            ps.Play();
            return ps;
        }

        /// <summary>寿命に応じた色と大きさの変化を設定する。</summary>
        public static void SetOverLifetime(ParticleSystem ps, Gradient fade, AnimationCurve size)
        {
            var color = ps.colorOverLifetime;
            color.color = fade;
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, size);
        }

        /// <summary>飛び出してから減速させる強さ（0 で減速しない）。</summary>
        public static void SetDrag(ParticleSystem ps, float drag)
        {
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = drag > 0f;
            limit.limit = 100f;
            limit.drag = drag;
            limit.multiplyDragByParticleSize = false;
            limit.multiplyDragByParticleVelocity = false;
        }

        public static void Emit(ParticleSystem ps, Vector3 position, Vector3 velocity, float size, float lifetime, Color color, float rotation = 0f)
        {
            ps.Emit(new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startSize = size,
                startLifetime = lifetime,
                startColor = color,
                rotation = rotation,
                applyShapeToPosition = false,
            }, 1);
        }

        public static Gradient Gradient(GradientColorKey[] colors, GradientAlphaKey[] alphas)
        {
            var g = new Gradient();
            g.SetKeys(colors, alphas);
            return g;
        }

        /// <summary>色は変えず、出てすぐ少し薄くなってから消える。</summary>
        public static Gradient PlainFade() => Gradient(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.3f), new GradientAlphaKey(0f, 1f) });
    }
}
