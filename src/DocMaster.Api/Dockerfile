FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY ["proto/storage.proto", "proto/"]
COPY ["src/DocMaster.Api/DocMaster.Api.csproj", "src/DocMaster.Api/"]
RUN dotnet restore "src/DocMaster.Api/DocMaster.Api.csproj"
COPY . .
WORKDIR "/src/src/DocMaster.Api"
RUN dotnet build "DocMaster.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DocMaster.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DocMaster.Api.dll"]
