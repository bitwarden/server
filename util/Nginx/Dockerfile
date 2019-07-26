FROM nginx:1.16

LABEL com.bitwarden.product="bitwarden"

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        gosu \
        curl \
    && rm -rf /var/lib/apt/lists/*

COPY nginx.conf /etc/nginx
COPY proxy.conf /etc/nginx
COPY mime.types /etc/nginx
COPY security-headers.conf /etc/nginx
COPY security-headers-ssl.conf /etc/nginx
COPY entrypoint.sh /

EXPOSE 8080
EXPOSE 8443

RUN chmod +x /entrypoint.sh

HEALTHCHECK curl -L -f http://localhost/alive || exit 1

ENTRYPOINT ["/entrypoint.sh"]
