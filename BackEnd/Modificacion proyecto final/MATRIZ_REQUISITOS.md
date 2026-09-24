# Matriz de cumplimiento del SRS

| Requisito | Implementación | Estado |
|---|---|---|
| RF-01 Usuarios y roles | API `/api/users`, JWT y políticas RBAC | Implementado |
| RF-02 a RF-04 Catálogos | Empleados, vehículos y departamentos en API/PWA | Implementado |
| RF-05 Solicitudes | Creación manual, campos y estado | Implementado; recurrencia almacena regla para calendarización institucional |
| RF-06 a RF-08 Tickets/QR/secuencia | UUID, correlativo anual, PDF, QR HMAC-SHA-256 y nonce | Implementado |
| RF-09 Correo/SMS | Cola `Notifications` al emitir; requiere credenciales SMTP/SMS | Integración preparada |
| RF-10 Estado | Pendiente, vencido, consumido y anulado en modelo y validación | Implementado |
| RF-11 Asignaciones | Solicitudes manuales y campos de recurrencia | Parcial configurable |
| RF-12 y RF-13 Despacho/PWA | Cámara, validación en línea, registro y sincronización inmediata | Implementado |
| RF-14 a RF-17 Inventario | Recepción, despacho, merma, ajustes, transferencias e historial | Implementado |
| RF-18 Cierre diario | Cálculo de movimientos, existencia física y diferencia | Implementado |
| RF-19 y RF-20 Reportes | Filtros base, CSV y Excel; ticket/cierre en PDF | Implementado |
| RF-21 Trazabilidad | Auditoría con usuario, UTC, acción, entidad e IP | Implementado |
| RF-22 Dashboard | Existencia, consumo, tickets, nivel crítico y departamentos | Implementado |
| RF-23 Alertas | Registros de notificación; envío externo requiere proveedor | Integración preparada |
| RF-24 API REST | Endpoints REST y Swagger | Implementado |
| RS-01 y RS-02 | JWT, sesión de 8 horas y RBAC | Implementado; MFA opcional externo |
| RS-03 | HTTPS/TLS del host; SQL Server TDE recomendado | Configuración de despliegue |
| RS-04 | Firma HMAC-SHA-256, hash, nonce y token codificado | Implementado |
| RS-05 | JWT Bearer; compatible con OAuth 2.0/OIDC externo | Implementado para JWT |
| RS-06 | Tabla de auditoría sin endpoints de modificación | Implementado |

Los puntos marcados como “integración preparada” dependen de cuentas externas que el SRS no suministra: servidor SMTP, proveedor de SMS y, si se desea, proveedor MFA/OAuth institucional.
