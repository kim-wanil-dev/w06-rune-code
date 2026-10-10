using System;
using System.IO;

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

using TMPro;

namespace RuneCode
{
    /// <summary>
    /// 미션 월드 표시에 쓰는 도형 스프라이트, 개체 뷰 Prefab과 스프라이트 재질을 준비하는 에디터 빌더다.
    /// 이미 있는 자산은 다시 만들지 않으므로 Prefab·스프라이트를 직접 고친 내용은 보존된다.
    /// </summary>
    public static class MissionWorldAssets
    {
        public const string SHAPE_FOLDER = "Assets/RuneCode/Art/Shapes";
        public const string PREFAB_FOLDER = "Assets/RuneCode/Prefabs/Mission";
        public const string SPRITE_MATERIAL_PATH = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

        private const int SHAPE_TEXTURE_SIZE = 128;
        private const int SUPERSAMPLE = 4;
        private const float RING_INNER = 0.86f;
        private const float RING_THIN_INNER = 0.97f;
        private const float ARC_INNER = 0.80f;
        private const float ARC_HALF_DEGREES = 60f;
        private const float SQUARE_OUTLINE_INNER = 0.92f;
        private const float DAMAGE_FONT_SIZE = 5.6f;

        private static readonly Color DefaultEnemyTint = new Color(0.94f, 0.34f, 0.40f);
        private static readonly Color SplitterEnemyTint = new Color(0.36f, 0.84f, 0.44f);
        private static readonly (string name, string id, string shape, Color tint)[] _additionalEnemies =
        {
            ("BombSeed", "enemy.bomb_seed", "Circle", new Color(1f, 0.55f, 0.2f)),
            ("ClockSniper", "enemy.clock_sniper", "Poly4", new Color(0.8f, 0.15f, 0.24f)),
            ("ShieldMelee", "enemy.shield_melee", "Poly3", new Color(0.3f, 0.95f, 0.85f)),
            ("ShieldRanged", "enemy.shield_ranged", "Poly4", new Color(0.3f, 0.95f, 0.85f)),
            ("Carrier", "enemy.carrier", "Poly8", new Color(0.3f, 0.4f, 0.8f)),
            ("Interceptor", "enemy.interceptor", "Poly3", new Color(1f, 0.45f, 0.5f))
        };

