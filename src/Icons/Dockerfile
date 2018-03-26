FROM microsoft/aspnetcore:2.0.5

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        unzip \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /tmp
COPY iconserver.sha256 .
RUN curl -L -o iconserver.zip https://github.com/mat/besticon/releases/download/v3.6.0/iconserver_linux_amd64.zip \
    && sha256sum -c iconserver.sha256 \
    && unzip iconserver.zip -d /etc/iconserver \
    && rm iconserver.*

ENV ASPNETCORE_URLS http://+:5000
WORKDIR /app
EXPOSE 5000
COPY obj/Docker/publish .
COPY entrypoint.sh /

RUN groupadd -g 999 bitwarden \
    && useradd -r -u 999 -g bitwarden bitwarden \
    && chown -R bitwarden:bitwarden /app \
    && chown -R bitwarden:bitwarden /etc/iconserver \
    && chmod +x /entrypoint.sh \
    && chown bitwarden:bitwarden /entrypoint.sh

USER bitwarden
ENTRYPOINT ["/entrypoint.sh"]
