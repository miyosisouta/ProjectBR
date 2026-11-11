using UnityEngine;

namespace BomberRally
{
    public enum DifficultyLevel
    {
        Easy,
        Normal,
        Hard,
        Expert,
    }

    /// <summary>
    /// 難易度ごとの数値。1 段階につき 1 アセット（Resources/Difficulty/ 以下にレベル名で置く）。
    /// 数値を変えたいときはアセットを Inspector で直接いじればよい。
    /// </summary>
    [CreateAssetMenu(menuName = "BomberRally/難易度設定", fileName = "Normal")]
    public class DifficultySettings : ScriptableObject
    {
        /// <summary>Resources からの読み込みフォルダ。ファイル名は DifficultyLevel の名前にする。</summary>
        public const string ResourceFolder = "Difficulty/";

        [Tooltip("画面に出す名前")]
        public string displayName = "Normal";

        [Header("CPU の強さ")]
        [Tooltip("爆弾が一番遅い（最初の速さの）ときに打ち返す確率")]
        [Range(0f, 1f)] public float cpuSuccessRateAtSlowest = 1f;
        [Tooltip("爆弾が一番速い（最速の）ときに打ち返す確率。速くなるほどこの値に近づく")]
        [Range(0f, 1f)] public float cpuSuccessRateAtFastest = 0.6f;
        [Tooltip("成功時にスマッシュ（ジャストタイミング）になる確率")]
        [Range(0f, 1f)] public float cpuPerfectRate = 0.2f;
        [Tooltip("CPU が打ち返すとき、人間プレイヤーを狙う確率。外れたときは全員からランダム")]
        [Range(0f, 1f)] public float cpuFocusHumanRate = 0.5f;

        [Header("爆弾の速さ")]
        [Tooltip("初球（各ラリーの最初の 1 球）の飛行時間（秒）。短いほど速い。脱落者が出るたびにこの速さに戻る")]
        public float initialFlightTime = 1.6f;
        [Tooltip("最速時の飛行時間（秒）。打ち返すたびにここまで縮む")]
        public float minFlightTime = 0.45f;

        [Header("判定の厳しさ")]
        public HitWindow hitWindow = HitWindow.Default;

        /// <summary>タイトルなどで選んだ難易度。シーンをまたいで覚えておく。</summary>
        public static DifficultyLevel Selected { get; set; } = DifficultyLevel.Normal;

        /// <summary>
        /// 爆弾の速さの度合い。最初の速さで 0、最速で 1。
        /// </summary>
        public float SpeedRatio(float flightTime) => Mathf.InverseLerp(initialFlightTime, minFlightTime, flightTime);

        /// <summary>この飛行時間で飛んできた爆弾を CPU が打ち返す確率。</summary>
        public float CpuSuccessRate(float flightTime)
        {
            return Mathf.Lerp(cpuSuccessRateAtSlowest, cpuSuccessRateAtFastest, SpeedRatio(flightTime));
        }

        /// <summary>表示名以外の数値をすべて写す。テスト用ウィンドウでいじった値をアセットに保存するときに使う。</summary>
        public void CopyValuesFrom(DifficultySettings other)
        {
            cpuSuccessRateAtSlowest = other.cpuSuccessRateAtSlowest;
            cpuSuccessRateAtFastest = other.cpuSuccessRateAtFastest;
            cpuPerfectRate = other.cpuPerfectRate;
            cpuFocusHumanRate = other.cpuFocusHumanRate;
            initialFlightTime = other.initialFlightTime;
            minFlightTime = other.minFlightTime;
            hitWindow = other.hitWindow;
        }

        public static DifficultySettings Load(DifficultyLevel level)
        {
            var settings = Resources.Load<DifficultySettings>(ResourceFolder + level);
            if (settings != null) return settings;

            // アセットが見つからなくても既定値で動かす
            Debug.LogWarning($"Resources/{ResourceFolder}{level} が見つからないため既定値を使います");
            settings = CreateInstance<DifficultySettings>();
            settings.displayName = level.ToString();
            return settings;
        }
    }
}
