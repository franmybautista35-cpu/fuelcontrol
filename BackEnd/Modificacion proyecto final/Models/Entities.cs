using System.ComponentModel.DataAnnotations;

namespace FuelTickets.Models;

public enum UserRole { Administrator, Supervisor, Dispatcher, Auditor, Viewer }
public enum RequestStatus { Pending, Approved, Rejected, Cancelled }
public enum TicketStatus { Created, Sent, Pending, NearExpiry, Expired, Consumed, Cancelled }
public enum MovementType { Receipt, Dispatch, PositiveAdjustment, NegativeAdjustment, TransferIn, TransferOut, Shrinkage }

public class AppUser
{
    public int Id { get; set; }
    [MaxLength(80)] public string Username { get; set; } = "";
    [MaxLength(150)] public string FullName { get; set; } = "";
    [MaxLength(200)] public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class Department
{
    public int Id { get; set; }
    [MaxLength(120)] public string Name { get; set; } = "";
    [MaxLength(30)] public string Code { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public class Employee
{
    public int Id { get; set; }
    [MaxLength(30)] public string EmployeeCode { get; set; } = "";
    [MaxLength(150)] public string FullName { get; set; } = "";
    [MaxLength(30)] public string NationalId { get; set; } = "";
    public int DepartmentId { get; set; }
    public Department? Department { get; set; }
    [MaxLength(100)] public string Position { get; set; } = "";
    [MaxLength(200)] public string Email { get; set; } = "";
    [MaxLength(30)] public string Mobile { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public class Vehicle
{
    public int Id { get; set; }
    [MaxLength(20)] public string Plate { get; set; } = "";
    [MaxLength(30)] public string InternalCode { get; set; } = "";
    [MaxLength(80)] public string Make { get; set; } = "";
    [MaxLength(80)] public string Model { get; set; } = "";
    public int Year { get; set; }
    [MaxLength(60)] public string Type { get; set; } = "";
    public int DepartmentId { get; set; }
    public Department? Department { get; set; }
    public decimal TankCapacityGallons { get; set; }
    public decimal OdometerKm { get; set; }
    public bool IsActive { get; set; } = true;
}

public class FuelTank
{
    public int Id { get; set; }
    [MaxLength(80)] public string Name { get; set; } = "";
    [MaxLength(30)] public string FuelType { get; set; } = "Gasolina";
    public decimal CapacityGallons { get; set; }
    public decimal CurrentGallons { get; set; }
    public decimal CriticalLevelGallons { get; set; }
    public bool IsActive { get; set; } = true;
}

public class FuelRequest
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public int VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
    public int DepartmentId { get; set; }
    public Department? Department { get; set; }
    public decimal AuthorizedGallons { get; set; }
    [MaxLength(30)] public string FuelType { get; set; } = "Gasolina";
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.Pending;
    public bool IsRecurring { get; set; }
    [MaxLength(80)] public string? RecurrenceRule { get; set; }
    public int CreatedByUserId { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
}

public class FuelTicket
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(30)] public string Sequence { get; set; } = "";
    public int RequestId { get; set; }
    public FuelRequest? Request { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.Created;
    [MaxLength(64)] public string Nonce { get; set; } = Guid.NewGuid().ToString("N");
    [MaxLength(128)] public string PayloadHash { get; set; } = "";
    public DateTime? ConsumedAtUtc { get; set; }
}

public class Dispatch
{
    public int Id { get; set; }
    public Guid TicketId { get; set; }
    public FuelTicket? Ticket { get; set; }
    public int TankId { get; set; }
    public FuelTank? Tank { get; set; }
    public decimal GallonsServed { get; set; }
    public int OperatorUserId { get; set; }
    public AppUser? OperatorUser { get; set; }
    [MaxLength(120)] public string Station { get; set; } = "Estación principal";
    [MaxLength(500)] public string? Notes { get; set; }
    public DateTime DispatchedAtUtc { get; set; } = DateTime.UtcNow;
}

public class InventoryMovement
{
    public long Id { get; set; }
    public int TankId { get; set; }
    public FuelTank? Tank { get; set; }
    public MovementType Type { get; set; }
    public decimal QuantityGallons { get; set; }
    public decimal BalanceAfterGallons { get; set; }
    [MaxLength(80)] public string Reference { get; set; } = "";
    [MaxLength(80)] public string? SupplierRnc { get; set; }
    [MaxLength(160)] public string? SupplierName { get; set; }
    [MaxLength(80)] public string? InvoiceNumber { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
    public int UserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class AuditLog
{
    public long Id { get; set; }
    public int? UserId { get; set; }
    [MaxLength(80)] public string Username { get; set; } = "system";
    [MaxLength(80)] public string Action { get; set; } = "";
    [MaxLength(80)] public string Entity { get; set; } = "";
    [MaxLength(80)] public string? EntityId { get; set; }
    public string? DetailsJson { get; set; }
    [MaxLength(64)] public string IpAddress { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class DailyClosure
{
    public int Id { get; set; }
    public DateOnly BusinessDate { get; set; }
    public decimal OpeningGallons { get; set; }
    public decimal ReceivedGallons { get; set; }
    public decimal DispatchedGallons { get; set; }
    public decimal AdjustmentsGallons { get; set; }
    public decimal ClosingGallons { get; set; }
    public decimal DifferenceGallons { get; set; }
    public int ClosedByUserId { get; set; }
    public DateTime ClosedAtUtc { get; set; } = DateTime.UtcNow;
}

public class SystemSetting
{
    public int Id { get; set; }
    [MaxLength(80)] public string Key { get; set; } = "";
    [MaxLength(500)] public string Value { get; set; } = "";
    [MaxLength(300)] public string? Description { get; set; }
}

public class Notification
{
    public long Id { get; set; }
    [MaxLength(30)] public string Channel { get; set; } = "System";
    [MaxLength(200)] public string Recipient { get; set; } = "";
    [MaxLength(180)] public string Subject { get; set; } = "";
    [MaxLength(1000)] public string Message { get; set; } = "";
    [MaxLength(30)] public string Status { get; set; } = "Pending";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SentAtUtc { get; set; }
}
