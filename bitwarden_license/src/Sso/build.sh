#!/usr/bin/env bash
set -e

CUR_DIR="$(pwd)"
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && "pwd")"

echo -e "\n## Building Sso"

echo -e "\nBuilding app"
echo ".NET Core version $(dotnet --version)"
echo "Restore"
dotnet restore "$DIR/Sso.csproj"
echo "Clean"
dotnet clean "$DIR/Sso.csproj" -c "Release" -o "$DIR/obj/build-output/publish"
echo "Node Build"
cd "$DIR"
npm ci
npm run build
cd "$CUR_DIR"
echo "Publish"
dotnet publish "$DIR/Sso.csproj" -c "Release" -o "$DIR/obj/build-output/publish"
