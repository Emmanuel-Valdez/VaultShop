# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release

WORKDIR /src

COPY ["VaultShop.Web/VaultShop.Web.csproj", "VaultShop.Web/"]
COPY ["VaultShop.DataAccess/VaultShop.DataAccess.csproj", "VaultShop.DataAccess/"]
COPY ["VaultShop.Models/VaultShop.Models.csproj", "VaultShop.Models/"]
COPY ["VaultShop.Utility/VaultShop.Utility.csproj", "VaultShop.Utility/"]

RUN dotnet restore "VaultShop.Web/VaultShop.Web.csproj"

COPY . .

WORKDIR "/src/VaultShop.Web"
RUN dotnet publish "VaultShop.Web.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:8080 \
    Database__RunMigrationsOnStartup=false

EXPOSE 8080

COPY --from=build /app/publish .

USER $APP_UID

ENTRYPOINT ["dotnet", "VaultShop.Web.dll"]
