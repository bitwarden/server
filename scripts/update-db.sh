#!/usr/bin/env bash
set -e

OUTPUT_DIR=~/bitwarden
if [ $# -eq 1 ]
then
    OUTPUT_DIR=$1
fi

docker run -it --rm --name setup --network container:mssql -v $OUTPUT_DIR:/bitwarden bitwarden/setup \
    dotnet Setup.dll -update 1 -db 1

echo "Database update complete"
