param([ValidateSet('win-x64','win-x86')][string]$Runtime='win-x64')
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
  dotnet run --project tests/Core.Tests -c Release
  if ($LASTEXITCODE -ne 0) { throw 'Core tests failed' }
  dotnet publish src/FeishuPixelTasks/FeishuPixelTasks.csproj -c Release -r $Runtime --self-contained true -p:PublishSingleFile=false -o "dist/$Runtime"
  if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
  Copy-Item README.md,LICENSE,THIRD-PARTY-NOTICES.md "dist/$Runtime/"
  Compress-Archive -Path "dist/$Runtime/*" -DestinationPath "dist/FeishuPixelTasks-$Runtime.zip" -Force
  Write-Host "Built dist/FeishuPixelTasks-$Runtime.zip"
} finally { Pop-Location }
