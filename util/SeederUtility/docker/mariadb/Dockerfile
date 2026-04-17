FROM mariadb:12

ARG PRESET_NAME=unknown
ARG GIT_SHA=unknown
ARG BUILD_DATE=unknown

LABEL bitwarden.seeder.preset="${PRESET_NAME}"
LABEL org.opencontainers.image.revision="${GIT_SHA}"
LABEL org.opencontainers.image.created="${BUILD_DATE}"

ENV MARIADB_DATABASE=vault_dev
ENV MARIADB_ROOT_PASSWORD=Password1!

COPY seed.sql /docker-entrypoint-initdb.d/seed.sql
