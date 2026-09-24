@echo off
title FuelControl
where dotnet >nul 2>nul
if errorlevel 1 (
  echo No se encontro .NET 8 SDK. Instalelo desde https://dotnet.microsoft.com/download/dotnet/8.0
  pause
  exit /b 1
)
echo Restaurando paquetes...
dotnet restore FuelTickets.csproj
if errorlevel 1 goto error
echo Iniciando FuelControl...
echo Abra en el navegador la direccion HTTPS que aparezca debajo.
dotnet run --project FuelTickets.csproj
exit /b 0
:error
echo No fue posible restaurar o compilar el proyecto.
pause
exit /b 1
