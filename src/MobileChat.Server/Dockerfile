FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["MobileChat.Server/MobileChat.Server.csproj", "MobileChat.Server/"]
RUN dotnet restore "MobileChat.Server/MobileChat.Server.csproj"
COPY . .
WORKDIR "/src/MobileChat.Server"

# Initialize and update the submodules
WORKDIR /src
RUN git submodule update --init

# Continue with the remaining steps
RUN dotnet build "MobileChat.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MobileChat.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MobileChat.Server.dll"]
