#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["MobileChat.Server/MobileChat.Server.csproj", "MobileChat.Server/"]
COPY ["MobileChat.Shared/MobileChat.Shared.csproj", "MobileChat.Shared/"]
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