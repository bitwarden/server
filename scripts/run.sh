#!/usr/bin/env bash
set -e

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

OUTPUT_DIR="../."
if [ $# -eq 1 ]
then
    OUTPUT_DIR=$1
fi

DOCKER_DIR=$DIR/../docker
if [ $# -eq 2 ]
then
    DOCKER_DIR=$2
fi

OS="linux"
if [ "$(uname)" == "Darwin" ]
then
    OS="macwin"
fi

docker --version
docker-compose --version

LETS_ENCRYPT_LIVE = "${outputDir}/letsencrypt/live"
if [ -d "$LETS_ENCRYPT_LIVE" ]
then
    docker run -it --rm --name certbot -p 443:443 -p 80:80 -v $OUTPUT_DIR/letsencrypt:/etc/letsencrypt/ certbot/certbot \
        renew
fi

docker-compose -f $DOCKER_DIR/docker-compose.yml -f $DOCKER_DIR/docker-compose.$OS.yml down
docker-compose -f $DOCKER_DIR/docker-compose.yml -f $DOCKER_DIR/docker-compose.$OS.yml up -d
