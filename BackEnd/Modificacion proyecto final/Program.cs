using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using FuelTickets.Contracts;
using FuelTickets.Data;
using FuelTickets.Models;
using FuelTickets.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<QrSecurityService>();
builder.Services.AddScoped<TicketService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"], ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
        ClockSkew = TimeSpan.FromMinutes(1)
    };
});
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Admin", p => p.RequireRole(nameof(UserRole.Administrator)))
    .AddPolicy("Manage", p => p.RequireRole(nameof(UserRole.Administrator), nameof(UserRole.Supervisor)))
    .AddPolicy("Dispatch", p => p.RequireRole(nameof(UserRole.Administrator), nameof(UserRole.Supervisor), nameof(UserRole.Dispatcher)))
    .AddPolicy("Reports", p => p.RequireRole(nameof(UserRole.Administrator), nameof(UserRole.Supervisor), nameof(UserRole.Auditor), nameof(UserRole.Viewer)));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();

await SeedAsync(app.Services);

var api = app.MapGroup("/api");

api.MapPost("/auth/login", async (LoginRequest input, AppDbContext db, TokenService tokens, AuditService audit) =>
{
    var user = await db.Users.SingleOrDefaultAsync(x => x.Username == input.Username && x.IsActive);
    if (user is null || !Passwords.Verify(user, input.Password)) return Results.Unauthorized();
    await audit.WriteAsync("LOGIN", "User", user.Id);
    return Results.Ok(new LoginResponse(tokens.Create(user), user.FullName, user.Role.ToString()));
}).AllowAnonymous();

api.MapGet("/dashboard", async (AppDbContext db) =>
{
    var now = DateTime.UtcNow;
    var monthStart = new DateTime(now.Year, now.Month, 1);
    return Results.Ok(new
    {
        inventory = await db.FuelTanks.Where(x => x.IsActive).SumAsync(x => x.CurrentGallons),
        dispatchedToday = await db.Dispatches.Where(x => x.DispatchedAtUtc.Date == now.Date).SumAsync(x => (decimal?)x.GallonsServed) ?? 0,
        dispatchedMonth = await db.Dispatches.Where(x => x.DispatchedAtUtc >= monthStart).SumAsync(x => (decimal?)x.GallonsServed) ?? 0,
        activeTickets = await db.FuelTickets.CountAsync(x => x.Status == TicketStatus.Pending && x.ExpiresAtUtc > now),
        expiredTickets = await db.FuelTickets.CountAsync(x => x.Status == TicketStatus.Expired || (x.Status == TicketStatus.Pending && x.ExpiresAtUtc <= now)),
        lowTanks = await db.FuelTanks.CountAsync(x => x.IsActive && x.CurrentGallons <= x.CriticalLevelGallons),
        byDepartment = await db.Dispatches.Where(x => x.DispatchedAtUtc >= monthStart)
            .GroupBy(x => x.Ticket!.Request!.Department!.Name).Select(g => new { name = g.Key, gallons = g.Sum(x => x.GallonsServed) }).ToListAsync(),
        recent = await db.Dispatches.OrderByDescending(x => x.DispatchedAtUtc).Take(8)
            .Select(x => new { x.Id, x.DispatchedAtUtc, x.GallonsServed, ticket = x.Ticket!.Sequence, plate = x.Ticket.Request!.Vehicle!.Plate }).ToListAsync()
    });
}).RequireAuthorization();

api.MapGet("/catalogs", async (AppDbContext db) => Results.Ok(new
{
    users = await db.Users.OrderBy(x => x.FullName).Select(x => new { x.Id, x.Username, x.FullName, x.Email, role=x.Role.ToString(), x.IsActive }).ToListAsync(),
    departments = await db.Departments.OrderBy(x => x.Name).ToListAsync(),
    employees = await db.Employees.Include(x => x.Department).OrderBy(x => x.FullName).ToListAsync(),
    vehicles = await db.Vehicles.Include(x => x.Department).OrderBy(x => x.Plate).ToListAsync(),
    tanks = await db.FuelTanks.OrderBy(x => x.Name).ToListAsync()
})).RequireAuthorization();

