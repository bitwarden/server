FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG TARGETPLATFORM
ARG BUILDPLATFORM

WORKDIR /build
COPY ../../ ./

WORKDIR /build/src/Api

RUN <<EOF
  case "$TARGETPLATFORM" in
    *"linux/amd64"*)
      dotnet publish --self-contained /p:PublishSingleFile=true -r linux-x64 -o out
      ;;
    *"linux/arm64"*)
      dotnet publish --self-contained /p:PublishSingleFile=true -r linux-arm64 -o out
      ;;
    *)
      echo "unsupported target platform: $TARGETPLATFORM"
      exit 1;;
  esac
EOF


FROM ghcr.io/linuxserver/baseimage-ubuntu:noble

LABEL com.bitwarden.product="bitwarden"

# RUN apt-get update \
#     && apt-get install -y --no-install-recommends \
#     gosu \
#     curl \
#     krb5-user \
#     && rm -rf /var/lib/apt/lists/*

ENV APP_UID=1654
ENV ASPNETCORE_HTTP_PORTS=8080
ENV DOTNET_RUNNING_IN_CONTAINER=true

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates \
        \
        # .NET dependencies
        libc6 \
        libgcc-s1 \
        # libicu70 \
        libicu74 \
        libssl3 \
        libstdc++6 \
        tzdata \
        zlib1g \
    && rm -rf /var/lib/apt/lists/*

# Create a non-root user and group
RUN groupadd \
        --gid=$APP_UID \
        app \
    && useradd -l \
        --uid=$APP_UID \
        --gid=$APP_UID \
        --create-home \
        app

EXPOSE 5000

USER app
ENV HOME=/home/app
ENV ASPNETCORE_URLS http://+:5000
WORKDIR /app
COPY --from=build /build/src/Api/out /app
HEALTHCHECK CMD curl -f http://localhost:5000/alive || exit 1
ENTRYPOINT ["./Api"]
