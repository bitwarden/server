#!/usr/bin/env bash
set -e

OUTPUT_DIR=/etc/bitwarden

docker run -it --rm --name setup --network container:mssql -v $OUTPUT_DIR:/bitwarden bitwarden/setup \
    dotnet Setup.dll -update 1 -db 1

echo "Database update complete"