api.MapPost("/users", async (UserInput x, AppDbContext db, AuditService audit) =>
{
    if(!Enum.TryParse<UserRole>(x.Role,true,out var role) || string.IsNullOrWhiteSpace(x.Password) || x.Password.Length<8) return Results.BadRequest("Rol inválido o contraseña menor de 8 caracteres.");
    var item=new AppUser { Username=x.Username.Trim(),FullName=x.FullName.Trim(),Email=x.Email.Trim(),Role=role,IsActive=x.IsActive };
    item.PasswordHash=Passwords.Hash(item,x.Password); db.Add(item); await db.SaveChangesAsync(); await audit.WriteAsync("CREATE","User",item.Id,new{item.Username,item.Role});
    return Results.Ok(new{item.Id,item.Username,item.FullName,item.Email,role=item.Role.ToString(),item.IsActive});
}).RequireAuthorization("Admin");

api.MapPut("/users/{id:int}", async (int id, UserInput x, AppDbContext db, AuditService audit) =>
{
    var item=await db.Users.FindAsync(id); if(item is null)return Results.NotFound(); if(!Enum.TryParse<UserRole>(x.Role,true,out var role))return Results.BadRequest("Rol inválido.");
    item.Username=x.Username.Trim();item.FullName=x.FullName.Trim();item.Email=x.Email.Trim();item.Role=role;item.IsActive=x.IsActive;if(!string.IsNullOrWhiteSpace(x.Password))item.PasswordHash=Passwords.Hash(item,x.Password);
    await db.SaveChangesAsync();await audit.WriteAsync("UPDATE","User",id,new{item.Username,item.Role,item.IsActive});return Results.Ok();
}).RequireAuthorization("Admin");

api.MapPost("/departments", async (DepartmentInput x, AppDbContext db, AuditService audit) =>
{
    if (string.IsNullOrWhiteSpace(x.Name) || string.IsNullOrWhiteSpace(x.Code)) return Results.BadRequest("Nombre y código son obligatorios.");
    var item = new Department { Name = x.Name.Trim(), Code = x.Code.Trim().ToUpperInvariant(), IsActive = x.IsActive };
    db.Add(item); await db.SaveChangesAsync(); await audit.WriteAsync("CREATE", "Department", item.Id, item); return Results.Created($"/api/departments/{item.Id}", item);
}).RequireAuthorization("Manage");

api.MapPut("/departments/{id:int}", async (int id, DepartmentInput x, AppDbContext db, AuditService audit) =>
{
    var item = await db.Departments.FindAsync(id); if (item is null) return Results.NotFound();
    item.Name = x.Name.Trim(); item.Code = x.Code.Trim().ToUpperInvariant(); item.IsActive = x.IsActive;
    await db.SaveChangesAsync(); await audit.WriteAsync("UPDATE", "Department", id, item); return Results.Ok(item);
}).RequireAuthorization("Manage");

api.MapPost("/employees", async (EmployeeInput x, AppDbContext db, AuditService audit) =>
{
    var item = new Employee { EmployeeCode=x.EmployeeCode, FullName=x.FullName, NationalId=x.NationalId, DepartmentId=x.DepartmentId, Position=x.Position, Email=x.Email, Mobile=x.Mobile, IsActive=x.IsActive };
    db.Add(item); await db.SaveChangesAsync(); await audit.WriteAsync("CREATE", "Employee", item.Id, new { item.EmployeeCode, item.FullName }); return Results.Ok(item);
}).RequireAuthorization("Manage");

