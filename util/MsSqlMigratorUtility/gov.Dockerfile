###############################################
#                 Build stage                 #
###############################################
# `-dev` is required: the shell-form RUN steps (RID detection, sourcing
# /tmp/rid.txt, dotnet restore/publish) need a shell + root, which the minimal
# `dotnet-sdk:latest` lacks.
FROM --platform=$BUILDPLATFORM cgr.dev/bitwarden.com/dotnet-sdk-fips:10-dev AS build

USER root

# Docker buildx supplies the value for this arg
ARG TARGETPLATFORM

# Chainguard (Wolfi) is glibc-based, so target the glibc RIDs (linux-x64), NOT
# the musl RIDs the Alpine image used — a musl self-contained binary will not
# run on the glibc runtime base below.
RUN if [ "$TARGETPLATFORM" = "linux/amd64" ]; then \
    RID=linux-x64 ; \
    elif [ "$TARGETPLATFORM" = "linux/arm64" ]; then \
    RID=linux-arm64 ; \
    else echo "Unsupported TARGETPLATFORM: $TARGETPLATFORM" >&2 && exit 1 ; \
    fi \
    && echo "RID=$RID" > /tmp/rid.txt

# Copy required project files
WORKDIR /source
COPY . ./

# Restore project dependencies and tools
WORKDIR /source/util/MsSqlMigratorUtility
RUN . /tmp/rid.txt && dotnet restore -r $RID

# Build project
WORKDIR /source/util/MsSqlMigratorUtility
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
# This is a console app (not an ASP.NET service), so the leaner
# `dotnet-runtime` is the correct family rather than `aspnet-runtime` — there
# are no ports, no ASPNETCORE_* config, and no HTTP healthcheck. `-dev` is
# required because the ENTRYPOINT runs `sh -c`, which the minimal distroless
# `dotnet-runtime:latest` (nonroot, no shell) cannot execute.
FROM cgr.dev/bitwarden.com/dotnet-runtime-fips:10-dev AS app

USER root

ARG TARGETPLATFORM
LABEL com.bitwarden.product="bitwarden"

ENV SSL_CERT_DIR=/etc/bitwarden/ca-certificates
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# Copy app from the build stage
WORKDIR /app
COPY --from=build /source/util/MsSqlMigratorUtility/out /app

# Wolfi packages (apk), not Alpine: `icu-libs` -> `icu`. No curl/shadow/gosu
# here — this image has no entrypoint.sh, no gosu step-down, and no healthcheck.
RUN apk add --no-cache icu

ENTRYPOINT ["sh", "-c", "/app/MsSqlMigratorUtility \"${MSSQL_CONN_STRING}\" ${@}", "--" ]
