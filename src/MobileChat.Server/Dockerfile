FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
RUN git clone https://github.com/jihadkhawaja/mobilechat.server MobileChatServer
COPY ["MobileChat.Server/MobileChat.Server.csproj", "MobileChat.Server/"]
COPY ["MobileChatServer/src/jihadkhawaja.mobilechat.server/jihadkhawaja.mobilechat.server.csproj", "MobileChatServer/src/jihadkhawaja.mobilechat.server/"]
RUN dotnet restore "MobileChat.Server/MobileChat.Server.csproj"
COPY . .
WORKDIR "/src/MobileChat.Server"
RUN dotnet build "MobileChat.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MobileChat.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MobileChat.Server.dll"]
