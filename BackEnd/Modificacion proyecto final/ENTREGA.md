# Entrega del proyecto FuelControl

## Contenido

1. Código fuente completo ASP.NET Core 8.
2. Web administrativa responsive y PWA para Android.
3. API REST documentada con Swagger.
4. Modelo Entity Framework Core y script `database.sql` para SQL Server.
5. Generación de QR y ticket PDF.
6. Validación del QR, consumo único y despacho transaccional.
7. Inventario, reportes, cierres y auditoría.
8. Dockerfile, Docker Compose e instrucciones de ejecución.
9. Matriz de cumplimiento del SRS.

## Demostración recomendada ante el profesor

- Mostrar el login y explicar los cinco roles.
- Crear una solicitud y aprobarla.
- Descargar el ticket PDF y enseñar el UUID, correlativo y QR.
- Abrir la PWA desde un Android, escanear y validar el QR.
- Confirmar el despacho y demostrar el descuento inmediato del inventario.
- Intentar escanear el mismo QR otra vez para demostrar que no se reutiliza.
- Mostrar el dashboard, la auditoría y exportar el reporte en Excel/PDF.

## Pendientes de infraestructura institucional

El código deja preparada la cola de notificaciones, pero el envío real requiere las credenciales SMTP y del gateway SMS de la institución. TLS 1.3 y cifrado TDE/AES-256 se activan en el servidor de despliegue, porque no son propiedades que se puedan simular desde el código de la aplicación.
