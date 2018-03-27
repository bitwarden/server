FROM microsoft/aspnetcore:2.0.5

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        cron \
        gosu \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS http://+:5000
WORKDIR /app
EXPOSE 5000
COPY obj/Docker/publish/Api .
COPY obj/Docker/publish/Jobs /jobs
COPY entrypoint.sh /

RUN mv /jobs/crontab /etc/cron.d/bitwarden-cron \
    && chmod 0644 /etc/cron.d/bitwarden-cron

RUN groupadd -g 999 bitwarden \
    && chmod +x /entrypoint.sh

ENTRYPOINT ["/entrypoint.sh"]
