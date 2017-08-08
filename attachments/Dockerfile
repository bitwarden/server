FROM node

RUN npm install http-server -g
EXPOSE 80

COPY entrypoint.sh /
RUN chmod +x /entrypoint.sh
ENTRYPOINT ["/entrypoint.sh"]
