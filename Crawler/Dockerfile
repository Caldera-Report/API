FROM mcr.microsoft.com/dotnet/sdk:9.0 AS restore
WORKDIR /src
ARG BUILD_CONFIGURATION=Release
COPY ["Crawler/Crawler.csproj", "Crawler/"]
COPY ["Domain/Domain.csproj", "Domain/"]
RUN dotnet restore "Crawler/Crawler.csproj"

FROM restore AS publish
ARG BUILD_CONFIGURATION=Release
COPY . .
WORKDIR /src/Crawler
RUN dotnet publish "Crawler.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

FROM mcr.microsoft.com/dotnet/runtime:9.0 AS final
WORKDIR /app
ENV DOTNET_EnableDiagnostics=0
RUN useradd --create-home --uid 1001 --shell /usr/sbin/nologin crawler
COPY --from=publish /app/publish .
USER crawler
ENTRYPOINT ["dotnet", "Crawler.dll"]
