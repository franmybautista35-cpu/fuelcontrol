namespace FuelTickets.Contracts;

public record LoginRequest(string Username, string Password);
public record LoginResponse(string Token, string FullName, string Role);
public record DepartmentInput(string Name, string Code, bool IsActive = true);
public record EmployeeInput(string EmployeeCode, string FullName, string NationalId, int DepartmentId, string Position, string Email, string Mobile, bool IsActive = true);
public record VehicleInput(string Plate, string InternalCode, string Make, string Model, int Year, string Type, int DepartmentId, decimal TankCapacityGallons, decimal OdometerKm, bool IsActive = true);
public record TankInput(string Name, string FuelType, decimal CapacityGallons, decimal CurrentGallons, decimal CriticalLevelGallons, bool IsActive = true);
public record RequestInput(int EmployeeId, int VehicleId, int DepartmentId, decimal AuthorizedGallons, string FuelType, DateTime ExpiresAtUtc, bool IsRecurring, string? RecurrenceRule, string? Notes);
public record InventoryInput(int TankId, string Type, decimal QuantityGallons, string? SupplierRnc, string? SupplierName, string? InvoiceNumber, string? Notes);
public record DispatchInput(string QrPayload, int TankId, decimal GallonsServed, string Station, string? Notes);
public record ClosureInput(DateOnly BusinessDate, decimal PhysicalClosingGallons);
public record UserInput(string Username, string FullName, string Email, string Role, string Password, bool IsActive = true);
public record SettingInput(string Value, string? Description);
