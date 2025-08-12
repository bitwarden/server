FROM --platform=$BUILDPLATFORM nginx:stable-alpine3.21

ARG TARGETPLATFORM
LABEL com.bitwarden.product="bitwarden"

ENV SSL_CERT_DIR=/etc/bitwarden/ca-certificates

RUN apk add --no-cache curl \
    shadow \
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

HEALTHCHECK CMD curl --insecure -Lfs https://localhost:8443/alive || curl -Lfs http://localhost:8080/alive || exit 1

ENTRYPOINT ["/entrypoint.sh"]
