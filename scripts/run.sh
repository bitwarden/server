#!/usr/bin/env bash
set -e

DIR="$(dirname $(readlink -f $0))"
DOCKER_DIR=$DIR/../docker

docker --version
docker-compose --version

docker-compose -f $DOCKER_DIR/docker-compose.yml -f $DOCKER_DIR/docker-compose.windows.yml down
docker-compose -f $DOCKER_DIR/docker-compose.yml -f $DOCKER_DIR/docker-compose.windows.yml up -d
