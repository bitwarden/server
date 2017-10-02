#!/usr/bin/env bash
set -e

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

echo ""

if [ $# -gt 0 -a "$1" == "push" ]
then
    echo "Pushing bitwarden"
    echo "=================="
    
    if [ $# -gt 1 ]
    then
        TAG=$2
        
        docker tag api bitwarden/api:$TAG
        docker tag identity bitwarden/identity:$TAG
        docker tag server bitwarden/server:$TAG
        docker tag attachments bitwarden/attachments:$TAG
        docker tag nginx bitwarden/nginx:$TAG
        docker tag mssql bitwarden/mssql:$TAG
        docker tag setup bitwarden/setup:$TAG
        
        docker push bitwarden/api:$TAG
        docker push bitwarden/identity:$TAG
        docker push bitwarden/server:$TAG
        docker push bitwarden/attachments:$TAG
        docker push bitwarden/nginx:$TAG
        docker push bitwarden/mssql:$TAG
        docker push bitwarden/setup:$TAG
    else
        docker push bitwarden/api
        docker push bitwarden/identity
        docker push bitwarden/server
        docker push bitwarden/attachments
        docker push bitwarden/nginx
        docker push bitwarden/mssql
        docker push bitwarden/setup
    fi
else
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
fi
