<#
  Cross-checks the names of OUR database against the day page of eortologio.net. Nothing from the site is stored or copied:
  the script only reports whether each name we list for a day also appears on that site's page for the same day.
  The site is "All rights reserved", so this is verification only; texts and lists are never taken from it.
  One request every 4 seconds, honest user agent, no retries.

  Usage: powershell -File Check-Names.ps1 -Month 11
#>
param([Parameter(Mandatory)] [int]$Month)
$ErrorActionPreference = 'Stop'
$ua = 'Chronikologio-Check/0.1 (https://github.com/giorstergiopoulos-design/san-shmera; personal non-commercial tool)'
$dataFile = Join-Path $PSScriptRoot ("..\..\data\orthodox-el\{0:00}.json" -f $Month)
$mf = Get-Content $dataFile -Raw -Encoding UTF8 | ConvertFrom-Json

function Fold([string]$s) {
    $d = $s.Normalize([Text.NormalizationForm]::FormD)
    $sb = New-Object Text.StringBuilder
    foreach ($c in $d.ToCharArray()) {
        if ([Globalization.CharUnicodeInfo]::GetUnicodeCategory($c) -eq 'NonSpacingMark') { continue }
        if ($c -eq [char]0x03C2) { [void]$sb.Append([char]0x03C3) } else { [void]$sb.Append([char]::ToLowerInvariant($c)) }
    }
    $sb.ToString()
}

foreach ($day in $mf.days) {
    $url = "https://www.eortologio.net/year/2026/month/$Month/day/$($day.day)/$($day.day)"
    try {
        $html = (Invoke-WebRequest -Uri $url -UserAgent $ua -UseBasicParsing -TimeoutSec 30).Content
    } catch { Write-Host "Day $($day.day): request failed ($($_.Exception.Message))"; Start-Sleep -Seconds 4; continue }
    $text = Fold ([regex]::Replace($html, '<script.*?</script>|<style.*?</style>|<[^>]+>', ' ', 'Singleline'))
    $missing = @()
    $found = 0
    foreach ($c in $day.commemorations) {
        foreach ($n in $c.names) {
            # a name counts as present if the name or one of its listed forms appears on the site's page
            $forms = @($n.name) + @($n.variants | Where-Object { $_ })
            if ($forms | Where-Object { $text.Contains((Fold $_)) }) { $found++ } else { $missing += $n.name }
        }
    }
    $status = if ($missing.Count -eq 0) { 'OK' } else { 'CHECK' }
    Write-Host ("Day {0,2}: {1}  names found {2}, not found: {3}" -f $day.day, $status, $found, ($missing -join ', '))
    Start-Sleep -Seconds 4
}
