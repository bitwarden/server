param (
    [string]$outputDir = "../.",
    [string]$coreVersion = "latest",
    [string]$webVersion = "latest"
)

if(!(Test-Path -Path $outputDir )){
    New-Item -ItemType directory -Path $outputDir | Out-Null
}

[string]$letsEncrypt = "n"
Write-Host "(!) " -f cyan -nonewline
[string]$domain = $( Read-Host "Enter the domain name for your bitwarden instance (ex. bitwarden.company.com)" )
echo ""

if($domain -eq "") {
    $domain = "localhost"
}

if($domain -ne "localhost") {
    Write-Host "(!) " -f cyan -nonewline
    $letsEncrypt = $( Read-Host "Do you want to use Let's Encrypt to generate a free SSL certificate? (y/n)" )
    echo ""

    if($letsEncrypt -eq "y") {
        Write-Host "(!) " -f cyan -nonewline
        [string]$email = $( Read-Host "Enter your email address (Let's Encrypt will send you certificate expiration reminders)" )
        echo ""

        $letsEncryptPath = "${outputDir}/letsencrypt"
        if(!(Test-Path -Path $letsEncryptPath )){
            New-Item -ItemType directory -Path $letsEncryptPath | Out-Null
        }
        docker pull certbot/certbot
        docker run -it --rm --name certbot -p 80:80 -v $outputDir/letsencrypt:/etc/letsencrypt/ certbot/certbot `
            certonly --standalone --noninteractive --agree-tos --preferred-challenges http --email $email -d $domain `
            --logs-dir /etc/letsencrypt/logs
    }
}

docker pull bitwarden/setup:$coreVersion
docker run -it --rm --name setup -v ${outputDir}:/bitwarden bitwarden/setup:$coreVersion `
    dotnet Setup.dll -install 1 -domain ${domain} -letsencrypt ${letsEncrypt} -os win -corev $coreVersion -webv $webVersion

echo ""
echo "Setup complete"
echo ""
