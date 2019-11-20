FROM mcr.microsoft.com/mssql/server:2017-CU14

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

# Unfortunately above rule does not work (https://github.com/microsoft/mssql-docker/issues/312)
# Thankfully service does not have public network anymore
# However telemetry should perhaps still impact performance
RUN /opt/mssql/bin/mssql-conf set telemetry.customerfeedback false

HEALTHCHECK --timeout=3s CMD /opt/mssql-tools/bin/sqlcmd \
    -S localhost -U sa -P ${SA_PASSWORD} -Q "SELECT 1" || exit 1

ENTRYPOINT ["/entrypoint.sh"]
