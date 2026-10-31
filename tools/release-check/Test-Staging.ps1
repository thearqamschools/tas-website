# PowerShell 7. Read-only apart from an intentionally rejected POST without CSRF.
param([Parameter(Mandatory)][uri]$BaseUrl)
$ErrorActionPreference = 'Stop'
if ($BaseUrl.Scheme -ne 'https') { throw 'Use a staging URL with a trusted HTTPS certificate.' }
function Check([string]$Path, [int]$Status, [string]$Method = 'GET') {
    $response = Invoke-WebRequest -Uri ([uri]::new($BaseUrl, $Path)) -Method $Method -SkipHttpErrorCheck -MaximumRedirection 0
    if ([int]$response.StatusCode -ne $Status) { throw "$Method $Path expected $Status, got $($response.StatusCode)" }
    foreach ($name in @('X-Content-Type-Options','X-Frame-Options','Referrer-Policy','Permissions-Policy')) {
        if (-not $response.Headers[$name]) { throw "Missing $name on $Path" }
    }
    if ($response.Content -match 'Stack Trace:|Server Error in|PRIVATE_TEST_MARKER') { throw "Unsafe error response on $Path" }
    Write-Output "PASS $Method $Path => $Status with security headers"
}
Check '/' 200
Check '/campuses' 200
Check '/campuses?q=Samanabad' 200
Check '/campuses?q=%3Cscript%3Ealert%281%29%3C%2Fscript%3E' 200
Check '/release-check-path-that-does-not-exist' 404
Check '/admissions/inquiry' 400 'POST'
Write-Output 'Still required: database row checks, valid submissions, rate limit, outage/recovery, IIS-native errors and browser/device checks in the runbook.'
