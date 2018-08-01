#!/usr/bin/env bash
set -e

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

echo -e "\n## Building Icons"

echo -e "\nBuilding app"
echo ".NET Core version $(dotnet --version)"
echo "Restore"
dotnet restore $DIR/Icons.csproj
echo "Clean"
dotnet clean $DIR/Icons.csproj -f netcoreapp2.1 -c "Release" -o $DIR/obj/Docker/publish
echo "Publish"
dotnet publish $DIR/Icons.csproj -f netcoreapp2.1 -c "Release" -o $DIR/obj/Docker/publish

echo -e "\nBuilding docker image"
docker --version
docker build -t bitwarden/icons $DIR/.
