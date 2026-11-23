# ============================================================
# سامانه یکپارچه منابع انسانی — ASP.NET Core 8 (فاز ۱۹)
# ساخت:  docker build -f aspnet/Dockerfile -t hr-web aspnet/
# اجرا:  docker run -p 3000:8080 -e Database__Provider=SqlServer \
#          -e ConnectionStrings__SqlServer="Server=host;Database=Hr;User Id=sa;Password=...;TrustServerCertificate=true" \
#          hr-web
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY HrAttendance.Web/HrAttendance.Web.csproj HrAttendance.Web/
RUN dotnet restore HrAttendance.Web/HrAttendance.Web.csproj
COPY HrAttendance.Web/ HrAttendance.Web/
RUN dotnet publish HrAttendance.Web/HrAttendance.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
# فونت فارسی وزیرمتن داخل ایمیج کپی می‌شود (خوداتکا)
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    Security__AllowEmbedding=false \
    Security__EnableHsts=true
EXPOSE 8080
# logs برای Serilog (فاز ۱۹) — mount بهتر است: -v hr-logs:/app/logs
VOLUME ["/app/logs"]
ENTRYPOINT ["dotnet", "HrAttendance.Web.dll"]
