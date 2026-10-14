using System;
using UnityEngine;

namespace BomberRally
{
    public enum HitResult
    {
        TooEarly,
        Good,
        Perfect,
    }

    /// <summary>
    /// 打ち返しの受付時間。爆弾の到着時刻を 0 として前後何秒まで受け付けるか。
    /// </summary>
    [Serializable]
    public struct HitWindow
    {
        [Tooltip("到着の何秒前から受け付けるか")]
        public float early;
        [Tooltip("到着の何秒後まで受け付けるか（これを過ぎると爆発）")]
        public float late;
        [Tooltip("到着時刻 ± この秒数ならスマッシュ")]
        public float perfect;

        public static HitWindow Default => new HitWindow { early = 0.25f, late = 0.1f, perfect = 0.06f };

        /// <param name="diff">押した時刻 - 到着時刻</param>
        public HitResult Judge(float diff)
        {
            if (diff < -early) return HitResult.TooEarly;
            return Mathf.Abs(diff) <= perfect ? HitResult.Perfect : HitResult.Good;
        }
    }
}
