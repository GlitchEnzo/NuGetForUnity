# What is NuGetForUnity?

NuGetForUnity is a NuGet client built from scratch to run inside the Unity Editor. NuGet is a package management system which makes it easy to create packages that are distributed on a server and consumed by users. NuGet supports [sematic versioning](http://semver.org/) for packages as well as dependencies on other packages.

You can learn more about NuGet here: [nuget.org](https://www.nuget.org/)

NuGetForUnity provides a visual editor window to see available packages on the server, see installed packages, and see available package updates. A visual interface is also provided to create and edit _.nuspec_ files in order to define and publish your own NuGet packages from within Unity.

<img alt="Online Packages Tap" src="docs/screenshots/online.png" height="500px" />

# How do I install NuGetForUnity?

<details>
<summary>Install via Package Manager</summary>

#### Unity 2019.3 or newer

1. Open Package Manager window (Window | Package Manager)
1. Click `+` button on the upper-left of a window, and select "Add package from git URL..."
1. Enter the following URL and click `Add` button

```
https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity
```

> **_NOTE:_** To install a concreate version you can specify the version by prepending #v{version} e.g. `#v2.0.0`. For more see [Unity UPM Documentation](https://docs.unity3d.com/Manual/upm-git.html).

#### Unity 2019.2 or earlier

1. Close Unity Editor
1. Open Packages/manifest.json by any Text editor
1. Insert the following line after `"dependencies": {`, and save the file.

    ```json
    "com.glitchenzo.nugetforunity": "https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity",
    ```

1. Reopen Unity project in Unity Editor

</details>

<details>
<summary>Install via OpenUPM</summary>
The package is available on the <a href="https://openupm.com/packages/com.github-glitchenzo.nugetforunity/">openupm</a> registry. So you can install it via openupm-cli.

```
openupm add com.github-glitchenzo.nugetforunity
```

</details>

<details>
<summary>Install via .unitypackage file</summary>

Install the provided Unity package into your Unity project. Located [here](https://github.com/GlitchEnzo/NuGetForUnity/releases).

Download the `*.unitypackage` file. Right-click on it in File Explorer and choose "Open in Unity."

</details>

# How do I use NuGetForUnity?

To launch, select **NuGet → Manage NuGet Packages**

<img alt="Menu Items" src="docs/screenshots/menu_item.png" height="170px" />

After several seconds (it can take some time to query the server for packages), you should see a window like this:

<img alt="Online Packages Tap" src="docs/screenshots/online.png" height="500px" />

The **Online** tab shows the packages available on the NuGet server.

Enable **Show All Versions** to list all old versions of a package (doesn't work with nuget.org).
Disable **Show All Versions** to only show the latest version of a package.

Enable **Show Prelease** to list prerelease versions of packages (alpha, beta, release candidate, etc).
Disable **Show Prerelease** to only show stable releases.

Type a search term in the **Search** box to filter what is displayed.

Press the **Refresh** button to refresh the window with the latest query settings. (Useful after pushing a new package to the server and wanting to see it without closing and reopening the window.)

The name of the package, the version of the package (in square brakets), and a description are displayed.

Click the **View License** to open the license in a web browser.

Click the **Install** to install the package.
Note: If the package is already installed an **Uninstall** button will be displayed which lets you uninstall the package.

The **Installed** tabs shows the packages already installed in the current Unity project.

<img alt="Installed Packages Tap" src="docs/screenshots/installed.png" height="500px" />

Click the **Uninstall** button to uninstall the package.

The **Updates** tab shows the packages currently installed that have updates available on the server.

<img alt="Updates Packages Tap" src="docs/screenshots/updates.png" height="500px" />

The version in brackets on the left is the new version number. The version in brackets in the **Update** button is the currently installed version.

Click the **Update** button to uninstall the current package and install the new package.

# How does NuGetForUnity work?

NuGetForUnity loads the _NuGet.config_ file in the Unity project (automatically created if there isn't already one) in order to determine the server it should pull packages down from and push packages up to. By default, this server is set to the nuget.org package source.

_The default NuGet.config file:_

```
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="NuGet" value="http://www.nuget.org/api/v2/" />
  </packageSources>
  <activePackageSource>
    <add key="NuGet" value="http://www.nuget.org/api/v2/" />
  </activePackageSource>
  <config>
    <add key="repositoryPath" value="./Packages" />
    <add key="DefaultPushSource" value="http://www.nuget.org/api/v2/" />
  </config>
</configuration>
```

You can change this to any other NuGet server (such as NuGet.Server or ProGet - see below). The **NuGet → Reload NuGet.config** menu item is useful if you are editing the _NuGet.config_ file.

See more information about _NuGet.config_ files here: [https://docs.nuget.org/consume/nuget-config-settings](https://docs.nuget.org/consume/nuget-config-settings)

<img alt="Menu Items" src="docs/screenshots/menu_item.png" height="170px" />

NuGetForUnity installs packages into the local repository path defined in the _NuGet.config_ file (`repositoryPath`). By default, this is set to the `Assets/Packages` folder. In the _NuGet.config_ file, this can either be a full path, or it can be a relative path based on the project's Assets folder. Note: You'll probably want your Packages folder to be ignored by your version control software to prevent NuGet packages from being versioned in your repository.

When a package is installed, the _packages.config_ file in the project is automatically updated with the specific package information, as well as all of the dependencies that are also installed. This allows for the packages to be restored from scratch at any point. The `Restore` operation is automatically run every time the project is opened or the code is recompiled in the project. It can be run manually by selecting the **NuGet → Restore Packages** menu item.

<img alt="Menu Items" src="docs/screenshots/menu_item.png" height="170px" />

Note: Depending on the size and number of packages you need to install, the `Restore` operation could take a _long_ time, so please be patient. If it appears the Unity isn't launching or responding, wait a few more minutes before attempting to kill the process.

If you are interested in the process NuGetForUnity follows or you are trying to debug an issue, you can force NuGetForUnity to use verbose logging to output an increased amount of data to the Unity console. Add the line `<add key="verbose" value="true" />` to the `<config>` element in the _NuGet.config_ file. You can disable verbose logging by either setting the value to false or completely deleting the line.

The _.nupkg_ files downloaded from the NuGet server are cached locally in the current user's Application Data folder. (`C:\Users\[username]\AppData\Local\NuGet\Cache`). Packages previously installed are installed via the cache folder instead of downloading it from the server again.

# Advanced settings

## Disabel automatic referencing of assemblies

To disable the automatic referenceing of assemblies of a NuGet package you can sett the `autoReferenced` attribute of a package inside the `packages.config` to `false`. _Currently this setting is not available from UI._

```xml
<?xml version="1.0" encoding="utf-8" ?>
<packages>
    <package id="Serilog" version="2.12.0" autoReferenced="false" />
</packages>
```

When this setting is set to `false` the assemblies of the NuGet package are only referenced by Unity projects that explicitly list them inside there `*.asmdef` file.

# How do I create my own NuGet packages from within Unity?

First, you'll need to create a _.nuspec_ file that defines your package. In your Project window, right click where you want the _.nuspec_ file to go and select **NuGet → Create Nuspec File**.

<img alt="Create Nuspec Menu" src="docs/screenshots/nuspec_menu.png" height="400px" />

Select the new _.nuspec_ file and you should see something like this:

<img alt="Nuspec editor" src="docs/screenshots/nuspec_editor.png" height="500px" />

Input the appropriate information for your package (ID, Version, Author, Description, etc). Be sure to include whatever dependencies are required by your package.

Press the **Pack** button to pack your package into a _.nupkg_ file that is saved in the `C:\Users\[username]\AppData\Local\NuGet\Cache` folder.

Press the **Push** button to push your package up to the server. Be sure to set the correct API Key that give you permission to push to the server (if you server is configured to use one).

# How do I create my own NuGet server to host NuGet packages?

You can use [NuGet.Server](http://nugetserver.net/), [NuGet Gallery](https://github.com/NuGet/NuGetGallery), [ProGet](http://inedo.com/proget), etc to create your own NuGet server.

> **Note:** NuGetForUnity currently only supports NuGet Api version v1

Alternatively, you can use a "local feed" which is just a folder on your hard-drive or a network share.

Be sure to set the proper URL/path in the _NuGet.config_ file and you should be good to go!

Read more information here: [http://docs.nuget.org/create/hosting-your-own-nuget-feeds](http://docs.nuget.org/create/hosting-your-own-nuget-feeds)

# Restoring NuGet Packages over the Command Line

For those with projects using automated build solutions like [continuous integration](https://en.wikipedia.org/wiki/Continuous_integration), NuGetForUnity provides the ability to restore your NuGet packages directly from the command line without starting Unity. This is achieved using a seperate [NuGetForUnity.Cli](https://www.nuget.org/packages/NuGetForUnity.Cli) NuGet package containing a [.Net Tool](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools).

## Installation

-   As a global tool using: `dotnet tool install --global NuGetForUnity.Cli`.
-   If you don't have a tool manifest (local tool instalation context) first creat one with: `dotnet new tool-manifest`. Than install NuGetForUnity.Cli using: `dotnet tool install NuGetForUnity.Cli`.

For more information see [.Net Tool Documentaion](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools).

## Usage

Restore nuget packages of a single Unity Project: `dotnet nugetforunity restore <PROJECT_PATH>`. If installed as a global tool it can be called without the `dotnet` prefix: `nugetforunity restore <PROJECT_PATH>`.

# Common issues when installing NuGet packages

In the .Net ecosystem Unity is relatively special as it doesn't use the standard .Net runtime from Microsoft instead, it uses a fork of the [Mono](https://docs.unity3d.com/Manual/Mono.html) runtime. For some platforms Unity even uses [IL2CPP](https://docs.unity3d.com/Manual/IL2CPP.html) that compiles all C# code to C++ code. So Unity also uses a different build-system. This can lead to some issues with NuGet packages that heavily depend on the standard .Net build-system. NuGetForUnity tries to handle most of the changes needed to allow using NuGet packages inside Unity but it is not able to resolve all issues. This section contains some common issues and potential solutions.

## Version Conflicts

If two NuGet packages depend on the same package but reference different versions of it, Unity shows a error like:

```
Assembly 'Assets/Packages/System.Diagnostics.EventLog.4.7.0/lib/netstandard2.0/System.Diagnostics.EventLog.dll' will not be loaded due to errors:
System.Diagnostics.EventLog references strong named System.Security.Principal.Windows Assembly references: 4.1.1.0 Found in project: 4.1.3.0.
Assembly Version Validation can be disabled in Player Settings "Assembly Version Validation"
```

As mentioned in the message this error can be suppressed by changing a setting. Just go to Edit -> Project Settings -> Player -> Scroll down and expand "Other Settings" than uncheck "Assembly Version Validation" under the "Configuration" section.

<details>
<summary>Screenshot of Player Settings</summary>
<img als="Assembly Version Validation settings page" src="docs/screenshots/assembly-version-validation-setting.png" height="550px" />
</details>

## Missing System libraries

When targeting .Net Framework, the 'Api Compatibility Level' setting under _Edit_ -> _Project Settings_ -> _Player_ -> _Other Settings_ is set to .NET Framework, Unity doesn't include some System libraries by default. See also [Unity documentation about system libraries](https://docs.unity3d.com/Manual/dotnetProfileAssemblies.html) and [Unity documetnation about .Net Profiles](https://docs.unity3d.com/Manual/dotnetProfileSupport.html). Libraries that are not imported by default are e.g. `System.Drawing`, `System.IO.Compression`, `System.IO.Compression.FileSystem` and `System.Net.Http`. If you try to use one of this libraries you get a error like:

```
The type `HttpClient` is defined in an assembly that is not referenced. You must add a reference to assembly 'System.Net.Http, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'.
```

To import one of the System libraries and resolve the error you need to create a `csc.rsp` file with the content e.g. to import `System.Net.Http`

```
-r:System.Net.Http.dll
```

and place it inside the containing project that requires the library e.g. the `Assets` folder. It can also be placed in a folder that contains a `.asmdef` to only add the reference to the sub-project. For example NuGetForUnity also uses a `csc.rsp` file see [csc.rsp](src/NuGetForUnity/Editor/csc.rsp).
