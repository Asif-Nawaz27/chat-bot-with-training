# Build from the repository root, e.g.:
#   docker build -f MiniGptChat.Api/Dockerfile -t minigptchat-api .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY MiniGptChat.Api/MiniGptChat.Api.csproj MiniGptChat.Api/
COPY MiniGptChat/MiniGptChat.csproj MiniGptChat/
RUN dotnet restore MiniGptChat.Api/MiniGptChat.Api.csproj -r linux-x64

COPY MiniGptChat.Api/ MiniGptChat.Api/
COPY MiniGptChat/ MiniGptChat/
RUN dotnet publish MiniGptChat.Api/MiniGptChat.Api.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained false \
    --no-restore \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "MiniGptChat.Api.dll"]
