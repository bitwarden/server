FROM microsoft/aspnetcore:2.0.5

USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        unzip \
    && rm -rf /var/lib/apt/lists/*

RUN groupadd -g 999 bitwarden && \
    useradd -r -u 999 -g bitwarden bitwarden

USER bitwarden
WORKDIR /tmp
COPY iconserver.sha256 .
RUN curl -L -o iconserver.zip https://github.com/mat/besticon/releases/download/v3.6.0/iconserver_linux_amd64.zip \
    && sha256sum -c iconserver.sha256 \
    && unzip iconserver.zip -d /etc/iconserver \
    && rm iconserver.*

WORKDIR /app
EXPOSE 80
COPY obj/Docker/publish .

COPY entrypoint.sh /

USER root
RUN chmod +x /entrypoint.sh

USER bitwarden
ENTRYPOINT ["/entrypoint.sh"]
