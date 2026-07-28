###############################################
#                 Build stage                 #
###############################################
FROM --platform=$BUILDPLATFORM cgr.dev/bitwarden.com/dotnet-sdk-fips:10-dev AS build

USER root

# Docker buildx supplies the value for this arg
ARG TARGETPLATFORM

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
RUN dotnet restore

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
FROM cgr.dev/bitwarden.com/dotnet-runtime-fips:10-dev AS app

USER root

ARG TARGETPLATFORM
LABEL com.bitwarden.product="bitwarden"

ENV SSL_CERT_DIR=/etc/bitwarden/ca-certificates
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# Copy app from the build stage
WORKDIR /app
COPY --from=build /source/util/MsSqlMigratorUtility/out /app

RUN apk add --no-cache icu

ENTRYPOINT ["sh", "-c", "/app/MsSqlMigratorUtility \"${MSSQL_CONN_STRING}\" ${@}", "--" ]
