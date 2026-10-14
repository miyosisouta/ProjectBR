using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BomberRally.EditorTools
{
    /// <summary>
    /// 仮モデル（カプセル・球）でラリーの確認用シーンを組み立てる。
    /// メニュー: BomberRally > プロトタイプシーンを作成
    /// </summary>
    public static class RallyPrototypeSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/RallyPrototype.unity";

        private static readonly Color[] PlayerColors =
        {
            new Color(1f, 0.35f, 0.6f),
            new Color(0.3f, 0.6f, 1f),
            new Color(1f, 0.85f, 0.2f),
            new Color(0.35f, 0.85f, 0.4f),
        };

        [MenuItem("BomberRally/プロトタイプシーンを作成")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (System.IO.File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("確認", $"{ScenePath} を作り直しますか？", "作り直す", "やめる"))
            {
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var cam = Camera.main;
            cam.transform.SetPositionAndRotation(new Vector3(0f, 10f, -11f), Quaternion.Euler(42f, 0f, 0f));

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ground.name = "Stage";
            ground.transform.localScale = new Vector3(12f, 0.1f, 12f);
            ground.transform.position = new Vector3(0f, -0.1f, 0f);
            SetColor(ground, new Color(0.55f, 0.45f, 0.35f));

            var formation = new GameObject("DiamondFormation").AddComponent<DiamondFormation>();

            var characters = new RallyCharacter[4];
            for (int i = 0; i < characters.Length; i++)
            {
                characters[i] = CreateCharacter(i);
            }

            var bomb = CreateBomb();

            var manager = new GameObject("RallyManager").AddComponent<RallyManager>();
            var so = new SerializedObject(manager);
            so.FindProperty("formation").objectReferenceValue = formation;
            so.FindProperty("bomb").objectReferenceValue = bomb;
            var charactersProp = so.FindProperty("characters");
            charactersProp.arraySize = characters.Length;
            for (int i = 0; i < characters.Length; i++)
            {
                charactersProp.GetArrayElementAtIndex(i).objectReferenceValue = characters[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            formation.Arrange(characters);

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"{ScenePath} を作成しました。再生して Space で打ち返し（P1 が手前）。");
        }

        private static RallyCharacter CreateCharacter(int index)
        {
            var root = new GameObject($"P{index + 1}");

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);
            SetColor(body, PlayerColors[index]);

            // 向きが分かるように正面に目印を付ける
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Front";
            nose.transform.SetParent(root.transform, false);
            nose.transform.localPosition = new Vector3(0f, 1.4f, 0.5f);
            nose.transform.localScale = new Vector3(0.3f, 0.2f, 0.3f);
            SetColor(nose, Color.white);

            var character = root.AddComponent<RallyCharacter>();
            var so = new SerializedObject(character);
            so.FindProperty("displayName").stringValue = $"P{index + 1}";
            so.ApplyModifiedPropertiesWithoutUndo();

            if (index == 0)
            {
                var human = root.AddComponent<HumanHitController>();
                var hso = new SerializedObject(human);
                hso.FindProperty("keyboardKey").intValue = (int)Key.Space;
                hso.FindProperty("gamepadIndex").intValue = 0;
                hso.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                root.AddComponent<CpuHitController>();
            }
            return character;
        }

        private static Bomb CreateBomb()
        {
            var bomb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bomb.name = "Bomb";
            bomb.transform.localScale = Vector3.one * 0.6f;
            Object.DestroyImmediate(bomb.GetComponent<Collider>());
            SetColor(bomb, new Color(0.1f, 0.1f, 0.1f));
            return bomb.AddComponent<Bomb>();
        }

        private static void SetColor(GameObject go, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return;
            var mat = new Material(shader) { color = color };
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
