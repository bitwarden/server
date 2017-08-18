FROM microsoft/mssql-server-linux

COPY setup.sql /

COPY entrypoint.sh /
RUN chmod +x /entrypoint.sh
ENTRYPOINT ["/entrypoint.sh"]
