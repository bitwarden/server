FROM nginx:stable

LABEL com.bitwarden.product="bitwarden"
LABEL com.bitwarden.project="nginx"

EXPOSE 8080
EXPOSE 8443

WORKDIR /etc/bitwarden
RUN mkdir -p /etc/bitwarden/ssl
RUN mkdir -p /etc/bitwarden/nginx
RUN mkdir -p /var/run/nginx
RUN chown -R nginx:nginx /var/run/nginx
RUN chown -R nginx:nginx /var/cache/nginx
RUN chown -R nginx:nginx /var/log/nginx

# TODO: Remove Vim after testing
RUN apt-get update && apt-get install -y --no-install-recommends \
        curl \
        wget \
        vim \
    && rm -rf /var/lib/apt/lists/*

COPY docker/nginx/confd/nginx-config.toml /etc/confd/conf.d/
COPY docker/nginx/confd/nginx-config.conf.tmpl /etc/confd/templates/

ADD https://github.com/kelseyhightower/confd/releases/download/v0.16.0/confd-0.16.0-linux-amd64 /usr/local/bin/confd
RUN chmod +x /usr/local/bin/confd

COPY docker/nginx/nginx.conf /etc/nginx
COPY docker/nginx/proxy.conf /etc/nginx
COPY docker/nginx/mime.types /etc/nginx
COPY docker/nginx/security-headers.conf /etc/nginx
COPY docker/nginx/security-headers-ssl.conf /etc/nginx
COPY docker/nginx/logrotate.sh /
COPY docker/nginx/entrypoint.sh /

RUN chmod +x /entrypoint.sh
RUN chmod +x /logrotate.sh

RUN chown -R nginx:nginx /etc/bitwarden

USER nginx:nginx
HEALTHCHECK CMD curl --insecure -Lfs https://localhost:8443/alive || curl -Lfs http://localhost:8080/alive || exit 1
ENTRYPOINT ["/entrypoint.sh"]
