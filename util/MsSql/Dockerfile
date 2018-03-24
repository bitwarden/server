FROM microsoft/mssql-server-linux:2017-CU4

USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        cron \
    && rm -rf /var/lib/apt/lists/*

RUN groupadd -g 999 bitwarden && \
    useradd -r -u 999 -g bitwarden bitwarden

COPY crontab /etc/cron.d/bitwarden-cron
RUN chmod 0644 /etc/cron.d/bitwarden-cron \
    && touch /var/log/cron.log

USER bitwarden
COPY backup-db.sql /
COPY backup-db.sh /
COPY entrypoint.sh /

USER root
RUN chmod +x /backup-db.sh
RUN chmod +x /entrypoint.sh

USER bitwarden
ENTRYPOINT ["/entrypoint.sh"]
