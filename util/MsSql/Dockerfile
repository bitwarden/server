FROM mcr.microsoft.com/mssql/rhel/server:2025-CU5-rhel-9.1

LABEL com.bitwarden.product="bitwarden"

USER root:root

COPY util/MsSql/backup-db.sql /
COPY util/MsSql/backup-db.sh /
COPY util/MsSql/entrypoint.sh /

RUN chmod +x /entrypoint.sh \
    && chmod +x /backup-db.sh

RUN /opt/mssql/bin/mssql-conf set telemetry.customerfeedback false

HEALTHCHECK --start-period=120s --timeout=3s CMD /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P ${SA_PASSWORD} -C -Q "SELECT 1" || exit 1

ENTRYPOINT ["/entrypoint.sh"]
