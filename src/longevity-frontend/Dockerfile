FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY longevity-frontend.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM nginx:alpine
WORKDIR /usr/share/nginx/html

COPY --from=build /app/publish/wwwroot .
COPY docker/nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80