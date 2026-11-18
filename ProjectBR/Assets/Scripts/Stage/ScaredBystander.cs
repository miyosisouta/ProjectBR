using UnityEngine;

namespace BomberRally
{
    /// <summary>
    /// ラリーを見て怖がる人（公園の子ども・ガソリンスタンドの店員）。最初から怖がっていて、爆弾が速くなるほどひどくなる。
    /// 段階は RallyManager.SpeedRatio（最初の速さで 0、最速で 1）で決まり、脱落者が出て速さが戻ると少し落ち着く。
    ///   0 不安：冷や汗が出る・震える・そわそわ足踏みしてきょろきょろ見回す・少し青ざめる
    ///   1 怯え：震えが大きくなる・汗だく・青ざめる
    ///   2 恐怖：「!!」が出る・しゃがんで縮こまる・隠れ場所（遊具や友達のそば）へ寄る・大きく震える
    /// 爆発の瞬間は爆弾の速さに関係なく大慌てする：跳び上がってのけぞり、「!!」が飛び出し、
    /// 首をぶんぶん振りながらその場を走り回って、少しして元の怖がり方に戻る。
    /// 見た目の変化はすべて Visual の子に対して行い、本体の位置は隠れ場所への移動にだけ使う。
    /// </summary>
    public class ScaredBystander : MonoBehaviour, IStageReaction
    {
        [Header("見た目")]
        [Tooltip("震わせる・縮こまらせる見た目の親")]
        [SerializeField] private Transform visual;
        [Tooltip("青ざめさせる肌（顔など）のレンダラー")]
        [SerializeField] private Renderer[] skinRenderers = new Renderer[0];
        [SerializeField] private Color skinColor = new Color(1f, 0.85f, 0.7f);
        [SerializeField] private Color paleColor = new Color(0.7f, 0.82f, 1f);
        [Tooltip("一番怖いときに頭の上に出す「!!」")]
        [SerializeField] private GameObject alert;
        [Tooltip("汗を出す高さ（頭の横）")]
        [SerializeField] private float headHeight = 0.95f;

        [Header("段階の境目（SpeedRatio）")]
        [SerializeField, Range(0f, 1f)] private float frightenedAt = 0.34f;
        [SerializeField, Range(0f, 1f)] private float terrifiedAt = 0.67f;

        [Header("段階ごとの強さ（不安・怯え・恐怖）")]
        [SerializeField] private Vector3 trembleAmplitude = new Vector3(0.02f, 0.045f, 0.08f);
        [Tooltip("1 秒あたりの汗の数")]
        [SerializeField] private Vector3 sweatPerSecond = new Vector3(1.5f, 4f, 9f);
        [Tooltip("青ざめる度合い（0 で元の肌色、1 で paleColor）")]
        [SerializeField] private Vector3 paleness = new Vector3(0.3f, 0.6f, 0.9f);
        [SerializeField] private float trembleFrequency = 38f;
        [Tooltip("そわそわと足踏みする高さ")]
        [SerializeField] private Vector3 fidgetHeight = new Vector3(0.025f, 0.045f, 0.03f);
        [Tooltip("足踏みの速さ（回/秒）")]
        [SerializeField] private Vector3 fidgetSpeed = new Vector3(2.5f, 4f, 6f);
        [Tooltip("きょろきょろ見回す角度（度）")]
        [SerializeField] private Vector3 glanceAngle = new Vector3(18f, 28f, 12f);
        [Tooltip("見回す速さ")]
        [SerializeField] private Vector3 glanceSpeed = new Vector3(0.9f, 1.6f, 3f);
        [Tooltip("一番怖いときの縮こまり（高さの倍率）")]
        [SerializeField] private float cowerScale = 0.82f;

        [Header("隠れる")]
        [Tooltip("一番怖いときに寄っていく場所。未設定ならその場で縮こまる")]
        [SerializeField] private Transform hideSpot;
        [SerializeField] private float moveSpeed = 2.2f;

