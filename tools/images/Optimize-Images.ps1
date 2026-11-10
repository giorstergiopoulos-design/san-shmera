<#
  Makes the photographs and icons of data\images smaller and in one format: every .jpg/.jpeg/.png becomes WebP (no side longer than -MaxSide pixels,
  quality -Quality) and the names in the data files are changed to the new names. Run it after downloading new images (New-QueryImages*.ps1, New-SaintImages*.ps1).
  The work is done by tools\ImageConvert (a small .NET program that uses ImageSharp). Files that are already WebP are not touched.
  Usage: powershell -File Optimize-Images.ps1 [-MaxSide 900] [-Quality 80]
#>
param([int]$MaxSide = 900, [int]$Quality = 80)
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
dotnet run --project (Join-Path $root 'tools\ImageConvert') -c Release -- (Join-Path $root 'data') --max $MaxSide --quality $Quality
