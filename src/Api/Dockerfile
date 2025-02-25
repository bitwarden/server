FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG TARGETPLATFORM
ARG BUILDPLATFORM
ARG PROJECT_NAME=Api

WORKDIR /build
COPY ../../ ./

WORKDIR /build/src/${PROJECT_NAME}

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
# TODO: move this to a base image
LABEL com.bitwarden.product="bitwarden"

ENV APP_UID=1654
ENV ASPNETCORE_HTTP_PORTS=8080
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV PROJECT_NAME=Api

RUN groupadd \
  --gid=$APP_UID \
  app \
  && useradd -l \
  --uid=$APP_UID \
  --gid=$APP_UID \
  --create-home \
  app

RUN mkdir -p {/admin,/api,/identity,/events,/notifications} \
  && chown -R app:app {/admin,/api,/identity,/events,/notifications}

RUN mkdir -p {/config} \
  && chown -R app:app {/config}

# RUN apt-get update \
#     && apt-get install -y --no-install-recommends \
#     gosu \
#     curl \
#     krb5-user \
#     && rm -rf /var/lib/apt/lists/*

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

ENV HOME=/home/app
ENV ASPNETCORE_URLS http://+:5000
# END: move to base image

EXPOSE 5000
WORKDIR /app
COPY --from=build /build/src/${PROJECT_NAME}/out /app
HEALTHCHECK CMD curl -f http://localhost:5000/alive || exit 1

# TODO: use an entrypoint script with `set -e && exec ${PROJECT_NAME}`
USER app
ENTRYPOINT ["./Api"]
