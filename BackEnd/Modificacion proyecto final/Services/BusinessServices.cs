using System.Security.Claims;
using System.Text.Json;
using FuelTickets.Data;
using FuelTickets.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FuelTickets.Services;

public class AuditService(AppDbContext db, IHttpContextAccessor http)
{
    public async Task WriteAsync(string action, string entity, object? entityId = null, object? details = null)
    {
        var ctx = http.HttpContext;
        int? userId = int.TryParse(ctx?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Username = ctx?.User.Identity?.Name ?? "anonymous",
            Action = action,
            Entity = entity,
            EntityId = entityId?.ToString(),
            DetailsJson = details is null ? null : JsonSerializer.Serialize(details),
            IpAddress = ctx?.Connection.RemoteIpAddress?.ToString() ?? "unknown"
        });
        await db.SaveChangesAsync();
    }
}

public class TicketService(AppDbContext db, QrSecurityService qr)
{
    // Bajo carga concurrente, dos aprobaciones simultáneas pueden calcular el mismo
    // "siguiente número" de secuencia antes de que cualquiera confirme. La transacción
    // Serializable detecta ese choque y SQL Server aborta una de las dos con un error
    // transitorio (deadlock 1205 o conflicto de instantánea 3960/3961). En vez de dejar
    // que ese error se propague como un 500 al usuario, reintentamos unas pocas veces:
    // cada intento vuelve a leer el último correlativo ya confirmado y genera uno nuevo.
    private const int MaxAttempts = 5;

    public async Task<FuelTicket> IssueAsync(FuelRequest request)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var year = DateTime.UtcNow.Year;
                var prefix = (await db.SystemSettings.FirstOrDefaultAsync(x => x.Key == "TicketPrefix"))?.Value ?? "COM";
                var last = await db.FuelTickets.Where(x => x.CreatedAtUtc.Year == year).OrderByDescending(x => x.Sequence).Select(x => x.Sequence).FirstOrDefaultAsync();
                var number = last is null ? 1 : int.Parse(last.Split('-').Last()) + 1;
                var ticket = new FuelTicket
                {
                    RequestId = request.Id,
                    Request = request,
                    Sequence = $"{prefix}-{year}-{number:000000}",
                    ExpiresAtUtc = request.ExpiresAtUtc,
                    Status = TicketStatus.Pending
                };
                var payload = qr.CreatePayload(ticket);
                ticket.PayloadHash = qr.HashPayload(payload);
                db.FuelTickets.Add(ticket);
                request.Status = RequestStatus.Approved;
                await db.SaveChangesAsync();
                await tx.CommitAsync();
                return ticket;
            }
            catch (Exception ex) when (attempt < MaxAttempts && IsTransientConflict(ex))
            {
                await tx.RollbackAsync();
                // Descarta el ticket que no llegó a confirmarse y vuelve a dejar la
                // solicitud como estaba, para que el siguiente intento parta limpio.
                db.ChangeTracker.Clear();
                request = await db.FuelRequests.FindAsync(request.Id) ?? request;
                await Task.Delay(Random.Shared.Next(25, 75) * attempt);
            }
        }
        throw new InvalidOperationException("No fue posible emitir el ticket por alta concurrencia. Intente aprobar la solicitud nuevamente.");
    }

    private static bool IsTransientConflict(Exception ex) =>
        ex switch
        {
            SqlException sql => sql.Number is 1205 or 3960 or 3961,
            DbUpdateException { InnerException: SqlException sql } => sql.Number is 1205 or 3960 or 3961,
            DbUpdateConcurrencyException => true,
            _ => false
        };

    public string Payload(FuelTicket ticket) => qr.CreatePayload(ticket);
}

public static class ClaimsExtensions
{
    public static int UserId(this ClaimsPrincipal user) => int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
