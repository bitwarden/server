#!/usr/bin/env bash
set -e

DIR="$(dirname $(readlink -f $0))"

echo -e "\n# Building API"

echo -e "\nBuilding app"
echo -e ".NET Core version $(dotnet --version)"
dotnet publish $DIR/Api.csproj -f netcoreapp2.0 -c "Release" -o $DIR/obj/Docker/publish

echo -e "\nBuilding docker image"
docker --version
docker build -t bitwarden/api $DIR/.
