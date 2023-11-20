###############################################
#                 Build stage                 #
###############################################

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:6.0 AS bitwarden-build

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
COPY util/MsSqlMigratorUtility/*.csproj ./util/MsSqlMigratorUtility/
COPY Directory.Build.props .

# Restore project dependencies and tools
WORKDIR /source/util/MsSqlMigratorUtility
RUN . /tmp/rid.txt && dotnet restore -r $RID

# Copy required project files
WORKDIR /source
COPY src/Core/. ./src/Core/
COPY util/Migrator/. ./util/Migrator/
COPY util/MsSqlMigratorUtility/. ./util/MsSqlMigratorUtility/
COPY .git/. ./.git/

# Build project
WORKDIR /source/util/MsSqlMigratorUtility
RUN . /tmp/rid.txt && dotnet publish -c release -o /app/MsSqlMigratorUtility --no-restore --no-self-contained -r $RID

WORKDIR /app

###############################################
#                  App stage                  #
###############################################
FROM mcr.microsoft.com/dotnet/aspnet:6.0

ARG TARGETPLATFORM
LABEL com.bitwarden.product="bitwarden"

# Copy app from the build stage
WORKDIR /app
COPY --from=bitwarden-build /app/MsSqlMigratorUtility ./

ENTRYPOINT ["sh", "-c", "dotnet /app/MsSqlMigratorUtility.dll \"${MSSQL_CONN_STRING}\" -v ${@}", "--" ]
