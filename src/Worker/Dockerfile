# for some reason, getting an error loading librdkafka in Alpine ARM64, so using Ubuntu Chiseled instead to not waste time right now trying to fix the issue :)
FROM mcr.microsoft.com/dotnet/aspnet:8.0-preview-jammy-chiseled AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0-preview-jammy AS build
WORKDIR /src
COPY ["src/Worker/Worker.csproj", "src/Worker/"]
COPY ["src/Shared/Shared.csproj", "src/Shared/"]
COPY ["nuget.config", "."]
RUN dotnet restore "src/Worker/Worker.csproj"
COPY . .
WORKDIR "/src/src/Worker"
RUN dotnet build "Worker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Worker.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Worker.dll"]
