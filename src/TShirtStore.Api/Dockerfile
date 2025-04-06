FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /source

# Define ARG for project path
ARG API_PROJ_PATH=src/TShirtStore.Api/TShirtStore.Api.csproj
ARG SHARED_PROJ_PATH=src/TShirtStore.Shared/TShirtStore.Shared.csproj

# Copy csproj files first for layer caching
COPY $API_PROJ_PATH ./src/TShirtStore.Api/
COPY $SHARED_PROJ_PATH ./src/TShirtStore.Shared/
# Optional: Copy Directory.Build.props if you have one at the root

# Restore only the main project and its dependencies
RUN dotnet restore ./src/TShirtStore.Api/ # Restore the API project specifically

# Copy the rest of the source code
# Consider using .dockerignore to exclude unnecessary files (like tests, .git etc.)
COPY . .

# Publish the application
WORKDIR /source/src/TShirtStore.Api
RUN dotnet publish -c Release -o /app --no-restore

# Final stage/image
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app ./

ENTRYPOINT ["dotnet", "TShirtStore.Api.dll"]