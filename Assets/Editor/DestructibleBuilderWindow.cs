using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DestructibleBuilderWindow : EditorWindow
{
    private GameObject sourceObject;
    private Material pieceMaterial;
    private string outputFolder = "Assets/Generated/Destructibles";
    private bool saveAsPrefab = true;
    private bool keepSceneInstance = true;

    private FractureMode fractureMode = FractureMode.Voxel;

    private bool useColliderFilter = true;
    private float surfaceTolerance = 0.15f;
    private float pieceSpacing = 0.01f;
    private bool autoSupportLowest = true;

    private VoxelFractureSettings voxelSettings = new VoxelFractureSettings();
    private VoronoiFractureSettings voronoiSettings = new VoronoiFractureSettings();
    private SliceFractureSettings sliceSettings = new SliceFractureSettings();

    private bool initializedPresets;

    [MenuItem("Tools/Cluster Destruction/Destructible Builder")]
    public static void Open()
    {
        GetWindow<DestructibleBuilderWindow>("Destructible Builder");
    }

    [MenuItem("Tools/Cluster Destruction/Load Selected In Builder")]
    public static void BuildSelected()
    {
        DestructibleBuilderWindow window = GetWindow<DestructibleBuilderWindow>("Destructible Builder");
        window.sourceObject = Selection.activeGameObject;
        window.Show();
        window.Focus();
        window.ShowNotification(new GUIContent("Loaded selection. Press Build Destructible when ready."));
    }

    [MenuItem("Tools/Cluster Destruction/Load Selected In Builder", true)]
    public static bool CanBuildSelected()
    {
        return Selection.activeGameObject != null;
    }

    private void OnGUI()
    {
        EnsurePresetDefaults();

        GUILayout.Label("Source", EditorStyles.boldLabel);
        sourceObject = (GameObject)EditorGUILayout.ObjectField(
            "Source Object",
            sourceObject,
            typeof(GameObject),
            true);

        pieceMaterial = (Material)EditorGUILayout.ObjectField(
            "Piece Material",
            pieceMaterial,
            typeof(Material),
            false);

        GUILayout.Space(8);
        GUILayout.Label("Output", EditorStyles.boldLabel);

        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
        saveAsPrefab = EditorGUILayout.Toggle("Save As Prefab", saveAsPrefab);
        using (new EditorGUI.DisabledScope(!saveAsPrefab))
        {
            keepSceneInstance = EditorGUILayout.Toggle("Keep Scene Instance", keepSceneInstance);
        }

        GUILayout.Space(8);
        GUILayout.Label("Common", EditorStyles.boldLabel);

        fractureMode = (FractureMode)EditorGUILayout.EnumPopup("Fracture Mode", fractureMode);
        useColliderFilter = EditorGUILayout.Toggle("Use Collider Filter", useColliderFilter);
        surfaceTolerance = Mathf.Max(0f, EditorGUILayout.FloatField("Surface Tolerance", surfaceTolerance));
        pieceSpacing = Mathf.Max(0f, EditorGUILayout.FloatField("Piece Spacing", pieceSpacing));
        autoSupportLowest = EditorGUILayout.Toggle("Auto Support Lowest", autoSupportLowest);

        GUILayout.Space(8);
        GUILayout.Label("Mode Settings", EditorStyles.boldLabel);

        switch (fractureMode)
        {
            case FractureMode.Voxel:
                voxelSettings.gridX = Mathf.Max(1, EditorGUILayout.IntField("Grid X", voxelSettings.gridX));
                voxelSettings.gridY = Mathf.Max(1, EditorGUILayout.IntField("Grid Y", voxelSettings.gridY));
                voxelSettings.gridZ = Mathf.Max(1, EditorGUILayout.IntField("Grid Z", voxelSettings.gridZ));
                break;

            case FractureMode.Voronoi:
                FractureQualityPreset newVoronoiPreset = (FractureQualityPreset)EditorGUILayout.EnumPopup("Quality Preset", voronoiSettings.qualityPreset);
                if (newVoronoiPreset != voronoiSettings.qualityPreset)
                {
                    voronoiSettings.qualityPreset = newVoronoiPreset;
                    if (voronoiSettings.qualityPreset != FractureQualityPreset.Custom)
                    {
                        voronoiSettings.ApplyQualityPreset();
                    }
                }

                EditorGUI.BeginChangeCheck();
                voronoiSettings.seedCount = Mathf.Max(1, EditorGUILayout.IntField("Seed Count", voronoiSettings.seedCount));
                voronoiSettings.randomSeed = EditorGUILayout.IntField("Random Seed", voronoiSettings.randomSeed);
                voronoiSettings.minPieceVolume = Mathf.Max(0.000001f, EditorGUILayout.FloatField("Min Piece Volume", voronoiSettings.minPieceVolume));
                voronoiSettings.minAxisSize = Mathf.Max(0.0001f, EditorGUILayout.FloatField("Min Axis Size", voronoiSettings.minAxisSize));
                voronoiSettings.minAxisRatio = Mathf.Clamp01(EditorGUILayout.FloatField("Min Axis Ratio", voronoiSettings.minAxisRatio));
                voronoiSettings.maxAspectRatio = Mathf.Max(1f, EditorGUILayout.FloatField("Max Aspect Ratio", voronoiSettings.maxAspectRatio));
                voronoiSettings.colliderMode = (FracturePieceColliderMode)EditorGUILayout.EnumPopup("Collider Mode", voronoiSettings.colliderMode);
                voronoiSettings.autoMeshColliderMaxTriangles = Mathf.Max(0, EditorGUILayout.IntField("Auto Mesh Collider Max Triangles", voronoiSettings.autoMeshColliderMaxTriangles));
                voronoiSettings.autoMeshColliderMinAxisSize = Mathf.Max(0f, EditorGUILayout.FloatField("Auto Mesh Collider Min Axis", voronoiSettings.autoMeshColliderMinAxisSize));
                voronoiSettings.autoMeshColliderMinVolume = Mathf.Max(0f, EditorGUILayout.FloatField("Auto Mesh Collider Min Volume", voronoiSettings.autoMeshColliderMinVolume));
                if (EditorGUI.EndChangeCheck() && voronoiSettings.qualityPreset != FractureQualityPreset.Custom)
                {
                    voronoiSettings.qualityPreset = FractureQualityPreset.Custom;
                }
                break;

            case FractureMode.Slice:
                FractureQualityPreset newSlicePreset = (FractureQualityPreset)EditorGUILayout.EnumPopup("Quality Preset", sliceSettings.qualityPreset);
                if (newSlicePreset != sliceSettings.qualityPreset)
                {
                    sliceSettings.qualityPreset = newSlicePreset;
                    if (sliceSettings.qualityPreset != FractureQualityPreset.Custom)
                    {
                        sliceSettings.ApplyQualityPreset();
                    }
                }

                EditorGUI.BeginChangeCheck();
                sliceSettings.sliceCount = Mathf.Clamp(EditorGUILayout.IntField("Slice Count", sliceSettings.sliceCount), 1, 16);
                sliceSettings.gridX = Mathf.Max(1, EditorGUILayout.IntField("Grid X", sliceSettings.gridX));
                sliceSettings.gridY = Mathf.Max(1, EditorGUILayout.IntField("Grid Y", sliceSettings.gridY));
                sliceSettings.gridZ = Mathf.Max(1, EditorGUILayout.IntField("Grid Z", sliceSettings.gridZ));
                sliceSettings.randomSeed = EditorGUILayout.IntField("Random Seed", sliceSettings.randomSeed);
                sliceSettings.angleJitter = Mathf.Max(0f, EditorGUILayout.FloatField("Angle Jitter", sliceSettings.angleJitter));
                sliceSettings.axisBias = EditorGUILayout.Vector3Field("Axis Bias", sliceSettings.axisBias);
                sliceSettings.minPieceVolume = Mathf.Max(0.000001f, EditorGUILayout.FloatField("Min Piece Volume", sliceSettings.minPieceVolume));
                sliceSettings.minAxisSize = Mathf.Max(0.0001f, EditorGUILayout.FloatField("Min Axis Size", sliceSettings.minAxisSize));
                sliceSettings.minAxisRatio = Mathf.Clamp01(EditorGUILayout.FloatField("Min Axis Ratio", sliceSettings.minAxisRatio));
                sliceSettings.maxAspectRatio = Mathf.Max(1f, EditorGUILayout.FloatField("Max Aspect Ratio", sliceSettings.maxAspectRatio));
                sliceSettings.maxRetryPerSlice = Mathf.Max(1, EditorGUILayout.IntField("Max Retry Per Slice", sliceSettings.maxRetryPerSlice));
                sliceSettings.mergeSmallPieces = EditorGUILayout.Toggle("Merge Small Pieces", sliceSettings.mergeSmallPieces);
                sliceSettings.maxSmallPieceMergePasses = Mathf.Max(0, EditorGUILayout.IntField("Max Merge Passes", sliceSettings.maxSmallPieceMergePasses));
                sliceSettings.colliderMode = (FracturePieceColliderMode)EditorGUILayout.EnumPopup("Collider Mode", sliceSettings.colliderMode);
                sliceSettings.autoMeshColliderMaxTriangles = Mathf.Max(0, EditorGUILayout.IntField("Auto Mesh Collider Max Triangles", sliceSettings.autoMeshColliderMaxTriangles));
                sliceSettings.autoMeshColliderMinAxisSize = Mathf.Max(0f, EditorGUILayout.FloatField("Auto Mesh Collider Min Axis", sliceSettings.autoMeshColliderMinAxisSize));
                sliceSettings.autoMeshColliderMinVolume = Mathf.Max(0f, EditorGUILayout.FloatField("Auto Mesh Collider Min Volume", sliceSettings.autoMeshColliderMinVolume));
                if (EditorGUI.EndChangeCheck() && sliceSettings.qualityPreset != FractureQualityPreset.Custom)
                {
                    sliceSettings.qualityPreset = FractureQualityPreset.Custom;
                }
                break;
        }

        GUILayout.Space(16);

        GUI.enabled = sourceObject != null;
        if (GUILayout.Button("Build Destructible"))
        {
            BuildDestructible();
        }
        GUI.enabled = true;
    }

    private void BuildDestructible()
    {
        if (sourceObject == null) return;

        Bounds localBounds = FractureBuilderCore.CalculateLocalBounds(sourceObject);
        Collider[] sourceColliders = sourceObject.GetComponentsInChildren<Collider>();
        Material resolvedPieceMaterial = ResolvePieceMaterial(sourceObject, pieceMaterial);

        FractureBuildContext context = new FractureBuildContext
        {
            sourceObject = sourceObject,
            localBounds = localBounds,
            sourceColliders = sourceColliders,
            useColliderFilter = useColliderFilter,
            surfaceTolerance = surfaceTolerance,
            pieceSpacing = pieceSpacing,
            pieceMaterial = resolvedPieceMaterial
        };

        List<FracturePieceData> pieces = null;
        List<SliceFracturePiece> slicePieces = null;
        List<SliceFracturePiece> voronoiPieces = null;

        if (fractureMode == FractureMode.Slice)
        {
            Mesh combinedMesh = MeshCombineUtility.CombineMeshesToLocal(sourceObject);
            if (combinedMesh == null)
            {
                Debug.LogError("combinedMesh is null");
                EditorUtility.DisplayDialog("Build Failed", "Slice モード用の結合メッシュを作れなかった。", "OK");
                return;
            }

            slicePieces = SliceFractureGenerator.Generate(
                combinedMesh,
                sliceSettings);

            if (slicePieces == null || slicePieces.Count == 0)
            {
                EditorUtility.DisplayDialog("Build Failed", "Slice 破片の生成に失敗した。", "OK");
                return;
            }
        }
        else if (fractureMode == FractureMode.Voronoi)
        {
            Mesh combinedMesh = MeshCombineUtility.CombineMeshesToLocal(sourceObject);
            if (combinedMesh == null)
            {
                Debug.LogError("combinedMesh is null");
                EditorUtility.DisplayDialog("Build Failed", "Voronoi 用の結合 Mesh を作れませんでした。", "OK");
                return;
            }

            voronoiPieces = VoronoiFractureGenerator.Generate(
                combinedMesh,
                voronoiSettings);

            if (voronoiPieces == null || voronoiPieces.Count == 0)
            {
                EditorUtility.DisplayDialog("Build Failed", "Voronoi 破片の生成に失敗しました。Seed Count を減らすか Preset を Light にしてください。", "OK");
                return;
            }
        }
        else
        {
            pieces = FractureBuilderCore.GeneratePieces(
                fractureMode,
                context,
                voxelSettings,
                voronoiSettings,
                sliceSettings);

            if (pieces == null || pieces.Count == 0)
            {
                EditorUtility.DisplayDialog("Build Failed", "破片が1つも生成されなかった。Collider Filter や解像度を見直して。", "OK");
                return;
            }
        }

        string safeName = MakeSafeAssetName(sourceObject.name);
        string modeName = GetFractureModeName(fractureMode);
        string generatedName = modeName + "_" + safeName;
        GameObject root = new GameObject("Destructible_" + generatedName);
        Undo.RegisterCreatedObjectUndo(root, "Create Destructible Root");

        bool sourceIsSceneObject = sourceObject.scene.IsValid();
        root.transform.position = sourceIsSceneObject ? sourceObject.transform.position : Vector3.zero;
        root.transform.rotation = sourceIsSceneObject ? sourceObject.transform.rotation : Quaternion.identity;
        root.transform.localScale = sourceIsSceneObject ? sourceObject.transform.lossyScale : Vector3.one;

        GameObject intactRoot = new GameObject("IntactRoot");
        GameObject fracturedRoot = new GameObject("FracturedRoot");

        Undo.RegisterCreatedObjectUndo(intactRoot, "Create Intact Root");
        Undo.RegisterCreatedObjectUndo(fracturedRoot, "Create Fractured Root");

        intactRoot.transform.SetParent(root.transform, false);
        fracturedRoot.transform.SetParent(root.transform, false);

        intactRoot.transform.localPosition = Vector3.zero;
        intactRoot.transform.localRotation = Quaternion.identity;
        intactRoot.transform.localScale = Vector3.one;

        fracturedRoot.transform.localPosition = Vector3.zero;
        fracturedRoot.transform.localRotation = Quaternion.identity;
        fracturedRoot.transform.localScale = Vector3.one;

        GameObject intactCopy = Instantiate(sourceObject, intactRoot.transform);
        Undo.RegisterCreatedObjectUndo(intactCopy, "Create Intact Copy");
        intactCopy.name = sourceObject.name;
        intactCopy.transform.localPosition = Vector3.zero;
        intactCopy.transform.localRotation = Quaternion.identity;
        intactCopy.transform.localScale = Vector3.one;

        if (fractureMode == FractureMode.Slice)
        {
            for (int i = 0; i < slicePieces.Count; i++)
            {
                GameObject pieceObject = MeshPieceObjectFactory.CreatePieceObject(
                    fracturedRoot.transform,
                    slicePieces[i].mesh,
                    i,
                    resolvedPieceMaterial,
                    sliceSettings.colliderMode,
                    sliceSettings.autoMeshColliderMaxTriangles,
                    sliceSettings.autoMeshColliderMinAxisSize,
                    sliceSettings.autoMeshColliderMinVolume);

                pieceObject.name = $"SlicePiece_{i:D2}";
                Undo.RegisterCreatedObjectUndo(pieceObject, "Create Slice Piece");
            }
        }
        else if (fractureMode == FractureMode.Voronoi)
        {
            for (int i = 0; i < voronoiPieces.Count; i++)
            {
                GameObject pieceObject = MeshPieceObjectFactory.CreatePieceObject(
                    fracturedRoot.transform,
                    voronoiPieces[i].mesh,
                    i,
                    resolvedPieceMaterial,
                    voronoiSettings.colliderMode,
                    voronoiSettings.autoMeshColliderMaxTriangles,
                    voronoiSettings.autoMeshColliderMinAxisSize,
                    voronoiSettings.autoMeshColliderMinVolume);

                pieceObject.name = $"VoronoiPiece_{i:D2}";
                Undo.RegisterCreatedObjectUndo(pieceObject, "Create Voronoi Piece");
            }
        }
        else
        {
            for (int i = 0; i < pieces.Count; i++)
            {
                GameObject pieceObject = FractureBuilderCore.CreatePieceObject(
                    fracturedRoot.transform,
                    pieces[i],
                    resolvedPieceMaterial,
                    pieceSpacing);

                pieceObject.name = $"VoxelPiece_{i:D2}";
                Undo.RegisterCreatedObjectUndo(pieceObject, "Create Fracture Piece");
            }
        }

        ConnectedClusterDestruction cluster = fracturedRoot.GetComponent<ConnectedClusterDestruction>();
        if (cluster == null)
        {
            cluster = fracturedRoot.AddComponent<ConnectedClusterDestruction>();
        }

        SetBoolProperty(cluster, "autoCollectChildren", true);
        SetBoolProperty(cluster, "autoMarkLowestAsSupport", autoSupportLowest);

        cluster.BuildCluster();

        DestructibleObject destructible = root.GetComponent<DestructibleObject>();
        if (destructible == null)
        {
            destructible = root.AddComponent<DestructibleObject>();
        }

        AssignDestructibleFields(destructible, intactRoot, fracturedRoot, cluster);

        fracturedRoot.SetActive(false);

        if (saveAsPrefab)
        {
            SaveGeneratedAssets(root, resolvedPieceMaterial, generatedName);

            if (!keepSceneInstance)
            {
                DestroyImmediate(root);
                return;
            }
        }

        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);
    }

    private void SaveGeneratedAssets(GameObject root, Material resolvedPieceMaterial, string safeName)
    {
        EnsureFolder(outputFolder);

        string meshAssetPath = AssetDatabase.GenerateUniqueAssetPath($"{outputFolder}/{safeName}_DestructionMeshes.asset");
        string prefabPath = AssetDatabase.GenerateUniqueAssetPath($"{outputFolder}/Destructible_{safeName}.prefab");

        SaveGeneratedMeshes(root, meshAssetPath);
        SaveGeneratedMaterial(resolvedPieceMaterial, safeName);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        if (prefab == null)
        {
            Debug.LogError("Failed to save destructible prefab: " + prefabPath);
            return;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
    }

    private void SaveGeneratedMeshes(GameObject root, string meshAssetPath)
    {
        MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
        Mesh firstMesh = null;
        int meshIndex = 0;

        for (int i = 0; i < meshFilters.Length; i++)
        {
            Mesh mesh = meshFilters[i].sharedMesh;
            if (mesh == null) continue;
            if (AssetDatabase.Contains(mesh)) continue;

            mesh.name = $"{meshFilters[i].gameObject.name}_Mesh";

            if (firstMesh == null)
            {
                firstMesh = mesh;
                AssetDatabase.CreateAsset(firstMesh, meshAssetPath);
            }
            else
            {
                mesh.name = $"{mesh.name}_{meshIndex}";
                AssetDatabase.AddObjectToAsset(mesh, firstMesh);
            }

            meshIndex++;
        }

        if (firstMesh != null)
        {
            EditorUtility.SetDirty(firstMesh);
        }
    }

    private void SaveGeneratedMaterial(Material resolvedPieceMaterial, string safeName)
    {
        if (resolvedPieceMaterial == null) return;
        if (AssetDatabase.Contains(resolvedPieceMaterial)) return;

        string materialPath = AssetDatabase.GenerateUniqueAssetPath($"{outputFolder}/{safeName}_DestructionPiece.mat");
        AssetDatabase.CreateAsset(resolvedPieceMaterial, materialPath);
    }

    private void EnsureFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            outputFolder = "Assets/Generated/Destructibles";
            folderPath = outputFolder;
        }

        folderPath = folderPath.Replace("\\", "/");
        if (!folderPath.StartsWith("Assets"))
        {
            folderPath = "Assets/Generated/Destructibles";
            outputFolder = folderPath;
        }

        string[] parts = folderPath.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private void AssignDestructibleFields(
        DestructibleObject destructible,
        GameObject intactRoot,
        GameObject fracturedRoot,
        ConnectedClusterDestruction cluster)
    {
        SerializedObject so = new SerializedObject(destructible);

        so.FindProperty("intactRoot").objectReferenceValue = intactRoot;
        so.FindProperty("fracturedRoot").objectReferenceValue = fracturedRoot;
        so.FindProperty("cluster").objectReferenceValue = cluster;

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private Material ResolvePieceMaterial(GameObject source, Material overrideMaterial)
    {
        if (overrideMaterial != null)
        {
            return overrideMaterial;
        }

        Material inherited = FindFirstSharedMaterial(source);
        if (inherited == null)
        {
            return null;
        }

        Material material = new Material(inherited);
        material.name = inherited.name + "_DestructionPiece";

        if (material.HasProperty("_Cull"))
        {
            material.SetFloat("_Cull", 0f);
        }

        material.doubleSidedGI = true;
        return material;
    }

    private Material FindFirstSharedMaterial(GameObject source)
    {
        if (source == null) return null;

        Renderer[] renderers = source.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] materials = renderers[i].sharedMaterials;
            for (int j = 0; j < materials.Length; j++)
            {
                if (materials[j] != null)
                {
                    return materials[j];
                }
            }
        }

        return null;
    }

    private string MakeSafeAssetName(string rawName)
    {
        string name = string.IsNullOrWhiteSpace(rawName) ? "Destructible" : rawName;
        char[] invalid = Path.GetInvalidFileNameChars();

        for (int i = 0; i < invalid.Length; i++)
        {
            name = name.Replace(invalid[i], '_');
        }

        return name.Replace(' ', '_');
    }

    private void EnsurePresetDefaults()
    {
        if (initializedPresets) return;

        initializedPresets = true;

        if (voronoiSettings.qualityPreset != FractureQualityPreset.Custom)
        {
            voronoiSettings.ApplyQualityPreset();
        }

        if (sliceSettings.qualityPreset != FractureQualityPreset.Custom)
        {
            sliceSettings.ApplyQualityPreset();
        }
    }

    private string GetFractureModeName(FractureMode mode)
    {
        switch (mode)
        {
            case FractureMode.Slice:
                return "Slice";

            case FractureMode.Voronoi:
                return "Voronoi";

            case FractureMode.Voxel:
                return "Voxel";
        }

        return mode.ToString();
    }

    private void SetBoolProperty(Object target, string propertyName, bool value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop != null)
        {
            prop.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

public sealed class DestructibleBuildingBuildProcessor : IProcessSceneWithReport
{
    private static readonly string[] BuildingNamePrefixes =
    {
        "ClusterTower_",
        "Building_",
        "office_building_",
        "factory_building_",
        "factory_chimney",
        "mid_house_",
        "small_house_",
        "basement_"
    };

    private const string BattleSceneName = "BattleScene";
    private const string VisualOnlyBuildingRootPrefix = "FieldOutsideBuildings";

    public int callbackOrder => -1000;

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (!scene.IsValid() || scene.name != BattleSceneName)
            return;

        int clearedObjectCount = 0;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < transforms.Length; j++)
            {
                Transform target = transforms[j];
                if (target == null ||
                    IsUnderVisualOnlyBuildingRoot(target) ||
                    !IsBuildingObjectName(target.name))
                {
                    continue;
                }

                clearedObjectCount += ClearBatchingStaticRecursively(target);
            }
        }

        Debug.Log(
            $"[DestructibleBuildingBuildProcessor] Disabled Static Batching on " +
            $"{clearedObjectCount} destructible building object(s) in {scene.name}.");
    }

    private static bool IsBuildingObjectName(string objectName)
    {
        for (int i = 0; i < BuildingNamePrefixes.Length; i++)
        {
            if (objectName.StartsWith(
                    BuildingNamePrefixes[i],
                    System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static int ClearBatchingStaticRecursively(Transform root)
    {
        int clearedObjectCount = 0;
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            GameObject target = transforms[i].gameObject;
            StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(target);
            if ((flags & StaticEditorFlags.BatchingStatic) == 0)
                continue;

            GameObjectUtility.SetStaticEditorFlags(
                target,
                flags & ~StaticEditorFlags.BatchingStatic);
            clearedObjectCount++;
        }

        return clearedObjectCount;
    }

    private static bool IsUnderVisualOnlyBuildingRoot(Transform target)
    {
        Transform current = target;
        while (current != null)
        {
            if (current.name.StartsWith(
                    VisualOnlyBuildingRootPrefix,
                    System.StringComparison.Ordinal))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }
}
