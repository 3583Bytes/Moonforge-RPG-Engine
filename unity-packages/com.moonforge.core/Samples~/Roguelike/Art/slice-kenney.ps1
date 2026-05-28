# slice-kenney.ps1
#
# Crops individual 16x16 sprites out of the bundled Kenney 1-Bit Pack tilesheet
# (Source/kenney_1bit_colored-packed.png) and writes them into Resources/Sprites/
# with the names the UnitySpriteCatalog expects.
#
# The tilesheet is 49 columns x 22 rows of 16x16 tiles, packed with NO spacing
# (tile at grid (col, row) starts at pixel (col*16, row*16)).
#
# If a sprite doesn't match what the role is supposed to be (e.g. the "hero" tile
# turns out to be a tree), edit the $tiles table below to point at a different
# (col, row) and re-run. Open the tilesheet in any image viewer (or Unity's
# Sprite Editor with Grid By Cell Size 16x16) to identify positions.
#
# Usage (from the imported sample folder in your Unity project):
#   PS> ./Art/slice-kenney.ps1
#
# Re-running is safe — output PNGs are overwritten.

[CmdletBinding()]
param(
    [string]$Source = "$PSScriptRoot/Source/kenney_1bit_colored-packed.png",
    [string]$OutDir = "$PSScriptRoot/Resources/Sprites"
)

# Best-guess (col, row) positions for each sprite name. Edit any of these to point
# at a different tile in the sheet and re-run the script.
$tiles = [ordered]@{
    # NOTE: positions below are best-guesses I picked while initially building this
    # script. Several are likely wrong because the Kenney 1-Bit Pack interleaves
    # characters, items, and tiles densely across the sheet. Open
    # Source/kenney_1bit_colored-packed.png in any image viewer (or use Unity's
    # Sprite Editor with Grid By Cell Size 16x16) to find the right (col, row) and
    # update the values below — then re-run the script.
    "dungeon_floor"    = @(3, 16)
    "dungeon_wall"     = @(15, 17)
    "dungeon_pillar"   = @(4, 13)
    "stairs_down"      = @(16, 12)
    "stairs_up"        = @(15, 12)

    "town_floor"       = @(1, 17)
    "town_wall"        = @(15, 0)
    "town_door"        = @(7, 14)

    "marker_shop"      = @(40, 12)
    "marker_healer"    = @(40, 13)
    "marker_alchemist" = @(40, 11)
    "marker_guard"     = @(28, 2)
    "marker_cache"     = @(36, 2)
    "marker_fountain"  = @(15, 5)
    "marker_questboard"= @(16, 8)
    "marker_shrine"    = @(38, 1)

    "hero"             = @(25, 2)
    "enemy"            = @(19, 7)
    "enemy_elite"      = @(19, 8)
    "enemy_boss"       = @(19, 9)
    "npc"              = @(26, 2)
}

if (-not (Test-Path $Source)) {
    Write-Error "Tilesheet not found: $Source"
    exit 1
}

if (-not (Test-Path $OutDir)) {
    New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
}

Add-Type -AssemblyName System.Drawing

$src = [System.Drawing.Image]::FromFile((Resolve-Path $Source))
try {
    $tileSize = 16
    Write-Host "Tilesheet: $($src.Width)x$($src.Height) px"
    Write-Host "Output:    $OutDir"
    Write-Host ""

    foreach ($name in $tiles.Keys) {
        $col = $tiles[$name][0]
        $row = $tiles[$name][1]
        $srcRect = New-Object System.Drawing.Rectangle ($col * $tileSize), ($row * $tileSize), $tileSize, $tileSize

        $bmp = New-Object System.Drawing.Bitmap $tileSize, $tileSize
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
        $dstRect = New-Object System.Drawing.Rectangle 0, 0, $tileSize, $tileSize
        $g.DrawImage($src, $dstRect, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)
        $g.Dispose()

        $outPath = Join-Path $OutDir "$name.png"
        $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        Write-Host ("  {0,-18} <- ({1,2},{2,2})  -> {3}.png" -f $name, $col, $row, $name)
    }
}
finally {
    $src.Dispose()
}

Write-Host ""
Write-Host "Done. Open Unity and the new sprites under Art/Resources/Sprites/ will"
Write-Host "be imported automatically. UnitySpriteCatalog will pick them up by name"
Write-Host "and stop using the runtime-generated coloured placeholders for them."
