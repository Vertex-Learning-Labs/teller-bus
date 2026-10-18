# Build from the repository root:
# docker build -f src/MockHost/Dockerfile -t mock-host .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/MockHost/MockHost.csproj src/MockHost/
RUN dotnet restore src/MockHost/MockHost.csproj
COPY src/MockHost/ src/MockHost/
RUN dotnet publish src/MockHost/MockHost.csproj -c Release -o /app /p:UseAppHost=false

# Alpine ships busybox wget, which the compose health check uses.
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "MockHost.dll"]
