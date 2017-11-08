#! /bin/sh

# Based on: https://gist.github.com/andrewlord1990/5624f4a6308d41f2551d1e1dbe1e8d2d#file-install-unity-sh
# Shown in this article: https://hackernoon.com/writing-an-open-source-unity-package-877bad3c8913

# Get the URLs manually from: https://unity3d.com/get-unity/download/archive
#URL=https://download.unity3d.com/download_unity/649f48bbbf0f/MacEditorInstaller/Unity-5.4.1f1.pkg
URL=https://download.unity3d.com/download_unity/e7947df39b5c/MacEditorInstaller/Unity-5.2.0f3.pkg
PACKAGE=Unity-5.2.0f3.pkg

echo "Downloading from $URL: "
curl -o `basename "$PACKAGE"` "$URL"

echo "Installing "`basename "$PACKAGE"`
sudo installer -dumplog -package `basename "$PACKAGE"` -target /