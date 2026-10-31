<#
.SYNOPSIS
  Uploads a published site folder to SmarterASP.NET (or any IIS host) over FTPS.

.DESCRIPTION
  Used by .github/workflows/deploy-smarterasp.yml. Credentials come only from
  environment variables (GitHub Secrets), never from parameters, so they are not
  written to the log.

  Order of work:
    1. upload app_offline.htm, so IIS stops the site and releases bin\*.dll;
    2. remove the host's placeholder start pages (they would hide the MVC homepage);
    3. upload every file, creating folders as needed (3 tries per file);
    4. delete app_offline.htm, also when an upload failed, so the site comes back.

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
$credential = New-Object System.Net.NetworkCredential($env:FTP_USERNAME, $env:FTP_PASSWORD)
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12

function Get-FtpUri([string]$relative) {
    $segments = @()
    foreach ($s in (($remoteRoot + '/' + $relative) -split '/')) {
        if ($s -ne '') { $segments += [Uri]::EscapeDataString($s) }
    }
    return "ftp://$server/" + ($segments -join '/')
}

function New-FtpRequest([string]$relative, [string]$method) {
    $request = [System.Net.FtpWebRequest]::Create((Get-FtpUri $relative))
    $request.Method = $method
    $request.Credentials = $credential
    $request.EnableSsl = $true        # explicit FTPS; the password never travels in plain text
    $request.UsePassive = $true
    $request.UseBinary = $true
    $request.KeepAlive = $true
    $request.Timeout = 60000
    $request.ReadWriteTimeout = 120000
    return $request
}

function Get-FtpCode($exception) {
    $inner = $exception
    while ($inner -ne $null -and -not ($inner -is [System.Net.WebException])) { $inner = $inner.InnerException }
    if ($inner -ne $null -and $inner.Response -is [System.Net.FtpWebResponse]) { return [int]$inner.Response.StatusCode }
    return 0
}

$createdFolders = @{}
function New-FtpFolder([string]$relative) {
    if ($relative -eq '' -or $createdFolders.ContainsKey($relative)) { return }
    $parent = Split-Path $relative -Parent
    if ($parent) { New-FtpFolder ($parent -replace '\\', '/') }
    try {
        (New-FtpRequest $relative ([System.Net.WebRequestMethods+Ftp]::MakeDirectory)).GetResponse().Close()
    }
    catch {
        # 550 = the folder already exists (or cannot be made; the upload will then report it).
        if ((Get-FtpCode $_.Exception) -ne 550) { throw }
    }
    $createdFolders[$relative] = $true
}

function Send-FtpFile([string]$localPath, [string]$relative) {
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        try {
            $request = New-FtpRequest $relative ([System.Net.WebRequestMethods+Ftp]::UploadFile)
            $bytes = [System.IO.File]::ReadAllBytes($localPath)
            $request.ContentLength = $bytes.Length
            $stream = $request.GetRequestStream()
            try { $stream.Write($bytes, 0, $bytes.Length) } finally { $stream.Close() }
            $request.GetResponse().Close()
            return
        }
        catch {
            if ($attempt -eq 3) { throw "Upload failed for ${relative}: $($_.Exception.Message)" }
            Start-Sleep -Seconds (2 * $attempt)
        }
    }
}

function Remove-FtpFile([string]$relative) {
    try {
        (New-FtpRequest $relative ([System.Net.WebRequestMethods+Ftp]::DeleteFile)).GetResponse().Close()
        return $true
    }
    catch {
        if ((Get-FtpCode $_.Exception) -eq 550) { return $false }   # not there
        throw
    }
}

$sourceRoot = (Resolve-Path $Source).Path.TrimEnd('\')
$files = Get-ChildItem -Path $sourceRoot -Recurse -File
"Files to upload: $($files.Count)"
"Target: ftps://$server$remoteRoot/"

New-FtpFolder ''
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
        $folder = Split-Path $relative -Parent
        if ($folder) { New-FtpFolder ($folder -replace '\\', '/') }
        Send-FtpFile $file.FullName $relative
        $done++
        if ($done % 25 -eq 0) { "Uploaded $done / $($files.Count)" }
    }
    "Uploaded $done files"
}
catch {
    $failed = $_
}
finally {
    if (Remove-FtpFile 'app_offline.htm') { "Site back online (app_offline.htm removed)" }
}

if ($failed) { throw $failed }
