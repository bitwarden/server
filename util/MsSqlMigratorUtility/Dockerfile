###############################################
#                 Build stage                 #
###############################################
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build

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
COPY util/MsSqlMigratorUtility/*.csproj ./util/MsSqlMigratorUtility/
COPY src/Core/*.csproj ./src/Core/
COPY util/Migrator/*.csproj ./util/Migrator/
COPY util/Server/*.csproj ./util/Server/
COPY util/Server/Properties/*.csproj ./util/Server/Properties/
COPY util/Server/Properties/launchSettings.json ./util/Server/Properties/
COPY Directory.Build.props .

# Restore project dependencies and tools
WORKDIR /source/util/MsSqlMigratorUtility
RUN . /tmp/rid.txt && dotnet restore -r $RID

# Copy required project files
WORKDIR /source
COPY util/MsSqlMigratorUtility/. ./util/MsSqlMigratorUtility/
COPY src/Core/. ./src/Core/
COPY util/Migrator/. ./util/Migrator/
COPY util/Server/. ./util/Server/
COPY .git/. ./.git/
COPY .editorconfig /source

# Build project
WORKDIR /source/util/MsSqlMigratorUtility
RUN . /tmp/rid.txt && dotnet publish \
  --self-contained \
  /p:PublishSingleFile=true \
  -r $RID \
  -o out

WORKDIR /app

###############################################
#                  App stage                  #
###############################################
FROM mcr.microsoft.com/dotnet/aspnet:8.0

ARG TARGETPLATFORM
LABEL com.bitwarden.product="bitwarden"

ENV SSL_CERT_DIR=/etc/bitwarden/ca-certificates

# Copy app from the build stage
WORKDIR /app
COPY --from=build /source/util/MsSqlMigratorUtility/out /app

ENTRYPOINT ["sh", "-c", "/app/MsSqlMigratorUtility \"${MSSQL_CONN_STRING}\" ${@}", "--" ]
