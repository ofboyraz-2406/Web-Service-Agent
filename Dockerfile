FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base 
WORKDIR /app 
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build 
WORKDIR /src 
COPY . . 
RUN dotnet restore "dami.api/dummyrest.api.csproj" 
RUN dotnet build "dami.api/dummyrest.api.csproj" -c Release -o /app/build 
RUN dotnet publish "dami.api/dummyrest.api.csproj" -c Release -o /app/publish 
FROM base AS final 
WORKDIR /app 
COPY --from=build /app/publish . 
ENTRYPOINT ["dotnet", "dummyrest.api.dll"] 
