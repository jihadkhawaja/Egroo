#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Egroo/Egroo/Egroo.csproj", "Egroo/Egroo/"]
COPY ["Egroo/Egroo.Client/Egroo.Client.csproj", "Egroo/Egroo.Client/"]
COPY ["Egroo.UI/Egroo.UI.csproj", "Egroo.UI/"]
COPY ["jihadkhawaja.mobilechat.client/jihadkhawaja.mobilechat.client.csproj", "jihadkhawaja.mobilechat.client/"]
RUN dotnet restore "./Egroo/Egroo/Egroo.csproj"
COPY . .
WORKDIR "/src/Egroo/Egroo"
RUN dotnet build "./Egroo.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Egroo.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Egroo.dll"]