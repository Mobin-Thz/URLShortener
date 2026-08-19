FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY URLShortener.Web/URLShortener.Web.csproj URLShortener.Web/
RUN dotnet restore URLShortener.Web/URLShortener.Web.csproj

COPY URLShortener.Web/ URLShortener.Web/
RUN dotnet publish URLShortener.Web/URLShortener.Web.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "URLShortener.Web.dll"]
