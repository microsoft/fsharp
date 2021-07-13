#
# This script controls the F# build process. This encompasess everything from build, testing to
# publishing of NuGet packages. The intent is to structure it to allow for a simple flow of logic
# between the following phases:
#
#   - restore
#   - build
#   - sign
#   - pack
#   - test
#   - publish
#
# Each of these phases has a separate command which can be executed independently. For instance
# it's fine to call `build.ps1 -build -testDesktop` followed by repeated calls to
# `.\build.ps1 -testDesktop`.

[CmdletBinding(PositionalBinding = $false)]
param (
    [string][Alias('c')]$configuration = "Debug",
    [string][Alias('v')]$verbosity = "m",
    [string]$msbuildEngine = "vs",

    # Actions
    [switch][Alias('r')]$restore,
    [switch]$noRestore,
    [switch][Alias('b')]$build,
    [switch]$rebuild,
    [switch]$sign,
    [switch]$noSign,
    [switch]$pack,
    [switch]$publish,
    [switch]$launch,
    [switch]$help,

    # Options
    [switch][Alias('proto')]$bootstrap,
    [string]$bootstrapConfiguration = "Proto",
    [string]$bootstrapTfm = "net472",
    [switch][Alias('bl')]$binaryLog = $true,
    [switch][Alias('nobl')]$excludeCIBinaryLog = $false,
    [switch][Alias('nolog')]$noBinaryLog = $false,
    [switch]$ci,
    [switch]$official,
    [switch]$procdump,
    [switch]$deployExtensions,
    [switch]$prepareMachine,
    [switch]$useGlobalNuGetCache = $true,
    [switch]$warnAsError = $true,
    [switch][Alias('test')]$testDesktop,
    [switch]$testCoreClr,
    [switch]$testCambridge,
    [switch]$testCompiler,
    [switch]$testCompilerService,
    [switch]$testFSharpCore,
    [switch]$testFSharpQA,
    [switch]$testScripting,
    [switch]$testVs,
    [switch]$testAll,
    [switch]$testpack,
    [string]$officialSkipTests = "false",
    [switch]$noVisualStudio,
    [switch]$sourceBuild,

    [parameter(ValueFromRemainingArguments = $true)][string[]]$properties)

Set-StrictMode -version 2.0
$ErrorActionPreference = "Stop"
$BuildCategory = ""
$BuildMessage = ""

function Print-Usage() {
    Write-Host "Common settings:"
    Write-Host "  -configuration <value>    Build configuration: 'Debug' or 'Release' (short: -c)"
    Write-Host "  -verbosity <value>        Msbuild verbosity: q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic]"
    Write-Host "  -deployExtensions         Deploy built vsixes"
    Write-Host "  -binaryLog                Create MSBuild binary log (short: -bl)"
    Write-Host "  -noLog                    Turn off logging (short: -nolog)"
    Write-Host "  -excludeCIBinaryLog       When running on CI, allow no binary log (short: -nobl)"
    Write-Host ""
    Write-Host "Actions:"
    Write-Host "  -restore                  Restore packages (short: -r)"
    Write-Host "  -norestore                Don't restore packages"
    Write-Host "  -build                    Build main solution (short: -b)"
    Write-Host "  -rebuild                  Rebuild main solution"
    Write-Host "  -pack                     Build NuGet packages, VS insertion manifests and installer"
    Write-Host "  -sign                     Sign our binaries"
    Write-Host "  -publish                  Publish build artifacts (e.g. symbols)"
    Write-Host "  -launch                   Launch Visual Studio in developer hive"
    Write-Host "  -help                     Print help and exit"
    Write-Host ""
    Write-Host "Test actions"
    Write-Host "  -testAll                  Run all tests"
    Write-Host "  -testCambridge            Run Cambridge tests"
    Write-Host "  -testCompiler             Run FSharpCompiler unit tests"
    Write-Host "  -testCompilerService      Run FSharpCompilerService unit tests"
    Write-Host "  -testDesktop              Run tests against full .NET Framework"
    Write-Host "  -testCoreClr              Run tests against CoreCLR"
    Write-Host "  -testFSharpCore           Run FSharpCore unit tests"
    Write-Host "  -testFSharpQA             Run F# Cambridge tests"
    Write-Host "  -testScripting            Run Scripting tests"
    Write-Host "  -testVs                   Run F# editor unit tests"
    Write-Host "  -testpack                 Verify built packages"
    Write-Host "  -officialSkipTests <bool> Set to 'true' to skip running tests"
    Write-Host ""
    Write-Host "Advanced settings:"
    Write-Host "  -ci                       Set when running on CI server"
    Write-Host "  -official                 Set when building an official build"
    Write-Host "  -bootstrap                Build using a bootstrap compiler"
    Write-Host "  -msbuildEngine <value>    Msbuild engine to use to run build ('dotnet', 'vs', or unspecified)."
    Write-Host "  -procdump                 Monitor test runs with procdump"
    Write-Host "  -prepareMachine           Prepare machine for CI run, clean up processes after build"
    Write-Host "  -useGlobalNuGetCache      Use global NuGet cache."
    Write-Host "  -noVisualStudio           Only build fsc and fsi as .NET Core applications. No Visual Studio required. '-configuration', '-verbosity', '-norestore', '-rebuild' are supported."
    Write-Host "  -sourceBuild              Simulate building for source-build."
    Write-Host ""
    Write-Host "Command line arguments starting with '/p:' are passed through to MSBuild."
}

