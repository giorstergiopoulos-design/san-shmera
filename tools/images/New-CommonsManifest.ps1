<#
  Builds a PREVIEW manifest of candidate images from Wikimedia Commons (api via commons.wikimedia.org, honest user agent).
  Only files with a reuse-friendly licence (public domain, CC0, CC BY) and a long side of at least -MinSide pixels are listed,
  so low-resolution images are never offered. Share-alike (CC BY-SA) files are left out on purpose.
  Nothing is downloaded. Review the table, set "approved": true on the entries you accept, then run Get-Images.ps1 -Download.

  Usage: powershell -File New-CommonsManifest.ps1 -Query "Saint Andrew icon" -Slug 11-30-andrew -Kind icon -Out andrew.json [-Max 8] [-MinSide 1200]
#>
param(
    [Parameter(Mandatory)] [string]$Query,
    [Parameter(Mandatory)] [string]$Slug,
    [Parameter(Mandatory)] [string]$Out,
    [string]$Kind = 'icon',
    [int]$Max = 8,
    [int]$MinSide = 1200
)
$ErrorActionPreference = 'Stop'
$ua = 'Chronikologio-ImageTool/0.1 (https://github.com/giorstergiopoulos-design/san-shmera; personal non-commercial tool)'
$licenseMap = @{
    'Public domain' = 'Public Domain'; 'CC0' = 'CC0 1.0'; 'CC BY 4.0' = 'CC BY 4.0'; 'CC BY 3.0' = 'CC BY 3.0'; 'CC BY 2.5' = 'CC BY 2.5'; 'CC BY 2.0' = 'CC BY 2.0'
}
function Strip-Html([string]$s) { if (-not $s) { return '' }; ([regex]::Replace($s, '<[^>]+>', '') -replace '&amp;', '&' -replace '&quot;', '"' -replace '\s+', ' ').Trim() }

$u = 'https://commons.wikimedia.org/w/api.php?action=query&format=json&generator=search&gsrnamespace=6&gsrlimit=30&gsrsearch=' + [uri]::EscapeDataString($Query) +
     '&prop=imageinfo&iiprop=url|size|mime|extmetadata&iiurlwidth=1920&iiextmetadatafilter=LicenseShortName|Artist|ImageDescription'
$r = Invoke-RestMethod -UserAgent $ua -Uri $u
$entries = @()
if ($r.query) {
    foreach ($p in ($r.query.pages.PSObject.Properties.Value | Sort-Object index)) {
        $i = $p.imageinfo[0]
        $lic = $licenseMap[[string]$i.extmetadata.LicenseShortName.value]
        if (-not $lic) { continue }
        if ($i.mime -notin 'image/jpeg', 'image/png') { continue }
        if ([math]::Max($i.width, $i.height) -lt $MinSide) { continue }
        # 1920 is one of the standard thumbnail widths Wikimedia asks clients to use; smaller originals are taken as they are.
        $url = if ($i.width -gt 1920 -and $i.thumburl) { $i.thumburl } else { $i.url }
        $ext = if ($i.mime -eq 'image/png') { '.png' } else { '.jpg' }
        $creator = Strip-Html $i.extmetadata.Artist.value
        if (-not $creator) { $creator = 'Unknown author' }
        $entries += [pscustomobject]@{
            approved    = $false
            file        = ("$($Kind)s/$Slug-$($entries.Count + 1)$ext") -replace '[^A-Za-z0-9._/-]', '_'
            url         = $url
            caption     = (Strip-Html $p.title) -replace '^File:', '' -replace '\.(jpe?g|png)$', ''
            creator     = $creator
            license     = $lic
            sourceUrl   = $i.descriptionurl
            kind        = $Kind
            description = (Strip-Html $i.extmetadata.ImageDescription.value)
            pixels      = "$($i.width)x$($i.height)"
        }
        if ($entries.Count -ge $Max) { break }
    }
}
$entries | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 $Out
Write-Host "Wrote $($entries.Count) candidate(s) to $Out (nothing downloaded)."
foreach ($e in $entries) {
    $d = if ($e.description.Length -gt 110) { $e.description.Substring(0, 110) } else { $e.description }
    Write-Host ("[{0}] {1} | {2} | {3} | {4}`n      {5}" -f $e.file, $e.caption, $e.pixels, $e.license, $e.creator, $d)
}
