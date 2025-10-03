FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Connectly/Connectly.csproj", "Connectly/"]
RUN dotnet restore "Connectly/Connectly.csproj"
COPY . .
WORKDIR "/src/Connectly"
RUN dotnet build "./Connectly.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Connectly.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV CORECLR_ENABLE_PROFILING=1 \
CORECLR_PROFILER={36032161-FFC0-4B61-B559-F6C5D41BAE5A} \
NEW_RELIC_LICENSE_KEY=29f63e35cedf7e7a53267548d7c6983fFFFFNRAL\
NEW_RELIC_APP_NAME="MOIT-153 Connectly"
ENTRYPOINT ["dotnet", "Connectly.dll"]
