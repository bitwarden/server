FROM mysql:8.0

LABEL com.bitwarden.product="bitwarden"

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        gosu \
        tzdata \
    && rm -rf /var/lib/apt/lists/*

COPY backup-db.sh /
COPY entrypoint.sh /

RUN chmod +x /entrypoint.sh \
    && chmod +x /backup-db.sh

HEALTHCHECK --start-period=120s --timeout=3s CMD mysql \
    -hlocalhost -uroot -p${MYSQL_ROOT_PASSWORD} -e"SELECT 1;" || exit 1

ENTRYPOINT ["/bw-entrypoint.sh"]
