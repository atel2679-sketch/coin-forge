# IC-06: builds the signed Play Store AAB (CoinForgeSetup.BuildReleaseAab).
# Asks for the keystore password (hidden input) and hands it to Unity through process environment variables only;
# nothing is written to disk and the variables are cleared afterwards. Keystore is PKCS12: key password = store password.
$ErrorActionPreference = 'Stop'
$unity   = 'E:\Unity\6000.3.27f1\Editor\Unity.exe'
$project = Split-Path -Parent $PSScriptRoot

$secure = Read-Host 'Keystore password for F:\Keystores\coinforge-release.keystore' -AsSecureString
$bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
try {
    $env:CF_KEYSTORE_PATH = 'F:\Keystores\coinforge-release.keystore'
    $env:CF_KEY_ALIAS     = 'coinforge'
    $env:CF_KEYSTORE_PASS = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    $env:CF_KEY_PASS      = $env:CF_KEYSTORE_PASS
    Write-Host 'Building release AAB (10-15 min)...'
    $p = Start-Process -FilePath $unity -Wait -PassThru -NoNewWindow -ArgumentList @(
        '-batchmode', '-quit', '-nographics', '-projectPath', "`"$project`"", '-buildTarget', 'Android',
        '-executeMethod', 'CoinForgeSetup.BuildReleaseAab', '-logFile', "`"$project\Logs\ic06-release.log`"")
    Write-Host "Unity exit code: $($p.ExitCode)"
}
finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    Remove-Item Env:CF_KEYSTORE_PASS, Env:CF_KEY_PASS, Env:CF_KEYSTORE_PATH, Env:CF_KEY_ALIAS -ErrorAction SilentlyContinue
}
Get-ChildItem "$project\Builds\CoinForge-*-*.aab" | Sort-Object LastWriteTime | Select-Object -Last 1 Name, Length, LastWriteTime
