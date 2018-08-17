#!/usr/bin/env bash
set -e

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

echo -e "\n## Building Notifications"

echo -e "\nBuilding app"
echo ".NET Core version $(dotnet --version)"
echo "Restore"
dotnet restore $DIR/Notifications.csproj
echo "Clean"
dotnet clean $DIR/Notifications.csproj -c "Release" -o $DIR/obj/Docker/publish
echo "Publish"
dotnet publish $DIR/Notifications.csproj -c "Release" -o $DIR/obj/Docker/publish

echo -e "\nBuilding docker image"
docker --version
docker build -t bitwarden/notifications $DIR/.
