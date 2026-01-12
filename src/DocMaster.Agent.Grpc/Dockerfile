FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5001

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY ["proto/storage.proto", "proto/"]
COPY ["src/DocMaster.Agent.Grpc/DocMaster.Agent.Grpc.csproj", "src/DocMaster.Agent.Grpc/"]
RUN dotnet restore "src/DocMaster.Agent.Grpc/DocMaster.Agent.Grpc.csproj"
COPY . .
WORKDIR "/src/src/DocMaster.Agent.Grpc"
RUN dotnet build "DocMaster.Agent.Grpc.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DocMaster.Agent.Grpc.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create data directory
RUN mkdir -p /data

ENTRYPOINT ["dotnet", "DocMaster.Agent.Grpc.dll"]
