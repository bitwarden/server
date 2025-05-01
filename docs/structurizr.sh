#!/bin/bash

## start Structurizr Lite with the given workspace file, relative to the current working directory. Omit the file extension.
## Optional second argument of a port number to use. Default is 8085.

PORT=${2:-8085}
# Check if the workspace file exists
if [ ! -f "$1.dsl" ]; then
  echo "Workspace file $1 does not exist."
  exit 1
fi

docker run -it --rm -p $PORT:8080 -v $(pwd):/usr/local/structurizr -e STRUCTURIZR_WORKSPACE_FILENAME=$1 structurizr/lite
