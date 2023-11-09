FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:6.0 AS dotnet-build

# Docker buildx supplies the value for this arg
ARG TARGETPLATFORM
ENV NODE_VERSION=16.20.2
ENV NVM_DIR /usr/local/nvm

# Determine proper runtime value for .NET
# We put the value in a file to be read by later layers.
RUN if [ "$TARGETPLATFORM" = "linux/amd64" ]; then \
      RID=linux-x64 ; \
    elif [ "$TARGETPLATFORM" = "linux/arm64" ]; then \
      RID=linux-arm64 ; \
    elif [ "$TARGETPLATFORM" = "linux/arm/v7" ]; then \
      RID=linux-arm ; \
    fi \
    && echo "RID=$RID" > /tmp/rid.txt

# Add packages
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Add packages
RUN mkdir -p $NVM_DIR
RUN curl -o- https://raw.githubusercontent.com/nvm-sh/nvm/v0.39.5/install.sh | bash \
    && . $NVM_DIR/nvm.sh \
    && nvm install $NODE_VERSION \
    && nvm alias default $NODE_VERSION \
    && nvm use default
ENV NODE_PATH $NVM_DIR/versions/node/v$NODE_VERSION/lib/node_modules
ENV PATH      $NVM_DIR/versions/node/v$NODE_VERSION/bin:$PATH

RUN npm install -g gulp

WORKDIR /source
COPY src/Admin/*.csproj .
COPY Directory.Build.props .

# Restore Admin project dependencies and tools
RUN . /tmp/rid.txt && dotnet restore -r $RID

COPY src/Admin/. .
#COPY ../../.git/. ./.git/

# Build Admin app
RUN npm install
RUN gulp --gulpfile "gulpfile.js" build
RUN . /tmp/rid.txt && dotnet publish -c release -o /app/Admin --no-restore --no-self-contained -r $RID

###############################################
#                  App stage                  #
###############################################
FROM mcr.microsoft.com/dotnet/aspnet:6.0
ARG TARGETPLATFORM
LABEL com.bitwarden.product="bitwarden"
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS http://+:5000
EXPOSE 5000

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        curl \
        gosu \
    && rm -rf /var/lib/apt/lists/*

# Copy all apps from dotnet-build stage
WORKDIR /app
COPY --from=dotnet-build /app ./

COPY src/Admin/entrypoint.sh /
RUN chmod +x /entrypoint.sh

HEALTHCHECK CMD curl -f http://localhost:5000 || exit 1

ENTRYPOINT ["/entrypoint.sh"]
