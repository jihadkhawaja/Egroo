FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
RUN dotnet workload install wasm-tools
COPY . .
ARG EGROO_API_URL=https://api.egroo.org/
RUN dotnet publish src/Egroo.Avalonia/Browser -c Release -p:EgrooApiUrl=${EGROO_API_URL} -o /app

FROM nginx:alpine
COPY src/Egroo.Avalonia/Browser/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/wwwroot/ /usr/share/nginx/html/
EXPOSE 8080
