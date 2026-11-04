<#
  Fetches the "Events" section of the Greek Wikipedia day pages ("25 Μαρτίου") through the official MediaWiki API (honest user agent, one request per 1.5 s)
  and saves the plain text of each list item to tools\history\el\MM-DD.txt. The files are a CANDIDATE LIST only: the app's texts are written from scratch
  and the encyclopedia is never quoted or linked as the only source.
  Usage: powershell -File Fetch-GreekDayPages.ps1
#>
$ErrorActionPreference = 'Continue'
$ua = 'Chronikologio-HistoryTool/0.1 (https://github.com/giorstergiopoulos-design/san-shmera; personal non-commercial tool)'
$months = 'Ιανουαρίου','Φεβρουαρίου','Μαρτίου','Απριλίου','Μαΐου','Ιουνίου','Ιουλίου','Αυγούστου','Σεπτεμβρίου','Οκτωβρίου','Νοεμβρίου','Δεκεμβρίου'
$outDir = Join-Path $PSScriptRoot 'el'
New-Item -ItemType Directory -Force $outDir | Out-Null
$utf8 = New-Object System.Text.UTF8Encoding($false)

function Clean([string]$s) {
    $s = [regex]::Replace($s, '<ref[^>]*>.*?</ref>', '')
    $s = [regex]::Replace($s, '<ref[^>]*/>', '')
    $s = [regex]::Replace($s, '\{\{[^{}]*\}\}', '')
    $s = [regex]::Replace($s, '\[\[(?:[^\]|]*\|)?([^\]]*)\]\]', '$1')
    $s = $s -replace "'''?", '' -replace '<[^>]+>', ''
    return $s.Trim()
}

for ($m = 1; $m -le 12; $m++) {
    for ($d = 1; $d -le [DateTime]::DaysInMonth(2024, $m); $d++) {
        $file = Join-Path $outDir ('{0:00}-{1:00}.txt' -f $m, $d)
        if (Test-Path $file) { continue }
        $title = "$d $($months[$m - 1])"
        $u = 'https://el.wikipedia.org/w/api.php?action=parse&format=json&prop=wikitext&redirects=1&page=' + [uri]::EscapeDataString($title)
        try {
            $r = Invoke-RestMethod -UserAgent $ua -Uri $u
            $text = $r.parse.wikitext.'*'
            $lines = @()
            $inEvents = $false
            foreach ($line in ($text -split "`n")) {
                if ($line -match '^==\s*(.+?)\s*==\s*$') { $inEvents = ($Matches[1] -match 'Γεγονότα|Συμβάντα'); continue }
                if ($inEvents -and $line -match '^\*+\s*(.+)$') { $c = Clean $Matches[1]; if ($c.Length -gt 8) { $lines += $c } }
            }
            [IO.File]::WriteAllText($file, ($lines -join "`n") + "`n", $utf8)
            Write-Host ("{0}: {1} items" -f $title, $lines.Count)
        } catch {
            Write-Host ("{0}: FAILED {1}" -f $title, $_.Exception.Message)
            if ($_.Exception.Message -match '429') { Start-Sleep -Seconds 30 }
        }
        Start-Sleep -Milliseconds 1500
    }
}
