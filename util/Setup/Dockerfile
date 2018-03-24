FROM microsoft/dotnet:2.0.5-runtime

USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        openssl \
    && rm -rf /var/lib/apt/lists/*

RUN groupadd -g 999 bitwarden && \
    useradd -r -u 999 -g bitwarden bitwarden

USER bitwarden
WORKDIR /app
COPY obj/Docker/publish .