api.MapPut("/employees/{id:int}", async (int id, EmployeeInput x, AppDbContext db, AuditService audit) =>
{
    var item = await db.Employees.FindAsync(id); if (item is null) return Results.NotFound();
    item.EmployeeCode=x.EmployeeCode; item.FullName=x.FullName; item.NationalId=x.NationalId; item.DepartmentId=x.DepartmentId; item.Position=x.Position; item.Email=x.Email; item.Mobile=x.Mobile; item.IsActive=x.IsActive;
    await db.SaveChangesAsync(); await audit.WriteAsync("UPDATE", "Employee", id); return Results.Ok(item);
}).RequireAuthorization("Manage");

api.MapPost("/vehicles", async (VehicleInput x, AppDbContext db, AuditService audit) =>
{
    var item = new Vehicle { Plate=x.Plate.ToUpperInvariant(), InternalCode=x.InternalCode, Make=x.Make, Model=x.Model, Year=x.Year, Type=x.Type, DepartmentId=x.DepartmentId, TankCapacityGallons=x.TankCapacityGallons, OdometerKm=x.OdometerKm, IsActive=x.IsActive };
    db.Add(item); await db.SaveChangesAsync(); await audit.WriteAsync("CREATE", "Vehicle", item.Id, new { item.Plate, item.InternalCode }); return Results.Ok(item);
}).RequireAuthorization("Manage");

api.MapPut("/vehicles/{id:int}", async (int id, VehicleInput x, AppDbContext db, AuditService audit) =>
{
    var item = await db.Vehicles.FindAsync(id); if (item is null) return Results.NotFound();
    item.Plate=x.Plate.ToUpperInvariant(); item.InternalCode=x.InternalCode; item.Make=x.Make; item.Model=x.Model; item.Year=x.Year; item.Type=x.Type; item.DepartmentId=x.DepartmentId; item.TankCapacityGallons=x.TankCapacityGallons; item.OdometerKm=x.OdometerKm; item.IsActive=x.IsActive;
    await db.SaveChangesAsync(); await audit.WriteAsync("UPDATE", "Vehicle", id); return Results.Ok(item);
}).RequireAuthorization("Manage");

api.MapPost("/tanks", async (TankInput x, AppDbContext db, AuditService audit) =>
{
    var item = new FuelTank { Name=x.Name, FuelType=x.FuelType, CapacityGallons=x.CapacityGallons, CurrentGallons=x.CurrentGallons, CriticalLevelGallons=x.CriticalLevelGallons, IsActive=x.IsActive };
    db.Add(item); await db.SaveChangesAsync(); await audit.WriteAsync("CREATE", "FuelTank", item.Id); return Results.Ok(item);
}).RequireAuthorization("Manage");

api.MapGet("/requests", async (AppDbContext db) => await db.FuelRequests.Include(x=>x.Employee).Include(x=>x.Vehicle).Include(x=>x.Department).OrderByDescending(x=>x.RequestedAtUtc).ToListAsync()).RequireAuthorization();
api.MapPost("/requests", async (RequestInput x, ClaimsPrincipal user, AppDbContext db, AuditService audit) =>
{
    if (x.AuthorizedGallons <= 0 || x.ExpiresAtUtc <= DateTime.UtcNow) return Results.BadRequest("La cantidad debe ser positiva y la fecha futura.");
    var item = new FuelRequest { EmployeeId=x.EmployeeId, VehicleId=x.VehicleId, DepartmentId=x.DepartmentId, AuthorizedGallons=x.AuthorizedGallons, FuelType=x.FuelType, ExpiresAtUtc=x.ExpiresAtUtc.ToUniversalTime(), IsRecurring=x.IsRecurring, RecurrenceRule=x.RecurrenceRule, Notes=x.Notes, CreatedByUserId=user.UserId() };
    db.Add(item); await db.SaveChangesAsync(); await audit.WriteAsync("CREATE", "FuelRequest", item.Id); return Results.Ok(item);
}).RequireAuthorization();

