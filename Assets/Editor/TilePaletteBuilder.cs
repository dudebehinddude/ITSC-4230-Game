using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

/// <summary>
/// Wayland-friendly tile workflow that needs no drag-and-drop (because 
/// I am on arch linux and I can't drag stuff properly).
/// </summary>
public static class TilePaletteBuilder
{
    private const string GeneratedTilesFolder = "Assets/Tiles/Generated";
    private const string AutotileFolder = "Assets/Tiles/Sprites/Autotile";
    private const string PalettePrefabPath = "Assets/Tiles/Sprites/Tilemap/Pallette.prefab";
    private const string AutoTileTemplatePath = "Assets/Aseprite AutoTile Template.asset";
    private const string OilLightPrefabsFolder = "Assets/Tiles/Prefabs";
    private const string CrystalMarkerPrefabPath = "Assets/Tiles/Prefabs/CrystalTileMarker.prefab";
    private const int PaletteColumns = 8;

    [MenuItem("Tools/Tiles/Wire Oil Tile Lights")]
    public static void WireOilTileLightsMenu()
    {
        int wired = WireAllOilTiles();
        Debug.Log($"[TilePaletteBuilder] Wired oil-tile lights on {wired} tile(s).");
    }

    [MenuItem("Tools/Tiles/Wire Crystal Tiles")]
    public static void WireCrystalTilesMenu()
    {
        int wired = WireAllCrystalTiles();
        Debug.Log($"[TilePaletteBuilder] Wired crystal tiles on {wired} tile asset(s).");
    }

    [MenuItem("Tools/Tiles/Create Tiles From Selected Sprites")]
    public static void CreateTilesFromSelection()
    {
        var sprites = GetSelectedSprites();
        if (sprites.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "No sprites selected",
                "Select one or more sprite or .aseprite assets in the Project window first.",
                "OK");
            return;
        }

