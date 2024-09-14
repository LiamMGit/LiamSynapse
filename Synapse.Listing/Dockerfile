FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Synapse.Listing/Synapse.Listing.csproj", "Synapse.Listing/"]
COPY ["Synapse.Networking/Synapse.Networking.csproj", "Synapse.Networking/"]
RUN dotnet restore "Synapse.Listing/Synapse.Listing.csproj"
COPY . .
WORKDIR "/src/Synapse.Listing"
RUN dotnet build "Synapse.Listing.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Synapse.Listing.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Synapse.Listing.dll"]
