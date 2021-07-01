# Development Guide

This document details more advanced options for developing in this codebase. It is not quite necessary to follow it, but it is likely that you'll find something you'll need from here.

## Recommended workflow

We recommend the following overall workflow when developing for this repository:

* Fork this repository
* Always work in your fork
* Always keep your fork up to date

Before updating your fork, run this command:
```
git remote add upstream https://github.com/dotnet/fsharp.git
```

This will make management of multiple forks and your own work easier over time.

## Updating your fork

We recommend the following commands to update your fork:

```
git checkout main
git clean -xdf
git fetch upstream
git rebase upstream/main
git push
```

Or more succinctly:

```
git checkout main && git clean -xdf && git fetch upstream && git rebase upstream/main && git push
```

This will update your fork with the latest from `dotnet/fsharp` on your machine and push those updates to your remote fork.

## Developing on Windows

Install the latest released [Visual Studio](https://www.visualstudio.com/downloads/), as that is what the `main` branch's tools are synced with. Select the following workloads:

* .NET desktop development (also check F# desktop support, as this will install some legacy templates)
* Visual Studio extension development

You will also need the latest .NET 5 SDK installed from [here](https://dotnet.microsoft.com/download/dotnet/5.0).

Building is simple:

    build.cmd

Desktop tests can be run with:

    build.cmd -test -c Release

After you build the first time you can open and use this solution in Visual Studio:

    .\VisualFSharp.sln
    
If you don't have everything installed yet, you'll get prompted by Visual Studio to install a few more things. This is because we use a `.vsconfig` file that specifies all our dependencies.

If you are just developing the core compiler and library then building ``FSharp.sln`` will be enough.

We recommend installing the latest released Visual Studio and using that if you are on Windows. However, if you prefer not to do that, you will need to install the following:

* [.NET Framework 4.7.2](https://dotnet.microsoft.com/download/dotnet-framework/net472)
* [.NET Core 3 SDK](https://dotnet.microsoft.com/download/dotnet-core/3.0)

You'll need to pass an additional flag to the build script:

    build.cmd -noVisualStudio
    
You can open `FSharp.sln` in your editor of choice.

## Developing on Linux or macOS

For Linux/Mac:

    ./build.sh

Running tests:

    ./build.sh --test
    
You can then open `FSharp.sln` in your editor of choice.

## Testing from the command line

You can find all test options as separate flags. For example `build -testAll`:

```
  -testAll                  Run all tests
  -testCambridge            Run Cambridge tests
  -testCompiler             Run FSharpCompiler unit tests
  -testCompilerService      Run FSharpCompilerService unit tests
  -testDesktop              Run tests against full .NET Framework
  -testCoreClr              Run tests against CoreCLR
  -testFSharpCore           Run FSharpCore unit tests
  -testFSharpQA             Run F# Cambridge tests
  -testScripting            Run Scripting tests
  -testVs                   Run F# editor unit tests
```

Running any of the above will build the latest changes and run tests against them.

## Updating FSComp.fs, FSComp.resx and XLF

If your changes involve modifying the list of language keywords in any way, (e.g. when implementing a new keyword), the XLF localization files need to be synced with the corresponding resx files. This can be done automatically by running

    pushd src\fsharp\FSharp.Compiler.Service
    msbuild FSharp.Compiler.Service.fsproj /t:UpdateXlf
    popd

This only works on Windows/.NETStandard framework, so changing this from any other platform requires editing and syncing all of the XLF files manually.

## Updating baselines in tests

Some tests use "baseline" files.  There is sometimes a way to update these baselines en-masse in your local build,
useful when some change affects many baselines.  For example, in the 'fsharpqa' tests the baselines
are updated using scripts or utilities that allow the following environment variable to be set:

```
set TEST_UPDATE_BSL=1
```


## Developing the F# tools for Visual Studio

As you would expect, doing this requires both Windows and Visual Studio are installed.

See (DEVGUIDE.md#Developing on Windows) for instructions to install what is needed; it's the same prerequisites.

### Quickly see your changes locally

First, ensure that `VisualFSharpDebug` is the startup project.

Then, use the **f5** or **ctrl+f5** keyboard shortcuts to test your tooling changes. The former will debug a new instance of Visual Studio. The latter will launch a new instance of Visual Studio, but with your changes installed.

Alternatively, you can do this entirely via the command line if you prefer that:

    devenv.exe /rootsuffix RoslynDev

### Install your changes into a current Visual Studio installation

If you'd like to "run with your changes", you can produce a VSIX and install it into your current Visual Studio instance:

```
VSIXInstaller.exe /u:"VisualFSharp"
VSIXInstaller.exe artifacts\VSSetup\Release\VisualFSharpDebug.vsix
```

It's important to use `Release` if you want to see if your changes have had a noticeable performance impact.

### Performance and debugging

Use the `Debug` configuration to test your changes locally. It is the default. Do not use the `Release` configuration! Local development and testing of Visual Studio tooling is not designed for the `Release` configuration.

### Troubleshooting a failed build of the tools

You may run into an issue with a somewhat difficult or cryptic error message, like:

> error VSSDK1077: Unable to locate the extensions directory. "ExternalSettingsManager::GetScopePaths failed to initialize PkgDefManager for C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\Common7\IDE\devenv.exe".

Or hard crash on launch ("Unknown Error").

To fix this, delete these folders:

- `%localappdata%\Microsoft\VisualStudio\<version>_(some number here)RoslynDev`
- `%localappdata%\Microsoft\VisualStudio\<version>_(some number here)`

Where `<version>` corresponds to the latest Visual Studio version on your machine.

## Additional resources

The primary technical guide to the core compiler code is [The F# Compiler Technical Guide](https://fsharp.github.io/2015/09/29/fsharp-compiler-guide.html). Please read and contribute to that guide.

See the "Debugging The Compiler" section of this [article](https://medium.com/@willie.tetlow/f-mentorship-week-1-36f51d3812d4) for some examples.

## Addendum: configuring a proxy server

If you are behind a proxy server, NuGet client tool must be configured to use it:

See the Nuget config file documention for use with a proxy server [https://docs.microsoft.com/en-us/nuget/reference/nuget-config-file](https://docs.microsoft.com/en-us/nuget/reference/nuget-config-file)
