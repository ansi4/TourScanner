FROM microsoft/dotnet:2.2-aspnetcore-runtime AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM microsoft/dotnet:2.2-sdk AS build
WORKDIR /src
COPY ["CheckAndSend/CheckAndSend.fsproj", "CheckAndSend/"]
RUN dotnet restore "CheckAndSend/CheckAndSend.fsproj"
COPY . .
WORKDIR "/src/CheckAndSend"
RUN dotnet build "CheckAndSend.fsproj" -c Release -o /app

FROM build AS publish
RUN dotnet publish "CheckAndSend.fsproj" -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "CheckAndSend.dll"]