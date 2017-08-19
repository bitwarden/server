param (
    [string]$outputDir = "c:/bitwarden"
)

$year = (Get-Date).year

Write-Host @'
 _     _ _                         _            
| |__ (_) |___      ____ _ _ __ __| | ___ _ __  
| '_ \| | __\ \ /\ / / _` | '__/ _` |/ _ \ '_ \ 
| |_) | | |_ \ V  V / (_| | | | (_| |  __/ | | |
|_.__/|_|\__| \_/\_/ \__,_|_|  \__,_|\___|_| |_|
'@

Write-Host "
Open source password management solutions
Copyright 2015-${year}, 8bit Solutions LLC
https://bitwarden.com, https://github.com/bitwarden
"

if(!(Test-Path -Path $outputDir )){
    New-Item -ItemType directory -Path $outputDir
}

docker --version
echo ""

[string]$domain = $( Read-Host "(!) Enter the domain name for your bitwarden instance (ex. bitwarden.company.com)" )
[string]$letsEncrypt = $( Read-Host "(!) Do you want to use Let's Encrypt to generate a free SSL certificate? (y/n)" )

if($letsEncrypt -eq "y") {
    [string]$email = $( Read-Host "(!) Enter your email address (Let's Encrypt will send you certificate expiration reminders)" )
    
    $letsEncryptPath = "${outputDir}/letsencrypt/live/${domain}"
    if(!(Test-Path -Path $letsEncryptPath )){
        New-Item -ItemType directory -Path $letsEncryptPath
    }
    docker run -it --rm --name certbot -p 80:80 -v $outputDir/letsencrypt:/etc/letsencrypt/ certbot/certbot `
        certonly --standalone --noninteractive --agree-tos --preferred-challenges http --email $email -d $domain
}

docker run -it --rm --name setup -v ${outputDir}:/bitwarden bitwarden/setup `
    dotnet Setup.dll -install 1 -domain ${domain} -letsencrypt ${letsEncrypt}

echo "Setup complete"
