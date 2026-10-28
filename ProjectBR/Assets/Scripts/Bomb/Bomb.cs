using UnityEngine;

namespace BomberRally
{
    /// <summary>
    /// 爆弾の見た目と飛行。判定は RallyManager が持ち、ここは決められた時間で放物線を描くだけ。
    /// 到着後は受け付け終了まで到着地点で待つ。飛んでいる間は進行方向に前転する。
    /// 導火線の火花・スマッシュの軌跡・回転の数値は BombEffectSettings にある。
    /// </summary>
    public class Bomb : MonoBehaviour
    {
        [SerializeField] private float arcHeight = 2.5f;
        [Tooltip("飛んでいないとき（サーブ待ち）に縦軸で回る速さ")]
        [SerializeField] private float spinSpeed = 360f;
        [Tooltip("爆発時に生成するエフェクト（未設定なら何も出さない）")]
        [SerializeField] private GameObject explosionPrefab;

        [Header("エフェクト")]
        [Tooltip("未設定なら Resources/" + BombEffectSettings.ResourcePath + " を使う")]
        [SerializeField] private BombEffectSettings effectSettings;
        [Tooltip("導火線の先の位置（未設定なら中心の真上。モデルを差し替えたときに使う）")]
        [SerializeField] private Transform fusePoint;

        private TrailRenderer smashTrail;
        private BombFuseEffect fuse;
        private Vector3 from;
        private Vector3 to;
        private float launchTime;
        private float duration;
        private bool flying;
        private bool smashFlight;
        private Vector3 tumbleAxis;

        public float ArrivalTime => launchTime + duration;
        public Transform FusePoint => fusePoint;
        /// <summary>今の回転（軸の向き × 度/秒、ワールド座標）。導火線の火花を回転に振り回すのに使う。</summary>
        public Vector3 AngularVelocity { get; private set; }

        public BombEffectSettings Settings
        {
            get
            {
                if (effectSettings == null) effectSettings = Resources.Load<BombEffectSettings>(BombEffectSettings.ResourcePath);
                // アセットが見つからなくても既定値で動かす
                if (effectSettings == null) effectSettings = ScriptableObject.CreateInstance<BombEffectSettings>();
                return effectSettings;
            }
            set => effectSettings = value;
        }

        /// <param name="smash">スマッシュなら飛んでいる間だけ軌跡を残す</param>
        public void Launch(Vector3 from, Vector3 to, float duration, bool smash = false)
        {
            this.from = from;
            this.to = to;
            this.duration = Mathf.Max(0.01f, duration);
            launchTime = Time.time;
            flying = true;
            smashFlight = smash;
            tumbleAxis = PickTumbleAxis(to - from);
            // 打ち返した位置が待っていた位置とずれていると、軌跡がその間をつないで線が走るので消しておく
            bool jumped = !gameObject.activeSelf || (transform.position - from).sqrMagnitude > 0.01f;
            transform.position = from;
            gameObject.SetActive(true);
            ApplyTrailSettings();
            if (jumped)
            {
                SmashTrail.Clear();
                Fuse.ClearTrail();
            }
            SmashTrail.emitting = smash;
            Fuse.SetHeat(this.duration, smash);
        }

        public void PlaceAt(Vector3 position)
        {
            flying = false;
            transform.position = position;
            // 飛んでいる間に傾いたままだと導火線が横を向くので起こしておく
            transform.rotation = Quaternion.identity;
            gameObject.SetActive(true);
            SmashTrail.emitting = false;
            SmashTrail.Clear();
            Fuse.Calm();
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
                smashTrail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
                smashTrail.minVertexDistance = 0.05f;
                smashTrail.sharedMaterial = EffectAssets.SoftLine;
                smashTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                smashTrail.receiveShadows = false;
                smashTrail.emitting = false;
                ApplyTrailSettings();
                return smashTrail;
            }
        }

        private BombFuseEffect Fuse
        {
            get
            {
                if (fuse != null) return fuse;
                fuse = GetComponent<BombFuseEffect>();
                if (fuse == null) fuse = gameObject.AddComponent<BombFuseEffect>();
                fuse.Setup(this);
                return fuse;
            }
        }

        /// <summary>設定を変えたら次に打ち返したときから効くよう、飛ばすたびに反映する。</summary>
        private void ApplyTrailSettings()
        {
            var s = Settings;
            SmashTrail.time = s.smashTrailTime;
            SmashTrail.widthMultiplier = s.smashTrailWidth;
            SmashTrail.colorGradient = EffectParticles.Gradient(
                new[] { new GradientColorKey(s.smashTrailColor, 0f), new GradientColorKey(s.smashTrailColor, 1f) },
                new[] { new GradientAlphaKey(s.smashTrailColor.a, 0f), new GradientAlphaKey(0f, 1f) });
        }

        /// <summary>進行方向に前転する軸。毎回少し傾けて回り方に変化をつける。</summary>
        private Vector3 PickTumbleAxis(Vector3 travel)
        {
            travel.y = 0f;
            var dir = travel.sqrMagnitude > 0.0001f ? travel.normalized : Vector3.forward;
            var axis = Vector3.Cross(Vector3.up, dir);
            float jitter = Settings.tumbleAxisJitter;
            return Quaternion.AngleAxis(Random.Range(-jitter, jitter), dir) * axis;
        }

        private void Update()
        {
            if (!flying)
            {
                AngularVelocity = Vector3.up * spinSpeed;
                transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
                return;
            }

            // ラリーが速いほど、スマッシュならさらに勢いよく回る
            var s = Settings;
            float tumble = Mathf.Lerp(s.tumbleSpeed.x, s.tumbleSpeed.y, s.Heat(duration));
            if (smashFlight) tumble *= s.smashTumbleMultiplier;
            AngularVelocity = tumbleAxis * tumble;
            transform.Rotate(tumbleAxis, tumble * Time.deltaTime, Space.World);

            float t = Mathf.Clamp01((Time.time - launchTime) / duration);
            var pos = Vector3.Lerp(from, to, t);
            pos.y += arcHeight * 4f * t * (1f - t);
            transform.position = pos;
        }
    }
}
