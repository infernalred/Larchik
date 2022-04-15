#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["Larchik.API/Larchik.API.csproj", "Larchik.API/"]
COPY ["Larchik.Infrastructure/Larchik.Infrastructure.csproj", "Larchik.Infrastructure/"]
COPY ["Larchik.Application/Larchik.Application.csproj", "Larchik.Application/"]
COPY ["Larchik.Domain/Larchik.Domain.csproj", "Larchik.Domain/"]
COPY ["Larchik.Persistence/Larchik.Persistence.csproj", "Larchik.Persistence/"]
RUN dotnet restore "Larchik.API/Larchik.API.csproj"
COPY . .
WORKDIR "/src/Larchik.API"
RUN dotnet build "Larchik.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Larchik.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Larchik.API.dll"]
