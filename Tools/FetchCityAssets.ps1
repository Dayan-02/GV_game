$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$artRoot = Join-Path $projectRoot 'Assets/Art/AbandonedCity/PolyHaven'
$headers = @{ 'User-Agent' = 'ZombieOutbreak-UniversityArtPass/1.0' }
$records = [System.Collections.Generic.List[object]]::new()

function Get-VerifiedAssetFile($descriptor, $destination) {
    if ($null -eq $descriptor.url) { throw "No download descriptor for $destination" }
    if (Test-Path -LiteralPath $destination) {
        if ((Get-FileHash -LiteralPath $destination -Algorithm MD5).Hash -ieq $descriptor.md5) { return }
    }
    Invoke-WebRequest -Uri $descriptor.url -Headers $headers -OutFile $destination
    if ((Get-FileHash -LiteralPath $destination -Algorithm MD5).Hash -ine $descriptor.md5) {
        throw "Downloaded asset hash differs from provider metadata: $destination"
    }
}

$selections = @(
    @{ id='asphalt_02'; type='texture' },
    @{ id='brick_wall_001'; type='texture' },
    @{ id='cracked_concrete_wall'; type='texture' },
    @{ id='rusty_corrugated_iron'; type='texture' },
    @{ id='wood_planks_grey'; type='texture' },
    @{ id='concrete_pavement'; type='texture' },
    @{ id='trashbag'; type='model' },
    @{ id='Barrel_01'; type='model' },
    @{ id='fire_hydrant'; type='model' },
    @{ id='metal_trash_can'; type='model' }
)
foreach ($selection in $selections) {
    $id = $selection.id
    $folder = Join-Path $artRoot $id
    [void](New-Item -ItemType Directory -Force -Path $folder)
    $files = Invoke-RestMethod -Uri "https://api.polyhaven.com/files/$id" -Headers $headers
    $info = Invoke-RestMethod -Uri "https://api.polyhaven.com/info/$id" -Headers $headers
    $downloads = [System.Collections.Generic.List[object]]::new()
    foreach ($channel in @('Diffuse', 'nor_gl', 'Rough')) {
        $resolution = if ($channel -eq 'Diffuse' -and $selection.type -eq 'texture') { '2k' } else { '1k' }
        $providerChannel = $channel
        if ($id -eq 'Barrel_01') {
            $providerChannel = @{ Diffuse='oxide_green_diff'; nor_gl='oxide_green_nor_gl'; Rough='oxide_green_roughness' }[$channel]
        }
        $descriptor = $files.$providerChannel.$resolution.jpg
        if ($null -eq $descriptor) { $descriptor = $files.$providerChannel.$resolution.png }
        $fileName = $channel + [IO.Path]::GetExtension(([uri]$descriptor.url).AbsolutePath)
        Get-VerifiedAssetFile $descriptor (Join-Path $folder $fileName)
        $downloads.Add(@{ file=$fileName; url=$descriptor.url; md5=$descriptor.md5; resolution=$resolution })
    }
    if ($selection.type -eq 'model') {
        $descriptor = $files.fbx.'1k'.fbx
        Get-VerifiedAssetFile $descriptor (Join-Path $folder "$id.fbx")
        $downloads.Add(@{ file="$id.fbx"; url=$descriptor.url; md5=$descriptor.md5 })
    }
    $records.Add(@{ id=$id; type=$selection.type; title=$info.name; authors=$info.authors; source="https://polyhaven.com/a/$id"; license='CC0-1.0'; licenseUrl='https://polyhaven.com/license'; files=$downloads })
    Write-Output "Downloaded and verified: $id"
}
$records | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $artRoot 'SourceRecords.json') -Encoding utf8
