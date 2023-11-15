###############################################
#                 Build stage                 #
###############################################
ARG BUILD_TAG=latest
FROM --platform=$BUILDPLATFORM localhost:5000/build:${BUILD_TAG} AS bitwarden-build

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
        gosu \
        curl \
    && rm -rf /var/lib/apt/lists/*

# Copy app from the build stage
WORKDIR /app
COPY --from=bitwarden-build /app/Icons ./
COPY entrypoint.sh /
RUN chmod +x /entrypoint.sh

HEALTHCHECK CMD curl -f http://localhost:5000/google.com/icon.png || exit 1

ENTRYPOINT ["/entrypoint.sh"]
