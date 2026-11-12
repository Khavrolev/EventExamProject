FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["shared/Contracts/Contracts.csproj", "shared/Contracts/"]
COPY ["services/Users/Users.Domain/Users.Domain.csproj", "services/Users/Users.Domain/"]
COPY ["services/Users/Users.Application/Users.Application.csproj", "services/Users/Users.Application/"]
COPY ["services/Users/Users.Infrastructure/Users.Infrastructure.csproj", "services/Users/Users.Infrastructure/"]
COPY ["services/Users/Users.Presentation/Users.Presentation.csproj", "services/Users/Users.Presentation/"]

RUN dotnet restore "services/Users/Users.Presentation/Users.Presentation.csproj"

COPY shared/Contracts/ shared/Contracts/
COPY services/Users/ services/Users/

RUN dotnet publish "services/Users/Users.Presentation/Users.Presentation.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:5001
EXPOSE 5001
ENTRYPOINT ["dotnet", "Users.Presentation.dll"]
