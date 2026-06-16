###############################################
#                 Build stage                 #
###############################################
# `-dev` is required: the shell-form RUN steps (RID detection, sourcing
# /tmp/rid.txt, dotnet restore/publish) need a shell + root, which the minimal
# `dotnet-sdk:latest` lacks.
FROM --platform=$BUILDPLATFORM cgr.dev/chainguard/dotnet-sdk:latest-dev AS build

USER root

# Docker buildx supplies the value for this arg
ARG TARGETPLATFORM

# Chainguard (Wolfi) is glibc-based, so target the glibc RIDs (linux-x64), NOT
# the musl RIDs the Alpine image used — a musl self-contained binary will not
# run on the glibc aspnet-runtime base below.
RUN if [ "$TARGETPLATFORM" = "linux/amd64" ]; then \
    RID=linux-x64 ; \
    elif [ "$TARGETPLATFORM" = "linux/arm64" ]; then \
    RID=linux-arm64 ; \
    elif [ "$TARGETPLATFORM" = "linux/arm/v7" ]; then \
    RID=linux-arm ; \
    fi \
    && echo "RID=$RID" > /tmp/rid.txt

# Copy required project files
WORKDIR /source
COPY . ./

# Restore project dependencies and tools
WORKDIR /source/src/Icons
RUN . /tmp/rid.txt && dotnet restore -r $RID

# Build project
RUN . /tmp/rid.txt && dotnet publish \
    -c release \
    --no-restore \
    --self-contained \
    /p:PublishSingleFile=true \
    -r $RID \
    -o out

###############################################
#                  App stage                  #
###############################################
# `-dev` runtime: entrypoint.sh runs as root and needs /bin/sh, the shadow
# utilities, and gosu. The minimal `aspnet-runtime:latest` (distroless, nonroot,
# no shell) cannot run it. See src/Api/chainguard.Dockerfile for the hardening
# trade-off and the longer-term nonroot path.
FROM cgr.dev/chainguard/aspnet-runtime:latest-dev

USER root

ARG TARGETPLATFORM
LABEL com.bitwarden.product="bitwarden"
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5000
ENV SSL_CERT_DIR=/etc/bitwarden/ca-certificates
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
EXPOSE 5000

# Wolfi packages (apk), not Alpine: `icu-libs` -> `icu`; the Alpine edge repo
# used for gosu is dropped (incompatible with Wolfi apk) and gosu comes from
# Wolfi's default repos.
RUN apk add --no-cache curl \
    krb5 \
    icu \
    shadow \
    gosu

# Copy app from the build stage
WORKDIR /app
COPY --from=build /source/src/Icons/out /app
COPY ./src/Icons/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh
HEALTHCHECK CMD curl -f http://localhost:5000/google.com/icon.png || exit 1

ENTRYPOINT ["/entrypoint.sh"]
