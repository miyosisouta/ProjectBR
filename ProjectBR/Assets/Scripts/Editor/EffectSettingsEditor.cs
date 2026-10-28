using UnityEditor;
using UnityEngine;

namespace BomberRally.EditorTools
{
    /// <summary>
    /// エフェクト設定アセットを用意する。無ければ既定値で Assets/Resources/Effects に作る。
    /// メニュー: BomberRally > エフェクト設定 > 打ち返し / 爆発 / 爆弾
    /// </summary>
    [InitializeOnLoad]
    public static class EffectSettingsAssets
    {
        private const string Folder = "Assets/Resources/Effects";
        public const string HitPath = Folder + "/HitEffectSettings.asset";
        public const string ExplosionPath = Folder + "/ExplosionEffectSettings.asset";
        public const string BombPath = Folder + "/BombEffectSettings.asset";

        static EffectSettingsAssets()
        {
            // 読み込み直後は AssetDatabase が使えないことがあるので一拍置く
            EditorApplication.delayCall += () =>
            {
                GetOrCreate<HitEffectSettings>(HitPath);
                GetOrCreate<ExplosionEffectSettings>(ExplosionPath);
                GetOrCreate<BombEffectSettings>(BombPath);
            };
        }

        public static T GetOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Resources", "Effects");
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Debug.Log($"エフェクト設定を作成しました: {path}");
            return asset;
        }

        [MenuItem("BomberRally/エフェクト設定/打ち返し")]
        private static void SelectHit() => Select(GetOrCreate<HitEffectSettings>(HitPath));

        [MenuItem("BomberRally/エフェクト設定/爆発")]
        private static void SelectExplosion() => Select(GetOrCreate<ExplosionEffectSettings>(ExplosionPath));

        [MenuItem("BomberRally/エフェクト設定/爆弾")]
        private static void SelectBomb() => Select(GetOrCreate<BombEffectSettings>(BombPath));

        private static void Select(Object asset)
        {
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }

    /// <summary>エフェクト設定の確認用。再生中はボタンでその場に出せる。</summary>
    internal static class EffectPreview
    {
        private static readonly Vector3 Center = new Vector3(0f, 1f, 0f);

        public static void DrawHit()
        {
            if (!BeginPreview()) return;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("通常を再生")) PlayHit(false);
                if (GUILayout.Button("スマッシュを再生")) PlayHit(true);
            }
        }

        public static void DrawExplosion()
        {
            if (!BeginPreview()) return;
            if (GUILayout.Button("爆発を再生", GUILayout.Height(28f)))
            {
                var fx = FindOrCreate<ExplosionEffect>();
                fx.Play(Center, 0f);
            }
        }

        private static bool BeginPreview()
        {
            EditorGUILayout.Space();
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("再生中はここからステージ中央にエフェクトを出せます。\n設定はアセットなので、再生中に変えた値も止めたあとに残ります。", MessageType.Info);
                return false;
            }
            EditorGUILayout.LabelField("プレビュー（ステージ中央に出す）", EditorStyles.boldLabel);
            return true;
        }

        private static void PlayHit(bool smash)
        {
            var fx = FindOrCreate<HitEffect>();
            fx.Play(Center, Center + Vector3.forward * 3f, smash);
        }

        // RallyManager が付けたものを使う。シーンに無ければプレビュー用に作る
        private static T FindOrCreate<T>() where T : Component
        {
            var fx = Object.FindAnyObjectByType<T>();
            return fx != null ? fx : new GameObject($"{typeof(T).Name}Preview").AddComponent<T>();
        }
    }

    [CustomEditor(typeof(HitEffectSettings))]
    public class HitEffectSettingsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EffectPreview.DrawHit();
        }
    }

    [CustomEditor(typeof(ExplosionEffectSettings))]
    public class ExplosionEffectSettingsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EffectPreview.DrawExplosion();
        }
    }

    /// <summary>
    /// RallyManager のインスペクターの下に、使われるエフェクト設定をそのまま並べて編集できるようにする。
    /// エフェクトのコンポーネントは再生時に付くので、編集中でもここから触れるようにしておく。
    /// </summary>
    [CustomEditor(typeof(RallyManager))]
    public class RallyManagerEditor : Editor
    {
        private Editor hitEditor;
        private Editor explosionEditor;
        private Editor bombEditor;
        private static bool hitOpen;
        private static bool explosionOpen;
        private static bool bombOpen;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField("エフェクト設定", EditorStyles.boldLabel);

            var manager = (RallyManager)target;
            var hit = manager.GetComponent<HitEffect>();
            var explosion = manager.GetComponent<ExplosionEffect>();
            // 再生中はコンポーネントが実際に使っている設定、編集中は既定のアセットを出す
            var hitSettings = hit != null ? hit.Settings : EffectSettingsAssets.GetOrCreate<HitEffectSettings>(EffectSettingsAssets.HitPath);
            var explosionSettings = explosion != null ? explosion.Settings : EffectSettingsAssets.GetOrCreate<ExplosionEffectSettings>(EffectSettingsAssets.ExplosionPath);

            hitOpen = DrawSettings("打ち返し", hitSettings, ref hitEditor, hitOpen);
            explosionOpen = DrawSettings("爆発", explosionSettings, ref explosionEditor, explosionOpen);

            var bomb = serializedObject.FindProperty("bomb").objectReferenceValue as Bomb;
            // 編集中に bomb.Settings を呼ぶとシーンの参照を書き換えてしまうので、再生中だけ使う
            var bombSettings = Application.isPlaying && bomb != null ? bomb.Settings :EffectSettingsAssets.GetOrCreate<BombEffectSettings>(EffectSettingsAssets.BombPath);
            bombOpen = DrawSettings("爆弾（導火線の火花・スマッシュの軌跡）", bombSettings, ref bombEditor, bombOpen);
        }

        private static bool DrawSettings(string label, Object settings, ref Editor editor, bool open)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                open = EditorGUILayout.Foldout(open, label, true);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField(settings, settings.GetType(), false);
                }
            }
            if (!open) return false;

            CreateCachedEditor(settings, null, ref editor);
            using (new EditorGUI.IndentLevelScope())
            {
                editor.OnInspectorGUI();
            }
            EditorGUILayout.Space();
            return true;
        }

        private void OnDisable()
        {
            if (hitEditor != null) DestroyImmediate(hitEditor);
            if (explosionEditor != null) DestroyImmediate(explosionEditor);
            if (bombEditor != null) DestroyImmediate(bombEditor);
        }
    }
}
