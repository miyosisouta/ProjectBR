using System;
using UnityEngine;

namespace BomberRally
{
    public enum Stance
    {
        Neutral,
        Left,
        Right,
    }

    /// <summary>
    /// ラリーに参加する 1 体分のキャラクター。操作役（IHitController）は同じ GameObject に付ける。
    /// </summary>
    public class RallyCharacter : MonoBehaviour
    {
        [SerializeField] private string displayName = "Player";
        [Tooltip("爆弾を打つ・受ける高さ")]
        [SerializeField] private float hitPointHeight = 1f;

        [Header("構え")]
        [Tooltip("左右に構えるときに体をひねる角度")]
        [SerializeField] private float stanceAngle = 45f;
        [Tooltip("正面からこの角度以内に飛んでくるときは正面で構える")]
        [SerializeField] private float neutralThreshold = 15f;
        [Tooltip("向きを変える速さ（度/秒）")]
        [SerializeField] private float turnSpeed = 900f;
        [Tooltip("打ち返した後、正面に戻るまで構えを保つ秒数")]
        [SerializeField] private float stanceHoldAfterHit = 0.4f;

        public string DisplayName => displayName;
        public int Index { get; private set; }
        public bool IsAlive { get; private set; } = true;
        public IHitController Controller { get; private set; }
        public CharacterAnimator Animator { get; private set; }
        public Vector3 HitPoint => transform.position + Vector3.up * hitPointHeight;
        public Stance Stance { get; private set; }

        private Quaternion homeRotation = Quaternion.identity;
        private Quaternion targetRotation = Quaternion.identity;
        private float stanceReleaseTime = -1f;

        /// <summary>空振りなどで入力を受け付けない時間の終わり</summary>
        public float LockedUntil { get; set; }

        public event Action<RallyCharacter> HitRequested;

        public void Setup(int index)
        {
            Index = index;
            Animator = GetComponent<CharacterAnimator>();
            Controller = GetComponent<IHitController>();
            if (Controller == null)
            {
                Debug.LogWarning($"{name} に IHitController がないため CPU を付けます", this);
                Controller = gameObject.AddComponent<CpuHitController>();
            }
            Controller.Initialize(this);
        }

        public void RequestHit() => HitRequested?.Invoke(this);

        /// <summary>立ち位置で正面とする方向（ふつうはステージ中央）を決め、すぐにその向きにする。</summary>
        public void SetHome(Vector3 lookAt)
        {
            var dir = lookAt - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f) homeRotation = Quaternion.LookRotation(dir);
            Stance = Stance.Neutral;
            targetRotation = homeRotation;
            transform.rotation = homeRotation;
            stanceReleaseTime = -1f;
        }

        /// <summary>爆弾が飛んでくる位置に合わせて左右の構えを切り替える。</summary>
        public void TakeStance(Vector3 source)
        {
            var dir = source - transform.position;
            dir.y = 0f;
            float angle = Vector3.SignedAngle(homeRotation * Vector3.forward, dir, Vector3.up);

            if (Mathf.Abs(angle) <= neutralThreshold) Stance = Stance.Neutral;
            else Stance = angle < 0f ? Stance.Left : Stance.Right;
            targetRotation = homeRotation * Quaternion.Euler(0f, StanceYaw(Stance), 0f);
            stanceReleaseTime = -1f;
        }

        /// <summary>打ち返した後、少し構えを保ってから正面に戻る。</summary>
        public void ReleaseStance()
        {
            stanceReleaseTime = Time.time + stanceHoldAfterHit;
        }

        private float StanceYaw(Stance stance)
        {
            switch (stance)
            {
                case Stance.Left: return -stanceAngle;
                case Stance.Right: return stanceAngle;
                default: return 0f;
            }
        }

        private void Update()
        {
            if (!IsAlive) return;
            if (stanceReleaseTime >= 0f && Time.time >= stanceReleaseTime)
            {
                stanceReleaseTime = -1f;
                Stance = Stance.Neutral;
                targetRotation = homeRotation;
            }
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }

        public void PlayHit(bool smash)
        {
            if (Animator != null) Animator.PlayHit(smash);
        }

        public void PlayVictory()
        {
            if (Animator != null) Animator.PlayVictory();
        }

        public void Eliminate()
        {
            IsAlive = false;
            // モーションがなければ仮モデルなので消す
            if (Animator != null) Animator.PlayDie();
            else gameObject.SetActive(false);
        }

        public void ResetForRound()
        {
            IsAlive = true;
            LockedUntil = 0f;
            gameObject.SetActive(true);
            Controller?.ResetState();
            if (Animator != null) Animator.PlayIdle();
        }
    }
}
