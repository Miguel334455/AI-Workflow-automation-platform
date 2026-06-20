FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files first for layer caching
COPY src/Shared/Shared.Contracts/Shared.Contracts.csproj src/Shared/Shared.Contracts/
COPY src/Shared/Shared.Messaging/Shared.Messaging.csproj src/Shared/Shared.Messaging/
COPY src/Services/WorkflowService/WorkflowService.API/WorkflowService.API.csproj src/Services/WorkflowService/WorkflowService.API/

RUN dotnet restore src/Services/WorkflowService/WorkflowService.API/WorkflowService.API.csproj

# Copy everything else and build
COPY src/Shared/ src/Shared/
COPY src/Services/WorkflowService/ src/Services/WorkflowService/

WORKDIR /src/src/Services/WorkflowService/WorkflowService.API
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "WorkflowService.API.dll"]
