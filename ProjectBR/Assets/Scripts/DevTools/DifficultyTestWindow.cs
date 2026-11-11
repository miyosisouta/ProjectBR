using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace BomberRally
{
    /// <summary>
    /// 難易度を試すためのテスト用ウィンドウ。シーンに置くと、Play を押すまでゲームが始まらない。
    /// 難易度別タブで Easy / Normal / Hard を選び、詳細設定タブでその数値をさらにいじれる。
    /// いじった数値は保存ボタンを押すまでアセットに書き込まない（保存はエディターのみ）。
    /// ゲームが終わるとまたこの画面に戻る。
    /// </summary>
    public class DifficultyTestWindow : MonoBehaviour
    {
        private enum Tab
        {
            Level,
            Detail,
        }

        /// <summary>いじれる数値 1 つ分。詳細設定のスライダーと比較表の両方で使う。</summary>
        private class Item
        {
            public string Label;
            public string Hint;
            public Func<DifficultySettings, float> Get;
            public Action<DifficultySettings, float> Set;
            public float Min;
            public float Max;
            public float Step;
            public bool Percent;

            public string Format(float value) => FormatNumber(value) + Unit;
            public string Unit => Percent ? "%" : "秒";

            /// <summary>入力欄に出す数字（単位なし）。割合は 0.6 ではなく 60 と出す。</summary>
            public string FormatNumber(float value)
            {
                return Percent
                    ? Mathf.RoundToInt(value * 100f).ToString(CultureInfo.InvariantCulture)
                    : value.ToString("0.00#", CultureInfo.InvariantCulture);
            }

            public bool TryParse(string text, out float value)
            {
                if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return false;
                if (Percent) value /= 100f;
                return true;
            }
        }

        private class Section
        {
            public string Title;
            public Item[] Items;
        }

        private static readonly Section[] Sections =
        {
            new Section
            {
                Title = "CPU の強さ",
                Items = new[]
                {
                    new Item
                    {
                        Label = "返球率（序盤）", Hint = "ラリー開始直後に CPU が打ち返す確率",
                        Get = s => s.cpuSuccessRateAtSlowest, Set = (s, v) => s.cpuSuccessRateAtSlowest = v,
                        Min = 0f, Max = 1f, Step = 0.01f, Percent = true,
                    },
                    new Item
                    {
                        Label = "返球率（最速時）", Hint = "爆弾が最速のときに打ち返す確率。速くなるほどこの値に近づく",
                        Get = s => s.cpuSuccessRateAtFastest, Set = (s, v) => s.cpuSuccessRateAtFastest = v,
                        Min = 0f, Max = 1f, Step = 0.01f, Percent = true,
                    },
                    new Item
                    {
                        Label = "スマッシュ率", Hint = "CPU が打ち返したときにスマッシュになる確率",
                        Get = s => s.cpuPerfectRate, Set = (s, v) => s.cpuPerfectRate = v,
                        Min = 0f, Max = 1f, Step = 0.01f, Percent = true,
                    },
                    new Item
                    {
                        Label = "プレイヤーを狙う率", Hint = "CPU が人間プレイヤーを狙って打つ確率",
                        Get = s => s.cpuFocusHumanRate, Set = (s, v) => s.cpuFocusHumanRate = v,
                        Min = 0f, Max = 1f, Step = 0.01f, Percent = true,
                    },
                },
            },
            new Section
            {
                Title = "爆弾の速さ",
                Items = new[]
                {
                    new Item
                    {
                        Label = "初球の到達時間", Hint = "各ラリーの最初の 1 球が届くまでの秒数。脱落者が出るたびにこの速さに戻る",
                        Get = s => s.initialFlightTime,
                        // 最速より遅くならないよう、最速の到達時間も一緒に縮める
                        Set = (s, v) => { s.initialFlightTime = v; s.minFlightTime = Mathf.Min(s.minFlightTime, v); },
                        Min = 0.2f, Max = 3f, Step = 0.05f,
                    },
                    new Item
                    {
                        Label = "最速の到達時間", Hint = "打ち返すたびに速くなり、この秒数で止まる",
                        Get = s => s.minFlightTime, Set = (s, v) => s.minFlightTime = Mathf.Min(v, s.initialFlightTime),
                        Min = 0.1f, Max = 3f, Step = 0.01f,
                    },
                },
            },
            new Section
            {
                Title = "判定の厳しさ",
                Items = new[]
                {
                    new Item
                    {
                        Label = "早押しの猶予", Hint = "爆弾が届く何秒前から打ち返せるか",
                        Get = s => s.hitWindow.early, Set = (s, v) => { var w = s.hitWindow; w.early = v; s.hitWindow = w; },
                        Min = 0f, Max = 0.5f, Step = 0.005f,
                    },
                    new Item
                    {
                        Label = "遅押しの猶予", Hint = "届いてから何秒後まで打ち返せるか。過ぎると爆発",
                        Get = s => s.hitWindow.late, Set = (s, v) => { var w = s.hitWindow; w.late = v; s.hitWindow = w; },
                        Min = 0f, Max = 0.3f, Step = 0.005f,
                    },
                    new Item
                    {
                        Label = "スマッシュの幅", Hint = "届く瞬間の前後この秒数以内に押すとスマッシュ",
                        Get = s => s.hitWindow.perfect, Set = (s, v) => { var w = s.hitWindow; w.perfect = v; s.hitWindow = w; },
                        Min = 0f, Max = 0.2f, Step = 0.005f,
                    },
                },
            },
        };

        private static readonly string[] TabLabels = { "難易度別", "詳細設定" };
        private static readonly DifficultyLevel[] Levels = (DifficultyLevel[])Enum.GetValues(typeof(DifficultyLevel));

        [Tooltip("未設定ならシーンから探す")]
        [SerializeField] private RallyManager manager;
        [Tooltip("オンなら右上、オフなら左上に出す")]
        [SerializeField] private bool alignRight = true;
        [Tooltip("ウィンドウの幅（高さ 720 の画面での大きさ。画面の高さに合わせて拡大縮小する）")]
        [SerializeField] private float windowWidth = 520f;

        private const float ReferenceHeight = 720f;
        private const float Margin = 12f;
        private const float Indent = 14f;
        private const float TableLabelWidth = 140f;
        private const float TableCellWidth = 72f;
        // タブの中身以外（折りたたみ・タブ・比較ボタン・Play・タイトル）の高さのおおよそ。残りをスクロール領域にする
        private const float FixedPartsHeight = 200f;

        private DifficultyLevel level;
        private DifficultySettings custom;
        private bool customized;
        private Tab tab;
        private bool collapsed;
        private bool showCompare;
        private bool playing;
        // 前回の結果（1 行目が難易度、続いて順位）。複数行を 1 つのラベルにすると高さが足りず切れるので 1 行ずつ持つ
        private readonly List<string> lastResultLines = new List<string>();
        private string saveMessage = "";

        // 毎フレーム高さ 0 から作り直すと GUILayout の大きさ計算が 1 フレーム遅れて点滅するので、前回の大きさを覚えておく。
        // 中身が減る操作（タブ切り替えなど）をしたときだけ縮め直す。
        private Rect mainRect;
        private Rect compareRect;
        private bool shrink = true;
        private Vector2 scroll;
        private float contentHeight = ReferenceHeight;
        // 入力欄に打ちかけの文字。打っている途中の "0." などを数字に直して消さないよう、フォーカス中だけ覚えておく
        private readonly Dictionary<Item, string> editingText = new Dictionary<Item, string>();

        private GUIStyle sectionStyle;
        private GUIStyle itemStyle;
        private GUIStyle hintStyle;
        private GUIStyle valueStyle;
        private GUIStyle itemHintStyle;
        private GUIStyle fieldStyle;
        private GUIStyle cellStyle;
        private GUIStyle selectedCellStyle;
        private GUIStyle changedCellStyle;

        private void Awake()
        {
            if (manager == null) manager = FindFirstObjectByType<RallyManager>();
            if (manager == null)
            {
                Debug.LogWarning("RallyManager が見つからないため難易度テストウィンドウを止めます", this);
                enabled = false;
                return;
            }
            manager.AutoStart = false;
            manager.GameFinished += OnGameFinished;
            SelectLevel(DifficultySettings.Selected);
        }

        private void OnDestroy()
        {
            if (manager != null) manager.GameFinished -= OnGameFinished;
            if (custom != null) Destroy(custom);
        }

        /// <summary>難易度を選び直す。詳細設定でいじった値はその難易度の値に戻る。</summary>
        private void SelectLevel(DifficultyLevel next)
        {
            level = next;
            DifficultySettings.Selected = next;
            if (custom != null) Destroy(custom);
            // 保存ボタンを押すまでアセットを書き換えないよう複製をいじる
            custom = Instantiate(DifficultySettings.Load(next));
            customized = false;
            saveMessage = "";
            editingText.Clear();
        }

        private void Play()
        {
            custom.displayName = customized ? $"{level}（カスタム）" : level.ToString();
            playing = true;
            manager.StartGame(custom);
        }

        private void Save()
        {
#if UNITY_EDITOR
            var asset = DifficultySettings.Load(level);
            if (!UnityEditor.AssetDatabase.Contains(asset))
            {
                saveMessage = $"Resources/{DifficultySettings.ResourceFolder}{level} が見つからないため保存できません";
                return;
            }
            UnityEditor.Undo.RecordObject(asset, "難易度設定を保存");
            asset.CopyValuesFrom(custom);
            UnityEditor.EditorUtility.SetDirty(asset);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(asset);
            customized = false;
            saveMessage = $"{level} に保存しました";
#endif
        }

        private void OnGameFinished(RallyCharacter winner)
        {
            playing = false;
            lastResultLines.Clear();
            lastResultLines.Add($"前回: {custom.displayName}");
            foreach (var entry in RallyRanking.Last)
            {
                lastResultLines.Add($"  {entry.ToDisplayString()}");
            }
            // 行が増えた分ウィンドウを測り直す
            shrink = true;
        }

        private void OnGUI()
        {
            if (playing) return;
            EnsureStyles();

            float scale = Screen.height / ReferenceHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float screenWidth = Screen.width / scale;

            if (shrink)
            {
                mainRect.height = 0f;
                compareRect = new Rect(0f, 0f, 0f, 0f);
                shrink = false;
            }

            mainRect.width = Mathf.Max(mainRect.width, windowWidth);
            mainRect.x = alignRight ? screenWidth - mainRect.width - Margin : Margin;
            mainRect.y = Margin;
            mainRect = GUILayout.Window(GetInstanceID(), mainRect, DrawMainWindow, "難易度テスト");

            if (showCompare && !collapsed)
            {
                // メインウィンドウの内側（画面の中央寄り）に並べる
                compareRect.x = alignRight ? mainRect.x - compareRect.width - Margin : mainRect.xMax + Margin;
                compareRect.y = Margin;
                compareRect = GUILayout.Window(GetInstanceID() + 1, compareRect, DrawCompareWindow, "難易度の比較");
            }
        }

        private void DrawMainWindow(int id)
        {
            if (GUILayout.Button(collapsed ? "▶ 開く" : "▼ 折りたたむ"))
            {
                collapsed = !collapsed;
                shrink = true;
            }

            if (!collapsed)
            {
                foreach (var line in lastResultLines) GUILayout.Label(line, itemStyle);
                GUILayout.Space(4f);

                var nextTab = (Tab)GUILayout.Toolbar((int)tab, TabLabels, GUILayout.Height(26f));
                if (nextTab != tab)
                {
                    tab = nextTab;
                    scroll = Vector2.zero;
                    shrink = true;
                }
                GUILayout.Space(6f);

                // 画面に収まらないときはマウスホイールでスクロールできるようにする
                float resultHeight = lastResultLines.Count * itemStyle.lineHeight;
                float maxHeight = ReferenceHeight - Margin * 2f - FixedPartsHeight - resultHeight;
                bool needsScroll = contentHeight > maxHeight;
                scroll = GUILayout.BeginScrollView(scroll, false, needsScroll, GUIStyle.none, GUI.skin.verticalScrollbar,
                    GUILayout.Height(Mathf.Min(contentHeight, maxHeight)));
                // 中身の幅を決めておかないと、スクロールバーの分だけ右端（値の入力欄）がはみ出して見えなくなる
                var scrollbar = GUI.skin.verticalScrollbar;
                float contentWidth = mainRect.width - GUI.skin.window.padding.horizontal
                    - (needsScroll ? scrollbar.fixedWidth + scrollbar.margin.horizontal : 0f) - 4f;
                GUILayout.BeginVertical(GUILayout.Width(contentWidth));
                if (tab == Tab.Level) DrawLevelTab();
                else DrawDetailTab();
                GUILayout.EndVertical();
                // 中身の高さは描画のときにしか正しく取れないので、次のフレームのスクロール領域の高さに使う。
                // 変わったとき（タブ切り替えなど）だけウィンドウを縮め直す
                if (Event.current.type == EventType.Repaint)
                {
                    float height = GUILayoutUtility.GetLastRect().height;
                    if (!Mathf.Approximately(height, contentHeight))
                    {
                        contentHeight = height;
                        shrink = true;
                    }
                }
                GUILayout.EndScrollView();

                GUILayout.Space(6f);
                if (GUILayout.Button(showCompare ? "比較表を閉じる" : "難易度を表で比較する"))
                {
                    showCompare = !showCompare;
                    shrink = true;
                }
                GUILayout.Space(6f);
            }

            var oldColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.7f, 1f, 0.2f);
            if (GUILayout.Button(customized ? $"Play（{level} 変更あり）" : $"Play（{level}）", GUILayout.Height(36f))) Play();
            GUI.backgroundColor = oldColor;
        }

        private void DrawLevelTab()
        {
            using (new GUILayout.HorizontalScope())
            {
                foreach (var l in Levels)
                {
                    bool selected = l == level;
                    // 選び中のものをもう一度押しても変更中の値は消さない
                    if (GUILayout.Toggle(selected, l.ToString(), GUI.skin.button, GUILayout.Height(30f)) && !selected) SelectLevel(l);
                }
            }
            GUILayout.Space(6f);
            if (customized) GUILayout.Label("※ 詳細設定で変更中の値を使います", hintStyle);

            foreach (var section in Sections)
            {
                BeginSection(section.Title);
                foreach (var item in section.Items)
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.Space(Indent);
                        GUILayout.Label(item.Label, itemStyle);
                        GUILayout.FlexibleSpace();
                        GUILayout.Label(item.Format(item.Get(custom)), valueStyle, GUILayout.Width(64f));
                    }
                }
                EndSection();
            }
        }

        private void DrawDetailTab()
        {
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label(customized ? $"{level} をもとに変更中" : $"{level} の値", itemStyle);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("元に戻す", GUILayout.Width(80f))) SelectLevel(level);
            }

            foreach (var section in Sections)
            {
                BeginSection(section.Title);
                foreach (var item in section.Items) DrawSlider(item);
                EndSection();
            }

