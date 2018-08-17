#!/usr/bin/env bash
set -e

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

echo ""

if [ $# -gt 1 -a "$1" == "push" ]
then
    TAG=$2

    echo "Pushing Bitwarden ($TAG)"
    echo "========================"
    
    docker push bitwarden/api:$TAG
    docker push bitwarden/identity:$TAG
    docker push bitwarden/server:$TAG
    docker push bitwarden/attachments:$TAG
    docker push bitwarden/icons:$TAG
    docker push bitwarden/notifications:$TAG
    docker push bitwarden/admin:$TAG
    docker push bitwarden/nginx:$TAG
    docker push bitwarden/mssql:$TAG
    docker push bitwarden/setup:$TAG
elif [ $# -gt 1 -a "$1" == "tag" ]
then
    TAG=$2
    
    echo "Tagging Bitwarden as '$TAG'"
    
    docker tag bitwarden/api bitwarden/api:$TAG
    docker tag bitwarden/identity bitwarden/identity:$TAG
    docker tag bitwarden/server bitwarden/server:$TAG
    docker tag bitwarden/attachments bitwarden/attachments:$TAG
    docker tag bitwarden/icons bitwarden/icons:$TAG
    docker tag bitwarden/notifications bitwarden/notifications:$TAG
    docker tag bitwarden/admin bitwarden/admin:$TAG
    docker tag bitwarden/nginx bitwarden/nginx:$TAG
    docker tag bitwarden/mssql bitwarden/mssql:$TAG
    docker tag bitwarden/setup bitwarden/setup:$TAG
else
    echo "Building Bitwarden"
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

    chmod u+x $DIR/src/Icons/build.sh
    $DIR/src/Icons/build.sh

    chmod u+x $DIR/src/Notifications/build.sh
    $DIR/src/Notifications/build.sh

    chmod u+x $DIR/src/Admin/build.sh
    $DIR/src/Admin/build.sh

    chmod u+x $DIR/util/MsSql/build.sh
    $DIR/util/MsSql/build.sh

    chmod u+x $DIR/util/Setup/build.sh
    $DIR/util/Setup/build.sh
fi
