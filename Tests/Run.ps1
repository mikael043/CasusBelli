param([string]$OutputDirectory = "$env:TEMP/cb-politica/bin")
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$game = Join-Path ${env:ProgramFiles(x86)} 'Steam/steamapps/common/Mount & Blade II Bannerlord'
$env:CB_TEST_GAME = $game
$env:CB_TEST_TITLES = Join-Path $project 'Modules/CasusBelli/ModuleData/titles.xml'
$config = [xml](Get-Content -LiteralPath $env:CB_TEST_TITLES -Raw)
$baseKingdoms = ([xml](Get-Content "$game/Modules/Sandbox/ModuleData/spkingdoms.xml" -Raw)).SelectNodes("//*[@id]").id
foreach ($pair in @(@('deJure','heroes'),@('settlement','settlements'),@('faction','spkingdoms'))) {
    $attribute, $catalog = $pair
    $native = [xml](Get-Content -LiteralPath "$game/Modules/Sandbox/ModuleData/$catalog.xml" -Raw)
    $ids = $native.SelectNodes('//*[@id]').id
    foreach ($node in $config.SelectNodes("//*[@$attribute]")) {
        if ($node.SelectSingleNode("ancestor-or-self::kingdom").faction -eq "nord" -and "nord" -cnotin $baseKingdoms) { continue }
        if ($node.GetAttribute($attribute) -cnotin $ids) { throw "Referência desconhecida em titles.xml: $($node.GetAttribute($attribute))" }
    }
}
dotnet msbuild "$project/CasusBelli.csproj" /p:Configuration=Debug /p:Platform=x64 "/p:OutputPath=$OutputDirectory/" "/p:IntermediateOutputPath=$OutputDirectory/../obj/" /v:q
if ($LASTEXITCODE) { exit $LASTEXITCODE }
$framework = "$env:windir/Microsoft.NET/Framework64/v4.0.30319"
$refs = @(Get-ChildItem "$game/bin/Win64_Shipping_Client/TaleWorlds*.dll" -Exclude TaleWorlds.Native.dll | ForEach-Object { '/r:' + $_.FullName })
$refs += @("/r:$framework/netstandard.dll","/r:$game/Modules/Bannerlord.Harmony/bin/Win64_Shipping_Client/0Harmony.dll","/r:$OutputDirectory/CasusBelli.dll")
& "$framework/csc.exe" /nologo /debug:full "/out:$OutputDirectory/check.exe" $refs (Join-Path $PSScriptRoot CampaignChecks.cs)
if ($LASTEXITCODE) { exit $LASTEXITCODE }
& "$OutputDirectory/check.exe"
if ($LASTEXITCODE) { exit $LASTEXITCODE }
$env:CB_TEST_TITLES = Join-Path $PSScriptRoot "titles-legacy.xml"
& "$OutputDirectory/check.exe"
exit $LASTEXITCODE