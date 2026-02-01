FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Install curl for health checks and ffmpeg for video/audio processing
RUN apt-get update && apt-get install -y curl ffmpeg && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["T4L.VideoSearch.Api.csproj", "./"]
RUN dotnet restore "T4L.VideoSearch.Api.csproj"
COPY . .
RUN dotnet build "T4L.VideoSearch.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "T4L.VideoSearch.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "T4L.VideoSearch.Api.dll"]
