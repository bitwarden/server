#!/usr/bin/env bash
set -e

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

echo -e "\n# Building Attachments"

echo -e "\nBuilding docker image"
docker --version
docker build -t bitwarden/attachments $DIR/.
