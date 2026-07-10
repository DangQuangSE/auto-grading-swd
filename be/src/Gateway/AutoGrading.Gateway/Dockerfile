FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["be/Directory.Build.props", "be/"]
COPY ["be/src/BuildingBlocks/AutoGrading.Contracts/AutoGrading.Contracts.csproj", "be/src/BuildingBlocks/AutoGrading.Contracts/"]
COPY ["be/src/BuildingBlocks/AutoGrading.Common/AutoGrading.Common.csproj", "be/src/BuildingBlocks/AutoGrading.Common/"]
COPY ["be/src/Gateway/AutoGrading.Gateway/AutoGrading.Gateway.csproj", "be/src/Gateway/AutoGrading.Gateway/"]
RUN dotnet restore "be/src/Gateway/AutoGrading.Gateway/AutoGrading.Gateway.csproj"
COPY . .
WORKDIR "/src/be/src/Gateway/AutoGrading.Gateway"
RUN dotnet build "AutoGrading.Gateway.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AutoGrading.Gateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AutoGrading.Gateway.dll"]
