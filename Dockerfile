# ================================
# Build stage
# ================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Копіюємо проект і відновлюємо залежності
COPY DeBillPay_Backend.csproj .
RUN dotnet restore

# Копіюємо все інше та збираємо
COPY . .
RUN dotnet publish "DeBillPay_Backend.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ================================
# Runtime stage
# ================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Копіюємо опубліковані файли з попереднього етапу
COPY --from=build /app/publish .

# Встановлюємо порт для ASP.NET
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Запускаємо додаток
ENTRYPOINT ["dotnet", "DeBillPay_Backend.dll"]
