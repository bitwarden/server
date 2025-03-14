###############################################
#                 Build stage                 #
###############################################
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ENV PROJECT_NAME=Icons
ARG GIT_COMMIT

# Docker buildx supplies the value for this arg
ARG TARGETPLATFORM

# Determine proper runtime value for .NET
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
COPY src/${PROJECT_NAME}/*.csproj ./src/${PROJECT_NAME}/
COPY src/Core/*.csproj ./src/Core/
COPY src/Infrastructure.Dapper/*.csproj ./src/Infrastructure.Dapper/
COPY src/Infrastructure.EntityFramework/*.csproj ./src/Infrastructure.EntityFramework/
COPY src/SharedWeb/*.csproj ./src/SharedWeb/
COPY Directory.Build.props .

# Restore project dependencies and tools
WORKDIR /source/src/${PROJECT_NAME}
RUN . /tmp/rid.txt && dotnet restore -r $RID

# Copy required project files
WORKDIR /source
COPY src/${PROJECT_NAME}/. ./src/${PROJECT_NAME}/
COPY src/Core/. ./src/Core/
COPY src/Infrastructure.Dapper/. ./src/Infrastructure.Dapper/
COPY src/Infrastructure.EntityFramework/. ./src/Infrastructure.EntityFramework/
COPY src/SharedWeb/. ./src/SharedWeb/
COPY .git/. ./.git/
COPY .editorconfig /source

# Build project
WORKDIR /source/src/${PROJECT_NAME}
RUN . /tmp/rid.txt && dotnet publish \
    --self-contained \
    /p:PublishSingleFile=true \
    /p:SourceRevisionId="$GIT_COMMIT" \
    -r $RID \
    -o out

WORKDIR /app

###############################################
#                  App stage                  #
###############################################
FROM mcr.microsoft.com/dotnet/aspnet:8.0
LABEL com.bitwarden.product="bitwarden"
EXPOSE 5000

ENV ASPNETCORE_URLS=http://+:5000
ENV PROJECT_NAME=Icons

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
    gosu \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Copy app from the build stage
WORKDIR /app
COPY --from=build /source/src/${PROJECT_NAME}/out /app
COPY ./src/${PROJECT_NAME}/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh
HEALTHCHECK CMD curl -f http://localhost:5000/google.com/icon.png || exit 1

ENTRYPOINT ["/entrypoint.sh"]
