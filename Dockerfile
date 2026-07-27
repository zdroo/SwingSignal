# Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, on project files only, so the layer caches across source edits.
# Directory.Build.props sets the TargetFramework for every project — it must be
# present before restore or the projects won't evaluate.
COPY Directory.Build.props ./
COPY RegimeDeck.Api/RegimeDeck.Api.csproj RegimeDeck.Api/
COPY RegimeDeck.Application/RegimeDeck.Application.csproj RegimeDeck.Application/
COPY RegimeDeck.Contracts/RegimeDeck.Contracts.csproj RegimeDeck.Contracts/
COPY RegimeDeck.Domain/RegimeDeck.Domain.csproj RegimeDeck.Domain/
COPY RegimeDeck.Infrastructure/RegimeDeck.Infrastructure.csproj RegimeDeck.Infrastructure/
RUN dotnet restore RegimeDeck.Api/RegimeDeck.Api.csproj

COPY . .
RUN dotnet publish RegimeDeck.Api/RegimeDeck.Api.csproj -c Release -o /app --no-restore

# Run
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Hosts route to whatever $PORT they hand us; default to 8080 when unset.
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app .

# Non-root: nothing here writes to disk, and the base image ships this user.
USER $APP_UID
ENTRYPOINT ["dotnet", "RegimeDeck.Api.dll"]
