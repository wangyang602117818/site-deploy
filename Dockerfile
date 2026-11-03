FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["SiteDeploy.Document.csproj", "./"]
RUN dotnet restore "./SiteDeploy.Document.csproj"

COPY . .
RUN dotnet publish "./SiteDeploy.Document.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish ./

ENTRYPOINT ["sh", "-c", "dotnet SiteDeploy.Document.dll --urls http://0.0.0.0:${PORT:-8080}"]