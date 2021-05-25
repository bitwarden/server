#!/usr/bin/env bash
set -e

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

echo ""
echo "Building Bitwarden"
echo "=================="

chmod u+x "$DIR/src/Api/build.sh"
"$DIR/src/Api/build.sh"

chmod u+x "$DIR/src/Identity/build.sh"
"$DIR/src/Identity/build.sh"

chmod u+x "$DIR/util/Server/build.sh"
"$DIR/util/Server/build.sh"

chmod u+x "$DIR/util/Nginx/build.sh"
"$DIR/util/Nginx/build.sh"

chmod u+x "$DIR/util/Attachments/build.sh"
"$DIR/util/Attachments/build.sh"

chmod u+x "$DIR/src/Icons/build.sh"
"$DIR/src/Icons/build.sh"

chmod u+x "$DIR/src/Notifications/build.sh"
"$DIR/src/Notifications/build.sh"

chmod u+x "$DIR/src/Events/build.sh"
"$DIR/src/Events/build.sh"

chmod u+x "$DIR/src/Admin/build.sh"
"$DIR/src/Admin/build.sh"

chmod u+x "$DIR/bitwarden_license/src/Sso/build.sh"
"$DIR/bitwarden_license/src/Sso/build.sh"

chmod u+x "$DIR/bitwarden_license/src/Portal/build.sh"
"$DIR/bitwarden_license/src/Portal/build.sh"

chmod u+x "$DIR/util/MsSql/build.sh"
"$DIR/util/MsSql/build.sh"

chmod u+x "$DIR/util/Setup/build.sh"
"$DIR/util/Setup/build.sh"
