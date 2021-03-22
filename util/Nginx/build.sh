#!/usr/bin/env bash
set -e

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

echo -e "\n## Building Nginx"

echo -e "\nBuilding docker image"
docker --version
docker build -t bitwarden/nginx "$DIR/."


echo -e "\n## Building k8s-proxy"
docker build -f Dockerfile-k8s -t bitwarden/k8s-proxy "$DIR/."
