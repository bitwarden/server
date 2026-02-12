FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o /app

# Runtime stage (distroless)
FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy-chiseled
WORKDIR /app
COPY --from=build /app .
EXPOSE 5000
LABEL com.bitwarden.product="bitwarden"
ENTRYPOINT ["dotnet", "SeederApi.dll"]