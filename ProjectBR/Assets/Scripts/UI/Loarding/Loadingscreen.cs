using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ローディング画面(Now Loading の文字など)のフェード担当。
/// 暗転用の黒い全画面フェードは SceneLoader が持っているので、ここでは持たない。
/// SceneLoader の Loading Indicator に設定したオブジェクトに付ける。
///
/// 3つの動きを個別に操作できる:
///  1) 文字だけ消す        … FadeOutTextAsync    (textGroup のアルファ 1→0)
///  2) 全体を黒く覆う      … FadeInOverlayAsync  (fadeOverlay のアルファ 0→1)
///  3) ローディング画面ごと消す … FadeOutAsync    (rootGroup のアルファ 1→0)
/// 表示された瞬間は、自動でローディング画面全体がふわっと現れる(FadeInAsync)。
///
/// 推奨の階層:
///   LoadingIndicator   ← LoadingScreen + CanvasGroup(=rootGroup)
///    ├ Background      ← ローディング画面の背景(任意)
///    ├ LoadingText     ← WaveText + CanvasGroup(=textGroup)
///    └ fadeOverlay     ← 全画面の黒Image(一番手前=最後の子、アルファ0)
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class LoadingScreen : MonoBehaviour
{
    [Header("ローディング画面全体")]
    [Tooltip("ローディング画面全体のアルファを操作する。未設定なら同じオブジェクトから自動取得")]
    [SerializeField] private CanvasGroup rootGroup;

    [Tooltip("オンにすると、SetActive(true) された瞬間に自動でフェードイン")]
    [SerializeField] private bool fadeInOnEnable = true;

    [Tooltip("表示時のフェード時間(秒)")]
    [SerializeField] private float enterFadeDuration = 0.4f;

    [Header("文字だけを消すためのCanvasGroup")]
    [Tooltip("「読み込み中」の文字(WaveTextのオブジェクト)に付けたCanvasGroup")]
    [SerializeField] private CanvasGroup textGroup;

    [Header("全体を覆う黒いオーバーレイ")]
    [Tooltip("ローディング画面の一番手前に置く、全画面の黒いImage")]
    [SerializeField] private Image fadeOverlay;

    private CancellationTokenSource enterCts;
    private float targetProgress;

    private void Awake()
    {
        if (rootGroup == null) rootGroup = GetComponent<CanvasGroup>();
    }

    private void OnEnable()
    {
        targetProgress = 0f;

        // 毎回、文字は見える状態・オーバーレイは透明から始める
        if (textGroup != null) textGroup.alpha = 1f;
        SetOverlayAlpha(0f);

        if (fadeInOnEnable)
        {
            rootGroup.alpha = 0f;
            enterCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            FadeInAsync(enterFadeDuration, enterCts.Token).Forget();
        }
        else
        {
            rootGroup.alpha = 1f;
        }
    }

    private void OnDisable()
    {
        // 表示中のフェードが残らないように止める
        enterCts?.Cancel();
        enterCts?.Dispose();
        enterCts = null;
    }

    /// <summary>進捗を 0〜1 で渡す(バーは無いので値を保持するだけ。旧LoadingBarとの互換用)</summary>
    public void SetProgress(float progress) => targetProgress = Mathf.Clamp01(progress);

    public bool IsFilled => targetProgress >= 1f;

    /// <summary>ローディング画面全体を、今のアルファから 1 へ表示する</summary>
    public UniTask FadeInAsync(float duration, CancellationToken ct = default)
        => LerpAsync(rootGroup.alpha, 1f, duration, a => rootGroup.alpha = a, ct);

    /// <summary>ローディング画面全体を、今のアルファから 0 へ消す</summary>
    public UniTask FadeOutAsync(float duration, CancellationToken ct = default)
        => LerpAsync(rootGroup.alpha, 0f, duration, a => rootGroup.alpha = a, ct);

    /// <summary>文字だけを 1→0 でフェードアウトさせる(背景・オーバーレイは残る)</summary>
    public UniTask FadeOutTextAsync(float duration, CancellationToken ct = default)
    {
        if (textGroup == null)
        {
            Debug.LogWarning("[LoadingScreen] Text Group が未設定のため、文字のフェードをスキップしました");
            return UniTask.CompletedTask;
        }
        return LerpAsync(textGroup.alpha, 0f, duration, a => textGroup.alpha = a, ct);
    }

    /// <summary>黒いオーバーレイを 0→1 で濃くして、ローディング画面全体を覆う</summary>
    public UniTask FadeInOverlayAsync(float duration, CancellationToken ct = default)
    {
        if (fadeOverlay == null)
        {
            Debug.LogWarning("[LoadingScreen] Fade Overlay が未設定のため、オーバーレイのフェードをスキップしました");
            return UniTask.CompletedTask;
        }
        return LerpAsync(fadeOverlay.color.a, 1f, duration, SetOverlayAlpha, ct);
    }

    private static async UniTask LerpAsync(
        float from, float to, float duration, Action<float> apply, CancellationToken ct)
    {
        if (duration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                apply(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }

        apply(to);
    }

    private void SetOverlayAlpha(float alpha)
    {
        if (fadeOverlay == null) return;
        var c = fadeOverlay.color;
        c.a = alpha;
        fadeOverlay.color = c;
    }
}