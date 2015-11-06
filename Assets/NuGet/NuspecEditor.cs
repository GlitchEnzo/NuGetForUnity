using System.IO;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DefaultAsset))]
public class NuspecEditor : Editor
{
    private bool isNuspec;

    private NuspecFile nuspec;

    private string filepath;

    public void OnEnable()
    {
        // TODO: Use a better method than string.Replace, since that could remove subfolders
        filepath = AssetDatabase.GetAssetPath(target);
        filepath = filepath.Replace("Assets/", string.Empty);
        filepath = Path.Combine(Application.dataPath, filepath);

        //Debug.Log(filepath);
        //Debug.Log(Path.GetExtension(filepath));
        isNuspec = Path.GetExtension(filepath) == ".nuspec";
        if (isNuspec)
        {
            nuspec = NuspecFile.Load(filepath);
        }
    }

    protected override void OnHeaderGUI()
    {
        base.OnHeaderGUI();

        if (isNuspec)
        {
            nuspec.Id = EditorGUILayout.TextField(new GUIContent("ID", "The name of the package."), nuspec.Id);
            nuspec.Version = EditorGUILayout.TextField(new GUIContent("Version", "The semantic version of the package."), nuspec.Version);
            nuspec.Authors = EditorGUILayout.TextField(new GUIContent("Authors", "The authors of the package."), nuspec.Authors);
            nuspec.Owners = EditorGUILayout.TextField(new GUIContent("Owners", "The owners of the package."), nuspec.Owners);
            nuspec.LicenseUrl = EditorGUILayout.TextField(new GUIContent("License URL", "The URL for the license of the package."), nuspec.LicenseUrl);
            nuspec.ProjectUrl = EditorGUILayout.TextField(new GUIContent("Project URL", "The URL of the package project."), nuspec.ProjectUrl);
            nuspec.IconUrl = EditorGUILayout.TextField(new GUIContent("Icon URL", "The URL for the icon of the package."), nuspec.IconUrl);
            nuspec.RequireLicenseAcceptance = EditorGUILayout.Toggle(new GUIContent("Require License Acceptance", "Does the package license need to be accepted before use?"), nuspec.RequireLicenseAcceptance);
            nuspec.Description = EditorGUILayout.TextField(new GUIContent("Description", "The description of the package."), nuspec.Description);
            nuspec.ReleaseNotes = EditorGUILayout.TextField(new GUIContent("Release Notes", "The release notes for this specific version of the package."), nuspec.ReleaseNotes);
            nuspec.Copyright = EditorGUILayout.TextField(new GUIContent("Copyright", "The copyright of the package."), nuspec.Copyright);
            nuspec.Tags = EditorGUILayout.TextField(new GUIContent("Tags", "The tags of the package."), nuspec.Tags);

            if (GUILayout.Button(string.Format("Save {0}", Path.GetFileName(filepath))))
            {
                nuspec.Save(filepath);
            }

            if (GUILayout.Button(string.Format("Pack {0}.nupkg", Path.GetFileNameWithoutExtension(filepath))))
            {
                NugetHelper.Pack(filepath);
            }

            if (GUILayout.Button(string.Format("Push to Server")))
            {
                NugetHelper.Push(nuspec, filepath);
            }
        }
    }

    public override void OnInspectorGUI()
    {
        if (isNuspec)
        {
            // do nothing
        }
    }
}
