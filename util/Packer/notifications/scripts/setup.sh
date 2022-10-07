#!/usr/bin/env bash
set -e

project_name="Notifications"

echo "======================"
echo "Set up nginx"
echo "======================"

apt-get update
apt-get -y install nginx unzip curl
nginx -v
systemctl enable nginx

echo "======================"
echo "Set up dotnet"
echo "======================"

wget -q https://packages.microsoft.com/config/ubuntu/18.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
dpkg -i packages-microsoft-prod.deb
add-apt-repository universe
apt-get update
apt-get -y install apt-transport-https
apt-get update
apt-get -y install dotnet-sdk-6.0
dotnet --info

echo "======================"
echo "Configure nginx"
echo "======================"

echo "Copying nginx configs"
mv /tmp/proxy.conf /etc/nginx/proxy.conf
mv /tmp/nginx.conf /etc/nginx/nginx.conf
mv /tmp/default /etc/nginx/sites-available/default

echo "Setting up SSL"
mkdir -p /etc/ssl/bitwarden.com
cp ~/bitwarden.com.crt /etc/ssl/bitwarden.com/bitwarden.com.crt
cp ~/bitwarden.com.key /etc/ssl/bitwarden.com/bitwarden.com.key
chown www-data:www-data /etc/ssl/bitwarden.com/bitwarden.com.crt
chown www-data:www-data /etc/ssl/bitwarden.com/bitwarden.com.key
rm ~/bitwarden.com.crt
rm ~/bitwarden.com.key

echo "======================"
echo "Configure Linux perf"
echo "======================"

sysctl -w net.ipv4.ip_local_port_range="5024 65000"
echo "net.ipv4.ip_local_port_range = 5024 65000" | tee -a /etc/sysctl.conf

echo "==========================="
echo "Setup Notifications Service"
echo "==========================="

echo "Unzip artifact"
mkdir /opt/bitwarden
unzip -q /tmp/Notifications.zip -d /opt/bitwarden/$project_name
chown -R www-data:www-data /opt/bitwarden/$project_name

cp ~/bitwarden.env /bitwarden.env

touch /etc/systemd/system/bitwarden.service
echo '[Unit]
Description=Bitwarden '""$project_name""'

[Service]
WorkingDirectory=/opt/bitwarden/'""$project_name""'/
ExecStart=/usr/bin/dotnet /opt/bitwarden/'""$project_name""'/'""$project_name""'.dll
Restart=always
RestartSec=10
SyslogIdentifier='""$project_name""'
User=www-data
EnvironmentFile=/bitwarden.env
LimitNOFILE=1048576

[Install]
WantedBy=multi-user.target' >/etc/systemd/system/bitwarden.service

systemctl enable bitwarden.service
systemctl start bitwarden.service
systemctl status bitwarden.service
