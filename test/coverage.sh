#!/bin/bash

# Set defaults if no values supplied
CONFIGURATION="Release"
OUTPUT="CoverageOutput"
REPORT_TYPE="lcov"


# Read in arguments
while [[ $# -gt 0  ]]; do
    key="$1"

    case $key in
        -c|--configuration)

        CONFIGURATION="$2"
        shift
        shift
        ;;
        -o|--output)
        OUTPUT="$2"
        shift
        shift
        ;;
        -rt|--reportType)
        REPORT_TYPE="$2"
        shift
        shift
        ;;
        *)
        shift
        ;;
    esac
done

echo "CONFIGURATION = ${CONFIGURATION}"
echo "OUTPUT = ${OUTPUT}"
echo "REPORT_TYPE = ${REPORT_TYPE}"

echo "Collectiong Code Coverage"
# Install tools
dotnet tool restore
# Print Environment
dotnet --version

if [[ -d $OUTPUT ]]; then
    echo "Cleaning output location"
    rm -rf $OUTPUT
fi

dotnet test "./bitwarden.tests.sln" /p:CoverletOutputFormatter="cobertura" --collect:"XPlat Code Coverage" --results-directory:"$OUTPUT" -c $CONFIGURATION

dotnet tool run reportgenerator -reports:$OUTPUT/**/*.cobertura.xml -targetdir:$OUTPUT -reporttype:"$REPORT_TYPE"
