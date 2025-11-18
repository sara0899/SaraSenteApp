using FirebirdSql.Data.FirebirdClient;

namespace DbMetaTool
{
    public static class Program
    {
        // Przykładowe wywołania:
        // DbMetaTool build-db --db-dir "C:\db\fb5" --scripts-dir "C:\scripts"
        // DbMetaTool export-scripts --connection-string "..." --output-dir "C:\out"
        // DbMetaTool update-db --connection-string "..." --scripts-dir "C:\scripts"
        public static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Użycie:");
                Console.WriteLine("  build-db --db-dir <ścieżka> --scripts-dir <ścieżka>");
                Console.WriteLine("  export-scripts --connection-string <connStr> --output-dir <ścieżka>");
                Console.WriteLine("  update-db --connection-string <connStr> --scripts-dir <ścieżka>");
                return 1;
            }

            try
            {
                var command = args[0].ToLowerInvariant();

                switch (command)
                {
                    case "build-db":
                        {
                            string dbDir = GetArgValue(args, "--db-dir");
                            string scriptsDir = GetArgValue(args, "--scripts-dir");

                            BuildDatabase(dbDir, scriptsDir);
                            Console.WriteLine("Baza danych została zbudowana pomyślnie.");
                            return 0;
                        }

                    case "export-scripts":
                        {
                            string connStr = GetArgValue(args, "--connection-string");
                            string outputDir = GetArgValue(args, "--output-dir");

                            ExportScripts(connStr, outputDir);
                            Console.WriteLine("Skrypty zostały wyeksportowane pomyślnie.");
                            return 0;
                        }

                    case "update-db":
                        {
                            string connStr = GetArgValue(args, "--connection-string");
                            string scriptsDir = GetArgValue(args, "--scripts-dir");

                            UpdateDatabase(connStr, scriptsDir);
                            Console.WriteLine("Baza danych została zaktualizowana pomyślnie.");
                            return 0;
                        }

                    default:
                        Console.WriteLine($"Nieznane polecenie: {command}");
                        return 1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Błąd: " + ex.Message);
                return -1;
            }
        }

        private static string GetArgValue(string[] args, string name)
        {
            int idx = Array.IndexOf(args, name);
            if (idx == -1 || idx + 1 >= args.Length)
                throw new ArgumentException($"Brak wymaganego parametru {name}");
            return args[idx + 1];
        }

