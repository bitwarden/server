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
COPY bitwarden_license/src/Sso/*.csproj ./bitwarden_license/src/Sso/
COPY src/Core/*.csproj ./src/Core/
COPY src/Infrastructure.Dapper/*.csproj ./src/Infrastructure.Dapper/
COPY src/Infrastructure.EntityFramework/*.csproj ./src/Infrastructure.EntityFramework/
COPY src/SharedWeb/*.csproj ./src/SharedWeb/
COPY Directory.Build.props .

# Restore project dependencies and tools
WORKDIR /source/bitwarden_license/src/Sso
RUN dotnet restore

# Copy required project files
WORKDIR /source
COPY bitwarden_license/src/Sso/. ./bitwarden_license/src/Sso/
COPY src/Core/. ./src/Core/
COPY src/Infrastructure.Dapper/. ./src/Infrastructure.Dapper/
COPY src/Infrastructure.EntityFramework/. ./src/Infrastructure.EntityFramework/
COPY src/SharedWeb/. ./src/SharedWeb/

# Build app
WORKDIR /source/bitwarden_license/src/Sso
RUN npm install -g gulp
RUN npm install
RUN gulp --gulpfile "gulpfile.js" build
RUN dotnet publish -c release -o /app --no-restore

###############################################
#                  App stage                  #
###############################################
FROM mcr.microsoft.com/dotnet/aspnet:5.0-alpine
LABEL com.bitwarden.product="bitwarden"
LABEL com.bitwarden.project="sso"
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS http://+:5000
EXPOSE 5000

# Add packages
RUN apk add --update-cache \
    curl \
    && rm -rf /var/cache/apk/*

# Create required directories
RUN mkdir -p /etc/bitwarden/core
RUN mkdir -p /etc/bitwarden/logs

# Copy app from build stage
WORKDIR /app
COPY --from=build /app ./

# Copy entrypoint script and make it executable
COPY docker/sso/entrypoint.sh /
RUN chmod +x /entrypoint.sh

# Create non-root user to run app
RUN adduser -s /bin/false -D bitwarden && chown -R bitwarden:bitwarden /app /etc/bitwarden

USER bitwarden:bitwarden
HEALTHCHECK CMD curl -f http://localhost:5000/alive || exit 1
ENTRYPOINT ["/entrypoint.sh"]
