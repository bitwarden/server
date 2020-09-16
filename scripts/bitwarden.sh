#!/usr/bin/env bash
set -e

cat << "EOF"
 _     _ _                         _            
| |__ (_) |___      ____ _ _ __ __| | ___ _ __  
| '_ \| | __\ \ /\ / / _` | '__/ _` |/ _ \ '_ \ 
| |_) | | |_ \ V  V / (_| | | | (_| |  __/ | | |
|_.__/|_|\__| \_/\_/ \__,_|_|  \__,_|\___|_| |_|

EOF

cat << EOF
Open source password management solutions
Copyright 2015-$(date +'%Y'), 8bit Solutions LLC
https://bitwarden.com, https://github.com/bitwarden

===================================================

EOF

docker --version
docker-compose --version

echo ""

# Setup

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
SCRIPT_NAME=`basename "$0"`
SCRIPT_PATH="$DIR/$SCRIPT_NAME"
OUTPUT="$DIR/bwdata"
if [ $# -eq 2 ]
then
    OUTPUT=$2
fi

SCRIPTS_DIR="$OUTPUT/scripts"
GITHUB_BASE_URL="https://raw.githubusercontent.com/bitwarden/server/master"
COREVERSION="1.37.1"
WEBVERSION="2.16.1"

# Functions

function downloadSelf() {
    if curl -s -w "http_code %{http_code}" -o $SCRIPT_PATH.1 $GITHUB_BASE_URL/scripts/bitwarden.sh | grep -q "^http_code 20[0-9]"
    then
        mv $SCRIPT_PATH.1 $SCRIPT_PATH
        chmod u+x $SCRIPT_PATH
    else
        rm -f $SCRIPT_PATH.1
    fi
}

function downloadRunFile() {
    if [ ! -d "$SCRIPTS_DIR" ]
    then
        mkdir $SCRIPTS_DIR
    fi
    curl -s -o $SCRIPTS_DIR/run.sh $GITHUB_BASE_URL/scripts/run.sh
    chmod u+x $SCRIPTS_DIR/run.sh
    rm -f $SCRIPTS_DIR/install.sh
}

function checkOutputDirExists() {
    if [ ! -d "$OUTPUT" ]
    then
        echo "Cannot find a Bitwarden installation at $OUTPUT."
        exit 1
    fi
}

function checkOutputDirNotExists() {
    if [ -d "$OUTPUT/docker" ]
    then
        echo "Looks like Bitwarden is already installed at $OUTPUT."
        exit 1
    fi
}

function listCommands() {
cat << EOT
Available commands:

install
start
restart
stop
update
updatedb
updaterun
updateself
updateconf
renewcert
rebuild
help

See more at https://help.bitwarden.com/article/install-on-premise/#script-commands

EOT
}

# Commands

if [ "$1" == "install" ]
then
    checkOutputDirNotExists
    mkdir -p $OUTPUT
    downloadRunFile
    $SCRIPTS_DIR/run.sh install $OUTPUT $COREVERSION $WEBVERSION
elif [ "$1" == "start" -o "$1" == "restart" ]
then
    checkOutputDirExists
    $SCRIPTS_DIR/run.sh restart $OUTPUT $COREVERSION $WEBVERSION
elif [ "$1" == "update" ]
then
    checkOutputDirExists
    downloadRunFile
    $SCRIPTS_DIR/run.sh update $OUTPUT $COREVERSION $WEBVERSION
elif [ "$1" == "rebuild" ]
then
    checkOutputDirExists
    $SCRIPTS_DIR/run.sh rebuild $OUTPUT $COREVERSION $WEBVERSION
elif [ "$1" == "updateconf" ]
then
    checkOutputDirExists
    $SCRIPTS_DIR/run.sh updateconf $OUTPUT $COREVERSION $WEBVERSION
elif [ "$1" == "updatedb" ]
then
    checkOutputDirExists
    $SCRIPTS_DIR/run.sh updatedb $OUTPUT $COREVERSION $WEBVERSION
elif [ "$1" == "stop" ]
then
    checkOutputDirExists
    $SCRIPTS_DIR/run.sh stop $OUTPUT $COREVERSION $WEBVERSION
elif [ "$1" == "renewcert" ]
then
    checkOutputDirExists
    $SCRIPTS_DIR/run.sh renewcert $OUTPUT $COREVERSION $WEBVERSION
elif [ "$1" == "updaterun" ]
then
    checkOutputDirExists
    downloadRunFile
elif [ "$1" == "updateself" ]
then
    downloadSelf && echo "Updated self." && exit
elif [ "$1" == "help" ]
then
    listCommands
else
    echo "No command found."
    echo
    listCommands
fi
