using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class WorkshopProjectBuilder
{
    private const string ROOT = "Assets/_RobotWorkshop";
    private const string ART = ROOT + "/Art";
    private const string DATA = ROOT + "/Data";
    private const string PREFABS = ROOT + "/Prefabs";
    private const string SCENES = ROOT + "/Scenes";
    private const string SCENE_PATH = SCENES + "/RobotWorkshop_PoC.unity";
    private const int SPRITE_PIXELS_PER_UNIT = 32;
    private const int WORLD_REFERENCE_WIDTH = 960;
    private const int WORLD_REFERENCE_HEIGHT = 540;

    /// <summary>Creates missing PoC assets, default definitions, the robot prefab, and the start scene.</summary>
    [MenuItem("Robot Workshop/Create PoC Assets")]
    public static void CreatePoCAssets()
    {
        EnsureFolders();
        CreatePixelArt();
        Dictionary<string, Sprite> sprites = LoadSprites();
        DefinitionSet definitions = CreateDefinitions(sprites);
        WorkshopRobotView robotPrefab = CreateRobotPrefab(definitions, sprites);
        CreateStartingScene(definitions, robotPrefab, sprites);
        PlayerSettings.companyName = "RobotWorkshop";
        PlayerSettings.productName = "RobotWorkshop_PoC";
        PlayerSettings.runInBackground = true;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Robot Workshop PoC assets and starting scene are ready.");
    }

    /// <summary>Builds a Windows x64 Development Player at the PRD's requested delivery path.</summary>
    [MenuItem("Robot Workshop/Build Windows Development Player")]
    public static void BuildWindowsDevelopmentPlayer()
    {
        CreatePoCAssets();
        string outputDirectory = Path.GetFullPath("Build/RobotWorkshop_PoC");
        Directory.CreateDirectory(outputDirectory);
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { SCENE_PATH },
            locationPathName = Path.Combine(outputDirectory, "RobotWorkshop_PoC.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            throw new InvalidOperationException($"Windows build ended with {report.summary.result}.");
        }

        Debug.Log($"Windows development build created: {options.locationPathName}");
    }

    /// <summary>Creates missing feature folders without replacing existing project assets.</summary>
    private static void EnsureFolders()
    {
        EnsureFolder("Assets", "_RobotWorkshop");
        EnsureFolder(ROOT, "Art");
        EnsureFolder(ROOT, "Data");
        EnsureFolder(ROOT, "Prefabs");
        EnsureFolder(ROOT, "Scenes");
    }

    /// <summary>Creates one missing child folder under an existing Unity asset folder.</summary>
    private static void EnsureFolder(string parent, string name)
    {
        string path = $"{parent}/{name}";
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    /// <summary>Writes all project-owned temporary PNGs and applies the replacement-friendly sprite import settings.</summary>
    private static void CreatePixelArt()
    {
        string[] robotNames =
        {
            "Core_Universal", "Core_Industrial", "Head_Basic", "Head_Compute", "Legs_Basic", "Legs_Heavy",
            "Arm_MultiTool_Left", "Arm_MultiTool_Right", "Arm_Magnet_Left", "Arm_Magnet_Right",
            "Arm_Cable_Left", "Arm_Cable_Right", "Arm_Precision_Left", "Arm_Precision_Right",
            "Arm_Claw_Left", "Arm_Claw_Right"
        };
        for (int i = 0; i < robotNames.Length; i++)
        {
            string name = robotNames[i];
            WriteSpriteIfMissing($"{ART}/{name}.png", DrawRobotArt(name), false);
        }

        string[] facilityNames =
        {
            "Storage_Damaged", "Storage_Restored", "Storage_Upgraded",
            "District_Damaged", "District_Restored", "District_Upgraded"
        };
        for (int i = 0; i < facilityNames.Length; i++)
        {
            WriteSpriteIfMissing($"{ART}/{facilityNames[i]}.png", DrawFacilityArt(facilityNames[i]), false);
        }

        string[] resourceNames = { "Iron", "Copper", "Electronics" };
        for (int i = 0; i < resourceNames.Length; i++)
        {
            WriteSpriteIfMissing($"{ART}/Source_{resourceNames[i]}.png", DrawResourceArt(resourceNames[i], false), false);
            WriteSpriteIfMissing($"{ART}/Bonus_{resourceNames[i]}.png", DrawResourceArt(resourceNames[i], true), false);
            WriteSpriteIfMissing($"{ART}/Icon_{resourceNames[i]}.png", DrawResourceIcon(resourceNames[i]), true);
        }

        WriteSpriteIfMissing($"{ART}/Ground_Tile.png", DrawGroundArt(), false);
        WriteSpriteIfMissing($"{ART}/Selection_Ring.png", DrawSelectionRing(), true);
        WriteSpriteIfMissing($"{ART}/Icon_Scrap.png", DrawScrapIcon(), true);
    }

    /// <summary>Creates the five shared-canvas robot body parts as detailed pixel art.</summary>
    private static PixelCanvas DrawRobotArt(string name)
    {
        PixelCanvas canvas = new PixelCanvas(64, 64);
        Color32 outline = new Color32(24, 32, 40, 255);
        Color32 steel = new Color32(104, 126, 138, 255);
        Color32 steelLight = new Color32(179, 197, 194, 255);
        Color32 steelDark = new Color32(59, 78, 88, 255);
        Color32 glow = new Color32(68, 219, 212, 255);
        Color32 amber = new Color32(231, 163, 64, 255);

        if (name.StartsWith("Core_", StringComparison.Ordinal))
        {
            bool industrial = name.EndsWith("Industrial", StringComparison.Ordinal);
            canvas.Ellipse(16, 13, 48, 50, outline);
            canvas.Ellipse(18, 15, 46, 48, industrial ? amber : steel);
            canvas.Ellipse(21, 36, 38, 45, steelLight);
            canvas.Rect(22, 25, 42, 35, steelDark);
            canvas.Rect(24, 27, 40, 33, industrial ? amber : steel);
            canvas.Rect(27, 29, 37, 31, glow);
            canvas.Pixel(23, 20, steelLight);
            canvas.Pixel(41, 20, steelLight);
            canvas.Line(24, 40, 40, 40, steelDark);
            canvas.Line(24, 18, 28, 16, steelLight);
            canvas.Line(38, 16, 42, 18, steelLight);
            canvas.Line(18, 31, 13, 31, steelDark);
            canvas.Line(46, 31, 51, 31, steelDark);
            if (industrial)
            {
                canvas.Line(20, 42, 29, 47, steelLight);
                canvas.Line(44, 42, 35, 47, steelLight);
                canvas.Rect(28, 12, 36, 16, amber);
            }

            return canvas;
        }

        if (name.StartsWith("Head_", StringComparison.Ordinal))
        {
            bool compute = name.EndsWith("Compute", StringComparison.Ordinal);
            int left = compute ? 19 : 22;
            int right = compute ? 45 : 42;
            canvas.Rect(left, 42, right, 56, outline);
            canvas.Rect(left + 2, 44, right - 2, 54, steel);
            canvas.Line(left + 4, 51, right - 4, 51, steelDark);
            canvas.Rect(25, 47, 29, 50, glow);
            canvas.Rect(35, 47, 39, 50, glow);
            canvas.Line(28, 56, 28, 60, steelDark);
            canvas.Line(36, 56, 36, 60, steelDark);
            canvas.Pixel(23, 44, steelLight);
            canvas.Pixel(40, 53, steelLight);
            if (compute)
            {
                canvas.Rect(17, 47, 21, 51, amber);
                canvas.Rect(43, 47, 47, 51, amber);
                canvas.Line(27, 57, 24, 62, glow);
                canvas.Line(37, 57, 40, 62, glow);
                canvas.Pixel(32, 45, amber);
            }

            return canvas;
        }

        if (name.StartsWith("Legs_", StringComparison.Ordinal))
        {
            bool heavy = name.EndsWith("Heavy", StringComparison.Ordinal);
            int width = heavy ? 9 : 7;
            canvas.Rect(23, 13, 32, 27, outline);
            canvas.Rect(33, 13, 42, 27, outline);
            canvas.Rect(24, 14, 24 + width, 25, heavy ? amber : steel);
            canvas.Rect(34, 14, 34 + width, 25, heavy ? amber : steel);
            canvas.Rect(25, 7, 33, 14, outline);
            canvas.Rect(34, 7, 42, 14, outline);
            canvas.Rect(26, 8, 32, 12, steelLight);
            canvas.Rect(35, 8, 41, 12, steelLight);
            canvas.Line(28, 17, 28, 22, glow);
            canvas.Line(38, 17, 38, 22, glow);
            canvas.Rect(29, 25, 35, 28, steelDark);
            if (heavy)
            {
                canvas.Rect(22, 20, 33, 25, amber);
                canvas.Rect(33, 20, 44, 25, amber);
                canvas.Line(27, 4, 33, 4, steelLight);
                canvas.Line(35, 4, 41, 4, steelLight);
            }

            return canvas;
        }

        if (name.StartsWith("Arm_", StringComparison.Ordinal))
        {
            bool leftSide = name.EndsWith("Left", StringComparison.Ordinal);
            bool isMagnet = name.Contains("Magnet", StringComparison.Ordinal);
            bool isCable = name.Contains("Cable", StringComparison.Ordinal);
            bool isPrecision = name.Contains("Precision", StringComparison.Ordinal);
            bool isClaw = name.Contains("Claw", StringComparison.Ordinal);
            Color32 tool = isMagnet ? new Color32(217, 94, 88, 255) : isCable ? amber : isPrecision ? glow : steelLight;
            int shoulderX = leftSide ? 15 : 41;
            int outerX = leftSide ? 4 : 60;
            int wristX = leftSide ? 10 : 54;
            canvas.Ellipse(shoulderX - 5, 27, shoulderX + 5, 37, outline);
            canvas.Ellipse(shoulderX - 3, 29, shoulderX + 3, 35, steel);
            canvas.Line(shoulderX, 31, wristX, 31, outline);
            canvas.Line(shoulderX, 32, wristX, 32, steelLight);
            canvas.Line(wristX, 32, leftSide ? 7 : 57, 22, outline);
            canvas.Line(wristX, 33, leftSide ? 7 : 57, 23, steel);
            canvas.Ellipse((leftSide ? 3 : 53), 18, (leftSide ? 11 : 61), 26, outline);
            canvas.Ellipse((leftSide ? 5 : 55), 20, (leftSide ? 9 : 59), 24, tool);
            if (isMagnet)
            {
                canvas.Line(outerX, 20, leftSide ? 2 : 62, 20, tool);
                canvas.Line(outerX, 18, leftSide ? 2 : 62, 18, steelLight);
            }
            else if (isCable)
            {
                canvas.Line(outerX, 24, leftSide ? 1 : 63, 19, tool);
                canvas.Line(leftSide ? 1 : 63, 19, leftSide ? 3 : 61, 15, steelLight);
                canvas.Pixel(leftSide ? 3 : 61, 15, amber);
            }
            else if (isPrecision)
            {
                canvas.Line(outerX, 21, leftSide ? 2 : 62, 17, tool);
                canvas.Line(outerX, 21, leftSide ? 2 : 62, 25, tool);
                canvas.Pixel(leftSide ? 1 : 63, 21, steelLight);
            }
            else if (isClaw)
            {
                canvas.Line(outerX, 22, leftSide ? 1 : 63, 17, steelLight);
                canvas.Line(outerX, 22, leftSide ? 1 : 63, 27, steelLight);
                canvas.Pixel(leftSide ? 1 : 63, 17, glow);
            }
            else
            {
                canvas.Rect(leftSide ? 4 : 55, 17, leftSide ? 8 : 59, 25, amber);
                canvas.Pixel(leftSide ? 6 : 57, 21, steelDark);
            }

            canvas.Pixel(leftSide ? 20 : 44, 35, steelLight);
            return canvas;
        }

        return canvas;
    }

    /// <summary>Draws a staged industrial storage shed or an expanded right-hand workshop building.</summary>
    private static PixelCanvas DrawFacilityArt(string name)
    {
        PixelCanvas canvas = new PixelCanvas(128, 96);
        bool district = name.StartsWith("District_", StringComparison.Ordinal);
        bool damaged = name.EndsWith("Damaged", StringComparison.Ordinal);
        bool upgraded = name.EndsWith("Upgraded", StringComparison.Ordinal);
        Color32 outline = new Color32(27, 35, 43, 255);
        Color32 wall = damaged ? new Color32(78, 86, 88, 255) : new Color32(100, 125, 133, 255);
        Color32 light = new Color32(175, 190, 181, 255);
        Color32 accent = upgraded ? new Color32(90, 201, 171, 255) : new Color32(226, 165, 66, 255);
        int width = district ? 94 : 75;
        int left = (128 - width) / 2;
        canvas.Polygon(new[] { new Vector2Int(left - 5, 58), new Vector2Int(64, 90), new Vector2Int(left + width + 5, 58), new Vector2Int(left + width, 49), new Vector2Int(left, 49) }, outline);
        canvas.Rect(left, 13, left + width, 59, outline);
        canvas.Rect(left + 3, 16, left + width - 3, 57, wall);
        canvas.Rect(left + 10, 16, left + 14, 54, light);
        canvas.Rect(left + width - 14, 16, left + width - 10, 54, light);
        canvas.Rect(55, 13, 73, 42, outline);
        canvas.Rect(58, 13, 70, 39, damaged ? new Color32(59, 66, 70, 255) : new Color32(42, 59, 67, 255));
        canvas.Line(60, 36, 68, 36, accent);
        canvas.Line(left + 22, 49, left + width - 22, 49, accent);
        canvas.Rect(left + 21, 30, left + 31, 42, outline);
        canvas.Rect(left + 23, 32, left + 29, 40, accent);
        canvas.Rect(left + width - 31, 30, left + width - 21, 42, outline);
        canvas.Rect(left + width - 29, 32, left + width - 23, 40, accent);
        canvas.Line(left - 3, 9, left + width + 3, 9, outline);
        canvas.Line(left + 4, 7, left + width - 4, 7, light);
        if (damaged)
        {
            canvas.Line(left + 13, 58, left + 22, 49, accent);
            canvas.Line(left + width - 20, 58, left + width - 9, 51, outline);
            canvas.Pixel(left + 38, 21, new Color32(197, 87, 69, 255));
        }
        else if (upgraded)
        {
            canvas.Rect(15, 62, 27, 77, outline);
            canvas.Rect(17, 64, 25, 75, accent);
            canvas.Rect(101, 62, 113, 77, outline);
            canvas.Rect(103, 64, 111, 75, accent);
            canvas.Line(18, 79, 110, 79, accent);
        }

        return canvas;
    }

    /// <summary>Draws a resource-specific supply source or a larger removable salvage pile.</summary>
    private static PixelCanvas DrawResourceArt(string resourceName, bool bonus)
    {
        PixelCanvas canvas = new PixelCanvas(64, 64);
        Color32 outline = new Color32(29, 33, 38, 255);
        Color32 main = resourceName == "Iron" ? new Color32(126, 137, 140, 255) : resourceName == "Copper" ? new Color32(186, 106, 60, 255) : new Color32(65, 134, 154, 255);
        Color32 shine = resourceName == "Iron" ? new Color32(204, 212, 207, 255) : resourceName == "Copper" ? new Color32(242, 169, 94, 255) : new Color32(111, 225, 222, 255);
        int baseY = bonus ? 6 : 4;
        canvas.Polygon(new[] { new Vector2Int(7, baseY + 8), new Vector2Int(14, baseY + 1), new Vector2Int(48, baseY + 1), new Vector2Int(57, baseY + 9), new Vector2Int(52, baseY + 24), new Vector2Int(12, baseY + 24) }, outline);
        canvas.Polygon(new[] { new Vector2Int(10, baseY + 9), new Vector2Int(16, baseY + 4), new Vector2Int(46, baseY + 4), new Vector2Int(53, baseY + 10), new Vector2Int(49, baseY + 21), new Vector2Int(14, baseY + 21) }, main);
        canvas.Line(17, baseY + 14, 24, baseY + 7, shine);
        canvas.Line(24, baseY + 7, 31, baseY + 14, outline);
        canvas.Line(36, baseY + 18, 44, baseY + 10, shine);
        canvas.Line(44, baseY + 10, 50, baseY + 16, outline);

        if (resourceName == "Electronics")
        {
            canvas.Rect(20, baseY + 27, 44, baseY + 42, outline);
            canvas.Rect(22, baseY + 29, 42, baseY + 40, main);
            canvas.Rect(27, baseY + 32, 37, baseY + 37, shine);
            canvas.Line(24, baseY + 27, 24, baseY + 24, shine);
            canvas.Line(40, baseY + 42, 40, baseY + 45, shine);
        }
        else if (resourceName == "Copper")
        {
            canvas.Line(20, baseY + 24, 20, baseY + 34, shine);
            canvas.Line(20, baseY + 34, 29, baseY + 39, shine);
            canvas.Line(29, baseY + 39, 38, baseY + 34, shine);
            canvas.Line(38, baseY + 34, 38, baseY + 25, shine);
            canvas.Pixel(45, baseY + 32, new Color32(255, 212, 133, 255));
        }
        else
        {
            canvas.Rect(22, baseY + 26, 42, baseY + 31, outline);
            canvas.Line(23, baseY + 28, 41, baseY + 28, shine);
            canvas.Pixel(34, baseY + 33, new Color32(228, 194, 111, 255));
        }

        canvas.Line(8, 1, 55, 1, new Color32(69, 78, 83, 255));
        return canvas;
    }

    /// <summary>Draws a compact 32-pixel resource icon with distinct iron, copper, or circuit details.</summary>
    private static PixelCanvas DrawResourceIcon(string resourceName)
    {
        PixelCanvas canvas = new PixelCanvas(32, 32);
        Color32[] compact = canvas.ToPixels();
        Color32 dark = new Color32(26, 34, 42, 255);
        Color32 metal = resourceName == "Iron" ? new Color32(150, 164, 164, 255) : resourceName == "Copper" ? new Color32(201, 119, 68, 255) : new Color32(54, 143, 165, 255);
        Color32 bright = resourceName == "Iron" ? new Color32(222, 226, 219, 255) : resourceName == "Copper" ? new Color32(250, 189, 108, 255) : new Color32(110, 235, 220, 255);
        for (int y = 5; y < 27; y++)
        {
            for (int x = 5; x < 27; x++)
            {
                compact[y * 32 + x] = dark;
            }
        }

        canvas.SetPixels(compact);
        canvas.Rect(6, 6, 25, 23, metal);
        canvas.Line(8, 21, 14, 12, bright);
        canvas.Line(14, 12, 19, 17, dark);
        canvas.Line(17, 9, 23, 15, bright);
        canvas.Pixel(22, 21, resourceName == "Electronics" ? bright : new Color32(245, 222, 172, 255));
        return canvas;
    }

    /// <summary>Draws a repeating riveted 64 by 32 steel floor tile.</summary>
    private static PixelCanvas DrawGroundArt()
    {
        PixelCanvas canvas = new PixelCanvas(64, 32);
        Color32 baseColor = new Color32(54, 69, 77, 255);
        Color32 shadow = new Color32(34, 46, 54, 255);
        Color32 line = new Color32(91, 111, 115, 255);
        Color32 bolt = new Color32(142, 153, 144, 255);
        canvas.Polygon(new[] { new Vector2Int(0, 16), new Vector2Int(32, 31), new Vector2Int(63, 16), new Vector2Int(32, 0) }, shadow);
        canvas.Polygon(new[] { new Vector2Int(2, 16), new Vector2Int(32, 29), new Vector2Int(61, 16), new Vector2Int(32, 2) }, baseColor);
        canvas.Line(3, 16, 32, 28, line);
        canvas.Line(32, 28, 60, 16, line);
        canvas.Line(32, 3, 32, 28, new Color32(43, 57, 63, 255));
        canvas.Pixel(8, 16, bolt);
        canvas.Pixel(32, 27, bolt);
        canvas.Pixel(55, 16, bolt);
        return canvas;
    }

    /// <summary>Draws a transparent outlined selection ring for the robot's ground anchor.</summary>
    private static PixelCanvas DrawSelectionRing()
    {
        PixelCanvas canvas = new PixelCanvas(64, 64);
        Color32 ring = new Color32(91, 219, 202, 210);
        Color32 edge = new Color32(24, 90, 92, 255);
        for (int y = 8; y < 27; y++)
        {
            for (int x = 7; x < 57; x++)
            {
                float dx = (x - 32f) / 25f;
                float dy = (y - 17f) / 9f;
                float radius = dx * dx + dy * dy;
                if (radius >= 0.8f && radius <= 1.08f)
                {
                    canvas.Pixel(x, y, radius > 1f ? edge : ring);
                }
            }
        }

        return canvas;
    }

    /// <summary>Draws the recycled-material currency icon used by the HUD.</summary>
    private static PixelCanvas DrawScrapIcon()
    {
        PixelCanvas canvas = new PixelCanvas(32, 32);
        Color32 dark = new Color32(36, 41, 46, 255);
        Color32 gold = new Color32(228, 174, 68, 255);
        Color32 light = new Color32(255, 224, 145, 255);
        canvas.Polygon(new[] { new Vector2Int(16, 3), new Vector2Int(26, 10), new Vector2Int(23, 24), new Vector2Int(14, 29), new Vector2Int(5, 21), new Vector2Int(7, 9) }, dark);
        canvas.Polygon(new[] { new Vector2Int(16, 6), new Vector2Int(23, 11), new Vector2Int(20, 22), new Vector2Int(14, 25), new Vector2Int(8, 20), new Vector2Int(10, 11) }, gold);
        canvas.Line(11, 18, 18, 10, light);
        canvas.Line(18, 10, 21, 16, dark);
        canvas.Pixel(15, 20, light);
        return canvas;
    }

    /// <summary>Writes a sprite PNG only when the source image does not already exist.</summary>
    private static void WriteSpriteIfMissing(string assetPath, PixelCanvas canvas, bool useCenterPivot)
    {
        if (File.Exists(assetPath))
        {
            return;
        }

        Texture2D texture = new Texture2D(canvas.Width, canvas.Height, TextureFormat.RGBA32, false, true);
        texture.SetPixels32(canvas.ToPixels());
        texture.Apply(false, false);
        File.WriteAllBytes(Path.GetFullPath(assetPath), texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = SPRITE_PIXELS_PER_UNIT;
            importer.spritePivot = useCenterPivot ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 0f);
            importer.mipmapEnabled = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = useCenterPivot ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 0f);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }

    /// <summary>Loads each generated image as a sprite keyed by its stable art filename.</summary>
    private static Dictionary<string, Sprite> LoadSprites()
    {
        Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        string[] paths = AssetDatabase.FindAssets("t:Texture2D", new[] { ART });
        for (int i = 0; i < paths.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(paths[i]);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
            {
                sprites[Path.GetFileNameWithoutExtension(path)] = sprite;
            }
        }

        return sprites;
    }

    /// <summary>Creates resource, grade, part, facility, gacha, bonus, and game config assets when absent.</summary>
    private static DefinitionSet CreateDefinitions(Dictionary<string, Sprite> sprites)
    {
        DefinitionSet set = new DefinitionSet();
        set.Iron = CreateDefinition("Resource_Iron.asset", "iron", ResourceKind.Iron, "철", 1f, 1, 1, 3f, sprites);
        set.Copper = CreateDefinition("Resource_Copper.asset", "copper", ResourceKind.Copper, "구리", 1f, 2, 1, 5f, sprites);
        set.Electronics = CreateDefinition("Resource_Electronics.asset", "electronics", ResourceKind.Electronics, "전자부품", 1f, 4, 1, 8f, sprites);
        set.GradeTable = CreateGradeTable();

        set.UniversalCore = CreatePart("Part_Core_Universal.asset", "core_universal", RobotPartSlot.Core, "구형 범용 코어", Stats(0, 2, 0, 5, 0, 0), Stats(0.25f, 2, 0.03f, 2, 0, 0), null, false, Sprite(sprites, "Core_Universal"));
        set.IndustrialCore = CreatePart("Part_Core_Industrial.asset", "core_industrial", RobotPartSlot.Core, "산업형 코어", Stats(0, 4, 0, 10, 0, 0), Stats(0.1f, 1.5f, 0.02f, 3, 0, 0), null, false, Sprite(sprites, "Core_Industrial"));
        set.BasicHead = CreatePart("Part_Head_Basic.asset", "head_basic", RobotPartSlot.Head, "기본 머리", Stats(10, 0, 0, 0, 0, 0), default, null, false, Sprite(sprites, "Head_Basic"));
        set.ComputeHead = CreatePart("Part_Head_Compute.asset", "head_compute", RobotPartSlot.Head, "연산형 머리", Stats(25, 0, 0, 0, 0, 0), default, null, false, Sprite(sprites, "Head_Compute"));
        set.BasicLegs = CreatePart("Part_Legs_Basic.asset", "legs_basic", RobotPartSlot.Legs, "기본 다리", Stats(0, 1, 1.5f, 15, 12, 0), default, null, false, Sprite(sprites, "Legs_Basic"));
        set.HeavyLegs = CreatePart("Part_Legs_Heavy.asset", "legs_heavy", RobotPartSlot.Legs, "중량형 다리", Stats(0, 3, 0.95f, 22, 20, 0), default, null, false, Sprite(sprites, "Legs_Heavy"));
        set.MultiTool = CreatePart("Part_Arm_MultiTool.asset", "arm_multitool", RobotPartSlot.Arms, "멀티툴", Stats(0, 1, 0, 0, 0, 0), default,
            new List<ResourceEfficiencyEntry> { new ResourceEfficiencyEntry(ResourceKind.Iron, 0.8f), new ResourceEfficiencyEntry(ResourceKind.Copper, 0.55f), new ResourceEfficiencyEntry(ResourceKind.Electronics, 0.35f) }, false,
            Sprite(sprites, "Arm_MultiTool_Left"), Sprite(sprites, "Arm_MultiTool_Left"), Sprite(sprites, "Arm_MultiTool_Right"));
        set.Magnet = CreatePart("Part_Arm_Magnet.asset", "arm_magnet", RobotPartSlot.Arms, "자석 팔", default,
            default, new List<ResourceEfficiencyEntry> { new ResourceEfficiencyEntry(ResourceKind.Iron, 1.2f) }, false,
            Sprite(sprites, "Arm_Magnet_Left"), Sprite(sprites, "Arm_Magnet_Left"), Sprite(sprites, "Arm_Magnet_Right"));
        set.Cable = CreatePart("Part_Arm_Cable.asset", "arm_cable", RobotPartSlot.Arms, "케이블 해체 팔", default,
            default, new List<ResourceEfficiencyEntry> { new ResourceEfficiencyEntry(ResourceKind.Copper, 1.2f) }, true,
            Sprite(sprites, "Arm_Cable_Left"), Sprite(sprites, "Arm_Cable_Left"), Sprite(sprites, "Arm_Cable_Right"));
        set.Precision = CreatePart("Part_Arm_Precision.asset", "arm_precision", RobotPartSlot.Arms, "정밀 분해 팔", default,
            default, new List<ResourceEfficiencyEntry> { new ResourceEfficiencyEntry(ResourceKind.Electronics, 1.2f) }, true,
            Sprite(sprites, "Arm_Precision_Left"), Sprite(sprites, "Arm_Precision_Left"), Sprite(sprites, "Arm_Precision_Right"));
        set.Claw = CreatePart("Part_Arm_Claw.asset", "arm_claw", RobotPartSlot.Arms, "범용 집게", Stats(0, 1, 0, 0, 0, 2), default,
            null, false, Sprite(sprites, "Arm_Claw_Left"), Sprite(sprites, "Arm_Claw_Left"), Sprite(sprites, "Arm_Claw_Right"));

        set.StorageFacility = CreateFacility("Facility_StorageBin.asset", "storage_bin", "미환전 자원함 확장", 100, 600f, 0, false, sprites, "Storage");
        set.ExpansionFacility = CreateFacility("Facility_EastExpansion.asset", "east_expansion", "오른쪽 작업 구역 증축", 150, 0f, 1, true, sprites, "District");
        set.Gacha = CreateGacha(set.BasicHead, set.BasicLegs, set.MultiTool, set.Claw);
        set.Bonus = CreateBonus(set.Iron, set.Copper, set.Electronics);
        set.Game = CreateGameConfig(set);
        return set;
    }

    /// <summary>Creates a missing resource definition with saved economy tuning and its art references.</summary>
    private static ResourceDefinition CreateDefinition(string fileName, string id, ResourceKind kind, string displayName, float unitWeight, int value, int experience, float workSeconds, Dictionary<string, Sprite> sprites)
    {
        string path = $"{DATA}/{fileName}";
        ResourceDefinition asset = AssetDatabase.LoadAssetAtPath<ResourceDefinition>(path);
        if (asset != null)
        {
            return asset;
        }

        asset = ScriptableObject.CreateInstance<ResourceDefinition>();
        Set(asset, "_id", id);
        Set(asset, "_kind", kind);
        Set(asset, "_displayName", displayName);
        Set(asset, "_unitWeight", unitWeight);
        Set(asset, "_scrapValue", value);
        Set(asset, "_experiencePerUnit", experience);
        Set(asset, "_baseWorkSeconds", workSeconds);
        Set(asset, "_quantityPerGather", 1);
        Set(asset, "_worldSprite", Sprite(sprites, $"Source_{kind}"));
        Set(asset, "_bonusSprite", Sprite(sprites, $"Bonus_{kind}"));
        Set(asset, "_icon", Sprite(sprites, $"Icon_{kind}"));
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    /// <summary>Creates the seven-grade weight and stat multiplier table specified by the PRD.</summary>
    private static GradeTable CreateGradeTable()
    {
        string path = $"{DATA}/GradeTable.asset";
        GradeTable asset = AssetDatabase.LoadAssetAtPath<GradeTable>(path);
        if (asset != null)
        {
            return asset;
        }

        List<WorkshopGradeEntry> grades = new List<WorkshopGradeEntry>
        {
            new WorkshopGradeEntry("F", "F", 1f, 40f, new Color32(188, 199, 195, 255)),
            new WorkshopGradeEntry("E", "E", 1.15f, 25f, new Color32(139, 202, 157, 255)),
            new WorkshopGradeEntry("D", "D", 1.35f, 18f, new Color32(111, 174, 225, 255)),
            new WorkshopGradeEntry("C", "C", 1.6f, 10f, new Color32(183, 130, 226, 255)),
            new WorkshopGradeEntry("B", "B", 2f, 5f, new Color32(234, 177, 87, 255)),
            new WorkshopGradeEntry("A", "A", 2.5f, 1.5f, new Color32(241, 124, 102, 255)),
            new WorkshopGradeEntry("S", "S", 3.2f, 0.5f, new Color32(255, 231, 144, 255))
        };
        asset = ScriptableObject.CreateInstance<GradeTable>();
        Set(asset, "_grades", grades);
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    /// <summary>Creates a missing part definition and binds its stable IDs, stats, efficiency, and PNG sprites.</summary>
    private static PartDefinition CreatePart(string fileName, string id, RobotPartSlot slot, string displayName, WorkshopStatBlock stats, WorkshopStatBlock growth, List<ResourceEfficiencyEntry> efficiencies, bool requiresExpansion, Sprite icon, Sprite left = null, Sprite right = null)
    {
        string path = $"{DATA}/{fileName}";
        PartDefinition asset = AssetDatabase.LoadAssetAtPath<PartDefinition>(path);
        if (asset != null)
        {
            return asset;
        }

        asset = ScriptableObject.CreateInstance<PartDefinition>();
        Set(asset, "_id", id);
        Set(asset, "_slot", slot);
        Set(asset, "_displayName", displayName);
        Set(asset, "_baseStats", stats);
        Set(asset, "_coreLevelGrowth", growth);
        Set(asset, "_resourceEfficiencies", efficiencies ?? new List<ResourceEfficiencyEntry>());
        Set(asset, "_drawWeight", 1f);
        Set(asset, "_requiresExpansion", requiresExpansion);
        Set(asset, "_icon", icon);
        Set(asset, "_leftSprite", left == null ? icon : left);
        Set(asset, "_rightSprite", right == null ? (left == null ? icon : left) : right);
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    /// <summary>Creates one facility asset with price, unlock effect, and three matching visual stages.</summary>
    private static FacilityDefinition CreateFacility(string fileName, string id, string displayName, int cost, float storageIncrease, int robotIncrease, bool unlocksDistrict, Dictionary<string, Sprite> sprites, string artPrefix)
    {
        string path = $"{DATA}/{fileName}";
        FacilityDefinition asset = AssetDatabase.LoadAssetAtPath<FacilityDefinition>(path);
        if (asset != null)
        {
            return asset;
        }

        asset = ScriptableObject.CreateInstance<FacilityDefinition>();
        Set(asset, "_id", id);
        Set(asset, "_displayName", displayName);
        Set(asset, "_cost", cost);
        Set(asset, "_storageCapacityIncrease", storageIncrease);
        Set(asset, "_activeRobotIncrease", robotIncrease);
        Set(asset, "_unlocksDistrict", unlocksDistrict);
        Set(asset, "_damagedSprite", Sprite(sprites, $"{artPrefix}_Damaged"));
        Set(asset, "_restoredSprite", Sprite(sprites, $"{artPrefix}_Restored"));
        Set(asset, "_upgradedSprite", Sprite(sprites, $"{artPrefix}_Upgraded"));
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    /// <summary>Creates the four single-draw prices and the F-grade starter-kit definition references.</summary>
    private static GachaConfig CreateGacha(PartDefinition head, PartDefinition legs, PartDefinition multiTool, PartDefinition claw)
    {
        string path = $"{DATA}/GachaConfig.asset";
        GachaConfig asset = AssetDatabase.LoadAssetAtPath<GachaConfig>(path);
        if (asset != null)
        {
            return asset;
        }

        asset = ScriptableObject.CreateInstance<GachaConfig>();
        Set(asset, "_slotCosts", new List<GachaSlotCost>
        {
            new GachaSlotCost(RobotPartSlot.Core, 20),
            new GachaSlotCost(RobotPartSlot.Head, 20),
            new GachaSlotCost(RobotPartSlot.Legs, 20),
            new GachaSlotCost(RobotPartSlot.Arms, 20)
        });
        Set(asset, "_starterKitCost", 40);
        Set(asset, "_starterHead", head);
        Set(asset, "_starterLegs", legs);
        Set(asset, "_starterMultiTool", multiTool);
        Set(asset, "_starterClaw", claw);
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    /// <summary>Creates the configured bonus intervals, two-pile limit, work multiplier, and resource quantities.</summary>
    private static BonusSpawnConfig CreateBonus(ResourceDefinition iron, ResourceDefinition copper, ResourceDefinition electronics)
    {
        string path = $"{DATA}/BonusSpawnConfig.asset";
        BonusSpawnConfig asset = AssetDatabase.LoadAssetAtPath<BonusSpawnConfig>(path);
        if (asset != null)
        {
            return asset;
        }

        asset = ScriptableObject.CreateInstance<BonusSpawnConfig>();
        Set(asset, "_minimumIntervalSeconds", 180f);
        Set(asset, "_maximumIntervalSeconds", 300f);
        Set(asset, "_maximumWaitingBonuses", 2);
        Set(asset, "_workTimeMultiplier", 0.8f);
        Set(asset, "_minimumWorldPosition", new Vector2(0f, 0f));
        Set(asset, "_maximumWorldPosition", new Vector2(7f, 0f));
        Set(asset, "_resourcePool", new List<BonusSpawnEntry>
        {
            new BonusSpawnEntry(iron, 1f, 20, 30),
            new BonusSpawnEntry(copper, 1f, 20, 30),
            new BonusSpawnEntry(electronics, 1f, 20, 30)
        });
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    /// <summary>Creates the game-wide starting references and every balance value as inspector-editable serialized data.</summary>
    private static WorkshopGameConfig CreateGameConfig(DefinitionSet definitions)
    {
        string path = $"{DATA}/WorkshopGameConfig.asset";
        WorkshopGameConfig asset = AssetDatabase.LoadAssetAtPath<WorkshopGameConfig>(path);
        if (asset != null)
        {
            return asset;
        }

        asset = ScriptableObject.CreateInstance<WorkshopGameConfig>();
        Set(asset, "_startingCore", definitions.UniversalCore);
        Set(asset, "_startingHead", definitions.BasicHead);
        Set(asset, "_startingLegs", definitions.BasicLegs);
        Set(asset, "_startingMultiTool", definitions.MultiTool);
        Set(asset, "_startingClaw", definitions.Claw);
        Set(asset, "_gradeTable", definitions.GradeTable);
        Set(asset, "_partDefinitions", new List<PartDefinition>
        {
            definitions.UniversalCore, definitions.IndustrialCore, definitions.BasicHead, definitions.ComputeHead,
            definitions.BasicLegs, definitions.HeavyLegs, definitions.MultiTool, definitions.Magnet,
            definitions.Cable, definitions.Precision, definitions.Claw
        });
        Set(asset, "_resourceDefinitions", new List<ResourceDefinition> { definitions.Iron, definitions.Copper, definitions.Electronics });
        Set(asset, "_startingScrap", 0);
        Set(asset, "_baseCargoCapacity", 8f);
        Set(asset, "_initialStorageCapacity", 600f);
        Set(asset, "_startingPrimaryResource", ResourceKind.Iron);
        Set(asset, "_baseOperatingSeconds", 60f);
        Set(asset, "_durabilitySecondsPerPoint", 6f);
        Set(asset, "_restSeconds", 30f);
        Set(asset, "_strengthSpeedFactor", 0.02f);
        Set(asset, "_minimumGatherSeconds", 0.25f);
        Set(asset, "_minimumMoveSpeed", 0.1f);
        Set(asset, "_midIntelligenceThreshold", 20f);
        Set(asset, "_highIntelligenceThreshold", 40f);
        Set(asset, "_levelOneExperience", 20);
        Set(asset, "_experienceIncreasePerLevel", 10);
        Set(asset, "_autoSaveSeconds", 30f);
        Set(asset, "_firstRobotName", "UNIT-01");
        Set(asset, "_cameraScrollDistance", 8f);
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    /// <summary>Creates the layered robot prefab with separate core, head, leg, and arm SpriteRenderers.</summary>
    private static WorkshopRobotView CreateRobotPrefab(DefinitionSet definitions, Dictionary<string, Sprite> sprites)
    {
        string path = $"{PREFABS}/Robot.prefab";
        WorkshopRobotView existing = AssetDatabase.LoadAssetAtPath<WorkshopRobotView>(path);
        if (existing != null)
        {
            return existing;
        }

        GameObject root = new GameObject("Robot", typeof(WorkshopRobotView));
        Transform visualRoot = CreateChild(root.transform, "VisualRoot");
        SpriteRenderer legs = CreateRenderer(visualRoot, "Legs", Sprite(sprites, "Legs_Basic"), 0);
        SpriteRenderer backArm = CreateRenderer(visualRoot, "BackArm", Sprite(sprites, "Arm_MultiTool_Right"), 1);
        SpriteRenderer core = CreateRenderer(visualRoot, "Core", Sprite(sprites, "Core_Universal"), 2);
        SpriteRenderer head = CreateRenderer(visualRoot, "Head", Sprite(sprites, "Head_Basic"), 3);
        SpriteRenderer frontArm = CreateRenderer(visualRoot, "FrontArm", Sprite(sprites, "Arm_Claw_Left"), 4);
        SpriteRenderer selection = CreateRenderer(root.transform, "SelectionRing", Sprite(sprites, "Selection_Ring"), -1);
        selection.transform.localScale = new Vector3(1.1f, 0.35f, 1f);
        selection.enabled = false;

        GameObject labelObject = new GameObject("IdentityLabel");
        labelObject.transform.SetParent(root.transform, false);
        labelObject.transform.localPosition = new Vector3(0f, 2.05f, 0f);
        TextMesh label = labelObject.AddComponent<TextMesh>();
        label.text = "◆ UNIT-01";
        label.anchor = TextAnchor.LowerCenter;
        label.alignment = TextAlignment.Center;
        label.characterSize = 0.08f;
        label.fontSize = 42;
        label.color = Color.white;

        WorkshopRobotView view = root.GetComponent<WorkshopRobotView>();
        Set(view, "_visualRoot", visualRoot);
        Set(view, "_legsRenderer", legs);
        Set(view, "_backArmRenderer", backArm);
        Set(view, "_coreRenderer", core);
        Set(view, "_headRenderer", head);
        Set(view, "_frontArmRenderer", frontArm);
        Set(view, "_identityLabel", label);
        Set(view, "_selectionRing", selection);
        WorkshopRobotView prefab = PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<WorkshopRobotView>();
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    /// <summary>Creates the single play-ready workshop scene, source anchors, world facilities, camera, canvas, and runtime links.</summary>
    private static void CreateStartingScene(DefinitionSet definitions, WorkshopRobotView robotPrefab, Dictionary<string, Sprite> sprites)
    {
        if (File.Exists(SCENE_PATH))
        {
            return;
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = WORLD_REFERENCE_HEIGHT / (2f * SPRITE_PIXELS_PER_UNIT);
        camera.allowHDR = false;
        camera.allowMSAA = false;
        camera.allowDynamicResolution = false;
        camera.backgroundColor = new Color(0.035f, 0.065f, 0.085f, 1f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        UnityEngine.Rendering.Universal.PixelPerfectCamera pixelCamera = cameraObject.AddComponent<UnityEngine.Rendering.Universal.PixelPerfectCamera>();
        pixelCamera.assetsPPU = SPRITE_PIXELS_PER_UNIT;
        pixelCamera.refResolutionX = WORLD_REFERENCE_WIDTH;
        pixelCamera.refResolutionY = WORLD_REFERENCE_HEIGHT;
        pixelCamera.gridSnapping = UnityEngine.Rendering.Universal.PixelPerfectCamera.GridSnapping.PixelSnapping;
        pixelCamera.cropFrame = UnityEngine.Rendering.Universal.PixelPerfectCamera.CropFrame.None;

        Transform worldRoot = new GameObject("WorkshopWorld").transform;
        CreateGround(worldRoot, sprites);
        Transform service = CreateAnchor(worldRoot, "ServiceBay", new Vector3(1.5f, 0f, 0f));
        CreateLabeledBuilding(worldRoot, "RepairStation", new Vector3(1.5f, 0f, 0f), Sprite(sprites, "District_Restored"), "정비소 · 입고 / 조립");
        SpriteRenderer storageRenderer = CreateFacility(worldRoot, "StorageFacility", new Vector3(5f, 0f, 0f), definitions.StorageFacility.DamagedSprite, "자원함");
        SpriteRenderer expansionRenderer = CreateFacility(worldRoot, "EastExpansion", new Vector3(14f, 0f, 0f), definitions.ExpansionFacility.DamagedSprite, "확장 구역");
        GameObject lockedSign = CreateLabeledBuilding(worldRoot, "LockedZoneSign", new Vector3(17f, 0f, 0f), Sprite(sprites, "District_Damaged"), "잠금 · 오른쪽 작업 구역 증축");
        lockedSign.name = "LockedZoneSign";
        lockedSign.transform.Find("Label").localPosition = new Vector3(0f, 4.2f, 0f);

        List<WorkshopResourceSource> sources = new List<WorkshopResourceSource>
        {
            CreateSource(worldRoot, "Source_Iron", "iron_node", definitions.Iron, new Vector3(-6f, 0f, 0f), 60, 60, 1.5f, 1, false),
            CreateSource(worldRoot, "Source_Copper", "copper_node", definitions.Copper, new Vector3(12f, 0f, 0f), 20, 20, 5f, 1, true),
            CreateSource(worldRoot, "Source_Electronics", "electronics_node", definitions.Electronics, new Vector3(20f, 0f, 0f), 10, 10, 20f, 1, true)
        };

        Transform robotRoot = new GameObject("RobotActors").transform;
        robotRoot.SetParent(worldRoot, false);
        Transform bonusRoot = new GameObject("BonusDebris").transform;
        bonusRoot.SetParent(worldRoot, false);
        GameObject runtimeObject = new GameObject("WorkshopRuntime", typeof(WorkshopRuntime));
        WorkshopRuntime runtime = runtimeObject.GetComponent<WorkshopRuntime>();
        Set(runtime, "_gameConfig", definitions.Game);
        Set(runtime, "_gachaConfig", definitions.Gacha);
        Set(runtime, "_bonusSpawnConfig", definitions.Bonus);
        Set(runtime, "_storageFacility", definitions.StorageFacility);
        Set(runtime, "_expansionFacility", definitions.ExpansionFacility);
        Set(runtime, "_resourceSources", sources);
        Set(runtime, "_servicePoint", service);
        Set(runtime, "_robotPrefab", robotPrefab);
        Set(runtime, "_robotParent", robotRoot);
        Set(runtime, "_bonusParent", bonusRoot);
        Set(runtime, "_worldCamera", camera);
        Set(runtime, "_storageRenderer", storageRenderer);
        Set(runtime, "_expansionRenderer", expansionRenderer);
        Set(runtime, "_lockedZoneSign", lockedSign);
        Set(runtime, "_minimumCameraX", -8f);
        Set(runtime, "_maximumCameraX", 8f);

        GameObject canvasObject = new GameObject("WorkshopCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster), typeof(WorkshopUI));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        UnityEngine.UI.CanvasScaler scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        Set(canvasObject.GetComponent<WorkshopUI>(), "_canvas", canvas);
        Set(canvasObject.GetComponent<WorkshopUI>(), "_currencyIcon", Sprite(sprites, "Icon_Scrap"));
        Set(runtime, "_workshopUI", canvasObject.GetComponent<WorkshopUI>());

        GameObject eventSystem = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        eventSystem.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().AssignDefaultActions();

        EditorSceneManager.SaveScene(scene, SCENE_PATH);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(SCENE_PATH, true) };
        AssetDatabase.Refresh();
    }

    /// <summary>Creates an anchored resource source object with its stock, refill, and world sprite settings.</summary>
    private static WorkshopResourceSource CreateSource(Transform parent, string name, string sourceId, ResourceDefinition resource, Vector3 position, int initial, int maximum, float refillSeconds, int refillQuantity, bool requiresExpansion)
    {
        GameObject sourceObject = new GameObject(name);
        sourceObject.transform.SetParent(parent, false);
        sourceObject.transform.position = position;
        SpriteRenderer renderer = sourceObject.AddComponent<SpriteRenderer>();
        renderer.sprite = resource.WorldSprite;
        renderer.sortingOrder = 2;

        GameObject quantityObject = new GameObject("StockLabel");
        quantityObject.transform.SetParent(sourceObject.transform, false);
        quantityObject.transform.localPosition = new Vector3(0f, 1.55f, 0f);
        TextMesh quantity = quantityObject.AddComponent<TextMesh>();
        quantity.text = initial.ToString();
        quantity.anchor = TextAnchor.MiddleCenter;
        quantity.alignment = TextAlignment.Center;
        quantity.fontSize = 34;
        quantity.characterSize = 0.1f;
        quantity.color = Color.white;

        WorkshopResourceSource source = sourceObject.AddComponent<WorkshopResourceSource>();
        Set(source, "_sourceId", sourceId);
        Set(source, "_resource", resource);
        Set(source, "_initialQuantity", initial);
        Set(source, "_maximumQuantity", maximum);
        Set(source, "_refillIntervalSeconds", refillSeconds);
        Set(source, "_refillQuantity", refillQuantity);
        Set(source, "_requiresExpansion", requiresExpansion);
        Set(source, "_spriteRenderer", renderer);
        Set(source, "_quantityLabel", quantity);
        return source;
    }

    /// <summary>Creates repeating floor sprites along the workroom and expansion camera range.</summary>
    private static void CreateGround(Transform parent, Dictionary<string, Sprite> sprites)
    {
        Sprite tile = Sprite(sprites, "Ground_Tile");
        for (int x = -8; x <= 27; x++)
        {
            GameObject tileObject = new GameObject($"FloorTile_{x}");
            tileObject.transform.SetParent(parent, false);
            tileObject.transform.position = new Vector3(x * 2f, -0.05f, 0f);
            SpriteRenderer renderer = tileObject.AddComponent<SpriteRenderer>();
            renderer.sprite = tile;
            renderer.sortingOrder = -10;
        }
    }

    /// <summary>Creates a static facility sprite and its label under the common world root.</summary>
    private static SpriteRenderer CreateFacility(Transform parent, string name, Vector3 position, Sprite sprite, string label)
    {
        GameObject building = new GameObject(name);
        building.transform.SetParent(parent, false);
        building.transform.position = position;
        SpriteRenderer renderer = building.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = -1;
        CreateWorldLabel(building.transform, label, new Vector3(0f, 3.05f, 0f), 0.15f);
        return renderer;
    }

    /// <summary>Creates a facility-sized illustration used for a building label and the service bay marker.</summary>
    private static GameObject CreateLabeledBuilding(Transform parent, string name, Vector3 position, Sprite sprite, string label)
    {
        GameObject building = new GameObject(name);
        building.transform.SetParent(parent, false);
        building.transform.position = position;
        SpriteRenderer renderer = building.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = -2;
        CreateWorldLabel(building.transform, label, new Vector3(0f, 3.05f, 0f), 0.15f);
        return building;
    }

    /// <summary>Creates a world-space sign with a dynamic operating-system font.</summary>
    private static TextMesh CreateWorldLabel(Transform parent, string value, Vector3 localPosition, float characterSize)
    {
        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(parent, false);
        labelObject.transform.localPosition = localPosition;
        TextMesh label = labelObject.AddComponent<TextMesh>();
        label.text = value;
        label.anchor = TextAnchor.LowerCenter;
        label.alignment = TextAlignment.Center;
        label.fontSize = 34;
        label.characterSize = characterSize;
        return label;
    }

    /// <summary>Creates a logical service point transform consumed by robot return and assembly checks.</summary>
    private static Transform CreateAnchor(Transform parent, string name, Vector3 position)
    {
        Transform anchor = new GameObject(name).transform;
        anchor.SetParent(parent, false);
        anchor.position = position;
        return anchor;
    }

    /// <summary>Creates a child transform for an independently ordered sprite layer.</summary>
    private static Transform CreateChild(Transform parent, string name)
    {
        Transform child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }

    /// <summary>Creates a sprite renderer child with the requested part image and sorting order.</summary>
    private static SpriteRenderer CreateRenderer(Transform parent, string name, Sprite sprite, int sortingOrder)
    {
        GameObject rendererObject = new GameObject(name);
        rendererObject.transform.SetParent(parent, false);
        SpriteRenderer renderer = rendererObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    /// <summary>Creates a populated stat value while respecting the runtime struct's private serialized fields.</summary>
    private static WorkshopStatBlock Stats(float intelligence, float strength, float moveSpeed, float durability, float supportWeight, float cargoBonus)
    {
        object boxedStats = default(WorkshopStatBlock);
        Set(boxedStats, "_intelligence", intelligence);
        Set(boxedStats, "_strength", strength);
        Set(boxedStats, "_moveSpeed", moveSpeed);
        Set(boxedStats, "_durability", durability);
        Set(boxedStats, "_supportWeight", supportWeight);
        Set(boxedStats, "_cargoBonus", cargoBonus);
        return (WorkshopStatBlock)boxedStats;
    }

    /// <summary>Assigns a private serialized field in an editor-owned ScriptableObject or scene component.</summary>
    private static void Set(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
        {
            throw new MissingFieldException(target.GetType().Name, fieldName);
        }

        field.SetValue(target, value);
        if (target is UnityEngine.Object unityObject)
        {
            EditorUtility.SetDirty(unityObject);
        }
    }

    /// <summary>Returns a required loaded sprite and reports missing generated PNG references immediately.</summary>
    private static Sprite Sprite(Dictionary<string, Sprite> sprites, string name)
    {
        if (sprites.TryGetValue(name, out Sprite sprite))
        {
            return sprite;
        }

        throw new FileNotFoundException($"Generated sprite '{name}' was not imported under {ART}.");
    }

    private sealed class DefinitionSet
    {
        public ResourceDefinition Iron { get; set; }
        public ResourceDefinition Copper { get; set; }
        public ResourceDefinition Electronics { get; set; }
        public GradeTable GradeTable { get; set; }
        public PartDefinition UniversalCore { get; set; }
        public PartDefinition IndustrialCore { get; set; }
        public PartDefinition BasicHead { get; set; }
        public PartDefinition ComputeHead { get; set; }
        public PartDefinition BasicLegs { get; set; }
        public PartDefinition HeavyLegs { get; set; }
        public PartDefinition MultiTool { get; set; }
        public PartDefinition Magnet { get; set; }
        public PartDefinition Cable { get; set; }
        public PartDefinition Precision { get; set; }
        public PartDefinition Claw { get; set; }
        public FacilityDefinition StorageFacility { get; set; }
        public FacilityDefinition ExpansionFacility { get; set; }
        public GachaConfig Gacha { get; set; }
        public BonusSpawnConfig Bonus { get; set; }
        public WorkshopGameConfig Game { get; set; }
    }

    private sealed class PixelCanvas
    {
        private readonly Color32[] _pixels;
        public int Width { get; }
        public int Height { get; }

        /// <summary>Creates a transparent raster canvas with the supplied pixel dimensions.</summary>
        public PixelCanvas(int width, int height)
        {
            Width = width;
            Height = height;
            _pixels = new Color32[width * height];
        }

        /// <summary>Returns the complete RGBA pixel buffer for Unity's PNG encoder.</summary>
        public Color32[] ToPixels()
        {
            return _pixels;
        }

        /// <summary>Copies an RGBA pixel buffer into this raster canvas.</summary>
        public void SetPixels(Color32[] pixels)
        {
            Array.Copy(pixels, _pixels, Mathf.Min(pixels.Length, _pixels.Length));
        }

        /// <summary>Sets one pixel when its coordinates are within the image canvas.</summary>
        public void Pixel(int x, int y, Color32 color)
        {
            if (x >= 0 && x < Width && y >= 0 && y < Height)
            {
                _pixels[y * Width + x] = color;
            }
        }

        /// <summary>Fills an inclusive rectangular region.</summary>
        public void Rect(int xMin, int yMin, int xMax, int yMax, Color32 color)
        {
            for (int y = yMin; y <= yMax; y++)
            {
                for (int x = xMin; x <= xMax; x++)
                {
                    Pixel(x, y, color);
                }
            }
        }

        /// <summary>Fills an ellipse from its inclusive bounding rectangle.</summary>
        public void Ellipse(int xMin, int yMin, int xMax, int yMax, Color32 color)
        {
            float centerX = (xMin + xMax) * 0.5f;
            float centerY = (yMin + yMax) * 0.5f;
            float radiusX = Mathf.Max(1f, (xMax - xMin) * 0.5f);
            float radiusY = Mathf.Max(1f, (yMax - yMin) * 0.5f);
            for (int y = yMin; y <= yMax; y++)
            {
                for (int x = xMin; x <= xMax; x++)
                {
                    float dx = (x - centerX) / radiusX;
                    float dy = (y - centerY) / radiusY;
                    if (dx * dx + dy * dy <= 1f)
                    {
                        Pixel(x, y, color);
                    }
                }
            }
        }

        /// <summary>Draws a raster line between two pixel coordinates.</summary>
        public void Line(int x0, int y0, int x1, int y1, Color32 color)
        {
            int dx = Mathf.Abs(x1 - x0);
            int sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0);
            int sy = y0 < y1 ? 1 : -1;
            int error = dx + dy;
            while (true)
            {
                Pixel(x0, y0, color);
                if (x0 == x1 && y0 == y1)
                {
                    break;
                }

                int twiceError = 2 * error;
                if (twiceError >= dy)
                {
                    error += dy;
                    x0 += sx;
                }

                if (twiceError <= dx)
                {
                    error += dx;
                    y0 += sy;
                }
            }
        }

        /// <summary>Fills a simple polygon using a pixel-center crossing test.</summary>
        public void Polygon(Vector2Int[] vertices, Color32 color)
        {
            int minimumX = Width - 1;
            int maximumX = 0;
            int minimumY = Height - 1;
            int maximumY = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                minimumX = Mathf.Min(minimumX, vertices[i].x);
                maximumX = Mathf.Max(maximumX, vertices[i].x);
                minimumY = Mathf.Min(minimumY, vertices[i].y);
                maximumY = Mathf.Max(maximumY, vertices[i].y);
            }

            for (int y = minimumY; y <= maximumY; y++)
            {
                for (int x = minimumX; x <= maximumX; x++)
                {
                    bool inside = false;
                    for (int i = 0, j = vertices.Length - 1; i < vertices.Length; j = i++)
                    {
                        Vector2Int a = vertices[i];
                        Vector2Int b = vertices[j];
                        bool crosses = (a.y > y) != (b.y > y) && x < (float)(b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x;
                        if (crosses)
                        {
                            inside = !inside;
                        }
                    }

                    if (inside)
                    {
                        Pixel(x, y, color);
                    }
                }
            }
        }
    }
}
