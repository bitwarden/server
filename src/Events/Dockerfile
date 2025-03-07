FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG TARGETPLATFORM
ARG BUILDPLATFORM
ENV PROJECT_NAME=Events

WORKDIR /build
COPY ../../ ./

WORKDIR /build/src/${PROJECT_NAME}

RUN dotnet publish --self-contained /p:PublishSingleFile=true -o out

FROM mcr.microsoft.com/dotnet/aspnet:8.0

LABEL com.bitwarden.product="bitwarden"

ENV PROJECT_NAME=Events

RUN apt-get update \
  && apt-get install -y --no-install-recommends \
  gosu \
  curl \
  krb5-user \
  && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:5000

EXPOSE 5000
WORKDIR /app
COPY --from=build /build/src/${PROJECT_NAME}/out /app
COPY ./src/${PROJECT_NAME}/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh
HEALTHCHECK CMD curl -f http://localhost:5000/alive || exit 1

ENTRYPOINT ["/entrypoint.sh"]
