###############################################
#                 Build stage                 #
###############################################
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ENV PROJECT_NAME=Setup
ARG GIT_COMMIT

# Docker buildx supplies the value for this arg
ARG TARGETPLATFORM

# Determine proper runtime value for .NET
# We put the value in a file to be read by later layers.
RUN if [ "$TARGETPLATFORM" = "linux/amd64" ]; then \
    RID=linux-x64 ; \
    elif [ "$TARGETPLATFORM" = "linux/arm64" ]; then \
    RID=linux-arm64 ; \
    elif [ "$TARGETPLATFORM" = "linux/arm/v7" ]; then \
    RID=linux-arm ; \
    fi \
    && echo "RID=$RID" > /tmp/rid.txt

# Copy csproj files as distinct layers
WORKDIR /source
COPY util/${PROJECT_NAME}/*.csproj ./util/${PROJECT_NAME}/
COPY src/Core/*.csproj ./src/Core/
COPY util/Migrator/*.csproj ./util/Migrator/
COPY Directory.Build.props .

# Restore project dependencies and tools
WORKDIR /source/util/${PROJECT_NAME}
RUN . /tmp/rid.txt && dotnet restore -r $RID

# Copy required project files
WORKDIR /source
COPY util/${PROJECT_NAME}/. ./util/${PROJECT_NAME}/
COPY src/Core/. ./src/Core/
COPY util/Migrator/. ./util/Migrator/
COPY .editorconfig /source

# Build project
WORKDIR /source/util/${PROJECT_NAME}
RUN . /tmp/rid.txt && dotnet publish \
    --self-contained \
    /p:PublishSingleFile=true \
    /p:SourceRevisionId="$GIT_COMMIT" \
    -r $RID \
    -o out

WORKDIR /app

###############################################
#                  App stage                  #
###############################################
FROM mcr.microsoft.com/dotnet/aspnet:8.0

ARG TARGETPLATFORM
LABEL com.bitwarden.product="bitwarden" com.bitwarden.project="setup"

ENV PROJECT_NAME=Setup

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
    openssl \
    gosu \
    && rm -rf /var/lib/apt/lists/*

# Copy app from the build stage
WORKDIR /app
COPY --from=build /source/util/${PROJECT_NAME}/out .
COPY util/${PROJECT_NAME}/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

ENTRYPOINT ["/entrypoint.sh"]
