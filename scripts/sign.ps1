# Script per la firma digitale Authenticode di Custode 2.0
$certSubject = "CN=Custode 2.0"

# 1. Trova il certificato Code Signing
$cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert | Where-Object { $_.Subject -eq $certSubject } | Select-Object -First 1

if (-not $cert) {
    Write-Host "Creazione certificato Code Signing: $certSubject..." -ForegroundColor Cyan
    $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject $certSubject -CertStoreLocation "Cert:\CurrentUser\My" -NotAfter (Get-Date).AddYears(10)
}

# Registra in TrustedPublisher (nessun prompt interattivo)
try {
    $pubStore = New-Object System.Security.Cryptography.X509Certificates.X509Store "TrustedPublisher", "CurrentUser"
    $pubStore.Open("ReadWrite")
    $pubStore.Add($cert)
    $pubStore.Close()
}
catch { }

# 2. Firma gli eseguibili
$filesToSign = @("src\Custode2\bin\Release\Custode2.exe", "bin\Custode2.exe")

foreach ($file in $filesToSign) {
    if (Test-Path $file) {
        Write-Host "Firma di $file con '$certSubject'..." -ForegroundColor Cyan
        try {
            $sig = Set-AuthenticodeSignature -FilePath $file -Certificate $cert -HashAlgorithm SHA256 -ErrorAction Stop
            Write-Host "File firmato con successo! Editore: $($sig.SignerCertificate.Subject)" -ForegroundColor Green
        }
        catch {
            Write-Warning "Errore firma su $file : $_"
        }
    }
}
