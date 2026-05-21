FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY csharp/NugsDownloader.sln csharp/
COPY csharp/NugsDownloader.Domain/NugsDownloader.Domain.csproj csharp/NugsDownloader.Domain/
COPY csharp/NugsDownloader.App/NugsDownloader.App.csproj csharp/NugsDownloader.App/
COPY csharp/NugsDownloader.Infrastructure/NugsDownloader.Infrastructure.csproj csharp/NugsDownloader.Infrastructure/
COPY csharp/NugsDownloader.Web/NugsDownloader.Web.csproj csharp/NugsDownloader.Web/

RUN dotnet restore csharp/NugsDownloader.Web/NugsDownloader.Web.csproj

COPY . .
RUN dotnet publish csharp/NugsDownloader.Web/NugsDownloader.Web.csproj \
    --configuration Release \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates ffmpeg \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /app/state /downloads

COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080
ENTRYPOINT ["dotnet", "NugsDownloader.Web.dll"]
