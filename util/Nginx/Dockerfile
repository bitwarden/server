FROM nginx:stable

LABEL com.bitwarden.product="bitwarden"

EXPOSE 8080
EXPOSE 8443

WORKDIR /bitwarden

RUN apt-get update && apt-get install -y --no-install-recommends \
        gosu \
        curl \
        wget \
    && rm -rf /var/lib/apt/lists/*

COPY confd/nginx-config.toml /etc/confd/conf.d/
COPY confd/nginx-config.conf.tmpl /etc/confd/templates/

RUN wget -O confd https://github.com/kelseyhightower/confd/releases/download/v0.16.0/confd-0.16.0-linux-amd64
RUN chmod +x confd
RUN ./confd -onetime -backend env 

COPY nginx.conf /etc/nginx
COPY proxy.conf /etc/nginx
COPY mime.types /etc/nginx
COPY security-headers.conf /etc/nginx
COPY security-headers-ssl.conf /etc/nginx
COPY logrotate.sh /
COPY entrypoint.sh /

RUN chmod +x /entrypoint.sh
RUN chmod +x /logrotate.sh

HEALTHCHECK CMD curl --insecure -Lfs https://localhost:8443/alive || curl -Lfs http://localhost:8080/alive || exit 1

ENTRYPOINT ["/entrypoint.sh"]
