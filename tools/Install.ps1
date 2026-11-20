# Installs the patched Sefirah build. Right-click > Run with PowerShell.
# Trusts the build's certificate (Trusted People, this PC only), then installs the MSIX
# as an update over the Store copy so your pairing and data stay.
$principal = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Start-Process powershell "-ExecutionPolicy Bypass -NoExit -File `"$PSCommandPath`"" -Verb RunAs
    exit
}
Set-Location $PSScriptRoot

Import-Certificate -FilePath .\SefirahPatched.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
Write-Host "Certificate trusted."

$msix = Get-ChildItem -Recurse -Filter *.msix | Where-Object { $_.DirectoryName -notlike '*Dependencies*' } | Select-Object -First 1
$deps = @(Get-ChildItem -Recurse -Path . -Include *.msix,*.appx | Where-Object { $_.DirectoryName -like '*Dependencies*' } | ForEach-Object FullName)
Write-Host "Installing $($msix.Name)..."
if ($deps.Count) {
    Add-AppxPackage -Path $msix.FullName -DependencyPath $deps -ForceApplicationShutdown -ForceUpdateFromAnyVersion
} else {
    Add-AppxPackage -Path $msix.FullName -ForceApplicationShutdown -ForceUpdateFromAnyVersion
}
Write-Host "Done. Start Sefirah from the Start menu."
