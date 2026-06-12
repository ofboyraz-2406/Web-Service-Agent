FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base 
WORKDIR /app 
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build 
WORKDIR /src 
COPY . . 
RUN dotnet restore "Background.Service/Background.Service.csproj" 
RUN dotnet publish "Background.Service/Background.Service.csproj" -c Release -o /app/publish 
FROM base AS final 
WORKDIR /app 
COPY --from=build /app/publish . 
ENTRYPOINT ["dotnet", "Background.Service.dll"] 
