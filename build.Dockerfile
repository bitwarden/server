###############################################
#                 Build stage                 #
###############################################
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:6.0

# Docker buildx supplies the value for this arg
ARG TARGETPLATFORM
ENV NODE_VERSION=16.20.2
ENV NVM_DIR /usr/local/nvm

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
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Set up Node
RUN mkdir -p $NVM_DIR
RUN curl -o- https://raw.githubusercontent.com/nvm-sh/nvm/v0.39.5/install.sh | bash \
    && . $NVM_DIR/nvm.sh \
    && nvm install $NODE_VERSION \
    && nvm alias default $NODE_VERSION \
    && nvm use default
ENV NODE_PATH $NVM_DIR/versions/node/v$NODE_VERSION/lib/node_modules
ENV PATH      $NVM_DIR/versions/node/v$NODE_VERSION/bin:$PATH

# Install gulp
RUN npm install -g gulp

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
COPY util/Server/*.csproj ./util/Server/
COPY util/Setup/*.csproj ./util/Setup/
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

# Restore Events project dependencies and tools
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

# Restore Server project dependencies and tools
WORKDIR /source/util/Server
RUN . /tmp/rid.txt && dotnet restore -r $RID

# Restore Setup project dependencies and tools
WORKDIR /source/util/Setup
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
COPY util/Server/. ./util/Server/
COPY util/Setup/. ./util/Setup/
COPY util/SqliteMigrations/. ./util/SqliteMigrations/
COPY util/EfShared/. ./util/EfShared/
COPY bitwarden_license/src/Commercial.Core/. ./bitwarden_license/src/Commercial.Core/
COPY bitwarden_license/src/Commercial.Infrastructure.EntityFramework/. ./bitwarden_license/src/Commercial.Infrastructure.EntityFramework/
COPY .git/. ./.git/

# Build Admin app
WORKDIR /source/src/Admin
RUN npm install
RUN gulp --gulpfile "gulpfile.js" build
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
RUN gulp --gulpfile "gulpfile.js" build
RUN . /tmp/rid.txt && dotnet publish -c release -o /app/Sso --no-restore --no-self-contained -r $RID

# Build Scim app
WORKDIR /source/bitwarden_license/src/Scim
RUN . /tmp/rid.txt && dotnet publish -c release -o /app/Scim --no-restore --no-self-contained -r $RID

# Build Server app
WORKDIR /source/util/Server
RUN . /tmp/rid.txt && dotnet publish -c release -o /app/Server --no-restore --no-self-contained -r $RID

# Build Setup app
WORKDIR /source/util/Setup
RUN . /tmp/rid.txt && dotnet publish -c release -o /app/Setup --no-restore --no-self-contained -r $RID
