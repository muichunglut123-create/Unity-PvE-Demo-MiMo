#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 建立紅色 Material（避免手寫 .mat YAML 與 Shader GUID）
/// </summary>
public static class RedMaterialCreator
{
    private const string MaterialsFolder = "Assets/Materials";
    private const string MaterialPath = "Assets/Materials/Red.mat";

    // Unity 執行編譯後會自動呼叫這段；只在 Red.mat 不存在時建立，避免重複干擾。
    [InitializeOnLoadMethod]
    private static void AutoCreateIfMissing()
    {
        EnsureMaterialsFolder();
        if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) != null) return;
        CreateRedMaterialInternal(showDialog: false);
    }

    [MenuItem("Tools/Materials/Create Red Material")]
    public static void CreateRedMaterial()
    {
        EnsureMaterialsFolder();
        if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) != null)
        {
            EditorUtility.DisplayDialog("Red Material", "Assets/Materials/Red.mat 已存在。", "OK");
            return;
        }

        CreateRedMaterialInternal(showDialog: true);
    }

    private static void CreateRedMaterialInternal(bool showDialog)
    {
        // 優先使用 URP Lit，找不到再用 Standard。
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        if (shader == null)
        {
            Debug.LogError("找不到可用 Shader（Universal Render Pipeline/Lit / Standard 都不存在）。");
            return;
        }

        var mat = new Material(shader)
        {
            name = "Red"
        };

        // URP Lit：BaseColor；Standard：Color。兩邊都設，Unity 會忽略不存在的屬性。
        mat.SetColor("_BaseColor", Color.red);
        mat.SetColor("_Color", Color.red);

        AssetDatabase.CreateAsset(mat, MaterialPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (showDialog)
            EditorUtility.DisplayDialog("Red Material", "已建立 Assets/Materials/Red.mat", "OK");
    }

    private static void EnsureMaterialsFolder()
    {
        if (AssetDatabase.IsValidFolder(MaterialsFolder)) return;
        AssetDatabase.CreateFolder("Assets", "Materials");
        AssetDatabase.Refresh();
    }
}
#endif

