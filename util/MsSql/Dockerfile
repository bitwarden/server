FROM mcr.microsoft.com/mssql/server:2022-CU12-ubuntu-22.04

LABEL com.bitwarden.product="bitwarden"

USER root:root

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
    gosu \
    tzdata \
    && rm -rf /var/lib/apt/lists/*

COPY backup-db.sql /
COPY backup-db.sh /
COPY entrypoint.sh /

RUN chmod +x /entrypoint.sh \
    && chmod +x /backup-db.sh

RUN /opt/mssql/bin/mssql-conf set telemetry.customerfeedback false

HEALTHCHECK --start-period=120s --timeout=3s CMD /opt/mssql-tools/bin/sqlcmd \
    -S localhost -U sa -P ${SA_PASSWORD} -Q "SELECT 1" || exit 1

ENTRYPOINT ["/entrypoint.sh"]