api.MapPost("/requests/{id:int}/approve", async (int id, AppDbContext db, TicketService tickets, AuditService audit) =>
{
    var req = await db.FuelRequests.FindAsync(id); if (req is null) return Results.NotFound();
    if (req.Status != RequestStatus.Pending) return Results.Conflict("La solicitud ya fue procesada.");
    var ticket = await tickets.IssueAsync(req); await audit.WriteAsync("APPROVE_AND_ISSUE", "FuelTicket", ticket.Id, new { ticket.Sequence });
    var employee=await db.Employees.FindAsync(req.EmployeeId);
    if(employee is not null&&!string.IsNullOrWhiteSpace(employee.Email))db.Notifications.Add(new Notification{Channel="Email",Recipient=employee.Email,Subject=$"Ticket {ticket.Sequence}",Message="Su ticket digital de combustible fue emitido. Consulte el PDF y presente el código QR.",Status="Queued"});
    if(employee is not null&&!string.IsNullOrWhiteSpace(employee.Mobile))db.Notifications.Add(new Notification{Channel="SMS",Recipient=employee.Mobile,Subject=ticket.Sequence,Message=$"Ticket {ticket.Sequence} disponible hasta {ticket.ExpiresAtUtc:dd/MM/yyyy HH:mm}.",Status="Queued"});
    await db.SaveChangesAsync();
    return Results.Ok(new { ticket.Id, ticket.Sequence, qrPayload = tickets.Payload(ticket) });
}).RequireAuthorization("Manage");

api.MapGet("/tickets", async (AppDbContext db) =>
{
    // Ningún proceso en segundo plano recorre los tickets, así que si nadie llega a
    // escanear uno vencido, se quedaría marcado "Pending" para siempre. Antes de listar,
    // sincronizamos el estado de los que ya vencieron para que la vista y los reportes
    // reflejen la realidad sin depender de que alguien lo intente despachar.
    var now = DateTime.UtcNow;
    var justExpired = await db.FuelTickets.Where(x => x.Status == TicketStatus.Pending && x.ExpiresAtUtc <= now).ToListAsync();
    if (justExpired.Count > 0)
    {
        foreach (var t in justExpired) t.Status = TicketStatus.Expired;
        await db.SaveChangesAsync();
    }
    return await db.FuelTickets.Include(x=>x.Request)!.ThenInclude(x=>x!.Employee).Include(x=>x.Request)!.ThenInclude(x=>x!.Vehicle).OrderByDescending(x=>x.CreatedAtUtc).ToListAsync();
}).RequireAuthorization();
api.MapGet("/tickets/{id:guid}/qr", async (Guid id, AppDbContext db, TicketService tickets) =>
{
    var t = await db.FuelTickets.Include(x=>x.Request).SingleOrDefaultAsync(x=>x.Id==id); return t is null ? Results.NotFound() : Results.File(DocumentServices.QrPng(tickets.Payload(t)), "image/png", $"QR-{t.Sequence}.png");
}).RequireAuthorization();
api.MapGet("/tickets/{id:guid}/pdf", async (Guid id, AppDbContext db, TicketService tickets) =>
{
    var t = await db.FuelTickets.Include(x=>x.Request)!.ThenInclude(x=>x!.Employee).Include(x=>x.Request)!.ThenInclude(x=>x!.Vehicle).Include(x=>x.Request)!.ThenInclude(x=>x!.Department).SingleOrDefaultAsync(x=>x.Id==id);
    return t is null ? Results.NotFound() : Results.File(DocumentServices.TicketPdf(t, tickets.Payload(t)), "application/pdf", $"Ticket-{t.Sequence}.pdf");
}).RequireAuthorization();
api.MapPost("/tickets/{id:guid}/cancel", async (Guid id, AppDbContext db, AuditService audit) =>
{
    var t=await db.FuelTickets.FindAsync(id);if(t is null)return Results.NotFound();if(t.Status==TicketStatus.Consumed)return Results.Conflict("Un ticket consumido no puede anularse.");t.Status=TicketStatus.Cancelled;await db.SaveChangesAsync();await audit.WriteAsync("CANCEL","FuelTicket",id,new{t.Sequence});return Results.Ok();
}).RequireAuthorization("Manage");

