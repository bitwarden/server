FROM microsoft/aspnetcore:2.0.0

WORKDIR /app
EXPOSE 80
COPY obj/Docker/publish .

COPY entrypoint.sh /
RUN chmod +x /entrypoint.sh
ENTRYPOINT ["/entrypoint.sh"]
