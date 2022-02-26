###############################################
#                 Build stage                 #
###############################################
FROM mcr.microsoft.com/dotnet/sdk:5.0-alpine AS build

# Add packages
RUN apk add --update-cache \
    npm \
    && rm -rf /var/cache/apk/*

# Copy csproj files as distinct layers
WORKDIR /source
COPY util/Server/*.csproj ./src/Server/
COPY Directory.Build.props .

# Restore project dependencies and tools
WORKDIR /source/src/Server
RUN dotnet restore

# Copy required project files
WORKDIR /source
COPY util/Server/. ./src/Server/

# Build app
WORKDIR /source/src/Server
RUN dotnet publish -c release -o /app --no-restore

###############################################
#                  App stage                  #
###############################################
FROM mcr.microsoft.com/dotnet/aspnet:5.0-alpine
LABEL com.bitwarden.product="bitwarden"

RUN apk add --update-cache \
    curl \
    && rm -rf /var/cache/apk/*

COPY --from=build /app /bitwarden_server

# Create required directories
RUN mkdir -p /etc/bitwarden/core/attachments

# Create non-root user to run app
RUN adduser -s /bin/false -D bitwarden && chown -R bitwarden:bitwarden /bitwarden_server /etc/bitwarden
