#!/usr/bin/env bash
set -e

echo "Repo tag name: ${APPVEYOR_REPO_TAG_NAME}"
echo "Rebuild: ${APPVEYOR_RE_BUILD}"

PROD_DEPLOY=false
TAG_NAME=""
if [ "${APPVEYOR_REPO_TAG_NAME}" != "" -a "${APPVEYOR_RE_BUILD}" == "True" ]
then
    PROD_DEPLOY=true
    TAG_NAME=${APPVEYOR_REPO_TAG_NAME#"v"}
fi

echo "Prod deploy: ${PROD_DEPLOY}"
echo "Tag name: ${TAG_NAME}"

#dotnet build --configuration Debug

if [ "${DOCKER_USERNAME}" != "" -a "${DOCKER_PASSWORD}" != "" ]
then
    echo "$DOCKER_PASSWORD" | docker login -u "$DOCKER_USERNAME" --password-stdin
fi

chmod +x ./build.sh
./build.sh
./build.sh tag dev

if [ "${PROD_DEPLOY}" == "true" ]
then
    ./build.sh tag beta
    ./build.sh tag $TAG_NAME
fi

docker images
./build.sh push dev

if [ "${PROD_DEPLOY}" == "true" ]
then
    ./build.sh push beta
    ./build.sh push latest
    ./build.sh push $TAG_NAME
fi
