FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/Shared/Shared.Contracts/Shared.Contracts.csproj src/Shared/Shared.Contracts/
COPY src/Shared/Shared.Messaging/Shared.Messaging.csproj src/Shared/Shared.Messaging/
COPY src/Services/TriggerService/TriggerService.API/TriggerService.API.csproj src/Services/TriggerService/TriggerService.API/

RUN dotnet restore src/Services/TriggerService/TriggerService.API/TriggerService.API.csproj

COPY src/Shared/ src/Shared/
COPY src/Services/TriggerService/ src/Services/TriggerService/

WORKDIR /src/src/Services/TriggerService/TriggerService.API
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "TriggerService.API.dll"]
