FROM mcr.microsoft.com/mssql/server:2017-CU18-ubuntu-16.04

LABEL com.bitwarden.product="bitwarden"

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

# Enable traceflag 460 (requires MSSQL 2017 CU12 or later)
# Ref: https://support.microsoft.com/en-us/help/4468101/optional-replacement-for-string-or-binary-data-would-be-truncated
RUN /opt/mssql/bin/mssql-conf traceflag 460 on

# Does not work unfortunately (https://github.com/bitwarden/server/issues/286)
RUN /opt/mssql/bin/mssql-conf set telemetry.customerfeedback false

HEALTHCHECK --timeout=3s CMD /opt/mssql-tools/bin/sqlcmd \
    -S localhost -U sa -P ${SA_PASSWORD} -Q "SELECT 1" || exit 1

ENTRYPOINT ["/entrypoint.sh"]
