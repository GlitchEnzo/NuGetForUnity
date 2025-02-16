<!-- omit in toc -->

# Contributing to NuGetForUnity

First off, thanks for taking the time to contribute! ❤️

All types of contributions are encouraged and valued. See the [Table of Contents](#table-of-contents) for different ways to help and details about how this project handles them. Please make sure to read the relevant section before making your contribution. It will make it a lot easier for us maintainers and smooth out the experience for all involved. The community looks forward to your contributions. 🎉

> And if you like the project, but just don't have time to contribute, that's fine. There are other easy ways to support the project and show your appreciation, which we would also be very happy about:
>
> - Star the project
> - Tweet about it
> - Refer this project in your project's readme
> - Mention the project at local meetups and tell your friends/colleagues

<!-- omit in toc -->

## Table of Contents

- [I Have a Question](#i-have-a-question)
- [I Want To Contribute](#i-want-to-contribute)
- [Reporting Bugs](#reporting-bugs)
- [Suggesting Enhancements](#suggesting-enhancements)
- [Pull Requests](#pull-requests)
- [Development Environment Setup](#development-environment-setup)
- [Running Unit Tests](#running-unit-tests)
- [Code Style](#code-style)
- [Auto Formatter](#auto-formatter)

## I Have a Question

> If you want to ask a question, we assume that you have read the available [Documentation (README.md)](https://github.com/GlitchEnzo/NuGetForUnity#readme).

Before you ask a question, it is best to search for existing [Issues](https://github.com/GlitchEnzo/NuGetForUnity/issues) that might help you. In case you have found a suitable issue and still need clarification, you can write your question in this issue. It is also advisable to search the internet for answers first.

If you then still feel the need to ask a question and need clarification, we recommend the following:

- Open an [Issue](https://github.com/GlitchEnzo/NuGetForUnity/issues/new).
- Provide as much context as you can about what you're running into.
- Provide project, Unity and package versions.

We will then take care of the issue as soon as possible.

## I Want To Contribute

> ### Legal Notice <!-- omit in toc -->
>
> When contributing to this project, you must agree that you have authored 100% of the content, that you have the necessary rights to the content and that the content you contribute may be provided under the project license.

### Reporting Bugs

<!-- omit in toc -->

#### Before Submitting a Bug Report

A good bug report shouldn't leave others needing to chase you up for more information. Therefore, we ask you to investigate carefully, collect information and describe the issue in detail in your report. Please complete the following steps in advance to help us fix any potential bug as fast as possible.

- Make sure that you are using the latest version.
- Determine if your bug is related to on of the issues mentioned under [Common issues when installing NuGet packages](https://github.com/GlitchEnzo/NuGetForUnity#common-issues-when-installing-nuget-packages).
- To see if other users have experienced (and potentially already solved) the same issue you are having, check if there is not already a bug report existing for your bug or error in the [Issues](https://github.com/GlitchEnzo/NuGetForUnity/issues).
- Also make sure to search the internet (including Stack Overflow) to see if users outside of the GitHub community have discussed the issue.
- Collect information about the bug:
- Get logs produced by enabling verbose logging in the [NuGetForUnity settings](docs/screenshots/preferences.png)
- Can you reliably reproduce the issue? And can you also reproduce it with older versions?

<!-- omit in toc -->

#### How Do I Submit a Good Bug Report?

We use GitHub issues to track bugs and errors. If you run into an issue with the project:

- Open an [Issue](https://github.com/GlitchEnzo/NuGetForUnity/issues/new).
- Explain the behavior you would expect and the actual behavior.
- Please provide as much context as possible and describe the _reproduction steps_ that someone else can follow to recreate the issue on their own.
- Provide the information you collected in the previous section.

We will then take care of the issue as soon as possible.

### Suggesting Enhancements

This section guides you through submitting an enhancement suggestion for NuGetForUnity, **including completely new features and minor improvements to existing functionality**. Following these guidelines will help maintainers and the community to understand your suggestion and find related suggestions.

<!-- omit in toc -->

#### Before Submitting an Enhancement

- Make sure that you are using the latest version.
- Read the [Documentation (README.md)](https://github.com/GlitchEnzo/NuGetForUnity#readme) carefully and find out if the functionality is already covered, maybe by an individual configuration.
- Perform a [search](https://github.com/GlitchEnzo/NuGetForUnity/issues) to see if the enhancement has already been suggested. If it has, add a comment to the existing issue instead of opening a new one.
- Find out whether your idea fits with the scope and aims of the project. It's up to you to make a strong case to convince the project's developers of the merits of this feature.

<!-- omit in toc -->

#### How Do I Submit a Good Enhancement Suggestion?

Enhancement suggestions are tracked as [GitHub issues](https://github.com/GlitchEnzo/NuGetForUnity/issues).

- Use a **clear and descriptive title** for the issue to identify the suggestion.
- Provide a **step-by-step description of the suggested enhancement** in as many details as possible.
- **Describe the current behavior** and **explain which behavior you expected to see instead** and why. At this point you can also tell which alternatives do not work for you.
- **Explain why this enhancement would be useful** to most NuGetForUnity users. You may also want to point out the other projects that solved it better and which could serve as inspiration.

### Pull Requests

We are using pull requests to add new features, no direct commits to master. To develop a new feature:

- First create a branch on your own fork.
- When you finish the development create a [Pull Request](https://github.com/GlitchEnzo/NuGetForUnity/pulls).
- We will then review the changes.
- The [GitHub Action](.github/workflows/main.yml) will enure everything builds, the unit tests are running successfully and some [test projects](src/TestProjects) that include NuGet packages imported using NuGetForUnity build successfully.
- The [GitHub Action](.github/workflows/main.yml) also creates a pre-release `.unitypackage` from the build to be able to import it in any Unity project without needing to wait for a new official release.
- If everything is working and fits our [Code Style](#code-style) we will merge the pull request. When merging please use the `Squash and merge` merge strategy to keep the commit history clean.

### Steps to release a new version

1. Update Version Information:

    - Run the script `tools/update-version-number.ps1` to update the version numbers
    - Edit `src/NuGetForUnity/package.json` to update the `"version"` field.
    - Edit `src/NuGetForUnity/Editor/Ui/NugetPreferences.cs` to update the `NuGetForUnityVersion` constant.

2. Create a Release (after merging the version changes):

    - Go to the GitHub Releases page.
    - Click "Draft a new release" and use the auto-generated release notes.

3. Upload Unity Package:

    - Download the generated `unitypackage` from the GitHub Action.
    - Manually upload it to the release page.

### Development Environment Setup

You can use any version of Unity to develop NuGetForUnity but you should only use features available in Unity version 2018.3+. The easiest way to edit and test code using a newer Unity version is by installing NuGetForUnity as a local package like in [TestProjects/ImportAndUseNuGetPackages](src/TestProjects/ImportAndUseNuGetPackages).

### Running Unit Tests

To run Unit-Tests you need Unity version 2018.4.30f1 as we support Unity version 2018.3+. Then open the [NuGetForUnity.Tests](src/NuGetForUnity.Tests) project and run the tests using Unity Test Runner. Unit Tests are also run for each pull request using a [GitHub Action](.github/workflows/main.yml).

### Code Style

The code style of NuGetForUnity is based on the [Microsoft C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions), but with some slight adjustments so we don't use prefixes for naming fields `private IWorkerQueue workerQueue;` instead of `private IWorkerQueue _workerQueue;`. The naming convention and some other coding conventions are checked using code analyzers (e.g. [StyleCop](https://www.nuget.org/packages/StyleCop.Analyzers)) or the IDE. The configuration for them are in the [.editorconfig](.editorconfig) and some are inside [Directory.Build.props](src/Directory.Build.props).

#### Auto Formatter

To ensue a consistent code style we use [pre-commit](https://pre-commit.com/). It includes running [ReSharper Command Line Tools (JetBrains.ReSharper.GlobalTools)](https://www.jetbrains.com/resharper/features/command-line.html) to format C# code and ensures file-layout (e.g. places fields at the top of the file). To install the pre-commit hook just use the following commands or follow the documentation at [pre-commit](https://pre-commit.com/).

```PowerShell
# Install the pre-commit tool, needs python including pip.
pip install pre-commit

# Enable the pre-commit hook so the auto-formatter runes on every commit. Need to be run inside the repository root.
pre-commit install
```

The auto-formatter can also be run manually using [format-staged.ps1](tools/format-staged.ps1) or [format-all.ps1](tools/format-all.ps1).
