FROM microsoft/dotnet:2.0.5-runtime

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        openssl \
        gosu \
&& rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY obj/Docker/publish .
COPY entrypoint.sh /

RUN groupadd -g 999 bitwarden \
    && chmod +x /entrypoint.sh

ENTRYPOINT ["/entrypoint.sh"]
