FROM mcr.microsoft.com/dotnet/aspnet:6.0

LABEL com.bitwarden.product="bitwarden"

WORKDIR /app
COPY obj/build-output/publish .

CMD ["sh", "-c", "dotnet /app/MsSqlMigratorUtility.dll \"${MSSQL_CONN_STRING}\" -v"]
