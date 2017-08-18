FROM microsoft/mssql-server-linux

COPY setup.sql /
COPY setup.sh /
RUN chmod +x /setup.sh

COPY entrypoint.sh /
RUN chmod +x /entrypoint.sh
ENTRYPOINT ["/entrypoint.sh"]
