namespace BomberRally
{
    /// <summary>
    /// キャラクターの「操作役」。人間の入力でも CPU でも、打ち返したいときに owner.RequestHit(方向) を呼ぶだけ。
    /// RallyManager は操作役の種類を知らない（ローカルマルチは操作役の差し替えで実現する）。
    /// </summary>
    public interface IHitController
    {
        void Initialize(RallyCharacter owner);

        /// <summary>自分に向かって爆弾が飛んできたときに呼ばれる。</summary>
        /// <param name="flightTime">今回の飛行時間（短いほど速い）</param>
        /// <param name="difficulty">今の難易度。受付時間もここにある</param>
        void OnBombIncoming(float arrivalTime, float flightTime, DifficultySettings difficulty);

        /// <summary>ラウンドのやり直し時に呼ばれる。</summary>
        void ResetState();
    }
}