# Process the command line arguments and establish defaults for the values which are not
# specified.
function Process-Arguments() {
    if ($help -or (($properties -ne $null) -and ($properties.Contains("/help") -or $properties.Contains("/?")))) {
        Print-Usage
        exit 0
    }

    $script:nodeReuse = $False;

    if ($testAll) {
        $script:testDesktop = $True
        $script:testCoreClr = $True
        $script:testFSharpQA = $True
        $script:testVs = $True
    }

    if ([System.Boolean]::Parse($script:officialSkipTests)) {
        $script:testAll = $False
        $script:testCambridge = $False
        $script:testCompiler = $False
        $script:testCompilerService = $False
        $script:testDesktop = $False
        $script:testCoreClr = $False
        $script:testFSharpCore = $False
        $script:testFSharpQA = $False
        $script:testVs = $False
        $script:testpack = $False
    }

    if ($noRestore) {
        $script:restore = $False;
    }

    if ($noSign) {
        $script:sign = $False;
    }

    if ($testpack) {
        $script:pack = $True;
    }

    if ($sourceBuild) {
        $script:testpack = $False;
    }

    if ($noBinaryLog) {
        $script:binaryLog = $False;
    }

    foreach ($property in $properties) {
        if (!$property.StartsWith("/p:", "InvariantCultureIgnoreCase")) {
            Write-Host "Invalid argument: $property"
            Print-Usage
            exit 1
        }
    }
}

function Update-Arguments() {
    if ($script:noVisualStudio) {
        $script:bootstrapTfm = "net5.0"
        $script:msbuildEngine = "dotnet"
    }

    if ($bootstrapTfm -eq "net5.0") {
        if (-Not (Test-Path "$ArtifactsDir\Bootstrap\fsc\fsc.runtimeconfig.json")) {
            $script:bootstrap = $True
        }
    }
    else {
        if (-Not (Test-Path "$ArtifactsDir\Bootstrap\fsc\fsc.exe") -or (Test-Path "$ArtifactsDir\Bootstrap\fsc\fsc.runtimeconfig.json")) {
            $script:bootstrap = $True
        }
    }
}

