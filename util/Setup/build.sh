#!/usr/bin/env bash
set -e

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

echo -e "\n# Building Setup"

echo -e "\nBuilding app"
echo -e ".NET Core version $(dotnet --version)"
dotnet publish $DIR/Setup.csproj -f netcoreapp2.0 -c "Release" -o $DIR/obj/Docker/publish

echo -e "\nBuilding docker image"
docker --version
docker build -t bitwarden/setup $DIR/.
