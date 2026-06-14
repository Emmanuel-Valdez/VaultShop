# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release

WORKDIR /src

COPY ["UkiyoMono/UkiyoDesignsWeb.csproj", "UkiyoMono/"]
COPY ["Ukiyo.DataAccess/UkiyoDesigns.DataAccess.csproj", "Ukiyo.DataAccess/"]
COPY ["Ukiyo.Models/UkiyoDesigns.Models.csproj", "Ukiyo.Models/"]
COPY ["Ukiyo.Utility/UkiyoDesigns.Utility.csproj", "Ukiyo.Utility/"]

RUN dotnet restore "UkiyoMono/UkiyoDesignsWeb.csproj"

COPY . .

WORKDIR "/src/UkiyoMono"
RUN dotnet publish "UkiyoDesignsWeb.csproj" \
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

ENTRYPOINT ["dotnet", "UkiyoDesignsWeb.dll"]
