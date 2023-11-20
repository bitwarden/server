###############################################
#                 Build stage                 #
###############################################
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:6.0 AS build

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
COPY src/Core/*.csproj ./src/Core/
COPY util/Migrator/*.csproj ./util/Migrator/
COPY util/Setup/*.csproj ./util/Setup/
COPY Directory.Build.props .

# Restore project dependencies and tools
WORKDIR /source/util/Setup
RUN . /tmp/rid.txt && dotnet restore -r $RID

# Copy required project files
WORKDIR /source
COPY src/Core/. ./src/Core/
COPY util/Migrator/. ./util/Migrator/
COPY util/Setup/. ./util/Setup/
COPY .git/. ./.git/

# Build project
WORKDIR /source/util/Setup
RUN . /tmp/rid.txt && dotnet publish -c release -o /app/Setup --no-restore --no-self-contained -r $RID

WORKDIR /app

###############################################
#                  App stage                  #
###############################################
FROM mcr.microsoft.com/dotnet/aspnet:6.0

ARG TARGETPLATFORM
LABEL com.bitwarden.product="bitwarden" com.bitwarden.project="setup"

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        openssl \
        gosu \
&& rm -rf /var/lib/apt/lists/*

# Copy app from the build stage
WORKDIR /app
COPY --from=build /app/Setup ./
COPY util/Setup/entrypoint.sh /
RUN chmod +x /entrypoint.sh

ENTRYPOINT ["/entrypoint.sh"]
