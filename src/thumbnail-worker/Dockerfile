FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY thumbnail-worker.fsproj ./
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "thumbnail-worker.dll"]
