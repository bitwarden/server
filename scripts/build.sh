#!/usr/bin/env bash

set -e


##############################
# Description of the function.
# Globals: 
#   global variables
# Arguments: 
#   1 - arg1
# Outputs: 
#   Output to STDOUT or STDERR.
# Returns: 
#   Returned values other than the default exit status of the last command run.
##############################

##############################
# Build a .NET project
# Arguments: 
#   1 - Name of the project
#   2 - Directory where the project lives
#   3 - .NET Project file
##############################
build_dotnet() {
  local project_name=$1
  local project_dir=$2
  local project_file=$3

  echo -e "\nBuilding app"
  echo "=====Restore====="
  dotnet restore "$project_dir/$project_file"
  echo "=====Clean====="
  dotnet clean "$project_dir/$project_file" -c "Release" -o "build/$project_name"
  echo "=====Publish====="
  dotnet publish "$project_dir/$project_file" -c "Release" -o "build/$project_name"
}

##############################
# Build a .NET docker container
# Arguments: 
#   1 - Name of the project
##############################
build_docker() {
  local project_name=$1

  echo "Building docker image for: $project_name"
  #echo -e "\nBuilding docker image"
  #docker build -t bitwarden/$(echo $project_name | awk '{print lower($0)}') "build/$project_name"
}

##############################
# Build the Api project
# Arguments: 
#   1 - project name
#   2 - Build the docker container
##############################
api() {
  local build_docker=$1

  local project_name="Api"
  local project_dir="$PWD/src/Api"
  local project_file="Api.csproj"
  
  echo -e "\n## Building API"
  build_dotnet $project_name $project_dir $project_file
  
  if [[ $build_docker -eq 1 ]]; then
    build_docker $project_name
  fi
}

# Get command
PROJECT=$1; shift

# Get Params
BUILD_DOCKER=0

while [ ! $# -eq 0 ]; do
  case "$1" in
    --docker ) BUILD_DOCKER=1 ;;
    -h | --help ) usage && exit ;;
    *) usage && exit ;;
  esac
  shift
done

case "$PROJECT" in
  api | Api) api $BUILD_DOCKER ;;
esac
