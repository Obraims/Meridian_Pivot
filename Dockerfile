FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["backend/StockSync.csproj", "backend/"]
RUN dotnet restore "backend/StockSync.csproj"
COPY . .
WORKDIR "/src/backend"
RUN dotnet build "StockSync.csproj" -c Release -o /app/build
RUN dotnet publish "StockSync.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY frontend /frontend
ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000
ENTRYPOINT ["dotnet", "StockSync.dll"]
