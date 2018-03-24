FROM microsoft/aspnetcore:2.0.5

USER root
RUN groupadd -g 999 bitwarden && \
    useradd -r -u 999 -g bitwarden bitwarden

USER bitwarden
WORKDIR /app
EXPOSE 80
COPY obj/Docker/publish .
COPY entrypoint.sh /

USER root
RUN chmod +x /entrypoint.sh

USER bitwarden
ENTRYPOINT ["/entrypoint.sh"]
