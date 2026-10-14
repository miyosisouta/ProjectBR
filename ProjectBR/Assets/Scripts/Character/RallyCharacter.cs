using System;
using UnityEngine;

namespace BomberRally
{
    /// <summary>
    /// ラリーに参加する 1 体分のキャラクター。操作役（IHitController）は同じ GameObject に付ける。
    /// </summary>
    public class RallyCharacter : MonoBehaviour
    {
        [SerializeField] private string displayName = "Player";
        [Tooltip("爆弾を打つ・受ける高さ")]
        [SerializeField] private float hitPointHeight = 1f;

        public string DisplayName => displayName;
        public int Index { get; private set; }
        public bool IsAlive { get; private set; } = true;
        public IHitController Controller { get; private set; }
        public CharacterAnimator Animator { get; private set; }
        public Vector3 HitPoint => transform.position + Vector3.up * hitPointHeight;

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

        /// <summary>構え方向の切り替え。今は向きを変えるだけ（左右の構えはモーション実装時に）。</summary>
        public void FaceTowards(Vector3 worldPosition)
        {
            var dir = worldPosition - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir);
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
