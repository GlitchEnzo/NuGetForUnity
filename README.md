# What is NuGetForUnity?
NuGetForUnity is a NuGet client built from scratch to run inside the Unity Editor.  NuGet is a package management system which makes it easy to create packages that are distributed on a server and consumed by users.  NuGet supports [sematic versioning](http://semver.org/) for packages as well as dependencies on other packages.

You can learn more about NuGet here: [nuget.org](https://www.nuget.org/)

NuGetForUnity provides a visual editor window to see available packages on the server, see installed packages, and see available package updates.  A visual interface is also provided to create and edit *.nuspec* files in order to define and publish your own NuGet packages from within Unity.

![](screenshots/online.png?raw=true)

# How do I install NuGetForUnity?
Install the provided Unity package into your Unity project.  Located [here](https://github.com/GlitchEnzo/NuGetForUnity/releases).

# How do I use NuGetForUnity?
To launch, select **NuGet → Manage NuGet Packages**

![](screenshots/menu_item.png?raw=true)

After several seconds (it can take some time to query the server for packages), you should see a window like this:

![](screenshots/online.png?raw=true)

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

![](screenshots/installed.png?raw=true)

Click the **Uninstall** button to uninstall the package.

The **Updates** tab shows the packages currently installed that have updates available on the server.

![](screenshots/updates.png?raw=true)

The version in brackets on the left is the new version number.  The version in brackets in the **Update** button is the currently installed version.

Click the **Update** button to uninstall the current package and install the new package.

# How does NuGetForUnity work?
NuGetForUnity loads the *NuGet.config* file in the Unity project (automatically created if there isn't already one) in order to determine the server it should pull packages down from and push packages up to.  By default, this server is set to the nuget.org package source.  

*The default NuGet.config file:*
```
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
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

You can change this to any other NuGet server (such as NuGet.Server or ProGet - see below).  The **NuGet → Reload NuGet.config** menu item is useful if you are editing the *NuGet.config* file.

See more information about *NuGet.config* files here: [https://docs.nuget.org/consume/nuget-config-settings](https://docs.nuget.org/consume/nuget-config-settings)

![](screenshots/menu_item.png?raw=true)

NuGetForUnity installs packages into the local repository path defined in the *NuGet.config* file (`repositoryPath`).  By default, this is set to the `Assets/Packages` folder.  In the *NuGet.config* file, this can either be a full path, or it can be a relative path based on the project's Assets folder.  Note:  You'll probably want your Packages folder to be ignored by your version control software to prevent NuGet packages from being versioned in your repository.

When a package is installed, the *packages.config* file in the project is automatically updated with the specific package information, as well as all of the dependencies that are also installed.  This allows for the packages to be restored from scratch at any point.  The `Restore` operation is automatically run every time the project is opened or the code is recompiled in the project.  It can be run manually by selecting the **NuGet → Restore Packages** menu item. 

![](screenshots/menu_item.png?raw=true)

Note: Depending on the size and number of packages you need to install, the `Restore` operation could take a _long_ time, so please be patient. If it appears the Unity isn't launching or responding, wait a few more minutes before attempting to kill the process.

If you are interested in the process NuGetForUnity follows or you are trying to debug an issue, you can force NuGetForUnity to use verbose logging to output an increased amount of data to the Unity console.  Add the line `<add key="verbose" value="true" />` to the `<config>` element in the *NuGet.config* file.  You can disable verbose logging by either setting the value to false or completely deleting the line.

The *.nupkg* files downloaded from the NuGet server are cached locally in the current user's Application Data folder.  (`C:\Users\[username]\AppData\Local\NuGet\Cache`).  Packages previously installed are installed via the cache folder instead of downloading it from the server again.

# How do I create my own NuGet packages from within Unity?
First, you'll need to create a *.nuspec* file that defines your package.  In your Project window, right click where you want the *.nuspec* file to go and select **NuGet → Create Nuspec File**.

![](screenshots/nuspec_menu.png?raw=true)

Select the new *.nuspec* file and you should see something like this:

![](screenshots/nuspec_editor.png?raw=true)

Input the appropriate information for your package (ID, Version, Author, Description, etc).  Be sure to include whatever dependencies are required by your package.

Press the **Pack** button to pack your package into a *.nupkg* file that is saved in the `C:\Users\[username]\AppData\Local\NuGet\Cache` folder.

Press the **Push** button to push your package up to the server.  Be sure to set the correct API Key that give you permission to push to the server (if you server is configured to use one).

# How do I create my own NuGet server to host NuGet packages?
You can use [NuGet.Server](http://nugetserver.net/), [NuGet Gallery](https://github.com/NuGet/NuGetGallery), [ProGet](http://inedo.com/proget), etc to create your own NuGet server.

Alternatively, you can use a "local feed" which is just a folder on your hard-drive or a network share.

Be sure to set the proper URL/path in the *NuGet.config* file and you should be good to go!

Read more information here: [http://docs.nuget.org/create/hosting-your-own-nuget-feeds](http://docs.nuget.org/create/hosting-your-own-nuget-feeds)

# Restoring NuGet Packages over the Command Line
For those with projects using automated build solutions like [continuous integration](https://en.wikipedia.org/wiki/Continuous_integration), NuGetForUnity provides the ability to restore your NuGet packages directly from the command line. On Windows, this can be done automatically by executing [RestoreNugetPackages.bat](https://github.com/GlitchEnzo/NuGetForUnity/blob/master/RestoreNugetPackages.bat) directly from within your Unity project directory, given that the NuGetForUnity Asset is already installed in your project.

This can also be done manually over the command line as shown here:

- Windows: `$ \progra~1\Unity\Editor\Unity.exe -quit -batchmode -projectPath <yourProjectDirHere> -executeMethod NugetForUnity.NugetHelper.Restore`
- Mac: `$ /Applications/Unity/Unity.app/Contents/MacOS/Unity -quit -batchmode -projectPath <yourProjectDirHere> -executeMethod NugetForUnity.NugetHelper.Restore`
