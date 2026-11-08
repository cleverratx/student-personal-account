# Образ API. Собирается из папки backend/: docker build -t dekanat-api backend
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Сначала только файлы проекта, чтобы слой с restore кешировался, пока не меняются зависимости.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Dekanat.Api/Dekanat.Api.csproj src/Dekanat.Api/
RUN dotnet restore src/Dekanat.Api/Dekanat.Api.csproj

COPY src/ src/
# OpenAPI-документ лежит в репозитории и в образе не нужен, поэтому при публикации его не генерируем.
RUN dotnet publish src/Dekanat.Api/Dekanat.Api.csproj --configuration Release --no-restore --output /app \
    -p:OpenApiGenerateDocuments=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# Встроенный в образ непривилегированный пользователь, API слушает порт 8080.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Dekanat.Api.dll"]
