FROM mcr.microsoft.com/mssql/server:2025-CU5-ubuntu-24.04

ARG PRESET_NAME=unknown
ARG GIT_SHA=unknown
ARG BUILD_DATE=unknown

LABEL bitwarden.seeder.preset="${PRESET_NAME}"
LABEL org.opencontainers.image.revision="${GIT_SHA}"
LABEL org.opencontainers.image.created="${BUILD_DATE}"

ENV ACCEPT_EULA=Y
ENV MSSQL_PID=Developer

USER root
RUN mkdir -p /seed
COPY vault_dev.mdf /seed/vault_dev.mdf
COPY vault_dev_log.ldf /seed/vault_dev_log.ldf
COPY docker-entrypoint.sh /docker-entrypoint.sh
RUN chmod +x /docker-entrypoint.sh

# Healthy only once the seed is attached, so dependents can wait on it rather than
# racing the attach and migrating an empty database into place
HEALTHCHECK --interval=10s --timeout=15s --retries=90 --start-period=30s \
    CMD /opt/mssql-tools18/bin/sqlcmd -S localhost -U SA -P "${SA_PASSWORD}" -C \
        -d vault_dev -b -Q "SET NOCOUNT ON; IF NOT EXISTS (SELECT 1 FROM [User]) RAISERROR('unseeded', 16, 1)" > /dev/null 2>&1

ENTRYPOINT ["/docker-entrypoint.sh"]
