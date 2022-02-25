###############################################
#                 Build stage                 #
###############################################
FROM mcr.microsoft.com/dotnet/sdk:5.0-alpine AS build

# Copy csproj files as distinct layers
WORKDIR /source
COPY src/Identity/*.csproj ./src/Identity/
COPY src/Core/*.csproj ./src/Core/
COPY src/Infrastructure.Dapper/*.csproj ./src/Infrastructure.Dapper/
COPY src/Infrastructure.EntityFramework/*.csproj ./src/Infrastructure.EntityFramework/
COPY src/SharedWeb/*.csproj ./src/SharedWeb/
COPY Directory.Build.props .

# Restore project dependencies and tools
WORKDIR /source/src/Identity
RUN dotnet restore
RUN dotnet clean

# Copy required project files
WORKDIR /source
COPY src/Identity/. ./src/Identity/
COPY src/Core/. ./src/Core/
COPY src/Infrastructure.Dapper/. ./src/Infrastructure.Dapper/
COPY src/Infrastructure.EntityFramework/. ./src/Infrastructure.EntityFramework/
COPY src/SharedWeb/. ./src/SharedWeb/

# Build app
WORKDIR /source/src/Identity
RUN dotnet publish -c release -o /app --no-restore

###############################################
#                  App stage                  #
###############################################
FROM mcr.microsoft.com/dotnet/aspnet:5.0-alpine
LABEL com.bitwarden.product="bitwarden"
ENV ASPNETCORE_URLS http://+:5000
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
EXPOSE 5000

# Add packages
RUN apk add --update-cache \
    curl \
    icu-libs \
    su-exec \
    && rm -rf /var/cache/apk/*

# Create required directories
RUN mkdir -p /etc/bitwarden/core
RUN mkdir -p /etc/bitwarden/logs
RUN mkdir -p /etc/bitwarden/ca-certificates

# Copy app from build stage
WORKDIR /app
COPY --from=build /app ./

# Copy entrypoint script and make it executable
COPY docker/identity/entrypoint.sh /
RUN chmod +x /entrypoint.sh

# Create non-root user to run app
RUN adduser -s /bin/false -D bitwarden && chown -R bitwarden:bitwarden /app /etc/bitwarden

USER bitwarden:bitwarden
HEALTHCHECK CMD curl -f http://localhost:5000/.well-known/openid-configuration || exit 1
ENTRYPOINT ["/entrypoint.sh"]
