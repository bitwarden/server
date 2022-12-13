#!/usr/bin/env bash

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

echo -e "\n## Building Billing"

echo -e "\nBuilding app"
echo -e ".NET Core version $(dotnet --version)"
echo -e "Restore"
dotnet restore $DIR/Billing.csproj
echo -e "Clean"
dotnet clean $DIR/Billing.csproj -c "Release" -o $DIR/obj/build-output/publish
echo -e "Publish"
dotnet publish $DIR/Billing.csproj -c "Release" -o $DIR/obj/build-output/publish
