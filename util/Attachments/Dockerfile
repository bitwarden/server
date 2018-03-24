FROM bitwarden/server

USER bitwarden
EXPOSE 80
COPY entrypoint.sh /

USER root
RUN chmod +x /entrypoint.sh

USER bitwarden
ENTRYPOINT ["/entrypoint.sh"]
