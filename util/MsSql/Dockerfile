FROM microsoft/mssql-server-linux:2017-CU4

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        cron \
    && rm -rf /var/lib/apt/lists/*

RUN groupadd -g 999 bitwarden \
    && useradd -r -u 999 -g bitwarden bitwarden

COPY crontab /etc/cron.d/bitwarden-cron
RUN chmod 0644 /etc/cron.d/bitwarden-cron \
    && touch /var/log/cron.log \
    && chown bitwarden:bitwarden /var/log/cron.log

COPY backup-db.sql /
COPY backup-db.sh /
COPY entrypoint.sh /

RUN mkdir -p /etc/bitwarden/mssql/backups \
    && chown -R bitwarden:bitwarden /etc/bitwarden \
    && mkdir /var/opt/mssql/data \
    && chown -R bitwarden:bitwarden /var/opt/mssql \
    && chmod +x /entrypoint.sh \
    && chmod +x /backup-db.sh \
    && chown bitwarden:bitwarden /entrypoint.sh \
    && chown bitwarden:bitwarden /backup-db.sh \
    && chown bitwarden:bitwarden /backup-db.sql

USER bitwarden
ENTRYPOINT ["/entrypoint.sh"]
