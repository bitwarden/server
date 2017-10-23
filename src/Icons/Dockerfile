FROM microsoft/aspnetcore:2.0.0

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        unzip \
    && rm -rf /var/lib/apt/lists/*

RUN curl -L -o /tmp/iconserver.zip https://github.com/mat/besticon/releases/download/v3.4.0/iconserver_linux_amd64.zip \
    && unzip /tmp/iconserver.zip -d /etc/iconserver \
    && rm /tmp/iconserver.zip

WORKDIR /app
EXPOSE 80
COPY obj/Docker/publish .

COPY entrypoint.sh /
RUN chmod +x /entrypoint.sh
ENTRYPOINT ["/entrypoint.sh"]
