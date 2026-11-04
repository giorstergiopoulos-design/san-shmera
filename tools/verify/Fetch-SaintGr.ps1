<#
  Reads the day pages of the Synaxarion of saint.gr (a private, non-commercial site; no robots.txt, no terms against reading) to learn WHICH saints it lists on each date
  and where their lives are (https://www.saint.gr/<id>/saint.aspx). Only the list of titles and ids is kept in tools\verify\saintgr\MM-DD.json.
  The texts of the site are not copied into the app: the app's lives are written in our own words, and saint.gr is cited as a source and used to check names and dates.
  One request per 1.5 seconds, honest user agent. Re-running skips the days already saved.
#>
$ErrorActionPreference = 'Continue'
$ua = 'Chronikologio-VerifyTool/0.1 (https://github.com/giorstergiopoulos-design/san-shmera; personal non-commercial tool)'
$outDir = Join-Path $PSScriptRoot 'saintgr'
New-Item -ItemType Directory -Force $outDir | Out-Null
$utf8 = New-Object System.Text.UTF8Encoding($false)
for ($m = 1; $m -le 12; $m++) {
    for ($d = 1; $d -le [DateTime]::DaysInMonth(2024, $m); $d++) {
        $file = Join-Path $outDir ('{0:00}-{1:00}.json' -f $m, $d)
        if (Test-Path $file) { continue }
        $url = 'https://www.saint.gr/{0:00}/{1:00}/index.aspx' -f $m, $d
        try {
            $r = Invoke-WebRequest -UserAgent $ua -Uri $url -UseBasicParsing
            $items = @()
            $seen = @{}
            foreach ($x in [regex]::Matches($r.Content, '<a[^>]+href="/(\d+)/saint\.aspx"[^>]*>(.*?)</a>', 'Singleline')) {
                $id = $x.Groups[1].Value
                $title = ([regex]::Replace($x.Groups[2].Value, '<[^>]+>', '') -replace '&nbsp;', ' ' -replace '\s+', ' ').Trim()
                if ($title.Length -lt 4 -or $seen.ContainsKey($id)) { continue }
                $seen[$id] = $true
                $items += [pscustomobject]@{ id = [int]$id; title = $title }
            }
            [IO.File]::WriteAllText($file, (ConvertTo-Json -InputObject @($items) -Depth 3), $utf8)
            Write-Host ("{0:00}-{1:00}: {2} saints" -f $m, $d, $items.Count)
        } catch {
            Write-Host ("{0:00}-{1:00}: FAILED {2}" -f $m, $d, $_.Exception.Message)
            if ($_.Exception.Message -match '429') { Start-Sleep -Seconds 30 }
        }
        Start-Sleep -Milliseconds 1500
    }
}
