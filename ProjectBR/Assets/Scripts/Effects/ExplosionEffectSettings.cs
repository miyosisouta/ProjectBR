using UnityEngine;

namespace BomberRally
{
    /// <summary>
    /// 爆発エフェクトの数値。アセットなので再生中に変えても止めたあとに残る。
    /// ExplosionEffect は再生のたびにここを読むので、変えた値は次の爆発から反映される。
    /// メニュー: BomberRally > エフェクト設定 > 爆発
    /// </summary>
    [CreateAssetMenu(menuName = "BomberRally/爆発エフェクト設定", fileName = "ExplosionEffectSettings")]
    public class ExplosionEffectSettings : ScriptableObject
    {
        /// <summary>Resources からの読み込みパス。ExplosionEffect に未設定のときはこれを使う。</summary>
        public const string ResourcePath = "Effects/ExplosionEffectSettings";

        [Header("全体")]
        [Tooltip("全体の大きさの倍率")]
        public float scale = 1f;
        [Tooltip("閃光・火花・衝撃波に共通の色の変化（開始色に掛け合わせる）")]
        public Gradient fade = EffectParticles.PlainFade();

        [Header("閃光")]
        [Tooltip("一瞬だけ出る大きな光（0 なら出さない）")]
        public float flashSize = 7f;
        public float flashLifetime = 0.18f;
        public Color flashColor = new Color(1f, 1f, 0.95f, 1f);
        [Tooltip("閃光より小さく長く残る芯（0 なら出さない）")]
        public float coreSize = 3.5f;
        public float coreLifetime = 0.35f;
        public Color coreColor = new Color(1f, 0.8f, 0.35f, 1f);
        public AnimationCurve flashSizeCurve = new AnimationCurve(new Keyframe(0f, 0.8f), new Keyframe(0.25f, 1.1f), new Keyframe(1f, 0.6f));

