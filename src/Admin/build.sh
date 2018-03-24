#!/usr/bin/env bash
set -e

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

echo -e "\n## Building Admin"

echo -e "\nBuilding app"
echo ".NET Core version $(dotnet --version)"
echo "Clean"
dotnet clean $DIR/Admin.csproj -f netcoreapp2.0 -c "Release" -o $DIR/obj/Docker/publish
echo "Publish"
dotnet publish $DIR/Admin.csproj -f netcoreapp2.0 -c "Release" -o $DIR/obj/Docker/publish

echo -e "\nBuilding docker image"
docker --version
docker build -t bitwarden/admin $DIR/.
