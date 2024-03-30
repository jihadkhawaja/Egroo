FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /
COPY ["src/Egroo.Server/Egroo.Server.csproj", "Egroo.Server/"]
RUN git clone --branch dev https://github.com/jihadkhawaja/mobilechat.server src/MobileChat.Server.Package
COPY . .
WORKDIR "/src/Egroo.Server"
RUN dotnet build "Egroo.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Egroo.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Egroo.Server.dll"]
