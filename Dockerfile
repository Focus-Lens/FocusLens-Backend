FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet publish src/FocusLens.Api/FocusLens.Api.csproj \
    -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

CMD ["sh", "-c", "dotnet FocusLens.Api.dll --urls http://0.0.0.0:${PORT:-8080}"]
