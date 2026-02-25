###############################################
#                 Build stage                 #
###############################################
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0-alpine3.21 AS build

# Docker buildx supplies the value for this arg
ARG TARGETPLATFORM

# Determine proper runtime value for .NET
# We put the value in a file to be read by later layers.
RUN if [ "$TARGETPLATFORM" = "linux/amd64" ]; then \
    RID=linux-x64 ; \
    RUST_TARGET=x86_64-unknown-linux-gnu ; \
    elif [ "$TARGETPLATFORM" = "linux/arm64" ]; then \
    RID=linux-arm64 ; \
    RUST_TARGET=aarch64-unknown-linux-gnu ; \
    elif [ "$TARGETPLATFORM" = "linux/arm/v7" ]; then \
    RID=linux-arm ; \
    RUST_TARGET=armv7-unknown-linux-gnueabihf ; \
    fi \
    && echo "RID=$RID" > /tmp/rid.txt \
    && echo "$RUST_TARGET" > /tmp/rust_target.txt

# Install Rust
RUN apk add --no-cache build-base curl
RUN curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | /bin/sh -s -- -y --default-toolchain stable --profile minimal \
     && . $HOME/.cargo/env \
     && rustup target add $(cat /tmp/rust_target.txt)
ENV PATH="/root/.cargo/bin:${PATH}"
RUN rustup set default-host $(cat /tmp/rust_target.txt)

# Copy required project files
WORKDIR /source
COPY . ./

# Restore project dependencies and tools
WORKDIR /source/util/SeederApi
# echo the files in the current working directory
RUN ls -la
RUN . /tmp/rid.txt && dotnet restore -r $RID

# Build project
WORKDIR /source/util/SeederApi
RUN . /tmp/rid.txt && dotnet publish SeederApi.csproj \
    -c release \
    --no-restore \
    --self-contained \
    /p:PublishSingleFile=true \
    -r $RID \
    -o out

###############################################
#                  App stage                  #
###############################################
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine3.21 AS app

ARG TARGETPLATFORM
LABEL com.bitwarden.product="bitwarden"

ENV SSL_CERT_DIR=/etc/bitwarden/ca-certificates
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000
HEALTHCHECK CMD curl -f http://localhost:5000/alive || exit 1

# Copy app from the build stage
WORKDIR /app
COPY --from=build /source/util/SeederApi/out /app

RUN apk add --no-cache curl icu-libs 

ENTRYPOINT ["/app/SeederApi"]