        /// <summary>공통 적에 필요한 새 표시와 외형을 추가하고 6종 Variant·캐리어 생성 참조를 준비한다. 기존 조정값은 보존한다.</summary>
        [MenuItem("Rune Code/Prepare Additional Enemies")]
        public static void PrepareAdditionalEnemies()
        {
            Ensure();
            const string enemyPath = PREFAB_FOLDER + "/MissionEnemy.prefab";
            const string variantFolder = "Assets/RuneCode/Resources/RuneCode/enemies";
            LayoutUtility.EnsureFolder(variantFolder);
            GameObject root = PrefabUtility.LoadPrefabContents(enemyPath);
            try
            {
                EnemyView view = root.GetComponent<EnemyView>();
                var serialized = new SerializedObject(view);
                bool hasChanged = false;
                foreach (string field in new[] { "_circleShield", "_sniperLine" })
                {
                    SerializedProperty property = serialized.FindProperty(field);
                    if (property.objectReferenceValue != null) continue;
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SHAPE_FOLDER + (field == "_circleShield" ? "/RingThin.png" : "/Square.png"));
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(SPRITE_MATERIAL_PATH);
                    property.objectReferenceValue = Child(root.transform, field == "_circleShield" ? "CircleShield" : "SniperLine", sprite, material, 48);
                    hasChanged = true;
                }
                SerializedProperty appearances = serialized.FindProperty("_appearances");
                foreach (var entry in _additionalEnemies)
                {
                    bool exists = false;
                    for (int i = 0; i < appearances.arraySize; i++)
                        if (appearances.GetArrayElementAtIndex(i).FindPropertyRelative("_enemyId").stringValue == entry.id) { exists = true; break; }
                    if (exists) continue;
                    appearances.InsertArrayElementAtIndex(appearances.arraySize);
                    SerializedProperty item = appearances.GetArrayElementAtIndex(appearances.arraySize - 1);
                    item.FindPropertyRelative("_enemyId").stringValue = entry.id;
                    item.FindPropertyRelative("_shape").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(SHAPE_FOLDER + "/" + entry.shape + ".png");
                    item.FindPropertyRelative("_tint").colorValue = entry.tint;
                    hasChanged = true;
                }
                if (hasChanged) { serialized.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SaveAsPrefabAsset(root, enemyPath); }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(enemyPath);
            foreach (var entry in _additionalEnemies)
            {
                string path = variantFolder + "/" + entry.name + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) continue;
                var scene = EditorSceneManager.NewPreviewScene();
                try
                {
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
                    instance.name = entry.name;
                    var serialized = new SerializedObject(instance.GetComponent<EnemyView>());
                    serialized.FindProperty("_enemyId").stringValue = entry.id;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(instance, path);
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
            string carrierPath = variantFolder + "/Carrier.prefab";
            root = PrefabUtility.LoadPrefabContents(carrierPath);
            try
            {
                if (root.GetComponent<CarrierSpawnSettings>() == null)
                {
                    CarrierSpawnSettings settings = root.AddComponent<CarrierSpawnSettings>();
                    var serialized = new SerializedObject(settings);
                    SerializedProperty targets = serialized.FindProperty("_targets");
                    targets.arraySize = 1;
                    targets.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<EnemyView>(variantFolder + "/Interceptor.prefab");
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, carrierPath);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            MissionLayout.PrepareAdditionalDebugPopup();
            CarrierSpawnSettings.LoadDefinition().Validate(EnemyCatalog.FromJson(Resources.Load<TextAsset>("RuneCode/enemies").text));
        }

        /// <summary>도형 스프라이트, 재질, 6종 뷰 Prefab을 준비해 씬 빌더가 연결할 자산 묶음으로 반환한다.</summary>
        public static Result Ensure()
        {
            var shapes = new Shapes
            {
                Triangle = EnsureShape("Poly3", (x, y) => InPolygon(x, y, 3)),
                Diamond = EnsureShape("Poly4", (x, y) => InPolygon(x, y, 4)),
                Hexagon = EnsureShape("Poly6", (x, y) => InPolygon(x, y, 6)),
                Octagon = EnsureShape("Poly8", (x, y) => InPolygon(x, y, 8)),
                Circle = EnsureShape("Circle", (x, y) => x * x + y * y <= 1),
                Square = EnsureShape("Square", (x, y) => true),
                SquareOutline = EnsureShape("SquareOutline", (x, y) => Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) >= SQUARE_OUTLINE_INNER),
                Ring = EnsureShape("Ring", (x, y) => InRing(x, y, RING_INNER)),
                RingThin = EnsureShape("RingThin", (x, y) => InRing(x, y, RING_THIN_INNER)),
                Arc = EnsureShape("Arc", (x, y) => InRing(x, y, ARC_INNER) && Mathf.Abs(Mathf.Atan2(y, x) * Mathf.Rad2Deg) <= ARC_HALF_DEGREES)
            };
            Material material = AssetDatabase.LoadAssetAtPath<Material>(SPRITE_MATERIAL_PATH);
            if (material == null) throw new InvalidOperationException("URP 스프라이트 재질을 찾을 수 없습니다: " + SPRITE_MATERIAL_PATH);
            LayoutUtility.EnsureFolder(PREFAB_FOLDER);
            return new Result
            {
                Square = shapes.Square,
                Material = material,
                Player = EnsurePrefab("MissionPlayer", () => BuildPlayer(shapes, material)).GetComponent<PlayerView>(),
                Enemy = EnsurePrefab("MissionEnemy", () => BuildEnemy(shapes, material)).GetComponent<EnemyView>(),
                Spell = EnsurePrefab("MissionSpell", () => BuildSpell(shapes, material)).GetComponent<SpellEntityView>(),
                Projectile = EnsurePrefab("MissionHostileProjectile", () => BuildProjectile(shapes, material)).GetComponent<HostileProjectileView>(),
                Orb = EnsurePrefab("MissionOrb", () => BuildOrb(shapes, material)).GetComponent<OrbView>(),
                ItemDrop = EnsurePrefab("MissionItemDrop", () => BuildItemDrop(shapes, material)).GetComponent<ItemDropView>(),
                DamageNumber = EnsurePrefab("MissionDamageNumber", BuildDamageNumber).GetComponent<DamageNumberView>()
            };
        }

        /// <summary>플레이어 몸체(삼각형), 링, 조준선, 대시 잔상, 보호막 링을 가진 플레이어 뷰를 만든다.</summary>
        private static GameObject BuildPlayer(Shapes shapes, Material material)
        {
            var root = new GameObject("MissionPlayer", typeof(PlayerView));
            PlayerView view = root.GetComponent<PlayerView>();
            LayoutUtility.SetReference(view, "_ring", Child(root.transform, "Ring", shapes.RingThin, material, 60));
            LayoutUtility.SetReference(view, "_dashTrail", Child(root.transform, "DashTrail", shapes.Square, material, 59));
            LayoutUtility.SetReference(view, "_body", Child(root.transform, "Body", shapes.Triangle, material, 61));
            LayoutUtility.SetReference(view, "_aimLine", Child(root.transform, "AimLine", shapes.Square, material, 62));
            LayoutUtility.SetReference(view, "_shieldRing", Child(root.transform, "ShieldRing", shapes.Ring, material, 63));
            return root;
        }

        /// <summary>종류별 모양 표와 몸체·방패(회전 자식), 예고·오라·패치·엘리트 링, 상태 아이콘, 체력 막대를 가진 적 뷰를 만든다.</summary>
        private static GameObject BuildEnemy(Shapes shapes, Material material)
        {
            var root = new GameObject("MissionEnemy", typeof(EnemyView));
            EnemyView view = root.GetComponent<EnemyView>();
            var rotator = new GameObject("Rotator").transform;
            rotator.SetParent(root.transform, false);
            LayoutUtility.SetReference(view, "_rotator", rotator);
            LayoutUtility.SetReference(view, "_auraRing", Child(root.transform, "AuraRing", shapes.RingThin, material, 49));
            LayoutUtility.SetReference(view, "_eliteRing", Child(root.transform, "EliteRing", shapes.RingThin, material, 50));
            LayoutUtility.SetReference(view, "_body", Child(rotator, "Body", shapes.Hexagon, material, 51));
            LayoutUtility.SetReference(view, "_core", Child(rotator, "Core", shapes.Hexagon, material, 52));
            LayoutUtility.SetReference(view, "_shieldArc", Child(rotator, "ShieldArc", shapes.Arc, material, 53));
            LayoutUtility.SetReference(view, "_warningRing", Child(root.transform, "WarningRing", shapes.Ring, material, 54));
            LayoutUtility.SetReference(view, "_patchRing", Child(root.transform, "PatchRing", shapes.Ring, material, 54));
            LayoutUtility.SetReference(view, "_burnIcon", Child(root.transform, "BurnIcon", shapes.Triangle, material, 55));
            LayoutUtility.SetReference(view, "_chillIcon", Child(root.transform, "ChillIcon", shapes.Hexagon, material, 55));
            LayoutUtility.SetReference(view, "_empIcon", Child(root.transform, "EmpIcon", shapes.Diamond, material, 55));
            LayoutUtility.SetReference(view, "_resistRing", Child(root.transform, "ResistRing", shapes.Ring, material, 55));
            LayoutUtility.SetReference(view, "_hpBack", Child(root.transform, "HpBack", shapes.Square, material, 56));
            LayoutUtility.SetReference(view, "_hpFill", Child(root.transform, "HpFill", shapes.Square, material, 57));
            LayoutUtility.SetReference(view, "_defaultShape", shapes.Hexagon);
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_defaultTint").colorValue = DefaultEnemyTint;
            (string id, Sprite shape, Color tint)[] appearances =
            {
                ("enemy.scout", shapes.Triangle, DefaultEnemyTint),
                ("enemy.sentry", shapes.Diamond, DefaultEnemyTint),
                ("enemy.hunter", shapes.Diamond, DefaultEnemyTint),
                ("enemy.aegis", shapes.Hexagon, new Color(0.96f, 0.69f, 0.29f)),
                ("enemy.relay", shapes.Octagon, new Color(0.62f, 0.39f, 0.97f)),
                ("boss.governor", shapes.Hexagon, new Color(0.89f, 0.35f, 0.92f)),
                ("enemy.splitter", shapes.Circle, SplitterEnemyTint),
                ("enemy.splitter_mid", shapes.Circle, SplitterEnemyTint),
                ("enemy.splitter_small", shapes.Circle, SplitterEnemyTint)
            };
            SerializedProperty list = serialized.FindProperty("_appearances");
            list.arraySize = appearances.Length;
            for (int i = 0; i < appearances.Length; i++)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("_enemyId").stringValue = appearances[i].id;
                element.FindPropertyRelative("_shape").objectReferenceValue = appearances[i].shape;
                element.FindPropertyRelative("_tint").colorValue = appearances[i].tint;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        /// <summary>잔상·도형(발사·공전), 채움·외곽선·안쪽 선(폭발·잔류)과 부채꼴 메시 자식을 가진 마법 개체 뷰를 만든다.</summary>
        private static GameObject BuildSpell(Shapes shapes, Material material)
        {
            var root = new GameObject("MissionSpell", typeof(SpellEntityView));
            SpellEntityView view = root.GetComponent<SpellEntityView>();
            LayoutUtility.SetReference(view, "_trail", Child(root.transform, "Trail", shapes.Square, material, 30));
            LayoutUtility.SetReference(view, "_fill", Child(root.transform, "Fill", shapes.Circle, material, 31));
            var cone = new GameObject("Cone", typeof(MeshFilter), typeof(MeshRenderer));
            cone.transform.SetParent(root.transform, false);
            MeshRenderer coneRenderer = cone.GetComponent<MeshRenderer>();
            coneRenderer.sharedMaterial = material;
            coneRenderer.sortingOrder = 31;
            LayoutUtility.SetReference(view, "_coneFilter", cone.GetComponent<MeshFilter>());
            LayoutUtility.SetReference(view, "_coneRenderer", coneRenderer);
            LayoutUtility.SetReference(view, "_outline", Child(root.transform, "Outline", shapes.Ring, material, 32));
            LayoutUtility.SetReference(view, "_inner", Child(root.transform, "Inner", shapes.Ring, material, 33));
            LayoutUtility.SetReference(view, "_shape", Child(root.transform, "Shape", shapes.Circle, material, 34));
            LayoutUtility.SetReference(view, "_fireShape", shapes.Triangle);
            LayoutUtility.SetReference(view, "_iceShape", shapes.Hexagon);
            LayoutUtility.SetReference(view, "_arcShape", shapes.Diamond);
            LayoutUtility.SetReference(view, "_defaultShape", shapes.Circle);
            LayoutUtility.SetReference(view, "_squareShape", shapes.Square);
            LayoutUtility.SetReference(view, "_ringShape", shapes.Ring);
            LayoutUtility.SetReference(view, "_squareOutlineShape", shapes.SquareOutline);
            return root;
        }

        /// <summary>적 탄환(마름모·잔상)과 위험 장판(채움·링·사선)을 가진 적 탄 뷰를 만든다.</summary>
        private static GameObject BuildProjectile(Shapes shapes, Material material)
        {
            var root = new GameObject("MissionHostileProjectile", typeof(HostileProjectileView));
            HostileProjectileView view = root.GetComponent<HostileProjectileView>();
            LayoutUtility.SetReference(view, "_hazardFill", Child(root.transform, "HazardFill", shapes.Circle, material, 20));
            LayoutUtility.SetReference(view, "_hazardRing", Child(root.transform, "HazardRing", shapes.Ring, material, 21));
            LayoutUtility.SetReference(view, "_hazardMark", Child(root.transform, "HazardMark", shapes.Square, material, 22));
            LayoutUtility.SetReference(view, "_trail", Child(root.transform, "Trail", shapes.Square, material, 40));
            LayoutUtility.SetReference(view, "_bullet", Child(root.transform, "Bullet", shapes.Diamond, material, 41));
            return root;
        }

        /// <summary>마름모와 링을 가진 조각 오브 뷰를 만든다.</summary>
        private static GameObject BuildOrb(Shapes shapes, Material material)
        {
            var root = new GameObject("MissionOrb", typeof(OrbView));
            OrbView view = root.GetComponent<OrbView>();
            LayoutUtility.SetReference(view, "_ring", Child(root.transform, "Ring", shapes.Ring, material, 10));
            LayoutUtility.SetReference(view, "_core", Child(root.transform, "Core", shapes.Diamond, material, 11));
            return root;
        }

        /// <summary>정사각 코어와 링을 가진 바닥 Modifier 드롭 뷰를 만든다.</summary>
        private static GameObject BuildItemDrop(Shapes shapes, Material material)
        {
            var root = new GameObject("MissionItemDrop", typeof(ItemDropView));
            ItemDropView view = root.GetComponent<ItemDropView>();
            LayoutUtility.SetReference(view, "_ring", Child(root.transform, "Ring", shapes.Square, material, 3));
            LayoutUtility.SetReference(view, "_core", Child(root.transform, "Core", shapes.Square, material, 4));
            return root;
        }

        /// <summary>프로젝트 한글 글꼴을 쓰는 월드 TMP 텍스트 하나를 가진 피해 숫자 뷰를 만든다.</summary>
        private static GameObject BuildDamageNumber()
        {
            var root = new GameObject("MissionDamageNumber", typeof(DamageNumberView));
            var labelObject = new GameObject("Label", typeof(TextMeshPro));
            labelObject.transform.SetParent(root.transform, false);
            TextMeshPro label = labelObject.GetComponent<TextMeshPro>();
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LayoutUtility.FONT_PATH);
            label.fontSize = DAMAGE_FONT_SIZE;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.sizeDelta = new Vector2(3, 1);
            label.sortingOrder = 70;
            LayoutUtility.SetReference(root.GetComponent<DamageNumberView>(), "_label", label);
            return root;
        }

        /// <summary>부모 아래에 스프라이트·재질·정렬 순서를 지정한 자식 스프라이트 렌더러를 만들어 반환한다.</summary>
        private static SpriteRenderer Child(Transform parent, string name, Sprite sprite, Material material, int order)
        {
            var renderer = new GameObject(name, typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            renderer.transform.SetParent(parent, false);
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.sortingOrder = order;
            return renderer;
        }

        /// <summary>Prefab이 없을 때만 만들어 저장하고, 있으면 기존 자산을 그대로 반환한다.</summary>
        private static GameObject EnsurePrefab(string name, Func<GameObject> build)
        {
            string path = PREFAB_FOLDER + "/" + name + ".prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            GameObject source = build();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, path);
            UnityEngine.Object.DestroyImmediate(source);
            return prefab;
        }

        /// <summary>
        /// 도형 스프라이트가 없을 때만 내부 판정 함수로 흰색 도형 PNG를 그려 저장하고 1 unit 크기 스프라이트로 가져온다.
        /// 판정 함수는 중심 0, 반경 1 좌표에서 점이 도형 안인지 반환한다.
        /// </summary>
        private static Sprite EnsureShape(string name, Func<float, float, bool> isInside)
        {
            string path = SHAPE_FOLDER + "/" + name + ".png";
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;
            LayoutUtility.EnsureFolder(SHAPE_FOLDER);
            var texture = new Texture2D(SHAPE_TEXTURE_SIZE, SHAPE_TEXTURE_SIZE, TextureFormat.RGBA32, false);
            var pixels = new Color32[SHAPE_TEXTURE_SIZE * SHAPE_TEXTURE_SIZE];
            for (int py = 0; py < SHAPE_TEXTURE_SIZE; py++)
                for (int px = 0; px < SHAPE_TEXTURE_SIZE; px++)
                {
                    int covered = 0;
                    for (int sy = 0; sy < SUPERSAMPLE; sy++)
                        for (int sx = 0; sx < SUPERSAMPLE; sx++)
                        {
                            float x = (px + (sx + 0.5f) / SUPERSAMPLE) / SHAPE_TEXTURE_SIZE * 2 - 1;
                            float y = (py + (sy + 0.5f) / SUPERSAMPLE) / SHAPE_TEXTURE_SIZE * 2 - 1;
                            if (isInside(x, y)) covered++;
                        }
                    pixels[py * SHAPE_TEXTURE_SIZE + px] = new Color32(255, 255, 255, (byte)(255 * covered / (SUPERSAMPLE * SUPERSAMPLE)));
                }
            texture.SetPixels32(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = SHAPE_TEXTURE_SIZE;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>꼭짓점 하나가 +x 방향인 정다각형(외접원 반경 1) 안에 점이 있는지 반환한다.</summary>
        private static bool InPolygon(float x, float y, int sides)
        {
            float sector = Mathf.PI * 2 / sides;
            float angle = Mathf.Repeat(Mathf.Atan2(y, x), Mathf.PI * 2);
            float center = (Mathf.Floor(angle / sector) + 0.5f) * sector;
            return Mathf.Sqrt(x * x + y * y) * Mathf.Cos(angle - center) <= Mathf.Cos(Mathf.PI / sides);
        }

        /// <summary>반경 1 원과 안쪽 반경 사이의 고리 안에 점이 있는지 반환한다.</summary>
        private static bool InRing(float x, float y, float inner)
        {
            float distance = x * x + y * y;
            return distance <= 1 && distance >= inner * inner;
        }

        private sealed class Shapes
        {
            public Sprite Triangle { get; set; }
            public Sprite Diamond { get; set; }
            public Sprite Hexagon { get; set; }
            public Sprite Octagon { get; set; }
            public Sprite Circle { get; set; }
            public Sprite Square { get; set; }
            public Sprite SquareOutline { get; set; }
            public Sprite Ring { get; set; }
            public Sprite RingThin { get; set; }
            public Sprite Arc { get; set; }
        }

        /// <summary>씬 빌더가 미션 월드에 연결할 자산 묶음이다.</summary>
        public sealed class Result
        {
            public Sprite Square { get; set; }
            public Material Material { get; set; }
            public PlayerView Player { get; set; }
            public EnemyView Enemy { get; set; }
            public SpellEntityView Spell { get; set; }
            public HostileProjectileView Projectile { get; set; }
            public OrbView Orb { get; set; }
            public ItemDropView ItemDrop { get; set; }
            public DamageNumberView DamageNumber { get; set; }
        }
    }
}
