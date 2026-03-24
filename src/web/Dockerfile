FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY web.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM nginx:alpine
WORKDIR /usr/share/nginx/html

COPY --from=build /app/publish/wwwroot .
COPY docker/nginx.conf /etc/nginx/conf.d/default.conf

ARG APP_INSIGHTS_CONN_STR
RUN sed -i "s|__APP_INSIGHTS_CONN_STR__|${APP_INSIGHTS_CONN_STR}|g" index.html

EXPOSE 80