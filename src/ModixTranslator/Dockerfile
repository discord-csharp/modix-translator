FROM mcr.microsoft.com/dotnet/core/runtime:5.0-buster-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:5.0-buster AS build
WORKDIR /src
COPY ["ModixTranslator/ModixTranslator.csproj", "ModixTranslator/"]
RUN dotnet restore "ModixTranslator/ModixTranslator.csproj"
COPY . .
WORKDIR "/src/ModixTranslator"
RUN dotnet build "ModixTranslator.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ModixTranslator.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ModixTranslator.dll"]