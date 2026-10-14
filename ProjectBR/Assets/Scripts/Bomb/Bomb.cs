using UnityEngine;

namespace BomberRally
{
    /// <summary>
    /// 爆弾の見た目と飛行。判定は RallyManager が持ち、ここは決められた時間で放物線を描くだけ。
    /// 到着後は受け付け終了まで到着地点で待つ。
    /// </summary>
    public class Bomb : MonoBehaviour
    {
        [SerializeField] private float arcHeight = 2.5f;
        [SerializeField] private float spinSpeed = 360f;
        [Tooltip("爆発時に生成するエフェクト（未設定なら何も出さない）")]
        [SerializeField] private GameObject explosionPrefab;

        private Vector3 from;
        private Vector3 to;
        private float launchTime;
        private float duration;
        private bool flying;

        public float ArrivalTime => launchTime + duration;

        public void Launch(Vector3 from, Vector3 to, float duration)
        {
            this.from = from;
            this.to = to;
            this.duration = Mathf.Max(0.01f, duration);
            launchTime = Time.time;
            flying = true;
            transform.position = from;
            gameObject.SetActive(true);
        }

        public void PlaceAt(Vector3 position)
        {
            flying = false;
            transform.position = position;
            gameObject.SetActive(true);
        }

        public void Explode()
        {
            flying = false;
            if (explosionPrefab != null)
            {
                var fx = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
                Destroy(fx, 3f);
            }
            gameObject.SetActive(false);
        }

        private void Update()
        {
            transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
            if (!flying) return;

            float t = Mathf.Clamp01((Time.time - launchTime) / duration);
            var pos = Vector3.Lerp(from, to, t);
            pos.y += arcHeight * 4f * t * (1f - t);
            transform.position = pos;
        }
    }
}