        [Header("爆発の瞬間（大慌て）")]
        [Tooltip("爆発で大慌てするか。オフにすると小さく跳ねるだけ（消火に走る店員など）")]
        [SerializeField] private bool panicOnExplosion = true;
        [Tooltip("跳び上がる高さ")]
        [SerializeField] private float flinchHeight = 0.7f;
        [SerializeField] private float flinchDuration = 0.45f;
        [Tooltip("爆発から遠ざかるようにのけぞる角度")]
        [SerializeField] private float recoilAngle = 30f;
        [Tooltip("大慌てが続く秒数")]
        [SerializeField] private float panicDuration = 2.4f;
        [Tooltip("その場を走り回る範囲（半径）")]
        [SerializeField] private float scrambleRadius = 0.7f;
        [Tooltip("走り回る速さ")]
        [SerializeField] private float scrambleSpeed = 5f;
        [Tooltip("首を振る角度")]
        [SerializeField] private float headShakeAngle = 40f;
        [Tooltip("大慌ての間に 1 秒あたりに出る汗の数")]
        [SerializeField] private float panicSweatPerSecond = 14f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private RallyManager manager;
        private ParticleSystem sweat;
        private MaterialPropertyBlock block;
        private Vector3 home;
        private Vector3 visualBasePosition;
        private Vector3 visualBaseScale;
        private Quaternion visualBaseRotation;
        private float fidgetPhase;
        private float glancePhase;
        private float fear;
        private float cower = 1f;
        private float sweatTimer;
        private float flinchStart = -10f;
        private float panicStart = -10f;
        private Vector3 recoilAxis = Vector3.right;
        private float seed;

        /// <summary>今の怖がりの段階（0〜2）。</summary>
        public int Level { get; private set; }

        public void Bind(RallyManager rallyManager)
        {
            if (manager != null) manager.BombExploded -= OnBombExploded;
            manager = rallyManager;
            if (manager != null) manager.BombExploded += OnBombExploded;
        }

        private void Awake()
        {
            if (visual == null) visual = transform;
            home = transform.position;
            visualBasePosition = visual.localPosition;
            visualBaseScale = visual.localScale;
            visualBaseRotation = visual.localRotation;
            seed = Random.Range(0f, 100f);
            block = new MaterialPropertyBlock();

            sweat = EffectParticles.Create(transform, "Sweat", EffectAssets.SoftDot, ParticleSystemRenderMode.Billboard, 64);
            var main = sweat.main;
            main.gravityModifier = 1.4f;
            EffectParticles.SetOverLifetime(sweat, EffectParticles.PlainFade(), AnimationCurve.Linear(0f, 1f, 1f, 0.6f));

            if (alert != null) alert.SetActive(false);
        }

        private void OnDestroy()
        {
            if (manager != null) manager.BombExploded -= OnBombExploded;
        }

        private void OnBombExploded(RallyCharacter victim, Vector3 position)
        {
            // 全員が同時に動くと機械的なので、反応を少しずつずらす
            flinchStart = Time.time + (panicOnExplosion ? Random.Range(0f, 0.12f) : 0f);
            // 爆発から遠ざかる向きにのけぞる（Visual の親から見た向きで回転軸を決める）
            var away = transform.InverseTransformDirection(transform.position - position);
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f) away = Vector3.back;
            recoilAxis = Vector3.Cross(Vector3.up, away.normalized);
            if (panicOnExplosion) panicStart = flinchStart;
            // 爆発の瞬間は汗が一気に吹き出す
            for (int i = 0; i < (panicOnExplosion ? 10 : 4); i++) EmitSweat();
        }

        /// <summary>大慌ての経過秒数。大慌てしていないときは負。</summary>
        private float PanicTime
        {
            get
            {
                float t = Time.time - panicStart;
                return t >= 0f && t < panicDuration ? t : -1f;
            }
        }

        /// <summary>走り回りの強さ。跳び上がった後から始まり、終わり際に弱まる。</summary>
        private float ScrambleWeight(float t)
        {
            if (t < 0f) return 0f;
            return Mathf.Clamp01((t - 0.3f) / 0.2f) * Mathf.Clamp01((panicDuration - t) / 0.5f);
        }

        private void Update()
        {
            float target = manager != null ? manager.SpeedRatio : 0f;
            // 怖くなるのは速く、落ち着くのはゆっくり
            fear = Mathf.MoveTowards(fear, target, Time.deltaTime * (target > fear ? 3f : 0.6f));
            Level = fear >= terrifiedAt ? 2 : fear >= frightenedAt ? 1 : 0;

            UpdateTremble();
            UpdatePaleness();
            UpdateSweat();
            UpdateHiding();
            if (alert != null)
            {
                float pt = PanicTime;
                bool show = Level == 2 || pt >= 0f;
                alert.SetActive(show);
                if (show)
                {
                    // 大慌ての瞬間は「!!」が大きく飛び出す
                    float pop = pt >= 0f ? 1.2f * (1f - Mathf.Clamp01(pt / 0.35f)) : 0f;
                    alert.transform.localScale = Vector3.one * (1f + pop + 0.15f * Mathf.Sin(Time.time * 14f));
                }
            }
        }

        /// <summary>段階をまたいでもカクつかないよう、段階の値を fear でなめらかにつなぐ。</summary>
        private float ByLevel(Vector3 perLevel)
        {
            if (fear < frightenedAt) return perLevel.x;
            if (fear < terrifiedAt) return Mathf.Lerp(perLevel.x, perLevel.y, Mathf.InverseLerp(frightenedAt, terrifiedAt, fear) * 0.5f + 0.5f);
            return Mathf.Lerp(perLevel.y, perLevel.z, Mathf.InverseLerp(terrifiedAt, 1f, fear) * 0.5f + 0.5f);
        }

