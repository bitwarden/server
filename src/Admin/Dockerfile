FROM microsoft/aspnetcore:2.0.5

WORKDIR /app
EXPOSE 80
COPY obj/Docker/publish .
COPY entrypoint.sh /

RUN groupadd -g 999 bitwarden \
    && useradd -r -u 999 -g bitwarden bitwarden \
    && mkdir /etc/bitwarden \
    && chown -R bitwarden:bitwarden /etc/bitwarden \
    && chmod +x /entrypoint.sh \
    && chown bitwarden:bitwarden /entrypoint.sh

USER bitwarden
ENTRYPOINT ["/entrypoint.sh"]
