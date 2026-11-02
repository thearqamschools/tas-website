<#
.SYNOPSIS
  Uploads a published site folder to SmarterASP.NET (or any IIS host) over FTPS with curl.

.DESCRIPTION
  Used by .github/workflows/deploy-smarterasp.yml. Credentials come only from
  environment variables (GitHub Secrets) and are passed to curl through a
  temporary netrc file, never on the command line or in the log.

  Order of work:
    1. upload app_offline.htm, so IIS stops the site and releases bin\*.dll;
    2. remove the host's placeholder start pages (they would hide the MVC homepage);
    3. upload every file (curl creates missing folders; 3 tries per file);
    4. only when every file arrived, delete app_offline.htm (4 tries). After a failed
       upload the "being updated" page stays, so visitors never get a half-new site.

  Files already on the server that are not in the new build are left in place.

  Environment variables:
    FTP_SERVER      host name only, for example win1234.site4now.net
    FTP_USERNAME    FTP user
    FTP_PASSWORD    FTP password
    FTP_REMOTE_DIR  site folder on the FTP server, for example /site1 ("/" for the FTP root)
#>
param(
    [Parameter(Mandatory = $true)][string]$Source,
    [Parameter(Mandatory = $true)][string]$OfflinePage
)

$ErrorActionPreference = 'Stop'

foreach ($name in 'FTP_SERVER', 'FTP_USERNAME', 'FTP_PASSWORD', 'FTP_REMOTE_DIR') {
    if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name))) {
        throw "Missing setting $name (add it under GitHub > Settings > Secrets and variables > Actions)."
    }
}

$server = $env:FTP_SERVER.Trim() -replace '^(?i)ftps?://', '' -replace '/+$', ''
$remoteRoot = '/' + ($env:FTP_REMOTE_DIR.Trim().Trim('/'))
if ($remoteRoot -eq '/') { $remoteRoot = '' }
$curl = (Get-Command curl.exe -ErrorAction Stop).Source

# netrc keeps the password off the command line; deleted at the end.
$netrc = Join-Path ([IO.Path]::GetTempPath()) ("ftp-" + [Guid]::NewGuid().ToString("N") + ".netrc")
Set-Content -Path $netrc -Value ("machine $server login $($env:FTP_USERNAME) password $($env:FTP_PASSWORD)") -NoNewline -Encoding ascii

function Get-FtpUrl([string]$relative) {
    $segments = @()
    foreach ($s in (($remoteRoot + '/' + $relative) -split '/')) {
        if ($s -ne '') { $segments += [Uri]::EscapeDataString($s) }
    }
    return "ftp://$server/" + ($segments -join '/')
}

function Invoke-Curl([string[]]$arguments) {
    $all = @('--silent', '--show-error', '--ssl-reqd', '--netrc-file', $netrc, '--connect-timeout', '30', '--max-time', '300') + $arguments
    $output = & $curl @all 2>&1
    return @{ Code = $LASTEXITCODE; Output = ($output | Out-String).Trim() }
}

function Send-FtpFile([string]$localPath, [string]$relative) {
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        $r = Invoke-Curl @('--ftp-create-dirs', '--upload-file', $localPath, (Get-FtpUrl $relative))
        if ($r.Code -eq 0) { return }
        if ($attempt -eq 3) { throw "Upload failed for ${relative} (curl exit $($r.Code)): $($r.Output)" }
        Start-Sleep -Seconds (2 * $attempt)
    }
}

# Deletes a file in the site folder. Returns $true when deleted, $false when it was not there.
function Remove-FtpFile([string]$relative) {
    $folder = Get-FtpUrl ''
    $r = Invoke-Curl @('--list-only', '--quote', ("-DELE " + ($remoteRoot + '/' + $relative)), ($folder + '/'))
    if ($r.Code -eq 0) { return $true }
    if ($r.Output -match '550' -or $r.Code -eq 21) { return $false }   # 21 = quote command failed (file not found)
    throw "Could not delete ${relative} (curl exit $($r.Code)): $($r.Output)"
}

try {
    $sourceRoot = (Resolve-Path $Source).Path.TrimEnd('\')
    $files = Get-ChildItem -Path $sourceRoot -Recurse -File
    "Files to upload: $($files.Count)"
    "Target: ftps://$server$remoteRoot/"
    & $curl --version | Select-Object -First 1

    Send-FtpFile $OfflinePage 'app_offline.htm'
    "Site taken offline (app_offline.htm uploaded)"
    Start-Sleep -Seconds 3

    $failed = $null
    try {
        # Host placeholder pages: IIS would serve these at "/" instead of the MVC homepage.
        foreach ($placeholder in 'index.html', 'index.htm', 'default.htm', 'default.html', 'default.aspx', 'hostingstart.html') {
            if (Remove-FtpFile $placeholder) { "Removed host placeholder: $placeholder" }
        }

        $done = 0
        foreach ($file in $files) {
            $relative = $file.FullName.Substring($sourceRoot.Length + 1) -replace '\\', '/'
            if ($relative -ieq 'app_offline.htm') { continue }
            Send-FtpFile $file.FullName $relative
            $done++
            if ($done % 25 -eq 0) { "Uploaded $done / $($files.Count)" }
        }
        "Uploaded $done files"
    }
    catch {
        $failed = $_
    }

    if ($failed) {
        # Half the files may be new and half old: keep the "being updated" page up rather than
        # serve a mixed site. Run the deployment again; it uploads everything and then removes it.
        Write-Host "::error::Upload failed; the site stays on the 'being updated' page until the next successful deployment."
        throw $failed
    }

    $removed = $false
    for ($attempt = 1; $attempt -le 4 -and -not $removed; $attempt++) {
        try {
            [void](Remove-FtpFile 'app_offline.htm')
            $removed = $true
            "Site back online (app_offline.htm removed)"
        }
        catch {
            if ($attempt -eq 4) { throw "All files uploaded, but app_offline.htm could not be removed: $($_.Exception.Message). Delete it by FTP or run the deployment again." }
            Start-Sleep -Seconds (3 * $attempt)
        }
    }
}
finally {
    Remove-Item -Force $netrc -ErrorAction SilentlyContinue
}
