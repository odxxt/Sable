# Requires PowerShell 7. The encrypted private key belongs outside the repository.
param([Parameter(Mandatory=$true)][string]$File, [Parameter(Mandatory=$true)][string]$PrivateKeyFile, [Parameter(Mandatory=$true)][string]$SignatureFile)
$ErrorActionPreference='Stop'
$secure=Read-Host 'Publisher key password' -AsSecureString
$plain=[Net.NetworkCredential]::new('', $secure).Password
$signer=[Security.Cryptography.ECDsa]::Create()
try {
    $read=0
    $signer.ImportEncryptedPkcs8PrivateKey($plain,[IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $PrivateKeyFile).Path),[ref]$read)
    $expected=[IO.File]::ReadAllBytes((Join-Path $PSScriptRoot 'publisher.pub'))
    if([Convert]::ToBase64String($signer.ExportSubjectPublicKeyInfo()) -ne [Convert]::ToBase64String($expected)){throw 'Private key does not match the app publisher key.'}
    $signature=$signer.SignData([IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $File).Path),[Security.Cryptography.HashAlgorithmName]::SHA256)
    [IO.File]::WriteAllBytes([IO.Path]::GetFullPath($SignatureFile),$signature)
    Write-Host 'Signature written. Repack the ZIP after signing its manifest.'
} finally {$plain=$null; $secure.Dispose(); $signer.Dispose()}
