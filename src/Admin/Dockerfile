FROM microsoft/aspnetcore:2.0.5

RUN groupadd -g 999 bitwarden && \
    useradd -r -u 999 -g bitwarden bitwarden
USER bitwarden

WORKDIR /app
EXPOSE 80
COPY obj/Docker/publish .

COPY entrypoint.sh /
RUN chmod +x /entrypoint.sh
ENTRYPOINT ["/entrypoint.sh"]
