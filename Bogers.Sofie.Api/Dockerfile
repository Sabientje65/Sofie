FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["Bogers.Sofie.Api/Bogers.Sofie.Api.csproj", "Bogers.Sofie.Api/"]
RUN dotnet restore "Bogers.Sofie.Api/Bogers.Sofie.Api.csproj"
COPY . .
WORKDIR "/src/Bogers.Sofie.Api"
RUN dotnet build "Bogers.Sofie.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Bogers.Sofie.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Bogers.Sofie.Api.dll"]