api.MapPost("/tickets/validate", async (Dictionary<string,string> body, AppDbContext db, QrSecurityService qr) =>
{
    if (!body.TryGetValue("qrPayload", out var encoded) || !qr.TryValidate(encoded, out var payload) || payload is null) return Results.BadRequest(new { valid=false, message="QR inválido o alterado." });
    var t = await db.FuelTickets.Include(x=>x.Request)!.ThenInclude(x=>x!.Employee).Include(x=>x.Request)!.ThenInclude(x=>x!.Vehicle).SingleOrDefaultAsync(x=>x.Id==payload.TicketId);
    Console.WriteLine($"[DEBUG validate] TicketId buscado={payload.TicketId} | encontrado={t is not null} | hashGuardado={t?.PayloadHash} | hashCalculado={qr.HashPayload(encoded)}");
    if (t is null || t.PayloadHash != qr.HashPayload(encoded)) return Results.NotFound(new { valid=false, message="El ticket no existe." });
    if (t.Status == TicketStatus.Consumed) return Results.Conflict(new { valid=false, message="El ticket ya fue consumido." });
    if (t.Status == TicketStatus.Cancelled) return Results.Conflict(new { valid=false, message="El ticket fue anulado." });
    if (t.ExpiresAtUtc <= DateTime.UtcNow) { t.Status=TicketStatus.Expired; await db.SaveChangesAsync(); return Results.BadRequest(new { valid=false, message="El ticket está vencido." }); }
    return Results.Ok(new { valid=true, message="Ticket válido", t.Id, t.Sequence, t.ExpiresAtUtc, employee=t.Request!.Employee!.FullName, vehicle=t.Request.Vehicle!.Plate, gallons=t.Request.AuthorizedGallons, fuelType=t.Request.FuelType });
}).RequireAuthorization("Dispatch");

api.MapPost("/dispatches", async (DispatchInput x, ClaimsPrincipal user, AppDbContext db, QrSecurityService qr, AuditService audit) =>
{
    if (!qr.TryValidate(x.QrPayload, out var payload) || payload is null) return Results.BadRequest("QR inválido o alterado.");
    await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
    var ticket = await db.FuelTickets.Include(t=>t.Request).SingleOrDefaultAsync(t=>t.Id==payload.TicketId);
    var tank = await db.FuelTanks.FindAsync(x.TankId);
    if (ticket is null || ticket.PayloadHash != qr.HashPayload(x.QrPayload)) return Results.NotFound("Ticket inexistente.");
    if (ticket.Status is TicketStatus.Consumed or TicketStatus.Cancelled || ticket.ExpiresAtUtc <= DateTime.UtcNow) return Results.Conflict("El ticket no está disponible.");
    if (x.GallonsServed <= 0 || x.GallonsServed > ticket.Request!.AuthorizedGallons) return Results.BadRequest("Cantidad servida inválida.");
    if (tank is null || tank.FuelType != ticket.Request.FuelType || tank.CurrentGallons < x.GallonsServed) return Results.BadRequest("Tanque incorrecto o inventario insuficiente.");
    tank.CurrentGallons -= x.GallonsServed;
    ticket.Status = TicketStatus.Consumed; ticket.ConsumedAtUtc = DateTime.UtcNow;
    var dispatch = new Dispatch { TicketId=ticket.Id, TankId=tank.Id, GallonsServed=x.GallonsServed, OperatorUserId=user.UserId(), Station=x.Station, Notes=x.Notes };
    db.Dispatches.Add(dispatch);
    db.InventoryMovements.Add(new InventoryMovement { TankId=tank.Id, Type=MovementType.Dispatch, QuantityGallons=-x.GallonsServed, BalanceAfterGallons=tank.CurrentGallons, Reference=ticket.Sequence, UserId=user.UserId(), Notes=x.Notes });
    await db.SaveChangesAsync(); await tx.CommitAsync(); await audit.WriteAsync("DISPATCH", "FuelTicket", ticket.Id, new { DispatchId = dispatch.Id, x.GallonsServed, TankId = tank.Id });
    return Results.Ok(new { dispatch.Id, ticket.Sequence, balance=tank.CurrentGallons, message="Despacho registrado correctamente." });
}).RequireAuthorization("Dispatch");

