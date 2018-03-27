FROM microsoft/mssql-server-linux:2017-CU4

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        cron \
        gosu \
    && rm -rf /var/lib/apt/lists/*

RUN groupadd -g 999 bitwarden

COPY crontab /etc/cron.d/bitwarden-cron
RUN chmod 0644 /etc/cron.d/bitwarden-cron
COPY backup-db.sql /
COPY backup-db.sh /
COPY entrypoint.sh /

RUN chmod +x /entrypoint.sh \
    && chmod +x /backup-db.sh
ENTRYPOINT ["/entrypoint.sh"]
