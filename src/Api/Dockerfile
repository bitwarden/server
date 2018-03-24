FROM microsoft/aspnetcore:2.0.5

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        cron \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
EXPOSE 80
COPY obj/Docker/publish/Api .

COPY obj/Docker/publish/Jobs /jobs
RUN mv /jobs/crontab /etc/cron.d/bitwarden-cron \
    && chmod 0644 /etc/cron.d/bitwarden-cron \
    && touch /var/log/cron.log

COPY entrypoint.sh /
RUN chmod +x /entrypoint.sh
ENTRYPOINT ["/entrypoint.sh"]
