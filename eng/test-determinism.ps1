[CmdletBinding(PositionalBinding=$false)]
param([string]$configuration = "Debug",
      [string]$msbuildEngine = "vs",
      [string]$altRootDrive = "q:",
      [switch]$help,
      [switch]$norestore,
      [switch]$rebuild)

Set-StrictMode -version 2.0
$ErrorActionPreference = "Stop"

function Print-Usage() {
  Write-Host "Usage: test-determinism.ps1"
  Write-Host "  -configuration <value>    Build configuration ('Debug' or 'Release')"
  Write-Host "  -msbuildEngine <value>    Msbuild engine to use to run build ('dotnet', 'vs', or unspecified)."
  Write-Host "  -bootstrapDir             Directory containing the bootstrap compiler"
  Write-Host "  -altRootDrive             The drive we build on (via subst) for verifying pathmap implementation"
}

if ($help) {
  Print-Usage
  exit 0
}

# List of binary names that should be skipped because they have a known issue that
# makes them non-deterministic.  
$script:skipList = @()
function Run-Build([string]$rootDir, [string]$logFileName) {

  # Clean out the previous run
  Write-Host "Cleaning binaries"
  $stopWatch = [System.Diagnostics.StopWatch]::StartNew()
  Remove-Item -Recurse (Get-BinDir $rootDir) -ErrorAction SilentlyContinue
  Remove-Item -Recurse (Get-ObjDir $rootDir) -ErrorAction SilentlyContinue
  $stopWatch.Stop()
  Write-Host "Cleaning took $($stopWatch.Elapsed)"

  $solution = Join-Path $rootDir "VisualFSharp.sln"

  if ($logFileName -eq "") {
    $logFileName = [IO.Path]::GetFileNameWithoutExtension($projectFilePath)
  }
  $logFileName = [IO.Path]::ChangeExtension($logFileName, ".binlog")
  $logFilePath = Join-Path $LogDir $logFileName

  Stop-Processes

  Write-Host "Building $solution using $bootstrapDir"
  MSBuild $toolsetBuildProj `
     /p:Projects=$solution `
     /p:Restore=true `
     /p:Build=true `
     /p:DebugDeterminism=true `
     /p:Features="debug-determinism" `
     /p:DeployExtension=false `
     /p:RepoRoot=$rootDir `
     /p:TreatWarningsAsErrors=true `
     /p:BootstrapBuildPath=$bootstrapDir `
     /p:RunAnalyzers=false `
     /p:RunAnalyzersDuringBuild=false `
     /p:RestoreUseStaticGraphEvaluation=true `
     /bl:$logFilePath

  Stop-Processes
}

function Get-ObjDir([string]$rootDir) { 
  return Join-Path $rootDir "artifacts\obj"
}

function Get-BinDir([string]$rootDir) { 
  return Join-Path $rootDir "artifacts\bin"
}

# Return all of the files that need to be processed for determinism under the given
# directory.
function Get-FilesToProcess([string]$rootDir) {
  $objDir = Get-ObjDir $rootDir
  foreach ($item in Get-ChildItem -re -in *.dll,*.exe,*.pdb,*.sourcelink.json $objDir) {
    $filePath = $item.FullName 
    $fileName = Split-Path -leaf $filePath
    $relativeDirectory = Split-Path -parent $filePath
    $relativeDirectory = $relativeDirectory.Substring($objDir.Length)
    $relativeDirectory = $relativeDirectory.TrimStart("\")

    if ($skipList.Contains($fileName)) {
      continue;
    }

    $fileId = $filePath.Substring($objDir.Length).Replace("\", ".").TrimStart(".")
    $fileHash = (Get-FileHash $filePath -algorithm MD5).Hash

    $data = @{}
    $data.Hash = $fileHash
    $data.Content = [IO.File]::ReadAllBytes($filePath)
    $data.FileId = $fileId
    $data.FileName = $fileName
    $data.FilePath = $filePath
    $data.RelativeDirectory = $relativeDirectory

    $keyFilePath = $filePath + ".key"
    $keyFileName = Split-Path -leaf $keyFilePath
    if (Test-Path $keyFilePath) { 
      $data.KeyFileName = $keyFileName
      $data.KeyFilePath = $keyFilePath
      $data.KeyFileContent = [IO.File]::ReadAllBytes($keyFilePath)
    }
    else {
      $data.KeyFileName = ""
      $data.KeyFilePath = ""
      $data.KeyFileContent = $null
    }

    Write-Output $data
  }
}

# This will build up the map of all of the binaries and their respective hashes.
function Record-Binaries([string]$rootDir) {
  $stopWatch = [System.Diagnostics.StopWatch]::StartNew()
  Write-Host "Recording file hashes"

  $map = @{ }
  foreach ($fileData in Get-FilesToProcess $rootDir) { 
    Write-Host "`t$($fileData.FileId) = $($fileData.Hash)"
    $map[$fileData.FileId] = $fileData
  }
  $stopWatch.Stop()
  Write-Host "Recording took $($stopWatch.Elapsed)"
  return $map
}

# This is a sanity check to ensure that we're actually putting the right entries into
# the core data map. Essentially to ensure things like if we change our directory layout 
# that this test fails beacuse we didn't record the binaries we intended to record. 
function Test-MapContents($dataMap) { 

  # Sanity check to ensure we didn't return a false positive because we failed
  # to examine any binaries.
  if ($dataMap.Count -lt 40) {
    throw "Didn't find the expected count of binaries"
  }

  # Test for some well known binaries
  $list = @(
    "FSharp.Core.dll",
    "FSharp.Compiler.Service.dll")

  foreach ($fileName in $list) { 
    $found = $false
    foreach ($value in $dataMap.Values) { 
      if ($value.FileName -eq $fileName) { 
        $found = $true
        break;
      }
    }

    if (-not $found) { 
      throw "Did not find the expected binary $fileName"
    }
  }
}

