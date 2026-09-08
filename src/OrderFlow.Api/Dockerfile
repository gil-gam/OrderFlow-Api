FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY nuget.config ./
COPY src/OrderFlow.Domain/*.csproj src/OrderFlow.Domain/
COPY src/OrderFlow.Application/*.csproj src/OrderFlow.Application/
COPY src/OrderFlow.Infrastructure/*.csproj src/OrderFlow.Infrastructure/
COPY src/OrderFlow.Api/*.csproj src/OrderFlow.Api/
RUN dotnet restore src/OrderFlow.Api/OrderFlow.Api.csproj

COPY . .
RUN dotnet publish src/OrderFlow.Api/OrderFlow.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Docker

ENTRYPOINT ["dotnet", "OrderFlow.Api.dll"]