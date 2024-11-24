using DbUp.Engine.Output;
using System.Transactions;

namespace DbUp.Api.Helpers
{
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
}
