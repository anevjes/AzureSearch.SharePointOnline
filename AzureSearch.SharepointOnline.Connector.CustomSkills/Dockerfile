FROM mcr.microsoft.com/dotnet/core/aspnet:2.2-stretch-slim AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/core/sdk:2.2-stretch AS build
WORKDIR /src
COPY ["BishopBlobCustomSkill/BishopBlobCustomSkill.csproj", "BishopBlobCustomSkill/"]
RUN dotnet restore "BishopBlobCustomSkill/BishopBlobCustomSkill.csproj"
COPY . .
WORKDIR "/src/BishopBlobCustomSkill"
RUN dotnet build "BishopBlobCustomSkill.csproj" -c Release -o /app

FROM build AS publish
RUN dotnet publish "BishopBlobCustomSkill.csproj" -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "BishopBlobCustomSkill.dll"]