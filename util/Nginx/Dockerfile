FROM nginx:1.12

COPY nginx.conf /etc/nginx
COPY proxy.conf /etc/nginx
COPY mime.types /etc/nginx

COPY entrypoint.sh /
RUN chmod +x /entrypoint.sh
ENTRYPOINT ["/entrypoint.sh"]
