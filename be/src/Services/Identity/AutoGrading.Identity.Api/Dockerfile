FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["be/Directory.Build.props", "be/"]
COPY ["be/src/BuildingBlocks/AutoGrading.Contracts/AutoGrading.Contracts.csproj", "be/src/BuildingBlocks/AutoGrading.Contracts/"]
COPY ["be/src/BuildingBlocks/AutoGrading.Common/AutoGrading.Common.csproj", "be/src/BuildingBlocks/AutoGrading.Common/"]
COPY ["be/src/Services/Identity/AutoGrading.Identity.Api/AutoGrading.Identity.Api.csproj", "be/src/Services/Identity/AutoGrading.Identity.Api/"]
RUN dotnet restore "be/src/Services/Identity/AutoGrading.Identity.Api/AutoGrading.Identity.Api.csproj"
COPY . .
WORKDIR "/src/be/src/Services/Identity/AutoGrading.Identity.Api"
RUN dotnet build "AutoGrading.Identity.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AutoGrading.Identity.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AutoGrading.Identity.Api.dll"]
