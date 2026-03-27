###############################################
#                 Build stage                 #
###############################################
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build

# Docker buildx supplies these values
ARG TARGETPLATFORM
ARG BUILDPLATFORM

# Install base build dependencies
RUN apt-get update && apt-get install -y \
    build-essential \
    curl \
    pkg-config \
    libssl-dev \
    && rm -rf /var/lib/apt/lists/*

# Install Rust toolchain on build platform
RUN curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh -s -- -y \
    --default-toolchain stable \
    --profile minimal \
    --no-modify-path

ENV PATH="/root/.cargo/bin:${PATH}"

# Determine target architecture and install cross-compilation tools
RUN case "$TARGETPLATFORM" in \
    "linux/amd64") \
        RUST_TARGET=x86_64-unknown-linux-gnu && \
        RID=linux-x64 && \
        ARCH_PACKAGES="" \
        ;; \
    "linux/arm64") \
        RUST_TARGET=aarch64-unknown-linux-gnu && \
        RID=linux-arm64 && \
        ARCH_PACKAGES="gcc-aarch64-linux-gnu g++-aarch64-linux-gnu libc6-dev-arm64-cross" \
        ;; \
    *) \
        echo "Unsupported platform: $TARGETPLATFORM" && exit 1 \
        ;; \
    esac \
    && if [ -n "$ARCH_PACKAGES" ]; then \
        apt-get update && apt-get install -y $ARCH_PACKAGES && rm -rf /var/lib/apt/lists/* ; \
    fi \
    && echo "RUST_TARGET=${RUST_TARGET}" >> /etc/environment \
    && echo "RID=${RID}" >> /etc/environment \
    && . /etc/environment \
    && rustup target add ${RUST_TARGET} \
    && echo "Rust target: ${RUST_TARGET}, .NET RID: ${RID}"

# Configure Rust for cross-compilation with proper linkers
RUN . /etc/environment \
    && mkdir -p /root/.cargo \
    && case "$TARGETPLATFORM" in \
    "linux/amd64") \
        echo "[target.x86_64-unknown-linux-gnu]" >> /root/.cargo/config.toml \
        && echo "linker = \"gcc\"" >> /root/.cargo/config.toml \
        ;; \
    "linux/arm64") \
        echo "[target.aarch64-unknown-linux-gnu]" >> /root/.cargo/config.toml \
        && echo "linker = \"aarch64-linux-gnu-gcc\"" >> /root/.cargo/config.toml \
        ;; \
    esac

# Copy project files
WORKDIR /source
COPY . ./

# Restore .NET dependencies
WORKDIR /source/util/SeederApi
RUN . /etc/environment && dotnet restore -r ${RID}

# Build the project with Rust support
WORKDIR /source/util/SeederApi
RUN . /etc/environment \
    && export CARGO_TARGET_DIR=/tmp/cargo_target \
    && export NoWarn="CA1305;CS1591" \
    && rustc --version \
    && cargo --version \
    && echo "Building for Rust target: ${RUST_TARGET}, .NET RID: ${RID}" \
    && dotnet publish SeederApi.csproj \
    -c Release \
    --no-restore \
    --self-contained \
    /p:PublishSingleFile=true \
    -r ${RID} \
    -o /app/out

###############################################
#                  App stage                  #
###############################################
FROM mcr.microsoft.com/dotnet/aspnet:8.0-azurelinux3.0-distroless-extra AS app

ARG TARGETPLATFORM
LABEL com.bitwarden.product="bitwarden"

ENV SSL_CERT_DIR=/etc/bitwarden/ca-certificates
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000

# Set up health check wrapper
# Get the executable and copy it to any path you want
COPY --from=ghcr.io/alexaka1/distroless-dotnet-healthchecks:1@sha256:d2a71a595020f13a9283f5e1f93c5f92bc2385c1d9b36e128a00d35863995aba / /healthcheck
# Setup your healthcheck endpoints via environment variable in Dockerfile, or at runtime via `docker run -e DISTROLESS_HEALTHCHECKS_URIS__0="http://localhost/healthz" -e DISTROLESS_HEALTHCHECKS_URIS__1="http://localhost/some/other/endpoint"`
ENV DISTROLESS_HEALTHCHECKS_URI="http://localhost:5000/alive"
# Setup the healthcheck using the EXEC array syntax
HEALTHCHECK CMD ["/healthcheck/Distroless.HealthChecks"]

# Copy app from the build stage
WORKDIR /app
COPY --from=build /app/out /app

ENTRYPOINT ["/app/SeederApi"]
