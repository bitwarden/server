###############################################
#                 Build stage                 #
###############################################
FROM node:16-slim AS node-build

# TODO: Change default branch name before merge into master
ARG web_branch=beeep_bitwarden_lite

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates \
        g++ \
        git \
        make \
        python3 \
    && rm -rf /var/lib/apt/lists/*

RUN git clone --branch $web_branch https://github.com/bitwarden/clients.git /source

WORKDIR /source
RUN npm ci

WORKDIR /source/apps/web
RUN npm run dist:bit:selfhost

###############################################
#                 Build stage                 #
###############################################
FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine AS dotnet-build

# Add packages
RUN apk add --update-cache \
    npm \
    && rm -rf /var/cache/apk/*

# Copy csproj files as distinct layers
WORKDIR /source
COPY src/Admin/*.csproj ./src/Admin/
COPY src/Api/*.csproj ./src/Api/
COPY src/Attachments/*.csproj ./src/Attachments/
COPY src/Events/*.csproj ./src/Events/
COPY src/Icons/*.csproj ./src/Icons/
COPY src/Identity/*.csproj ./src/Identity/
COPY src/Notifications/*.csproj ./src/Notifications/
COPY bitwarden_license/src/Sso/*.csproj ./bitwarden_license/src/Sso/
COPY src/Core/*.csproj ./src/Core/
COPY src/Infrastructure.Dapper/*.csproj ./src/Infrastructure.Dapper/
COPY src/Infrastructure.EntityFramework/*.csproj ./src/Infrastructure.EntityFramework/
COPY src/SharedWeb/*.csproj ./src/SharedWeb/
COPY util/Migrator/*.csproj ./util/Migrator/
COPY util/MySqlMigrations/*.csproj ./util/MySqlMigrations/
COPY util/PostgresMigrations/*.csproj ./util/PostgresMigrations/
COPY bitwarden_license/src/Commercial.Core/*.csproj ./bitwarden_license/src/Commercial.Core/
COPY Directory.Build.props .

# Restore Admin project dependencies and tools
WORKDIR /source/src/Admin
RUN dotnet restore

# Restore Api project dependencies and tools
WORKDIR /source/src/Api
RUN dotnet restore

# Restore Attachments project dependencies and tools
WORKDIR /source/src/Attachments
RUN dotnet restore

# Restore Events project dependencies and tools
WORKDIR /source/src/Events
RUN dotnet restore

# Restore Icons project dependencies and tools
WORKDIR /source/src/Icons
RUN dotnet restore

# Restore Identity project dependencies and tools
WORKDIR /source/src/Identity
RUN dotnet restore

# Restore Notifications project dependencies and tools
WORKDIR /source/src/Notifications
RUN dotnet restore

# Restore Sso project dependencies and tools
WORKDIR /source/bitwarden_license/src/Sso
RUN dotnet restore

# Copy required project files
WORKDIR /source
COPY src/Admin/. ./src/Admin/
COPY src/Api/. ./src/Api/
COPY src/Attachments/. ./src/Attachments/
COPY src/Events/. ./src/Events/
COPY src/Icons/. ./src/Icons/
COPY src/Identity/. ./src/Identity/
COPY src/Notifications/. ./src/Notifications/
COPY bitwarden_license/src/Sso/. ./bitwarden_license/src/Sso/
COPY src/Core/. ./src/Core/
COPY src/Infrastructure.Dapper/. ./src/Infrastructure.Dapper/
COPY src/Infrastructure.EntityFramework/. ./src/Infrastructure.EntityFramework/
COPY src/SharedWeb/. ./src/SharedWeb/
COPY util/Migrator/. ./util/Migrator/
COPY util/MySqlMigrations/. ./util/MySqlMigrations/
COPY util/PostgresMigrations/. ./util/PostgresMigrations/
COPY util/EfShared/. ./util/EfShared/
COPY bitwarden_license/src/Commercial.Core/. ./bitwarden_license/src/Commercial.Core/
COPY .git/. ./.git/

# Copy required project files for Web
#COPY --from=node-build /source/apps/web/dotnet-src/Web/. /app/Web/

# Build Admin app
WORKDIR /source/src/Admin
RUN npm install -g gulp
RUN npm install
RUN gulp --gulpfile "gulpfile.js" build
RUN dotnet publish -c release -o /app/Admin --no-restore

# Build Api app
WORKDIR /source/src/Api
RUN dotnet publish -c release -o /app/Api --no-restore

# Build Attachments app
WORKDIR /source/src/Attachments
RUN dotnet publish -c release -o /app/Attachments --no-restore

# Build Events app
WORKDIR /source/src/Events
RUN dotnet publish -c release -o /app/Events --no-restore

# Build Icons app
WORKDIR /source/src/Icons
RUN dotnet publish -c release -o /app/Icons --no-restore

# Build Identity app
WORKDIR /source/src/Identity
RUN dotnet publish -c release -o /app/Identity --no-restore

# Build Notifications app
WORKDIR /source/src/Notifications
RUN dotnet publish -c release -o /app/Notifications --no-restore

# Build Sso app
WORKDIR /source/bitwarden_license/src/Sso
RUN npm install -g gulp
RUN npm install
RUN gulp --gulpfile "gulpfile.js" build
RUN dotnet publish -c release -o /app/Sso --no-restore

###############################################
#                  App stage                  #
###############################################
FROM mcr.microsoft.com/dotnet/aspnet:6.0-alpine
LABEL com.bitwarden.product="bitwarden"
LABEL com.bitwarden.project="lite"
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV BW_SERVICE_NAME_ADMIN="localhost"
ENV BW_SERVICE_NAME_API="localhost"
ENV BW_SERVICE_NAME_ATTACHMENTS="localhost"
ENV BW_SERVICE_NAME_EVENTS="localhost"
ENV BW_SERVICE_NAME_ICONS="localhost"
ENV BW_SERVICE_NAME_IDENTITY="localhost"
ENV BW_SERVICE_NAME_NOTIFICATIONS="localhost"
ENV BW_SERVICE_NAME_SSO="localhost"
ENV BW_SERVICE_PORT_ADMIN="5000"
ENV BW_SERVICE_PORT_API="5001"
ENV BW_SERVICE_PORT_ATTACHMENTS="5002"
ENV BW_SERVICE_PORT_EVENTS="5003"
ENV BW_SERVICE_PORT_ICONS="5004"
ENV BW_SERVICE_PORT_IDENTITY="5005"
ENV BW_SERVICE_PORT_NOTIFICATIONS="5006"
ENV BW_SERVICE_PORT_SSO="5007"
ENV globalSettings__selfHosted="true"
ENV globalSettings__baseServiceUri__internalAdmin="http://localhost:5000"
ENV globalSettings__baseServiceUri__internalApi="http://localhost:5001"
ENV globalSettings__baseServiceUri__internalAttachments="http://localhost:5002"
ENV globalSettings__baseServiceUri__internalEvents="http://localhost:5003"
ENV globalSettings__baseServiceUri__internalIcons="http://localhost:5004"
ENV globalSettings__baseServiceUri__internalIdentity="http://localhost:5005"
ENV globalSettings__baseServiceUri__internalNotifications="http://localhost:5006"
ENV globalSettings__baseServiceUri__internalSso="http://localhost:5007"
EXPOSE 8080
EXPOSE 8443

# Add packages
RUN apk add --update-cache \
    curl \
    icu-libs \
    nginx \
    openssl \
    su-exec \
    supervisor \
    tzdata \
    && rm -rf /var/cache/apk/*

# Create required directories
RUN mkdir -p /etc/bitwarden/core/attachments
RUN mkdir -p /etc/bitwarden/core/aspnet-dataprotection
RUN mkdir -p /etc/bitwarden/identity
RUN mkdir -p /etc/bitwarden/logs
RUN mkdir -p /etc/bitwarden/nginx
RUN mkdir -p /etc/bitwarden/ssl
RUN mkdir -p /etc/bitwarden/web

# Copy all apps from dotnet-build stage
WORKDIR /app
COPY --from=dotnet-build /app ./

# Copy Web files from node-build stage
COPY --from=node-build /source/apps/web/build /app/Web

# Set up supervisord
RUN mkdir -p /etc/supervisor
RUN mkdir -p /var/log/bitwarden
COPY docker-lite/supervisord.ini /etc/supervisor.d/bitwarden-lite.ini
COPY docker-lite/supervisord.conf /etc/supervisor/supervisord.conf
RUN rm -f /etc/supervisord.conf

# Set up nginx
RUN mkdir -p /var/log/nginx/logs
COPY docker-lite/nginx/confd/nginx-config.toml /etc/confd/conf.d/
COPY docker-lite/nginx/confd/nginx-config.conf.tmpl /etc/confd/templates/
COPY docker-lite/nginx/nginx.conf /etc/nginx
COPY docker-lite/nginx/proxy.conf /etc/nginx
COPY docker-lite/nginx/mime.types /etc/nginx
COPY docker-lite/nginx/security-headers.conf /etc/nginx
COPY docker-lite/nginx/security-headers-ssl.conf /etc/nginx
COPY docker-lite/nginx/logrotate.sh /
RUN chmod +x /logrotate.sh

# Copy app-id configuration template
COPY --from=node-build /source/apps/web/docker/confd/app-id.toml /etc/confd/conf.d/
COPY --from=node-build /source/apps/web/docker/confd/app-id.conf.tmpl /etc/confd/templates/

# Add confd tool for generating final configurations
ADD https://github.com/kelseyhightower/confd/releases/download/v0.16.0/confd-0.16.0-linux-amd64 /usr/local/bin/confd
RUN chmod +x /usr/local/bin/confd

# Copy entrypoint script and make it executable
COPY docker-lite/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

# TODO: Remove after testing
RUN apk add --update-cache \
    vim \
    && rm -rf /var/cache/apk/*

# Create non-root user to run app
RUN adduser -s /bin/false -D bitwarden && chown -R bitwarden:bitwarden \
    /app \
    /etc/bitwarden \
    /etc/supervisor \
    /var/lib/nginx \
    /var/log \
    /run

VOLUME ["/etc/bitwarden"]

WORKDIR /app
USER bitwarden:bitwarden
HEALTHCHECK CMD curl --insecure -Lfs https://localhost:8443/alive || curl -Lfs http://localhost:8080/alive || exit 1
ENTRYPOINT ["/entrypoint.sh"]
