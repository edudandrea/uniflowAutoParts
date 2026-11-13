
# Etapa 1 - Compilação
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY ["UniflowAutoParts.Api.csproj", "./"]

RUN dotnet restore "UniflowAutoParts.Api.csproj"

COPY . .

RUN dotnet publish "UniflowAutoParts.Api.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore

# Etapa 2 - Execução
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# SEFAZ/RS uses SERPRO SSLv1 under ICP-Brasil v10, absent from the default trust store.
# Install only the official public root; preserve normal TLS chain and hostname validation.
RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates openssl \
    && rm -rf /var/lib/apt/lists/*

COPY certificates/icp-brasil-v10.crt /usr/local/share/ca-certificates/icp-brasil-v10.crt
RUN openssl x509 -in /usr/local/share/ca-certificates/icp-brasil-v10.crt \
        -outform DER -out /tmp/icp-brasil-v10.der \
    && echo "6e0bff069a26994c15de2c4888cc54af84882e5495b7fbf66be9ccffec7489f6  /tmp/icp-brasil-v10.der" | sha256sum --check --strict \
    && openssl verify -check_ss_sig \
        -CAfile /usr/local/share/ca-certificates/icp-brasil-v10.crt \
        /usr/local/share/ca-certificates/icp-brasil-v10.crt \
    && update-ca-certificates \
    && rm /tmp/icp-brasil-v10.der


WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "UniflowAutoParts.Api.dll"]
