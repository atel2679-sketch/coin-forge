# IC-06: creates the Coin Forge release (upload) keystore. keytool asks for the password itself (hidden input);
# it is never written to a file, the repo or a log. Refuses to overwrite an existing keystore.
$ErrorActionPreference = 'Stop'
$keystore = 'F:\Keystores\coinforge-release.keystore'
$keytool  = 'E:\Unity\6000.3.27f1\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin\keytool.exe'

if (Test-Path $keystore) { throw "Keystore already exists: $keystore (not overwritten)" }

Write-Host "Creating $keystore (alias coinforge, RSA 2048, 10000 days)."
Write-Host "keytool will ask for a password (min. 6 characters) and to repeat it. Keep it safe: Play updates need it."
& $keytool -genkeypair -v -keystore $keystore -storetype PKCS12 -alias coinforge `
    -keyalg RSA -keysize 2048 -validity 10000 -dname 'CN=PublicVoid, O=PublicVoid'
if ($LASTEXITCODE -ne 0) { throw "keytool failed ($LASTEXITCODE)" }

# No "keytool -list" here: piping it hid nothing and echoed the typed password (IC-06 incident). Check later with
#   keytool -list -keystore <file>   (run directly, never through a pipe)
Write-Host "Done. Back up $keystore and its password somewhere safe (not in the repo)."
