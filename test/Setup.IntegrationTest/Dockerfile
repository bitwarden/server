FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine3.21 AS build

WORKDIR /source
COPY ./Directory.Build.props .
COPY ./.editorconfig .
COPY ./util/Setup ./util/Setup/
COPY ./util/Migrator ./util/Migrator/
COPY ./src/Core ./src/Core/


WORKDIR /source/util/Setup
RUN dotnet restore
RUN dotnet publish --no-restore --output out

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine3.21

WORKDIR /app
COPY --from=build /source/util/Setup/out .

ENTRYPOINT ["dotnet", "Setup.dll"]
