param([Parameter(Mandatory=$true)][string]$Folder, [Parameter(Mandatory=$true)][string]$Destination)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$moduleRoot=(Resolve-Path -LiteralPath $Folder).Path
if (!(Test-Path -LiteralPath (Join-Path $moduleRoot 'manifest.json'))) { throw 'The folder needs manifest.json.' }
if (Test-Path -LiteralPath $Destination) { throw 'Choose a new ZIP filename.' }
$zip=[IO.Compression.ZipFile]::Open([IO.Path]::GetFullPath($Destination),[IO.Compression.ZipArchiveMode]::Create)
try {
    foreach($name in @('manifest.json','README.md','LICENSE','module.sig')) {
        $file=Join-Path $moduleRoot $name
        if(Test-Path -LiteralPath $file) { [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$file,$name) }
    }
} finally { $zip.Dispose() }
Write-Host "Created $Destination. Install it from SableLatch Settings > Plugins & modules."
