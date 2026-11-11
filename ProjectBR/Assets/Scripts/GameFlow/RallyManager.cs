using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

namespace BomberRally
{
    /// <summary>
    /// ラリーの進行役。爆弾の行き先・打ち返し判定・脱落・順位・勝敗を管理する。
    /// 入力は直接読まず、各キャラクターの HitRequested だけを受け取る。
    /// AutoStart を切ると、StartGame が呼ばれるまで待ち、終わったら GameFinished で知らせる。
    /// </summary>
    public class RallyManager : MonoBehaviour
    {
        private enum State
        {
            Idle,
            Serving,
            Flying,
            Exploded,
            GameOver,
        }

        [Header("参照")]
        [SerializeField] private DiamondFormation formation;
        [SerializeField] private RallyCharacter[] characters = new RallyCharacter[4];
        [SerializeField] private Bomb bomb;
        [Tooltip("未設定なら同じ GameObject に付ける")]
        [SerializeField] private HitEffect hitEffect;
        [Tooltip("未設定なら同じ GameObject に付ける")]
        [SerializeField] private ExplosionEffect explosionEffect;

        [Header("判定")]
        [Tooltip("早押しした後に入力を受け付けない秒数（連打対策）")]
        [SerializeField] private float tooEarlyLockout = 0.4f;

        [Header("爆弾の速さ（最初・最速の速さは難易度設定にある）")]
        [Tooltip("打ち返すたびに飛行時間に掛ける倍率")]
        [SerializeField] private float speedUpPerHit = 0.94f;
        [Tooltip("スマッシュのときに追加で掛ける倍率")]
        [SerializeField] private float smashMultiplier = 0.75f;

        [Header("間")]
        [SerializeField] private float serveDelay = 1.0f;
        [SerializeField] private float afterExplosionDelay = 1.5f;
        [Tooltip("勝敗が決まってから GameFinished を知らせるまでの秒数（勝ちポーズを見せる間）")]
        [SerializeField] private float afterGameOverDelay = 2.0f;

        private State state;
        private float stateTimer;
        private float currentFlightTime;
        private RallyCharacter holder;
        private RallyCharacter target;
        private RallyCharacter winner;
        private int rallyCount;
        private string lastJudgement = "";
        private DifficultySettings difficulty;
        private readonly RallyRanking ranking = new RallyRanking();

        private HitWindow HitWindow => difficulty.hitWindow;

        /// <summary>false にすると、シーン開始時に始めずに StartGame を待つ。Start より前（Awake）で設定する。</summary>
        public bool AutoStart { get; set; } = true;

        /// <summary>勝敗が決まり、少し間を置いたあとに呼ばれる。引数は勝者（引き分けなら null）。</summary>
        public event Action<RallyCharacter> GameFinished;

        /// <summary>
        /// 誰かの順位が決まったときに呼ばれる（脱落した瞬間・勝者が決まった瞬間）。引数はキャラクターと順位（1 始まり）。
        /// 順位確定時の演出はここにつなぐ。
        /// </summary>
        public event Action<RallyCharacter, int> RankDecided;

        public int RallyCount => rallyCount;

        /// <summary>今の試合の順位。試合が終わった後の結果は RallyRanking.Last でも読める。</summary>
        public RallyRanking Ranking => ranking;

        private void Start()
        {
            if (hitEffect == null) hitEffect = GetComponent<HitEffect>();
            if (hitEffect == null) hitEffect = gameObject.AddComponent<HitEffect>();
            if (explosionEffect == null) explosionEffect = GetComponent<ExplosionEffect>();
            if (explosionEffect == null) explosionEffect = gameObject.AddComponent<ExplosionEffect>();

            for (int i = 0; i < characters.Length; i++)
            {
                characters[i].Setup(i, characters);
                characters[i].HitRequested += OnHitRequested;
            }

            if (AutoStart)
            {
                StartGame(DifficultySettings.Load(DifficultySettings.Selected));
            }
            else
            {
                formation.Arrange(characters);
                bomb.gameObject.SetActive(false);
            }
        }

        /// <summary>指定の難易度で最初から始める。</summary>
        public void StartGame(DifficultySettings settings)
        {
            difficulty = settings;
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
            lastJudgement = "";
            foreach (var c in characters) c.ResetForRound();
            ranking.Reset(characters.Length);
            formation.Arrange(characters);
            winner = null;
            BeginServe(characters[Random.Range(0, characters.Length)]);
        }

