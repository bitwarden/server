#!/usr/bin/env bash
set -e

DIR="$(dirname $(readlink -f $0))"

echo -e "\n# Building Identity"

echo -e "\nBuilding app"
echo -e ".NET Core version $(dotnet --version)"
dotnet publish $DIR/Identity.csproj -f netcoreapp2.0 -c "Release" -o $DIR/obj/Docker/publish

echo -e "\nBuilding docker image"
docker --version
docker build -t bitwarden/identity $DIR/.
