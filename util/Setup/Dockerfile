FROM microsoft/dotnet:2.1.5-runtime

LABEL com.bitwarden.product="bitwarden" com.bitwarden.project="setup"

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        openssl \
        gosu \
&& rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY obj/Docker/publish .
COPY entrypoint.sh /
RUN chmod +x /entrypoint.sh

ENTRYPOINT ["/entrypoint.sh"]
