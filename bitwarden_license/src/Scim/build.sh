#!/usr/bin/env bash
set -e

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && "pwd" )"

echo -e "\n## Building SCIM"

echo -e "\nBuilding app"
echo ".NET Core version $(dotnet --version)"
echo "Restore"
dotnet restore "$DIR/Scim.csproj"
echo "Clean"
dotnet clean "$DIR/Scim.csproj" -c "Release" -o "$DIR/obj/build-output/publish"
echo "Publish"
dotnet publish "$DIR/Scim.csproj" -c "Release" -o "$DIR/obj/build-output/publish"
