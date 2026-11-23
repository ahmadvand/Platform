; ==============================================================================
; فاز ۹۲ — بستهٔ نصب تک‌فایلی (تجربهٔ MSI مانند) — درخواست کاربر:
;   «نصب راحت که خودش IIS و ASP.NET و سایت را نصب کند و در آخر نرم‌افزار بالا بیاید»
; اجرای نهایی روی ویندوز: HR-Platform-Setup.exe
;   ۱) پیش‌نیازها: IIS (Windows Features) + NET 8 Hosting Bundle (نصب خاموش از prereq)
;   ۲) کپی فایل‌های برنامه به C:\HrPlatform
;   ۳) اجرای Install-HRPlatform.ps1 (ساخت اپ‌پول + سایت IIS + فایروال + وظیفهٔ به‌روزرسان)
;   ۴) اجرای سایت + باز شدن مرورگر روی ویزارد راه‌اندازی اولیه
; کامپایل این فایل با Inno Setup 6 (ابزار رایگان: https://jrsoftware.org/isdl.php):
;   ISCC.exe HR-Platform-Setup.iss
; payload لازم (ساخت در لینوکس با installer/build-installer.sh):
;   installer/HR-Platform-Setup-payload/app/**   ← خروجی dotnet publish win-x64
;   installer/HR-Platform-Setup-payload/prereq/dotnet-hosting-8.0.x-win.exe  (اختیاری — نصب آفلاین)
; ==============================================================================

#define MyAppName "HR Platform"
#define MyAppVersion GetEnv("HRP_VERSION")
#define MyAppPublisher "HR Platform"
#define MyAppExeName "Install-HRPlatform.ps1"

[Setup]
AppId={{7E1D5E90-8B4A-4C7A-9F3D-1A2B3C4D5E6F}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName=C:\HrPlatform
DisableProgramGroupPage=yes
OutputDir=dist
OutputBaseFilename=HR-Platform-Setup-{#MyAppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
Uninstallable=no
ShowLanguageDetector=yes

[Languages]
Name: "fa"; MessagesFile: "compiler:Languages\Default.isl"

[Files]
; فایل‌های برنامه
Source: "HR-Platform-Setup-payload\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; نصب‌کننده و به‌روزرسان
Source: "Install-HRPlatform.ps1"; DestDir: "{app}\installer"; Flags: ignoreversion
Source: "update-runner.ps1"; DestDir: "{app}\installer"; Flags: ignoreversion
; پیش‌نیاز آفلاین (اختیاری — اگر در payload باشد)
Source: "HR-Platform-Setup-payload\prereq\dotnet-hosting-8*.exe"; DestDir: "{app}\installer\prereq"; Flags: ignoreversion skipifsourcedoesntexist

[Run]
; پس از کپی فایل‌ها: نصب‌کنندهٔ اصلی (IIS + پول + سایت + اجرا)
Filename: "powershell.exe"; \
  Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\installer\Install-HRPlatform.ps1"" -InstallDir ""{app}"" -AppVersion ""{#MyAppVersion}"""; \
  Flags: runhidden waitforscriptdone; StatusMsg: "Installing IIS / .NET 8 / Site..."
