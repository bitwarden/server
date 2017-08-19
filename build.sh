#!/usr/bin/env bash
set -e

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

echo ""
echo "Building bitwarden"
echo "=================="

chmod u+x $DIR/src/Api/build.sh
$DIR/src/Api/build.sh

chmod u+x $DIR/src/Identity/build.sh
$DIR/src/Identity/build.sh

chmod u+x $DIR/util/Server/build.sh
$DIR/util/Server/build.sh

chmod u+x $DIR/util/Nginx/build.sh
$DIR/util/Nginx/build.sh

chmod u+x $DIR/util/Attachments/build.sh
$DIR/util/Attachments/build.sh

chmod u+x $DIR/util/MsSql/build.sh
$DIR/util/MsSql/build.sh

chmod u+x $DIR/util/Setup/build.sh
$DIR/util/Setup/build.sh
