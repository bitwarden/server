FROM mcr.microsoft.com/mssql/server:2017-CU12

LABEL com.bitwarden.product="bitwarden"

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        cron \
        gosu \
    && rm -rf /var/lib/apt/lists/*

COPY crontab /etc/cron.d/bitwarden-cron
RUN chmod 0644 /etc/cron.d/bitwarden-cron
COPY backup-db.sql /
COPY backup-db.sh /
COPY entrypoint.sh /

RUN chmod +x /entrypoint.sh \
    && chmod +x /backup-db.sh

RUN /opt/mssql/bin/mssql-conf set telemetry.customerfeedback false
# As the setting above does not work, let's use the workaround below
RUN echo 127.0.0.1 settings-win.data.microsoft.com >> /etc/hosts
RUN echo 127.0.0.1 vortex.data.microsoft.com >> /etc/hosts

ENTRYPOINT ["/entrypoint.sh"]
