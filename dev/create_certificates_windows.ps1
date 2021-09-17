# Script for generating and installing the Bitwarden development certificates on Windows.

$params = @{
    'KeyAlgorithm' = 'RSA';
    'KeyLength' = 4096;
    'NotAfter' = (Get-Date).AddDays(3650);
    'CertStoreLocation' = 'Cert:\CurrentUser\My';
};

$params['Subject'] = 'CN=Bitwarden Identity Server Dev';
New-SelfSignedCertificate @params;

$params['Subject'] = 'CN=Bitwarden Data Protection Dev';
New-SelfSignedCertificate @params;
