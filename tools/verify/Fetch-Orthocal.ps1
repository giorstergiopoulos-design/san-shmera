<#
  Cross-check source for the fixed feasts: the open calendar Orthocal (orthocal.info, code under the MIT licence), which follows the OCA calendar.
  The app's dates are those of the revised Julian calendar, which for fixed feasts equal the dates of the Julian calendar (Greek church).
  Orthocal takes a Gregorian date and returns the commemorations of the matching Julian date, so for a Julian date D we ask for D + 13 days (valid 1900-2100).
  Output: tools\verify\orthocal\MM-DD.json with the names of the saints and feasts of each Julian date. One request per 1.5 seconds, honest user agent.
  The texts of Orthocal are NOT used: only the names are compared with the app's own, to confirm that a date is right.
#>
$ErrorActionPreference = 'Continue'
$ua = 'Chronikologio-VerifyTool/0.1 (https://github.com/giorstergiopoulos-design/san-shmera; personal non-commercial tool)'
$outDir = Join-Path $PSScriptRoot 'orthocal'
New-Item -ItemType Directory -Force $outDir | Out-Null
for ($m = 1; $m -le 12; $m++) {
    for ($d = 1; $d -le [DateTime]::DaysInMonth(2024, $m); $d++) {
        $file = Join-Path $outDir ('{0:00}-{1:00}.json' -f $m, $d)
        if (Test-Path $file) { continue }
        $g = (Get-Date -Year 2024 -Month $m -Day $d).Date.AddDays(13)
        $u = 'https://orthocal.info/api/julian/{0}/{1}/{2}/' -f $g.Year, $g.Month, $g.Day
        try {
            $r = Invoke-RestMethod -UserAgent $ua -Uri $u
            $names = @($r.saints) + @($r.feasts) + @($r.summary_title) | Where-Object { $_ } | Select-Object -Unique
            $obj = [pscustomobject]@{ url = $u; julian = ('{0:00}-{1:00}' -f $r.month, $r.day); names = @($names) }
            [IO.File]::WriteAllText($file, ($obj | ConvertTo-Json -Depth 4), (New-Object System.Text.UTF8Encoding($false)))
            Write-Host ("{0:00}-{1:00}: {2} names (julian {3}/{4})" -f $m, $d, @($names).Count, $r.month, $r.day)
        } catch {
            Write-Host ("{0:00}-{1:00}: FAILED {2}" -f $m, $d, $_.Exception.Message)
            if ($_.Exception.Message -match '429') { Start-Sleep -Seconds 30 }
        }
        Start-Sleep -Milliseconds 1500
    }
}
