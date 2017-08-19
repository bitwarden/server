#!/usr/bin/env bash
set -e

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

echo -e "\n# Building API"

echo -e "\nBuilding app"
echo -e ".NET Core version $(dotnet --version)"
dotnet publish $DIR/Api.csproj -f netcoreapp2.0 -c "Release" -o $DIR/obj/Docker/publish/Api
dotnet publish $DIR/../Jobs/Jobs.csproj -f netcoreapp2.0 -c "Release" -o $DIR/obj/Docker/publish/Jobs

echo -e "\nBuilding docker image"
docker --version
docker build -t bitwarden/api $DIR/.
