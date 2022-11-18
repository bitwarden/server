#!/usr/bin/env bash

dotnet tool restore

stty -echo
printf "Identity Server Dev Password (enter for no password):"
read identity_server_dev_pass
stty echo
printf "\n"

openssl req -x509 -newkey rsa:4096 -sha256 -nodes -keyout identity_server_dev.key -out identity_server_dev.crt \
    -subj "/CN=Bitwarden Identity Server Dev" -days 3650
openssl pkcs12 -export -out identity_server_dev.pfx -inkey identity_server_dev.key -in identity_server_dev.crt \
    -certfile identity_server_dev.crt -password pass:$identity_server_dev_pass

identity="$(openssl x509 -in identity_server_dev.crt -outform der | shasum -a 1 | head -c 40 | tr a-z A-Z)"

if [ -z "$identity_server_dev_pass"]; 
then
    dotnet tool run certificate-tool add --file ./identity_server_dev.pfx
else
    dotnet tool run certificate-tool add --file ./identity_server_dev.pfx --password $identity_server_dev_pass
fi

stty -echo
printf "Bitwarden Data Protection Dev Password (enter for no password):"
read bitwarden_data_protection_pass
stty echo
printf "\n"

openssl req -x509 -newkey rsa:4096 -sha256 -nodes -keyout data_protection_dev.key -out data_protection_dev.crt \
    -subj "/CN=Bitwarden Data Protection Dev" -days 3650
openssl pkcs12 -export -out data_protection_dev.pfx -inkey data_protection_dev.key -in data_protection_dev.crt \
    -certfile data_protection_dev.crt -password pass:$bitwarden_data_protection_pass

data="$(openssl x509 -in data_protection_dev.crt -outform der | shasum -a 1 | head -c 40 | tr a-z A-Z)"

if [ -z "$bitwarden_data_protection_pass"]; 
then
    dotnet tool run certificate-tool add --file ./data_protection_dev.pfx
else
    dotnet tool run certificate-tool add --file ./data_protection_dev.pfx --password $bitwarden_data_protection_pass
fi

echo "Certificate fingerprints:"

echo "Identity Server Dev: ${identity}"
echo "Data Protection Dev: ${data}"
