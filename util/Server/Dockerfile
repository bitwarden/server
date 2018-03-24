FROM microsoft/aspnetcore:2.0.5

RUN groupadd -g 999 bitwarden && \
    useradd -r -u 999 -g bitwarden bitwarden
USER bitwarden

COPY obj/Docker/publish /bitwarden_server
