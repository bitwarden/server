#!/usr/bin/env bash
set -e

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && "pwd" )"

echo -e "\n## Building API"

echo -e "\nBuilding app"
echo ".NET Core version $(dotnet --version)"
echo "Restore"
dotnet restore "$DIR/Api.csproj"
echo "Clean"
dotnet clean "$DIR/Api.csproj" -c "Release" -o "$DIR/obj/build-output/publish"
echo "Publish"
dotnet publish "$DIR/Api.csproj" -c "Release" -o "$DIR/obj/build-output/publish"
