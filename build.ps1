param([ValidateSet('Debug','Release')][string]$Configuration='Release', [switch]$Portable)
$ErrorActionPreference='Stop'
Push-Location $PSScriptRoot
try {
    dotnet restore tests/SableLatch.Tests --configfile NuGet.Config
    if ($LASTEXITCODE) { throw 'Dependency restore failed' }
    dotnet build tests/SableLatch.Tests -c $Configuration --no-restore
    if ($LASTEXITCODE) { throw 'Test build failed' }
    dotnet run --project tests/SableLatch.Tests -c $Configuration --no-build
    if ($LASTEXITCODE) { throw 'Security and storage checks failed' }
    dotnet run --project tests/SableLatch.ModuleTests -c $Configuration
    if ($LASTEXITCODE) { throw 'Windows module/pipe checks failed' }
    if ($Portable) {
        dotnet publish src/SableLatch.Desktop -c $Configuration -r win-x64 --self-contained true -p:DebugType=None -p:DebugSymbols=false -o artifacts/Sable-portable
    } else { dotnet build src/SableLatch.Desktop -c $Configuration }
    if ($LASTEXITCODE) { throw 'Desktop build failed' }
} finally { Pop-Location }
