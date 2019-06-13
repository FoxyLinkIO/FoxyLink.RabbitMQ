FROM mcr.microsoft.com/dotnet/core/sdk:3.0 AS build-env
WORKDIR /app

# Copy csproj and restore as distinct layers
#COPY src/FoxyLink.Core/*.csproj ./
#RUN dotnet restore

# Copy everything and build
COPY . ./
RUN dotnet restore
RUN dotnet publish -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/core/aspnet:3.0
WORKDIR /app
COPY --from=build-env /app/out .
#COPY --from=build-env /app/config ./config
ENTRYPOINT ["dotnet", "FoxyLink.Core.dll"]

