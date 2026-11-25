using System.Collections;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 「READY → START」の一連の流れを再生するスクリプト。
/// 空のGameObjectなどにアタッチして、インスペクターで各項目を設定してください。
/// ※新しいInput System / 従来のInput Manager のどちらの設定でも動作します。
/// </summary>
public class ReadyStartController : MonoBehaviour
{
    [Header("アニメーション参照")]
    [SerializeField] private ReadyAnimation readyAnimation;
    [SerializeField] private StartAnimation startAnimation;

    [Header("READY の設定")]
    [Tooltip("再生を開始してから、READYを表示するまでの時間(秒)")]
    [SerializeField, Min(0f)] private float readyDelay = 1.0f;

    [Tooltip("ONにすると、READYがフェードアウトしてからSTARTを表示する")]
    [SerializeField] private bool fadeReadyOut = true;

    [Tooltip("READYのフェードアウトにかける時間(秒)")]
    [SerializeField, Min(0f)] private float readyFadeDuration = 0.5f;

    [Header("START の設定")]
    [Tooltip("READYを表示してから、STARTを表示するまでの時間(秒)")]
    [SerializeField, Min(0f)] private float timeUntilStart = 2.0f;

    [Tooltip("STARTが表示された後、このキーを押すとSTARTの文字が消える")]
#if ENABLE_INPUT_SYSTEM
    [SerializeField] private Key hideStartKey = Key.Space;
#else
    [SerializeField] private KeyCode hideStartKey = KeyCode.Space;
#endif

    [Header("その他")]
    [Tooltip("ONにすると、ゲーム開始時に自動で再生する")]
    [SerializeField] private bool playOnStart = true;

    [Tooltip("STARTが消えたあとに呼ばれるイベント(ゲーム本編の開始処理など)")]
    public UnityEvent onFinished;

    private Coroutine playing;

    private void Start()
    {
        if (playOnStart)
        {
            Play();
        }
    }

    /// <summary>READY → START の再生を開始する(他のスクリプトやボタンからも呼べます)</summary>
    public void Play()
    {
        if (playing != null)
        {
            StopCoroutine(playing);
        }

        readyAnimation.HideImmediately();
        startAnimation.HideImmediately();
        playing = StartCoroutine(PlayRoutine());
    }

    /// <summary>指定したキーがこのフレームで押されたか</summary>
    private bool IsHideKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || hideStartKey == Key.None)
        {
            return false;
        }
        return keyboard[hideStartKey].wasPressedThisFrame;
#else
        return Input.GetKeyDown(hideStartKey);
#endif
    }

    private IEnumerator PlayRoutine()
    {
        // 1. READYを表示するまで待つ
        yield return new WaitForSeconds(readyDelay);

        // 2. READYを表示
        yield return StartCoroutine(readyAnimation.ShowRoutine());

        // 3. STARTを表示するまでの時間を待つ
        float wait = timeUntilStart;

        if (fadeReadyOut)
        {
            // フェードの時間はSTART表示までの時間に収まるようにする
            float fade = Mathf.Min(readyFadeDuration, wait);
            yield return new WaitForSeconds(wait - fade);
            yield return StartCoroutine(readyAnimation.FadeOutRoutine(fade));
        }
        else
        {
            yield return new WaitForSeconds(wait);
            readyAnimation.HideImmediately();
        }

        // 4. STARTを表示
        yield return StartCoroutine(startAnimation.ShowRoutine());

        // 5. 指定したキーが押されるまで待つ
        while (!IsHideKeyPressed())
        {
            yield return null;
        }

        // 6. STARTを消す
        yield return StartCoroutine(startAnimation.HideRoutine());

        playing = null;
        onFinished?.Invoke();
    }
}