FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG TARGETPLATFORM
ARG BUILDPLATFORM
ARG PROJECT_NAME=Server

WORKDIR /build
COPY ../../ ./

WORKDIR /build/util/${PROJECT_NAME}

RUN <<EOF
  case "$TARGETPLATFORM" in
    *"linux/amd64"*)
      dotnet publish "./Server.csproj" -c "Release" --self-contained /p:PublishSingleFile=true -r linux-x64 -o out # || \
      # ls -hal && exit 1
      ;;
    *"linux/arm64"*)
      dotnet publish "./Server.csproj" -c "Release" --self-contained /p:PublishSingleFile=true -r linux-arm64 -o out # || \
      # ls -hal && exit 1
      ;;
    *)
      echo "unsupported target platform: $TARGETPLATFORM"
      exit 1
      ;;
  esac
EOF

FROM mcr.microsoft.com/dotnet/aspnet:8.0
RUN true
LABEL com.bitwarden.product="bitwarden"
ARG TARGETPLATFORM
ARG BUILDPLATFORM
ARG PROJECT_NAME=Server

# RUN apt-get update \
#     && apt-get install -y --no-install-recommends \
#     gosu \
#     curl \
#     krb5-user \
#     && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:5000

# file will be in: /build/util/Server/bin/Release/net8.0/linux-arm64/Server.dll
COPY --from=build /build/util/${PROJECT_NAME}/out/ /bitwarden_server

RUN mkdir -p {/app,/bitwarden_server,/config,/storage} \
  && chown -R app:app {/app,/bitwarden_server,/config,/storage}
