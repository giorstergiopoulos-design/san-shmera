<#
  Builds a PREVIEW manifest of images from the NASA Image and Video Library (images-api.nasa.gov, NASA media are public domain
  unless the entry says otherwise). Nothing is downloaded. Review the manifest, set "approved": true on the entries you accept,
  then run Get-Images.ps1 -Download.

  Usage: powershell -File New-NasaManifest.ps1 -Query "apollo 11 armstrong" -Max 5 -Kind photo -Slug 1969-07-20-apollo11 -Out apollo11.json
#>
param(
    [Parameter(Mandatory)] [string]$Query,
    [int]$Max = 5,
    [string]$Kind = 'photo',
    [Parameter(Mandatory)] [string]$Slug,
    [Parameter(Mandatory)] [string]$Out
)
$ErrorActionPreference = 'Stop'
$uri = 'https://images-api.nasa.gov/search?media_type=image&q=' + [uri]::EscapeDataString($Query)
$response = Invoke-RestMethod -Uri $uri -UserAgent 'Chronikologio-ImageTool/0.1'
$entries = @()
foreach ($item in ($response.collection.items | Select-Object -First $Max)) {
    $data = $item.data[0]
    $id = $data.nasa_id
    $creator = if ($data.photographer) { $data.photographer } elseif ($data.secondary_creator) { $data.secondary_creator } else { "NASA ($($data.center))" }
    $entries += [pscustomobject]@{
        approved  = $false
        file      = "$($Kind)s/$Slug-nasa-$id.jpg" -replace '[^A-Za-z0-9._/-]', '_'
        url       = "https://images-assets.nasa.gov/image/$id/$id~medium.jpg"
        caption   = $data.title
        creator   = $creator
        license   = 'NASA public domain'
        sourceUrl = "https://images.nasa.gov/details/$id"
        kind      = $Kind
    }
}
$entries | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 $Out
Write-Host "Wrote $($entries.Count) entries to $Out (nothing downloaded). Run Get-Images.ps1 -Manifest $Out to see sizes."
