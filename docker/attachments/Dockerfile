###############################################
#                 Build stage                 #
###############################################
FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine AS build

# Add packages
RUN apk add --update-cache \
    npm \
    && rm -rf /var/cache/apk/*

# Copy csproj files as distinct layers
WORKDIR /source
COPY src/Attachments/*.csproj ./src/Attachments/
COPY Directory.Build.props .

# Restore project dependencies and tools
WORKDIR /source/src/Attachments
RUN dotnet restore

# Copy required project files
WORKDIR /source
COPY src/Attachments/. ./src/Attachments/
COPY .git/. ./.git/

# Build app
WORKDIR /source/src/Attachments
RUN dotnet publish -c release -o /app --no-restore

###############################################
#                  App stage                  #
###############################################
FROM mcr.microsoft.com/dotnet/aspnet:6.0-alpine
LABEL com.bitwarden.product="bitwarden"
LABEL com.bitwarden.project="attachments"
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS http://+:5000
EXPOSE 5000

# Add packages
RUN apk add --update-cache \
    curl \
    && rm -rf /var/cache/apk/*

# Create required directories
RUN mkdir -p /etc/bitwarden/core/attachments

# Copy app from build stage
WORKDIR /app
COPY --from=build /app ./

# Create non-root user to run app
RUN adduser -s /bin/false -D bitwarden && chown -R bitwarden:bitwarden /app /etc/bitwarden

USER bitwarden:bitwarden
HEALTHCHECK CMD curl -f http://localhost:5000/alive || exit 1
ENTRYPOINT ["dotnet", "/app/Attachments.dll", "/contentRoot=/etc/bitwarden/core/attachments", "/webRoot=."]
