FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG TARGETPLATFORM
ARG BUILDPLATFORM
ENV PROJECT_NAME=Server

WORKDIR /build
COPY ../../ ./

WORKDIR /build/util/${PROJECT_NAME}

RUN dotnet publish "./Server.csproj" -c "Release" --self-contained /p:PublishSingleFile=true -o out

FROM mcr.microsoft.com/dotnet/aspnet:8.0

LABEL com.bitwarden.product="bitwarden"
ARG TARGETPLATFORM
ARG BUILDPLATFORM
ENV PROJECT_NAME=Server

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
    gosu \
    curl \
    krb5-user \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:5000

COPY --from=build /build/util/${PROJECT_NAME}/out/ /bitwarden_server
