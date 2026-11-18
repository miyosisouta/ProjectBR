using UnityEngine;

namespace BomberRally
{
    /// <summary>
    /// 打ち返したとき、手に持ったフライパンの面を打ち返す相手の方へ向ける。
    /// モーションで動く手の位置はそのままに、向きだけを上書きする。
    /// フライパンは持ち手が原点、面の表が +X、柄から先端が +Y になっている前提（FryingPan.prefab）。
    /// </summary>
    public class FryingPanAim : MonoBehaviour
    {
        [Tooltip("向きを変えるフライパン（weapon_r の下の FryingPan）")]
        [SerializeField] private Transform pan;
        [Tooltip("相手の方へ向くまでの秒数")]
        [SerializeField] private float blendInTime = 0.06f;
        [Tooltip("相手の方を向いたままにする秒数")]
        [SerializeField] private float holdTime = 0.35f;
        [Tooltip("モーションどおりの向きに戻るまでの秒数")]
        [SerializeField] private float blendOutTime = 0.2f;

        private Quaternion restLocalRotation = Quaternion.identity;
        // フライパンの +X をこの向きへ向ける
        private Vector3 aimDirection;
        private float aimStartTime = -1f;

        private void Awake()
        {
            if (pan == null)
            {
                foreach (var t in GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "FryingPan") { pan = t; break; }
                }
            }
            if (pan != null) restLocalRotation = pan.localRotation;
        }

        /// <summary>
        /// フライパンの面を target の方へ向ける。
        /// バックハンドは +X 面、フォアハンドは手首の向きに合わせて反対の -X 面を相手へ向ける。
        /// </summary>
        public void AimAt(Vector3 target, SwingSide side)
        {
            if (pan == null) return;
            var dir = target - pan.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            aimDirection = side == SwingSide.Backhand ? dir.normalized : -dir.normalized;
            aimStartTime = Time.time;
        }

        public void ResetAim()
        {
            aimStartTime = -1f;
            if (pan != null) pan.localRotation = restLocalRotation;
        }

        // Animator が手を動かした後に向きを上書きする
        private void LateUpdate()
        {
            if (pan == null || aimStartTime < 0f) return;

            float elapsed = Time.time - aimStartTime;
            if (elapsed >= blendInTime + holdTime + blendOutTime)
            {
                ResetAim();
                return;
            }
            float weight = Weight(elapsed);

            var restRotation = pan.parent != null ? pan.parent.rotation * restLocalRotation : restLocalRotation;
            // +X を aimDirection へ、柄から先端（+Y）を真上へ
            var aimRotation = Quaternion.LookRotation(Vector3.Cross(aimDirection, Vector3.up), Vector3.up);
            pan.rotation = Quaternion.Slerp(restRotation, aimRotation, weight);
        }

        private float Weight(float elapsed)
        {
            if (elapsed < blendInTime) return Mathf.SmoothStep(0f, 1f, elapsed / Mathf.Max(blendInTime, 0.0001f));
            elapsed -= blendInTime;
            if (elapsed < holdTime) return 1f;
            elapsed -= holdTime;
            if (elapsed < blendOutTime) return Mathf.SmoothStep(1f, 0f, elapsed / Mathf.Max(blendOutTime, 0.0001f));
            return 0f;
        }
    }
}
