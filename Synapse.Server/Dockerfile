FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Synapse.Server.csproj", "./"]
RUN dotnet restore "Synapse.Server.csproj"
COPY . .
WORKDIR "/src/"
RUN dotnet build "Synapse.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Synapse.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Synapse.Server.dll"]
