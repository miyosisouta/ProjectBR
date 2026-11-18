using System.Collections;
using UnityEngine;

namespace BomberRally
{
    /// <summary>給油機の横に停めてある車。誘爆の勢いで跳ね上がり、ボンネットが開いて閉じる。</summary>
    public class ParkedCar : MonoBehaviour
    {
        [Tooltip("跳ねさせる見た目の親")]
        [SerializeField] private Transform body;
        [Tooltip("開くボンネット（付け根を回転の中心にしておく）。なくてもよい")]
        [SerializeField] private Transform hood;
        [SerializeField] private float jumpHeight = 0.7f;
        [SerializeField] private float jumpDuration = 0.55f;
        [SerializeField] private float maxTilt = 10f;
        [SerializeField] private float hoodOpenAngle = 55f;

        private Vector3 bodyPosition;
        private Quaternion bodyRotation;
        private Quaternion hoodRotation;

        private void Awake()
        {
            if (body == null) body = transform;
            bodyPosition = body.localPosition;
            bodyRotation = body.localRotation;
            if (hood != null) hoodRotation = hood.localRotation;
        }

        public void Bounce(float strength = 1f)
        {
            StopAllCoroutines();
            StartCoroutine(BounceRoutine(Mathf.Clamp01(strength)));
        }

        private IEnumerator BounceRoutine(float strength)
        {
            var tilt = Quaternion.Euler(Random.Range(-maxTilt, maxTilt) * strength, 0f, Random.Range(-maxTilt, maxTilt) * strength);
            for (float t = 0f; t < jumpDuration; t += Time.deltaTime)
            {
                float k = t / jumpDuration;
                body.localPosition = bodyPosition + Vector3.up * (4f * k * (1f - k) * jumpHeight * strength);
                body.localRotation = bodyRotation * Quaternion.Slerp(Quaternion.identity, tilt, Mathf.Sin(k * Mathf.PI));
                if (hood != null) hood.localRotation = hoodRotation * Quaternion.Euler(-hoodOpenAngle * Mathf.Min(1f, k * 3f), 0f, 0f);
                yield return null;
            }
            body.localPosition = bodyPosition;
            body.localRotation = bodyRotation;

            // ボンネットはしばらく開いたまま、ぱたんと閉じる
            if (hood == null) yield break;
            yield return new WaitForSeconds(0.8f);
            const float close = 0.25f;
            for (float t = 0f; t < close; t += Time.deltaTime)
            {
                hood.localRotation = hoodRotation * Quaternion.Euler(-hoodOpenAngle * (1f - t / close), 0f, 0f);
                yield return null;
            }
            hood.localRotation = hoodRotation;
        }
    }
}
