using UnityEngine;

namespace BomberRally
{
    /// <summary>
    /// ゲームのシーンに 1 つ置く。試合が始まるたびに、難易度設定にあるステージのプレハブを読み込み、時間帯を反映する。
    /// 同じステージ・同じ時間帯なら読み直さない。ラリーの仕組み・カメラ・UI はシーン側で共通のまま。
    /// </summary>
    public class StageLoader : MonoBehaviour
    {
        [SerializeField] private RallyManager manager;
        [Tooltip("ステージを読み込んだら隠す仮の地面")]
        [SerializeField] private GameObject placeholderGround;
        [Tooltip("ステージを置く位置（ひし形の中心）。未設定ならこの GameObject の位置")]
        [SerializeField] private Transform anchor;

        private GameObject loadedPrefab;
        private StageTimeOfDay loadedTime;
        private GameObject current;

        private void Awake()
        {
            if (manager == null) manager = FindFirstObjectByType<RallyManager>();
            if (manager != null) manager.GameStarted += Load;
        }

        private void Start()
        {
            // 試合開始を待つ間（難易度テストウィンドウなど）も、選んでいる難易度のステージを見せておく
            if (current == null) Load(DifficultySettings.Load(DifficultySettings.Selected));
        }

        private void OnDestroy()
        {
            if (manager != null) manager.GameStarted -= Load;
        }

        public void Load(DifficultySettings settings)
        {
            var prefab = settings != null ? settings.stagePrefab : null;
            var time = settings != null ? settings.timeOfDay : StageTimeOfDay.Day;
            if (current != null && prefab == loadedPrefab && time == loadedTime) return;

            if (current != null) Destroy(current);
            current = null;
            loadedPrefab = prefab;
            loadedTime = time;

            if (placeholderGround != null) placeholderGround.SetActive(prefab == null);
            if (prefab == null) return;

            var origin = anchor != null ? anchor : transform;
            current = Instantiate(prefab, origin.position, origin.rotation, transform);
            current.name = prefab.name;

            var environment = current.GetComponent<StageEnvironment>();
            if (environment != null)
            {
                environment.Apply(time);
                environment.Bind(manager);
            }
            else if (manager != null)
            {
                foreach (var reaction in current.GetComponentsInChildren<IStageReaction>(true)) reaction.Bind(manager);
            }
        }
    }
}
