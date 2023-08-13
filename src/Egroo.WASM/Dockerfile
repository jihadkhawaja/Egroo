#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:7.0.305 AS build

WORKDIR /

COPY ["src/Egroo.WASM/Egroo.WASM.csproj", "Egroo.WASM/"]
COPY ["src/Egroo.UI/Egroo.UI.csproj", "Egroo.UI/"]
RUN git clone --branch dev https://github.com/jihadkhawaja/mobilechat.client src/Egroo.Client.Package
RUN ls
COPY . .
WORKDIR "/src/Egroo.WASM"
RUN dotnet build "Egroo.WASM.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Egroo.WASM.csproj" -c Release -o /app/publish

FROM nginx:alpine AS final
WORKDIR /usr/share/nginx/html
COPY --from=publish /app/publish/wwwroot .
COPY ["src/Egroo.WASM/nginx.conf", "/etc/nginx/nginx.conf"]
