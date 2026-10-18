# Build from the repository root:
# docker build -f src/Teller.Api/Dockerfile -t teller-api .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/Teller.Domain/Teller.Domain.csproj src/Teller.Domain/
COPY src/Teller.Application/Teller.Application.csproj src/Teller.Application/
COPY src/Teller.Infrastructure/Teller.Infrastructure.csproj src/Teller.Infrastructure/
COPY src/Teller.Api/Teller.Api.csproj src/Teller.Api/
RUN dotnet restore src/Teller.Api/Teller.Api.csproj
COPY src/Teller.Domain/ src/Teller.Domain/
COPY src/Teller.Application/ src/Teller.Application/
COPY src/Teller.Infrastructure/ src/Teller.Infrastructure/
COPY src/Teller.Api/ src/Teller.Api/
RUN dotnet publish src/Teller.Api/Teller.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false

# Alpine ships busybox wget, which the compose health check uses.
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Teller.Api.dll"]