        private void BeginServe(RallyCharacter server)
        {
            holder = server;
            target = null;
            rallyCount = 0;
            currentFlightTime = difficulty.initialFlightTime;
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
                    if (Time.time > bomb.ArrivalTime + HitWindow.late) ExplodeOn(target);
                    break;

                case State.Exploded:
                    if (Time.time >= stateTimer) BeginServe(PickRandom(AliveCharacters()));
                    break;

                case State.GameOver:
                    // 知らせる先があればそちらに任せる（テスト用ウィンドウなど）。なければ R でもう一度
                    if (GameFinished != null)
                    {
                        if (Time.time < stateTimer) break;
                        SetState(State.Idle, 0f);
                        GameFinished.Invoke(winner);
                        break;
                    }
                    var keyboard = Keyboard.current;
                    if (keyboard != null && keyboard.rKey.wasPressedThisFrame) StartRound();
                    break;
            }
        }

        private void OnHitRequested(RallyCharacter character, HitDirection direction)
        {
            if (state != State.Flying || character != target) return;
            if (Time.time < character.LockedUntil) return;

            var result = HitWindow.Judge(Time.time - bomb.ArrivalTime);
            if (result == HitResult.TooEarly)
            {
                character.LockedUntil = Time.time + tooEarlyLockout;
                lastJudgement = $"{character.DisplayName}: 早すぎ";
                return;
            }

            rallyCount++;
            float minFlightTime = difficulty.minFlightTime;
            currentFlightTime = Mathf.Max(minFlightTime, currentFlightTime * speedUpPerHit);
            bool smash = result == HitResult.Perfect;
            float flightTime = currentFlightTime;
            if (smash) flightTime = Mathf.Max(minFlightTime, flightTime * smashMultiplier);
            lastJudgement = $"{character.DisplayName}: {(smash ? "スマッシュ!" : "ナイス")}";
            character.PlayHit(smash);

            SendBomb(character, flightTime, direction, smash);
            if (target != null) hitEffect.Play(character.HitPoint, target.HitPoint, smash);
        }

        private void SendBomb(RallyCharacter from, float flightTime, HitDirection direction = HitDirection.None, bool smash = false)
        {
            holder = from;
            target = PickTarget(from, direction);
            if (target == null)
            {
                EndGame(from);
                return;
            }

            from.ReleaseStance();
            target.TakeStance(from.transform.position);
            bomb.Launch(from.HitPoint, target.HitPoint, flightTime, smash);
            target.Controller.OnBombIncoming(bomb.ArrivalTime, flightTime, difficulty);
            SetState(State.Flying, 0f);
        }

        private void ExplodeOn(RallyCharacter victim)
        {
            explosionEffect.Play(bomb.transform.position, victim.transform.position.y);
            bomb.Explode();
            victim.Eliminate();
            int rank = ranking.RecordElimination(victim);
            lastJudgement = $"{victim.DisplayName}: アウト! ({rank}位)";
            RankDecided?.Invoke(victim, rank);

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
            if (champion != null)
            {
                champion.PlayVictory();
                RankDecided?.Invoke(champion, ranking.RecordWinner(champion));
            }
            SetState(State.GameOver, afterGameOverDelay);
        }

        private void SetState(State next, float delay)
        {
            state = next;
            stateTimer = Time.time + delay;
        }

        /// <summary>指定方向の相手。方向指定なし・その相手が脱落済みならランダム。</summary>
        private RallyCharacter PickTarget(RallyCharacter from, HitDirection direction)
        {
            int slot = DiamondFormation.GetSlotInDirection(from.Index, direction);
            if (slot >= 0 && slot < characters.Length && characters[slot].IsAlive && characters[slot] != from)
            {
                return characters[slot];
            }
            return PickRandom(AliveCharacters(exclude: from));
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
            if (difficulty == null) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 22 };
            GUILayout.BeginArea(new Rect(16, 16, 600, 480));
            GUILayout.Label($"難易度: {difficulty.displayName}", style);
            GUILayout.Label($"ラリー: {rallyCount}   飛行時間: {currentFlightTime:F2}s", style);
            GUILayout.Label(lastJudgement, style);
            if (state == State.GameOver)
            {
                foreach (var entry in ranking.GetEntries())
                {
                    GUILayout.Label(entry.ToDisplayString(), style);
                }
                if (GameFinished == null) GUILayout.Label("R でもう一度", style);
            }
            GUILayout.EndArea();
        }
    }
}
