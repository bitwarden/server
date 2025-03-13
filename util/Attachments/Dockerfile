###############################################
#                 Build stage                 #
###############################################
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ENV PROJECT_NAME=Attachments
ARG GIT_COMMIT

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
COPY util/Server/*.csproj ./util/Server/
COPY Directory.Build.props .

# Restore project dependencies and tools
WORKDIR /source/util/Server
RUN . /tmp/rid.txt && dotnet restore -r $RID

# Copy required project files
WORKDIR /source
COPY util/Server/. ./util/Server/
COPY .git/. ./.git/
COPY .editorconfig /source

# Build project
WORKDIR /source/util/Server
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
ENV PROJECT_NAME=Attachments

RUN apt-get update \
  && apt-get install -y --no-install-recommends \
  gosu \
  curl \
  && rm -rf /var/lib/apt/lists/*

# Copy app from the build stage
WORKDIR /bitwarden_server
COPY --from=build /source/util/Server/out /bitwarden_server
COPY util/${PROJECT_NAME}/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

HEALTHCHECK CMD curl -f http://localhost:5000/alive || exit 1

ENTRYPOINT ["/entrypoint.sh"]