        /// <summary>
        /// Buduje nową bazę danych Firebird 5.0 na podstawie skryptów.
        /// </summary>
        public static void BuildDatabase(string databaseDirectory, string scriptsDirectory)
        {
            if (string.IsNullOrWhiteSpace(databaseDirectory))
                throw new ArgumentException("databaseDirectory nie może być pusty.");
            if (string.IsNullOrWhiteSpace(scriptsDirectory))
                throw new ArgumentException("scriptsDirectory nie może być pusty.");
            if (!Directory.Exists(scriptsDirectory))
                throw new DirectoryNotFoundException($"Nie znaleziono katalogu ze skryptami: {scriptsDirectory}");

            if (!Directory.Exists(databaseDirectory))
                Directory.CreateDirectory(databaseDirectory);

            string dbPath = Path.Combine(databaseDirectory, "new_database.fdb");

            if (File.Exists(dbPath))
                throw new IOException($"Plik bazy już istnieje: {dbPath}");

            string createConnString = $"User=SYSDBA;Password=masterkey;Database={dbPath};DataSource=localhost;Port=3050";
            FbConnection.CreateDatabase(createConnString);
            Console.WriteLine("Utworzono nową bazę danych: " + dbPath);

            using var conn = new FbConnection(createConnString);
            try
            {
                conn.Open();

                // 1. Domeny
                var domainFiles = Directory.GetFiles(scriptsDirectory, "domains*.sql");
                foreach (var file in domainFiles)
                {
                    Console.WriteLine("Tworzę domeny z: " + file);
                    ExecuteSqlFile(conn, file);
                }

                // 2. Tabele
                var tableFiles = Directory.GetFiles(scriptsDirectory, "tables*.sql");
                foreach (var file in tableFiles)
                {
                    Console.WriteLine("Tworzę tabele z: " + file);
                    ExecuteSqlFile(conn, file);
                }

                // 3. Procedury
                var procFiles = Directory.GetFiles(scriptsDirectory, "procedures*.sql");
                foreach (var file in procFiles)
                {
                    Console.WriteLine("Tworzę procedury z: " + file);
                    ExecuteSqlFile(conn, file);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd podczas tworzenia bazy: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Generuje skrypty metadanych z istniejącej bazy danych Firebird 5.0.
        /// </summary>
        public static void ExportScripts(string connectionString, string outputDirectory)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("connectionString nie może być pusty.");
            if (string.IsNullOrWhiteSpace(outputDirectory))
                throw new ArgumentException("outputDirectory nie może być pusty.");

            if (!Directory.Exists(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            using var conn = new FbConnection(connectionString);
            try
            {
                conn.Open();

                ExportDomains(conn, outputDirectory);
                ExportTables(conn, outputDirectory);
                ExportProcedures(conn, outputDirectory);

                Console.WriteLine("Eksport skryptów zakończony pomyślnie.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd podczas eksportu: {ex.Message}");
                throw;
            }
        }

        private static void ExportDomains(FbConnection conn, string outputDirectory)
        {       
            var domainsFile = Path.Combine(outputDirectory, "domains.sql");
            using (var sw = new StreamWriter(domainsFile, false))
            using (var cmd = new FbCommand(
                @"SELECT RDB$FIELD_NAME, RDB$FIELD_TYPE, RDB$CHARACTER_LENGTH, RDB$FIELD_SCALE, 
                RDB$FIELD_SUB_TYPE, RDB$FIELD_PRECISION
                FROM RDB$FIELDS
                WHERE RDB$SYSTEM_FLAG=0", conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    string domainName = reader.GetString(0).Trim();
                    int fieldType = Convert.ToInt32(reader["RDB$FIELD_TYPE"]);
                    int charLength = reader["RDB$CHARACTER_LENGTH"] != DBNull.Value ? Convert.ToInt32(reader["RDB$CHARACTER_LENGTH"]) : 0;
                    int scale = reader["RDB$FIELD_SCALE"] != DBNull.Value ? Convert.ToInt32(reader["RDB$FIELD_SCALE"]) : 0;
                    int subType = reader["RDB$FIELD_SUB_TYPE"] != DBNull.Value ? Convert.ToInt32(reader["RDB$FIELD_SUB_TYPE"]) : 0;
                    int precision = reader["RDB$FIELD_PRECISION"] != DBNull.Value ? Convert.ToInt32(reader["RDB$FIELD_PRECISION"]) : 0;

                    string sqlType = fieldType switch
                    {
                        7 => "SMALLINT",
                        8 => "INTEGER",
                        10 => "FLOAT",
                        12 => "DATE",
                        13 => "TIME",
                        14 => $"CHAR({charLength})",
                        16 when subType == 0 => scale < 0 ? $"BIGINT" : $"NUMERIC({precision},{Math.Abs(scale)})",
                        16 when subType == 1 => $"NUMERIC({precision},{Math.Abs(scale)})",
                        16 when subType == 2 => // DECIMAL
                            $"DECIMAL({(precision > 0 ? precision : Math.Abs(scale) + 1)},{Math.Abs(scale)})",
                        27 => "DOUBLE PRECISION",
                        35 => "TIMESTAMP",
                        37 => $"VARCHAR({charLength})",
                        261 => "BLOB SUB_TYPE 0",
                        _ => "UNKNOWN"
                    };

                    sw.WriteLine($"CREATE DOMAIN {domainName} AS {sqlType}");
                    sw.WriteLine("--@@END_OF_STATEMENT@@");
                }
            }
            Console.WriteLine("Eksport domen zapisany w: " + domainsFile);
        }

        private static void ExportTables(FbConnection conn, string outputDirectory)
        {
            var tablesFile = Path.Combine(outputDirectory, "tables.sql");
            using (var sw = new StreamWriter(tablesFile, false))
            using (var cmdTables = new FbCommand(
                @"SELECT RDB$RELATION_NAME 
                FROM RDB$RELATIONS 
                WHERE RDB$SYSTEM_FLAG=0 AND RDB$VIEW_BLR IS NULL", conn))
            using (var readerTables = cmdTables.ExecuteReader())
            {
                while (readerTables.Read())
                {
                    string tableName = readerTables.GetString(0).Trim();
                    sw.WriteLine($"CREATE TABLE {tableName} (");

                    using var cmdCols = new FbCommand(
                        @"SELECT RDB$FIELD_NAME, RDB$FIELD_SOURCE, RDB$FIELD_POSITION, RDB$NULL_FLAG
                        FROM RDB$RELATION_FIELDS
                        WHERE RDB$RELATION_NAME=@table
                        ORDER BY RDB$FIELD_POSITION", conn);
                    cmdCols.Parameters.AddWithValue("@table", tableName);
                    using var readerCols = cmdCols.ExecuteReader();

                    bool first = true;
                    while (readerCols.Read())
                    {
                        if (!first) sw.WriteLine(",");
                        first = false;

                        string colName = readerCols.GetString(0).Trim();
                        string domainName = readerCols.GetString(1).Trim();
                        bool notNull = readerCols["RDB$NULL_FLAG"] != DBNull.Value && Convert.ToInt32(readerCols["RDB$NULL_FLAG"]) == 1;

                        sw.Write($"  {colName} {domainName}");
                        if (notNull)
                            sw.Write(" NOT NULL");
                    }

                    sw.WriteLine();
                    sw.WriteLine(")");
                    sw.WriteLine("--@@END_OF_STATEMENT@@");
                    sw.WriteLine();
                }
            }
            Console.WriteLine("Eksport tabel zapisany w: " + tablesFile);
        }

        private static void ExportProcedures(FbConnection conn, string outputDirectory)
        {
            string sqlFilePath = Path.Combine(outputDirectory, "procedures.sql");
            using var writer = new StreamWriter(sqlFilePath, false);
            var procedures = new List<string>();
            using (var cmd = new FbCommand(
                "SELECT RDB$PROCEDURE_NAME FROM RDB$PROCEDURES WHERE RDB$SYSTEM_FLAG = 0", conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                    procedures.Add(reader.GetString(0).Trim());
            }

            foreach (var procName in procedures)
            {
                writer.WriteLine($"CREATE PROCEDURE {procName}");

                // Parametry wejściowe (IN)
                var inParams = new List<string>();
                using (var cmd = new FbCommand(
                    "SELECT RDB$PARAMETER_NAME, RDB$FIELD_SOURCE FROM RDB$PROCEDURE_PARAMETERS " +
                    "WHERE RDB$PROCEDURE_NAME = @proc AND RDB$PARAMETER_TYPE = 0 " +
                    "ORDER BY RDB$PARAMETER_NUMBER", conn))
                {
                    cmd.Parameters.AddWithValue("@proc", procName);
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        string paramName = reader.GetString(0).Trim();
                        string domainName = reader.GetString(1).Trim();
                        inParams.Add($"{paramName} {domainName}");
                    }
                }

                if (inParams.Count > 0)
                    writer.WriteLine($"({string.Join(", ", inParams)})");

                // Parametry wyjściowe (RETURNS)
                var outParams = new List<string>();
                using (var cmd = new FbCommand(
                    "SELECT RDB$PARAMETER_NAME, RDB$FIELD_SOURCE FROM RDB$PROCEDURE_PARAMETERS " +
                    "WHERE RDB$PROCEDURE_NAME = @proc AND RDB$PARAMETER_TYPE = 1 " +
                    "ORDER BY RDB$PARAMETER_NUMBER", conn))
                {
                    cmd.Parameters.AddWithValue("@proc", procName);
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        string paramName = reader.GetString(0).Trim();
                        string domainName = reader.GetString(1).Trim();
                        outParams.Add($"{paramName} {domainName}");
                    }
                }

                if (outParams.Count > 0)
                    writer.WriteLine("RETURNS (" + string.Join(", ", outParams) + ")");

                // Blok AS ... BEGIN ... END
                using (var cmd = new FbCommand(
                    "SELECT RDB$PROCEDURE_SOURCE FROM RDB$PROCEDURES WHERE RDB$PROCEDURE_NAME = @proc", conn))
                {
                    cmd.Parameters.AddWithValue("@proc", procName);
                    string source = cmd.ExecuteScalar()?.ToString() ?? "";
                    source = source.Replace("\r\n", "\n").Trim();
                    writer.WriteLine("AS");
                    writer.WriteLine(source);
                    writer.WriteLine("--@@END_OF_STATEMENT@@");
                }
            }

            writer.Flush();
            Console.WriteLine("Eksport procedur zakończony do jednego pliku procedures.sql");
        }

        /// <summary>
        /// Aktualizuje istniejącą bazę danych Firebird 5.0 na podstawie skryptów.
        /// </summary>
        public static void UpdateDatabase(string connectionString, string scriptsDirectory)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("connectionString nie może być pusty.");
            if (string.IsNullOrWhiteSpace(scriptsDirectory))
                throw new ArgumentException("scriptsDirectory nie może być pusty.");
            if (!Directory.Exists(scriptsDirectory))
                throw new DirectoryNotFoundException($"Nie znaleziono katalogu ze skryptami: {scriptsDirectory}");

            using var conn = new FbConnection(connectionString);
            try
            {
                conn.Open();

                // 1. Domeny
                var domainFiles = Directory.GetFiles(scriptsDirectory, "domains*.sql");
                foreach (var file in domainFiles)
                {
                    Console.WriteLine("Wykonuję skrypty domen z: " + file);
                    ExecuteSqlFile(conn, file);
                }

                // 2. Tabele
                var tableFiles = Directory.GetFiles(scriptsDirectory, "tables*.sql");
                foreach (var file in tableFiles)
                {
                    Console.WriteLine("Wykonuję skrypty tabel z: " + file);
                    ExecuteSqlFile(conn, file);
                }

                // 3. Procedury
                var procFiles = Directory.GetFiles(scriptsDirectory, "procedures*.sql");
                foreach (var file in procFiles)
                {
                    Console.WriteLine("Wykonuję skrypty procedur z: " + file);
                    ExecuteSqlFile(conn, file);
                }

                Console.WriteLine("Aktualizacja bazy zakończona pomyślnie.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd podczas aktualizacji: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Pomocnicza metoda wykonująca skrypt SQL z pliku.
        /// </summary>
        private static void ExecuteSqlFile(FbConnection conn, string filePath)
        {
            var commands = File.ReadAllText(filePath)
                    .Split(new[] { "--@@END_OF_STATEMENT@@" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(c => c.Trim())
                    .Where(c => !string.IsNullOrEmpty(c))
                    .ToArray();

            foreach (var cmdText in commands)
            {
                string trimmed = cmdText.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                using var cmd = new FbCommand(trimmed, conn);
                try
                {
                    cmd.ExecuteNonQuery();
                }
                catch (FbException ex)
                {
                    // Ignoruj błąd jeśli obiekt już istnieje
                    if (ex.ErrorCode == 335544351)
                        continue;

                    Console.WriteLine($"Błąd podczas wykonywania polecenia:\n{trimmed}\n{ex.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Błąd podczas wykonywania polecenia:\n{trimmed}\n{ex.Message}");
                }
            }
        }

    }
}
