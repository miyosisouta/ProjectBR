using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace BomberRally
{
    /// <summary>
    /// エフェクト用のテクスチャとマテリアルをコードで作る。素材を用意するまでの仮。
    /// 一度作ったものは使い回す。
    /// </summary>
    public static class EffectAssets
    {
        private static Material softDot;
        private static Material ring;
        private static Material softLine;

        /// <summary>中心が明るく外に向かって消える丸（火花・閃光用）</summary>
        public static Material SoftDot => softDot != null ? softDot : softDot = CreateAdditive(CreateTexture("SoftDot", (u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v);
            return Mathf.Pow(Mathf.Clamp01(1f - r), 2f);
        }));

        /// <summary>細い輪（打った瞬間に広がる輪用）</summary>
        public static Material Ring => ring != null ? ring : ring = CreateAdditive(CreateTexture("Ring", (u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v);
            return r > 1f ? 0f : Mathf.Exp(-Mathf.Pow((r - 0.78f) / 0.09f, 2f));
        }));

        /// <summary>幅方向だけぼけた帯（軌跡用）</summary>
        public static Material SoftLine => softLine != null ? softLine : softLine = CreateAdditive(CreateTexture("SoftLine", (u, v) =>
        {
            return Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(v)), 1.5f);
        }));

        /// <summary>白地に透明度だけ描いたテクスチャ。uv は -1〜1。</summary>
        private static Texture2D CreateTexture(string name, Func<float, float, float> alpha)
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    byte a = (byte)(Mathf.Clamp01(alpha(u, v)) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }

        /// <summary>
        /// URP の Particles/Unlit を加算合成にしたマテリアル。
        /// ビルドでシェーダーが見つからないときは Sprites/Default（半透明）で代用する。
        /// </summary>
        private static Material CreateAdditive(Texture texture)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) return new Material(Shader.Find("Sprites/Default")) { mainTexture = texture };

            var mat = new Material(shader) { name = $"Additive_{texture.name}" };
            mat.SetTexture("_BaseMap", texture);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 2f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.One);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
            return mat;
        }
    }
}
