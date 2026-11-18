using UnityEngine;

namespace BomberRally
{
    /// <summary>
    /// ラリー用のモーション再生。子にあるモデルの Animator を操作する。
    /// Animator Controller には Idle / Hit / Smash / Die / Victory（Hero はさらに Forehand）の状態がある前提
    /// （BomberRally > プロトタイプシーンを作成 で自動生成される）。
    /// </summary>
    public class CharacterAnimator : MonoBehaviour
    {
        public static readonly int IdleState = Animator.StringToHash("Idle");
        public static readonly int HitState = Animator.StringToHash("Hit");
        public static readonly int ForehandState = Animator.StringToHash("Forehand");
        public static readonly int SmashState = Animator.StringToHash("Smash");
        public static readonly int DieState = Animator.StringToHash("Die");
        public static readonly int VictoryState = Animator.StringToHash("Victory");

        [SerializeField] private Animator animator;
        [SerializeField] private float crossFadeTime = 0.08f;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        public void PlayIdle() => Play(IdleState);
        /// <summary>Hit はバックハンド。Forehand の状態がない Controller では Hit で代用する。</summary>
        public void PlayHit(bool smash, SwingSide side)
        {
            if (smash) Play(SmashState);
            else if (side == SwingSide.Forehand && HasState(ForehandState)) Play(ForehandState);
            else Play(HitState);
        }

        private bool HasState(int stateHash) => animator != null && animator.isActiveAndEnabled && animator.HasState(0, stateHash);
        public void PlayDie() => Play(DieState);
        public void PlayVictory() => Play(VictoryState);

        private void Play(int stateHash)
        {
            if (animator == null || !animator.isActiveAndEnabled) return;
            animator.CrossFadeInFixedTime(stateHash, crossFadeTime, 0, 0f);
        }
    }
}
