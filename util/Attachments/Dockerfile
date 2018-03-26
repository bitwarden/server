FROM bitwarden/server

ENV ASPNETCORE_URLS http://+:5000
EXPOSE 5000
COPY entrypoint.sh /

RUN groupadd -g 999 bitwarden \
    && useradd -r -u 999 -g bitwarden bitwarden \
    && chown -R bitwarden:bitwarden /bitwarden_server \
    && mkdir /etc/bitwarden \
    && chown -R bitwarden:bitwarden /etc/bitwarden \
    && chmod +x /entrypoint.sh \
    && chown bitwarden:bitwarden /entrypoint.sh

USER bitwarden
ENTRYPOINT ["/entrypoint.sh"]
