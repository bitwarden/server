param (
    [string]$outputDir = "../."
)

if(!(Test-Path -Path $outputDir )){
    New-Item -ItemType directory -Path $outputDir | Out-Null
}

docker --version
echo ""

[string]$letsEncrypt = "n"
[string]$domain = $( Read-Host "(!) Enter the domain name for your bitwarden instance (ex. bitwarden.company.com)" )

if($domain -ne "localhost") {
    $letsEncrypt = $( Read-Host "(!) Do you want to use Let's Encrypt to generate a free SSL certificate? (y/n)" )

    if($letsEncrypt -eq "y") {
        [string]$email = $( Read-Host "(!) Enter your email address (Let's Encrypt will send you certificate expiration reminders)" )
        
        $letsEncryptPath = "${outputDir}/letsencrypt"
        if(!(Test-Path -Path $letsEncryptPath )){
            New-Item -ItemType directory -Path $letsEncryptPath | Out-Null
        }
        docker run -it --rm --name certbot -p 80:80 -v $outputDir/letsencrypt:/etc/letsencrypt/ certbot/certbot `
            certonly --standalone --noninteractive --agree-tos --preferred-challenges http --email $email -d $domain `
            --logs-dir /etc/letsencrypt/logs
    }
}

docker run -it --rm --name setup -v ${outputDir}:/bitwarden bitwarden/setup `
    dotnet Setup.dll -install 1 -domain ${domain} -letsencrypt ${letsEncrypt}

echo "Setup complete"
