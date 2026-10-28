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

        [Header("スマッシュの軌跡")]
        [SerializeField] private Color smashTrailColor = new Color(1f, 0.7f, 0.3f, 0.7f);
        [SerializeField] private float smashTrailWidth = 0.5f;
        [SerializeField] private float smashTrailTime = 0.25f;

        private TrailRenderer smashTrail;
        private Vector3 from;
        private Vector3 to;
        private float launchTime;
        private float duration;
        private bool flying;

        public float ArrivalTime => launchTime + duration;

        /// <param name="smash">スマッシュなら飛んでいる間だけ軌跡を残す</param>
        public void Launch(Vector3 from, Vector3 to, float duration, bool smash = false)
        {
            this.from = from;
            this.to = to;
            this.duration = Mathf.Max(0.01f, duration);
            launchTime = Time.time;
            flying = true;
            transform.position = from;
            gameObject.SetActive(true);
            SmashTrail.emitting = smash;
        }

        public void PlaceAt(Vector3 position)
        {
            flying = false;
            transform.position = position;
            gameObject.SetActive(true);
            SmashTrail.emitting = false;
            SmashTrail.Clear();
        }

        public void Explode()
        {
            flying = false;
            SmashTrail.emitting = false;
            SmashTrail.Clear();
            if (explosionPrefab != null)
            {
                var fx = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
                Destroy(fx, 3f);
            }
            gameObject.SetActive(false);
        }

        // 爆弾は非アクティブのまま Launch されることがあるので Awake ではなく初回に作る
        private TrailRenderer SmashTrail
        {
            get
            {
                if (smashTrail != null) return smashTrail;

                var go = new GameObject("SmashTrail");
                go.transform.SetParent(transform, false);
                smashTrail = go.AddComponent<TrailRenderer>();
                smashTrail.time = smashTrailTime;
                smashTrail.widthMultiplier = smashTrailWidth;
                smashTrail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
                smashTrail.minVertexDistance = 0.05f;
                smashTrail.sharedMaterial = EffectAssets.SoftLine;
                smashTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                smashTrail.receiveShadows = false;

                var fade = new Gradient();
                fade.SetKeys(
                    new[] { new GradientColorKey(smashTrailColor, 0f), new GradientColorKey(smashTrailColor, 1f) },
                    new[] { new GradientAlphaKey(smashTrailColor.a, 0f), new GradientAlphaKey(0f, 1f) });
                smashTrail.colorGradient = fade;
                smashTrail.emitting = false;
                return smashTrail;
            }
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