function BuildSolution([string] $solutionName) {
    Write-Host "${solutionName}:"

    $bl = if ($binaryLog) { "/bl:" + (Join-Path $LogDir "Build.binlog") } else { "" }

    $projects = Join-Path $RepoRoot  $solutionName
    $officialBuildId = if ($official) { $env:BUILD_BUILDNUMBER } else { "" }
    $toolsetBuildProj = InitializeToolset
    $quietRestore = !$ci
    $testTargetFrameworks = if ($testCoreClr) { "net5.0" } else { "" }

    # Do not set the property to true explicitly, since that would override value projects might set.
    $suppressExtensionDeployment = if (!$deployExtensions) { "/p:DeployExtension=false" } else { "" }

    MSBuild $toolsetBuildProj `
        $bl `
        /p:Configuration=$configuration `
        /p:Projects=$projects `
        /p:RepoRoot=$RepoRoot `
        /p:Restore=$restore `
        /p:Build=$build `
        /p:Rebuild=$rebuild `
        /p:Pack=$pack `
        /p:Sign=$sign `
        /p:Publish=$publish `
        /p:ContinuousIntegrationBuild=$ci `
        /p:OfficialBuildId=$officialBuildId `
        /p:QuietRestore=$quietRestore `
        /p:QuietRestoreBinaryLog=$binaryLog `
        /p:TestTargetFrameworks=$testTargetFrameworks `
        /p:DotNetBuildFromSource=$sourceBuild `
        /v:$verbosity `
        $suppressExtensionDeployment `
        @properties
}

function TestAndAddToPath([string] $testPath) {
    if (Test-Path $testPath) {
        $env:PATH = "$testPath;$env:PATH"
        Write-Host "Added [$testPath] to the path."
    }
}

function UpdatePath() {
    # add highest framework dir
    $subdir = ""
    foreach ($child in Get-ChildItem "$env:WINDIR\Microsoft.NET\Framework\v4.0.?????") {
        $subdir = $child
    }
    TestAndAddToPath $subdir

    # add windows SDK dir for ildasm.exe
    foreach ($child in Get-ChildItem "${env:ProgramFiles(x86)}\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.* Tools") {
        $subdir = $child
    }
    TestAndAddToPath $subdir

    TestAndAddToPath "$ArtifactsDir\bin\fsc\$configuration\net472"
    TestAndAddToPath "$ArtifactsDir\bin\fsiAnyCpu\$configuration\net472"
}

function VerifyAssemblyVersionsAndSymbols() {
    $assemblyVerCheckPath = Join-Path $ArtifactsDir "Bootstrap\AssemblyCheck\AssemblyCheck.dll"

    # Only verify versions on CI or official build
    if ($ci -or $official) {
        $dotnetPath = InitializeDotNetCli
        $dotnetExe = Join-Path $dotnetPath "dotnet.exe"
        Exec-Console $dotnetExe """$assemblyVerCheckPath"" ""$ArtifactsDir"""
    }
}

function TestUsingMSBuild([string] $testProject, [string] $targetFramework, [string]$testadapterpath, [boolean] $noTestFilter = $false) {
    $dotnetPath = InitializeDotNetCli
    $dotnetExe = Join-Path $dotnetPath "dotnet.exe"
    $projectName = [System.IO.Path]::GetFileNameWithoutExtension($testProject)
    $testLogPath = "$ArtifactsDir\TestResults\$configuration\${projectName}_$targetFramework.xml"
    $testBinLogPath = "$LogDir\${projectName}_$targetFramework.binlog"
    $args = "test $testProject -c $configuration -f $targetFramework -v n --test-adapter-path $testadapterpath --logger ""nunit;LogFilePath=$testLogPath"" /bl:$testBinLogPath"
    $args += " --blame --results-directory $ArtifactsDir\TestResults\$configuration"

    if (-not $noVisualStudio -or $norestore) {
        $args += " --no-restore"
    }

    if (-not $noVisualStudio) {
        $args += " --no-build"
    }

    if ($env:RunningAsPullRequest -ne "true" -and $noTestFilter -eq $false) {
        $args += " --filter TestCategory!=PullRequest"
    }

    Write-Host("$args")
    Exec-Console $dotnetExe $args
}

function TestUsingXUnit([string] $testProject, [string] $targetFramework, [string]$testadapterpath) {
    TestUsingMsBuild -testProject $testProject -targetFramework $targetFramework -testadapterpath $testadapterpath -noTestFilter $true
}

function TestUsingNUnit([string] $testProject, [string] $targetFramework, [string]$testadapterpath) {
    TestUsingMsBuild -testProject $testProject -targetFramework $targetFramework -testadapterpath $testadapterpath -noTestFilter $false
}

function Prepare-TempDir() {
    Copy-Item (Join-Path $RepoRoot "tests\Resources\Directory.Build.props") $TempDir
    Copy-Item (Join-Path $RepoRoot "tests\Resources\Directory.Build.targets") $TempDir
}

function DownloadDotnetFrameworkSdk() {
    $dlTempPath = [System.IO.Path]::GetTempPath()
    $dlRandomFile = [System.IO.Path]::GetRandomFileName()
    $net48Dir = Join-Path $dlTempPath $dlRandomFile
    Create-Directory $net48Dir

    $net48Exe = Join-Path $net48Dir "ndp48-devpack-enu.exe"
    $dlLogFilePath = Join-Path $LogDir "dotnet48.install.log"
    Invoke-WebRequest "https://go.microsoft.com/fwlink/?linkid=2088517" -OutFile $net48Exe

    Write-Host "Exec-Console $net48Exe /install /quiet /norestart /log $dlLogFilePath"
    Exec-Console $net48Exe "/install /quiet /norestart /log $dlLogFilePath"
}

function Test-IsAdmin {
    ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")
}

function TryDownloadDotnetFrameworkSdk() {
    # If we are not running as admin user, don't bother grabbing ndp sdk -- since we don't need sn.exe
    $isAdmin = Test-IsAdmin
    Write-Host "TryDownloadDotnetFrameworkSdk -- Test-IsAdmin = '$isAdmin'"
    if ($isAdmin -eq $true) {
        # Get program files(x86) location
        if (${env:ProgramFiles(x86)} -eq $null) {
            $programFiles = $env:ProgramFiles
        }
        else {
            $programFiles = ${env:ProgramFiles(x86)}
        }

        # Get windowsSDK location for x86
        $windowsSDK_ExecutablePath_x86 = $env:WindowsSDK_ExecutablePath_x86
        $newWindowsSDK_ExecutablePath_x86 = Join-Path "$programFiles" "Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools"

        if ($windowsSDK_ExecutablePath_x86 -eq $null) {
            $snPathX86 = Join-Path $newWindowsSDK_ExecutablePath_x86 "sn.exe"
        }
        else {
            $snPathX86 = Join-Path $windowsSDK_ExecutablePath_x86 "sn.exe"
            $snPathX86Exists = Test-Path $snPathX86 -PathType Leaf
            if ($snPathX86Exists -ne $true) {
                $windowsSDK_ExecutablePath_x86 = null
                $snPathX86 = Join-Path $newWindowsSDK_ExecutablePath_x86 "sn.exe"
            }
        }

        $windowsSDK_ExecutablePath_x64 = $env:WindowsSDK_ExecutablePath_x64
        $newWindowsSDK_ExecutablePath_x64 = Join-Path "$programFiles" "Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\x64"

        if ($windowsSDK_ExecutablePath_x64 -eq $null) {
            $snPathX64 = Join-Path $newWindowsSDK_ExecutablePath_x64 "sn.exe"
        }
        else {
            $snPathX64 = Join-Path $windowsSDK_ExecutablePath_x64 "sn.exe"
            $snPathX64Exists = Test-Path $snPathX64 -PathType Leaf
            if ($snPathX64Exists -ne $true) {
                $windowsSDK_ExecutablePath_x86 = null
                $snPathX64 = Join-Path $newWindowsSDK_ExecutablePath_x64 "sn.exe"
            }
        }

        $snPathX86Exists = Test-Path $snPathX86 -PathType Leaf
        Write-Host "pre-dl snPathX86Exists : $snPathX86Exists - '$snPathX86'"
        if ($snPathX86Exists -ne $true) {
            DownloadDotnetFrameworkSdk
        }

        $snPathX86Exists = Test-Path $snPathX86 -PathType Leaf
        if ($snPathX86Exists -eq $true) {
            if ($windowsSDK_ExecutablePath_x86 -ne $newWindowsSDK_ExecutablePath_x86) {
                $windowsSDK_ExecutablePath_x86 = $newWindowsSDK_ExecutablePath_x86
                # x86 environment variable
                Write-Host "set WindowsSDK_ExecutablePath_x86=$WindowsSDK_ExecutablePath_x86"
                [System.Environment]::SetEnvironmentVariable("WindowsSDK_ExecutablePath_x86", "$newWindowsSDK_ExecutablePath_x86", [System.EnvironmentVariableTarget]::Machine)
                $env:WindowsSDK_ExecutablePath_x86 = $newWindowsSDK_ExecutablePath_x86
            }
        }

        # Also update environment variable for x64
        $snPathX64Exists = Test-Path $snPathX64 -PathType Leaf
        if ($snPathX64Exists -eq $true) {
            if ($windowsSDK_ExecutablePath_x64 -ne $newWindowsSDK_ExecutablePath_x64) {
                $windowsSDK_ExecutablePath_x64 = $newWindowsSDK_ExecutablePath_x64
                # x64 environment variable
                Write-Host "set WindowsSDK_ExecutablePath_x64=$WindowsSDK_ExecutablePath_x64"
                [System.Environment]::SetEnvironmentVariable("WindowsSDK_ExecutablePath_x64", "$newWindowsSDK_ExecutablePath_x64", [System.EnvironmentVariableTarget]::Machine)
                $env:WindowsSDK_ExecutablePath_x64 = $newWindowsSDK_ExecutablePath_x64
            }
        }
    }
}

function EnablePreviewSdks() {
    if (Test-Path variable:global:_MSBuildExe) {
        return
    }
    $vsInfo = LocateVisualStudio
    if ($vsInfo -eq $null) {
        # Preview SDKs are allowed when no Visual Studio instance is installed
        return
    }

    $vsId = $vsInfo.instanceId
    $vsMajorVersion = $vsInfo.installationVersion.Split('.')[0]

    $instanceDir = Join-Path ${env:USERPROFILE} "AppData\Local\Microsoft\VisualStudio\$vsMajorVersion.0_$vsId"
    Create-Directory $instanceDir
    $sdkFile = Join-Path $instanceDir "sdk.txt"
    'UsePreviews=True' | Set-Content $sdkFile
}

try {
    $script:BuildCategory = "Build"
    $script:BuildMessage = "Failure preparing build"

    [System.Environment]::SetEnvironmentVariable('DOTNET_ROLL_FORWARD_TO_PRERELEASE', '1', [System.EnvironmentVariableTarget]::User)

    Process-Arguments

    . (Join-Path $PSScriptRoot "build-utils.ps1")

    Update-Arguments

    Push-Location $RepoRoot

    Get-ChildItem ENV: | Sort-Object Name
    Write-Host ""

    if ($ci) {
        Prepare-TempDir
        EnablePreviewSdks
    }

    $buildTool = InitializeBuildTool
    $toolsetBuildProj = InitializeToolset
    TryDownloadDotnetFrameworkSdk

    $dotnetPath = InitializeDotNetCli
    $env:DOTNET_ROOT = "$dotnetPath"
    Get-Item -Path Env:

    if ($bootstrap) {
        $script:BuildMessage = "Failure building bootstrap compiler"
        $bootstrapDir = Make-BootstrapBuild
    }

    $script:BuildMessage = "Failure building product"
    if ($restore -or $build -or $rebuild -or $pack -or $sign -or $publish) {
        if ($noVisualStudio) {
            BuildSolution "FSharp.sln"
        }
        else {
            BuildSolution "VisualFSharp.sln"
        }
    }

    if ($build) {
        VerifyAssemblyVersionsAndSymbols
    }

    $script:BuildCategory = "Test"
    $script:BuildMessage = "Failure running tests"
    $desktopTargetFramework = "net472"
    $coreclrTargetFramework = "net5.0"

    if ($testDesktop) {
        TestUsingXUnit -testProject "$RepoRoot\tests\FSharp.Compiler.ComponentTests\FSharp.Compiler.ComponentTests.fsproj" -targetFramework $desktopTargetFramework -testadapterpath "$ArtifactsDir\bin\FSharp.Compiler.ComponentTests\" -noTestFilter $true
        TestUsingNUnit -testProject "$RepoRoot\tests\FSharp.Compiler.UnitTests\FSharp.Compiler.UnitTests.fsproj" -targetFramework $desktopTargetFramework  -testadapterpath "$ArtifactsDir\bin\FSharp.Compiler.UnitTests\"
        TestUsingNUnit -testProject "$RepoRoot\tests\FSharp.Compiler.Service.Tests\FSharp.Compiler.Service.Tests.fsproj" -targetFramework $desktopTargetFramework -testadapterpath "$ArtifactsDir\bin\FSharp.Compiler.Service.Tests\"
        TestUsingXUnit -testProject "$RepoRoot\tests\FSharp.Compiler.Private.Scripting.UnitTests\FSharp.Compiler.Private.Scripting.UnitTests.fsproj" -targetFramework $desktopTargetFramework  -testadapterpath "$ArtifactsDir\bin\FSharp.Compiler.Private.Scripting.UnitTests\"
        TestUsingNUnit -testProject "$RepoRoot\tests\FSharp.Build.UnitTests\FSharp.Build.UnitTests.fsproj" -targetFramework $desktopTargetFramework -testadapterpath "$ArtifactsDir\bin\FSharp.Build.UnitTests\"
        TestUsingXUnit -testProject "$RepoRoot\tests\FSharp.Core.UnitTests\FSharp.Core.UnitTests.fsproj" -targetFramework $desktopTargetFramework -testadapterpath "$ArtifactsDir\bin\FSharp.Core.UnitTests\"
        TestUsingNUnit -testProject "$RepoRoot\tests\fsharp\FSharpSuite.Tests.fsproj" -targetFramework $desktopTargetFramework -testadapterpath "$ArtifactsDir\bin\FSharpSuite.Tests\"
    }

    if ($testCoreClr) {
        TestUsingXUnit -testProject "$RepoRoot\tests\FSharp.Compiler.ComponentTests\FSharp.Compiler.ComponentTests.fsproj" -targetFramework $coreclrTargetFramework -testadapterpath "$ArtifactsDir\bin\FSharp.Compiler.ComponentTests\"
        TestUsingNUnit -testProject "$RepoRoot\tests\FSharp.Compiler.UnitTests\FSharp.Compiler.UnitTests.fsproj" -targetFramework $coreclrTargetFramework -testadapterpath "$ArtifactsDir\bin\FSharp.Compiler.UnitTests\"
        TestUsingNUnit -testProject "$RepoRoot\tests\FSharp.Compiler.Service.Tests\FSharp.Compiler.Service.Tests.fsproj" -targetFramework $coreclrTargetFramework -testadapterpath "$ArtifactsDir\bin\FSharp.Compiler.Service.Tests\"
        TestUsingXUnit -testProject "$RepoRoot\tests\FSharp.Compiler.Private.Scripting.UnitTests\FSharp.Compiler.Private.Scripting.UnitTests.fsproj" -targetFramework $coreclrTargetFramework -testadapterpath "$ArtifactsDir\bin\FSharp.Compiler.Private.Scripting.UnitTests\"
        TestUsingNUnit -testProject "$RepoRoot\tests\FSharp.Build.UnitTests\FSharp.Build.UnitTests.fsproj" -targetFramework $coreclrTargetFramework -testadapterpath "$ArtifactsDir\bin\FSharp.Build.UnitTests\"
        TestUsingXUnit -testProject "$RepoRoot\tests\FSharp.Core.UnitTests\FSharp.Core.UnitTests.fsproj" -targetFramework $coreclrTargetFramework -testadapterpath "$ArtifactsDir\bin\FSharp.Core.UnitTests\"        
        TestUsingNUnit -testProject "$RepoRoot\tests\fsharp\FSharpSuite.Tests.fsproj" -targetFramework $coreclrTargetFramework -testadapterpath "$ArtifactsDir\bin\FSharpSuite.Tests\"
    }

    if ($testFSharpQA) {
        Push-Location "$RepoRoot\tests\fsharpqa\source"
        $nugetPackages = Get-PackagesDir
        $resultsRoot = "$ArtifactsDir\TestResults\$configuration"
        $resultsLog = "test-net40-fsharpqa-results.log"
        $errorLog = "test-net40-fsharpqa-errors.log"
        $failLog = "test-net40-fsharpqa-errors"
        $perlPackageRoot = "$nugetPackages\StrawberryPerl\5.28.0.1";
        $perlExe = "$perlPackageRoot\bin\perl.exe"
        Create-Directory $resultsRoot
        UpdatePath
        $env:HOSTED_COMPILER = 1
        $env:CSC_PIPE = "$nugetPackages\Microsoft.Net.Compilers\2.7.0\tools\csc.exe"
        $env:FSCOREDLLPATH = "$ArtifactsDir\bin\fsc\$configuration\net472\FSharp.Core.dll"
        $env:LINK_EXE = "$RepoRoot\tests\fsharpqa\testenv\bin\link\link.exe"
        $env:OSARCH = $env:PROCESSOR_ARCHITECTURE
        $env:PERL5LIB = "$perlPackageRoot\vendor\lib"
        Exec-Console $perlExe """$RepoRoot\tests\fsharpqa\testenv\bin\runall.pl"" -resultsroot ""$resultsRoot"" -results $resultsLog -log $errorLog -fail $failLog -cleanup:no -procs:$env:NUMBER_OF_PROCESSORS"
        Pop-Location
    }

    if ($testFSharpCore) {
        TestUsingXUnit -testProject "$RepoRoot\tests\FSharp.Core.UnitTests\FSharp.Core.UnitTests.fsproj" -targetFramework $desktopTargetFramework -testadapterpath "$ArtifactsDir\bin\FSharp.Core.UnitTests\"
        TestUsingXUnit -testProject "$RepoRoot\tests\FSharp.Core.UnitTests\FSharp.Core.UnitTests.fsproj" -targetFramework $coreclrTargetFramework -testadapterpath "$ArtifactsDir\bin\FSharp.Core.UnitTests\"
    }

    if ($testCompiler) {
        TestUsingXUnit -testProject "$RepoRoot\tests\FSharp.Compiler.ComponentTests\FSharp.Compiler.ComponentTests.fsproj" -targetFramework $desktopTargetFramework -testadapterpath "$ArtifactsDir\bin\FSharp.Compiler.ComponentTests\" -noTestFilter $true
        TestUsingXUnit -testProject "$RepoRoot\tests\FSharp.Compiler.ComponentTests\FSharp.Compiler.ComponentTests.fsproj" -targetFramework $coreclrTargetFramework -testadapterpath "$ArtifactsDir\bin\FSharp.Compiler.ComponentTests\" -noTestFilter $true
        TestUsingNUnit -testProject "$RepoRoot\tests\FSharp.Compiler.UnitTests\FSharp.Compiler.UnitTests.fsproj" -targetFramework $desktopTargetFramework -testadapterpath "$ArtifactsDir\bin\FSharp.Compiler.UnitTests\"
        TestUsingNUnit -testProject "$RepoRoot\tests\FSharp.Compiler.UnitTests\FSharp.Compiler.UnitTests.fsproj" -targetFramework $coreclrTargetFramework -testadapterpath "$ArtifactsDir\bin\FSharp.Compiler.UnitTests\"
    }

    if ($testCompilerService) {
        TestUsingNUnit -testProject "$RepoRoot\tests\FSharp.Compiler.Service.Tests\FSharp.Compiler.Service.Tests.fsproj" -targetFramework $desktopTargetFramework -testadapterpath "$ArtifactsDir\bin\FSharp.Compiler.Service.Tests\"
        TestUsingNUnit -testProject "$RepoRoot\tests\FSharp.Compiler.Service.Tests\FSharp.Compiler.Service.Tests.fsproj" -targetFramework $coreclrTargetFramework -testadapterpath "$ArtifactsDir\bin\FSharp.Compiler.Service.Tests\"
    }

    if ($testCambridge) {
        TestUsingNUnit -testProject "$RepoRoot\tests\fsharp\FSharpSuite.Tests.fsproj" -targetFramework $desktopTargetFramework -testadapterpath "$ArtifactsDir\bin\FSharpSuite.Tests\"
        TestUsingNUnit -testProject "$RepoRoot\tests\fsharp\FSharpSuite.Tests.fsproj" -targetFramework $coreclrTargetFramework -testadapterpath "$ArtifactsDir\bin\FSharpSuite.Tests\"
    }
`
        if ($testScripting) {
        TestUsingXUnit -testProject "$RepoRoot\tests\FSharp.Compiler.Private.Scripting.UnitTests\FSharp.Compiler.Private.Scripting.UnitTests.fsproj" -targetFramework $desktopTargetFramework  -testadapterpath "$ArtifactsDir\bin\FSharp.Compiler.Private.Scripting.UnitTests\"
        TestUsingXUnit -testProject "$RepoRoot\tests\FSharp.Compiler.Private.Scripting.UnitTests\FSharp.Compiler.Private.Scripting.UnitTests.fsproj" -targetFramework $coreclrTargetFramework  -testadapterpath "$ArtifactsDir\bin\FSharp.Compiler.Private.Scripting.UnitTests\"
    }

    if ($testVs -and -not $noVisualStudio) {
        TestUsingNUnit -testProject "$RepoRoot\vsintegration\tests\GetTypesVS.UnitTests\GetTypesVS.UnitTests.fsproj" -targetFramework $desktopTargetFramework -testadapterpath "$ArtifactsDir\bin\GetTypesVS.UnitTests"
        TestUsingNUnit -testProject "$RepoRoot\vsintegration\tests\UnitTests\VisualFSharp.UnitTests.fsproj" -targetFramework $desktopTargetFramework -testadapterpath "$ArtifactsDir\bin\VisualFSharp.UnitTests"
    }

    # verify nupkgs have access to the source code
    $nupkgtestFailed = $false
    if ($testpack) {
        # Fetch soucelink test
        $dotnetPath = InitializeDotNetCli
        $dotnetExe = Join-Path $dotnetPath "dotnet.exe"
        $dotnettoolsPath = Join-Path $RepoRoot "\.tools\"
        $sourcelink = Join-Path $dotnettoolsPath "sourcelink.exe"
        try {
            $out = New-Item -Path $dotnettoolsPath -ItemType Directory -Force
            Exec-Console $dotnetExe "tool install sourcelink --tool-path $dotnettoolsPath"
        }
        catch {
            Write-Host "Already installed is not a problem"
        }

        $nupkgs = @(Get-ChildItem "$artifactsDir\packages\$configuration\Shipping\*.nupkg" -recurse)
        $nupkgs | Foreach {
            Exec-Console """$sourcelink"" test ""$_"""
            if (-not $?) { $nupkgtestFailed = $true }
        }
    }
    if ($nupkgtestFailed) {
        throw "Error Verifying nupkgs have access to the source code"
    }

    ExitWithExitCode 0
}
catch {
    Write-Host $_
    Write-Host $_.Exception
    Write-Host $_.ScriptStackTrace
    Write-PipelineTelemetryError -Category $script:BuildCategory -Message $script:BuildMessage
    ExitWithExitCode 1
}
finally {
    Pop-Location
}