api.MapGet("/inventory/movements", async (AppDbContext db) => await db.InventoryMovements.Include(x=>x.Tank).OrderByDescending(x=>x.CreatedAtUtc).Take(500).ToListAsync()).RequireAuthorization("Reports");
api.MapPost("/inventory/movements", async (InventoryInput x, ClaimsPrincipal user, AppDbContext db, AuditService audit) =>
{
    if (!Enum.TryParse<MovementType>(x.Type, true, out var type) || type == MovementType.Dispatch) return Results.BadRequest("Tipo de movimiento inválido.");
    var tank = await db.FuelTanks.FindAsync(x.TankId); if (tank is null) return Results.NotFound();
    var signed = type is MovementType.NegativeAdjustment or MovementType.Shrinkage or MovementType.TransferOut ? -Math.Abs(x.QuantityGallons) : Math.Abs(x.QuantityGallons);
    var balance = tank.CurrentGallons + signed;
    if (balance < 0 || balance > tank.CapacityGallons) return Results.BadRequest("El movimiento excede la capacidad o produce inventario negativo.");
    tank.CurrentGallons = balance;
    var item = new InventoryMovement { TankId=tank.Id, Type=type, QuantityGallons=signed, BalanceAfterGallons=balance, Reference=type==MovementType.Receipt ? x.InvoiceNumber ?? "RECEPCIÓN" : type.ToString(), SupplierRnc=x.SupplierRnc, SupplierName=x.SupplierName, InvoiceNumber=x.InvoiceNumber, Notes=x.Notes, UserId=user.UserId() };
    db.Add(item); await db.SaveChangesAsync(); await audit.WriteAsync("INVENTORY_MOVEMENT", "FuelTank", tank.Id, new { type, signed, balance }); return Results.Ok(item);
}).RequireAuthorization("Manage");

