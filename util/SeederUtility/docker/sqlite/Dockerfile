FROM busybox:stable

ARG PRESET_NAME=unknown
ARG GIT_SHA=unknown
ARG BUILD_DATE=unknown

LABEL bitwarden.seeder.preset="${PRESET_NAME}"
LABEL org.opencontainers.image.revision="${GIT_SHA}"
LABEL org.opencontainers.image.created="${BUILD_DATE}"

# The SQLite database file — mounted by the application at runtime
COPY seed.db /seed.db

CMD ["sh", "-c", "cp /seed.db /data/vault_dev.db && echo 'SQLite seed copied'"]
