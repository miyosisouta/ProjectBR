using System;
using UnityEngine;

namespace BomberRally
{
    /// <summary>ステージの時間帯。難易度ごとに DifficultySettings で選ぶ（始まりの時間帯）。</summary>
    public enum StageTimeOfDay
    {
        Day,
        Evening,
        Night,
    }

    /// <summary>ステージの演出部品。StageEnvironment が読み込み時に RallyManager を渡す。</summary>
    public interface IStageReaction
    {
        void Bind(RallyManager manager);
    }

    /// <summary>時間帯ごとの光と空の色。</summary>
    [Serializable]
    public class StageLighting
    {
        [Header("太陽（シーンの Directional Light）")]
        public Color sunColor = Color.white;
        public float sunIntensity = 1f;
        [Tooltip("太陽の向き（オイラー角）。X が小さいほど低い位置から照らす＝影が長い")]
        public Vector3 sunEuler = new Vector3(50f, -30f, 0f);

        [Header("環境光（上・横・下）")]
        public Color skyAmbient = Color.gray;
        public Color equatorAmbient = Color.gray;
        public Color groundAmbient = Color.gray;

        [Header("空（Procedural Skybox のときだけ効く）")]
        public Color skyTint = new Color(0.5f, 0.5f, 0.5f);
        [Tooltip("大きいほど夕焼けらしく赤くなる")]
        public float atmosphereThickness = 1f;
        public float skyExposure = 1.3f;

        [Header("霧")]
        public bool fog;
        public Color fogColor = Color.gray;
        public float fogStart = 25f;
        public float fogEnd = 70f;

        public static StageLighting Day() => new StageLighting
        {
            sunColor = new Color(1f, 0.96f, 0.88f),
            sunIntensity = 1.25f,
            sunEuler = new Vector3(52f, -35f, 0f),
            skyAmbient = new Color(0.62f, 0.72f, 0.88f),
            equatorAmbient = new Color(0.58f, 0.6f, 0.58f),
            groundAmbient = new Color(0.32f, 0.3f, 0.26f),
            skyTint = new Color(0.5f, 0.55f, 0.65f),
            atmosphereThickness = 0.9f,
            skyExposure = 1.3f,
            fog = false,
        };

        public static StageLighting Evening() => new StageLighting
        {
            sunColor = new Color(1f, 0.55f, 0.3f),
            sunIntensity = 0.9f,
            // 奥から低く照らし、影が手前に長く伸びるようにする
            sunEuler = new Vector3(13f, 160f, 0f),
            skyAmbient = new Color(0.5f, 0.38f, 0.48f),
            equatorAmbient = new Color(0.62f, 0.42f, 0.32f),
            groundAmbient = new Color(0.2f, 0.15f, 0.15f),
            skyTint = new Color(0.65f, 0.45f, 0.4f),
            atmosphereThickness = 2.4f,
            skyExposure = 1.1f,
            fog = true,
            fogColor = new Color(0.85f, 0.55f, 0.4f),
            fogStart = 30f,
            fogEnd = 80f,
        };

        public static StageLighting Night() => new StageLighting
        {
            // 月明かり。ラリーが見えなくならないよう、真っ暗にはしない
            sunColor = new Color(0.6f, 0.7f, 1f),
            sunIntensity = 0.35f,
            sunEuler = new Vector3(40f, 200f, 0f),
            skyAmbient = new Color(0.2f, 0.24f, 0.4f),
            equatorAmbient = new Color(0.16f, 0.18f, 0.28f),
            groundAmbient = new Color(0.08f, 0.08f, 0.1f),
            skyTint = new Color(0.2f, 0.25f, 0.45f),
            atmosphereThickness = 0.5f,
            skyExposure = 0.3f,
            fog = true,
            fogColor = new Color(0.1f, 0.12f, 0.2f),
            fogStart = 30f,
            fogEnd = 80f,
        };
    }

    /// <summary>
    /// ステージのプレハブの一番上に付ける。時間帯に合わせて光・空・昼だけ／明かりが要る時間だけの物（街灯の明かりなど）を切り替え、
    /// 子にある IStageReaction（怖がる子ども・燃える給油機など）に RallyManager を渡す。
    /// 試合中は時間が流れ、始まりの時間帯から次の時間帯（昼→夕方、夕方→夜）へ transitionSeconds かけて空と光が移り変わる。
    /// 時計は試合中だけ進み、試合が始まるたびに始まりの時間帯に戻る。
    /// </summary>
    public class StageEnvironment : MonoBehaviour
    {
        [SerializeField] private StageLighting day = StageLighting.Day();
        [SerializeField] private StageLighting evening = StageLighting.Evening();
        [SerializeField] private StageLighting night = StageLighting.Night();
        [Tooltip("昼だけ出す物（明かりがつくと消える）")]
        [SerializeField] private GameObject[] dayOnly = Array.Empty<GameObject>();
        [Tooltip("明かりが要る時間だけ出す物（街灯の明かり・看板の光など）。夕方・夜は最初から、昼から始まったときは途中で点く")]
        [SerializeField] private GameObject[] eveningOnly = Array.Empty<GameObject>();

        [Header("時間の流れ")]
        [Tooltip("始まりの時間帯から次の時間帯へ移り変わるまでの試合時間（秒）")]
        [SerializeField] private float transitionSeconds = 180f;
        [Tooltip("昼から始まったとき、移り変わりがこの割合まで進んだら明かりを点ける")]
        [SerializeField, Range(0f, 1f)] private float lightsOnAt = 0.6f;
        [Tooltip("空の映り込みを更新する間隔（秒）。毎フレームは重いので間を空ける")]
        [SerializeField] private float environmentRefreshInterval = 1f;