api.MapGet("/reports/dispatches", async (DateTime? from, DateTime? to, int? departmentId, AppDbContext db) =>
{
    var q = db.Dispatches.Include(x=>x.OperatorUser).Include(x=>x.Ticket)!.ThenInclude(x=>x!.Request)!.ThenInclude(x=>x!.Employee).Include(x=>x.Ticket)!.ThenInclude(x=>x!.Request)!.ThenInclude(x=>x!.Vehicle).Include(x=>x.Ticket)!.ThenInclude(x=>x!.Request)!.ThenInclude(x=>x!.Department).AsQueryable();
    if (from.HasValue) q=q.Where(x=>x.DispatchedAtUtc>=from.Value.ToUniversalTime()); if(to.HasValue) q=q.Where(x=>x.DispatchedAtUtc<to.Value.ToUniversalTime().AddDays(1)); if(departmentId.HasValue) q=q.Where(x=>x.Ticket!.Request!.DepartmentId==departmentId.Value);
    return Results.Ok(await q.OrderByDescending(x=>x.DispatchedAtUtc).ToListAsync());
}).RequireAuthorization("Reports");
api.MapGet("/reports/dispatches.xlsx", async (DateTime? from, DateTime? to, int? departmentId, AppDbContext db) =>
{
    var q=db.Dispatches.Include(x=>x.OperatorUser).Include(x=>x.Ticket)!.ThenInclude(x=>x!.Request)!.ThenInclude(x=>x!.Employee).Include(x=>x.Ticket)!.ThenInclude(x=>x!.Request)!.ThenInclude(x=>x!.Vehicle).Include(x=>x.Ticket)!.ThenInclude(x=>x!.Request)!.ThenInclude(x=>x!.Department).AsQueryable();
    if(from.HasValue)q=q.Where(x=>x.DispatchedAtUtc>=from.Value.ToUniversalTime()); if(to.HasValue)q=q.Where(x=>x.DispatchedAtUtc<to.Value.ToUniversalTime().AddDays(1)); if(departmentId.HasValue)q=q.Where(x=>x.Ticket!.Request!.DepartmentId==departmentId.Value);
    return Results.File(DocumentServices.ReportExcel(await q.OrderByDescending(x=>x.DispatchedAtUtc).ToListAsync()), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "ReporteDespachos.xlsx");
}).RequireAuthorization("Reports");
api.MapGet("/reports/dispatches.csv", async (DateTime? from, DateTime? to, int? departmentId, AppDbContext db) =>
{
    var q=db.Dispatches.Include(x=>x.Ticket)!.ThenInclude(x=>x!.Request)!.ThenInclude(x=>x!.Vehicle).AsQueryable();
    if(from.HasValue)q=q.Where(x=>x.DispatchedAtUtc>=from.Value.ToUniversalTime()); if(to.HasValue)q=q.Where(x=>x.DispatchedAtUtc<to.Value.ToUniversalTime().AddDays(1)); if(departmentId.HasValue)q=q.Where(x=>x.Ticket!.Request!.DepartmentId==departmentId.Value);
    var rows=await q.OrderByDescending(x=>x.DispatchedAtUtc).ToListAsync();
    var sb=new StringBuilder("Fecha,Ticket,Vehiculo,Galones,Estacion\n"); foreach(var x in rows) sb.AppendLine($"{x.DispatchedAtUtc:O},{x.Ticket?.Sequence},{x.Ticket?.Request?.Vehicle?.Plate},{x.GallonsServed.ToString(CultureInfo.InvariantCulture)},\"{x.Station.Replace("\"","\"\"")}\"");
    return Results.File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "ReporteDespachos.csv");
}).RequireAuthorization("Reports");
api.MapGet("/reports/dispatches.pdf", async (DateTime? from, DateTime? to, int? departmentId, AppDbContext db) =>
{
    var q=db.Dispatches.Include(x=>x.Ticket)!.ThenInclude(x=>x!.Request)!.ThenInclude(x=>x!.Employee).Include(x=>x.Ticket)!.ThenInclude(x=>x!.Request)!.ThenInclude(x=>x!.Vehicle).Include(x=>x.Ticket)!.ThenInclude(x=>x!.Request)!.ThenInclude(x=>x!.Department).AsQueryable();
    if(from.HasValue)q=q.Where(x=>x.DispatchedAtUtc>=from.Value.ToUniversalTime()); if(to.HasValue)q=q.Where(x=>x.DispatchedAtUtc<to.Value.ToUniversalTime().AddDays(1)); if(departmentId.HasValue)q=q.Where(x=>x.Ticket!.Request!.DepartmentId==departmentId.Value);
    var rows=await q.OrderByDescending(x=>x.DispatchedAtUtc).ToListAsync();
    return Results.File(DocumentServices.ReportPdf(rows),"application/pdf","ReporteDespachos.pdf");
}).RequireAuthorization("Reports");