        private void UpdateTremble()
        {
            float amp = ByLevel(trembleAmplitude);
            float t = Time.time * trembleFrequency;
            var jitter = new Vector3(
                (Mathf.PerlinNoise(seed, t) - 0.5f) * 2f * amp,
                0f,
                (Mathf.PerlinNoise(t, seed) - 0.5f) * 2f * amp);

            // そわそわと小さく足踏みし、きょろきょろ見回す（落ち着いて立っている人はいない）
            fidgetPhase += Time.deltaTime * ByLevel(fidgetSpeed) * Mathf.PI * 2f;
            glancePhase += Time.deltaTime * ByLevel(glanceSpeed);
            jitter.y += Mathf.Abs(Mathf.Sin(fidgetPhase + seed)) * ByLevel(fidgetHeight);
            float glance = (Mathf.PerlinNoise(seed + 7f, glancePhase) - 0.5f) * 2f * ByLevel(glanceAngle);

            // 爆発の瞬間：跳び上がってのけぞり、体が伸びる
            float flinchT = (Time.time - flinchStart) / flinchDuration;
            float flinch = flinchT >= 0f && flinchT < 1f ? Mathf.Sin(flinchT * Mathf.PI) : 0f;
            jitter.y += flinch * (panicOnExplosion ? flinchHeight : flinchHeight * 0.5f);
            var recoil = Quaternion.AngleAxis(panicOnExplosion ? recoilAngle * flinch : 0f, recoilAxis);

            // そのあと：首を振りながら、その場を 8 の字に走り回る
            float pt = PanicTime;
            float scramble = ScrambleWeight(pt);
            float runYaw = 0f;
            if (scramble > 0f)
            {
                float a = (pt - 0.3f) * scrambleSpeed + seed;
                jitter += new Vector3(Mathf.Sin(a), 0f, Mathf.Sin(2f * a) * 0.6f) * (scrambleRadius * scramble);
                runYaw = Mathf.Atan2(Mathf.Cos(a), 1.2f * Mathf.Cos(2f * a)) * Mathf.Rad2Deg * scramble;
                jitter.y += Mathf.Abs(Mathf.Sin(pt * 18f)) * 0.09f * scramble;
                glance += Mathf.Sin(pt * 22f + seed) * headShakeAngle * scramble;
            }

            visual.localPosition = visualBasePosition + jitter;
            visual.localRotation = recoil * visualBaseRotation * Quaternion.Euler(0f, glance + runYaw, 0f);

            // 大慌ての間はしゃがまず、跳び上がった瞬間は体が縦に伸びる
            cower = Mathf.Lerp(cower, Level == 2 && pt < 0f ? cowerScale : 1f, Time.deltaTime * 8f);
            var scale = visualBaseScale;
            scale.y *= cower * (1f + 0.25f * flinch);
            visual.localScale = scale;
        }

        private void UpdatePaleness()
        {
            // 大慌ての間は一番青ざめる
            float pale = ByLevel(paleness);
            if (PanicTime >= 0f) pale = Mathf.Max(pale, paleness.z);
            var color = Color.Lerp(skinColor, paleColor, pale);
            foreach (var r in skinRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, color);
                block.SetColor(ColorId, color);
                r.SetPropertyBlock(block);
            }
        }

        private void UpdateSweat()
        {
            float rate = ByLevel(sweatPerSecond) + (PanicTime >= 0f ? panicSweatPerSecond : 0f);
            if (rate <= 0f) return;
            sweatTimer += Time.deltaTime * rate;
            while (sweatTimer >= 1f)
            {
                sweatTimer -= 1f;
                EmitSweat();
            }
        }

        private void EmitSweat()
        {
            float side = Random.value < 0.5f ? -1f : 1f;
            var head = visual.position + Vector3.up * headHeight * cower;
            var pos = head + visual.right * 0.18f * side + Vector3.up * 0.05f;
            var vel = visual.right * side * Random.Range(0.3f, 0.7f) + Vector3.up * Random.Range(0.6f, 1.2f);
            EffectParticles.Emit(sweat, pos, vel, Random.Range(0.08f, 0.13f), 0.6f, new Color(0.55f, 0.8f, 1f, 1f));
        }

        private void UpdateHiding()
        {
            if (hideSpot == null) return;
            var goal = Level == 2 ? hideSpot.position : home;
            transform.position = Vector3.MoveTowards(transform.position, goal, moveSpeed * Time.deltaTime);
        }
    }
}