function Test-Build([string]$rootDir, $dataMap, [string]$logFileName) {
  Run-Build $rootDir -logFile $logFileName

  $errorList = @()
  $allGood = $true

  Write-Host "Testing the binaries"
  $stopWatch = [System.Diagnostics.StopWatch]::StartNew()
  foreach ($fileData in Get-FilesToProcess $rootDir) {
    $fileId = $fileData.FileId
    $fileName = $fileData.FileName
    $filePath = $fileData.FilePath
    $relativeDir = $fileData.RelativeDirectory

    if (-not $dataMap.Contains($fileId)) {
      Write-Host "ERROR! Missing entry in map $fileId->$filePath"
      $allGood = $false
      continue
    }

    $oldfileData = $datamap[$fileId]
    if ($fileData.Hash -ne $oldFileData.Hash) { 
      Write-Host "`tERROR! $relativeDir\$fileName contents don't match"
      $allGood = $false
      $errorList += $fileName

      $errorCurrentDirLeft = Join-Path $errorDirLeft $relativeDir
      Create-Directory $errorCurrentDirLeft
      $errorCurrentDirRight = Join-Path $errorDirRight $relativeDir
      Create-Directory $errorCurrentDirRight

      # Save out the original and baseline for investigation
      [IO.File]::WriteAllBytes((Join-Path $errorCurrentDirLeft $fileName), $oldFileData.Content)
      Copy-Item $filePath (Join-Path $errorCurrentDirRight $fileName)

      # Copy the key files if available too
      $keyFileName = $oldFileData.KeyFileName
      if ($keyFileName -ne "") {
        [IO.File]::WriteAllBytes((Join-Path $errorCurrentDirLeft $keyFileName), $oldFileData.KeyFileContent)
        Copy-Item $fileData.KeyFilePath (Join-Path $errorCurrentDirRight $keyFileName)
      }

      continue
    }

    Write-Host "`tVerified $relativeDir\$fileName"
  }

  if (-not $allGood) {
    Write-Host "Determinism failed for the following binaries:"
    foreach ($name in $errorList) {
      Write-Host "`t$name"
    }

    Write-Host "Archiving failure information"
    $zipFile = Join-Path $LogDir "determinism.zip"
    Add-Type -Assembly "System.IO.Compression.FileSystem";
    [System.IO.Compression.ZipFile]::CreateFromDirectory($script:errorDir, $zipFile, "Fastest", $true);

    Write-Host "Please send $zipFile to compiler team for analysis"
    exit 1
  }

  $stopWatch.Stop()
  Write-Host "Testing took $($stopWatch.Elapsed)"
}

function Run-Test() {
  # Run the initial build so that we can populate the maps
  Run-Build $RepoRoot -logFileName "Initial" -useBootstrap
  $dataMap = Record-Binaries $RepoRoot
  Test-MapContents $dataMap

  # Run a test against the source in the same directory location
  Test-Build -rootDir $RepoRoot -dataMap $dataMap -logFileName "test1"

  # Run another build in a different source location and verify that path mapping 
  # allows the build to be identical.  To do this we'll copy the entire source 
  # tree under the artifacts\q directory and run a build from there.
  # Write-Host "Building in a different directory"
  # Exec-Command "subst" "$altRootDrive $(Split-Path -parent $RepoRoot)"
  # try {
  #   $altRootDir = Join-Path "$($altRootDrive)\" (Split-Path -leaf $RepoRoot)
  #   Test-Build -rootDir $altRootDir -dataMap $dataMap -logFileName "test2"
  # }
  # finally {
  #   Exec-Command "subst" "$altRootDrive /d"
  # }
}

function Test-IsAdmin {
    ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")
}

function TryDownloadDotnetFrameworkSdk() {
    # If we are not running as admin user, don't bother grabbing ndp sdk -- since we don't need sn.exe
    $isAdmin = Test-IsAdmin
    Write-Host "TryDownloadDotnetFrameworkSdk -- Test-IsAdmin = '$isAdmin'"
    if ($isAdmin -eq $true)
    {
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
                [System.Environment]::SetEnvironmentVariable("WindowsSDK_ExecutablePath_x86","$newWindowsSDK_ExecutablePath_x86",[System.EnvironmentVariableTarget]::Machine)
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
                [System.Environment]::SetEnvironmentVariable("WindowsSDK_ExecutablePath_x64","$newWindowsSDK_ExecutablePath_x64",[System.EnvironmentVariableTarget]::Machine)
                $env:WindowsSDK_ExecutablePath_x64 = $newWindowsSDK_ExecutablePath_x64
            }
        }
    }
}

try {
  . (Join-Path $PSScriptRoot "build-utils.ps1")

  # Create all of the logging directories
  $errorDir = Join-Path $LogDir "DeterminismFailures"
  $errorDirLeft = Join-Path $errorDir "Left"
  $errorDirRight = Join-Path $errorDir "Right"

  Create-Directory $LogDir
  Create-Directory $errorDirLeft
  Create-Directory $errorDirRight

  $ci = $true
  $runAnalyzers = $false
  $binaryLog = $true
  $officialBuildId = ""
  $nodeReuse = $false
  $properties = @()
  $script:bootstrap = $true
  $script:bootstrapConfiguration = "Proto"

  $buildTool = InitializeBuildTool
  $toolsetBuildProj = InitializeToolset
  TryDownloadDotnetFrameworkSdk

  $bootstrapDir = Make-BootstrapBuild

  Run-Test
  exit 0
}
catch {
  Write-Host $_
  Write-Host $_.Exception
  Write-Host $_.ScriptStackTrace
  exit 1
}

