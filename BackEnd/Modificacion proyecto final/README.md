# FuelControl

Plataforma web y PWA para gestionar solicitudes, tickets digitales QR y el inventario de combustible. Está construida con ASP.NET Core 8, Web API, Entity Framework Core y SQL Server.

## Funciones incluidas

- Inicio de sesión JWT y autorización por roles: administrador, supervisor, despachador, auditor y consulta.
- Usuarios, departamentos, empleados, vehículos y tanques.
- Solicitudes y aprobación con secuencia `COM-AÑO-000001`.
- Ticket PDF y QR firmado con HMAC-SHA-256, UUID, nonce, expiración y validación de uso único.
- PWA responsive con lectura de QR por la cámara en Chrome/Android.
- Despacho transaccional: consume el ticket y descuenta el inventario en una misma transacción.
- Recepciones, ajustes, transferencias, mermas e historial de inventario.
- Dashboard, cierres diarios, auditoría con usuario/IP y reportes CSV/Excel.
- Cola de notificaciones para correo y SMS, lista para conectar las credenciales del proveedor.
- Swagger en `/swagger`.

## Ejecución rápida con Docker

Requisitos: Docker Desktop.

```bash
docker compose up --build
```

Abra `http://localhost:8080`.

Credenciales iniciales:

- Usuario: `admin`
- Contraseña: `Admin123!`

El sistema crea la base, el administrador inicial y la configuración institucional la primera vez. Los catálogos operativos se registran desde la aplicación. Cambie las contraseñas y las claves de `docker-compose.yml` antes de una entrega pública o producción.

## Ejecución con Visual Studio 2022

1. Instale .NET 8 SDK y SQL Server Express/LocalDB.
2. Abra `FuelTickets.csproj`.
3. Restaure los paquetes NuGet.
4. Compruebe la conexión `DefaultConnection` en `appsettings.json`.
5. Ejecute con HTTPS. La base se crea automáticamente.

Para probar la cámara desde un teléfono, la página debe servirse por HTTPS. Puede abrir el sistema en Chrome Android, aceptar el permiso de cámara y usar **Despacho móvil**.

## Flujo de demostración

1. Inicie sesión como administrador.
2. En **Catálogos**, registre departamentos, empleados, vehículos y tanques reales.
3. En **Solicitudes**, cree una usando los registros creados.
4. Pulse **Aprobar**. Se crea un ticket único.
5. En **Tickets QR**, descargue el PDF.
6. Abra **Despacho móvil**, escanee el QR o pegue el contenido codificado.
7. Valide y confirme el despacho.
8. Compruebe el descuento en **Inventario**, el consumo en el dashboard y el evento en **Auditoría**.
9. Exporte el reporte desde **Reportes**.

## Seguridad y producción

- No mantenga las claves de demostración. Use variables de entorno o un gestor de secretos.
- Termine TLS 1.3 en IIS, Nginx o el balanceador de producción.
- Habilite Transparent Data Encryption de SQL Server para AES-256 en reposo.
- Configure MFA en el proveedor de identidad si se despliega institucionalmente.
- La cola `Notifications` registra correo/SMS. El envío real necesita SMTP y/o el gateway SMS contratado por la institución.
- Realice copias de seguridad y aplique una política de retención para auditoría.

## Archivos importantes

- `Program.cs`: API, seguridad, reglas y endpoints.
- `Models/Entities.cs`: modelo relacional.
- `Data/AppDbContext.cs`: Entity Framework y restricciones.
- `Services/SecurityServices.cs`: JWT y firma/validación QR.
- `Services/DocumentServices.cs`: QR, PDF y Excel.
- `wwwroot/`: aplicación web/PWA.
- `database.sql`: esquema SQL Server de referencia.
- `MATRIZ_REQUISITOS.md`: correspondencia entre SRS e implementación.
