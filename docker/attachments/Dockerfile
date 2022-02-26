FROM bitwarden/server

LABEL com.bitwarden.product="bitwarden"
ENV ASPNETCORE_URLS http://+:5000
EXPOSE 5000

USER bitwarden:bitwarden
HEALTHCHECK CMD curl -f http://localhost:5000/alive || exit 1
ENTRYPOINT ["dotnet", "/bitwarden_server/Server.dll", "/contentRoot=/etc/bitwarden/core/attachments", "/webRoot=.", "/serveUnknown=true"]
