using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// パネルのフェードイン/フェードアウト。Panel(CanvasGroup付き)に付ける。
/// CanvasGroup が無ければ自動で追加される。
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class PanelFader : MonoBehaviour
{
    public enum FadeMode { FadeIn, FadeOut }

    [Header("フェード設定")]
    [SerializeField] FadeMode mode = FadeMode.FadeOut;
    [SerializeField, Min(0f)] float delay = 0f;      // 開始までの待ち時間(秒)
    [SerializeField, Min(0f)] float duration = 1f;   // フェードにかける時間(秒)
    [SerializeField] AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("開始・終了時の挙動")]
    [SerializeField] bool playOnStart = true;        // Start時に自動で再生
    [SerializeField] bool useStartAlpha = true;      // 開始時のアルファを自動設定(FadeIn=0, FadeOut=1)
    [SerializeField] bool deactivateOnFadeOut = false; // フェードアウト終了後に非アクティブ化
    [SerializeField] bool ignoreTimeScale = true;    // Time.timeScale の影響を受けない

    [Header("イベント")]
    public UnityEvent onFadeComplete;

    CanvasGroup group;
    Coroutine running;

    public bool IsPlaying => running != null;

    void Awake() => group = GetComponent<CanvasGroup>();

    void Start()
    {
        if (playOnStart) Play();
    }

    /// <summary>インスペクターの設定どおりに再生</summary>
    public void Play() => Play(mode, delay, duration);

    public void PlayFadeIn() => Play(FadeMode.FadeIn, delay, duration);
    public void PlayFadeOut() => Play(FadeMode.FadeOut, delay, duration);

    /// <summary>コードから設定を指定して再生</summary>
    public void Play(FadeMode fadeMode, float delaySeconds, float durationSeconds)
    {
        if (group == null) group = GetComponent<CanvasGroup>();
        if (running != null) StopCoroutine(running);
        gameObject.SetActive(true);
        running = StartCoroutine(FadeRoutine(fadeMode, delaySeconds, durationSeconds));
    }

    IEnumerator FadeRoutine(FadeMode fadeMode, float delaySeconds, float durationSeconds)
    {
        float from = fadeMode == FadeMode.FadeIn ? 0f : 1f;
        float to = fadeMode == FadeMode.FadeIn ? 1f : 0f;

        if (useStartAlpha) group.alpha = from;
        else from = group.alpha; // 現在値から続けてフェード

        // フェード中は操作を受け付けない
        group.blocksRaycasts = true;
        group.interactable = false;

        if (delaySeconds > 0f)
        {
            if (ignoreTimeScale) yield return new WaitForSecondsRealtime(delaySeconds);
            else yield return new WaitForSeconds(delaySeconds);
        }

        float t = 0f;
        while (t < durationSeconds)
        {
            t += ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
            float k = curve.Evaluate(Mathf.Clamp01(t / durationSeconds));
            group.alpha = Mathf.LerpUnclamped(from, to, k);
            yield return null;
        }

        group.alpha = to;

        bool visible = fadeMode == FadeMode.FadeIn;
        group.blocksRaycasts = visible;  // 透明になったらクリックを通す
        group.interactable = visible;

        running = null;
        onFadeComplete?.Invoke();

        if (!visible && deactivateOnFadeOut) gameObject.SetActive(false);
    }
}