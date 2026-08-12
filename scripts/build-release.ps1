param(
    [string]$Configuration = "Release",
    [string]$MWCManagedDir = "",
    [string]$DotNetExecutable = "",
    [string]$ReferenceDll = "",
    [string]$MscAuditReferenceDll = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($DotNetExecutable))
{
    $dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -eq $dotnetCommand) { throw "A .NET SDK was not found. Install one or pass -DotNetExecutable." }
    $DotNetExecutable = $dotnetCommand.Source
}

if ([string]::IsNullOrWhiteSpace($ReferenceDll)) { $ReferenceDll = $env:SAVESLOTS_MWC_V3_REFERENCE }
if ([string]::IsNullOrWhiteSpace($MscAuditReferenceDll)) { $MscAuditReferenceDll = $env:SAVESLOTS_MSC_11_REFERENCE }
if ([string]::IsNullOrWhiteSpace($ReferenceDll)) { throw "Pass -ReferenceDll or set SAVESLOTS_MWC_V3_REFERENCE." }
if ([string]::IsNullOrWhiteSpace($MscAuditReferenceDll)) { throw "Pass -MscAuditReferenceDll or set SAVESLOTS_MSC_11_REFERENCE." }

$projectRoot = (Resolve-Path -LiteralPath (Split-Path -Parent $PSScriptRoot)).Path
$solution = Join-Path $projectRoot "SaveSlotsMWC.sln"
$testProject = Join-Path $projectRoot "tests\SaveSlotsMWC.Tests\SaveSlotsMWC.Tests.csproj"
$auditProject = Join-Path $projectRoot "tools\SaveSlotsAudit\SaveSlotsAudit.csproj"
$distDll = Join-Path $projectRoot "dist\SaveSlots.dll"
$releaseRoot = Join-Path $projectRoot "production"
$releaseDir = Join-Path $releaseRoot "SaveSlotsMWC-4.0.0"
$releaseZip = Join-Path $releaseRoot "SaveSlotsMWC-4.0.0-Nexus.zip"
$referenceDllPath = (Resolve-Path -LiteralPath $ReferenceDll).Path
$mscAuditReferenceDllPath = (Resolve-Path -LiteralPath $MscAuditReferenceDll).Path

$buildArguments = @("build", $solution, "-c", $Configuration)
if ($MWCManagedDir -ne "")
{
    $resolvedManaged = (Resolve-Path -LiteralPath $MWCManagedDir).Path
    $buildArguments += "-p:MWCManagedDir=$resolvedManaged"
}

& $DotNetExecutable @buildArguments
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE" }
& $DotNetExecutable run --project $testProject -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "Tests failed with exit code $LASTEXITCODE" }

if (-not (Test-Path -LiteralPath $distDll -PathType Leaf)) { throw "Build did not create $distDll" }
if (-not (Test-Path -LiteralPath $referenceDllPath -PathType Leaf)) { throw "Immutable reference DLL is missing: $referenceDllPath" }
if (-not (Test-Path -LiteralPath $mscAuditReferenceDllPath -PathType Leaf)) { throw "MSC audit reference DLL is missing: $mscAuditReferenceDllPath" }

$resolvedReleaseRoot = (New-Item -ItemType Directory -Force -Path $releaseRoot).FullName
$expectedPrefix = $projectRoot + [IO.Path]::DirectorySeparatorChar
if (-not $resolvedReleaseRoot.StartsWith($expectedPrefix, [StringComparison]::OrdinalIgnoreCase))
{
    throw "Release path escaped the workspace: $resolvedReleaseRoot"
}
if (Test-Path -LiteralPath $releaseDir)
{
    $resolvedReleaseDir = (Resolve-Path -LiteralPath $releaseDir).Path
    if (-not $resolvedReleaseDir.StartsWith($resolvedReleaseRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase))
    {
        throw "Release directory escaped the verified release root: $resolvedReleaseDir"
    }
    Remove-Item -LiteralPath $resolvedReleaseDir -Recurse -Force
}

New-Item -ItemType Directory -Path $releaseDir | Out-Null
Copy-Item -LiteralPath $distDll -Destination (Join-Path $releaseDir "SaveSlots.dll")
Copy-Item -LiteralPath (Join-Path $projectRoot "README.md") -Destination (Join-Path $releaseDir "README.txt")
Copy-Item -LiteralPath (Join-Path $projectRoot "ORIGINALITY_REPORT.md") -Destination (Join-Path $releaseDir "ORIGINALITY_REPORT.txt")
Copy-Item -LiteralPath (Join-Path $projectRoot "CHANGELOG.md") -Destination (Join-Path $releaseDir "CHANGELOG.txt")
Copy-Item -LiteralPath (Join-Path $projectRoot "THIRD_PARTY_NOTICES.txt") -Destination (Join-Path $releaseDir "THIRD_PARTY_NOTICES.txt")

if (Test-Path -LiteralPath $releaseZip) { Remove-Item -LiteralPath $releaseZip -Force }
Compress-Archive -Path (Join-Path $releaseDir "*") -DestinationPath $releaseZip

& $DotNetExecutable run --project $auditProject -c $Configuration -- $distDll $referenceDllPath $releaseDir $mscAuditReferenceDllPath
if ($LASTEXITCODE -ne 0) { throw "Binary/package audit failed with exit code $LASTEXITCODE" }

$newHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $distDll).Hash
$oldHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $referenceDllPath).Hash
if ($newHash -eq $oldHash) { throw "Output unexpectedly matches the immutable reference DLL." }

Write-Host "New DLL SHA-256: $newHash"
Write-Host "Reference SHA-256: $oldHash"
Write-Host "Package: $releaseZip"
