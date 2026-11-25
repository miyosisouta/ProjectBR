using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// シーン遷移を一元管理するクラス。
/// フェードアウト／フェードインの実処理もこのクラスが持つ。
///
/// このコンポーネントは DontDestroyOnLoad で永続化されるため、
/// フェード用のCanvas（黒画像＋CanvasGroup）もここに子オブジェクトとして
/// ぶら下げておけば、どのシーンから呼んでも同じフェードが使い回せる。
///
/// 呼び出し側（SceneTransitionTrigger等）は
///   ・フェードを使うかどうか（bool）
///   ・フェードや遅延の秒数（float）
/// だけを渡せばよい。フェードの中身（Lerpやalpha操作）はここに隠蔽する。
///
/// 遷移の流れ（useFade・useLoadingIndicatorともにtrueの場合）:
///   現在の画面 → 暗転 → ローディング表示(文字がふわっと出る) → ロード → 裏でシーン切り替え(暗いまま)
///   → 文字がフェードアウト → フェードオーバーレイが濃くなる → ローディング画面がフェードアウト
///   → 暗転が解けて次の画面
/// </summary>
public class SceneLoader : MonoBehaviour
{
    private static SceneLoader _instance;

    /// <summary>
    /// シングルトンインスタンス。
    /// シーン内に配置済みならそれを使い、なければ自動生成する。
    /// これにより「SceneLoaderが置かれた起動用シーンを経由せず、
    /// 単体のシーンをエディタで直接Playした」場合でも
    /// NullReferenceExceptionにならない。
    /// ただし自動生成された場合はフェード用CanvasGroupが未設定のため、
    /// フェードは自動的にスキップされる（Resourcesフォルダにプレハブを
    /// 置いておけばフェード付きで自動生成できる。下記コメント参照）。
    /// </summary>
    public static SceneLoader Instance
    {
        get
        {
            if (_instance != null) return _instance;

            // シーン内に既に置かれていないか探す
            _instance = FindFirstObjectByType<SceneLoader>();
            if (_instance != null)
            {
                DontDestroyOnLoad(_instance.gameObject);
                return _instance;
            }

            // Resources/SceneLoader.prefab があればそれを使う
            // （CanvasGroupまで設定済みのプレハブを用意しておくとフェードも自動で有効になる）
            var prefab = Resources.Load<SceneLoader>("SceneLoader");
            if (prefab != null)
            {
                _instance = Instantiate(prefab);
                _instance.name = "SceneLoader";
            }
            else
            {
                Debug.LogWarning(
                    "[SceneLoader] シーン内にSceneLoaderが見つからず、Resources/SceneLoader.prefab も" +
                    "見つからなかったため、フェードなしの簡易インスタンスを自動生成しました。" +
                    "フェードを使いたい場合はブートストラップシーンにSceneLoaderを配置するか、" +
                    "Resourcesフォルダに設定済みのSceneLoaderプレハブを置いてください。");
                var go = new GameObject("SceneLoader (Auto Generated)");
                _instance = go.AddComponent<SceneLoader>();
            }

            DontDestroyOnLoad(_instance.gameObject);
            return _instance;
        }
    }

    [Header("フェード設定")]
    [Tooltip("画面全体を覆う黒画像等のCanvasGroup。未設定でもエラーにはならず、フェードだけスキップされる")]
    [SerializeField] private CanvasGroup _fadeCanvasGroup;

    [Tooltip("呼び出し側でフェード秒数を指定しなかった場合（-1指定）に使うデフォルト値")]
    [SerializeField] private float _defaultFadeOutDuration = 0.3f;
    [SerializeField] private float _defaultFadeInDuration = 0.3f;

    [Header("ローディング表示")]
    [Tooltip("フェードアウト完了後〜フェードイン開始前の間だけ表示するUI（Now Loadingのテキストなど）。任意。FadeCanvasの子にしておくと自動でDontDestroyOnLoad対象になる。このオブジェクト(またはその子)にLoadingScreenを付けると、表示/消去がふわっとフェードする")]
    [SerializeField] private GameObject _loadingIndicator;

    // 読み込み完了後の演出
    [Tooltip("読み込みが終わってから、文字を見せるために止める秒数")]
    [SerializeField] private float _completeHoldSeconds = 0.3f;
    [Tooltip("文字を見せたあと、文字だけがフェードアウトする秒数（LoadingScreenがある場合のみ有効）")]
    [SerializeField] private float _completeFadeSeconds = 0.3f;
    [Tooltip("文字が消えたあと、フェードオーバーレイ（黒）が濃くなってローディング画面全体を覆う秒数")]
    [SerializeField] private float _overlayFadeSeconds = 0.3f;
    [Tooltip("オーバーレイで覆ったあと、ローディング画面そのものがフェードアウトする秒数")]
    [SerializeField] private float _loadingScreenFadeSeconds = 0.3f;

