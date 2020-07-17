#!/usr/bin/env bash
set -e

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

echo -e "\n## Building Icons"

echo -e "\nBuilding app"
echo ".NET Core version $(dotnet --version)"
echo "Restore"
dotnet restore "$DIR/Icons.csproj"
echo "Clean"
dotnet clean "$DIR/Icons.csproj" -c "Release" -o "$DIR/obj/Docker/publish"
echo "Publish"
dotnet publish "$DIR/Icons.csproj" -c "Release" -o "$DIR/obj/Docker/publish"

if [ "$1" != "nodocker" ]
then
    echo -e "\nBuilding docker image"
    docker --version
    docker build -t bitwarden/icons "$DIR/."
fi
