FROM nginx:stable

LABEL com.bitwarden.product="bitwarden"

EXPOSE 8080
EXPOSE 8443

WORKDIR /bitwarden
RUN mkdir -p /bitwarden/ssl

# TODO: Remove Vim after testing
RUN apt-get update && apt-get install -y --no-install-recommends \
        gosu \
        curl \
        wget \
        vim \
    && rm -rf /var/lib/apt/lists/*

COPY util/Nginx/confd/nginx-config.toml /etc/confd/conf.d/
COPY util/Nginx/confd/nginx-config.conf.tmpl /etc/confd/templates/

RUN wget -O /usr/local/bin/confd https://github.com/kelseyhightower/confd/releases/download/v0.16.0/confd-0.16.0-linux-amd64
RUN chmod +x /usr/local/bin/confd

COPY util/Nginx/nginx.conf /etc/nginx
COPY util/Nginx/proxy.conf /etc/nginx
COPY util/Nginx/mime.types /etc/nginx
COPY util/Nginx/security-headers.conf /etc/nginx
COPY util/Nginx/security-headers-ssl.conf /etc/nginx
COPY logrotate.sh /
COPY entrypoint.sh /

RUN chmod +x /entrypoint.sh
RUN chmod +x /logrotate.sh

HEALTHCHECK CMD curl --insecure -Lfs https://localhost:8443/alive || curl -Lfs http://localhost:8080/alive || exit 1

ENTRYPOINT ["/entrypoint.sh"]
