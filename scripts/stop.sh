#!/usr/bin/env bash
set -e

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
DOCKER_DIR=$DIR/../docker
if [ $# -eq 1 ]
then
    DOCKER_DIR=$1
fi

OS="linux"
if [ "$(uname)" == "Darwin" ]
then
    OS="macwin"
fi

docker --version
docker-compose --version

docker-compose -f $DOCKER_DIR/docker-compose.yml -f $DOCKER_DIR/docker-compose.$OS.yml down
