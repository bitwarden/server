###############################################
#                 Build stage                 #
###############################################
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /source

# Copy solution and project files for dependency restoration
COPY bitwarden-server.sln ./
COPY global.json ./
COPY Directory.Build.props ./
COPY util/DbSeederUtility/DbSeederUtility.csproj ./util/DbSeederUtility/
COPY util/Seeder/Seeder.csproj ./util/Seeder/
COPY util/RustSdk/RustSdk.csproj ./util/RustSdk/
COPY src/Core/Core.csproj ./src/Core/
COPY src/Infrastructure.Dapper/Infrastructure.Dapper.csproj ./src/Infrastructure.Dapper/
COPY src/Infrastructure.EntityFramework/Infrastructure.EntityFramework.csproj ./src/Infrastructure.EntityFramework/
COPY src/SharedWeb/SharedWeb.csproj ./src/SharedWeb/

# Install Rust toolchain for building RustSdk
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    build-essential \
    && curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh -s -- -y --default-toolchain stable --profile minimal \
    && rm -rf /var/lib/apt/lists/*

# Set Cargo environment
ENV PATH="/root/.cargo/bin:${PATH}"
ENV CARGO_HOME="/root/.cargo"

# Restore dependencies
RUN dotnet restore util/DbSeederUtility/DbSeederUtility.csproj -r linux-x64

# Copy all source code
COPY . .

# Build and publish the application
WORKDIR /source/util/DbSeederUtility
RUN dotnet publish -c Release -o /app \
    --no-restore \
    --runtime linux-x64 \
    --self-contained true \
    /p:PublishSingleFile=true \
    /p:PublishTrimmed=false \
    /p:EnableCompressionInSingleFile=true

###############################################
#                 Runtime stage               #
###############################################
FROM mcr.microsoft.com/dotnet/aspnet:8.0

LABEL com.bitwarden.product="bitwarden"

# Set working directory
WORKDIR /app

# Install runtime dependencies for database drivers and Python for migration monitoring
RUN apt-get update && apt-get install -y --no-install-recommends \
    # Required for SSH tunneling (if needed)
    openssh-client \
    # ICU libraries for globalization support
    libicu-dev \
    # Python3 and pip for migration monitoring script
    python3 \
    python3-pip \
    python3-venv \
    # SQLite3 for direct database access in monitoring
    sqlite3 \
    && rm -rf /var/lib/apt/lists/*

# Install Python packages for migration monitoring
RUN pip3 install --no-cache-dir --break-system-packages \
    psycopg2-binary \
    mysql-connector-python \
    pymssql

# Copy published application from build stage
COPY --from=build /app .

# Create directories for runtime
RUN mkdir -p /app/exports /app/logs

# Set environment variables
ENV DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    ASPNETCORE_ENVIRONMENT=Production

# Make the executable file executable (in case permissions were lost)
RUN chmod +x ./DbSeeder || true

# Default command shows help
ENTRYPOINT ["./DbSeeder"]
CMD ["--help"]
