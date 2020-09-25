FROM mcr.microsoft.com/dotnet/runtime:5.0-buster-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:5.0-buster-slim AS build
WORKDIR /src
COPY ["ModixTranslator/ModixTranslator.csproj", "ModixTranslator/"]
COPY [".nuget/nuget.config", ".nuget/"]
RUN dotnet restore "ModixTranslator/ModixTranslator.csproj" --configfile .nuget/nuget.config
COPY . .
WORKDIR "/src/ModixTranslator"
RUN dotnet build "ModixTranslator.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ModixTranslator.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ModixTranslator.dll"]