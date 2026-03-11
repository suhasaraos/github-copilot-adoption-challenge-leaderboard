# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy the project file
COPY ["LeaderboardApp.csproj", "./"]

# Restore dependencies
RUN dotnet restore "LeaderboardApp.csproj"

# Copy the rest of the source code
COPY . .

# Build the application
RUN dotnet build "LeaderboardApp.csproj" -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "LeaderboardApp.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Copy the published application from the publish stage
COPY --from=publish /app/publish .

# Expose ports
EXPOSE 8080
EXPOSE 8081

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080;https://+:8081
ENV ASPNETCORE_ENVIRONMENT=Production

# Start the application
ENTRYPOINT ["dotnet", "LeaderboardApp.dll"]

LABEL org.opencontainers.image.source=https://github.com/Azure-Samples/github-copilot-adoption-challenge-leaderboard
LABEL org.opencontainers.image.description="Leaderboard App image"
LABEL org.opencontainers.image.licenses=MIT