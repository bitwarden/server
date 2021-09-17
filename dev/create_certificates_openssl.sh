openssl req -x509 -newkey rsa:4096 -sha256 -nodes -keyout identity_server_dev.key -out identity_server_dev.crt -subj "/CN=Bitwarden Identity Server Dev" -days 3650
openssl pkcs12 -export -out identity_server_dev.pfx -inkey identity_server_dev.key -in identity_server_dev.crt -certfile identity_server_dev.crt

openssl req -x509 -newkey rsa:4096 -sha256 -nodes -keyout data_protection_dev.key -out data_protection_dev.crt -subj "/CN=Bitwarden Data Protection Dev" -days 3650
openssl pkcs12 -export -out data_protection_dev.pfx -inkey data_protection_dev.key -in data_protection_dev.crt -certfile data_protection_dev.crt
