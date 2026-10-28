using System;
using UnityEngine;

namespace BomberRally
{
    /// <summary>
    /// 打ち返しエフェクトの数値。アセットなので再生中に変えても止めたあとに残る。
    /// HitEffect は再生のたびにここを読むので、変えた値は次に打ち返したときから反映される。
    /// メニュー: BomberRally > エフェクト設定 > 打ち返し
    /// </summary>
    [CreateAssetMenu(menuName = "BomberRally/打ち返しエフェクト設定", fileName = "HitEffectSettings")]
    public class HitEffectSettings : ScriptableObject
    {
        /// <summary>Resources からの読み込みパス。HitEffect に未設定のときはこれを使う。</summary>
        public const string ResourcePath = "Effects/HitEffectSettings";

        [Serializable]
        public struct Style
        {
            [Tooltip("輪と閃光の色（火花は少し濃くなる）")]
            public Color color;

            [Header("輪")]
            [Tooltip("輪が広がりきったときの大きさ")]
            public float ringSize;
            public float ringLifetime;
            [Tooltip("内側にもう一つ出す輪の大きさの倍率（0 なら出さない）")]
            public float secondRingScale;
            [Tooltip("内側の輪の寿命の倍率（外側の輪に対して）")]
            public float secondRingLifetimeScale;

            [Header("閃光")]
            [Tooltip("中心の閃光の大きさ（0 なら出さない）")]
            public float flashSize;
            public float flashLifetime;

            [Header("火花")]
            public int sparkCount;
            public float sparkSpeed;
            public float sparkSize;
            public float sparkLifetime;
            [Tooltip("打った方向から左右に火花が散る角度")]
            public float sparkSpread;
            [Tooltip("火花の色を輪の色より濃くする量")]
            public float sparkAlphaBoost;

            [Header("放射状の線")]
            [Tooltip("全方向に放射状に伸びる線の数（0 なら出さない）")]
            public int burstCount;
            public float burstSpeed;
            public float burstSize;
            public float burstLifetime;
            public Color burstColor;
        }

        public Style normal = new Style
        {
            color = new Color(0.8f, 0.92f, 1f, 0.35f),
            ringSize = 0.9f,
            ringLifetime = 0.14f,
            secondRingScale = 0f,
            secondRingLifetimeScale = 1.3f,
            flashSize = 0f,
            flashLifetime = 0.07f,
            sparkCount = 3,
            sparkSpeed = 4f,
            sparkSize = 0.05f,
            sparkLifetime = 0.14f,
            sparkSpread = 50f,
            sparkAlphaBoost = 0.3f,
            burstCount = 0,
            burstSpeed = 16f,
            burstSize = 0.07f,
            burstLifetime = 0.12f,
            burstColor = new Color(1f, 0.95f, 0.75f, 0.9f),
        };

        public Style smash = new Style
        {
            color = new Color(1f, 0.72f, 0.3f, 0.8f),
            ringSize = 1.9f,
            ringLifetime = 0.22f,
            secondRingScale = 0.55f,
            secondRingLifetimeScale = 1.3f,
            flashSize = 1.5f,
            flashLifetime = 0.11f,
            sparkCount = 12,
            sparkSpeed = 9f,
            sparkSize = 0.09f,
            sparkLifetime = 0.24f,
            sparkSpread = 35f,
            sparkAlphaBoost = 0.3f,
            burstCount = 8,
            burstSpeed = 16f,
            burstSize = 0.07f,
            burstLifetime = 0.12f,
            burstColor = new Color(1f, 0.95f, 0.75f, 0.9f),
        };

        [Header("火花の動き")]
        [Tooltip("飛び出してから減速する強さ（大きいほど遠くまで飛ばない）")]
        public float sparkDrag = 10f;
        [Tooltip("火花の上下の向きのばらつき（下限・上限）")]
        public Vector2 sparkVerticalRange = new Vector2(-0.2f, 0.5f);
        [Tooltip("速さに応じて火花を伸ばす量")]
        public float sparkVelocityStretch = 0.035f;
        [Tooltip("火花の長さの倍率")]
        public float sparkLengthStretch = 1.5f;

        [Header("寿命に応じた変化")]
        [Tooltip("輪の大きさ（勢いよく広がってから止まる）")]
        public AnimationCurve ringSizeCurve = new AnimationCurve(new Keyframe(0f, 0.35f, 2.5f, 2.5f), new Keyframe(1f, 1f, 0f, 0f));
        public AnimationCurve sparkSizeCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        public AnimationCurve flashSizeCurve = new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.8f));
        [Tooltip("輪・火花・閃光に共通の色の変化（開始色に掛け合わせる）")]
        public Gradient fade = EffectParticles.PlainFade();

        [Header("スマッシュの画面揺れ（0 で揺らさない）")]
        public float shakeAmplitude = 0.05f;
        public float shakeDuration = 0.1f;
    }
}
