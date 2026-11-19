param(
    [string]$GamePath = "${env:ProgramFiles(x86)}\Steam\steamapps\common\Car Mechanic Simulator 2018"
)

$ErrorActionPreference = 'Stop'
$game = (Resolve-Path -LiteralPath $GamePath).Path
$managed = Join-Path $game 'cms2018_Data\Managed'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$output = Join-Path $PSScriptRoot 'BepInEx\plugins\CMS18ModMenu'

$references = @(
    'mscorlib', 'System', 'System.Core', 'Assembly-CSharp-firstpass',
    'UnityEngine', 'UnityEngine.CoreModule', 'UnityEngine.IMGUIModule',
    'UnityEngine.InputModule', 'UnityEngine.TextRenderingModule',
    'UnityEngine.UIModule', 'UnityEngine.UI'
) | ForEach-Object { Join-Path $managed "$_.dll" }
$references += Join-Path $game 'BepInEx\core\BepInEx.dll'
foreach ($reference in $references) {
    if (-not (Test-Path -LiteralPath $reference -PathType Leaf)) {
        throw "Missing build reference $reference"
    }
}

New-Item -ItemType Directory -Force -Path $output | Out-Null
$sources = Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Source') -Filter '*.cs' -File
$arguments = @('/nologo', '/noconfig', '/nostdlib+', '/target:library', '/optimize+')
$arguments += '/out:' + (Join-Path $output 'CMS18ModMenu.dll')
$arguments += $references | ForEach-Object { "/r:$_" }
$arguments += $sources.FullName
& $compiler @arguments
if ($LASTEXITCODE -ne 0) {
    throw "C# compiler exited with code $LASTEXITCODE"
}
Get-Item -LiteralPath (Join-Path $output 'CMS18ModMenu.dll')
