FROM microsoft/dotnet:2.0.5-runtime

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        openssl \
&& rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY obj/Docker/publish .

RUN groupadd -g 999 bitwarden \
    && useradd -r -u 999 -g bitwarden bitwarden \
    && chown -R bitwarden:bitwarden /app \
    && mkdir /bitwarden \
    && chown -R bitwarden:bitwarden /bitwarden

USER bitwarden
