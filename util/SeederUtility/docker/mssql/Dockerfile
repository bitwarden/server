FROM mcr.microsoft.com/mssql/server:2022-CU22-ubuntu-22.04

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

ENTRYPOINT ["/docker-entrypoint.sh"]
