using System.Collections;
using UnityEngine;

namespace BomberRally
{
    /// <summary>
    /// 手前の道路を走る車。各車は自分の前（+Z）方向へ進み、端まで行くと反対側から戻ってくる。
    /// 爆発すると全員が急ブレーキで止まり（つんのめる）、少しして走り出す。
    /// </summary>
    public class RoadTraffic : MonoBehaviour
    {
        [SerializeField] private Transform[] cars = new Transform[0];
        [SerializeField] private Vector2 speedRange = new Vector2(6f, 9f);
        [Tooltip("道路の中心から端までの長さ。これを越えると反対側に戻る")]
        [SerializeField] private float halfLength = 24f;
        [SerializeField] private float brakeDuration = 2.2f;
        [Tooltip("急ブレーキでつんのめる角度")]
        [SerializeField] private float brakePitch = 6f;

        private float[] speeds;
        private Quaternion[] baseRotations;
        private float factor = 1f;
        private float brakeUntil = -10f;
        private float brakeStart = -10f;

        private void Awake()
        {
            speeds = new float[cars.Length];
            baseRotations = new Quaternion[cars.Length];
            for (int i = 0; i < cars.Length; i++)
            {
                speeds[i] = Random.Range(speedRange.x, speedRange.y);
                if (cars[i] != null) baseRotations[i] = cars[i].localRotation;
            }
        }

        public void Brake()
        {
            brakeStart = Time.time;
            brakeUntil = Time.time + brakeDuration;
        }

        private void Update()
        {
            bool braking = Time.time < brakeUntil;
            factor = Mathf.MoveTowards(factor, braking ? 0f : 1f, Time.deltaTime * (braking ? 5f : 0.7f));
            float lurch = Mathf.Clamp01(1f - (Time.time - brakeStart) / 0.5f);

            for (int i = 0; i < cars.Length; i++)
            {
                var car = cars[i];
                if (car == null) continue;
                var local = car.localPosition + baseRotations[i] * Vector3.forward * (speeds[i] * factor * Time.deltaTime);
                if (local.x > halfLength) local.x -= halfLength * 2f;
                if (local.x < -halfLength) local.x += halfLength * 2f;
                car.localPosition = local;
                car.localRotation = baseRotations[i] * Quaternion.Euler(brakePitch * Mathf.Sin(lurch * Mathf.PI), 0f, 0f);
            }
        }
    }
}
