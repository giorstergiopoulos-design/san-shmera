<#
  Reads the day lists of https://www.synaxarion.gr (robots.txt allows it) and writes, for every day, the commemorations it lists:
  MM-DD|sid|title|beginning of the text. The output is a reading aid kept OUTSIDE the repository (-Out): the texts of the site are not copied
  into the app, they are only read to check which saints a day has and to write our own wording. One request every 2 seconds, generic client name.
  Usage: powershell -File Get-SynaxarionDays.ps1 -Out <file> [-From 1] [-To 366]
#>
param([Parameter(Mandatory)] [string]$Out, [int]$From = 1, [int]$To = 366)
$ErrorActionPreference = 'Continue'
$ua = 'Chronikologio-DataTool/0.1 (personal non-commercial tool)'
Add-Type -AssemblyName System.Web
$utf8 = New-Object System.Text.UTF8Encoding($false)
function Clean([string]$s) { ([System.Web.HttpUtility]::HtmlDecode([System.Web.HttpUtility]::HtmlDecode(([regex]::Replace($s, '<[^>]+>', ' ')))) -replace '\s+', ' ').Trim() }
$lines = New-Object Collections.Generic.List[string]
if (Test-Path $Out) { foreach ($l in Get-Content $Out -Encoding UTF8) { $lines.Add($l) } }
$have = @{}; foreach ($l in $lines) { $have[$l.Substring(0, 5)] = $true }
$start = [datetime]'2024-01-01'
for ($i = $From - 1; $i -lt $To; $i++) {
    $d = $start.AddDays($i)
    $key = $d.ToString('MM-dd')
    if ($have.ContainsKey($key)) { continue }
    $url = "https://www.synaxarion.gr/gr/m/$($d.Month)/d/$($d.Day)/sxsaintlist.aspx"
    try {
        $html = (Invoke-WebRequest -UserAgent $ua -Uri $url -UseBasicParsing -TimeoutSec 40).Content
        $blocks = [regex]::Split($html, '(?=<h4)')
        $n = 0
        foreach ($b in $blocks) {
            if ($b -notmatch '(?s)^<h4[^>]*>(.*?)</h4>') { continue }
            $title = Clean $Matches[1]
            $sid = if ($b -match '/gr/sid/(\d+)/sxsaintinfo') { $Matches[1] } else { '' }
            $snip = ''
            if ($b -match '(?s)padding-top: 4px;">(.*?)</td>') { $snip = Clean $Matches[1] }
            if ($snip.Length -gt 240) { $snip = $snip.Substring(0, 240) }
            $lines.Add("$key|$sid|$title|$snip".Replace("`r", ' ')); $n++
        }
        if ($n -eq 0) { $lines.Add("$key|||") }
        Write-Host "$key $n"
    } catch { Write-Host "$key FAILED $($_.Exception.Message)"; if ($_.Exception.Message -match '429') { Start-Sleep -Seconds 60 } }
    [IO.File]::WriteAllLines($Out, $lines, $utf8)
    Start-Sleep -Seconds 2
}
