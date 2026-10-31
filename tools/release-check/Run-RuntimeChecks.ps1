param([string]$WebRoot = (Join-Path $PSScriptRoot '../../src/TheArqamSchools.Web'))
$ErrorActionPreference = 'Stop'
$WebRoot = (Resolve-Path $WebRoot).Path
if (-not (Test-Path "$WebRoot/bin/TheArqamSchools.Web.dll")) { throw 'Build the website first.' }
$compiler = Join-Path $WebRoot 'bin/roslyn/csc.exe'
$scratch = Join-Path ([IO.Path]::GetTempPath()) ('tas-release-' + [Guid]::NewGuid().ToString('N'))
$site = Join-Path $scratch 'site'
New-Item -ItemType Directory -Force $site | Out-Null
# Keep the isolated directory for inspection; it contains no working credentials.
Copy-Item "$WebRoot/*" $site -Recurse -Force
[xml]$config = Get-Content "$site/Web.config"
$config.configuration.connectionStrings.add.connectionString = 'Data Source=127.0.0.1,1;Initial Catalog=UnavailableReleaseTest;Integrated Security=True;Connect Timeout=1'
$config.Save("$site/Web.config")
Set-Content "$site/release-test.aspx" '<%@ Page Language="C#" %><script runat="server">protected void Page_Load(object sender, EventArgs e) { var unsafeValue = Request.QueryString["unsafe"]; throw new System.Web.HttpException(int.Parse(Request.QueryString["code"]), "PRIVATE_TEST_MARKER"); }</script>'
$exe = Join-Path $scratch 'RuntimeChecks.exe'
& $compiler /nologo /target:exe "/reference:$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/System.Web.dll" "/out:$exe" "$PSScriptRoot/RuntimeChecks.cs"
if ($LASTEXITCODE -ne 0) { throw 'Harness compile failed' }
Copy-Item $exe "$site/bin/RuntimeChecks.exe"
Set-Content "$exe.config" '<configuration><startup><supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8"/></startup></configuration>'
& $exe ($site + '\')
if ($LASTEXITCODE -ne 0) { throw "Runtime checks failed; inspect $scratch" }
Write-Output "Isolated test copy: $scratch (not a deployment)"