        [Header("火球")]
        public int fireCount = 28;
        public float fireSpeed = 7f;
        public float fireSize = 1.8f;
        public float fireLifetime = 0.7f;
        [Tooltip("火球ごとの開始色はこの2色の間でばらつく")]
        public Color fireColorA = new Color(1f, 0.95f, 0.6f, 1f);
        public Color fireColorB = new Color(1f, 0.55f, 0.15f, 1f);
        [Tooltip("飛び出してから減速する強さ")]
        public float fireDrag = 6f;
        [Tooltip("白 → 黄 → 橙 → 赤 と冷めながら消える")]
        public Gradient fireGradient = EffectParticles.Gradient(
            new[] { new GradientColorKey(new Color(1f, 1f, 0.9f), 0f), new GradientColorKey(new Color(1f, 0.85f, 0.3f), 0.15f),
                    new GradientColorKey(new Color(1f, 0.45f, 0.1f), 0.45f), new GradientColorKey(new Color(0.6f, 0.1f, 0.05f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.4f), new GradientAlphaKey(0f, 1f) });
        [Tooltip("一気に膨らんでからゆっくり育つ")]
        public AnimationCurve fireSizeCurve = new AnimationCurve(new Keyframe(0f, 0.4f, 4f, 4f), new Keyframe(0.35f, 1f, 0.6f, 0.6f), new Keyframe(1f, 1.25f));

        [Header("煙")]
        public int smokeCount = 16;
        public float smokeSize = 2.2f;
        public float smokeLifetime = 1.6f;
        public Color smokeColor = new Color(0.18f, 0.16f, 0.15f, 0.75f);
        [Tooltip("飛び出す速さ（最小・最大）")]
        public Vector2 smokeSpeed = new Vector2(2f, 5f);
        public float smokeDrag = 3f;
        [Tooltip("昇っていく強さ")]
        public float smokeRise = 0.15f;
        [Tooltip("回転の速さ（ラジアン/秒、左右どちらにも回る）")]
        public float smokeSpin = 0.8f;
        [Tooltip("出た直後は火に隠れるので、少し遅れて濃くなる")]
        public Gradient smokeGradient = EffectParticles.Gradient(
            new[] { new GradientColorKey(new Color(0.6f, 0.35f, 0.2f), 0f), new GradientColorKey(Color.white, 0.3f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
        public AnimationCurve smokeSizeCurve = new AnimationCurve(new Keyframe(0f, 0.5f, 2f, 2f), new Keyframe(1f, 1.8f, 0.3f, 0.3f));

        [Header("火花")]
        public int sparkCount = 48;
        public float sparkSpeed = 18f;
        public Color sparkColor = new Color(1f, 0.8f, 0.4f, 1f);
        [Tooltip("大きさ（最小・最大）")]
        public Vector2 sparkSize = new Vector2(0.06f, 0.14f);
        [Tooltip("寿命（最小・最大）")]
        public Vector2 sparkLifetime = new Vector2(0.4f, 0.9f);
        [Tooltip("落ちていく強さ")]
        public float sparkGravity = 1.6f;
        public float sparkDrag = 1.5f;
        [Tooltip("速さに応じて火花を伸ばす量")]
        public float sparkVelocityStretch = 0.04f;
        [Tooltip("火花の長さの倍率")]
        public float sparkLengthStretch = 2f;
        public AnimationCurve sparkSizeCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        [Header("集中線")]
        [Tooltip("画面に対して放射状に伸びる線の数（0 なら出さない）")]
        public int lineCount = 18;
        public float lineSpeed = 30f;
        public float lineSize = 0.18f;
        public float lineLifetime = 0.16f;
        public Color lineColor = new Color(1f, 0.95f, 0.8f, 1f);

        [Header("衝撃波")]
        [Tooltip("地面を這う輪の大きさ（0 なら出さない）")]
        public float shockwaveSize = 9f;
        public float shockwaveLifetime = 0.45f;
        public Color shockwaveColor = new Color(1f, 0.85f, 0.6f, 0.9f);
        [Tooltip("地面の内側の輪（大きさの倍率・寿命）")]
        public float innerRingScale = 0.6f;
        public float innerRingLifetime = 0.6f;
        public Color innerRingColor = new Color(1f, 0.6f, 0.3f, 0.63f);
        [Tooltip("空中でカメラに向いて広がる輪（大きさの倍率・寿命）")]
        public float airRingScale = 0.7f;
        public float airRingLifetime = 0.3f;
        [Tooltip("勢いよく広がってから止まる")]
        public AnimationCurve ringSizeCurve = new AnimationCurve(new Keyframe(0f, 0.15f, 3.5f, 3.5f), new Keyframe(1f, 1f, 0f, 0f));

        [Header("遅れて起きる小爆発")]
        public int aftershockCount = 4;
        [Tooltip("中心からどこまで離れた場所で起きるか")]
        public float aftershockRadius = 1.4f;
        [Tooltip("一つ前からの間隔（最小・最大）秒")]
        public Vector2 aftershockInterval = new Vector2(0.06f, 0.14f);
        [Tooltip("本体に対する大きさ（最小・最大）")]
        public Vector2 aftershockSize = new Vector2(0.4f, 0.6f);

        [Header("ヒットストップ（0 で止めない）")]
        public float hitStopDuration = 0.12f;
        [Tooltip("止めている間の時間の速さ（0 で完全に止める）")]
        [Range(0f, 1f)] public float hitStopTimeScale = 0.05f;

        [Header("画面フラッシュ（0 で光らせない）")]
        [Range(0f, 1f)] public float screenFlashAlpha = 0.65f;
        public float screenFlashDuration = 0.3f;
        public Color screenFlashColor = new Color(1f, 0.95f, 0.85f);

        [Header("画面揺れ（0 で揺らさない）")]
        public float shakeAmplitude = 0.45f;
        public float shakeDuration = 0.55f;
        [Tooltip("大きいほど細かく揺れる")]
        public float shakeFrequency = 28f;

        [Header("光源（0 で光らせない）")]
        public Color lightColor = new Color(1f, 0.6f, 0.25f);
        public float lightIntensity = 12f;
        public float lightRange = 14f;
        public float lightDuration = 0.6f;
    }
}
