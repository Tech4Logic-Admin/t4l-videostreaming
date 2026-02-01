FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["T4L.VideoSearch.Worker.csproj", "./"]
RUN dotnet restore "T4L.VideoSearch.Worker.csproj"
COPY . .
RUN dotnet build "T4L.VideoSearch.Worker.csproj" -c Release -o /app/build
RUN dotnet publish "T4L.VideoSearch.Worker.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated8.0
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true
COPY --from=build /app/publish /home/site/wwwroot
