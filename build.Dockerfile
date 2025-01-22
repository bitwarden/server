# syntax = docker/dockerfile:1.11
###############################################
#                 Build stage                 #
###############################################
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS dotnet-build

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
COPY src/Admin/*.csproj ./src/Admin/
COPY src/Api/*.csproj ./src/Api/
COPY src/Billing/*.csproj ./src/Billing/
COPY src/Events/*.csproj ./src/Events/
COPY src/EventsProcessor/*.csproj ./src/EventsProcessor/
COPY src/Icons/*.csproj ./src/Icons/
COPY src/Identity/*.csproj ./src/Identity/
COPY src/Notifications/*.csproj ./src/Notifications/
COPY bitwarden_license/src/Sso/*.csproj ./bitwarden_license/src/Sso/
COPY bitwarden_license/src/Scim/*.csproj ./bitwarden_license/src/Scim/
COPY src/Core/*.csproj ./src/Core/
COPY src/Infrastructure.Dapper/*.csproj ./src/Infrastructure.Dapper/
COPY src/Infrastructure.EntityFramework/*.csproj ./src/Infrastructure.EntityFramework/
COPY src/SharedWeb/*.csproj ./src/SharedWeb/
COPY util/Migrator/*.csproj ./util/Migrator/
COPY util/MySqlMigrations/*.csproj ./util/MySqlMigrations/
COPY util/PostgresMigrations/*.csproj ./util/PostgresMigrations/
COPY util/SqliteMigrations/*.csproj ./util/SqliteMigrations/
COPY bitwarden_license/src/Commercial.Core/*.csproj ./bitwarden_license/src/Commercial.Core/
COPY bitwarden_license/src/Commercial.Infrastructure.EntityFramework/*.csproj ./bitwarden_license/src/Commercial.Infrastructure.EntityFramework/
COPY Directory.Build.props .

# Restore Admin project dependencies and tools
WORKDIR /source/src/Admin
RUN . /tmp/rid.txt && dotnet restore -r $RID

# Restore Api project dependencies and tools
WORKDIR /source/src/Api
RUN . /tmp/rid.txt && dotnet restore -r $RID

# Restore Billing project dependencies and tools
WORKDIR /source/src/Billing
RUN . /tmp/rid.txt && dotnet restore -r $RID

# Restore Events project dependencies and tools
WORKDIR /source/src/Events
RUN . /tmp/rid.txt && dotnet restore -r $RID

# Restore EventsProcessor project dependencies and tools
WORKDIR /source/src/EventsProcessor
RUN . /tmp/rid.txt && dotnet restore -r $RID

# Restore Icons project dependencies and tools
WORKDIR /source/src/Icons
RUN . /tmp/rid.txt && dotnet restore -r $RID

# Restore Identity project dependencies and tools
WORKDIR /source/src/Identity
RUN . /tmp/rid.txt && dotnet restore -r $RID

# Restore Notifications project dependencies and tools
WORKDIR /source/src/Notifications
RUN . /tmp/rid.txt && dotnet restore -r $RID

# Restore Sso project dependencies and tools
WORKDIR /source/bitwarden_license/src/Sso
RUN . /tmp/rid.txt && dotnet restore -r $RID

# Restore Scim project dependencies and tools
WORKDIR /source/bitwarden_license/src/Scim
RUN . /tmp/rid.txt && dotnet restore -r $RID

# Copy required project files
WORKDIR /source
COPY src/Admin/. ./src/Admin/
COPY src/Api/. ./src/Api/
COPY src/Billing/. ./src/Billing/
COPY src/Events/. ./src/Events/
COPY src/EventsProcessor/. ./src/EventsProcessor/
COPY src/Icons/. ./src/Icons/
COPY src/Identity/. ./src/Identity/
COPY src/Notifications/. ./src/Notifications/
COPY bitwarden_license/src/Sso/. ./bitwarden_license/src/Sso/
COPY bitwarden_license/src/Scim/. ./bitwarden_license/src/Scim/
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

# Build Api app
WORKDIR /source/src/Api
RUN . /tmp/rid.txt && dotnet publish -c release -o /app/Api --no-restore --no-self-contained -r $RID

# Build Billing app
WORKDIR /source/src/Billing
RUN . /tmp/rid.txt && dotnet publish -c release -o /app/Billing --no-restore --no-self-contained -r $RID

# Build Events app
WORKDIR /source/src/Events
RUN . /tmp/rid.txt && dotnet publish -c release -o /app/Events --no-restore --no-self-contained -r $RID

# Build EventsProcessor app
WORKDIR /source/src/EventsProcessor
RUN . /tmp/rid.txt && dotnet publish -c release -o /app/EventsProcessor --no-restore --no-self-contained -r $RID

# Build Icons app
WORKDIR /source/src/Icons
RUN . /tmp/rid.txt && dotnet publish -c release -o /app/Icons --no-restore --no-self-contained -r $RID

# Build Identity app
WORKDIR /source/src/Identity
RUN . /tmp/rid.txt && dotnet publish -c release -o /app/Identity --no-restore --no-self-contained -r $RID

# Build Notifications app
WORKDIR /source/src/Notifications
RUN . /tmp/rid.txt && dotnet publish -c release -o /app/Notifications --no-restore --no-self-contained -r $RID

# Build Sso app
WORKDIR /source/bitwarden_license/src/Sso
RUN npm install
RUN npm run build
RUN . /tmp/rid.txt && dotnet publish -c release -o /app/Sso --no-restore --no-self-contained -r $RID

# Build Scim app
WORKDIR /source/bitwarden_license/src/Scim
RUN . /tmp/rid.txt && dotnet publish -c release -o /app/Scim --no-restore --no-self-contained -r $RID

WORKDIR /app

COPY src/Admin/entrypoint.sh ./Admin/
COPY src/Api/entrypoint.sh ./Api/
COPY src/Billing/entrypoint.sh ./Billing/
COPY src/Events/entrypoint.sh ./Events/
COPY src/EventsProcessor/entrypoint.sh ./EventsProcessor/
COPY src/Icons/entrypoint.sh ./Icons/
COPY src/Identity/entrypoint.sh ./Identity/
COPY src/Notifications/entrypoint.sh ./Notifications/
COPY bitwarden_license/src/Sso/entrypoint.sh ./Sso/
COPY bitwarden_license/src/Scim/entrypoint.sh ./Scim/

###################################################
#              App stage - Base Image             #
###################################################
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS app-base

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

WORKDIR /app

HEALTHCHECK CMD curl -f http://localhost:5000 || exit 1

ENTRYPOINT ["/app/entrypoint.sh"]

###################################################
#                App stage - Admin                #
###################################################
FROM app-base AS app-admin

LABEL com.bitwarden.project="admin"

COPY --from=dotnet-build /app/Admin ./
RUN chmod +x entrypoint.sh

###################################################
#                 App stage - Api                 #
###################################################
FROM app-base AS app-api

LABEL com.bitwarden.project="api"

COPY --from=dotnet-build /app/Api ./
RUN chmod +x entrypoint.sh

###################################################
#               App stage - Billing               #
###################################################
FROM app-base AS app-billing

LABEL com.bitwarden.project="billing"

COPY --from=dotnet-build /app/Billing ./
RUN chmod +x entrypoint.sh

###################################################
#               App stage - Events                #
###################################################
FROM app-base AS app-events

LABEL com.bitwarden.project="events"

COPY --from=dotnet-build /app/Events ./
RUN chmod +x entrypoint.sh

###################################################
#           App stage - EventsProcessor           #
###################################################
FROM app-base AS app-eventsprocessor

LABEL com.bitwarden.project="eventsprocessor"

COPY --from=dotnet-build /app/EventsProcessor ./
RUN chmod +x entrypoint.sh

###################################################
#               App stage - Icons                 #
###################################################
FROM app-base AS app-icons

LABEL com.bitwarden.project="icons"

COPY --from=dotnet-build /app/Icons ./
RUN chmod +x entrypoint.sh

###################################################
#              App stage - Identity               #
###################################################
FROM app-base AS app-identity

LABEL com.bitwarden.project="identity"

COPY --from=dotnet-build /app/Identity ./
RUN chmod +x entrypoint.sh

###################################################
#            App stage - Notifications            #
###################################################
FROM app-base AS app-notifications

LABEL com.bitwarden.project="notifications"

COPY --from=dotnet-build /app/Notifications ./
RUN chmod +x entrypoint.sh

###################################################
#                 App stage - Sso                 #
###################################################
FROM app-base AS app-sso

LABEL com.bitwarden.project="sso"

COPY --from=dotnet-build /app/Sso ./
RUN chmod +x entrypoint.sh

###################################################
#                App stage - Scim                 #
###################################################
FROM app-base AS app-scim

LABEL com.bitwarden.project="scim"

COPY --from=dotnet-build /app/Scim ./
RUN chmod +x entrypoint.sh
