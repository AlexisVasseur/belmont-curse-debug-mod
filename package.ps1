# Crée dist\BelmontPractice-<version>.zip, à décompresser dans le dossier du jeu par-dessus BepInEx 6.0.0-be.791.
# Contenu : BepInEx\plugins\BelmontPractice (DLL + icônes), LISEZMOI_BelmontPractice.txt et, sauf -SansDonneesBoss,
# les conditions des boss et les entrées d'arène déjà apprises (BelmontPractice\scenarios_boss.json, arenes.json).
param([switch]$SansDonneesBoss)
$ErrorActionPreference = 'Stop'

$proj = $PSScriptRoot
$version = [regex]::Match((Get-Content "$proj\Plugin.cs" -Raw), 'Version = "([^"]+)"').Groups[1].Value
if (-not $version) { throw "Version introuvable dans Plugin.cs" }

dotnet build "$proj\BelmontPractice.csproj" -c Release -p:SkipGameCopy=true --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "La compilation a échoué" }

$stage = Join-Path ([IO.Path]::GetTempPath()) "BelmontPractice-package"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
$plugin = New-Item -ItemType Directory -Force "$stage\BepInEx\plugins\BelmontPractice"
Copy-Item "$proj\bin\Release\net6.0\BelmontPractice.dll" $plugin
New-Item -ItemType Directory -Force "$plugin\icons" | Out-Null
Copy-Item "$proj\icons\*.png" "$plugin\icons"
(Get-Content "$proj\LISEZMOI.txt" -Raw).Replace('{VERSION}', $version) |
    Set-Content "$stage\LISEZMOI_BelmontPractice.txt" -Encoding utf8BOM  # BOM : accents corrects dans le Bloc-notes

if (-not $SansDonneesBoss) {
    $gameData = "C:\Games\Castlevania Belmont's Curse (Modded)\BelmontPractice"
    $data = New-Item -ItemType Directory -Force "$stage\BelmontPractice"
    foreach ($f in 'scenarios_boss.json', 'arenes.json') {
        if (Test-Path "$gameData\$f") { Copy-Item "$gameData\$f" $data } else { Write-Warning "$f absent, non inclus" }
    }
}

$dist = New-Item -ItemType Directory -Force "$proj\dist"
$zip = "$dist\BelmontPractice-$version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path "$stage\*" -DestinationPath $zip
Remove-Item $stage -Recurse -Force
Write-Output "Paquet créé : $zip"
