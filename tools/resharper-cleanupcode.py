import os
import subprocess
import sys
import time

scriptLocation = os.path.dirname(os.path.realpath(sys.argv[0]))
repositoryRoot = os.path.dirname(scriptLocation)

solutionFiles = ["src/NuGetForUnity.Tests/NuGetForUnity.Tests.sln"]
toolsRoot = repositoryRoot

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
                    # file path can't be outside solution-directory "../" we fall-back to absolute file path
                    changedFile = absoluteChangedFile
                cleanupArg += ";" + changedFile
        buildStartTime = time.time()
        subprocess.run(["dotnet", "build", "-property:RunAnalyzers=false", "-clp:ErrorsOnly", solutionFile], cwd = repositoryRoot, check = True)
        clenupStartTime = time.time()
        print(f"Building solution '{relativeSolutionFile}' took: {time.strftime('%H:%M:%S', time.gmtime(clenupStartTime - buildStartTime))}")
        extentionsArg = "--eXtensions=JetBrains.Unity"
        profileArg = ""
        subprocess.run(["dotnet", "jb", "cleanupcode", extentionsArg, profileArg, cleanupArg, solutionFile], cwd = toolsRoot, check = True)
        print(f"Cleanup of solution '{relativeSolutionFile}' took: {time.strftime('%H:%M:%S', time.gmtime(time.time() - clenupStartTime))}")

finally:
    print(f"Total resharper cleanup took: {time.strftime('%H:%M:%S', time.gmtime(time.time() - startTime))}")