    // フェード中に1フレームで進める時間の上限(秒)。30fps相当。
    // これより重いフレームでは、フェードが指定の秒数より少し長くなる。
    private const float MaxFadeStepSeconds = 1f / 30f;

    // Loading Indicator に付いている LoadingScreen（無ければnull。その場合は即座に消える）
    private LoadingScreen _loadingScreen;

    /// <summary>現在ロード処理中かどうか。多重起動防止に使う</summary>
    public bool IsLoading { get; private set; }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            // 既に他のSceneLoaderが存在する（＝手動配置と自動生成が重複した等）ので自分は破棄
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        // 起動直後は画面が見えている状態（フェードで覆われていない状態）にしておく
        if (_fadeCanvasGroup != null)
        {
            _fadeCanvasGroup.alpha = 0f;
            _fadeCanvasGroup.blocksRaycasts = false;
        }

        // 非アクティブでも取得できるよう includeInactive = true
        if (_loadingIndicator != null)
            _loadingScreen = _loadingIndicator.GetComponentInChildren<LoadingScreen>(true);

        HideLoadingIndicator();
    }

    /// <summary>シーン名で遷移する（フェードなし・最小構成）</summary>
    public UniTask LoadSceneAsync(string sceneName, CancellationToken ct = default)
        => LoadSceneAsync(sceneName, useFade: false, ct: ct);

    /// <summary>
    /// シーン遷移の本体。
    /// 暗転 → シーンロード（最低表示時間待ち含む） → 裏で切り替え → ローディング表示を消す → 暗転を解く、の順で処理する。
    /// </summary>
    /// <param name="sceneName">読み込むシーン名（Build Settingsに追加されていること）</param>
    /// <param name="useFade">フェードイン・アウトを使うか</param>
    /// <param name="useLoadingIndicator">ロード中にLoading Indicatorを表示するか。falseならLoading Indicatorが割り当て済みでも一切触らない</param>
    /// <param name="fadeOutDuration">暗転する秒数。0未満を指定するとデフォルト値を使用</param>
    /// <param name="fadeInDuration">暗転を解く秒数。0未満を指定するとデフォルト値を使用</param>
    /// <param name="progress">ロード進捗(0〜1)の通知先。不要ならnullでよい</param>
    /// <param name="minimumDuration">ロードが一瞬で終わっても、最低この秒数はロード扱いにする</param>
    /// <param name="ct">キャンセルトークン</param>
    public async UniTask LoadSceneAsync(
        string sceneName,
        bool useFade,
        bool useLoadingIndicator = false,
        float fadeOutDuration = -1f,
        float fadeInDuration = -1f,
        IProgress<float> progress = null,
        float minimumDuration = 0.0f,
        CancellationToken ct = default)
    {
        // 多重起動防止：ロード中に別の遷移リクエストが来ても無視する
        if (IsLoading)
        {
            Debug.LogWarning($"[SceneLoader] 既にロード中のため無視しました: {sceneName}");
            return;
        }

        IsLoading = true;
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            ct, destroyCancellationToken);
        var linkedCt = linkedCts.Token;

        try
        {
            if (useFade)
            {
                float duration = fadeOutDuration < 0f ? _defaultFadeOutDuration : fadeOutDuration;
                await FadeOutAsync(duration, linkedCt);
            }

            // フェードで画面が覆われている（＝見えていない）間だけNow Loading的な表示を出す
            // （LoadingScreenが付いていれば、表示された瞬間に自動でふわっと現れる）
            if (useLoadingIndicator) ShowLoadingIndicator();

            var op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
            {
                // sceneNameがBuild SettingsのScene Listに登録されていない場合などにnullが返る
                Debug.LogError(
                    $"[SceneLoader] シーン '{sceneName}' をロードできませんでした。" +
                    "File > Build Profiles（または旧 File > Build Settings）の Scene List に" +
                    "このシーンが追加されているか、シーン名の綴り・大文字小文字が一致しているか確認してください。");

                if (useLoadingIndicator) HideLoadingIndicator();

                // フェードアウト済みなら画面が黒いまま固まらないよう、フェードインして復帰する
                if (useFade)
                {
                    float fadeBackDuration = fadeInDuration < 0f ? _defaultFadeInDuration : fadeInDuration;
                    await FadeInAsync(fadeBackDuration, linkedCt);
                }
                return;
            }
            op.allowSceneActivation = false;

            float startTime = Time.realtimeSinceStartup;
            progress?.Report(0.0f);

            // ロードが90%完了するまで待つ（残り10%はallowSceneActivationを待っている）
            while (true)
            {
                float elapsed = Time.realtimeSinceStartup - startTime;
                float loadRatio = Mathf.Clamp01(op.progress / 0.9f);
                float timeRatio = minimumDuration <= 0.0f ? 1.0f : Mathf.Clamp01(elapsed / minimumDuration);

                // 実進捗と経過時間の遅いほうに合わせると、ゲージが一瞬で振り切れない
                progress?.Report(Mathf.Min(loadRatio, timeRatio));

                if (op.progress >= 0.9f && elapsed >= minimumDuration) break;

                await UniTask.Yield(PlayerLoopTiming.Update, linkedCt);
            }

            progress?.Report(1.0f);

            // 画面が覆われたまま（暗いまま）、裏で新しいシーンに切り替える
            op.allowSceneActivation = true;
            await UniTask.WaitUntil(() => op.isDone, cancellationToken: linkedCt);

            if (useLoadingIndicator)
            {
                // 読み込み完了後、文字を見せるために少し止める
                if (_completeHoldSeconds > 0f)
                {
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(_completeHoldSeconds),
                        ignoreTimeScale: true,
                        cancellationToken: linkedCt);
                }

                if (_loadingScreen != null)
                {
                    // 1) 文字だけがフェードアウト
                    await _loadingScreen.FadeOutTextAsync(_completeFadeSeconds, linkedCt);
                    // 2) フェードオーバーレイ（黒）が濃くなって、ローディング画面全体を覆う
                    await _loadingScreen.FadeInOverlayAsync(_overlayFadeSeconds, linkedCt);
                    // 3) ローディング画面そのものがフェードアウト
                    await _loadingScreen.FadeOutAsync(_loadingScreenFadeSeconds, linkedCt);
                }
                else
                {
                    Debug.LogWarning(
                        "[SceneLoader] Loading Indicator に LoadingScreen が付いていないため、" +
                        "文字はフェードせず即座に消えます");
                }

                HideLoadingIndicator();
            }

            // ローディング画面が消えたら、暗転を解いて次の画面を見せる
            if (useFade)
            {
                float duration = fadeInDuration < 0f ? _defaultFadeInDuration : fadeInDuration;
                await FadeInAsync(duration, linkedCt);
            }
        }
        catch (OperationCanceledException)
        {
            Debug.Log($"[SceneLoader] ロードがキャンセルされました: {sceneName}");
            throw;
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ==================================================
    // フェード処理本体
    // ==================================================

    // フェードアウト/インを単体で呼べるようにpublicにしている。
    // durationに0未満を指定するとデフォルト値を使う。
    // （このクラスでは FadeOut = 画面が暗くなる、FadeIn = 暗いのが消えて画面が見える）
    //
    // どちらも「今の明るさ」から始める。すでに暗い画面でFadeOutを呼んだり、明るい画面でFadeInを
    // 呼んだりしても、いったん逆側に戻ってちらつくことがないようにするため。

    public UniTask FadeOutAsync(float duration = -1f, CancellationToken ct = default)
        => FadeAsync(CurrentFadeAlpha, 1f, duration < 0f ? _defaultFadeOutDuration : duration, ct);

    public UniTask FadeInAsync(float duration = -1f, CancellationToken ct = default)
        => FadeAsync(CurrentFadeAlpha, 0f, duration < 0f ? _defaultFadeInDuration : duration, ct);

    // 今のフェードの濃さ(0=見えている、1=真っ暗)。フェード用のCanvasGroupが無ければ0扱い。
    private float CurrentFadeAlpha => _fadeCanvasGroup != null ? _fadeCanvasGroup.alpha : 0f;

    private async UniTask FadeAsync(float from, float to, float duration, CancellationToken ct)
    {
        if (_fadeCanvasGroup == null)
        {
            Debug.LogWarning("[SceneLoader] _fadeCanvasGroup が未設定のためフェードをスキップしました");
            return;
        }

        // フェード中は下のUIへの入力を止める
        _fadeCanvasGroup.blocksRaycasts = true;

        if (duration <= 0f)
        {
            _fadeCanvasGroup.alpha = to;
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                // ポーズ中(timeScale=0)でも動くように、unscaledDeltaTimeを使う。
                // シーン読み込み直後は1フレームが長くなりやすく(0.3秒を超えることもある)、
                // そのまま足すとフェードインが1フレームで終わって一瞬で明るくなってしまう。
                // 1フレームで進める時間に上限を付けて、重いフレームがあっても少しずつ変わるようにする。
                elapsed += Mathf.Min(Time.unscaledDeltaTime, MaxFadeStepSeconds);
                float t = Mathf.Clamp01(elapsed / duration);
                _fadeCanvasGroup.alpha = Mathf.Lerp(from, to, t);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            _fadeCanvasGroup.alpha = to;
        }

        // 完全に透明（画面が見えている状態）になったら入力を通す
        _fadeCanvasGroup.blocksRaycasts = to > 0f;
    }

    // ==================================================
    // ローディング表示のON/OFF
    // ==================================================

    private void ShowLoadingIndicator()
    {
        if (_loadingIndicator != null) _loadingIndicator.SetActive(true);
    }

    private void HideLoadingIndicator()
    {
        if (_loadingIndicator != null) _loadingIndicator.SetActive(false);
    }
}