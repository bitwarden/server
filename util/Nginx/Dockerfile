FROM nginx:1.12

COPY nginx.conf /etc/nginx
COPY proxy.conf /etc/nginx
COPY mime.types /etc/nginx
COPY entrypoint.sh /

RUN groupadd -g 999 bitwarden \
    && useradd -r -u 999 -g bitwarden bitwarden \
    && mkdir -p /etc/bitwarden/nginx \
    && chown -R bitwarden:bitwarden /etc/bitwarden \
    && mkdir /etc/letsencrypt \
    && chown -R bitwarden:bitwarden /etc/letsencrypt \
    && mkdir /etc/ssl \
    && chown -R bitwarden:bitwarden /etc/ssl \
    && chmod +x /entrypoint.sh \
    && chown bitwarden:bitwarden /entrypoint.sh \
    && touch /var/run/nginx.pid \
    && touch /etc/nginx/conf.d/default.conf \
    && chown bitwarden:bitwarden /var/run/nginx.pid \
    && chown bitwarden:bitwarden /etc/nginx/conf.d/default.conf \
    && chown -R bitwarden:bitwarden /var/cache/nginx

USER bitwarden
ENTRYPOINT ["/entrypoint.sh"]
