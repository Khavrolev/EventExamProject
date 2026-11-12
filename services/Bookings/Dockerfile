FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["shared/Contracts/Contracts.csproj", "shared/Contracts/"]
COPY ["services/Bookings/Bookings.Domain/Bookings.Domain.csproj", "services/Bookings/Bookings.Domain/"]
COPY ["services/Bookings/Bookings.Application/Bookings.Application.csproj", "services/Bookings/Bookings.Application/"]
COPY ["services/Bookings/Bookings.Infrastructure/Bookings.Infrastructure.csproj", "services/Bookings/Bookings.Infrastructure/"]
COPY ["services/Bookings/Bookings.Presentation/Bookings.Presentation.csproj", "services/Bookings/Bookings.Presentation/"]

RUN dotnet restore "services/Bookings/Bookings.Presentation/Bookings.Presentation.csproj"

COPY shared/Contracts/ shared/Contracts/
COPY services/Bookings/ services/Bookings/

RUN dotnet publish "services/Bookings/Bookings.Presentation/Bookings.Presentation.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:5003
EXPOSE 5003
ENTRYPOINT ["dotnet", "Bookings.Presentation.dll"]
