# syntax = docker/dockerfile:1.11
###############################################
#                 Build stage                 #
###############################################
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build-dotnet

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

# Add packages
RUN apt-get update && apt-get install -y \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Set up Node
ENV NODE_VERSION=16.20.2
RUN curl -o- https://raw.githubusercontent.com/nvm-sh/nvm/v0.40.1/install.sh | bash
ENV NVM_DIR=/root/.nvm
RUN . "$NVM_DIR/nvm.sh" && nvm install ${NODE_VERSION}
RUN . "$NVM_DIR/nvm.sh" && nvm use v${NODE_VERSION}
RUN . "$NVM_DIR/nvm.sh" && nvm alias default v${NODE_VERSION}
ENV PATH="${NVM_DIR}/versions/node/v${NODE_VERSION}/bin/:${PATH}"
RUN node --version
RUN npm --version

# Copy csproj files as distinct layers
WORKDIR /source
COPY bitwarden_license/src/Commercial.Core/*.csproj ./bitwarden_license/src/Commercial.Core/
COPY bitwarden_license/src/Commercial.Infrastructure.EntityFramework/*.csproj ./bitwarden_license/src/Commercial.Infrastructure.EntityFramework/
COPY src/Admin/*.csproj ./src/Admin/
COPY src/Core/*.csproj ./src/Core/
COPY src/Infrastructure.Dapper/*.csproj ./src/Infrastructure.Dapper/
COPY src/Infrastructure.EntityFramework/*.csproj ./src/Infrastructure.EntityFramework/
COPY src/SharedWeb/*.csproj ./src/SharedWeb/
COPY util/Migrator/*.csproj ./util/Migrator/
COPY util/MySqlMigrations/*.csproj ./util/MySqlMigrations/
COPY util/PostgresMigrations/*.csproj ./util/PostgresMigrations/
COPY util/SqliteMigrations/*.csproj ./util/SqliteMigrations/
COPY Directory.Build.props .

# Restore Admin project dependencies and tools
WORKDIR /source/src/Admin
RUN . /tmp/rid.txt && dotnet restore -r $RID

# Copy required project files
WORKDIR /source
COPY src/Admin/. ./src/Admin/
COPY src/Core/. ./src/Core/
COPY src/Infrastructure.Dapper/. ./src/Infrastructure.Dapper/
COPY src/Infrastructure.EntityFramework/. ./src/Infrastructure.EntityFramework/
COPY src/SharedWeb/. ./src/SharedWeb/
COPY util/Migrator/. ./util/Migrator/
COPY util/MySqlMigrations/. ./util/MySqlMigrations/
COPY util/PostgresMigrations/. ./util/PostgresMigrations/
COPY util/SqliteMigrations/. ./util/SqliteMigrations/
COPY util/EfShared/. ./util/EfShared/
COPY bitwarden_license/src/Commercial.Core/. ./bitwarden_license/src/Commercial.Core/
COPY bitwarden_license/src/Commercial.Infrastructure.EntityFramework/. ./bitwarden_license/src/Commercial.Infrastructure.EntityFramework/
COPY .git/. ./.git/

# Build Admin app
WORKDIR /source/src/Admin
RUN npm install
RUN npm run build
RUN . /tmp/rid.txt && dotnet publish -c release -o /app/Admin --no-restore --no-self-contained -r $RID

WORKDIR /app
COPY src/Admin/entrypoint.sh ./Admin/

###################################################
#                App stage - Admin                #
###################################################
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS app-admin

ARG TARGETPLATFORM

ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV ASPNETCORE_URLS=http://+:5000

EXPOSE 5000

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
    gosu \
    curl \
    krb5-user \    
    && rm -rf /var/lib/apt/lists/*

LABEL com.bitwarden.product="bitwarden"
LABEL com.bitwarden.project="admin"

WORKDIR /app
COPY --from=build-dotnet /app/Admin ./
RUN chmod +x entrypoint.sh

HEALTHCHECK CMD curl -f http://localhost:5000 || exit 1

ENTRYPOINT ["/app/entrypoint.sh"]
