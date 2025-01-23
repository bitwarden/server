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

# Copy csproj files as distinct layers
WORKDIR /source
COPY src/Icons/*.csproj ./src/Icons/
COPY src/Core/*.csproj ./src/Core/
COPY src/Infrastructure.Dapper/*.csproj ./src/Infrastructure.Dapper/
COPY src/Infrastructure.EntityFramework/*.csproj ./src/Infrastructure.EntityFramework/
COPY src/SharedWeb/*.csproj ./src/SharedWeb/
COPY Directory.Build.props .

# Restore Icons project dependencies and tools
WORKDIR /source/src/Icons
RUN . /tmp/rid.txt && dotnet restore -r $RID

# Copy required project files
WORKDIR /source
COPY src/Icons/. ./src/Icons/
COPY src/Core/. ./src/Core/
COPY src/Infrastructure.Dapper/. ./src/Infrastructure.Dapper/
COPY src/Infrastructure.EntityFramework/. ./src/Infrastructure.EntityFramework/
COPY src/SharedWeb/. ./src/SharedWeb/
COPY .git/. ./.git/

# Build Icons app
WORKDIR /source/src/Icons
RUN . /tmp/rid.txt && dotnet publish -c release -o /app/Icons --no-restore --no-self-contained -r $RID

WORKDIR /app
COPY src/Icons/entrypoint.sh ./Icons/

###################################################
#               App stage - Icons                 #
###################################################
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS app-icons

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
LABEL com.bitwarden.project="icons"

WORKDIR /app
COPY --from=build-dotnet /app/Icons ./
RUN chmod +x entrypoint.sh

HEALTHCHECK CMD curl -f http://localhost:5000 || exit 1

ENTRYPOINT ["/app/entrypoint.sh"]
