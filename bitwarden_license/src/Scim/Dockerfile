FROM mcr.microsoft.com/dotnet/aspnet:6.0

LABEL com.bitwarden.product="bitwarden"

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        gosu \
        curl \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS http://+:5000
WORKDIR /app
EXPOSE 5000
COPY obj/build-output/publish .
COPY entrypoint.sh /
RUN chmod +x /entrypoint.sh

HEALTHCHECK CMD curl -f http://localhost:5000/alive || exit 1

ENTRYPOINT ["/entrypoint.sh"]
