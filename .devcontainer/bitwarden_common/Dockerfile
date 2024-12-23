FROM mcr.microsoft.com/devcontainers/dotnet:8.0

ARG NODE_MAJOR_VERSION="16"

RUN curl -fsSL https://deb.nodesource.com/setup_${NODE_MAJOR_VERSION}.x | bash - \
    && apt-get update \
    && apt-get install -y nodejs