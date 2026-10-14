namespace BomberRally
{
    /// <summary>
    /// 打ち返す方向。打つキャラクター自身から見た前・左・右。None は方向指定なし（ランダム）。
    /// </summary>
    public enum HitDirection
    {
        None,
        Forward,
        Left,
        Right,
    }
}
