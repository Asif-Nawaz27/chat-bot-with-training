# Build from the repository root, e.g.:
#   docker build -f ChatBot.Api/Dockerfile -t chatbot-api .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ChatBot.Api/ChatBot.Api.csproj ChatBot.Api/
COPY ChatBot.Data/ChatBot.Data.csproj ChatBot.Data/
RUN dotnet restore ChatBot.Api/ChatBot.Api.csproj -r linux-x64

COPY ChatBot.Api/ ChatBot.Api/
COPY ChatBot.Data/ ChatBot.Data/
RUN dotnet publish ChatBot.Api/ChatBot.Api.csproj \
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

ENTRYPOINT ["dotnet", "ChatBot.Api.dll"]
