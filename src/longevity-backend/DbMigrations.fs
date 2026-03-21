module DbMigrations

open System.Reflection
open DbUp

let run (connStr: string) =
    let result =
        DeployChanges.To
            .PostgresqlDatabase(connStr)
            .WithScriptsEmbeddedInAssembly(
                Assembly.GetExecutingAssembly(),
                fun s -> s.Contains(".Migrations."))
            .WithTransactionPerScript()
            .LogToConsole()
            .Build()
            .PerformUpgrade()

    if not result.Successful then
        failwithf "DB migration failed: %s" (result.Error.Message)
