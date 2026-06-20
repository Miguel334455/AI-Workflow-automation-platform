FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/Shared/Shared.Contracts/Shared.Contracts.csproj src/Shared/Shared.Contracts/
COPY src/Shared/Shared.Messaging/Shared.Messaging.csproj src/Shared/Shared.Messaging/
COPY src/Services/ExecutionService/ExecutionService.API/ExecutionService.API.csproj src/Services/ExecutionService/ExecutionService.API/

RUN dotnet restore src/Services/ExecutionService/ExecutionService.API/ExecutionService.API.csproj

COPY src/Shared/ src/Shared/
COPY src/Services/ExecutionService/ src/Services/ExecutionService/

WORKDIR /src/src/Services/ExecutionService/ExecutionService.API
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "ExecutionService.API.dll"]
