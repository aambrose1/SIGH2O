FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY app.csproj .
RUN dotnet restore app.csproj

COPY . .
RUN dotnet publish app.csproj -c Release -o /app/out

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/out .
ENTRYPOINT ["dotnet", "app.dll"]