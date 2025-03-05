FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5276

ENV ASPNETCORE_URLS=http://+:5276

USER app
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG configuration=Release
WORKDIR /src
COPY ["GithubStarTracker.API/GithubStarTracker.API.csproj", "GithubStarTracker.API/"]
RUN dotnet restore "GithubStarTracker.API/GithubStarTracker.API.csproj"
COPY . .
WORKDIR "/src/GithubStarTracker.API"
RUN dotnet build "GithubStarTracker.API.csproj" -c $configuration -o /app/build

FROM build AS publish
ARG configuration=Release
RUN dotnet publish "GithubStarTracker.API.csproj" -c $configuration -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GithubStarTracker.API.dll"]
