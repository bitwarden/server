FROM mcr.microsoft.com/dotnet/aspnet:6.0

LABEL com.bitwarden.product="bitwarden"

# Copy csproj files as distinct layers
WORKDIR /source
COPY util/MsSqlMigratorUtility/*.csproj ./util/MsSqlMigratorUtility/

# Restore MsSqlMigratorUtility project dependencies and tools
WORKDIR /source/util/MsSqlMigratorUtility
RUN dotnet restore

# Copy required project files
WORKDIR /source
COPY . ./util/MsSqlMigratorUtility/

# Build Setup app
WORKDIR /source/util/MsSqlMigratorUtility
RUN dotnet publish -c release -o /app/MsSqlMigratorUtility --no-restore --no-self-contained

WORKDIR /app

ENTRYPOINT ["sh", "-c", "dotnet /app/MsSqlMigratorUtility.dll \"${MSSQL_CONN_STRING}\" -v ${@}", "--" ]
