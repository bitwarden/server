#!/bin/bash

## start Structurizr Lite with the given workspace file, relative to the current working directory. Omit the file extension.
## Optional second argument of a port number to use. Default is 8085.

SCRIPT_PATH=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )
SCRIPT_DIR=$(basename "$SCRIPT_PATH")
PROJ_DIR=$(dirname "$SCRIPT_PATH")

echo $SCRIPT_PATH
echo $PROJ_DIR

echo "hosting on http://localhost:${PORT:=${2:-8085}}"
# Check if the workspace file exists
if [ ! -f "$SCRIPT_PATH/$1.dsl" ]; then
  echo "Workspace file $1.dsl does not exist in $SCRIPT_PATH."
  exit 1
fi

echo "Loading workspace file: $SCRIPT_PATH/$1.dsl"

docker run -it --rm -p $PORT:8080 -v $PROJ_DIR:/usr/local/structurizr -e STRUCTURIZR_WORKSPACE_FILENAME=$SCRIPT_DIR/$1 structurizr/lite
