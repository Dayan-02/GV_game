$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$dest = Join-Path $projectRoot 'Assets/Art/ImportedCharacters'
$records = [Collections.Generic.List[object]]::new()
function Fetch($repo, $revision, $source, $target, $license) {
    $url = 'https://raw.githubusercontent.com/' + $repo + '/' + $revision + '/' + (($source.Split('/') | ForEach-Object { [Uri]::EscapeDataString($_) }) -join '/')
    $path = Join-Path $dest $target
    [void](New-Item -ItemType Directory -Force (Split-Path $path -Parent))
    if (!(Test-Path -LiteralPath $path)) { Invoke-WebRequest $url -OutFile $path }
    $records.Add(@{file=$target;source=$url;license=$license;sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash})
    Write-Output $target
}
$rocket = 'microsoft/Microsoft-Rocketbox'
$rev = '0943055db6ec570bcef9f2c8b41c9e5467c808f9'
$tree = (Invoke-RestMethod "https://api.github.com/repos/$rocket/git/trees/${rev}?recursive=1").tree
foreach($entry in $tree) {
    if ($entry.path -match '^Assets/Avatars/Professions/Military_Male_01/(Export/Military_Male_01.fbx|Textures/.*\.tga)$') {
        Fetch $rocket $rev $entry.path ('Rocketbox/' + ($entry.path -replace '^.*/(Export|Textures)/','')) 'MIT'
    }
}
Fetch $rocket $rev 'LICENSE.md' 'Rocketbox/LICENSE.md' 'MIT'
$unity = 'Unity-Technologies/Standard-Assets-Characters'
$urev = '8f185aa3c782d824976653dd6a6476312daad241'
$utree = (Invoke-RestMethod "https://api.github.com/repos/$unity/git/trees/${urev}?recursive=1").tree
$names = @('m@Idle','m@WalkForwards','m@RunForwards','m@SprintForwards','m@StrafeIdle','m@StrafeRight','m@StrafeWalkForwards','m@Falling','m@StandingJump_Frame02')
foreach($entry in $utree) {
    if ($entry.path -cmatch '/Male/Animations/' -and $entry.path.EndsWith('.fbx') -and $names -contains [IO.Path]::GetFileNameWithoutExtension($entry.path)) {
        Fetch $unity $urev $entry.path ('UnityMotion/'+[IO.Path]::GetFileName($entry.path)) 'Unity-Companion'
    }
}
Fetch $unity $urev 'LICENSE.md' 'UnityMotion/LICENSE.md' 'Unity-Companion'
$records | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $dest 'SourceRecords.json') -Encoding utf8

