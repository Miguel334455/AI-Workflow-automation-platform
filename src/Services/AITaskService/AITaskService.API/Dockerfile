FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/Shared/Shared.Contracts/Shared.Contracts.csproj src/Shared/Shared.Contracts/
COPY src/Shared/Shared.Messaging/Shared.Messaging.csproj src/Shared/Shared.Messaging/
COPY src/Services/AITaskService/AITaskService.API/AITaskService.API.csproj src/Services/AITaskService/AITaskService.API/

RUN dotnet restore src/Services/AITaskService/AITaskService.API/AITaskService.API.csproj

COPY src/Shared/ src/Shared/
COPY src/Services/AITaskService/ src/Services/AITaskService/

WORKDIR /src/src/Services/AITaskService/AITaskService.API
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "AITaskService.API.dll"]
