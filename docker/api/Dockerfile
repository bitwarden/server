###############################################
#                 Build stage                 #
###############################################
FROM mcr.microsoft.com/dotnet/sdk:5.0-alpine AS build

# Copy csproj files as distinct layers
WORKDIR /source
COPY src/Api/*.csproj ./src/Api/
COPY src/Core/*.csproj ./src/Core/
COPY src/Infrastructure.Dapper/*.csproj ./src/Infrastructure.Dapper/
COPY src/Infrastructure.EntityFramework/*.csproj ./src/Infrastructure.EntityFramework/
COPY src/SharedWeb/*.csproj ./src/SharedWeb/
COPY bitwarden_license/src/CommCore/*.csproj ./bitwarden_license/src/CommCore/
COPY Directory.Build.props .

# Restore project dependencies and tools
WORKDIR /source/src/Api
RUN dotnet restore

# Copy required project files
WORKDIR /source
COPY src/Api/. ./src/Api/
COPY src/Core/. ./src/Core/
COPY src/Infrastructure.Dapper/. ./src/Infrastructure.Dapper/
COPY src/Infrastructure.EntityFramework/. ./src/Infrastructure.EntityFramework/
COPY src/SharedWeb/. ./src/SharedWeb/
COPY bitwarden_license/src/CommCore/. ./bitwarden_license/src/CommCore/

# Build app
WORKDIR /source/src/Api
RUN dotnet publish -c release -o /app --no-restore

###############################################
#                  App stage                  #
###############################################
FROM mcr.microsoft.com/dotnet/aspnet:5.0-alpine
LABEL com.bitwarden.product="bitwarden"
LABEL com.bitwarden.project="api"
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS http://+:5000
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
EXPOSE 5000

# Add packages
RUN apk add --update-cache \
    curl \
    icu-libs \
    && rm -rf /var/cache/apk/*

# Create required directories
RUN mkdir -p /etc/bitwarden/core/aspnet-dataprotection
RUN mkdir -p /etc/bitwarden/logs
RUN mkdir -p /etc/bitwarden/ca-certificates

# Copy app from build stage
WORKDIR /app
COPY --from=build /app ./

# Copy entrypoint script and make it executable
COPY docker/api/entrypoint.sh /
RUN chmod +x /entrypoint.sh

# Create non-root user to run app
RUN adduser -s /bin/false -D bitwarden && chown -R bitwarden:bitwarden /app /etc/bitwarden

USER bitwarden:bitwarden
HEALTHCHECK CMD curl -f http://localhost:5000/alive || exit 1
ENTRYPOINT ["/entrypoint.sh"]
