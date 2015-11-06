using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DefaultAsset))]
public class NuspecEditor : Editor
{
    //[MenuItem("Assets/Edit Nuspec")]
    //static void EditMyType()
    //{
    //    //MyEditorWindow.Init(Selection.activeObject as MyType);
    //}

    //[MenuItem("Assets/Edit Nuspec", true)]
    //static bool EditMyTypeValidate()
    //{
    //    Debug.Log(Selection.activeObject);
    //    //return (Selection.activeObject.GetType() == typeof(MyType));
    //    return false;
    //}

    public void OnEnable()
    {
        string assetPath = AssetDatabase.GetAssetPath(target);
        Debug.Log("Custom Editor! " + assetPath);
        if (target.name == "manifest")
        {
        }
    }
}
