<#
  Shows (default) or downloads (-Download) the images of a manifest.
    * Without -Download: lists file name, source, licence and the size reported by the server. Nothing is saved.
    * With -Download: saves ONLY the entries marked "approved": true into data\images\<file>.
  Every entry is checked against policy.json (official host, allowed licence, extension, size cap, real image header).
  After a download it prints the "images" JSON to paste into the day record of the data file.

  Usage: powershell -File Get-Images.ps1 -Manifest apollo11.json [-Download]
#>
param(
    [Parameter(Mandatory)] [string]$Manifest,
    [switch]$Download
)
$ErrorActionPreference = 'Stop'
$policy = Get-Content (Join-Path $PSScriptRoot 'policy.json') -Raw | ConvertFrom-Json
$imagesRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\data\images'))
# Windows PowerShell 5.1 returns a JSON array as ONE object; piping it through ForEach-Object unrolls it into the entries.
$entries = @(Get-Content $Manifest -Raw | ConvertFrom-Json | ForEach-Object { $_ })

function Test-OfficialHost([string]$url) {
    $u = $null
    if (-not [uri]::TryCreate($url, [UriKind]::Absolute, [ref]$u) -or $u.Scheme -ne 'https') { return $false }
    foreach ($h in $policy.allowedHosts) { if ($u.Host -eq $h -or $u.Host.EndsWith('.' + $h)) { return $true } }
    return $false
}

function Get-Problems($e) {
    $p = @()
    if (-not (Test-OfficialHost $e.url)) { $p += 'download host is not an allowed official body' }
    if (-not (Test-OfficialHost $e.sourceUrl)) { $p += 'source page is not an allowed official body' }
    if ($policy.allowedLicenses -notcontains $e.license) { $p += "licence '$($e.license)' is not allowed" }
    if (-not $e.creator) { $p += 'creator is missing' }
    if (-not $e.caption) { $p += 'caption is missing' }
    if ($policy.allowedExtensions -notcontains ([IO.Path]::GetExtension($e.file).ToLowerInvariant())) { $p += 'extension is not allowed' }
    $full = [IO.Path]::GetFullPath((Join-Path $imagesRoot $e.file))
    if (-not $full.StartsWith($imagesRoot + [IO.Path]::DirectorySeparatorChar)) { $p += 'file name leaves data\images' }
    return $p
}

$report = foreach ($e in $entries) {
    $problems = @(Get-Problems $e)
    $size = $null
    if ($problems.Count -eq 0) {
        try {
            $head = Invoke-WebRequest -Uri $e.url -Method Head -UserAgent 'Chronikologio-ImageTool/0.1' -UseBasicParsing
            $size = [int64]($head.Headers['Content-Length'] | Select-Object -First 1)
            if ($size -gt $policy.maxBytes) { $problems += "larger than $($policy.maxBytes) bytes" }
        } catch { $problems += "size check failed: $($_.Exception.Message)" }
    }
    [pscustomobject]@{ Approved = [bool]$e.approved; File = $e.file; Source = $e.sourceUrl; License = $e.license; KB = if ($size) { [math]::Round($size / 1KB) } else { $null }; Problems = ($problems -join '; ') }
}
$report | Format-Table -AutoSize -Wrap | Out-String -Width 220 | Write-Host

if (-not $Download) { Write-Host 'Preview only. Nothing was downloaded.'; return }

$snippets = @()
foreach ($e in $entries) {
    if (-not $e.approved) { continue }
    $problems = @(Get-Problems $e)
    if ($problems.Count -gt 0) { Write-Warning "Skipped $($e.file): $($problems -join '; ')"; continue }
    $target = [IO.Path]::GetFullPath((Join-Path $imagesRoot $e.file))
    New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
    # One request at a time, politely spaced. On "429 Too many requests" wait as the server asks (Retry-After, else 60 s) and try once more;
    # if it is still refused, stop instead of pushing on.
    Start-Sleep -Seconds 4
    $saved = $false
    foreach ($attempt in 1, 2) {
        try {
            Invoke-WebRequest -Uri $e.url -OutFile $target -UserAgent 'Chronikologio-ImageTool/0.1 (https://github.com/giorstergiopoulos-design/san-shmera; personal non-commercial tool)' -UseBasicParsing
            $saved = $true; break
        } catch {
            $status = $_.Exception.Response.StatusCode.value__
            if ($status -ne 429 -or $attempt -eq 2) { Write-Warning "Download of $($e.file) failed (HTTP $status). Stopping."; break }
            $wait = [int]($_.Exception.Response.Headers['Retry-After'])
            if ($wait -lt 1) { $wait = 60 }
            Write-Host "Rate limited; waiting $wait s..."
            Start-Sleep -Seconds $wait
        }
    }
    if (-not $saved) { break }
    $bytes = [IO.File]::ReadAllBytes($target)
    $isJpeg = $bytes.Length -gt 3 -and $bytes[0] -eq 0xFF -and $bytes[1] -eq 0xD8
    $isPng = $bytes.Length -gt 4 -and $bytes[0] -eq 0x89 -and $bytes[1] -eq 0x50 -and $bytes[2] -eq 0x4E -and $bytes[3] -eq 0x47
    if (-not ($isJpeg -or $isPng) -or $bytes.Length -gt $policy.maxBytes) {
        Remove-Item $target -Force
        Write-Warning "Removed $($e.file): not a valid image or too large."
        continue
    }
    Add-Type -AssemblyName System.Drawing
    $img = [System.Drawing.Image]::FromFile($target)
    $longSide = [math]::Max($img.Width, $img.Height)
    $dimensions = "$($img.Width)x$($img.Height)"
    $img.Dispose()
    if ($longSide -lt $policy.minLongSidePixels) {
        Remove-Item $target -Force
        Write-Warning "Removed $($e.file): only $dimensions pixels (minimum long side $($policy.minLongSidePixels))."
        continue
    }
    Write-Host "Saved $($e.file) ($([math]::Round($bytes.Length / 1KB)) KB, $dimensions)"
    $snippets += [ordered]@{ file = $e.file; caption = $e.caption; creator = $e.creator; license = $e.license; sourceUrl = $e.sourceUrl; kind = $e.kind }
}
if ($snippets.Count -gt 0) {
    Write-Host "`nAdd this to the 'images' array of the matching record in data\...:"
    ($snippets | ConvertTo-Json -Depth 4) | Write-Host
}
