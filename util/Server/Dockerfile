###############################################
#                 Build stage                 #
###############################################
ARG BUILD_TAG=latest
FROM --platform=$BUILDPLATFORM build:${BUILD_TAG} AS bitwarden-build

###############################################
#                  App stage                  #
###############################################
FROM mcr.microsoft.com/dotnet/aspnet:6.0

LABEL com.bitwarden.product="bitwarden"

# Copy app from the build stage
WORKDIR /bitwarden_server
COPY --from=bitwarden-build /app/Server ./