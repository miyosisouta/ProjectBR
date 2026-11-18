using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BomberRally.EditorTools
{
    /// <summary>
    /// 仮モデル（箱・円柱・球）で公園とガソリンスタンドのステージを組み立て、プレハブにして難易度設定に割り当てる。
    ///   公園          : Easy（昼） / Hard（夕方）
    ///   ガソリンスタンド: Normal（昼）/ Expert（夕方）
    /// 開いているシーンに RallyManager があれば StageLoader も置く。
    /// メニュー: BomberRally > ステージを作成
    /// 見た目は後から本番のモデルに差し替える前提。配置と演出の動きを確かめるためのもの。
    /// </summary>
    public static class StageBuilder
    {
        private const string PrefabFolder = "Assets/Prefabs/Stages";
        private const string MaterialFolder = "Assets/Materials/Stages";
        public const string ParkPrefabPath = PrefabFolder + "/ParkStage.prefab";
        public const string GasStationPrefabPath = PrefabFolder + "/GasStationStage.prefab";

        // ひし形の半径（DiamondFormation の既定値）。配置はこれを基準にする
        private const float Radius = 4f;

        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        [MenuItem("BomberRally/ステージを作成")]
        public static void BuildAll()
        {
            Materials.Clear();
            EnsureFolder(PrefabFolder);
            EnsureFolder(MaterialFolder);

            var park = SavePrefab(BuildPark(), ParkPrefabPath);
            var gas = SavePrefab(BuildGasStation(), GasStationPrefabPath);

            Assign(DifficultyLevel.Easy, park, StageTimeOfDay.Day);
            Assign(DifficultyLevel.Hard, park, StageTimeOfDay.Evening);
            Assign(DifficultyLevel.Normal, gas, StageTimeOfDay.Day);
            Assign(DifficultyLevel.Expert, gas, StageTimeOfDay.Evening);
            AssetDatabase.SaveAssets();

            var manager = Object.FindFirstObjectByType<RallyManager>();
            if (manager != null && AddLoaderToScene(manager) != null)
            {
                EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            }
            Debug.Log("ステージを作成しました（Easy/Hard: 公園、Normal/Expert: ガソリンスタンド）。シーンを保存してから再生してください。");
        }

        /// <summary>シーンに StageLoader を置く（すでにあればそれを返す）。</summary>
        public static StageLoader AddLoaderToScene(RallyManager manager)
        {
            var existing = Object.FindFirstObjectByType<StageLoader>();
            if (existing != null) return existing;

            var loader = new GameObject("StageLoader").AddComponent<StageLoader>();
            var so = new SerializedObject(loader);
            so.FindProperty("manager").objectReferenceValue = manager;
            so.FindProperty("placeholderGround").objectReferenceValue = GameObject.Find("Stage");
            var formation = Object.FindFirstObjectByType<DiamondFormation>();
            if (formation != null) so.FindProperty("anchor").objectReferenceValue = formation.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
            return loader;
        }

        // ---------------------------------------------------------------- 公園

        private static GameObject BuildPark()
        {
            var root = new GameObject("ParkStage");
            var env = root.AddComponent<StageEnvironment>();
            var evening = new List<GameObject>();

            Box(root, "Ground", new Vector3(0f, -0.1f, 0f), new Vector3(60f, 0.2f, 60f), Mat("ParkGrass", new Color(0.45f, 0.7f, 0.3f)));
            // 中央は土の広場にして爆弾と影を見やすくする
            Cyl(root, "PlayArea", new Vector3(0f, 0.005f, 0f), new Vector3(Radius * 2.8f, 0.01f, Radius * 2.8f), Mat("ParkDirt", new Color(0.78f, 0.68f, 0.5f)));
            Box(root, "Sidewalk", new Vector3(0f, 0.01f, -9f), new Vector3(60f, 0.02f, 3f), Mat("ParkSidewalk", new Color(0.75f, 0.74f, 0.7f)));

            BuildSlide(root, new Vector3(-7f, 0f, 8.5f));
            BuildSwing(root, new Vector3(7f, 0f, 9.5f));
            BuildSandbox(root, new Vector3(-8.5f, 0f, -3.5f));
            BuildJungleGym(root, new Vector3(8.5f, 0f, -3.5f));
            BuildBench(root, new Vector3(-2.5f, 0f, 12f));
            BuildBench(root, new Vector3(2.5f, 0f, 12f));

            // 奥と左右を木で囲む（手前のカメラ側は空ける）
            for (int i = 0; i < 18; i++)
            {
                float angle = Mathf.Lerp(-100f, 100f, i / 17f) * Mathf.Deg2Rad;
                float r = 16f + (i % 3) * 1.5f;
                BuildTree(root, new Vector3(Mathf.Sin(angle) * r, 0f, Mathf.Cos(angle) * r + 2f), 0.9f + (i % 4) * 0.12f);
            }

            evening.Add(BuildStreetLamp(root, new Vector3(-11f, 0f, 3f)));
            evening.Add(BuildStreetLamp(root, new Vector3(11f, 0f, 3f)));
            evening.Add(BuildStreetLamp(root, new Vector3(0f, 0f, 13.5f)));

            // 子ども。全員、遊具よりカメラ側か、ひらけた場所に置いて隠れないようにする
            var shirts = new[]
            {
                new Color(0.95f, 0.45f, 0.45f), new Color(0.45f, 0.65f, 0.95f), new Color(0.98f, 0.8f, 0.3f),
                new Color(0.5f, 0.8f, 0.5f), new Color(0.8f, 0.55f, 0.9f),
            };
            int n = 0;
            Color Shirt() => shirts[n++ % shirts.Length];

            // 遊具のそば：一番怖いときは遊具にしがみつく
            Kid(root, "Kid_Slide", new Vector3(-5.8f, 0f, 6.6f), Shirt(), Spot(root, "Hide_Slide", new Vector3(-6.4f, 0f, 7.4f)));
            var swingSpot = Spot(root, "Hide_Swing", new Vector3(7f, 0f, 8.2f));
            Kid(root, "Kid_Swing_A", new Vector3(6.1f, 0f, 7.4f), Shirt(), swingSpot, -0.3f);
            Kid(root, "Kid_Swing_B", new Vector3(7.9f, 0f, 7.4f), Shirt(), swingSpot, 0.3f);
            Kid(root, "Kid_Sandbox", new Vector3(-8.3f, 0f, -3.2f), Shirt(), Spot(root, "Hide_Sandbox", new Vector3(-8.5f, 0f, -4.6f)));
            Kid(root, "Kid_JungleGym", new Vector3(7.8f, 0f, -5.4f), Shirt(), Spot(root, "Hide_JungleGym", new Vector3(8.2f, 0f, -4.9f)));

            // ひらけた場所の 2 人組：一番怖いときは抱き合う
            var rightSpot = Spot(root, "Hug_Right", new Vector3(7.2f, 0f, 0.1f));
            Kid(root, "Kid_Right_A", new Vector3(6.9f, 0f, -0.7f), Shirt(), rightSpot, -0.25f);
            Kid(root, "Kid_Right_B", new Vector3(7.5f, 0f, 0.8f), Shirt(), rightSpot, 0.25f);
            var backRightSpot = Spot(root, "Hug_BackRight", new Vector3(5.1f, 0f, 5f));
            Kid(root, "Kid_BackRight_A", new Vector3(4.6f, 0f, 5.5f), Shirt(), backRightSpot, -0.25f);
            Kid(root, "Kid_BackRight_B", new Vector3(5.7f, 0f, 4.5f), Shirt(), backRightSpot, 0.25f);
            // 左奥のひとり：その場で縮こまる
            Kid(root, "Kid_BackLeft", new Vector3(-5.2f, 0f, 5f), Shirt(), null);

            SetObjects(env, "eveningOnly", evening);
            return root;
        }

        private static void BuildSlide(GameObject root, Vector3 pos)
        {
            var g = Group(root, "Slide", pos);
            var metal = Mat("ParkMetal", new Color(0.6f, 0.62f, 0.66f));
            var red = Mat("ParkSlide", new Color(0.92f, 0.45f, 0.3f));
            Box(g, "Platform", new Vector3(0f, 2f, 1.2f), new Vector3(1.4f, 0.15f, 1.2f), metal);
            for (int i = 0; i < 4; i++)
            {
                Cyl(g, "Leg", new Vector3((i % 2 == 0 ? -0.6f : 0.6f), 1f, i < 2 ? 0.7f : 1.7f), new Vector3(0.12f, 1f, 0.12f), metal);
            }
            var slope = Box(g, "Slope", new Vector3(0f, 1f, -0.6f), new Vector3(1f, 0.1f, 3f), red);
            slope.transform.localRotation = Quaternion.Euler(-38f, 0f, 0f);
        }

        private static void BuildSwing(GameObject root, Vector3 pos)
        {
            var g = Group(root, "Swing", pos);
            var metal = Mat("ParkMetal", new Color(0.6f, 0.62f, 0.66f));
            var seat = Mat("ParkSeat", new Color(0.85f, 0.6f, 0.2f));
            Cyl(g, "TopBar", new Vector3(0f, 2.6f, 0f), new Vector3(0.12f, 2.2f, 0.12f), metal).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            foreach (float x in new[] { -2.1f, 2.1f })
            {
                foreach (float z in new[] { -0.6f, 0.6f })
                {
                    var leg = Cyl(g, "Leg", new Vector3(x, 1.3f, z * 0.5f), new Vector3(0.12f, 1.35f, 0.12f), metal);
                    leg.transform.localRotation = Quaternion.Euler(z > 0 ? -12f : 12f, 0f, 0f);
                }
            }
            foreach (float x in new[] { -0.9f, 0.9f })
            {
                Box(g, "Seat", new Vector3(x, 0.55f, 0f), new Vector3(0.7f, 0.08f, 0.35f), seat);
                Cyl(g, "Chain", new Vector3(x - 0.3f, 1.6f, 0f), new Vector3(0.03f, 1f, 0.03f), metal);
                Cyl(g, "Chain", new Vector3(x + 0.3f, 1.6f, 0f), new Vector3(0.03f, 1f, 0.03f), metal);
            }
        }

        private static void BuildSandbox(GameObject root, Vector3 pos)
        {
            var g = Group(root, "Sandbox", pos);
            var wood = Mat("ParkWood", new Color(0.6f, 0.42f, 0.25f));
            Box(g, "Sand", new Vector3(0f, 0.05f, 0f), new Vector3(3.2f, 0.1f, 2.6f), Mat("ParkSand", new Color(0.95f, 0.85f, 0.55f)));
            Box(g, "FrameN", new Vector3(0f, 0.15f, 1.35f), new Vector3(3.4f, 0.3f, 0.15f), wood);
            Box(g, "FrameS", new Vector3(0f, 0.15f, -1.35f), new Vector3(3.4f, 0.3f, 0.15f), wood);
            Box(g, "FrameE", new Vector3(1.65f, 0.15f, 0f), new Vector3(0.15f, 0.3f, 2.6f), wood);
            Box(g, "FrameW", new Vector3(-1.65f, 0.15f, 0f), new Vector3(0.15f, 0.3f, 2.6f), wood);
        }

        private static void BuildJungleGym(GameObject root, Vector3 pos)
        {
            var g = Group(root, "JungleGym", pos);
            var blue = Mat("ParkJungleGym", new Color(0.3f, 0.5f, 0.85f));
            const float size = 2.4f, height = 2f;
            for (int ix = 0; ix < 3; ix++)
            {
                for (int iz = 0; iz < 3; iz++)
                {
                    float x = -size / 2f + ix * size / 2f, z = -size / 2f + iz * size / 2f;
                    Cyl(g, "Pole", new Vector3(x, height / 2f, z), new Vector3(0.08f, height / 2f, 0.08f), blue);
                }
            }
            for (int level = 1; level <= 2; level++)
            {
                float y = height * level / 2f;
                for (int i = 0; i < 3; i++)
                {
                    float o = -size / 2f + i * size / 2f;
                    Box(g, "BarX", new Vector3(0f, y, o), new Vector3(size, 0.06f, 0.06f), blue);
                    Box(g, "BarZ", new Vector3(o, y, 0f), new Vector3(0.06f, 0.06f, size), blue);
                }
            }
        }

        private static void BuildBench(GameObject root, Vector3 pos)
        {
            var g = Group(root, "Bench", pos);
            var wood = Mat("ParkWood", new Color(0.6f, 0.42f, 0.25f));
            Box(g, "Seat", new Vector3(0f, 0.45f, 0f), new Vector3(1.8f, 0.08f, 0.5f), wood);
            Box(g, "Back", new Vector3(0f, 0.8f, 0.22f), new Vector3(1.8f, 0.5f, 0.06f), wood);
            Box(g, "LegL", new Vector3(-0.8f, 0.22f, 0f), new Vector3(0.08f, 0.45f, 0.45f), wood);
            Box(g, "LegR", new Vector3(0.8f, 0.22f, 0f), new Vector3(0.08f, 0.45f, 0.45f), wood);
        }

        private static void BuildTree(GameObject root, Vector3 pos, float scale)
        {
            var g = Group(root, "Tree", pos);
            g.transform.localScale = Vector3.one * scale;
            Cyl(g, "Trunk", new Vector3(0f, 1.2f, 0f), new Vector3(0.4f, 1.2f, 0.4f), Mat("ParkTrunk", new Color(0.45f, 0.32f, 0.2f)));
            Sphere(g, "Crown", new Vector3(0f, 3.4f, 0f), new Vector3(2.8f, 2.6f, 2.8f), Mat("ParkLeaves", new Color(0.3f, 0.55f, 0.25f)));
        }

        /// <summary>街灯を作り、夕方だけ点く明かり（光る電球と点光源）を返す。</summary>
        private static GameObject BuildStreetLamp(GameObject root, Vector3 pos)
        {
            var g = Group(root, "StreetLamp", pos);
            Cyl(g, "Pole", new Vector3(0f, 1.8f, 0f), new Vector3(0.12f, 1.8f, 0.12f), Mat("LampPole", new Color(0.25f, 0.27f, 0.3f)));
            Sphere(g, "Bulb", new Vector3(0f, 3.7f, 0f), Vector3.one * 0.45f, Mat("LampBulbOff", new Color(0.85f, 0.85f, 0.8f)));
            var glow = Group(g, "Glow", Vector3.zero);
            Sphere(glow, "LitBulb", new Vector3(0f, 3.7f, 0f), Vector3.one * 0.5f, Mat("LampBulbOn", new Color(1f, 0.85f, 0.55f), emission: 3f));
            PointLight(glow, new Vector3(0f, 3.4f, 0f), new Color(1f, 0.8f, 0.5f), 4f, 9f);
            return glow;
        }

        private static void Kid(GameObject root, string name, Vector3 pos, Color shirt, Transform hideSpot, float hugOffset = 0f)
        {
            var kid = Group(root, name, pos);
            FaceCenter(kid.transform);
            var visual = Group(kid, "Visual", Vector3.zero);
            var skinColor = new Color(1f, 0.84f, 0.7f);
            Capsule(visual, "Body", new Vector3(0f, 0.42f, 0f), new Vector3(0.42f, 0.38f, 0.32f), Mat("Shirt_" + ColorUtility.ToHtmlStringRGB(shirt), shirt));
            var head = Sphere(visual, "Head", new Vector3(0f, 0.98f, 0f), Vector3.one * 0.38f, Mat("KidSkin", skinColor));
            // 怖がっていると分かるよう、目は大きめ
            var eye = Mat("KidEye", new Color(0.1f, 0.1f, 0.12f));
            Sphere(visual, "EyeL", new Vector3(-0.07f, 1.02f, 0.17f), Vector3.one * 0.07f, eye);
            Sphere(visual, "EyeR", new Vector3(0.07f, 1.02f, 0.17f), Vector3.one * 0.07f, eye);
            var alert = BuildAlert(visual, 1.5f);

            // 2 人組は同じ場所に向かうので、少し左右にずらして重ならないようにする
            if (hideSpot != null && Mathf.Abs(hugOffset) > 0f)
            {
                var own = Spot(hideSpot.gameObject, name + "_Spot", Vector3.right * hugOffset);
                hideSpot = own;
            }

            var scared = kid.AddComponent<ScaredBystander>();
            var so = new SerializedObject(scared);
            so.FindProperty("visual").objectReferenceValue = visual.transform;
            var skins = so.FindProperty("skinRenderers");
            skins.arraySize = 1;
            skins.GetArrayElementAtIndex(0).objectReferenceValue = head.GetComponent<Renderer>();
            so.FindProperty("skinColor").colorValue = skinColor;
            so.FindProperty("alert").objectReferenceValue = alert;
            so.FindProperty("hideSpot").objectReferenceValue = hideSpot;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>頭の上の「!!」。</summary>
        private static GameObject BuildAlert(GameObject parent, float height)
        {
            var alert = Group(parent, "Alert", new Vector3(0f, height, 0f));
            var red = Mat("AlertRed", new Color(1f, 0.15f, 0.15f), emission: 1.5f);
            foreach (float x in new[] { -0.1f, 0.1f })
            {
                Box(alert, "Bar", new Vector3(x, 0.12f, 0f), new Vector3(0.07f, 0.26f, 0.07f), red);
                Box(alert, "Dot", new Vector3(x, -0.1f, 0f), new Vector3(0.07f, 0.07f, 0.07f), red);
            }
            alert.SetActive(false);
            return alert;
        }

        // ---------------------------------------------------------------- ガソリンスタンド

        private static GameObject BuildGasStation()
        {
            var root = new GameObject("GasStationStage");
            var env = root.AddComponent<StageEnvironment>();
            var reaction = root.AddComponent<GasStationReaction>();
            var evening = new List<GameObject>();

            Box(root, "Ground", new Vector3(0f, -0.1f, 0f), new Vector3(60f, 0.2f, 60f), Mat("GasConcrete", new Color(0.66f, 0.65f, 0.62f)));
            // 中央は少し明るい舗装にして爆弾と影を見やすくする
            Box(root, "PlayArea", new Vector3(0f, 0.005f, 0f), new Vector3(Radius * 2.6f, 0.01f, Radius * 2.6f), Mat("GasPaving", new Color(0.78f, 0.77f, 0.74f)));
            var paint = Mat("GasPaint", new Color(0.95f, 0.85f, 0.2f));
            foreach (float x in new[] { -Radius * 1.3f, Radius * 1.3f })
            {
                Box(root, "Line", new Vector3(x, 0.012f, 0f), new Vector3(0.15f, 0.01f, Radius * 2.6f), paint);
            }

            // 店舗（奥）
            var store = Group(root, "Store", new Vector3(0f, 0f, 12.5f));
            Box(store, "Building", new Vector3(0f, 1.8f, 0f), new Vector3(12f, 3.6f, 5f), Mat("GasStoreWall", new Color(0.92f, 0.92f, 0.9f)));
            Box(store, "Roof", new Vector3(0f, 3.75f, 0f), new Vector3(12.4f, 0.3f, 5.4f), Mat("GasRed", new Color(0.85f, 0.15f, 0.12f)));
            Box(store, "Window", new Vector3(-1.5f, 1.4f, -2.52f), new Vector3(7f, 2f, 0.05f), Mat("GasGlass", new Color(0.55f, 0.75f, 0.9f)));
            Box(store, "Door", new Vector3(4f, 1.1f, -2.52f), new Vector3(1.6f, 2.2f, 0.05f), Mat("GasGlass", new Color(0.55f, 0.75f, 0.9f)));
            var storeGlow = Group(store, "WindowGlow", Vector3.zero);
            Box(storeGlow, "LitWindow", new Vector3(-1.5f, 1.4f, -2.54f), new Vector3(7f, 2f, 0.05f), Mat("GasWindowLit", new Color(1f, 0.92f, 0.7f), emission: 2f));
            PointLight(storeGlow, new Vector3(0f, 2f, -3.5f), new Color(1f, 0.9f, 0.7f), 3f, 8f);
            evening.Add(storeGlow);

            // 価格の看板（左奥）
            var sign = Group(root, "PriceSign", new Vector3(-10f, 0f, 9f));
            Cyl(sign, "Pole", new Vector3(0f, 2.5f, 0f), new Vector3(0.25f, 2.5f, 0.25f), Mat("GasPole", new Color(0.3f, 0.3f, 0.32f)));
            Box(sign, "Board", new Vector3(0f, 5.6f, 0f), new Vector3(2.6f, 2.2f, 0.4f), Mat("GasRed", new Color(0.85f, 0.15f, 0.12f)));
            var signGlow = Group(sign, "Glow", Vector3.zero);
            Box(signGlow, "LitPanel", new Vector3(0f, 5.4f, -0.22f), new Vector3(2.2f, 1.2f, 0.05f), Mat("GasSignLit", new Color(1f, 0.95f, 0.6f), emission: 3f));
            evening.Add(signGlow);
            Box(root, "FireNotice", new Vector3(-3.5f, 1.2f, 9.95f), new Vector3(1.6f, 0.7f, 0.05f), Mat("GasNotice", new Color(1f, 0.9f, 0.2f)));

            // 給油機：プレイヤーの外側に 1 台ずつ。手前（カメラ側）は低くし、屋根と車は付けない
            var pumps = new List<FuelPump>();
            var cars = new List<ParkedCar>();
            float pumpDistance = Radius + 2.6f;
            pumps.Add(BuildPump(root, "Pump_Front", new Vector3(0f, 0f, -(Radius + 2.2f)), 0f, low: true, roof: false, evening));
            pumps.Add(BuildPump(root, "Pump_Right", new Vector3(pumpDistance, 0f, 0f), 90f, low: false, roof: true, evening));
            pumps.Add(BuildPump(root, "Pump_Back", new Vector3(0f, 0f, pumpDistance), 0f, low: false, roof: true, evening));
            pumps.Add(BuildPump(root, "Pump_Left", new Vector3(-pumpDistance, 0f, 0f), 90f, low: false, roof: true, evening));

            // 給油機の横の車（奥：軽トラ、右：ワゴン、左：軽トラ）
            cars.Add(BuildCar(root, "KeiTruck_Back", new Vector3(-3.6f, 0f, pumpDistance), 90f, CarKind.KeiTruck, new Color(0.95f, 0.95f, 0.95f)));
            cars.Add(BuildCar(root, "Wagon_Right", new Vector3(pumpDistance, 0f, 3.9f), 0f, CarKind.Wagon, new Color(0.35f, 0.55f, 0.85f)));
            // 左の洗車機の中で洗車中の車（洗車機は BuildGasStationSurroundings で (-13, 0, 4) に置く）
            cars.Add(BuildCar(root, "Wagon_CarWash", new Vector3(-13f, 0f, 3.7f), 0f, CarKind.Wagon, new Color(0.78f, 0.79f, 0.8f)));
            cars.Add(BuildCar(root, "KeiTruck_Left", new Vector3(-pumpDistance, 0f, 3.4f), 180f, CarKind.KeiTruck, new Color(0.3f, 0.6f, 0.4f)));

            // 手前の道路と、行き来する車。カメラの下端にかかるので、車の上半分が画面の手前を横切る
            var road = Group(root, "Road", new Vector3(0f, 0f, -8.8f));
            Box(road, "Asphalt", new Vector3(0f, 0.01f, 0f), new Vector3(60f, 0.02f, 3.8f), Mat("GasAsphalt", new Color(0.25f, 0.25f, 0.27f)));
            var white = Mat("GasRoadLine", new Color(0.95f, 0.95f, 0.92f));
            for (int i = -10; i <= 10; i++) Box(road, "CenterLine", new Vector3(i * 3f, 0.025f, 0f), new Vector3(1.6f, 0.01f, 0.15f), white);
            var traffic = road.AddComponent<RoadTraffic>();
            var movingCars = new List<Transform>
            {
                BuildCar(road, "Traffic_A", new Vector3(-14f, 0f, 0.95f), 90f, CarKind.Wagon, new Color(0.9f, 0.35f, 0.35f)).transform,
                BuildCar(road, "Traffic_B", new Vector3(6f, 0f, 0.95f), 90f, CarKind.KeiTruck, new Color(0.95f, 0.8f, 0.3f)).transform,
                BuildCar(road, "Traffic_C", new Vector3(12f, 0f, -0.95f), -90f, CarKind.Wagon, new Color(0.4f, 0.4f, 0.45f)).transform,
                BuildCar(road, "Traffic_D", new Vector3(-6f, 0f, -0.95f), -90f, CarKind.Wagon, new Color(0.95f, 0.95f, 0.95f)).transform,
            };
            // 道路の車は跳ねさせないので ParkedCar は外す
            foreach (var car in movingCars) Object.DestroyImmediate(car.GetComponent<ParkedCar>());
            SetObjects(traffic, "cars", movingCars);

            // まわりの小物と背景（洗車機・自販機・植え込み・電柱・近所の建物など）
            BuildGasStationSurroundings(root, evening);

            // 店員（店の前）。普段は青ざめて見守り、爆発したら消火に走る
            var staff = BuildStaff(root, new Vector3(3.5f, 0f, 9.2f));

            var so = new SerializedObject(reaction);
            SetArray(so.FindProperty("pumps"), pumps);
            SetArray(so.FindProperty("parkedCars"), cars);
            so.FindProperty("traffic").objectReferenceValue = traffic;
            so.FindProperty("staff").objectReferenceValue = staff;
            so.ApplyModifiedPropertiesWithoutUndo();

            SetObjects(env, "eveningOnly", evening);
            return root;
        }

        /// <summary>
        /// ガソリンスタンドのまわりに、実際のスタンドにありそうな物を置く。主役はラリーなので、まわりはあくまで背景。
        /// 色は落ち着かせ、プレイ範囲（中央の半径 7m ほど）とカメラから中央への見通しには何も置かない。
        /// </summary>
        private static void BuildGasStationSurroundings(GameObject root, List<GameObject> evening)
        {
            var g = Group(root, "Surroundings", Vector3.zero);
            var gray = Mat("GasCurb", new Color(0.72f, 0.72f, 0.7f));
            var pole = Mat("GasPole", new Color(0.3f, 0.3f, 0.32f));
            var tire = Mat("CarTire", new Color(0.08f, 0.08f, 0.08f));
            var darkGlass = Mat("BuildingWindow", new Color(0.25f, 0.32f, 0.4f));
            var litWindow = Mat("BuildingWindowLit", new Color(1f, 0.85f, 0.55f), emission: 1f);

            // 道路との境の縁石（手前の給油機の前はあける）
            Box(g, "CurbL", new Vector3(-16f, 0.07f, -6.95f), new Vector3(28f, 0.14f, 0.2f), gray);
            Box(g, "CurbR", new Vector3(16f, 0.07f, -6.95f), new Vector3(28f, 0.14f, 0.2f), gray);

            // 洗車機（左）
            var wash = Group(g, "CarWash", new Vector3(-13f, 0f, 4f));
            var washWall = Mat("GasCarWash", new Color(0.92f, 0.93f, 0.95f));
            var brush = Mat("GasBrush", new Color(0.45f, 0.53f, 0.65f));
            Box(wash, "WallL", new Vector3(-1.7f, 1.6f, 0f), new Vector3(0.2f, 3.2f, 6f), washWall);
            Box(wash, "WallR", new Vector3(1.7f, 1.6f, 0f), new Vector3(0.2f, 3.2f, 6f), washWall);
            Box(wash, "Roof", new Vector3(0f, 3.3f, 0f), new Vector3(3.8f, 0.3f, 6.2f), washWall);
            Cyl(wash, "BrushL", new Vector3(-1.3f, 1.4f, 1.6f), new Vector3(0.5f, 1.3f, 0.5f), brush);
            Cyl(wash, "BrushR", new Vector3(1.3f, 1.4f, 1.6f), new Vector3(0.5f, 1.3f, 0.5f), brush);
            Box(wash, "Sign", new Vector3(0f, 4f, -2.9f), new Vector3(3f, 0.9f, 0.15f), brush);
            var washGlow = Group(wash, "Glow", Vector3.zero);
            Box(washGlow, "LitSign", new Vector3(0f, 4f, -2.99f), new Vector3(2.6f, 0.6f, 0.03f), Mat("GasCarWashLit", new Color(0.7f, 0.85f, 0.95f), emission: 1.2f));
            PointLight(washGlow, new Vector3(0f, 2.8f, 0f), new Color(0.85f, 0.92f, 1f), 1.2f, 5f);
            evening.Add(washGlow);

            // 積んだタイヤ（右奥）
            var stack = Group(g, "TireStack", new Vector3(12.8f, 0f, 6f));
            for (int i = 0; i < 4; i++) Cyl(stack, "Tire", new Vector3(0f, 0.12f + i * 0.24f, 0f), new Vector3(0.8f, 0.12f, 0.8f), tire);

            // 自販機とゴミ箱（店の右）
            var machineColors = new[] { new Color(0.68f, 0.38f, 0.36f), new Color(0.42f, 0.5f, 0.62f) };
            for (int i = 0; i < 2; i++)
            {
                var vm = Group(g, "VendingMachine", new Vector3(6.9f + i * 1f, 0f, 11f));
                Box(vm, "Body", new Vector3(0f, 0.9f, 0f), new Vector3(0.9f, 1.8f, 0.75f), Mat("GasVending" + i, machineColors[i]));
                Box(vm, "Display", new Vector3(0f, 1.15f, -0.38f), new Vector3(0.7f, 0.7f, 0.02f), Mat("GasVendingPanel", new Color(0.92f, 0.92f, 0.9f)));
                var glow = Group(vm, "Glow", Vector3.zero);
                Box(glow, "LitPanel", new Vector3(0f, 1.15f, -0.4f), new Vector3(0.7f, 0.7f, 0.02f), Mat("GasVendingLit", new Color(0.95f, 0.97f, 1f), emission: 1.2f));
                evening.Add(glow);
            }
            var binColors = new[] { new Color(0.45f, 0.52f, 0.45f), new Color(0.45f, 0.5f, 0.58f), new Color(0.6f, 0.57f, 0.45f) };
            for (int i = 0; i < 3; i++)
            {
                Cyl(g, "TrashBin", new Vector3(8.9f + i * 0.6f, 0.45f, 11f), new Vector3(0.5f, 0.45f, 0.5f), Mat("GasBin" + i, binColors[i]));
            }

            // オイル缶の棚（店の前・左）
            var shelf = Group(g, "OilShelf", new Vector3(-5f, 0f, 9.5f));
            Box(shelf, "Shelf", new Vector3(0f, 0.4f, 0f), new Vector3(1.8f, 0.8f, 0.5f), pole);
            var canColors = new[] { new Color(0.65f, 0.4f, 0.35f), new Color(0.68f, 0.62f, 0.42f), new Color(0.42f, 0.5f, 0.62f), new Color(0.45f, 0.56f, 0.46f) };
            for (int i = 0; i < 4; i++)
            {
                Box(shelf, "OilCan", new Vector3(-0.6f + i * 0.4f, 0.97f, 0f), new Vector3(0.25f, 0.35f, 0.18f), Mat("GasOilCan" + i, canColors[i]));
            }

            // 植え込み（左右と奥）
            var hedge = Mat("GasHedge", new Color(0.28f, 0.5f, 0.25f));
            var brick = Mat("GasBrick", new Color(0.6f, 0.35f, 0.25f));
            foreach (float x in new[] { -16.5f, 16.5f })
            {
                Box(g, "PlanterBase", new Vector3(x, 0.2f, 4f), new Vector3(1.1f, 0.4f, 21f), brick);
                Box(g, "Hedge", new Vector3(x, 0.75f, 4f), new Vector3(0.9f, 0.8f, 20.6f), hedge);
            }
            Box(g, "PlanterBaseBack", new Vector3(0f, 0.2f, 16.2f), new Vector3(33f, 0.4f, 1.1f), brick);
            Box(g, "HedgeBack", new Vector3(0f, 0.75f, 16.2f), new Vector3(32.6f, 0.8f, 0.9f), hedge);
            foreach (var pos in new[] { new Vector3(-19f, 0f, -1f), new Vector3(-19.5f, 0f, 9f), new Vector3(19f, 0f, 1f), new Vector3(19.5f, 0f, 10f), new Vector3(-10f, 0f, 18f), new Vector3(11f, 0f, 18.5f) })
            {
                BuildTree(g, pos, 1.1f);
            }

            // 電柱と電線（奥）
            var poleXs = new[] { -18f, -6f, 6f, 18f };
            foreach (float x in poleXs)
            {
                Cyl(g, "UtilityPole", new Vector3(x, 4f, 18f), new Vector3(0.3f, 4f, 0.3f), Mat("UtilityPole", new Color(0.55f, 0.55f, 0.52f)));
                Box(g, "CrossArm", new Vector3(x, 7.4f, 18f), new Vector3(1.6f, 0.12f, 0.12f), pole);
            }
            for (int i = 0; i < poleXs.Length - 1; i++)
            {
                float mid = (poleXs[i] + poleXs[i + 1]) / 2f, len = poleXs[i + 1] - poleXs[i];
                foreach (float dx in new[] { -0.6f, 0.6f })
                {
                    Box(g, "Wire", new Vector3(mid + dx, 7.5f, 18f), new Vector3(len, 0.03f, 0.03f), tire);
                }
            }

            // 近所の建物（奥）。夕方は窓に明かりがつく
            BuildBuilding(g, "House_L", new Vector3(-14f, 0f, 23f), new Vector3(6f, 4f, 5f), new Color(0.78f, 0.75f, 0.7f), new Color(0.42f, 0.36f, 0.33f), 1, 2, darkGlass, litWindow, evening);
            BuildBuilding(g, "Apartment", new Vector3(-3.5f, 0f, 26f), new Vector3(10f, 9f, 6f), new Color(0.72f, 0.72f, 0.7f), new Color(0.5f, 0.5f, 0.5f), 3, 4, darkGlass, litWindow, evening);
            BuildBuilding(g, "Shop", new Vector3(8f, 0f, 23.5f), new Vector3(7f, 4.5f, 5f), new Color(0.8f, 0.79f, 0.76f), new Color(0.4f, 0.48f, 0.43f), 1, 3, darkGlass, litWindow, evening);
            BuildBuilding(g, "House_R", new Vector3(17f, 0f, 23f), new Vector3(6f, 4f, 5f), new Color(0.74f, 0.76f, 0.78f), new Color(0.38f, 0.4f, 0.46f), 1, 2, darkGlass, litWindow, evening);
        }

        /// <summary>箱の建物。正面（-Z 側）に窓を並べ、夕方用の光る窓を重ねる。</summary>
        private static void BuildBuilding(GameObject parent, string name, Vector3 pos, Vector3 size, Color wall, Color roof,
            int floors, int columns, Material window, Material litWindow, List<GameObject> evening)
        {
            var b = Group(parent, name, pos);
            Box(b, "Wall", new Vector3(0f, size.y / 2f, 0f), size, Mat("Wall_" + ColorUtility.ToHtmlStringRGB(wall), wall));
            Box(b, "Roof", new Vector3(0f, size.y + 0.15f, 0f), new Vector3(size.x + 0.4f, 0.3f, size.z + 0.4f), Mat("Roof_" + ColorUtility.ToHtmlStringRGB(roof), roof));
            var glow = Group(b, "WindowGlow", Vector3.zero);
            float floorHeight = size.y / floors;
            for (int f = 0; f < floors; f++)
            {
                for (int c = 0; c < columns; c++)
                {
                    var p = new Vector3(-size.x / 2f + size.x * (c + 0.5f) / columns, floorHeight * (f + 0.55f), -size.z / 2f - 0.02f);
                    var s = new Vector3(Mathf.Min(1.4f, size.x / columns * 0.6f), floorHeight * 0.45f, 0.04f);
                    Box(b, "Window", p, s, window);
                    // 背景が目立ちすぎないよう、明かりがつくのは一部の窓だけにする
                    if ((f * 7 + c * 3) % 3 == 0) Box(glow, "LitWindow", p + Vector3.back * 0.02f, s, litWindow);
                }
            }
            evening.Add(glow);
        }

        private static FuelPump BuildPump(GameObject root, string name, Vector3 pos, float yaw, bool low, bool roof, List<GameObject> evening)
        {
            var g = Group(root, name, pos);
            g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            float h = low ? 0.9f : 1.7f;
            var red = Mat("GasPumpBody", new Color(0.88f, 0.2f, 0.18f));
            Box(g, "Island", new Vector3(0f, 0.1f, 0f), new Vector3(2.8f, 0.2f, 1.2f), Mat("GasIsland", new Color(0.85f, 0.85f, 0.82f)));
            var body = Box(g, "Body", new Vector3(0f, 0.2f + h / 2f, 0f), new Vector3(0.9f, h, 0.6f), red);
            var panel = Box(g, "Panel", new Vector3(0f, 0.2f + h * 0.7f, -0.31f), new Vector3(0.6f, h * 0.3f, 0.02f), Mat("GasPumpPanel", new Color(0.95f, 0.95f, 0.95f)));
            Box(g, "Nozzle", new Vector3(0.5f, 0.2f + h * 0.5f, 0f), new Vector3(0.12f, 0.35f, 0.12f), Mat("GasNozzle", new Color(0.12f, 0.12f, 0.12f)));
            var blast = Spot(g, "BlastPoint", new Vector3(0f, 0.2f + h * 0.6f, 0f));

            if (roof)
            {
                var pole = Mat("GasPole", new Color(0.3f, 0.3f, 0.32f));
                Cyl(g, "PillarL", new Vector3(-1.5f, 2f, 0f), new Vector3(0.18f, 2f, 0.18f), pole);
                Cyl(g, "PillarR", new Vector3(1.5f, 2f, 0f), new Vector3(0.18f, 2f, 0.18f), pole);
                Box(g, "Canopy", new Vector3(0f, 4.1f, 0f), new Vector3(3.8f, 0.25f, 2.4f), Mat("GasRed", new Color(0.85f, 0.15f, 0.12f)));
                var glow = Group(g, "CanopyLight", Vector3.zero);
                Box(glow, "LitPanel", new Vector3(0f, 3.96f, 0f), new Vector3(3f, 0.04f, 1.6f), Mat("GasCanopyLit", new Color(1f, 0.97f, 0.85f), emission: 2.5f));
                PointLight(glow, new Vector3(0f, 3.5f, 0f), new Color(1f, 0.95f, 0.85f), 3.5f, 7f);
                evening.Add(glow);
            }

            var pump = g.AddComponent<FuelPump>();
            var so = new SerializedObject(pump);
            so.FindProperty("blastPoint").objectReferenceValue = blast;
            SetArray(so.FindProperty("scorchRenderers"), new List<Renderer> { body.GetComponent<Renderer>(), panel.GetComponent<Renderer>() });
            so.ApplyModifiedPropertiesWithoutUndo();
            return pump;
        }

        private enum CarKind
        {
            KeiTruck,
            Wagon,
        }

        /// <summary>車の前は +Z。ボンネットは付け根（フロントガラス側）を中心に開く。</summary>
        private static ParkedCar BuildCar(GameObject root, string name, Vector3 pos, float yaw, CarKind kind, Color color)
        {
            var g = Group(root, name, pos);
            g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var body = Group(g, "Body", Vector3.zero);
            var paintMat = Mat("CarPaint_" + ColorUtility.ToHtmlStringRGB(color), color);
            var glass = Mat("CarGlass", new Color(0.3f, 0.4f, 0.5f));
            var tire = Mat("CarTire", new Color(0.08f, 0.08f, 0.08f));

            Transform hood;
            float length, width = kind == CarKind.KeiTruck ? 1.45f : 1.75f;
            if (kind == CarKind.KeiTruck)
            {
                length = 3.4f;
                Box(body, "Cab", new Vector3(0f, 1.05f, 1.05f), new Vector3(width, 1.3f, 1.3f), paintMat);
                Box(body, "Windshield", new Vector3(0f, 1.35f, 1.71f), new Vector3(width * 0.85f, 0.55f, 0.03f), glass);
                Box(body, "Bed", new Vector3(0f, 0.55f, -0.65f), new Vector3(width, 0.3f, 2.1f), paintMat);
                Box(body, "BedWallL", new Vector3(-width / 2f, 0.85f, -0.65f), new Vector3(0.06f, 0.35f, 2.1f), paintMat);
                Box(body, "BedWallR", new Vector3(width / 2f, 0.85f, -0.65f), new Vector3(0.06f, 0.35f, 2.1f), paintMat);
                // 軽トラはボンネットの代わりに荷台のあおりが跳ねる
                hood = Group(body, "Hood", new Vector3(0f, 0.7f, -1.7f)).transform;
                Box(hood.gameObject, "Gate", new Vector3(0f, 0.18f, 0f), new Vector3(width, 0.35f, 0.06f), paintMat);
            }
            else
            {
                length = 4.3f;
                Box(body, "Lower", new Vector3(0f, 0.65f, 0f), new Vector3(width, 0.7f, length), paintMat);
                Box(body, "Cabin", new Vector3(0f, 1.35f, -0.4f), new Vector3(width * 0.94f, 0.7f, 3f), glass);
                Box(body, "CabinRoof", new Vector3(0f, 1.72f, -0.4f), new Vector3(width * 0.96f, 0.06f, 3.05f), paintMat);
                hood = Group(body, "Hood", new Vector3(0f, 1.01f, 1.1f)).transform;
                Box(hood.gameObject, "Panel", new Vector3(0f, 0f, 0.5f), new Vector3(width * 0.95f, 0.05f, 1f), paintMat);
            }

            foreach (float x in new[] { -width / 2f, width / 2f })
            {
                foreach (float z in new[] { -length * 0.32f, length * 0.32f })
                {
                    var wheel = Cyl(body, "Wheel", new Vector3(x, 0.32f, z), new Vector3(0.62f, 0.12f, 0.62f), tire);
                    wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }
            }

            var car = g.AddComponent<ParkedCar>();
            var so = new SerializedObject(car);
            so.FindProperty("body").objectReferenceValue = body.transform;
            so.FindProperty("hood").objectReferenceValue = hood;
            so.ApplyModifiedPropertiesWithoutUndo();
            return car;
        }

        private static GasStationStaff BuildStaff(GameObject root, Vector3 pos)
        {
            var staff = Group(root, "Staff", pos);
            FaceCenter(staff.transform);
            var visual = Group(staff, "Visual", Vector3.zero);
            var skinColor = new Color(1f, 0.84f, 0.7f);
            Capsule(visual, "Body", new Vector3(0f, 0.75f, 0f), new Vector3(0.55f, 0.7f, 0.4f), Mat("StaffUniform", new Color(0.2f, 0.45f, 0.75f)));
            Box(visual, "Cap", new Vector3(0f, 1.82f, 0.03f), new Vector3(0.42f, 0.12f, 0.45f), Mat("GasRed", new Color(0.85f, 0.15f, 0.12f)));
            var head = Sphere(visual, "Head", new Vector3(0f, 1.62f, 0f), Vector3.one * 0.4f, Mat("KidSkin", skinColor));
            var eye = Mat("KidEye", new Color(0.1f, 0.1f, 0.12f));
            Sphere(visual, "EyeL", new Vector3(-0.08f, 1.66f, 0.18f), Vector3.one * 0.07f, eye);
            Sphere(visual, "EyeR", new Vector3(0.08f, 1.66f, 0.18f), Vector3.one * 0.07f, eye);
            var extinguisher = Group(visual, "Extinguisher", new Vector3(0.38f, 0.55f, 0.15f));
            Cyl(extinguisher, "Tank", Vector3.zero, new Vector3(0.22f, 0.3f, 0.22f), Mat("GasRed", new Color(0.85f, 0.15f, 0.12f)));
            var nozzle = Spot(extinguisher, "Nozzle", new Vector3(0f, 0.35f, 0.2f));
            var alert = BuildAlert(visual, 2.2f);

            var scared = staff.AddComponent<ScaredBystander>();
            var sso = new SerializedObject(scared);
            sso.FindProperty("visual").objectReferenceValue = visual.transform;
            SetArray(sso.FindProperty("skinRenderers"), new List<Renderer> { head.GetComponent<Renderer>() });
            sso.FindProperty("skinColor").colorValue = skinColor;
            sso.FindProperty("alert").objectReferenceValue = alert;
            sso.FindProperty("headHeight").floatValue = 1.62f;
            // 店員は爆発で大慌てせず、すぐ消火に走る
            sso.FindProperty("panicOnExplosion").boolValue = false;
            sso.ApplyModifiedPropertiesWithoutUndo();

            var rush = staff.AddComponent<GasStationStaff>();
            var rso = new SerializedObject(rush);
            rso.FindProperty("nozzle").objectReferenceValue = nozzle;
            rso.ApplyModifiedPropertiesWithoutUndo();
            return rush;
        }

        // ---------------------------------------------------------------- 共通

        private static void Assign(DifficultyLevel level, GameObject prefab, StageTimeOfDay time)
        {
            string path = $"Assets/Resources/{DifficultySettings.ResourceFolder}{level}.asset";
            var settings = AssetDatabase.LoadAssetAtPath<DifficultySettings>(path);
            if (settings == null)
            {
                Debug.LogWarning($"{path} が見つからないため、{level} にステージを割り当てられませんでした");
                return;
            }
            settings.stagePrefab = prefab;
            settings.timeOfDay = time;
            EditorUtility.SetDirty(settings);
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void FaceCenter(Transform t)
        {
            var dir = -t.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f) t.rotation = Quaternion.LookRotation(dir);
        }

        private static GameObject Group(GameObject parent, string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            return go;
        }

        private static Transform Spot(GameObject parent, string name, Vector3 localPos) => Group(parent, name, localPos).transform;

        private static GameObject Box(GameObject parent, string name, Vector3 pos, Vector3 scale, Material mat) => Primitive(PrimitiveType.Cube, parent, name, pos, scale, mat);
        private static GameObject Cyl(GameObject parent, string name, Vector3 pos, Vector3 scale, Material mat) => Primitive(PrimitiveType.Cylinder, parent, name, pos, scale, mat);
        private static GameObject Sphere(GameObject parent, string name, Vector3 pos, Vector3 scale, Material mat) => Primitive(PrimitiveType.Sphere, parent, name, pos, scale, mat);
        private static GameObject Capsule(GameObject parent, string name, Vector3 pos, Vector3 scale, Material mat) => Primitive(PrimitiveType.Capsule, parent, name, pos, scale, mat);

        private static GameObject Primitive(PrimitiveType type, GameObject parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static void PointLight(GameObject parent, Vector3 pos, Color color, float intensity, float range)
        {
            var go = Group(parent, "Light", pos);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }

        /// <summary>色ごとにマテリアルをアセットとして作る（プレハブから参照できるように）。2 回目以降は色だけ更新する。</summary>
        private static Material Mat(string name, Color color, float emission = 0f)
        {
            if (Materials.TryGetValue(name, out var cached)) return cached;

            string path = $"{MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (emission > 0f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                mat.SetColor("_EmissionColor", color * emission);
            }
            EditorUtility.SetDirty(mat);
            Materials[name] = mat;
            return mat;
        }

        private static void SetObjects<T>(Object target, string property, List<T> items) where T : Object
        {
            var so = new SerializedObject(target);
            SetArray(so.FindProperty(property), items);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray<T>(SerializedProperty prop, List<T> items) where T : Object
        {
            prop.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
