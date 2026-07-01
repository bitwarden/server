FROM --platform=$BUILDPLATFORM nginx:1.30-alpine3.23

ARG TARGETPLATFORM
LABEL com.bitwarden.product="bitwarden"

ENV SSL_CERT_DIR=/etc/bitwarden/ca-certificates

RUN apk add --no-cache shadow \
    && apk add --no-cache --repository=http://dl-cdn.alpinelinux.org/alpine/edge/community gosu 

COPY util/Nginx/nginx.conf /etc/nginx
COPY util/Nginx/proxy.conf /etc/nginx
COPY util/Nginx/mime.types /etc/nginx
COPY util/Nginx/security-headers.conf /etc/nginx
COPY util/Nginx/security-headers-ssl.conf /etc/nginx
COPY util/Nginx/logrotate.sh /
COPY util/Nginx/entrypoint.sh /

EXPOSE 8080
EXPOSE 8443

RUN chmod +x /entrypoint.sh \
    && chmod +x /logrotate.sh

HEALTHCHECK CMD wget --quiet --tries=1 --no-check-certificate --output-document=/dev/null https://localhost:8443/alive || wget --quiet --tries=1 --output-document=/dev/null http://localhost:8080/alive || exit 1

ENTRYPOINT ["/entrypoint.sh"]
