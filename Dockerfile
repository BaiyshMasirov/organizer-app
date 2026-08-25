FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY organaizer/organaizer.csproj organaizer/
RUN dotnet restore organaizer/organaizer.csproj
COPY . .
RUN dotnet publish organaizer/organaizer.csproj -c Release -o /app/publish --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "organaizer.dll"]
