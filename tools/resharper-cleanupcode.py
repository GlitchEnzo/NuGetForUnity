import os
import subprocess
import sys
import time

scriptLocation = os.path.dirname(os.path.realpath(sys.argv[0]))
repositoryRoot = os.path.dirname(scriptLocation)

solutionFiles = ["src/NuGetForUnity.Tests/NuGetForUnity.Tests.sln", "src/NuGetForUnity.Cli/NuGetForUnity.Cli.sln", "src/NuGetForUnity.PluginAPI/NuGetForUnity.PluginAPI.sln"]
toolsRoot = repositoryRoot
mainPackageFolder = os.path.realpath(os.path.join(repositoryRoot, "src/NuGetForUnity")) + os.sep

subprocess.run(["dotnet", "tool", "restore"], cwd = toolsRoot, check = True)
startTime = time.time()

try:
    for solutionFile in solutionFiles:
        relativeSolutionFile = solutionFile
        solutionFile = os.path.realpath(os.path.join(repositoryRoot, solutionFile))
        if not os.path.isfile(solutionFile):
           sys.exit(f"can't find the solution file: {solutionFile}")
        solutionDir = os.path.dirname(solutionFile)
        if len(sys.argv) <= 1:
            cleanupArg = ""
        else:
            cleanupArg = "--include="
            for changedFile in sys.argv[1:]:
                # file pathes from command line args are relative to the repository root but we need them relative to the solution folder
                absoluteChangedFile = os.path.realpath(os.path.join(repositoryRoot, changedFile))
                changedFile = os.path.relpath(absoluteChangedFile, solutionDir)
                if changedFile.startswith(".."):
                    if solutionFile.endswith('NuGetForUnity.Cli.sln') or solutionFile.endswith('NuGetForUnity.PluginAPI.sln'):
                        # for NuGetForUnity.Cli and NuGetForUnity.PluginAPI we don't format external files
                        continue
                    elif not absoluteChangedFile.startswith(mainPackageFolder):
                        # for NuGetForUnity.Tests we only include external files that lie inside src/NuGetForUnity
                        continue

                    # file path can't be outside solution-directory "../" we fall-back to absolute file path
                    changedFile = absoluteChangedFile
                cleanupArg += ";" + changedFile
            if cleanupArg.endswith('='):
                # no changed file owned by solution so skip
                continue
        buildStartTime = time.time()
        subprocess.run(["dotnet", "build", "-property:RunAnalyzers=false", "-clp:ErrorsOnly", "--no-restore", solutionFile], cwd = repositoryRoot, check = True)
        clenupStartTime = time.time()
        print(f"Building solution '{relativeSolutionFile}' took: {time.strftime('%H:%M:%S', time.gmtime(clenupStartTime - buildStartTime))}")
        extentionsArg = "--eXtensions=JetBrains.Unity"
        profileArg = "--profile=Custom: Full Cleanup"
        subprocess.run(["dotnet", "jb", "cleanupcode", "--no-build", extentionsArg, profileArg, cleanupArg, solutionFile], cwd = toolsRoot, check = True)
        print(f"Cleanup of solution '{relativeSolutionFile}' took: {time.strftime('%H:%M:%S', time.gmtime(time.time() - clenupStartTime))}")

finally:
    print(f"Total resharper cleanup took: {time.strftime('%H:%M:%S', time.gmtime(time.time() - startTime))}")
