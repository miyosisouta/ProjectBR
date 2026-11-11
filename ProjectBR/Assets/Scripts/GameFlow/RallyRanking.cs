using System.Collections.Generic;

namespace BomberRally
{
    /// <summary>
    /// 1 試合分の順位。最初に脱落した人が最下位、最後まで残った人が 1 位。
    /// シーンをまたいで読めるよう、試合が終わると Last に結果を残す（リザルト画面用）。
    /// </summary>
    public class RallyRanking
    {
        /// <summary>順位 1 人分。リザルト画面はこれだけ読めば表示できる。</summary>
        public readonly struct Entry
        {
            /// <summary>1 始まりの順位</summary>
            public readonly int Rank;
            /// <summary>立ち位置の番号（0 始まり。P1 = 0）</summary>
            public readonly int PlayerIndex;
            public readonly string DisplayName;
            public readonly bool IsHuman;

            public Entry(int rank, RallyCharacter character)
            {
                Rank = rank;
                PlayerIndex = character.Index;
                DisplayName = character.DisplayName;
                IsHuman = character.IsHuman;
            }

            /// <summary>"1P" のようなプレイヤー番号の表記</summary>
            public string PlayerLabel => $"{PlayerIndex + 1}P";

            /// <summary>"1位:1P" の表記。CPU には (CPU) を付ける。</summary>
            public string ToDisplayString() => $"{Rank}位:{PlayerLabel}{(IsHuman ? "" : " (CPU)")}";
        }

        /// <summary>直前に終わった試合の順位（1 位から順）。まだ試合がなければ空。</summary>
        public static IReadOnlyList<Entry> Last { get; private set; } = new Entry[0];

        private readonly Dictionary<RallyCharacter, int> ranks = new Dictionary<RallyCharacter, int>();
        private int participantCount;

        /// <summary>順位が決まっていない人の中で、次に脱落した人がなる順位</summary>
        public int NextEliminationRank => participantCount - ranks.Count;

        public bool IsComplete => participantCount > 0 && ranks.Count == participantCount;

        public void Reset(int participants)
        {
            participantCount = participants;
            ranks.Clear();
        }

        /// <summary>脱落を記録して、その人の順位を返す。</summary>
        public int RecordElimination(RallyCharacter character) => Record(character, NextEliminationRank);

        /// <summary>最後まで残った人を 1 位にする。</summary>
        public int RecordWinner(RallyCharacter character) => Record(character, 1);

        /// <summary>順位（1 始まり）。まだ決まっていなければ 0。</summary>
        public int GetRank(RallyCharacter character) => ranks.TryGetValue(character, out int rank) ? rank : 0;

        /// <summary>決まっている順位を 1 位から順に並べる。</summary>
        public List<Entry> GetEntries()
        {
            var list = new List<Entry>();
            foreach (var pair in ranks) list.Add(new Entry(pair.Value, pair.Key));
            list.Sort((a, b) => a.Rank.CompareTo(b.Rank));
            return list;
        }

        private int Record(RallyCharacter character, int rank)
        {
            if (ranks.TryGetValue(character, out int existing)) return existing;
            ranks[character] = rank;
            if (IsComplete) Last = GetEntries();
            return rank;
        }
    }
}
