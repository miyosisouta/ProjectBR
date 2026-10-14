using UnityEngine;

namespace BomberRally
{
    /// <summary>
    /// CPU の操作役。爆弾が飛んできた時点で成功するかを抽選し、決めた時刻にボタンを押す。
    /// </summary>
    public class CpuHitController : MonoBehaviour, IHitController
    {
        [Range(0f, 1f)] [SerializeField] private float successRate = 0.85f;
        [Tooltip("成功時にスマッシュ（ジャストタイミング）になる確率")]
        [Range(0f, 1f)] [SerializeField] private float perfectRate = 0.2f;

        private RallyCharacter owner;
        private float scheduledPressTime = -1f;

        public void Initialize(RallyCharacter owner) => this.owner = owner;

        public void OnBombIncoming(float arrivalTime, HitWindow window)
        {
            if (Random.value > successRate)
            {
                // 失敗：押さずに見送る
                scheduledPressTime = -1f;
                return;
            }

            float offset = Random.value < perfectRate
                ? Random.Range(-window.perfect, window.perfect)
                : Random.Range(-window.early * 0.9f, window.late * 0.9f);
            scheduledPressTime = arrivalTime + offset;
        }

        public void ResetState() => scheduledPressTime = -1f;

        private void Update()
        {
            if (owner == null || scheduledPressTime < 0f) return;
            if (Time.time < scheduledPressTime) return;

            scheduledPressTime = -1f;
            owner.RequestHit();
        }
    }
}
