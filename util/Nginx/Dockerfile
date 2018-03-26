FROM nginx:1.12

COPY nginx.conf /etc/nginx
COPY proxy.conf /etc/nginx
COPY mime.types /etc/nginx
COPY entrypoint.sh /

RUN groupadd -g 999 bitwarden \
    && useradd -r -u 999 -g bitwarden bitwarden \
    && mkdir /etc/bitwarden \
    && chown -R bitwarden:bitwarden /etc/bitwarden \
    && chmod +x /entrypoint.sh \
    && chown bitwarden:bitwarden /entrypoint.sh \
    && touch /var/run/nginx.pid \
    && chown -R bitwarden:bitwarden /var/run/nginx.pid \
    && chown -R bitwarden:bitwarden /var/cache/nginx

USER bitwarden
ENTRYPOINT ["/entrypoint.sh"]
