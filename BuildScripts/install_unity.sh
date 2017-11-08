#! /bin/sh

# Based on: https://gist.github.com/andrewlord1990/5624f4a6308d41f2551d1e1dbe1e8d2d#file-install-unity-sh
# Shown in this article: https://hackernoon.com/writing-an-open-source-unity-package-877bad3c8913

# Get the URLs manually from: https://unity3d.com/get-unity/download/archive
BASE_URL=http://netstorage.unity3d.com/unity

# https://download.unity3d.com/download_unity/649f48bbbf0f/MacEditorInstaller/Unity-5.4.1f1.pkg
#HASH=649f48bbbf0f
#VERSION=5.4.1f1

# https://download.unity3d.com/download_unity/e7947df39b5c/MacEditorInstaller/Unity-5.2.0f3.pkg
HASH=e7947df39b5c
VERSION=5.2.0f3

install() {
  package=$1
  url="$BASE_URL/$HASH/$package"

  echo "Downloading from $url: "
  curl -o `basename "$package"` "$url"

  echo "Installing "`basename "$package"`
  sudo installer -dumplog -package `basename "$package"` -target /
}

install "MacEditorInstaller/Unity-$VERSION.pkg"