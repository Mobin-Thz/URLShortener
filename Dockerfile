FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY URLShortener.sln ./
COPY URLShortener.API/URLShortener.API.csproj URLShortener.API/
COPY URLShortener.Application/URLShortener.Application.csproj URLShortener.Application/
COPY URLShortener.Domain/URLShortener.Domain.csproj URLShortener.Domain/
COPY URLShortener.Infrastructure/URLShortener.Infrastructure.csproj URLShortener.Infrastructure/
RUN dotnet restore URLShortener.API/URLShortener.API.csproj

COPY . .
RUN dotnet publish URLShortener.API/URLShortener.API.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "URLShortener.API.dll"]
