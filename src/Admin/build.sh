#!/usr/bin/env bash
set -e

CUR_DIR="$(pwd)"
DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

echo -e "\n## Building Admin"

echo -e "\nBuilding app"
echo ".NET Core version $(dotnet --version)"
echo "Restore"
dotnet restore "$DIR/Admin.csproj"
echo "Clean"
dotnet clean "$DIR/Admin.csproj" -c "Release" -o "$DIR/obj/build-output/publish"
echo "Node Build"
cd "$DIR"
npm install
cd "$CUR_DIR"
gulp --gulpfile "$DIR/gulpfile.js" build
echo "Publish"
dotnet publish "$DIR/Admin.csproj" -c "Release" -o "$DIR/obj/build-output/publish"
