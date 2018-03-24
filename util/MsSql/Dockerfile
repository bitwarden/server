FROM microsoft/mssql-server-linux:2017-CU4

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        cron \
    && rm -rf /var/lib/apt/lists/*

COPY crontab /etc/cron.d/bitwarden-cron
RUN chmod 0644 /etc/cron.d/bitwarden-cron \
    && touch /var/log/cron.log

COPY backup-db.sql /
COPY backup-db.sh /
RUN chmod +x /backup-db.sh

COPY entrypoint.sh /
RUN chmod +x /entrypoint.sh
ENTRYPOINT ["/entrypoint.sh"]
