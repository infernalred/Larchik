FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Larchik.sln ./
COPY src/Larchik.Jobs/Larchik.Jobs.csproj src/Larchik.Jobs/
COPY src/Larchik.Application/Larchik.Application.csproj src/Larchik.Application/
COPY src/Larchik.Infrastructure/Larchik.Infrastructure.csproj src/Larchik.Infrastructure/
COPY src/Larchik.Persistence/Larchik.Persistence.csproj src/Larchik.Persistence/

RUN dotnet restore src/Larchik.Jobs/Larchik.Jobs.csproj

COPY src ./src

RUN dotnet publish src/Larchik.Jobs/Larchik.Jobs.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Larchik.Jobs.dll"]

