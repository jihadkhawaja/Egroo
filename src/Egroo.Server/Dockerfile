FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY Directory.Build.props ./
COPY src/Egroo.Server/Egroo.Server.csproj src/Egroo.Server/
COPY src/Egroo.Server.SignalR/Egroo.Server.SignalR.csproj src/Egroo.Server.SignalR/
COPY src/Egroo.Contracts/Egroo.Contracts.csproj src/Egroo.Contracts/
RUN dotnet restore src/Egroo.Server/Egroo.Server.csproj

COPY . .
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish src/Egroo.Server/Egroo.Server.csproj --no-restore \
    -c "$BUILD_CONFIGURATION" -o /app/publish -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
RUN mkdir -p /app/App_Data && chown app /app/App_Data
USER app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Egroo.Server.dll"]
