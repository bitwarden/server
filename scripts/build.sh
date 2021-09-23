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
##############################
build_dotnet() {
  local project_name=$1
  local project_dir=$2

  local project_file="$project_name.csproj"

  echo -e "\n## Building $project_name"
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
# Build a .NET docker container
# Arguments: 
#   1 - Name of the project
#   2 - Build Docker flag
#   3 - Director where project lives
##############################
build() {
  local project_name=$1
  local build_docker=$2
  local project_dir=$3

  build_dotnet $project_name $project_dir
  
  if [[ $build_docker -eq 1 ]]; then
    build_docker $project_name
  fi
}

# Get command
PROJECT=$1;
if [ "$PROJECT" != "" ]; then
  shift
fi

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

# Run script
case "$PROJECT" in
  admin | Admin) build "Admin" $BUILD_DOCKER "$PWD/src/Admin" ;;
  api | Api) build "Api" $BUILD_DOCKER "$PWD/src/Api" ;;
  billing | Billing) build "Billing" $BUILD_DOCKER "$PWD/src/Billing" ;;
  events | Events) build "Events" $BUILD_DOCKER "$PWD/src/Events" ;;
  identity | Identity) build "Identity" $BUILD_DOCKER "$PWD/src/Identity" ;;
  portal | Portal) build "Portal" $BUILD_DOCKER "$PWD/bitwarden_license/src/Portal" ;;
  sso | Sso) build "Sso" $BUILD_DOCKER "$PWD/bitwarden_license/src/Sso" ;;
  * | "")
    echo "building all"
    build "Admin" $BUILD_DOCKER "$PWD/src/Admin"
    build "Api" $BUILD_DOCKER "$PWD/src/Api"
    build "Billing" $BUILD_DOCKER "$PWD/src/Billing"
    build "Events" $BUILD_DOCKER "$PWD/src/Events"
    build "Identity" $BUILD_DOCKER "$PWD/src/Identity"
    build "Portal" $BUILD_DOCKER "$PWD/bitwarden_license/src/Portal"
    build "Sso" $BUILD_DOCKER "$PWD/bitwarden_license/src/Sso"
    ;;
esac
