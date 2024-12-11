# Plugin Development

## Introduction

In order to develop a NuGetForUnity Plugin you need to start with these steps:

1. Decide on your plugin name. It could just be your company's name for example.
2. You plugin project name should be <PluginName>.NugetForUnityPlugin since the plugin loader will look for dlls which contain NugetForUnityPlugin in its name.
3. If you are targeting Unity older than 2021.3 create a `.netstandard2.0` C# library project.
4. If you are targeting Unity 2021.3 or newer create a `.netstandard2.1` C# library project.
5. Add a reference to [NuGetForUnity.PluginAPI](https://www.nuget.org/packages/NuGetForUnity.PluginAPI) nuget package in your project. This package contains the interfaces that you will need to implement.
6. Depending on the needs of your plugin you might also need to add references to `UnityEngine.dll` and `UnityEditor.dll` from your Unity installation.
7. Write a class that implements `INugetPlugin` interface. In the `Register` method you will get a `INugetPluginRegistry` that has methods you can use to register your classes that implement custom handling of certain functionalities like installing and uninstalling the packages.

Note that `INugetPluginRegistry` provides you a few things you can use in your plugin:

- `IsRunningInUnity` property will be true if the plugin is being run from Unity and false if it is run from command line.
- `PluginService` property that you can pass to your custom handlers if they need to use any of these:
    - `ProjectAssetsDir` property that gives you the absolute path to the project's Assets directory.
    - `LogError`, `LogErrorFormat` and `LogVerbose` methods that you can use for logging. You should not use `UnityEngine.Debug.Log` methods since they will not work if plugin is used from command line.

## Extension points

NuGetForUnity implements a certain extension points that your plugin can register to in order to provide custom processing. It can add new extension points in the future without breaking backward compatibility with existing ones.

### Custom action buttons in Nuget window

If you want to provide a custom action button next to some packages in NuGetForUnity Manage Packages window you can write a class that implements `IPackageButtonsHandler` interface. It will give you a method bellow that you need to implement:

```cs
void DrawButtons(INugetPackage package, INugetPackage? installedPackage, bool existsInUnity);
```

Inside this method you will get info about an online `package` that is being rendered. The `installedPackage` is that same info about the version of that package that is installed if it is installed. It will be null if the package is not currently installed in the project. The third parameter tells you if this package is actually included in Unity itself which means installing it should be disabled.

Inside the method you can use `GUILayout.Button` Unity method to render additional buttons that will be rendered to the left of current Install/Uninstall/Update and similar buttons.

Since code here will use UnityEditor functionality which is not available when NuGetForUnity is run from command line you should only register this class in plugin registry if it running from Unity. You can do so in your `INugetPlugin.Register` implementation like this:

```cs
if (registry.IsRunningInUnity)
{
    var myButtonHandler = new MyButtonHandler(registry.PluginService);
    registry.RegisterPackageButtonDrawer(linkUnlinkSourceButton);
}
```

### Custom package installation

If you want to customize how packages are installed or just how certain files from packages are extracted and handled you can write a class that implements `IPackageInstallFileHandler` interface. It declares this method:

```cs
bool HandleFileExtraction(INugetPackage package, ZipArchiveEntry entry, string extractDirectory);`
```

When you implement that method you can choose if you want to handle each specific entry from `nupkg` file and how. If you handle the entry your self and you do not want the default installation of that file to occur you should return true from this method indicating that you have done all the processing you need for this entry. If you still want default installation logic to handle this entry just return false from this method.

### Custom handling of package uninstall

If you implement custom handling of installation you will often also need to implement custom handling of uninstall. For that you need to write a class that implements `IPackageUninstallHandler` interface. It declares two methods:

```cs
void HandleUninstall(INugetPackage package, PackageUninstallReason uninstallReason);
void HandleUninstalledAll();
```

The first method is called for each package that is being uninstalled. The `uninstallReason` can be:

- `IndividualUninstall` when individual package uninstall has be requested by the user.
- `UninstallAll` when user requested all packages from the project to be uninstalled.
- `IndividualUpdate` when user requested a package to be updated so we are uninstalling the current version.
- `UpdateAll` when user requested all packages to be updated so we are uninstalling old versions.

The second method, `HandleUninstalledAll()` will only be called if user requested all packages to be uninstalled after all the default uninstall processing has been done. If you don't need to do anything special in this case you can leave this method empty.

## New extension points

In case you have an idea for a plugin that requires some new extension points please open an issue requesting it with a description of how are you planing to use it. Pull requests implementing new extension points are also welcome as long as a clear description for their need is given.

# Plugin support implementation details

This section explains how plugin support is implemented in NugetForUnity which should also explain how new extension points can be added.

Under src/NugetForUnity.CreateDll there is a NuGetForUnity.CreateDll.sln solution. That solution contains two projects: NugetForUnity.CreateDll itself and NugetForUnity.PluginAPI project. PluginAPI project defines all the interfaces that should be visible to plugin implementations. NugetForUnity project references this one since it also implements and extends some of these interfaces.

PluginAPI project is setup so that it copies the built NugetForUnity.PluginAPI.dll to src/NuGetForUnity/Editor/ folder where the rest of actual source files of NugetForUnity reside. This is needed because src/NugetForUnity folder contains package.json file identifying that folder as a Unity package that can be locally referenced from the file system.

CreateDll project has two classes under PluginSupport folder:

- `NugetPluginSupport` which implements the `INugetPluginService`
- `PluginRegistry` that implements `INugetPluginRegistry` and also has `InitPlugins` method that is called after Nuget.config is loaded and a list of enabled plugins is read from it.

    Note that `AssemblyLoader` class it uses to load the plugins has a different implementation in `NuGetForUnity.Cli` project which is for running from command line. It also has a different implementation of `SessionStorage` class that will return "false" for `IsRunningInUnity` key.

`NugetPreferences` constructor has code that looks for all plugins installed in the project by checking all assemblies whose name contains "NugetForUnityPlugin" in its name. It will list these plugins in the preferences window so each can be enables or disabled.

In order to find where are extension points executed in the code you can just search for `PluginRegistry.Instance` through the entire solution. For example you will find that `PluginRegistry.Instance.HandleFileExtraction(...)` is called in `NugetPackageInstaller.Install()` method within the loop that handles the entries for `nupkg` file that is being installed.
