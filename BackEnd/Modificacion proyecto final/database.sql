IF DB_ID(N'FuelTicketsDb') IS NULL CREATE DATABASE FuelTicketsDb;
GO
USE FuelTicketsDb;
GO

CREATE TABLE Departments(
 Id int IDENTITY PRIMARY KEY, Name nvarchar(120) NOT NULL, Code nvarchar(30) NOT NULL UNIQUE, IsActive bit NOT NULL DEFAULT 1
);
CREATE TABLE Users(
 Id int IDENTITY PRIMARY KEY, Username nvarchar(80) NOT NULL UNIQUE, FullName nvarchar(150) NOT NULL,
 Email nvarchar(200) NOT NULL, PasswordHash nvarchar(max) NOT NULL, Role int NOT NULL, IsActive bit NOT NULL DEFAULT 1,
 CreatedAtUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE TABLE Employees(
 Id int IDENTITY PRIMARY KEY, EmployeeCode nvarchar(30) NOT NULL UNIQUE, FullName nvarchar(150) NOT NULL,
 NationalId nvarchar(30) NOT NULL UNIQUE, DepartmentId int NOT NULL, Position nvarchar(100) NOT NULL,
 Email nvarchar(200) NOT NULL, Mobile nvarchar(30) NOT NULL, IsActive bit NOT NULL DEFAULT 1,
 CONSTRAINT FK_Employees_Departments FOREIGN KEY(DepartmentId) REFERENCES Departments(Id)
);
CREATE TABLE Vehicles(
 Id int IDENTITY PRIMARY KEY, Plate nvarchar(20) NOT NULL UNIQUE, InternalCode nvarchar(30) NOT NULL UNIQUE,
 Make nvarchar(80) NOT NULL, Model nvarchar(80) NOT NULL, [Year] int NOT NULL, [Type] nvarchar(60) NOT NULL,
 DepartmentId int NOT NULL, TankCapacityGallons decimal(18,2) NOT NULL, OdometerKm decimal(18,2) NOT NULL,
 IsActive bit NOT NULL DEFAULT 1, CONSTRAINT FK_Vehicles_Departments FOREIGN KEY(DepartmentId) REFERENCES Departments(Id)
);
CREATE TABLE FuelTanks(
 Id int IDENTITY PRIMARY KEY, Name nvarchar(80) NOT NULL, FuelType nvarchar(30) NOT NULL,
 CapacityGallons decimal(18,2) NOT NULL, CurrentGallons decimal(18,2) NOT NULL,
 CriticalLevelGallons decimal(18,2) NOT NULL, IsActive bit NOT NULL DEFAULT 1,
 CONSTRAINT CK_FuelTanks_Amounts CHECK(CapacityGallons>0 AND CurrentGallons>=0 AND CurrentGallons<=CapacityGallons)
);
CREATE TABLE FuelRequests(
 Id int IDENTITY PRIMARY KEY, EmployeeId int NOT NULL, VehicleId int NOT NULL, DepartmentId int NOT NULL,
 AuthorizedGallons decimal(18,2) NOT NULL, FuelType nvarchar(30) NOT NULL, RequestedAtUtc datetime2 NOT NULL,
 ExpiresAtUtc datetime2 NOT NULL, [Status] int NOT NULL, IsRecurring bit NOT NULL DEFAULT 0,
 RecurrenceRule nvarchar(80) NULL, CreatedByUserId int NOT NULL, Notes nvarchar(500) NULL,
 CONSTRAINT FK_Requests_Employees FOREIGN KEY(EmployeeId) REFERENCES Employees(Id),
 CONSTRAINT FK_Requests_Vehicles FOREIGN KEY(VehicleId) REFERENCES Vehicles(Id),
 CONSTRAINT FK_Requests_Departments FOREIGN KEY(DepartmentId) REFERENCES Departments(Id),
 CONSTRAINT FK_Requests_Users FOREIGN KEY(CreatedByUserId) REFERENCES Users(Id),
 CONSTRAINT CK_Requests_Gallons CHECK(AuthorizedGallons>0)
);
CREATE TABLE FuelTickets(
 Id uniqueidentifier PRIMARY KEY, Sequence nvarchar(30) NOT NULL UNIQUE, RequestId int NOT NULL UNIQUE,
 CreatedAtUtc datetime2 NOT NULL, ExpiresAtUtc datetime2 NOT NULL, [Status] int NOT NULL,
 Nonce nvarchar(64) NOT NULL UNIQUE, PayloadHash nvarchar(128) NOT NULL, ConsumedAtUtc datetime2 NULL,
 CONSTRAINT FK_Tickets_Requests FOREIGN KEY(RequestId) REFERENCES FuelRequests(Id)
);
CREATE TABLE Dispatches(
 Id int IDENTITY PRIMARY KEY, TicketId uniqueidentifier NOT NULL UNIQUE, TankId int NOT NULL,
 GallonsServed decimal(18,2) NOT NULL, OperatorUserId int NOT NULL, Station nvarchar(120) NOT NULL,
 Notes nvarchar(500) NULL, DispatchedAtUtc datetime2 NOT NULL,
 CONSTRAINT FK_Dispatches_Tickets FOREIGN KEY(TicketId) REFERENCES FuelTickets(Id),
 CONSTRAINT FK_Dispatches_Tanks FOREIGN KEY(TankId) REFERENCES FuelTanks(Id),
 CONSTRAINT FK_Dispatches_Users FOREIGN KEY(OperatorUserId) REFERENCES Users(Id),
 CONSTRAINT CK_Dispatches_Gallons CHECK(GallonsServed>0)
);
CREATE TABLE InventoryMovements(
 Id bigint IDENTITY PRIMARY KEY, TankId int NOT NULL, [Type] int NOT NULL, QuantityGallons decimal(18,2) NOT NULL,
 BalanceAfterGallons decimal(18,2) NOT NULL, Reference nvarchar(80) NOT NULL, SupplierRnc nvarchar(80) NULL,
 SupplierName nvarchar(160) NULL, InvoiceNumber nvarchar(80) NULL, Notes nvarchar(500) NULL,
 UserId int NOT NULL, CreatedAtUtc datetime2 NOT NULL,
 CONSTRAINT FK_Movements_Tanks FOREIGN KEY(TankId) REFERENCES FuelTanks(Id),
 CONSTRAINT FK_Movements_Users FOREIGN KEY(UserId) REFERENCES Users(Id)
);
CREATE TABLE AuditLogs(
 Id bigint IDENTITY PRIMARY KEY, UserId int NULL, Username nvarchar(80) NOT NULL, [Action] nvarchar(80) NOT NULL,
 Entity nvarchar(80) NOT NULL, EntityId nvarchar(80) NULL, DetailsJson nvarchar(max) NULL,
 IpAddress nvarchar(64) NOT NULL, CreatedAtUtc datetime2 NOT NULL
);
CREATE TABLE DailyClosures(
 Id int IDENTITY PRIMARY KEY, BusinessDate date NOT NULL UNIQUE, OpeningGallons decimal(18,2) NOT NULL,
 ReceivedGallons decimal(18,2) NOT NULL, DispatchedGallons decimal(18,2) NOT NULL,
 AdjustmentsGallons decimal(18,2) NOT NULL, ClosingGallons decimal(18,2) NOT NULL,
 DifferenceGallons decimal(18,2) NOT NULL, ClosedByUserId int NOT NULL, ClosedAtUtc datetime2 NOT NULL,
 CONSTRAINT FK_Closures_Users FOREIGN KEY(ClosedByUserId) REFERENCES Users(Id)
);
CREATE TABLE SystemSettings(
 Id int IDENTITY PRIMARY KEY, [Key] nvarchar(80) NOT NULL UNIQUE, [Value] nvarchar(500) NOT NULL, Description nvarchar(300) NULL
);
CREATE TABLE Notifications(
 Id bigint IDENTITY PRIMARY KEY, Channel nvarchar(30) NOT NULL, Recipient nvarchar(200) NOT NULL,
 Subject nvarchar(180) NOT NULL, [Message] nvarchar(1000) NOT NULL, [Status] nvarchar(30) NOT NULL,
 CreatedAtUtc datetime2 NOT NULL, SentAtUtc datetime2 NULL
);

CREATE INDEX IX_Requests_Status ON FuelRequests([Status], ExpiresAtUtc);
CREATE INDEX IX_Tickets_Status ON FuelTickets([Status], ExpiresAtUtc);
CREATE INDEX IX_Movements_TankDate ON InventoryMovements(TankId, CreatedAtUtc DESC);
CREATE INDEX IX_Dispatches_Date ON Dispatches(DispatchedAtUtc DESC);
CREATE INDEX IX_Audit_Date ON AuditLogs(CreatedAtUtc DESC);
GO