api.MapPost("/closures", async (ClosureInput x, ClaimsPrincipal user, AppDbContext db, AuditService audit) =>
{
    if(await db.DailyClosures.AnyAsync(c=>c.BusinessDate==x.BusinessDate)) return Results.Conflict("El día ya fue cerrado.");
    var start=x.BusinessDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc); var end=start.AddDays(1);
    var received=await db.InventoryMovements.Where(m=>m.CreatedAtUtc>=start&&m.CreatedAtUtc<end&&m.Type==MovementType.Receipt).SumAsync(m=>(decimal?)m.QuantityGallons)??0;
    var dispatched=await db.Dispatches.Where(d=>d.DispatchedAtUtc>=start&&d.DispatchedAtUtc<end).SumAsync(d=>(decimal?)d.GallonsServed)??0;
    var adjustments=await db.InventoryMovements.Where(m=>m.CreatedAtUtc>=start&&m.CreatedAtUtc<end&&(m.Type==MovementType.PositiveAdjustment||m.Type==MovementType.NegativeAdjustment||m.Type==MovementType.Shrinkage)).SumAsync(m=>(decimal?)m.QuantityGallons)??0;
    var systemClosing=await db.FuelTanks.SumAsync(t=>t.CurrentGallons); var opening=systemClosing-received+dispatched-adjustments;
    var closure=new DailyClosure { BusinessDate=x.BusinessDate, OpeningGallons=opening, ReceivedGallons=received, DispatchedGallons=dispatched, AdjustmentsGallons=adjustments, ClosingGallons=x.PhysicalClosingGallons, DifferenceGallons=x.PhysicalClosingGallons-systemClosing, ClosedByUserId=user.UserId() };
    db.Add(closure); await db.SaveChangesAsync(); await audit.WriteAsync("DAILY_CLOSURE","DailyClosure",closure.Id,closure); return Results.Ok(closure);
}).RequireAuthorization("Manage");
api.MapGet("/closures", async (AppDbContext db)=>await db.DailyClosures.OrderByDescending(x=>x.BusinessDate).ToListAsync()).RequireAuthorization("Reports");
api.MapGet("/audit", async (AppDbContext db)=>await db.AuditLogs.OrderByDescending(x=>x.CreatedAtUtc).Take(1000).ToListAsync()).RequireAuthorization("Reports");
api.MapGet("/notifications", async (AppDbContext db)=>await db.Notifications.OrderByDescending(x=>x.CreatedAtUtc).Take(500).ToListAsync()).RequireAuthorization("Manage");
api.MapGet("/settings", async (AppDbContext db)=>await db.SystemSettings.OrderBy(x=>x.Key).ToListAsync()).RequireAuthorization("Admin");
api.MapPut("/settings/{key}", async (string key, SettingInput x, AppDbContext db, AuditService audit)=>{var item=await db.SystemSettings.SingleOrDefaultAsync(s=>s.Key==key);if(item is null){item=new SystemSetting{Key=key};db.Add(item);}item.Value=x.Value;item.Description=x.Description;await db.SaveChangesAsync();await audit.WriteAsync("UPDATE","SystemSetting",key);return Results.Ok(item);}).RequireAuthorization("Admin");

app.MapFallbackToFile("index.html");
app.Run();

static async Task SeedAsync(IServiceProvider services)
{
    using var scope=services.CreateScope(); var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    if(!await db.Users.AnyAsync())
    {
        var admin=new AppUser { Username="admin", FullName="Administrador General", Email="admin@fuelcontrol.local", Role=UserRole.Administrator };
        admin.PasswordHash=Passwords.Hash(admin,"Admin123!"); db.Users.Add(admin);
    }
    if(!await db.SystemSettings.AnyAsync(x=>x.Key=="TicketPrefix")) db.SystemSettings.Add(new SystemSetting { Key="TicketPrefix",Value="COM",Description="Prefijo de numeración de tickets" });
    if(!await db.SystemSettings.AnyAsync(x=>x.Key=="InstitutionName")) db.SystemSettings.Add(new SystemSetting { Key="InstitutionName",Value="Institución",Description="Nombre de la institución" });
    if(!await db.SystemSettings.AnyAsync(x=>x.Key=="InstitutionRnc")) db.SystemSettings.Add(new SystemSetting { Key="InstitutionRnc",Value="",Description="RNC o identificación institucional" });
    if(!await db.SystemSettings.AnyAsync(x=>x.Key=="ContactEmail")) db.SystemSettings.Add(new SystemSetting { Key="ContactEmail",Value="",Description="Correo administrativo" });
    if(!await db.SystemSettings.AnyAsync(x=>x.Key=="TicketValidityHours")) db.SystemSettings.Add(new SystemSetting { Key="TicketValidityHours",Value="24",Description="Vigencia predeterminada del ticket en horas" });
    await db.SaveChangesAsync();
}

public partial class Program { }
