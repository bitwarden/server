FROM nginx:1.12

USER root
RUN groupadd -g 999 bitwarden && \
    useradd -r -u 999 -g bitwarden bitwarden

USER bitwarden
COPY nginx.conf /etc/nginx
COPY proxy.conf /etc/nginx
COPY mime.types /etc/nginx
COPY entrypoint.sh /

USER root
RUN chmod +x /entrypoint.sh

USER bitwarden
ENTRYPOINT ["/entrypoint.sh"]