        var tiles = CreateTiles(sprites);
        Debug.Log($"[TilePaletteBuilder] Created/updated {tiles.Count} tile assets in {GeneratedTilesFolder}.");
    }

    [MenuItem("Tools/Tiles/Add Selected Tile Assets To Palette")]
    public static void AddSelectedTileAssetsToPalette()
    {
        var tiles = GetSelectedTileAssets();
        if (tiles.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "No tile assets selected",
                "Select one or more Tile, OilTile, or AutoTile .asset files in the Project window first.",
                "OK");
            return;
        }

        string palettePath = FindPalettePrefabPath();
        if (string.IsNullOrEmpty(palettePath))
        {
            EditorUtility.DisplayDialog(
                "No Tile Palette found",
                "Could not find a Tile Palette prefab. Create one via Window > 2D > Tile Palette first.",
                "OK");
            return;
        }

        int added = AddTileBasesToPalette(palettePath, tiles);
        if (added > 0)
        {
            Debug.Log($"[TilePaletteBuilder] Added {added} new tile asset(s) to palette at {palettePath}.");
            return;
        }

        if (tilemapAlreadyHasTile(palettePath, tiles))
        {
            EditorUtility.DisplayDialog(
                "Already on palette",
                "The selected tile(s) are already on the palette. If you do not see them, set Default Sprite on AutoTiles — an empty default makes the palette slot look blank.",
                "OK");
            return;
        }

        EditorUtility.DisplayDialog(
            "Nothing added",
            "Could not add the selected tile(s).\n\n" +
            "• Select the .asset file (e.g. stone_47.asset), not the .aseprite\n" +
            "• For AutoTiles, set a Default Sprite so the palette icon is visible\n" +
            "• Check the Console for [TilePaletteBuilder] errors",
            "OK");
    }

    private static bool tilemapAlreadyHasTile(string palettePath, List<TileBase> tiles)
    {
        GameObject paletteRoot = PrefabUtility.LoadPrefabContents(palettePath);
        try
        {
            var tilemap = paletteRoot.GetComponentInChildren<Tilemap>();
            if (tilemap == null)
                return false;

            var existing = new HashSet<TileBase>();
            foreach (var pos in tilemap.cellBounds.allPositionsWithin)
            {
                var tile = tilemap.GetTile(pos);
                if (tile != null)
                    existing.Add(tile);
            }

            return tiles.Any(existing.Contains);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(paletteRoot);
        }
    }

    [MenuItem("Tools/Tiles/Apply AutoTile Template To Selected")]
    public static void ApplyAutoTileTemplateToSelected()
    {
        var autoTiles = Selection.objects
            .Select(obj => AssetDatabase.LoadAssetAtPath<AutoTile>(AssetDatabase.GetAssetPath(obj)))
            .Where(tile => tile != null)
            .ToList();

        if (autoTiles.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "No AutoTiles selected",
                "Select one or more AutoTile .asset files (e.g. Assets/Tiles/Sprites/Autotile/stone_47.asset).",
                "OK");
            return;
        }

        var template = AssetDatabase.LoadAssetAtPath<AutoTileTemplate>(AutoTileTemplatePath);
        if (template == null)
        {
            EditorUtility.DisplayDialog(
                "Template not found",
                $"Could not find AutoTile template at {AutoTileTemplatePath}.",
                "OK");
            return;
        }

        int configured = 0;
        foreach (var autoTile in autoTiles)
        {
            if (ConfigureAutoTileFromTemplate(autoTile, template))
            {
                configured++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[TilePaletteBuilder] Applied AutoTile template to {configured} asset(s).");
    }

    [MenuItem("Tools/Tiles/Create Tiles And Add To Palette")]
    public static void CreateTilesAndAddToPalette()
    {
        var sprites = GetSelectedSprites();
        if (sprites.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "No sprites selected",
                "Select one or more sprite or .aseprite assets in the Project window first.",
                "OK");
            return;
        }

        string palettePath = FindPalettePrefabPath();
        if (string.IsNullOrEmpty(palettePath))
        {
            EditorUtility.DisplayDialog(
                "No Tile Palette found",
                "Could not find a Tile Palette prefab. Create one via Window > 2D > Tile Palette first.",
                "OK");
            return;
        }

        var tiles = CreateTiles(sprites);
        int added = AddTileBasesToPalette(palettePath, tiles.Cast<TileBase>().ToList());
        Debug.Log($"[TilePaletteBuilder] Added {added} new tile(s) to palette at {palettePath}.");
    }

    [MenuItem("Tools/Tiles/Rebuild Palette From Generated Folder")]
    public static void RebuildPaletteFromGenerated()
    {
        string palettePath = FindPalettePrefabPath();
        if (string.IsNullOrEmpty(palettePath))
        {
            EditorUtility.DisplayDialog(
                "No Tile Palette found",
                "Could not find a Tile Palette prefab. Create one via Window > 2D > Tile Palette first.",
                "OK");
            return;
        }

        var tiles = AssetDatabase.FindAssets("t:Tile", new[] { GeneratedTilesFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(p => AssetDatabase.LoadAssetAtPath<TileBase>(p))
            .Where(t => t != null)
            .Concat(
                AssetDatabase.FindAssets("t:AutoTile", new[] { AutotileFolder })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(p => AssetDatabase.LoadAssetAtPath<TileBase>(p))
                    .Where(t => t != null))
            .OrderBy(t => t.name)
            .ToList();

        if (tiles.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "No tiles found",
                $"No Tile assets found in {GeneratedTilesFolder}.",
                "OK");
            return;
        }

        GameObject paletteRoot = PrefabUtility.LoadPrefabContents(palettePath);
        try
        {
            var tilemap = paletteRoot.GetComponentInChildren<Tilemap>();
            if (tilemap == null)
            {
                Debug.LogError($"[TilePaletteBuilder] No Tilemap found inside palette {palettePath}.");
                return;
            }

            RemoveBrokenPrefabChildren(paletteRoot);
            tilemap.ClearAllTiles();

            for (int i = 0; i < tiles.Count; i++)
            {
                int col = i % PaletteColumns;
                int row = i / PaletteColumns;
                tilemap.SetTile(new Vector3Int(col, -row, 0), tiles[i]);
            }

            tilemap.CompressBounds();
            PrefabUtility.SaveAsPrefabAsset(paletteRoot, palettePath);
            Debug.Log($"[TilePaletteBuilder] Rebuilt palette with {tiles.Count} tile(s) from {GeneratedTilesFolder}.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(paletteRoot);
        }
    }

    [MenuItem("Tools/Tiles/Remove Broken Prefabs From Palette")]
    public static void RemoveBrokenPrefabsFromPalette()
    {
        string palettePath = FindPalettePrefabPath();
        if (string.IsNullOrEmpty(palettePath))
        {
            EditorUtility.DisplayDialog(
                "No Tile Palette found",
                "Could not find a Tile Palette prefab.",
                "OK");
            return;
        }

        GameObject paletteRoot = PrefabUtility.LoadPrefabContents(palettePath);
        try
        {
            int removed = RemoveBrokenPrefabChildren(paletteRoot);
            if (removed > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(paletteRoot, palettePath);
                Debug.Log($"[TilePaletteBuilder] Removed {removed} broken Aseprite prefab child(ren) from palette.");
            }
            else
            {
                Debug.Log("[TilePaletteBuilder] No broken prefab children found on palette.");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(paletteRoot);
        }
    }

    private static List<TileBase> GetSelectedTileAssets()
    {
        var tiles = new List<TileBase>();
        foreach (var obj in Selection.objects)
        {
            if (obj is TileBase tileBase)
            {
                tiles.Add(tileBase);
                continue;
            }

            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path))
                continue;

            var tile = AssetDatabase.LoadAssetAtPath<TileBase>(path);
            if (tile != null)
                tiles.Add(tile);
        }

        return tiles
            .GroupBy(t => t.name)
            .Select(g => g.First())
            .OrderBy(t => t.name)
            .ToList();
    }

    private static void EnsureAutoTileDefaultSprite(AutoTile autoTile)
    {
        if (autoTile.m_DefaultSprite != null)
            return;

        FieldInfo dictionaryField = typeof(AutoTile).GetField(
            "m_AutoTileDictionary",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (dictionaryField?.GetValue(autoTile) is not IDictionary dict)
            return;

        foreach (DictionaryEntry entry in dict)
        {
            if (entry.Value == null)
                continue;

            var spriteListField = entry.Value.GetType().GetField(
                "spriteList",
                BindingFlags.Instance | BindingFlags.Public);
            if (spriteListField?.GetValue(entry.Value) is IList<Sprite> sprites
                && sprites.Count > 0
                && sprites[0] != null)
            {
                autoTile.m_DefaultSprite = sprites[0];
                EditorUtility.SetDirty(autoTile);
                return;
            }
        }
    }

    private static bool ConfigureAutoTileFromTemplate(AutoTile autoTile, AutoTileTemplate template)
    {
        if (autoTile.m_TextureList == null || autoTile.m_TextureList.Count == 0)
        {
            Debug.LogWarning($"[TilePaletteBuilder] {autoTile.name} has no textures assigned. Add stone_47.aseprite in the AutoTile inspector first.");
            return false;
        }

        if (autoTile.m_MaskType != template.maskType)
        {
            Debug.LogWarning(
                $"[TilePaletteBuilder] {autoTile.name} mask type ({autoTile.m_MaskType}) does not match template ({template.maskType}).");
            return false;
        }

        var texture = autoTile.m_TextureList[0];
        string texturePath = AssetDatabase.GetAssetPath(texture);
        var sprites = AssetDatabase.LoadAllAssetsAtPath(texturePath).OfType<Sprite>().ToList();
        if (sprites.Count == 0)
        {
            Debug.LogWarning($"[TilePaletteBuilder] No sprites found for texture on {autoTile.name}.");
            return false;
        }

        ClearAutoTileRules(autoTile);
        autoTile.m_TextureList.Clear();
        autoTile.m_TextureScaleList.Clear();
        template.ApplyTemplateToAutoTile(texture, sprites, autoTile);
        autoTile.Validate();
        EditorUtility.SetDirty(autoTile);
        return true;
    }

    private static void ClearAutoTileRules(AutoTile autoTile)
    {
        FieldInfo dictionaryField = typeof(AutoTile).GetField(
            "m_AutoTileDictionary",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (dictionaryField?.GetValue(autoTile) is IDictionary dict)
        {
            dict.Clear();
        }
    }

    private static List<Sprite> GetSelectedSprites()
    {
        var sprites = new List<Sprite>();
        foreach (var obj in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path))
                continue;

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Sprite sprite)
                    sprites.Add(sprite);
            }
        }

        return sprites
            .GroupBy(s => s.name)
            .Select(g => g.First())
            .OrderBy(s => s.name)
            .ToList();
    }

    private static List<Tile> CreateTiles(List<Sprite> sprites)
    {
        if (!AssetDatabase.IsValidFolder(GeneratedTilesFolder))
        {
            Directory.CreateDirectory(GeneratedTilesFolder);
            AssetDatabase.Refresh();
        }

        var orderedSprites = sprites
            .OrderBy(s => s.name == "crystal" ? 1 : 0)
            .ThenBy(s => s.name)
            .ToList();

        var tiles = new List<Tile>();
        foreach (var sprite in orderedSprites)
        {
            string tilePath = $"{GeneratedTilesFolder}/{sprite.name}.asset";
            Tile tile = CreateOrUpdateTile(tilePath, sprite);
            tiles.Add(tile);
        }

        WireAllCrystalTiles();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return tiles;
    }

    private static Tile CreateOrUpdateTile(string tilePath, Sprite sprite)
    {
        if (IsFuelSprite(sprite.name))
        {
            var existing = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
            if (existing != null && existing is not OilTile)
            {
                AssetDatabase.DeleteAsset(tilePath);
            }

            var oilTile = AssetDatabase.LoadAssetAtPath<OilTile>(tilePath);
            if (oilTile == null)
            {
                oilTile = ScriptableObject.CreateInstance<OilTile>();
                AssetDatabase.CreateAsset(oilTile, tilePath);
            }

            oilTile.sprite = sprite;
            oilTile.colliderType = Tile.ColliderType.Grid;
            var serializedTile = new SerializedObject(oilTile);
            serializedTile.FindProperty("fuelKind").enumValueIndex = (int)FuelKindFromSpriteName(sprite.name);
            serializedTile.ApplyModifiedPropertiesWithoutUndo();
            WireOilTileLightPrefab(oilTile);
            EditorUtility.SetDirty(oilTile);
            return oilTile;
        }

        if (IsCrystalSprite(sprite.name))
        {
            return CreateOrUpdateCrystalTile(tilePath, sprite);
        }

        var existingTile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
        if (existingTile != null && existingTile is OilTile)
        {
            AssetDatabase.DeleteAsset(tilePath);
        }

        var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tile, tilePath);
        }

        tile.sprite = sprite;
        tile.colliderType = Tile.ColliderType.Grid;
        EditorUtility.SetDirty(tile);
        return tile;
    }

    private static bool IsFuelSprite(string spriteName) =>
        spriteName.StartsWith("lantern_") && spriteName != "lantern_unlit";

    private static bool IsCrystalSprite(string spriteName) =>
        spriteName is "crystal" or "activated_crystal";

    private static FuelKind FuelKindFromSpriteName(string spriteName) =>
        spriteName.Contains("blue") ? FuelKind.Blue : FuelKind.Orange;

    private static Tile CreateOrUpdateCrystalTile(string tilePath, Sprite sprite)
    {
        GameObject markerPrefab = GetOrCreateCrystalTileMarkerPrefab();

        if (sprite.name == "activated_crystal")
        {
            var existing = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
            if (existing != null && existing is not ActivatedCrystalTile)
            {
                AssetDatabase.DeleteAsset(tilePath);
            }

            var activatedTile = AssetDatabase.LoadAssetAtPath<ActivatedCrystalTile>(tilePath);
            if (activatedTile == null)
            {
                activatedTile = ScriptableObject.CreateInstance<ActivatedCrystalTile>();
                AssetDatabase.CreateAsset(activatedTile, tilePath);
            }

            activatedTile.sprite = sprite;
            activatedTile.colliderType = Tile.ColliderType.Grid;
            WireCrystalTileMarkerPrefab(activatedTile, markerPrefab);
            EditorUtility.SetDirty(activatedTile);
            return activatedTile;
        }

        var crystalExisting = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
        if (crystalExisting != null && crystalExisting is not CrystalTile)
        {
            AssetDatabase.DeleteAsset(tilePath);
        }

        var crystalTile = AssetDatabase.LoadAssetAtPath<CrystalTile>(tilePath);
        if (crystalTile == null)
        {
            crystalTile = ScriptableObject.CreateInstance<CrystalTile>();
            AssetDatabase.CreateAsset(crystalTile, tilePath);
        }

        crystalTile.sprite = sprite;
        crystalTile.colliderType = Tile.ColliderType.Grid;
        WireCrystalTileMarkerPrefab(crystalTile, markerPrefab);
        EditorUtility.SetDirty(crystalTile);
        return crystalTile;
    }

    private static int WireAllCrystalTiles()
    {
        GameObject markerPrefab = GetOrCreateCrystalTileMarkerPrefab();
        int wired = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:Tile", new[] { GeneratedTilesFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var plainTile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (plainTile == null || plainTile is CrystalTile or ActivatedCrystalTile or OilTile)
            {
                continue;
            }

            if (plainTile.name is "crystal" or "activated_crystal" && plainTile.sprite != null)
            {
                CreateOrUpdateCrystalTile(path, plainTile.sprite);
                wired++;
            }
        }

        string activatedPath = $"{GeneratedTilesFolder}/activated_crystal.asset";
        var activatedTile = AssetDatabase.LoadAssetAtPath<ActivatedCrystalTile>(activatedPath);

        foreach (string guid in AssetDatabase.FindAssets("t:CrystalTile", new[] { GeneratedTilesFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var crystalTile = AssetDatabase.LoadAssetAtPath<CrystalTile>(path);
            if (crystalTile == null)
            {
                continue;
            }

            if (WireCrystalTileMarkerPrefab(crystalTile, markerPrefab))
            {
                wired++;
            }

            if (activatedTile != null)
            {
                var serialized = new SerializedObject(crystalTile);
                serialized.FindProperty("activatedTile").objectReferenceValue = activatedTile;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(crystalTile);
            }
        }

        foreach (string guid in AssetDatabase.FindAssets("t:ActivatedCrystalTile", new[] { GeneratedTilesFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var tile = AssetDatabase.LoadAssetAtPath<ActivatedCrystalTile>(path);
            if (tile != null && WireCrystalTileMarkerPrefab(tile, markerPrefab))
            {
                wired++;
            }
        }

        RefreshSceneTilemaps();
        AssetDatabase.SaveAssets();
        return wired;
    }

    private static bool WireCrystalTileMarkerPrefab(Tile tile, GameObject markerPrefab)
    {
        if (tile == null || markerPrefab == null)
        {
            return false;
        }

        tile.gameObject = markerPrefab;
        tile.flags = TileFlags.LockColor | TileFlags.LockTransform | TileFlags.InstantiateGameObjectRuntimeOnly;
        EditorUtility.SetDirty(tile);
        return true;
    }

    private static GameObject GetOrCreateCrystalTileMarkerPrefab()
    {
        EnsureFolder("Assets/Tiles");
        EnsureFolder(OilLightPrefabsFolder);

        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(CrystalMarkerPrefabPath);
        if (existing != null)
        {
            return existing;
        }

        var go = new GameObject("CrystalTileMarker");
        try
        {
            go.AddComponent<CrystalTileMarker>();
            return PrefabUtility.SaveAsPrefabAsset(go, CrystalMarkerPrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static int WireAllOilTiles()
    {
        int wired = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:OilTile", new[] { GeneratedTilesFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var oilTile = AssetDatabase.LoadAssetAtPath<OilTile>(path);
            if (oilTile != null && WireOilTileLightPrefab(oilTile))
            {
                wired++;
            }
        }

        RefreshSceneTilemaps();
        AssetDatabase.SaveAssets();
        return wired;
    }

    private static bool WireOilTileLightPrefab(OilTile oilTile)
    {
        string prefabName = oilTile.FuelKind == FuelKind.Blue
            ? "OilTileLight_Blue"
            : "OilTileLight_Orange";
        string prefabPath = $"{OilLightPrefabsFolder}/{prefabName}.prefab";

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)
            ?? GetOrCreateOilTileLightPrefab(oilTile.FuelKind);
        if (prefab == null)
        {
            return false;
        }

        oilTile.gameObject = prefab;
        oilTile.flags = TileFlags.LockColor | TileFlags.LockTransform | TileFlags.InstantiateGameObjectRuntimeOnly;
        EditorUtility.SetDirty(oilTile);
        return true;
    }

    private static GameObject GetOrCreateOilTileLightPrefab(FuelKind kind)
    {
        EnsureFolder("Assets/Tiles");
        EnsureFolder(OilLightPrefabsFolder);

        string name = kind == FuelKind.Blue ? "OilTileLight_Blue" : "OilTileLight_Orange";
        string path = $"{OilLightPrefabsFolder}/{name}.prefab";

        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
        {
            return existing;
        }

        var go = new GameObject(name);
        try
        {
            go.transform.localPosition = OilTile.FlameOffsetFromTileCenter();

            var light2D = go.AddComponent<Light2D>();
            light2D.lightType = Light2D.LightType.Point;
            light2D.falloffIntensity = 0.5f;
            light2D.shadowsEnabled = false;

            var oilLight = go.AddComponent<OilTileLight>();
            var serializedLight = new SerializedObject(oilLight);
            serializedLight.FindProperty("fuelKind").enumValueIndex = (int)kind;
            serializedLight.ApplyModifiedPropertiesWithoutUndo();

            return PrefabUtility.SaveAsPrefabAsset(go, path);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static void RefreshSceneTilemaps()
    {
        foreach (var tilemap in Object.FindObjectsByType<Tilemap>())
        {
            tilemap.RefreshAllTiles();
            EditorUtility.SetDirty(tilemap);
        }
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
        string child = Path.GetFileName(folder);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, child);
    }

    private static string FindPalettePrefabPath()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PalettePrefabPath) != null)
            return PalettePrefabPath;

        var guids = AssetDatabase.FindAssets("t:GridPalette");
        if (guids.Length == 0)
            return null;

        var paths = guids.Select(AssetDatabase.GUIDToAssetPath).ToList();
        return paths.FirstOrDefault(p => p.StartsWith("Assets/Tiles")) ?? paths[0];
    }

    /// <summary>
    /// Appends tiles that are not already on the palette. Returns how many were added.
    /// </summary>
    private static int AddTileBasesToPalette(string palettePath, List<TileBase> tiles)
    {
        GameObject paletteRoot = PrefabUtility.LoadPrefabContents(palettePath);
        try
        {
            var tilemap = paletteRoot.GetComponentInChildren<Tilemap>();
            if (tilemap == null)
            {
                Debug.LogError($"[TilePaletteBuilder] No Tilemap found inside palette {palettePath}.");
                return 0;
            }

            RemoveBrokenPrefabChildren(paletteRoot);

            var existingTiles = new HashSet<TileBase>();
            foreach (var pos in tilemap.cellBounds.allPositionsWithin)
            {
                var existing = tilemap.GetTile(pos);
                if (existing != null)
                    existingTiles.Add(existing);
            }

            var toAdd = tiles.Where(t => !existingTiles.Contains(t)).ToList();
            if (toAdd.Count == 0)
                return 0;

            foreach (var tile in toAdd)
            {
                if (tile is AutoTile autoTile)
                    EnsureAutoTileDefaultSprite(autoTile);
            }

            AssetDatabase.SaveAssets();

            int startIndex = GetNextPaletteIndex(tilemap);

            for (int i = 0; i < toAdd.Count; i++)
            {
                int index = startIndex + i;
                int col = index % PaletteColumns;
                int row = index / PaletteColumns;
                tilemap.SetTile(new Vector3Int(col, -row, 0), toAdd[i]);
            }

            tilemap.CompressBounds();
            PrefabUtility.SaveAsPrefabAsset(paletteRoot, palettePath);
            return toAdd.Count;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(paletteRoot);
        }
    }

    /// <summary>
    /// Aseprite files dragged into a palette become prefab children that look fine
    /// but cannot be painted. Strip them whenever we touch the palette.
    /// </summary>
    private static int RemoveBrokenPrefabChildren(GameObject paletteRoot)
    {
        int removed = 0;
        var tilemapTransform = paletteRoot.GetComponentInChildren<Tilemap>()?.transform;
        if (tilemapTransform == null)
            return 0;

        for (int i = tilemapTransform.childCount - 1; i >= 0; i--)
        {
            var child = tilemapTransform.GetChild(i);
            if (PrefabUtility.IsPartOfPrefabInstance(child.gameObject))
            {
                Object.DestroyImmediate(child.gameObject);
                removed++;
            }
        }

        return removed;
    }

    /// <summary>
    /// Returns the next linear palette slot after the highest occupied index.
    /// Avoids overwriting tiles on lower rows when xMax alone would reuse row 0.
    /// </summary>
    private static int GetNextPaletteIndex(Tilemap tilemap)
    {
        int maxIndex = -1;
        foreach (var pos in tilemap.cellBounds.allPositionsWithin)
        {
            if (tilemap.GetTile(pos) == null)
            {
                continue;
            }

            int row = -pos.y;
            int index = row * PaletteColumns + pos.x;
            maxIndex = Mathf.Max(maxIndex, index);
        }

        return maxIndex + 1;
    }

    [InitializeOnLoadMethod]
    private static void AutoWireOilTilesOnLoad()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder(OilLightPrefabsFolder))
            {
                return;
            }

            bool needsWire = false;
            foreach (string guid in AssetDatabase.FindAssets("t:OilTile", new[] { GeneratedTilesFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var oilTile = AssetDatabase.LoadAssetAtPath<OilTile>(path);
                if (oilTile != null && oilTile.gameObject == null)
                {
                    needsWire = true;
                    break;
                }
            }

            if (!needsWire)
            {
                return;
            }

            WireAllOilTiles();
        };
    }

    [InitializeOnLoadMethod]
    private static void AutoWireCrystalTilesOnLoad()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder(GeneratedTilesFolder))
            {
                return;
            }

            bool needsWire = AssetDatabase.FindAssets("t:Tile", new[] { GeneratedTilesFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(p => AssetDatabase.LoadAssetAtPath<Tile>(p))
                .Any(t => t != null && t.name is "crystal" or "activated_crystal" && t is not CrystalTile and not ActivatedCrystalTile);

            if (!needsWire)
            {
                needsWire = AssetDatabase.FindAssets("t:CrystalTile", new[] { GeneratedTilesFolder })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(p => AssetDatabase.LoadAssetAtPath<CrystalTile>(p))
                    .Any(t => t != null && (t.gameObject == null || t.ActivatedTile == null));
            }

            if (!needsWire)
            {
                return;
            }

            WireAllCrystalTiles();
        };
    }
}
