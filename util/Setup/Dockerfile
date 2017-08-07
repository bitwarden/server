FROM microsoft/dotnet:2.0.0-preview2-runtime

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
# Dependencies
        openssl \
&& rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY obj/Docker/publish .
