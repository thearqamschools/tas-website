#!/bin/bash
# Compile-check the website's C# code on Linux with Mono (stubs replace MVC and Dapper).
# Razor views are not compiled here; Visual Studio compiles them (MvcBuildViews) on Windows.
set -e
HERE="$(cd "$(dirname "$0")" && pwd)"
WEB="$HERE/../../src/TheArqamSchools.Web"
mapfile -t FILES < <(find "$WEB" -name "*.cs" -not -path "*/obj/*" -not -path "*/bin/*")
mcs -langversion:experimental -target:library -nowarn:1591,169,649,414 -out:/tmp/arqam-check.dll \
  -r:System.Web.dll -r:System.Configuration.dll -r:System.Data.dll -r:System.Runtime.Caching.dll \
  -r:/usr/lib/mono/gac/Newtonsoft.Json/6.0.0.0__b9a188c8922137c6/Newtonsoft.Json.dll \
  -r:Microsoft.CSharp.dll -r:System.Core.dll -r:System.Xml.Linq.dll -r:System.Net.Http.dll -r:System.Drawing.dll \
  "$HERE/stubs.cs" "${FILES[@]}"
echo "C# compile check passed (${#FILES[@]} files)"
