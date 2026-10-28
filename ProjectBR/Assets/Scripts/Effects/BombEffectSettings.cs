using System;
using UnityEngine;

namespace BomberRally
{
    /// <summary>
    /// 爆弾そのものに付くエフェクト（導火線の火花・スマッシュの軌跡・飛んでいる間の回転）の数値。
    /// アセットなので再生中に変えても止めたあとに残り、変えた瞬間に反映される。
    /// メニュー: BomberRally > エフェクト設定 > 爆弾
    /// </summary>
    [CreateAssetMenu(menuName = "BomberRally/爆弾エフェクト設定", fileName = "BombEffectSettings")]
    public class BombEffectSettings : ScriptableObject
    {
        /// <summary>Resources からの読み込みパス。Bomb に未設定のときはこれを使う。</summary>
        public const string ResourcePath = "Effects/BombEffectSettings";

        [Header("導火線の位置")]
        [Tooltip("爆弾の中心からの高さ（Bomb に Fuse Point を設定したときはそちらを使う）")]
        public float fuseHeight = 0.35f;

        [Header("盛り上がり")]
        [Tooltip("飛行時間がこれ以上なら一番おとなしい火花")]
        public float calmFlightTime = 1.6f;
        [Tooltip("飛行時間がこれ以下なら一番激しい火花。ラリーが速くなるほど火花が増える")]
        public float wildFlightTime = 0.45f;
        [Tooltip("スマッシュで飛んでいる間は火花の量にこれを掛ける")]
        public float smashMultiplier = 1.6f;

        [Header("飛んでいる間の回転")]
        [Tooltip("進行方向に前転する速さ（度/秒）。おとなしいとき・激しいとき")]
        public Vector2 tumbleSpeed = new Vector2(540f, 1260f);
        [Tooltip("スマッシュで飛んでいる間は回転の速さにこれを掛ける")]
        public float smashTumbleMultiplier = 1.5f;
        [Tooltip("回転軸を毎回ランダムに傾ける角度（度）。0 なら毎回まっすぐ前転")]
        [Range(0f, 90f)] public float tumbleAxisJitter = 25f;

        [Header("火花")]
        [Tooltip("1秒に出す数（おとなしいとき・激しいとき）")]
        public Vector2 sparkRate = new Vector2(35f, 160f);
        [Tooltip("飛び出す速さ（最小・最大）。激しいときはさらに速くなる")]
        public Vector2 sparkSpeed = new Vector2(1.5f, 4f);
        [Tooltip("激しいときの速さの倍率")]
        public float wildSpeedMultiplier = 1.6f;
        [Tooltip("真上からどれだけ広がって飛ぶか（度）")]
        [Range(0f, 90f)] public float sparkSpread = 55f;
        [Tooltip("回転の勢いを火花が受け継ぐ割合。大きいほど回転に振り回されて渦を巻くように散る")]
        [Range(0f, 1f)] public float sparkInheritVelocity = 0.5f;
        [Tooltip("大きさ（最小・最大）")]
        public Vector2 sparkSize = new Vector2(0.03f, 0.07f);
        [Tooltip("寿命（最小・最大）")]
        public Vector2 sparkLifetime = new Vector2(0.2f, 0.45f);
        [Tooltip("落ちていく強さ")]
        public float sparkGravity = 1.2f;
        [Tooltip("飛び出してから減速する強さ")]
        public float sparkDrag = 2f;
        [Tooltip("火花ごとの開始色はこの2色の間でばらつく")]
        public Color sparkColorA = new Color(1f, 1f, 0.85f, 1f);
        public Color sparkColorB = new Color(1f, 0.6f, 0.15f, 1f);
        [Tooltip("白く光ってから橙に冷めて消える")]
        public Gradient sparkGradient = EffectParticles.Gradient(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.75f, 0.35f), 0.4f), new GradientColorKey(new Color(1f, 0.35f, 0.1f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
        public AnimationCurve sparkSizeCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        [Tooltip("速さに応じて火花を伸ばす量")]
        public float sparkVelocityStretch = 0.05f;
        [Tooltip("火花の長さの倍率")]
        public float sparkLengthStretch = 1.5f;

        [Header("導火線の先の光")]
        [Tooltip("大きさ（おとなしいとき・激しいとき）。0 なら出さない")]
        public Vector2 glowSize = new Vector2(0.35f, 0.65f);
        public Color glowColor = new Color(1f, 0.75f, 0.35f, 0.8f);
        [Tooltip("ちらつきの細かさ（1秒に出す光の数）")]
        public float glowFlickerRate = 30f;

        [Header("導火線の先の軌跡（回っている様子が分かるよう、光の通り道を残す）")]
        [Tooltip("残る時間（0 なら出さない）")]
        public float fuseTrailTime = 0.12f;
        public float fuseTrailWidth = 0.15f;
        public Color fuseTrailColor = new Color(1f, 0.7f, 0.3f, 0.8f);

        [Header("スマッシュの軌跡")]
        [Tooltip("爆弾の中心から伸びる風切りの筋。火花と同じ色にすると火の一部に見えて浮くので、別の色にしておく")]
        public Color smashTrailColor = new Color(0.75f, 0.9f, 1f, 0.55f);
        public float smashTrailWidth = 0.5f;
        public float smashTrailTime = 0.25f;

        /// <summary>飛行時間からの盛り上がり具合（0: おとなしい 〜 1: 激しい）。</summary>
        public float Heat(float flightTime) => Mathf.InverseLerp(calmFlightTime, wildFlightTime, flightTime);

        /// <summary>インスペクターで値が変わるたびに増える。使う側はこれを見て反映し直す。</summary>
        [NonSerialized] public int Version;

        private void OnValidate() => Version++;
    }
}
