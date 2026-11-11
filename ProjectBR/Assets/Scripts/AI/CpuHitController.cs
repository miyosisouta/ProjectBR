using System.Collections.Generic;
using UnityEngine;

namespace BomberRally
{
    /// <summary>
    /// CPU の操作役。爆弾が飛んできた時点で成功するかを抽選し、決めた時刻にボタンを押す。
    /// 強さ（成功率・スマッシュ率・人間を狙う率）は DifficultySettings にある。
    /// 成功率は爆弾が速いほど下がるので、序盤はラリーが続き、速くなると崩れやすくなる。
    /// </summary>
    public class CpuHitController : MonoBehaviour, IHitController
    {
        private static readonly HitDirection[] AimDirections = { HitDirection.Forward, HitDirection.Left, HitDirection.Right };

        private RallyCharacter owner;
        private float scheduledPressTime = -1f;
        private float focusHumanRate;

        public void Initialize(RallyCharacter owner) => this.owner = owner;

        public void OnBombIncoming(float arrivalTime, float flightTime, DifficultySettings difficulty)
        {
            focusHumanRate = difficulty.cpuFocusHumanRate;
            if (Random.value > difficulty.CpuSuccessRate(flightTime))
            {
                // 失敗：押さずに見送る
                scheduledPressTime = -1f;
                return;
            }

            var window = difficulty.hitWindow;
            float offset = Random.value < difficulty.cpuPerfectRate
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
            owner.RequestHit(PickAim());
        }

        /// <summary>
        /// 一定の確率で生き残っている人間を狙い、それ以外はランダム。
        /// 脱落済みの方向を選んだときは RallyManager がランダムに振り直す。
        /// </summary>
        private HitDirection PickAim()
        {
            if (Random.value < focusHumanRate)
            {
                var humans = new List<HitDirection>();
                foreach (var dir in AimDirections)
                {
                    var rival = owner.GetRivalInDirection(dir);
                    if (rival != null && rival.IsAlive && rival.IsHuman) humans.Add(dir);
                }
                if (humans.Count > 0) return humans[Random.Range(0, humans.Count)];
            }
            return AimDirections[Random.Range(0, AimDirections.Length)];
        }
    }
}
