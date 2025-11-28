###############################################
#                 Build stage                 #
###############################################
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0-alpine3.21 AS build

# Docker buildx supplies the value for this arg
ARG TARGETPLATFORM

# Determine proper runtime value for .NET
# We put the value in a file to be read by later layers.
RUN if [ "$TARGETPLATFORM" = "linux/amd64" ]; then \
    RID=linux-musl-x64 ; \
    elif [ "$TARGETPLATFORM" = "linux/arm64" ]; then \
    RID=linux-musl-arm64 ; \
    elif [ "$TARGETPLATFORM" = "linux/arm/v7" ]; then \
    RID=linux-musl-arm ; \
    fi \
    && echo "RID=$RID" > /tmp/rid.txt

# Copy required project files
WORKDIR /source
COPY . ./

# Restore project dependencies and tools
WORKDIR /source/bitwarden_license/src/Scim
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
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine3.21

ARG TARGETPLATFORM
LABEL com.bitwarden.product="bitwarden"
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5000
ENV SSL_CERT_DIR=/etc/bitwarden/ca-certificates
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
EXPOSE 5000

RUN apk add --no-cache curl \
    krb5 \
    icu-libs \
    shadow \
    && apk add --no-cache --repository=http://dl-cdn.alpinelinux.org/alpine/edge/community gosu

# Copy app from the build stage
WORKDIR /app
COPY --from=build /source/bitwarden_license/src/Scim/out /app
COPY ./bitwarden_license/src/Scim/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

HEALTHCHECK CMD curl -f http://localhost:5000/alive || exit 1

ENTRYPOINT ["/entrypoint.sh"]
