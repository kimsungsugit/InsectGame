$ErrorActionPreference = "Stop"

$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$toolsRoot = Join-Path $projectRoot ".codex\tools"
$javaRoot = Join-Path $toolsRoot "jre21"
$java = Get-ChildItem -Path $javaRoot -Recurse -Filter "java.exe" -ErrorAction SilentlyContinue |
    Select-Object -First 1

if ($null -eq $java) {
    $zip = Join-Path $toolsRoot "temurin-jre21.zip"
    New-Item -ItemType Directory -Force -Path $toolsRoot | Out-Null
    Write-Host "Java 21 runtime downloading..."
    Invoke-WebRequest -UseBasicParsing `
        -Uri "https://api.adoptium.net/v3/binary/latest/21/ga/windows/x64/jre/hotspot/normal/eclipse" `
        -OutFile $zip
    New-Item -ItemType Directory -Force -Path $javaRoot | Out-Null
    Expand-Archive -LiteralPath $zip -DestinationPath $javaRoot
    $java = Get-ChildItem -Path $javaRoot -Recurse -Filter "java.exe" |
        Select-Object -First 1
}

if ($null -eq $java) {
    throw "Java 21 runtime was not found."
}

$env:JAVA_HOME = Split-Path (Split-Path $java.FullName -Parent) -Parent
$env:PATH = (Join-Path $env:JAVA_HOME "bin") + ";" + $env:PATH
# 첫 Functions 로드 시 firebase-admin/googleapis 초기화가 10초를 넘을 수 있다.
$env:FUNCTIONS_DISCOVERY_TIMEOUT = "60"

Push-Location $projectRoot
try {
    & npx --yes firebase-tools@15.22.1 emulators:exec `
        --only "auth,firestore,functions" `
        --project "insect-exploration-8f0ca" `
        "node functions/emulator/social-pvp-flow.js"
    if ($LASTEXITCODE -ne 0) {
        throw "Social PvP emulator test failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}
