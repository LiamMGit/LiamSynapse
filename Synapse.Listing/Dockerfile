FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Synapse.Listing.csproj", "./"]
RUN dotnet restore "Synapse.Listing.csproj"
COPY . .
WORKDIR "/src/"
RUN dotnet build "Synapse.Listing.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Synapse.Listing.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Synapse.Listing.dll"]
