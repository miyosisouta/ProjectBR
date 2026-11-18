using System.Collections;
using UnityEngine;

namespace BomberRally
{
    /// <summary>
    /// ガソリンスタンドの爆発演出。爆弾が爆発すると、脱落した人の後ろの給油機 1 台だけが派手に爆発して燃え上がる。
    /// 横の車が跳ね、道路の車は急ブレーキ、店員が消火器で少しずつ火を消す。ほかの給油機は誘爆しない。
    /// 爆発の見た目は ExplosionEffect を流用する。ヒットストップと画面揺れは本体の爆発に任せ、こちらでは出さない。
    /// </summary>
    public class GasStationReaction : MonoBehaviour, IStageReaction
    {
        [SerializeField] private FuelPump[] pumps = new FuelPump[0];
        [SerializeField] private ParkedCar[] parkedCars = new ParkedCar[0];
        [SerializeField] private RoadTraffic traffic;
        [SerializeField] private GasStationStaff staff;

        [Header("給油機の爆発")]
        [Tooltip("本体の爆発から給油機が爆発するまでの秒数")]
        [SerializeField] private float delay = 0.2f;
        [Tooltip("給油機の爆発の大きさ（本体の爆発に対する倍率）")]
        [SerializeField] private float blastScale = 1.5f;
        [Tooltip("1 発目に続いて上で起きる 2 発目までの秒数（火柱が立ち上がるように見せる）")]
        [SerializeField] private float secondBlastDelay = 0.14f;
        [SerializeField] private float secondBlastHeight = 1.6f;
        [Tooltip("給油機からこの距離以内の車が跳ねる")]
        [SerializeField] private float carReach = 4.5f;

        private RallyManager manager;
        private ExplosionEffect blastEffect;
        private ExplosionEffectSettings blastSettings;

        public void Bind(RallyManager rallyManager)
        {
            Unbind();
            manager = rallyManager;
            if (manager == null) return;
            manager.BombExploded += OnBombExploded;
            manager.GameStarted += OnGameStarted;
        }

        private void Awake()
        {
            blastEffect = gameObject.AddComponent<ExplosionEffect>();
            blastSettings = CreateBlastSettings();
            blastEffect.Settings = blastSettings;
        }

        private void OnDestroy()
        {
            Unbind();
            if (blastSettings != null) Destroy(blastSettings);
        }

        private void Unbind()
        {
            if (manager == null) return;
            manager.BombExploded -= OnBombExploded;
            manager.GameStarted -= OnGameStarted;
            manager = null;
        }

        private void OnGameStarted(DifficultySettings settings)
        {
            StopAllCoroutines();
            foreach (var pump in pumps) if (pump != null) pump.ResetState();
            if (staff != null) staff.ReturnHome();
        }

        private void OnBombExploded(RallyCharacter victim, Vector3 position)
        {
            if (traffic != null) traffic.Brake();
            var pump = PumpBehind(victim != null ? victim.transform.position : position);
            if (pump != null) StartCoroutine(Blast(pump));
        }

        /// <summary>脱落した人のすぐ後ろ（一番近い）の給油機。</summary>
        private FuelPump PumpBehind(Vector3 point)
        {
            FuelPump best = null;
            float bestDistance = float.MaxValue;
            foreach (var pump in pumps)
            {
                if (pump == null) continue;
                float d = (pump.transform.position - point).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = pump;
                }
            }
            return best;
        }

        private IEnumerator Blast(FuelPump pump)
        {
            yield return new WaitForSeconds(delay);
            blastEffect.Play(pump.BlastPoint, pump.transform.position.y);
            pump.Detonate();
            pump.Ignite();
            foreach (var car in parkedCars)
            {
                if (car == null) continue;
                float distance = Vector3.Distance(car.transform.position, pump.transform.position);
                if (distance <= carReach) car.Bounce(1f - distance / carReach * 0.5f);
            }

            yield return new WaitForSeconds(secondBlastDelay);
            blastEffect.Play(pump.BlastPoint + Vector3.up * secondBlastHeight, pump.transform.position.y);

            if (staff != null) staff.Extinguish(pump);
        }

        private ExplosionEffectSettings CreateBlastSettings()
        {
            var source = Resources.Load<ExplosionEffectSettings>(ExplosionEffectSettings.ResourcePath);
            var s = source != null ? Instantiate(source) : ScriptableObject.CreateInstance<ExplosionEffectSettings>();
            s.scale *= blastScale;
            s.hitStopDuration = 0f;
            s.shakeAmplitude = 0f;
            s.screenFlashAlpha = 0f;
            return s;
        }
    }
}