#if UNITY_EDITOR
            GUI.enabled = customized;
            if (GUILayout.Button($"この値を {level} に保存", GUILayout.Height(28f))) Save();
            GUI.enabled = true;
#else
            GUILayout.Label("保存はエディターでのみできます", hintStyle);
#endif
            if (saveMessage.Length > 0) GUILayout.Label(saveMessage, hintStyle);
        }

        /// <summary>見出し付きの枠を始める。中の項目は字下げして並べる。</summary>
        private void BeginSection(string title)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(title, sectionStyle);
        }

        private static void EndSection()
        {
            GUILayout.EndVertical();
            GUILayout.Space(4f);
        }

        /// <summary>
        /// 名前・スライダー・入力欄を 1 行に、説明をその下に並べる。値が変わったら変更あり扱いにする。
        /// 入力欄に打った値はスライダーの刻みに丸めない（範囲には収める）。
        /// </summary>
        private void DrawSlider(Item item)
        {
            float value = item.Get(custom);
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Space(Indent);
                GUILayout.Label(item.Label, itemStyle, GUILayout.Width(120f));
                float next = GUILayout.HorizontalSlider(value, item.Min, item.Max, GUILayout.ExpandWidth(true));

                // 触っていないときに刻みへ丸めると、刻みに乗っていない値（0.144 など）が勝手に変わるので、動かしたときだけ丸める
                if (next != value)
                {
                    next = Mathf.Clamp(Mathf.Round(next / item.Step) * item.Step, item.Min, item.Max);
                    editingText.Remove(item);
                    GUI.FocusControl(null);
                    Apply(item, next);
                    value = next;
                }

                string controlName = "DifficultyField_" + item.Label;
                bool focused = GUI.GetNameOfFocusedControl() == controlName;
                if (!focused) editingText.Remove(item);
                string shown = editingText.TryGetValue(item, out var typing) ? typing : item.FormatNumber(value);

                GUI.SetNextControlName(controlName);
                string typed = GUILayout.TextField(shown, fieldStyle, GUILayout.Width(52f));
                GUILayout.Label(item.Unit, itemStyle, GUILayout.Width(24f));
                if (typed != shown)
                {
                    editingText[item] = typed;
                    if (item.TryParse(typed, out float parsed)) Apply(item, Mathf.Clamp(parsed, item.Min, item.Max));
                }
            }
            // 説明は長いと幅を押し広げて右端を見切れさせるので、折り返して中身の幅に収める
            GUILayout.Label(item.Hint, itemHintStyle);
        }

        private void Apply(Item item, float value)
        {
            item.Set(custom, value);
            customized = true;
            saveMessage = "";
        }

        private void DrawCompareWindow(int id)
        {
            // 列：Easy / Normal / Hard（＋変更中の値）。選んでいる難易度の列を強調する
            var assets = new DifficultySettings[Levels.Length];
            for (int i = 0; i < Levels.Length; i++) assets[i] = DifficultySettings.Load(Levels[i]);

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("", GUILayout.Width(TableLabelWidth));
                for (int i = 0; i < Levels.Length; i++)
                {
                    GUILayout.Label(Levels[i].ToString(), Levels[i] == level ? selectedCellStyle : cellStyle, GUILayout.Width(TableCellWidth));
                }
                if (customized) GUILayout.Label("変更中", changedCellStyle, GUILayout.Width(TableCellWidth));
            }

            foreach (var section in Sections)
            {
                BeginSection(section.Title);
                foreach (var item in section.Items)
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.Space(Indent);
                        GUILayout.Label(item.Label, itemStyle, GUILayout.Width(TableLabelWidth - Indent - 4f));
                        for (int i = 0; i < Levels.Length; i++)
                        {
                            var style = Levels[i] == level ? selectedCellStyle : cellStyle;
                            GUILayout.Label(item.Format(item.Get(assets[i])), style, GUILayout.Width(TableCellWidth));
                        }
                        if (customized)
                        {
                            // 元の値から変えたところだけ色を付ける
                            float value = item.Get(custom);
                            bool changed = !Mathf.Approximately(value, item.Get(assets[(int)level]));
                            GUILayout.Label(item.Format(value), changed ? changedCellStyle : cellStyle, GUILayout.Width(TableCellWidth));
                        }
                    }
                }
                EndSection();
            }
        }

        // GUI.skin は OnGUI の中でしか触れないので、初回の OnGUI で作る
        private void EnsureStyles()
        {
            if (sectionStyle != null) return;

            sectionStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            sectionStyle.normal.textColor = new Color(1f, 0.75f, 0.3f);

            itemStyle = new GUIStyle(GUI.skin.label) { fontSize = 13 };

            // 折り返しありのラベルは自動で大きさが決まるウィンドウの中で高さがぶれるので、折り返さない
            hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, wordWrap = false };
            // 中身の幅を固定したので、こちらは折り返しても高さはぶれない
            itemHintStyle = new GUIStyle(hintStyle) { wordWrap = true };
            itemHintStyle.margin.left += (int)(Indent * 2f);
            hintStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);

            valueStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };

            fieldStyle = new GUIStyle(GUI.skin.textField) { fontSize = 13, alignment = TextAnchor.MiddleRight };

            cellStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
            selectedCellStyle = new GUIStyle(cellStyle) { fontStyle = FontStyle.Bold };
            selectedCellStyle.normal.textColor = new Color(0.7f, 1f, 0.2f);
            changedCellStyle = new GUIStyle(cellStyle) { fontStyle = FontStyle.Bold };
            changedCellStyle.normal.textColor = new Color(1f, 0.85f, 0.2f);
        }
    }
}
