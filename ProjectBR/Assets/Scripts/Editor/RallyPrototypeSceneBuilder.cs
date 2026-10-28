using System.Linq;
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
        private const string HeroPrefab = "Assets/Prefabs/Character/Hero/FemaleCharacterPBR.prefab";
        private const string SwordPrefab = "Assets/Prefabs/Character/Hero/OHS06PBR.prefab";
        private const string ShieldPrefab = "Assets/Prefabs/Character/Hero/Shield05PBR.prefab";
        private const string SlimePrefab = "Assets/Prefabs/Character/Slime/SlimePBR.prefab";

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

            var heroController = RallyAnimatorBuilder.Build(RallyAnimatorBuilder.Hero);
            var slimeController = RallyAnimatorBuilder.Build(RallyAnimatorBuilder.Slime);

            // P1（人間）は Hero、CPU は Slime
            var characters = new RallyCharacter[4];
            for (int i = 0; i < characters.Length; i++)
            {
                characters[i] = i == 0
                    ? CreateCharacter(i, HeroPrefab, heroController, true)
                    : CreateCharacter(i, SlimePrefab, slimeController, false);
            }

            var bomb = CreateBomb();

            var manager = new GameObject("RallyManager").AddComponent<RallyManager>();
            var hitEffect = manager.gameObject.AddComponent<HitEffect>();
            var so = new SerializedObject(manager);
            so.FindProperty("formation").objectReferenceValue = formation;
            so.FindProperty("bomb").objectReferenceValue = bomb;
            so.FindProperty("hitEffect").objectReferenceValue = hitEffect;
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

        private static RallyCharacter CreateCharacter(int index, string prefabPath, RuntimeAnimatorController controller, bool isHero)
        {
            var root = new GameObject($"P{index + 1}");

            // 足元にプレイヤーカラーの輪を置いて見分ける
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "PlayerRing";
            Object.DestroyImmediate(ring.GetComponent<Collider>());
            ring.transform.SetParent(root.transform, false);
            ring.transform.localScale = new Vector3(1.4f, 0.02f, 1.4f);
            ring.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            SetColor(ring, PlayerColors[index]);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab != null && controller != null)
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                model.name = "Model";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;

                var animator = model.GetComponentInChildren<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;

                if (isHero)
                {
                    AttachToBone(model.transform, "weapon_r", SwordPrefab);
                    AttachToBone(model.transform, "weapon_l", ShieldPrefab);
                }

                var anim = root.AddComponent<CharacterAnimator>();
                var aso = new SerializedObject(anim);
                aso.FindProperty("animator").objectReferenceValue = animator;
                aso.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning($"{prefabPath} またはアニメーションが見つからないため仮モデルで作ります");
                CreatePlaceholderBody(root, index);
            }

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

        private static void AttachToBone(Transform model, string boneName, string prefabPath)
        {
            var bone = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == boneName);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (bone == null || prefab == null) return;

            var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab, bone);
            item.transform.localPosition = Vector3.zero;
            item.transform.localRotation = Quaternion.identity;
        }

        private static void CreatePlaceholderBody(GameObject root, int index)
        {
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
