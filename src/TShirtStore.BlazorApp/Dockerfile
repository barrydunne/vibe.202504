FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /source

# Install wasm-tools workload recommended for Blazor WASM builds
# --skip-manifest-update prevents modifying the SDK manifest globally (safer in CI/build environments)
RUN dotnet workload install wasm-tools --skip-manifest-update

# Install Python for Emscripten (dependency of wasm-tools AOT)
# Update package lists and install python3 (common name)
RUN apt-get update && \
    apt-get install -y --no-install-recommends python3 && \
    rm -rf /var/lib/apt/lists/*

# Define ARG for project path
ARG BLZ_PROJ_PATH=src/TShirtStore.BlazorApp/TShirtStore.BlazorApp.csproj
ARG SHARED_PROJ_PATH=src/TShirtStore.Shared/TShirtStore.Shared.csproj

# Copy csproj files first for layer caching
COPY $BLZ_PROJ_PATH ./src/TShirtStore.BlazorApp/
COPY $SHARED_PROJ_PATH ./src/TShirtStore.Shared/
# Optional: Copy Directory.Build.props if you have one at the root

# Restore only the main project and its dependencies
RUN dotnet clean ./src/TShirtStore.BlazorApp/
RUN dotnet restore ./src/TShirtStore.BlazorApp/ # Restore the Blazor project specifically

# Copy the rest of the source code
# Consider using .dockerignore
COPY . .

# Publish the Blazor WASM app
WORKDIR /source/src/TShirtStore.BlazorApp
RUN dotnet publish -c Release -o /app/publish --no-restore

# --- Nginx Runtime Stage for Blazor WASM ---
FROM nginx:alpine AS final
WORKDIR /usr/share/nginx/html
# Copy built Blazor app files
COPY --from=build /app/publish/wwwroot .
COPY src/TShirtStore.BlazorApp/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80