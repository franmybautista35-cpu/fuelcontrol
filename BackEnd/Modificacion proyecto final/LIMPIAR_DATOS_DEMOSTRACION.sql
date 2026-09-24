USE FuelTicketsDb;
GO
BEGIN TRANSACTION;
DELETE FROM Notifications;
DELETE FROM AuditLogs;
DELETE FROM DailyClosures;
DELETE FROM InventoryMovements;
DELETE FROM Dispatches;
DELETE FROM FuelTickets;
DELETE FROM FuelRequests;
DELETE FROM Vehicles WHERE Plate = 'DEMO001' OR InternalCode = 'VH-001';
DELETE FROM Employees WHERE EmployeeCode = 'EMP-001' OR NationalId = '000-0000000-0';
DELETE FROM FuelTanks WHERE Name = 'Tanque principal';
DELETE FROM Departments WHERE Code = 'ADM' AND Name = N'Administración';
COMMIT TRANSACTION;
GO
SELECT 'Datos de demostración eliminados. El administrador se conservó.' AS Resultado;
