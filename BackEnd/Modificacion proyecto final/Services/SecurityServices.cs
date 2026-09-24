using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FuelTickets.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace FuelTickets.Services;

public class TokenService(IConfiguration configuration)
{
    public string Create(AppUser user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim("full_name", user.FullName),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };
        var token = new JwtSecurityToken(
            configuration["Jwt:Issuer"], configuration["Jwt:Audience"], claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public record QrTicketPayload(Guid TicketId, string Sequence, DateTime IssuedAtUtc, DateTime ExpiresAtUtc, int EmployeeId, int VehicleId, decimal Gallons, string FuelType, string Nonce, string DataHash, string Signature);

public class QrSecurityService(IConfiguration configuration)
{
    private string Key => configuration["Qr:SigningKey"] ?? throw new InvalidOperationException("Missing QR signing key");

    public string CreatePayload(FuelTicket ticket)
    {
        var request = ticket.Request ?? throw new InvalidOperationException("The ticket request must be loaded to create its QR.");
        var data = CanonicalData(ticket.Id, ticket.Sequence, ticket.CreatedAtUtc, ticket.ExpiresAtUtc, request.EmployeeId, request.VehicleId, request.AuthorizedGallons, request.FuelType, ticket.Nonce);
        var dataHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data)));
        var signature = Sign(dataHash);
        return Base64Url(JsonSerializer.Serialize(new QrTicketPayload(ticket.Id, ticket.Sequence, ticket.CreatedAtUtc, ticket.ExpiresAtUtc, request.EmployeeId, request.VehicleId, request.AuthorizedGallons, request.FuelType, ticket.Nonce, dataHash, signature)));
    }

    public bool TryValidate(string encoded, out QrTicketPayload? payload)
    {
        payload = null;
        try
        {
            var json = Encoding.UTF8.GetString(Base64UrlDecode(encoded));
            payload = JsonSerializer.Deserialize<QrTicketPayload>(json);
            if (payload is null) return false;
            var data = CanonicalData(payload.TicketId, payload.Sequence, payload.IssuedAtUtc, payload.ExpiresAtUtc, payload.EmployeeId, payload.VehicleId, payload.Gallons, payload.FuelType, payload.Nonce);
            var expectedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data)));
            var expectedSignature = Sign(expectedHash);
            return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(expectedHash), Convert.FromHexString(payload.DataHash))
                && CryptographicOperations.FixedTimeEquals(Convert.FromHexString(expectedSignature), Convert.FromHexString(payload.Signature));
        }
        catch { return false; }
    }

    public string HashPayload(string payload) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    private string Sign(string data) => Convert.ToHexString(new HMACSHA256(Encoding.UTF8.GetBytes(Key)).ComputeHash(Encoding.UTF8.GetBytes(data)));
    private static string CanonicalData(Guid id, string sequence, DateTime issued, DateTime expiry, int employeeId, int vehicleId, decimal gallons, string fuelType, string nonce) => $"{id:N}|{sequence}|{DateTime.SpecifyKind(issued, DateTimeKind.Utc):O}|{DateTime.SpecifyKind(expiry, DateTimeKind.Utc):O}|{employeeId}|{vehicleId}|{gallons.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}|{fuelType}|{nonce}";
    private static string Base64Url(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static byte[] Base64UrlDecode(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        s += new string('=', (4 - s.Length % 4) % 4);
        return Convert.FromBase64String(s);
    }
}

public static class Passwords
{
    public static string Hash(AppUser user, string password) => new PasswordHasher<AppUser>().HashPassword(user, password);
    public static bool Verify(AppUser user, string password) => new PasswordHasher<AppUser>().VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;
}
