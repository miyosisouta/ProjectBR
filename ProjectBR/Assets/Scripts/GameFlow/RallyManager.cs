using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BomberRally
{
    /// <summary>
    /// ラリーの進行役。爆弾の行き先・打ち返し判定・脱落・勝敗を管理する。
    /// 入力は直接読まず、各キャラクターの HitRequested だけを受け取る。
    /// </summary>
    public class RallyManager : MonoBehaviour
    {
        private enum State
        {
            Serving,
            Flying,
            Exploded,
            GameOver,
        }

        [Header("参照")]
        [SerializeField] private DiamondFormation formation;
        [SerializeField] private RallyCharacter[] characters = new RallyCharacter[4];
        [SerializeField] private Bomb bomb;

        [Header("判定")]
        [SerializeField] private HitWindow hitWindow = HitWindow.Default;
        [Tooltip("早押しした後に入力を受け付けない秒数（連打対策）")]
        [SerializeField] private float tooEarlyLockout = 0.4f;

        [Header("爆弾の速さ")]
        [SerializeField] private float initialFlightTime = 1.6f;
        [SerializeField] private float minFlightTime = 0.45f;
        [Tooltip("打ち返すたびに飛行時間に掛ける倍率")]
        [SerializeField] private float speedUpPerHit = 0.94f;
        [Tooltip("スマッシュのときに追加で掛ける倍率")]
        [SerializeField] private float smashMultiplier = 0.75f;

        [Header("間")]
        [SerializeField] private float serveDelay = 1.0f;
        [SerializeField] private float afterExplosionDelay = 1.5f;

        private State state;
        private float stateTimer;
        private float currentFlightTime;
        private RallyCharacter holder;
        private RallyCharacter target;
        private RallyCharacter winner;
        private int rallyCount;
        private string lastJudgement = "";

        private void Start()
        {
            for (int i = 0; i < characters.Length; i++)
            {
                characters[i].Setup(i);
                characters[i].HitRequested += OnHitRequested;
            }
            StartRound();
        }

        private void OnDestroy()
        {
            foreach (var c in characters)
            {
                if (c != null) c.HitRequested -= OnHitRequested;
            }
        }

        private void StartRound()
        {
            foreach (var c in characters) c.ResetForRound();
            formation.Arrange(characters);
            winner = null;
            BeginServe(characters[Random.Range(0, characters.Length)]);
        }

        private void BeginServe(RallyCharacter server)
        {
            holder = server;
            target = null;
            rallyCount = 0;
            currentFlightTime = initialFlightTime;
            bomb.PlaceAt(server.HitPoint);
            SetState(State.Serving, serveDelay);
        }

        private void Update()
        {
            switch (state)
            {
                case State.Serving:
                    if (Time.time >= stateTimer) SendBomb(holder, currentFlightTime);
                    break;

                case State.Flying:
                    if (Time.time > bomb.ArrivalTime + hitWindow.late) ExplodeOn(target);
                    break;

                case State.Exploded:
                    if (Time.time >= stateTimer) BeginServe(PickRandom(AliveCharacters()));
                    break;

                case State.GameOver:
                    var keyboard = Keyboard.current;
                    if (keyboard != null && keyboard.rKey.wasPressedThisFrame) StartRound();
                    break;
            }
        }

        private void OnHitRequested(RallyCharacter character)
        {
            if (state != State.Flying || character != target) return;
            if (Time.time < character.LockedUntil) return;

            var result = hitWindow.Judge(Time.time - bomb.ArrivalTime);
            if (result == HitResult.TooEarly)
            {
                character.LockedUntil = Time.time + tooEarlyLockout;
                lastJudgement = $"{character.DisplayName}: 早すぎ";
                return;
            }

            rallyCount++;
            currentFlightTime = Mathf.Max(minFlightTime, currentFlightTime * speedUpPerHit);
            float flightTime = currentFlightTime;
            if (result == HitResult.Perfect) flightTime = Mathf.Max(minFlightTime, flightTime * smashMultiplier);
            lastJudgement = $"{character.DisplayName}: {(result == HitResult.Perfect ? "スマッシュ!" : "ナイス")}";
            character.PlayHit(result == HitResult.Perfect);

            SendBomb(character, flightTime);
        }

        private void SendBomb(RallyCharacter from, float flightTime)
        {
            holder = from;
            target = PickRandom(AliveCharacters(exclude: from));
            if (target == null)
            {
                EndGame(from);
                return;
            }

            from.ReleaseStance();
            target.TakeStance(from.transform.position);
            bomb.Launch(from.HitPoint, target.HitPoint, flightTime);
            target.Controller.OnBombIncoming(bomb.ArrivalTime, hitWindow);
            SetState(State.Flying, 0f);
        }

        private void ExplodeOn(RallyCharacter victim)
        {
            bomb.Explode();
            victim.Eliminate();
            lastJudgement = $"{victim.DisplayName}: アウト!";

            var alive = AliveCharacters();
            if (alive.Count <= 1)
            {
                EndGame(alive.Count == 1 ? alive[0] : null);
                return;
            }
            SetState(State.Exploded, afterExplosionDelay);
        }

        private void EndGame(RallyCharacter champion)
        {
            winner = champion;
            if (champion != null) champion.PlayVictory();
            SetState(State.GameOver, 0f);
        }

        private void SetState(State next, float delay)
        {
            state = next;
            stateTimer = Time.time + delay;
        }

        private List<RallyCharacter> AliveCharacters(RallyCharacter exclude = null)
        {
            var list = new List<RallyCharacter>();
            foreach (var c in characters)
            {
                if (c.IsAlive && c != exclude) list.Add(c);
            }
            return list;
        }

        private static RallyCharacter PickRandom(List<RallyCharacter> list)
        {
            return list.Count == 0 ? null : list[Random.Range(0, list.Count)];
        }

        // HUD ができるまでの確認用表示
        private void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 22 };
            GUILayout.BeginArea(new Rect(16, 16, 600, 300));
            GUILayout.Label($"ラリー: {rallyCount}   飛行時間: {currentFlightTime:F2}s", style);
            GUILayout.Label(lastJudgement, style);
            if (state == State.GameOver)
            {
                GUILayout.Label(winner != null ? $"{winner.DisplayName} の勝ち!" : "引き分け", style);
                GUILayout.Label("R でもう一度", style);
            }
            GUILayout.EndArea();
        }
    }
}
