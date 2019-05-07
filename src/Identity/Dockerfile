FROM mcr.microsoft.com/dotnet/core/aspnet:2.1.10

LABEL com.bitwarden.product="bitwarden"

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        gosu \
        libcurl3 \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS http://+:5000
WORKDIR /app
EXPOSE 5000
COPY obj/Docker/publish .
COPY entrypoint.sh /
RUN chmod +x /entrypoint.sh

ENTRYPOINT ["/entrypoint.sh"]
