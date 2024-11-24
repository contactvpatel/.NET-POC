# **Using DbUp for Schema Migrations in a .NET API**
Here is a comprehensive end-to-end guide on using **DbUp** in a .NET API project for schema migration, including a rollback strategy and multiple iteration examples.

## **1. Overview**
DbUp is a .NET library for managing database schema migrations. It uses plain SQL scripts to upgrade and manage database schemas incrementally. This document outlines how to:
- Set up DbUp in a .NET API.
- Implement schema migration.
- Enable a rollback strategy.
- Demonstrate usage with multiple iterations.

More Info: https://dbup.readthedocs.io/en/latest/

---

## **2. Prerequisites**
1. **.NET Environment**: Ensure the .NET 6 or later SDK is installed.
2. **Database**: Any database supported by DbUp (SQL Server, SQLite, PostgreSQL, etc.).
3. **NuGet Packages**:
   - `DbUp` (Base package for DbUp operations)
   - `DbUp.ScriptProviders` (Optional for advanced script loading)

---

## **3. Folder Structure**

### **3.1. Steps to Embed SQL Scripts in Assembly**

**Example folder structure:**

```plaintext
ProjectRoot
│
├── Migrations
│   ├── 20241124_153000_AddCustomersTable.sql
│   ├── 20241124_153010_AddOrdersTable.sql
├── Rollbacks
│       ├── 20241124_153000_AddCustomersTable.sql
│       └── 20241124_153010_AddOrdersTable.sql
│
└── Helpers
    ├── MigrationHelper.cs
└── Program.cs
```
---

## **4. Step-by-Step Implementation**

### **4.1. Add DbUp to Your Project**
Run the following command in the project root:

```bash
dotnet add package DbUp
```

### **4.2. Create a Migration Helper Class**
Add a `MigrationHelper.cs` to handle schema upgrades using a transaction-scoped connection with versioning using the built-in SchemaVersions table and enhanced logging.

```csharp
using DbUp.Engine.Output;
using System.Transactions;

public class MigrationHelper
{
    private readonly string _connectionString;
    private readonly ILogger<MigrationHelper> _logger;

    public MigrationHelper(IConfiguration configuration, ILogger<MigrationHelper> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        _logger = logger;
    }

    public void Execute()
    {
        var logger = new ConsoleUpgradeLog();

        try
        {
            // Configure the DbUp upgrader
            var upgrader = DeployChanges.To
                .SqlDatabase(_connectionString)
                .WithTransactionPerScript() // Alternatively, use .WithTransaction() for a single transaction
                .WithScriptsFromFileSystem("Migrations")
                .LogTo(logger) // Use detailed logging
                .JournalToSqlTable("dbo", "SchemaVersions") // Use SchemaVersions table for versioning
                .Build();

            using (var scope = new TransactionScope(TransactionScopeOption.Required, 
                                   new TransactionOptions { IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted }, TransactionScopeAsyncFlowOption.Enabled))
            {
                // Perform database upgrade
                var result = upgrader.PerformUpgrade();

                if (!result.Successful)
                {
                    logger.WriteError($"Upgrade failed: {result.Error}");
                    throw result.Error;
                }

                // Commit the transaction
                scope.Complete();

                // Show success message if any migration applied
                if (result.Scripts.Count() > 0)
                {
                    logger.WriteInformation("Database upgrade successful!");
                }
            }
        }
        catch (Exception ex)
        {
            logger.WriteError($"Upgrade failed. Exception Message: {ex.Message}");
            throw;
        }
    }
}
```
### **4.3. Add SQL Migration Scripts**
Place the following scripts in the `Migrations` folder:

#### **`20241124_153000_AddCustomersTable.sql`**
```sql
CREATE TABLE Customers (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(100),
    Email NVARCHAR(100),
    CreatedDate DATETIME DEFAULT GETDATE()
);
```

#### **`20241124_153010_AddOrdersTable.sql`**
```sql
CREATE TABLE Orders (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    CustomerId INT FOREIGN KEY REFERENCES Customers(Id),
    OrderDate DATETIME DEFAULT GETDATE(),
    Amount DECIMAL(18,2)
);
```

#### Rollback Scripts in `Rollbacks`:
##### **`20241124_153000_AddCustomersTable.sql`**
```sql
DROP TABLE IF EXISTS Customers;
```

##### **`20241124_153010_AddOrdersTable.sql`**
```sql
DROP TABLE IF EXISTS Orders;
```

### **4.4 SchemaVersions Table for Versioning**

DbUp automatically creates a `SchemaVersions` table if it does not exist. It stores details about which scripts have been executed, ensuring:
- Scripts are only executed once.
- You can track the migration history.

The table has the following structure:

| ScriptName          | Applied          |
|---------------------|------------------|
| `20241124_153000_AddCustomersTable.sql` | `2024-11-24 15:30:00` |
| `20241124_153010_AddOrdersTable.sql` | `2024-11-24 15:30:10` |

---

### **4.5. Configure the Application**
Update the `Program.cs` to execute migrations:

```csharp
using DbUp.Api.Helpers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddScoped<MigrationHelper>();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

// Apply migrations at startup
using (var scope = app.Services.CreateScope())
{
    var migrationManager = scope.ServiceProvider.GetRequiredService<MigrationHelper>();
    migrationManager.Execute();
}

app.MapControllers();

app.Run();

```

---

## **5. Multiple Iteration Example**

### **Iteration 1**: Adding New Tables
Run the program to apply the following migrations:
1. `20241124_153000_AddCustomersTable.sql`
2. `20241124_153010_AddOrdersTable.sql`

If any migration fails, run rollback scripts manually if needed. It may not need since migration applied using transaction scope hence rollback if it fails:
- `20241124_153000_AddCustomersTable.sql`
- `20241124_153010_AddOrdersTable.sql`

### **Iteration 2**: Altering the Database
Add a new migration script (`20241125_172000_AddIndexToOrders.sql`) for creating an index:

#### **`20241125_172000_AddIndexToOrders.sql`**
```sql
CREATE INDEX IX_CustomerId ON Orders (CustomerId);
```

Rollback script:
#### **`20241125_172000_AddIndexToOrders.sql`**
```sql
DROP INDEX IF EXISTS IX_CustomerId ON Orders;
```

Re-run the application to upgrade.

---

## **6. Notes on Rollback Strategy**
1. Rollbacks are **manual** with DbUp (DbUp does not support automatic rollbacks).
2. Always test rollback scripts before deployment.
3. Ensure rollback scripts reverse the exact changes made in migration scripts.

---

## **7. Benefits of this Approach**

1. **Versioning with SchemaVersions Table**:
   - Ensures all executed scripts are tracked.
   - Prevents re-execution of already applied scripts.

2. **Detailed Logging**:
   - Provides insights during migrations and rollbacks.
   - Helps identify issues quickly.

3. **Rollback Capability**:
   - Safeguards database integrity in case of migration failures.

4. **Extensibility**:
   - Supports multiple databases with minor modifications.
   - Easily integrates into CI/CD pipelines.

---

## **8. Conclusion**
DbUp provides a simple yet robust way to handle schema migrations in a .NET API. By organizing scripts, setting up rollback mechanisms, and running migrations iteratively, you can ensure your database schema evolves seamlessly with your application.

