using FuelTickets.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FuelTickets.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<FuelTank> FuelTanks => Set<FuelTank>();
    public DbSet<FuelRequest> FuelRequests => Set<FuelRequest>();
    public DbSet<FuelTicket> FuelTickets => Set<FuelTicket>();
    public DbSet<Dispatch> Dispatches => Set<Dispatch>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<DailyClosure> DailyClosures => Set<DailyClosure>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AppUser>().HasIndex(x => x.Username).IsUnique();
        b.Entity<Department>().HasIndex(x => x.Code).IsUnique();
        b.Entity<Employee>().HasIndex(x => x.EmployeeCode).IsUnique();
        b.Entity<Employee>().HasIndex(x => x.NationalId).IsUnique();
        b.Entity<Vehicle>().HasIndex(x => x.Plate).IsUnique();
        b.Entity<Vehicle>().HasIndex(x => x.InternalCode).IsUnique();
        b.Entity<FuelTicket>().HasIndex(x => x.Sequence).IsUnique();
        b.Entity<FuelTicket>().HasIndex(x => x.Nonce).IsUnique();
        b.Entity<Dispatch>().HasIndex(x => x.TicketId).IsUnique();
        b.Entity<SystemSetting>().HasIndex(x => x.Key).IsUnique();
        b.Entity<DailyClosure>().HasIndex(x => x.BusinessDate).IsUnique();

        // SQL Server does not allow the multiple cascade paths produced by
        // Department -> Employee/Vehicle -> FuelRequest. Restricting deletes
        // also protects operational and audit history from accidental removal.
        foreach (var foreignKey in b.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            foreignKey.DeleteBehavior = DeleteBehavior.Restrict;

        foreach (var p in b.Model.GetEntityTypes().SelectMany(e => e.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
            p.SetColumnType("decimal(18,2)");

        b.Entity<AuditLog>().Property(x => x.DetailsJson).HasColumnType("nvarchar(max)");

        // SQL Server's datetime2 no guarda si una fecha es UTC o local; EF Core siempre
        // devuelve DateTimeKind.Unspecified al leerla de vuelta. Toda la app asume que
        // estas columnas son UTC (por eso se llaman "...Utc"), y QrSecurityService llama
        // .ToUniversalTime() al firmar el QR — con Kind=Unspecified, .NET interpreta la
        // fecha como si fuera hora LOCAL y la desplaza por el huso horario del servidor,
        // rompiendo la firma cada vez que el ticket se recarga de la base de datos. Este
        // conversor obliga a que toda fecha guardada o leída quede marcada como UTC.
        var utcConverter = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(v, DateTimeKind.Utc) : v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        var utcConverterNullable = new ValueConverter<DateTime?, DateTime?>(
            v => v.HasValue ? (v.Value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v.Value.ToUniversalTime()) : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

        foreach (var p in b.Model.GetEntityTypes().SelectMany(e => e.GetProperties()))
        {
            if (p.ClrType == typeof(DateTime)) p.SetValueConverter(utcConverter);
            else if (p.ClrType == typeof(DateTime?)) p.SetValueConverter(utcConverterNullable);
        }
    }
}
