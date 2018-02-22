FROM microsoft/aspnetcore:2.0.5

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        unzip \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /tmp
COPY iconserver.sha256 .
RUN curl -L -o iconserver.zip https://github.com/mat/besticon/releases/download/v3.4.0/iconserver_linux_amd64.zip \
    && sha256sum -c iconserver.sha256 \
    && unzip iconserver.zip -d /etc/iconserver \
    && rm iconserver.*

WORKDIR /app
EXPOSE 80
COPY obj/Docker/publish .

COPY entrypoint.sh /
RUN chmod +x /entrypoint.sh
ENTRYPOINT ["/entrypoint.sh"]
