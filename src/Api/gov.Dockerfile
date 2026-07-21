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
WORKDIR /source/src/Api
RUN dotnet restore

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
FROM cgr.dev/bitwarden.com/dotnet-runtime-fips:10

LABEL com.bitwarden.product="bitwarden"
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5000
ENV SSL_CERT_DIR=/etc/bitwarden/ca-certificates
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV globalSettings__logDirectory=
EXPOSE 5000

WORKDIR /app
COPY --from=build --chown=65532:65532 /source/src/Api/out /app

# Run as the built-in nonroot user
USER 65532

# Run app binary as PID 1
ENTRYPOINT ["/app/Api"]
