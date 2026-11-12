FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["shared/Contracts/Contracts.csproj", "shared/Contracts/"]
COPY ["services/Events/Events.Domain/Events.Domain.csproj", "services/Events/Events.Domain/"]
COPY ["services/Events/Events.Application/Events.Application.csproj", "services/Events/Events.Application/"]
COPY ["services/Events/Events.Infrastructure/Events.Infrastructure.csproj", "services/Events/Events.Infrastructure/"]
COPY ["services/Events/Events.Presentation/Events.Presentation.csproj", "services/Events/Events.Presentation/"]

RUN dotnet restore "services/Events/Events.Presentation/Events.Presentation.csproj"

COPY shared/Contracts/ shared/Contracts/
COPY services/Events/ services/Events/

RUN dotnet publish "services/Events/Events.Presentation/Events.Presentation.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:5002
EXPOSE 5002
ENTRYPOINT ["dotnet", "Events.Presentation.dll"]
