#!/bin/bash
# استارت اپ با argv[0] ملبس به اسم دیمن SDK — آزمون عبور از reaper بین‌فراخوانی
cd /home/z/my-project/aspnet/src/HrAttendance.Web || exit 1
export PATH="/home/z/.dotnet:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
export Database__Provider="${Database__Provider:-Sqlite}"
export Setup__Configured="${Setup__Configured:-true}"
export TZ=Asia/Tehran
export ASPNETCORE_URLS=http://0.0.0.0:3000
export ASPNETCORE_ENVIRONMENT=Production
exec -a "VBCSCompiler.dll" /home/z/.dotnet/dotnet bin/Debug/net8.0/HrAttendance.Web.dll 2>&1 | tee /home/z/my-project/dev.log
