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

**Add SQL Scripts to the Project:**
   Place your SQL migration scripts in a folder (e.g., `Migrations`) within the project.

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
    ├── MigrationManager.cs
└── Program.cs
```

### **3.2. Set SQL Files as Embedded Resources:**
   - Right-click on each `.sql` file in the `Migrations` folder in your project.
   - Set the **Build Action** to **Embedded Resource** (in Visual Studio or your editor's properties for the file).

   Alternatively, edit the `.csproj` file to include all `.sql` files as embedded resources:

   ```xml
   <ItemGroup>
       <EmbeddedResource Include="Migrations\*.sql" />
   </ItemGroup>
   ```
---

## **4. Step-by-Step Implementation**

### **4.1. Add DbUp to Your Project**
Run the following command in the project root:

```bash
dotnet add package DbUp
```

### **4.2. Create a Migration Manager**
Add a `MigrationManager.cs` to handle schema upgrades using a transaction-scoped connection with versioning using the built-in SchemaVersions table and enhanced logging.

```csharp
using DbUp;
using DbUp.Engine.Output;
using System;
using System.Data;
using System.Data.SqlClient;

public class MigrationManager
{
    public static void Execute(string connectionString)
    {
        var logger = new ConsoleUpgradeLog();

        // Create a connection to the database
        using var connection = new SqlConnection(connectionString);
        connection.Open();

        // Start a transaction
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);

        try
        {
            // Configure the DbUp upgrader
            var upgrader = DeployChanges.To
                .SqlDatabase(() => connection, () => transaction)
                .WithScriptsEmbeddedInAssembly(typeof(MigrationManager).Assembly)
                .LogTo(logger) // Use detailed logging
                .JournalToSqlTable("dbo", "SchemaVersions") // Use SchemaVersions table for versioning
                .Build();

            // Perform the upgrade
            var result = upgrader.PerformUpgrade();

            if (!result.Successful)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Migration failed: {result.Error}");
                Console.ResetColor();

                // Rollback the transaction if migration fails
                transaction.Rollback();
                throw new Exception("Database upgrade failed!");
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Database upgrade successful!");
            Console.ResetColor();

            // Commit the transaction on success
            transaction.Commit();
        }
        catch (Exception ex)
        {
            // Rollback the transaction on any exception
            transaction.Rollback();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Transaction rolled back: {ex.Message}");
            Console.ResetColor();
            throw;
        }
        finally
        {
            connection.Close();
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
using System;

class Program
{
    static void Main(string[] args)
    {
        var connectionString = "YourConnectionStringHere";

        Console.WriteLine("Executing database migrations...");
        try
        {
            MigrationManager.Execute(connectionString);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during migration: {ex.Message}");
        }

        Console.WriteLine("Database operations completed.");
    }
}
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

## **7. Advanced Considerations**
- **Versioning**: Store the executed scripts in a `SchemaVersions` table (DbUp handles this internally).
- **Error Logging**: Use `DbUp.Engine.Output.IUpgradeLog` for detailed logging.
- **Script Providers**: Use `WithScriptsEmbeddedInAssembly` for embedded scripts if you don’t want a file-based approach.

---

## **8. Conclusion**
DbUp provides a simple yet robust way to handle schema migrations in a .NET API. By organizing scripts, setting up rollback mechanisms, and running migrations iteratively, you can ensure your database schema evolves seamlessly with your application.

