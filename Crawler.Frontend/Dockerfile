FROM mcr.microsoft.com/dotnet/sdk:9.0 AS restore
WORKDIR /src
ARG BUILD_CONFIGURATION=Release
COPY ["Crawler.Frontend/Crawler.Frontend.csproj", "Crawler.Frontend/"]
COPY ["Domain/Domain.csproj", "Domain/"]
RUN dotnet restore "Crawler.Frontend/Crawler.Frontend.csproj"

FROM restore AS publish
ARG BUILD_CONFIGURATION=Release
COPY . .
WORKDIR /src/Crawler.Frontend
RUN dotnet publish "Crawler.Frontend.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
# Listen on 8081 by default; override ASPNETCORE_URLS at runtime if needed.
ENV ASPNETCORE_URLS=http://+:8081 \
    DOTNET_EnableDiagnostics=0
RUN useradd --create-home --uid 1001 --shell /usr/sbin/nologin crawler
COPY --from=publish /app/publish .
EXPOSE 8081
USER crawler
ENTRYPOINT ["dotnet", "Crawler.Frontend.dll"]
