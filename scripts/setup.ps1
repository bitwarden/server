param (
    [string]$outputDir = "c:/bitwarden"
)

if(!(Test-Path -Path $outputDir )){
    New-Item -ItemType directory -Path $outputDir
}

docker --version

[string]$installId = $( Read-Host "(!) Enter your installation id (get it at https://bitwarden.com/host/)" )
[string]$installKey = $( Read-Host "(!) Enter your installation key" )
[string]$domain = $( Read-Host "(!) Enter the domain name for your bitwarden instance (ex. bitwarden.company.com)" )
[string]$letsEncrypt = $( Read-Host "(!) Do you want to use Let's Encrypt to generate a free SSL certificate? (y/n)" )

$databasePassword=-join ((48..57) + (97..122) | Get-Random -Count 32 | % {[char]$_})

if($letsEncrypt -eq "y") {
    [string]$email = $( Read-Host "(!) Enter your email address (Let's Encrypt will send you certificate expiration reminders)" )
    
    $letsEncryptPath = "${outputDir}/letsencrypt/live/${domain}"
    if(!(Test-Path -Path $letsEncryptPath )){
        New-Item -ItemType directory -Path $letsEncryptPath
    }
    docker run -it --rm --name certbot -p 80:80 -v $outputDir/letsencrypt:/etc/letsencrypt/ certbot/certbot certonly --standalone --noninteractive --preferred-challenges http --email $email --agree-tos -d $domain
}

docker run -it --rm --name setup -v ${outputDir}:/bitwarden bitwarden/setup dotnet Setup.dll -domain ${domain} -letsencrypt ${letsEncrypt} -db_pass ${databasePassword} -install_id $installId -install_key $installKey

echo "Setup complete"
