FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/Shared/Shared.Contracts/Shared.Contracts.csproj src/Shared/Shared.Contracts/
COPY src/Shared/Shared.Messaging/Shared.Messaging.csproj src/Shared/Shared.Messaging/
COPY src/Services/NotificationService/NotificationService.API/NotificationService.API.csproj src/Services/NotificationService/NotificationService.API/

RUN dotnet restore src/Services/NotificationService/NotificationService.API/NotificationService.API.csproj

COPY src/Shared/ src/Shared/
COPY src/Services/NotificationService/ src/Services/NotificationService/

WORKDIR /src/src/Services/NotificationService/NotificationService.API
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "NotificationService.API.dll"]