        private Material skyboxInstance;
        private Material originalSkybox;
        private RallyManager manager;
        private float elapsed;
        private float lastRefresh = -10f;
        private bool? lightsOn;

        public StageTimeOfDay TimeOfDay { get; private set; }

        /// <summary>移り変わりの進み具合（0 で始まりの時間帯、1 で次の時間帯）。</summary>
        public float Progress => transitionSeconds > 0f ? Mathf.Clamp01(elapsed / transitionSeconds) : 1f;

        public void Apply(StageTimeOfDay time)
        {
            TimeOfDay = time;
            elapsed = 0f;
            lightsOn = null;
            Refresh(true);
        }

        public void Bind(RallyManager rallyManager)
        {
            if (rallyManager == null) return;
            if (manager != null) manager.GameStarted -= OnGameStarted;
            manager = rallyManager;
            manager.GameStarted += OnGameStarted;
            foreach (var reaction in GetComponentsInChildren<IStageReaction>(true)) reaction.Bind(manager);
        }

        private void OnGameStarted(DifficultySettings settings)
        {
            // 同じステージのまま次の試合が始まったときも、始まりの時間帯に戻す
            Apply(TimeOfDay);
        }

        private void Update()
        {
            if (manager != null && !manager.IsInPlay) return;
            if (elapsed >= transitionSeconds) return;
            elapsed += Time.deltaTime;
            Refresh(false);
        }

        private void Refresh(bool force)
        {
            float t = Progress;
            ApplyLighting(StartLighting(TimeOfDay), EndLighting(TimeOfDay), t, force);

            bool lit = TimeOfDay != StageTimeOfDay.Day || t >= lightsOnAt;
            if (lightsOn == lit) return;
            lightsOn = lit;
            foreach (var go in dayOnly) if (go != null) go.SetActive(!lit);
            foreach (var go in eveningOnly) if (go != null) go.SetActive(lit);
        }

        private StageLighting StartLighting(StageTimeOfDay time) => time switch
        {
            StageTimeOfDay.Day => day,
            StageTimeOfDay.Evening => evening,
            _ => night,
        };

        private StageLighting EndLighting(StageTimeOfDay time) => time switch
        {
            StageTimeOfDay.Day => evening,
            _ => night,
        };

        private void ApplyLighting(StageLighting a, StageLighting b, float t, bool force)
        {
            var sun = FindSun();
            if (sun != null)
            {
                sun.color = Color.Lerp(a.sunColor, b.sunColor, t);
                sun.intensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, t);
                sun.transform.rotation = Quaternion.Slerp(Quaternion.Euler(a.sunEuler), Quaternion.Euler(b.sunEuler), t);
                RenderSettings.sun = sun;
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(a.skyAmbient, b.skyAmbient, t);
            RenderSettings.ambientEquatorColor = Color.Lerp(a.equatorAmbient, b.equatorAmbient, t);
            RenderSettings.ambientGroundColor = Color.Lerp(a.groundAmbient, b.groundAmbient, t);

            // 霧がない時間帯は、霧がとても遠くにある扱いにしてなめらかにつなぐ
            RenderSettings.fog = a.fog || b.fog;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Color.Lerp(a.fog ? a.fogColor : b.fogColor, b.fog ? b.fogColor : a.fogColor, t);
            RenderSettings.fogStartDistance = Mathf.Lerp(a.fog ? a.fogStart : 300f, b.fog ? b.fogStart : 300f, t);
            RenderSettings.fogEndDistance = Mathf.Lerp(a.fog ? a.fogEnd : 600f, b.fog ? b.fogEnd : 600f, t);

            // 空のマテリアルはアセットを書き換えないよう複製してから変える
            if (skyboxInstance == null && RenderSettings.skybox != null)
            {
                originalSkybox = RenderSettings.skybox;
                skyboxInstance = new Material(originalSkybox);
                RenderSettings.skybox = skyboxInstance;
            }
            if (skyboxInstance != null)
            {
                if (skyboxInstance.HasProperty("_SkyTint")) skyboxInstance.SetColor("_SkyTint", Color.Lerp(a.skyTint, b.skyTint, t));
                if (skyboxInstance.HasProperty("_AtmosphereThickness")) skyboxInstance.SetFloat("_AtmosphereThickness", Mathf.Lerp(a.atmosphereThickness, b.atmosphereThickness, t));
                if (skyboxInstance.HasProperty("_Exposure")) skyboxInstance.SetFloat("_Exposure", Mathf.Lerp(a.skyExposure, b.skyExposure, t));
            }

            if (force || Time.time - lastRefresh >= environmentRefreshInterval)
            {
                lastRefresh = Time.time;
                DynamicGI.UpdateEnvironment();
            }
        }

        private static Light FindSun()
        {
            if (RenderSettings.sun != null) return RenderSettings.sun;
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional) return light;
            }
            return null;
        }

        private void OnDestroy()
        {
            if (manager != null) manager.GameStarted -= OnGameStarted;
            if (skyboxInstance == null) return;
            if (RenderSettings.skybox == skyboxInstance) RenderSettings.skybox = originalSkybox;
            Destroy(skyboxInstance);
        }
    }
}
