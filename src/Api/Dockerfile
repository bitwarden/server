FROM microsoft/aspnetcore:2.0.5

USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        cron \
    && rm -rf /var/lib/apt/lists/*

RUN groupadd -g 999 bitwarden && \
    useradd -r -u 999 -g bitwarden bitwarden

USER bitwarden
WORKDIR /app
EXPOSE 80
COPY obj/Docker/publish/Api .
COPY obj/Docker/publish/Jobs /jobs

USER root
RUN mv /jobs/crontab /etc/cron.d/bitwarden-cron \
    && chmod 0644 /etc/cron.d/bitwarden-cron \
    && touch /var/log/cron.log

USER bitwarden
COPY entrypoint.sh /

USER root
RUN chmod +x /entrypoint.sh

USER bitwarden
ENTRYPOINT ["/entrypoint.sh"]
