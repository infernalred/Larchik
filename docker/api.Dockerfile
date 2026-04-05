FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Larchik.sln ./
COPY src/Larchik.API/Larchik.API.csproj src/Larchik.API/
COPY src/Larchik.Application/Larchik.Application.csproj src/Larchik.Application/
COPY src/Larchik.Infrastructure/Larchik.Infrastructure.csproj src/Larchik.Infrastructure/
COPY src/Larchik.Persistence/Larchik.Persistence.csproj src/Larchik.Persistence/

RUN dotnet restore src/Larchik.API/Larchik.API.csproj

COPY src ./src

RUN dotnet publish src/Larchik.API/Larchik.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "Larchik.API.dll"]

