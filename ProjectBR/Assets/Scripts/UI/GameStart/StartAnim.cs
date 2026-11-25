using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 「START」文字のアニメーション専用スクリプト。
/// STARTのテキスト(UI)オブジェクトにアタッチしてください。CanvasGroupは自動で追加されます。
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class StartAnimation : MonoBehaviour
{
    [Header("拡縮アニメーション")]
    [Tooltip("ONにすると、表示するときに拡縮しながら現れる。OFFだとその場でパッと表示される")]
    [SerializeField] private bool useScaleAnimation = true;

    [Tooltip("表示開始時の大きさ(元の大きさに対する倍率。1 = 元のサイズ)")]
    [SerializeField, Min(0f)] private float scaleFrom = 1.5f;

    [Tooltip("表示終了時の大きさ(元の大きさに対する倍率。1 = 元のサイズ)")]
    [SerializeField, Min(0f)] private float scaleTo = 1.0f;

    [Tooltip("scaleFrom から scaleTo まで変化する時間(秒)")]
    [SerializeField, Min(0f)] private float scaleDuration = 0.2f;

    [Header("消えるアニメーション")]
    [Tooltip("キーを押してから完全に消えるまでの時間(秒)")]
    [SerializeField, Min(0f)] private float hideDuration = 0.2f;

    private CanvasGroup canvasGroup;
    private Vector3 baseScale;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        baseScale = transform.localScale;
        canvasGroup.alpha = 0f; // 最初は非表示
    }

    /// <summary>STARTを表示する</summary>
    public IEnumerator ShowRoutine()
    {
        if (!useScaleAnimation || scaleDuration <= 0f)
        {
            // 拡縮なし:その場で表示
            transform.localScale = baseScale;
            canvasGroup.alpha = 1f;
            yield break;
        }

        float t = 0f;
        canvasGroup.alpha = 0f;
        transform.localScale = baseScale * scaleFrom;

        while (t < scaleDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / scaleDuration));
            canvasGroup.alpha = p;
            transform.localScale = baseScale * Mathf.Lerp(scaleFrom, scaleTo, p);
            yield return null;
        }

        canvasGroup.alpha = 1f;
        transform.localScale = baseScale * scaleTo;
    }

    /// <summary>STARTを消す(フェードアウト)</summary>
    public IEnumerator HideRoutine()
    {
        float startAlpha = canvasGroup.alpha;
        float t = 0f;

        while (t < hideDuration)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, Mathf.Clamp01(t / hideDuration));
            yield return null;
        }

        canvasGroup.alpha = 0f;
    }

    /// <summary>STARTを即座に非表示にする</summary>
    public void HideImmediately()
    {
        StopAllCoroutines();
        canvasGroup.alpha = 0f;
        transform.localScale = baseScale;
    }
}

#if UNITY_EDITOR
/// <summary>チェックがOFFのときは、拡縮の数値をインスペクターに表示しない</summary>
[CustomEditor(typeof(StartAnimation))]
public class StartAnimationEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty useScale = serializedObject.FindProperty("useScaleAnimation");
        EditorGUILayout.PropertyField(useScale);

        if (useScale.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("scaleFrom"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("scaleTo"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("scaleDuration"));
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("hideDuration"));

        serializedObject.ApplyModifiedProperties();
    }
}
#endif