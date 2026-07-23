using System;
using System.Configuration;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Text;
using ArchiveServices.Common.Managers.Enterprise;
using ArchiveServices.Common.Types.Archie;
using ArchiveServices.Common.Types.Response;
using ArchiveServices.Common.Utilities;
using NLog;
using NLog.Targets;

namespace ArchiveServices.Common.Managers.Archie
{
    public class RepoTableManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static SqlConnection GetArchRepoSqlConnection(int stepID, SqlConnection cnArchConfig, SqlConnection cnArchRepoMaster, SqlConnection cnDestination, OldArchiveTableConfig archiveTableConfig)
        {
            var cnArchRepo = new SqlConnection();
            try
            {
                // Physical Repo structure
                if (!CheckRepoDatabaseExists(cnArchRepoMaster, archiveTableConfig.RepoDatabaseName))
                {
                    CreateRepoDatabase(cnArchRepoMaster, archiveTableConfig.RepoDatabaseName);
                }
                SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(cnArchRepoMaster.ConnectionString);
                var repoServer = builder.DataSource;
                cnArchRepo.ConnectionString = ConnectionManager.GetSqlConnectionFromParts(repoServer, $"{archiveTableConfig.RepoDatabaseName}").ConnectionString;
                cnArchRepo.Open();
                if (!CheckRepoSchemaExists(cnArchRepo, archiveTableConfig))
                {
                    CreateRepoSchema(cnArchRepo, cnDestination, archiveTableConfig);
                }
                if (!CheckRepoTableExists(cnArchRepo, archiveTableConfig))
                {
                    CreateRepoTable(cnArchRepo, cnDestination, archiveTableConfig);
                }
                if (!CheckRepoTableIndexExists(cnArchRepo, archiveTableConfig))
                {
                    CreateRepoTableIndex(cnArchRepo, archiveTableConfig);
                }
                if (!CheckRepoLinkedServerExists(cnArchRepoMaster, archiveTableConfig))
                {
                    CreateRepoLinkedServer(cnArchRepoMaster, archiveTableConfig);
                }
                //if (!TableManager.CheckTargetLinkedServerExists(cnDestination, cnArchRepo, archiveTableConfig))
                //{
                //    TableManager.CreateTargetLinkedServer(cnDestination, cnArchRepo, archiveTableConfig);
                //}
                else
                {
                    // Check if the Repo table and the SF table have the same structure
                    if (!CheckRepoTableStructure(cnArchRepo, archiveTableConfig))
                    {
                        var validationError = new ValidationError($@"ERROR: {cnDestination.ConnectionString} Archie repo table structure does not match {archiveTableConfig.ServerQualifiedName} structure.", "Validation", "Fatal", true);
                        //validationErrors.Add(validationError);
                        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                        archiveTableConfig.ValidationStatus = validationError.Message;
                        Logger.Error(validationError.Message);
                    }
                    else
                    {
                        // Repo Ledger structure
                        if (!CheckRepoLedgerSchemaExists(cnArchRepo, archiveTableConfig))
                        {
                            CreateRepoLedgerSchema(cnArchRepo, cnDestination, archiveTableConfig);
                        }
                        // Check to see if the Ledger table and trigger exists; if not create
                        if (!CheckRepoLedgerTableExists(cnArchRepo, archiveTableConfig))
                        {
                            CreateRepoLedgerTable(cnArchRepo, cnDestination, archiveTableConfig);
                        }
                        // Check if the Ledger table has the same structure
                        if (!CheckRepoLedgerTableStructure(cnArchRepo, archiveTableConfig))
                        {
                            var validationError = new ValidationError($@"ERROR: {cnDestination.ConnectionString} Archie repo Ledger table structure does not match {archiveTableConfig.ServerQualifiedName} structure.", "Validation", "Fatal", true);
                            //validationErrors.Add(validationError);
                            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                            archiveTableConfig.ValidationStatus = validationError.Message;
                            Logger.Error(validationError.Message);
                        }
                        else
                        {
                            // Check to see if the Ledger table trigger exists; if not create
                            CreateRepoLedgerTableTrigger(cnArchRepo, archiveTableConfig);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnArchRepoMaster.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new ApplicationException($"ArchieRepositoryServer {cnArchRepoMaster.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return cnArchRepo;
        }

        public static SqlCommand GetLedgerTableTriggerInsertMetadata2()
        {
            var cmd = new SqlCommand();
            try
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = $@"
                    SELECT 
                        CASE WHEN [ORDINAL_POSITION] = 1 THEN ''
                             ELSE ','
                        END + 'i.[' + COLUMN_NAME + ']' AS 'FieldDef'
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE [TABLE_SCHEMA] = @schemaName
                          AND [TABLE_NAME] = @tableName
                    ORDER BY [ORDINAL_POSITION];
                ";
            }
            catch (Exception e)
            {
                throw new Exception(e.ToString());
            }
            cmd.Parameters.Add(new SqlParameter("@SchemaName", SqlDbType.VarChar));
            cmd.Parameters.Add(new SqlParameter("@TableName", SqlDbType.VarChar));
            return cmd;
        }

        public static SqlCommand GetLedgerTableTriggerInsertMetadata1()
        {
            var cmd = new SqlCommand();
            try
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = $@"
                    SELECT 
                        CASE WHEN [ORDINAL_POSITION] = 1 THEN ''
                             ELSE ','
                        END + '[' + COLUMN_NAME + ']' AS 'FieldDef'
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE [TABLE_SCHEMA] = @schemaName
                          AND [TABLE_NAME] = @tableName
                    ORDER BY [ORDINAL_POSITION];
                ";
            }
            catch (Exception e)
            {
                throw new Exception(e.ToString());
            }
            cmd.Parameters.Add(new SqlParameter("@SchemaName", SqlDbType.VarChar));
            cmd.Parameters.Add(new SqlParameter("@TableName", SqlDbType.VarChar));
            return cmd;
        }

        public static SqlCommand GetLedgerTableTriggerUpdateMetadata()
        {
            var cmd = new SqlCommand();
            try
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = $@"
                    SELECT 
                        CASE WHEN [ORDINAL_POSITION] = 1 THEN ''
                             ELSE ','
                        END + 'a.[' + COLUMN_NAME + '] = i.[' + COLUMN_NAME + ']' AS 'FieldDef'
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE [TABLE_SCHEMA] = @schemaName
                          AND [TABLE_NAME] = @tableName
                    ORDER BY [ORDINAL_POSITION];
                ";
            }
            catch (Exception e)
            {
                throw new Exception(e.ToString());
            }
            cmd.Parameters.Add(new SqlParameter("@SchemaName", SqlDbType.VarChar));
            cmd.Parameters.Add(new SqlParameter("@TableName", SqlDbType.VarChar));
            return cmd;
        }

        private static String BuildCreateLedgerTableTriggerString(SqlConnection cnArchRepo, OldArchiveTableConfig archiveTableConfig)
        {
            string str;
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($@"
                    CREATE OR ALTER TRIGGER [Ledger].[Trig_Insert_{archiveTableConfig.TableName}] ON [Ledger].{archiveTableConfig.TableName}
						AFTER INSERT
					AS 
					BEGIN 
						SET NOCOUNT ON;");
                sb.Append(Environment.NewLine);
                sb.Append($@"
                        INSERT INTO [{archiveTableConfig.RepoDatabaseName}].[{archiveTableConfig.SchemaName}].[{archiveTableConfig.TableName}]");
                sb.Append($@"
                    (");
                var cmdInsert1 = new SqlCommand();
                cmdInsert1 = GetLedgerTableTriggerInsertMetadata1();
                cmdInsert1.Parameters["@TableName"].Value = archiveTableConfig.TableName;
                cmdInsert1.Parameters["@SchemaName"].Value = archiveTableConfig.SchemaName;
                cmdInsert1.Connection = cnArchRepo;
                Logger.Info(Logging.DbCmdString(cmdInsert1));
                using (IDataReader reader = cmdInsert1.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        var fieldDef = CastDbNull.To(reader["FieldDef"], string.Empty).Trim();
                        sb.Append(fieldDef);
                    }
                    reader.Close();
                }
                sb.Append($@") ");
                sb.Append($@"
                        SELECT ");
                var cmdInsert2 = new SqlCommand();
                cmdInsert2 = GetLedgerTableTriggerInsertMetadata2();
                cmdInsert2.Parameters["@TableName"].Value = archiveTableConfig.TableName;
                cmdInsert2.Parameters["@SchemaName"].Value = archiveTableConfig.SchemaName;
                cmdInsert2.Connection = cnArchRepo;
                Logger.Info(Logging.DbCmdString(cmdInsert2));
                using (IDataReader reader = cmdInsert2.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        var fieldDef = CastDbNull.To(reader["FieldDef"], string.Empty).Trim();
                        sb.Append(fieldDef);
                    }
                    reader.Close();
                }
                sb.Append($@"
                        FROM INSERTED i
                        WHERE NOT EXISTS
                        (
                            SELECT 1
                            FROM [{archiveTableConfig.RepoDatabaseName}].[{archiveTableConfig.SchemaName}].[{archiveTableConfig.TableName}] a
                            WHERE ");
                // Build TargetKeyColumns collection
                var keyColumnList = archiveTableConfig.KeyColumn.Split(',').ToList();
                var i = 0;
                foreach (var keyColumn in keyColumnList)
                {
                    i++;
                    sb.Append($@"
                            i.[{keyColumn}] = a.[{keyColumn}] ");
                    if (i < keyColumnList.Count) sb.Append("AND ");
                }
                sb.Append($@")");
                sb.Append(Environment.NewLine);
                sb.Append($@"
                    END");
                str = sb.ToString();
                Logger.Info($@"str = {str}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {cnArchRepo.ConnectionString}  {e.Message}: {e.StackTrace}");
                str = "";
            }
            return str;
        }

        private static String BuildCreateLedgerTableTriggerStringMerge(SqlConnection cnArchRepo, OldArchiveTableConfig archiveTableConfig)
        {
            string str;
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($@"
                    CREATE OR ALTER TRIGGER [Ledger].[Trig_Insert_{archiveTableConfig.TableName}] ON [Ledger].{archiveTableConfig.TableName}
						AFTER INSERT
					AS 
					BEGIN 
						SET NOCOUNT ON;
                        MERGE [{archiveTableConfig.RepoDatabaseName}].[{archiveTableConfig.SchemaName}].[{archiveTableConfig.TableName}] AS PRD
                            USING 
                            ( SELECT ");
                var cmdUpdate = new SqlCommand();
                cmdUpdate = GetLedgerTableTriggerUpdateMetadata();
                cmdUpdate.Parameters["@TableName"].Value = archiveTableConfig.TableName;
                cmdUpdate.Parameters["@SchemaName"].Value = archiveTableConfig.SchemaName;
                cmdUpdate.Connection = cnArchRepo;
                Logger.Info(Logging.DbCmdString(cmdUpdate));
                using (IDataReader reader = cmdUpdate.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        var fieldDef = CastDbNull.To(reader["FieldDef"], string.Empty).Trim();
                        sb.Append(fieldDef);
                    }
                    reader.Close();
                }
                sb.Append($@" 
                        FROM INSERTED 
                        ) AS STG 
                        ON i.[{archiveTableConfig.KeyColumn}] = a.[{archiveTableConfig.KeyColumn}] 
                        INSERT INTO [{archiveTableConfig.RepoDatabaseName}].[{archiveTableConfig.SchemaName}].[{archiveTableConfig.TableName}] (");
                var cmdInsert1 = new SqlCommand();
                cmdInsert1 = GetLedgerTableTriggerInsertMetadata1();
                cmdInsert1.Parameters["@TableName"].Value = archiveTableConfig.TableName;
                cmdInsert1.Parameters["@SchemaName"].Value = archiveTableConfig.SchemaName;
                cmdInsert1.Connection = cnArchRepo;
                Logger.Info(Logging.DbCmdString(cmdInsert1));
                using (IDataReader reader = cmdInsert1.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        var fieldDef = CastDbNull.To(reader["FieldDef"], string.Empty).Trim();
                        sb.Append(fieldDef);
                    }
                    reader.Close();
                }
                sb.Append(") SELECT ");
                var cmdInsert2 = new SqlCommand();
                cmdInsert2 = GetLedgerTableTriggerInsertMetadata2();
                cmdInsert2.Parameters["@TableName"].Value = archiveTableConfig.TableName;
                cmdInsert2.Parameters["@SchemaName"].Value = archiveTableConfig.SchemaName;
                cmdInsert2.Connection = cnArchRepo;
                Logger.Info(Logging.DbCmdString(cmdInsert2));
                using (IDataReader reader = cmdInsert2.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        var fieldDef = CastDbNull.To(reader["FieldDef"], string.Empty).Trim();
                        sb.Append(fieldDef);
                    }
                    reader.Close();
                }
                sb.Append($@" FROM INSERTED i
                                LEFT OUTER JOIN [{archiveTableConfig.RepoDatabaseName}].[{archiveTableConfig.SchemaName}].[{archiveTableConfig.TableName}] a
                    ON i.[{archiveTableConfig.KeyColumn}] = a.[{archiveTableConfig.KeyColumn}] 
                    WHERE
                    a.[{archiveTableConfig.KeyColumn}] IS NULL

                    END
                ");
                str = sb.ToString();
                Logger.Info($@"str = {str}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {cnArchRepo.ConnectionString}  {e.Message}: {e.StackTrace}");
                str = "";
            }
            return str;
        }

        private static String BuildCreateLedgerTableTriggerStringOld(SqlConnection cnArchRepo, OldArchiveTableConfig archiveTableConfig)
        {
            string str;
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($@"CREATE OR ALTER TRIGGER [Ledger].[Trig_Insert_{archiveTableConfig.TableName}]
								   ON  [Ledger].{archiveTableConfig.TableName}
								   AFTER INSERT
								AS 
								BEGIN 
									SET NOCOUNT ON;
									UPDATE a SET ");
                var cmdUpdate = new SqlCommand();
                cmdUpdate = GetLedgerTableTriggerUpdateMetadata();
                cmdUpdate.Parameters["@TableName"].Value = archiveTableConfig.TableName;
                cmdUpdate.Parameters["@SchemaName"].Value = archiveTableConfig.SchemaName;
                cmdUpdate.Connection = cnArchRepo;
                Logger.Info(Logging.DbCmdString(cmdUpdate));
                using (IDataReader reader = cmdUpdate.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        var fieldDef = CastDbNull.To(reader["FieldDef"], string.Empty).Trim();
                        sb.Append(fieldDef);
                    }
                    reader.Close();
                }
                sb.Append($@" FROM [{archiveTableConfig.RepoDatabaseName}].[{archiveTableConfig.SchemaName}].[{archiveTableConfig.TableName}] a
							  INNER JOIN INSERTED i 
                                  ON i.[{archiveTableConfig.KeyColumn}] = a.[{archiveTableConfig.KeyColumn}] 

                        INSERT INTO [{archiveTableConfig.RepoDatabaseName}].[{archiveTableConfig.SchemaName}].[{archiveTableConfig.TableName}] (");
                var cmdInsert1 = new SqlCommand();
                cmdInsert1 = GetLedgerTableTriggerInsertMetadata1();
                cmdInsert1.Parameters["@TableName"].Value = archiveTableConfig.TableName;
                cmdInsert1.Parameters["@SchemaName"].Value = archiveTableConfig.SchemaName;
                cmdInsert1.Connection = cnArchRepo;
                Logger.Info(Logging.DbCmdString(cmdInsert1));
                using (IDataReader reader = cmdInsert1.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        var fieldDef = CastDbNull.To(reader["FieldDef"], string.Empty).Trim();
                        sb.Append(fieldDef);
                    }
                    reader.Close();
                }
                sb.Append(") SELECT ");
                var cmdInsert2 = new SqlCommand();
                cmdInsert2 = GetLedgerTableTriggerInsertMetadata2();
                cmdInsert2.Parameters["@TableName"].Value = archiveTableConfig.TableName;
                cmdInsert2.Parameters["@SchemaName"].Value = archiveTableConfig.SchemaName;
                cmdInsert2.Connection = cnArchRepo;
                Logger.Info(Logging.DbCmdString(cmdInsert2));
                using (IDataReader reader = cmdInsert2.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        var fieldDef = CastDbNull.To(reader["FieldDef"], string.Empty).Trim();
                        sb.Append(fieldDef);
                    }
                    reader.Close();
                }
                sb.Append($@" FROM INSERTED i
                              WHERE NOT EXISTS
                              (
                                  SELECT 1
                                  FROM [{archiveTableConfig.RepoDatabaseName}].[{archiveTableConfig.SchemaName}].[{archiveTableConfig.TableName}] a
                                  WHERE ");
                // Build TargetKeyColumns collection
                var keyColumnList = archiveTableConfig.KeyColumn.Split(',').ToList();
                var i = 0;
                foreach (var keyColumn in keyColumnList)
                {
                    i++;
                    sb.Append($@"i.[{keyColumn}] = a.[{keyColumn}] ");
                    sb.Append(Environment.NewLine);
                    if (i < keyColumnList.Count) sb.Append("AND ");
                }
                sb.Append(") ");
                sb.Append(" END");
                str = sb.ToString();
                //Logger.Info($@"str = {str}"); Logger.Info($@"str = {str}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {cnArchRepo.ConnectionString}  {e.Message}: {e.StackTrace}");
                str = "";
            }
            return str;
        }

        private static bool CreateRepoLedgerTableTrigger(SqlConnection cnArchRepo, OldArchiveTableConfig archiveTableConfig)
        {
            bool retVal = false;
            try
            {
                String str = BuildCreateLedgerTableTriggerString(cnArchRepo, archiveTableConfig);
                SqlCommand cmd = new SqlCommand(str, cnArchRepo);
                Logger.Info(Logging.DbCmdString(cmd));
                var x = cmd.ExecuteNonQuery();
                retVal = true;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {cnArchRepo.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        private static bool CheckRepoLedgerTableStructure(SqlConnection cnArchRepo, OldArchiveTableConfig archiveTableConfig)
        {
            bool retVal = false;
            try
            {
                string str = $@"
                    SELECT  ledg.[COLUMN_NAME]
                           ,ledg.[DATA_TYPE]
                           ,repo.[DATA_TYPE]
                    FROM    ( SELECT    *
                              FROM      {archiveTableConfig.ServerName}.{archiveTableConfig.DatabaseName}.information_schema.columns
                              WHERE     [TABLE_SCHEMA] = 'Ledger'
                                        AND [TABLE_NAME] = @TableNameTarg
                            ) ledg
                            FULL JOIN ( SELECT  *
                                        FROM    {archiveTableConfig.ServerName}.{archiveTableConfig.DatabaseName}.information_schema.COLUMNS
                                        WHERE   [TABLE_SCHEMA] = @SchemaNameRepo
                                                AND [TABLE_NAME] = @TableNameRepo
                                      ) repo ON repo.[COLUMN_NAME] = ledg.[COLUMN_NAME]
                    WHERE   repo.[DATA_TYPE] != ledg.[DATA_TYPE]";
                SqlCommand cmd = new SqlCommand(str, cnArchRepo);
                cmd.Parameters.Add("@SchemaNameTarg", SqlDbType.VarChar).Value = archiveTableConfig.SchemaName;
                cmd.Parameters.Add("@TableNameTarg", SqlDbType.VarChar).Value = archiveTableConfig.TableName;
                cmd.Parameters.Add("@SchemaNameRepo", SqlDbType.VarChar).Value = archiveTableConfig.SchemaName;
                cmd.Parameters.Add("@TableNameRepo", SqlDbType.VarChar).Value = archiveTableConfig.TableName;
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt32(cmd.ExecuteScalar()) == 0;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnArchRepo.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        public static SqlCommand GetTargetTableMetadata()
        {
            var cmd = new SqlCommand();
            try
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = $@"
                    SELECT '[' + c.name + '] [' + UPPER(t.name) + ']'
                           + CASE
                                 WHEN t.name IN ( 'bigint', 'bit', 'int', 'money', 'smallint', 'smallmoney', 'tinyint', 'float', 'real',
                                                  'date', 'datetime', 'datetime2', 'smalldatetime', 'datetimeoffset', 'time', 'image',
                                                  'uniqueidentifier', 'sysname', 'sql_variant', 'xml'
                                                ) THEN
                                     ''
                                 WHEN t.name IN ( 'varchar', 'char', 'text', 'binary', 'varbinary', 'nchar', 'nvarchar', 'ntext' ) THEN
                                     '(' + CASE WHEN c.max_length = -1 THEN 'MAX' else CONVERT(VARCHAR, c.max_length ) END + ')'
                                 WHEN t.name IN ( 'decimal', 'numeric' ) THEN
                                     '(' + CONVERT(VARCHAR, c.precision) + ', ' + CONVERT(VARCHAR, c.scale) + ')'
                                 ELSE
                                     '***'
                             END + ' NULL' + CASE
                                                 WHEN c.column_id = x.MaxColId THEN
                                                     ''
                                                 ELSE
                                                     ','
                                             END AS FieldDef,
                           c.column_id,
                           x.MaxColId,
                           c.max_length,
                           c.precision,
                           c.scale,
                           t.name,
                           t.schema_id,
                           t.max_length,
                           t.precision,
                           t.scale
                    FROM sys.columns c
                        INNER JOIN sys.types t
                            ON t.system_type_id = c.system_type_id
							AND t.user_type_id = c.user_type_id
	                    INNER JOIN sys.tables tb 
		                    ON TB.OBJECT_ID = c.object_id
						INNER JOIN sys.schemas s ON s.schema_id = tb.schema_id
                        OUTER APPLY
                    (
                        SELECT MAX(column_id) AS MaxColId
                        FROM sys.columns c2
                        WHERE c2.object_id = c.object_id
                    ) x
                    WHERE s.name = @SchemaName
                        AND tb.name = @TableName
                    ORDER BY c.column_id;
                ";
            }
            catch (Exception e)
            {
                throw new Exception(e.ToString());
            }
            cmd.Parameters.Add(new SqlParameter("@SchemaName", SqlDbType.VarChar));
            cmd.Parameters.Add(new SqlParameter("@TableName", SqlDbType.VarChar));
            return cmd;
        }

        private static String BuildCreateLedgerTableString(SqlConnection cnDestination, OldArchiveTableConfig archiveTableConfig)
        {
            string str;
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($@"
                    CREATE TABLE [Ledger].[{archiveTableConfig.TableName}] ");
                sb.Append($@"
                    (");
                var cmd = new SqlCommand();
                cmd = GetTargetTableMetadata();
                cmd.Parameters["@TableName"].Value = archiveTableConfig.TableName;
                cmd.Parameters["@SchemaName"].Value = archiveTableConfig.SchemaName;
                cmd.Connection = cnDestination;
                Logger.Info(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        var fieldDef = CastDbNull.To(reader["FieldDef"], string.Empty).Trim();
                        sb.Append(fieldDef);
                    }
                    reader.Close();
                }
                sb.Append($@"
                    )");
                str = sb.ToString();
                Logger.Info($@"str = {str}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
                str = "";
            }
            return str;
        }

        private static bool CreateRepoLedgerTable(SqlConnection cnArchRepo, SqlConnection cnDestination, OldArchiveTableConfig archiveTableConfig)
        {
            bool retVal = false;
            try
            {
                String str = BuildCreateLedgerTableString(cnDestination, archiveTableConfig);
                SqlCommand cmd = new SqlCommand(str, cnArchRepo);
                Logger.Info(Logging.DbCmdString(cmd));
                var x = cmd.ExecuteNonQuery();
                retVal = true;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {cnArchRepo.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        private static bool CheckRepoLedgerTableExists(SqlConnection cnArchRepo, OldArchiveTableConfig archiveTableConfig)
        {
            bool retVal = false;
            try
            {
                string str = $@"
                    SELECT 1
                    FROM sys.tables t
                    INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
                    WHERE s.name = @SchemaName
                    AND t.name = @TableName";
                SqlCommand cmd = new SqlCommand(str, cnArchRepo);
                cmd.Parameters.Add("@SchemaName", SqlDbType.VarChar).Value = "Ledger";
                cmd.Parameters.Add("@TableName", SqlDbType.VarChar).Value = archiveTableConfig.TableName;
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnArchRepo.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        private static bool CreateRepoLedgerSchema(SqlConnection cnArchRepo, SqlConnection cnDestinationstring, OldArchiveTableConfig archiveTableConfig)
        {
            bool retVal = false;
            try
            {
                string str = $@"
                    CREATE SCHEMA [Ledger]";
                SqlCommand cmd = new SqlCommand(str, cnArchRepo);
                Logger.Info(Logging.DbCmdString(cmd));
                cmd.ExecuteNonQuery();
                retVal = true;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {cnArchRepo.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        private static bool CheckRepoLedgerSchemaExists(SqlConnection cnArchRepo, OldArchiveTableConfig archiveTableConfig)
        {
            bool retVal = false;
            try
            {
                string str = $@"
                    SELECT 1 FROM sys.schemas WHERE name = @SchemaName";
                SqlCommand cmd = new SqlCommand(str, cnArchRepo);
                cmd.Parameters.Add("@SchemaName", SqlDbType.VarChar).Value = "Ledger";
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnArchRepo.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        private static bool CheckRepoTableStructure(SqlConnection cnArchRepo, OldArchiveTableConfig archiveTableConfig)
        {
            bool retVal = false;
            try
            {
                string str = $@"
                    SELECT  targ.[COLUMN_NAME]
                           ,targ.[DATA_TYPE]
                           ,repo.[DATA_TYPE]
                    FROM    ( SELECT    *
                              FROM      {archiveTableConfig.ServerName}.{archiveTableConfig.DatabaseName}.information_schema.columns
                              WHERE     [TABLE_SCHEMA] = @SchemaNameTarg
                                        AND [TABLE_NAME] = @TableNameTarg
                            ) targ
                            FULL JOIN ( SELECT  *
                                        FROM    {archiveTableConfig.RepoDatabaseName}.information_schema.COLUMNS
                                        WHERE   [TABLE_SCHEMA] = @SchemaNameRepo
                                                AND [TABLE_NAME] = @TableNameRepo
                                      ) repo ON repo.[COLUMN_NAME] = targ.[COLUMN_NAME]
                    WHERE   repo.[DATA_TYPE] != targ.[DATA_TYPE]";
                SqlCommand cmd = new SqlCommand(str, cnArchRepo);
                cmd.Parameters.Add("@SchemaNameTarg", SqlDbType.VarChar).Value = archiveTableConfig.SchemaName;
                cmd.Parameters.Add("@TableNameTarg", SqlDbType.VarChar).Value = archiveTableConfig.TableName;
                cmd.Parameters.Add("@SchemaNameRepo", SqlDbType.VarChar).Value = archiveTableConfig.SchemaName;
                cmd.Parameters.Add("@TableNameRepo", SqlDbType.VarChar).Value = archiveTableConfig.TableName;
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt32(cmd.ExecuteScalar()) == 0;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnArchRepo.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        private static bool CreateRepoLinkedServer(SqlConnection cnArchRepoMaster, OldArchiveTableConfig archiveTableConfig)
        {
            bool retVal = false;
            try
            {
                string str = $@"
                    EXEC sp_addlinkedserver @server=N'{archiveTableConfig.ServerName}'";
                SqlCommand cmd = new SqlCommand(str, cnArchRepoMaster);
                //cmd.Parameters.Add("@SchemaName", SqlDbType.VarChar).Value = archiveTableConfig.SchemaName;
                Logger.Info(Logging.DbCmdString(cmd));
                cmd.ExecuteNonQuery();
                retVal = true;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {cnArchRepoMaster.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        private static bool CheckRepoLinkedServerExists(SqlConnection cnArchRepoMaster, OldArchiveTableConfig archiveTableConfig)
        {
            bool retVal = false;
            try
            {
                string str = $@"
                    SELECT 1 FROM sys.servers WHERE Name = @TargetServerName";
                SqlCommand cmd = new SqlCommand(str, cnArchRepoMaster);
                cmd.Parameters.Add("@TargetServerName", SqlDbType.VarChar).Value = archiveTableConfig.ServerName;
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnArchRepoMaster.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        private static String BuildCreateTableIndexString(SqlConnection cnDestination, OldArchiveTableConfig archiveTableConfig)
        {
            string str;
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($@"
                    CREATE TABLE [{archiveTableConfig.SchemaName}].[{archiveTableConfig.TableName}] ");
                sb.Append($@"
                    (");
                var cmd = new SqlCommand();
                cmd = GetTargetTableMetadata();
                cmd.Parameters["@TableName"].Value = archiveTableConfig.TableName;
                cmd.Parameters["@SchemaName"].Value = archiveTableConfig.SchemaName;
                cmd.Connection = cnDestination;
                Logger.Info(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        var fieldDef = CastDbNull.To(reader["FieldDef"], string.Empty).Trim();
                        sb.Append(fieldDef);
                    }
                    reader.Close();
                }
                sb.Append($@"
                    )");
                str = sb.ToString();
                Logger.Info($@"str = {str}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
                str = "";
            }
            return str;
        }

        private static bool CreateRepoTableIndex(SqlConnection cnArchRepoMaster, OldArchiveTableConfig archiveTableConfig)
        {
            bool retVal = false;
            string str;
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($@"
                    CREATE NONCLUSTERED INDEX [IX_Archie] ON [{archiveTableConfig.SchemaName}].[{archiveTableConfig.TableName}]");
                sb.Append(Environment.NewLine);
                sb.Append($@"
                    (");
                sb.Append(Environment.NewLine);
                // Build TargetKeyColumns collection
                var keyColumnList = archiveTableConfig.KeyColumn.Split(',').ToList();
                var i = 0;
                foreach (var keyColumn in keyColumnList)
                {
                    i++;
                    sb.Append($@"[{keyColumn}] ASC ");
                    if (i < keyColumnList.Count) sb.Append(", ");
                    sb.Append(Environment.NewLine);
                }
                sb.Append($@"
                    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]");
                str = sb.ToString();
                SqlCommand cmd = new SqlCommand(str, cnArchRepoMaster);
                cmd.CommandTimeout = 300;
                Logger.Info(Logging.DbCmdString(cmd));
                var x = cmd.ExecuteNonQuery();
                retVal = true;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {cnArchRepoMaster.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        private static bool CheckRepoTableIndexExists(SqlConnection cnArchRepo, OldArchiveTableConfig archiveTableConfig)
        {
            bool retVal = false;
            try
            {
                string str = $@"
                    SELECT 1
                    FROM SYS.INDEXES
                    WHERE OBJECT_ID = OBJECT_ID('[{archiveTableConfig.SchemaName}].[{archiveTableConfig.TableName}]')
                    AND [NAME] = 'IX_Archie' ";
                SqlCommand cmd = new SqlCommand(str, cnArchRepo);
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnArchRepo.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        private static String BuildCreateTableString(SqlConnection cnDestination, OldArchiveTableConfig archiveTableConfig)
        {
            string str;
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($@"CREATE TABLE [{archiveTableConfig.SchemaName}].[{archiveTableConfig.TableName}] ");
                sb.Append(" ( ");
                var cmd = new SqlCommand();
                cmd = GetTargetTableMetadata();
                cmd.Parameters["@TableName"].Value = archiveTableConfig.TableName;
                cmd.Parameters["@SchemaName"].Value = archiveTableConfig.SchemaName;
                cmd.Connection = cnDestination;
                Logger.Info(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        var fieldDef = CastDbNull.To(reader["FieldDef"], string.Empty).Trim();
                        sb.Append(fieldDef);
                    }
                    reader.Close();
                }
                sb.Append(")");
                str = sb.ToString();
                Logger.Info($@"str = {str}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
                str = "";
            }
            return str;
        }

        private static bool CreateRepoTable(SqlConnection cnArchRepo, SqlConnection cnDestination, OldArchiveTableConfig archiveTableConfig)
        {
            bool retVal = false;
            try
            {
                String str = BuildCreateTableString(cnDestination, archiveTableConfig);
                SqlCommand cmd = new SqlCommand(str, cnArchRepo);
                Logger.Info(Logging.DbCmdString(cmd));
                var x = cmd.ExecuteNonQuery();
                retVal = true;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {cnArchRepo.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        private static bool CheckRepoTableExists(SqlConnection cnArchRepo, OldArchiveTableConfig archiveTableConfig)
        {
            bool retVal = false;
            try
            {
                string str = string.Format($@"
                    SELECT 1
                    FROM sys.tables t
                    INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
                    WHERE s.name = @SchemaName
                    AND t.name = @TableName 
                ");
                SqlCommand cmd = new SqlCommand(str, cnArchRepo);
                cmd.Parameters.Add("@SchemaName", SqlDbType.VarChar).Value = archiveTableConfig.SchemaName;
                cmd.Parameters.Add("@TableName", SqlDbType.VarChar).Value = archiveTableConfig.TableName;
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnArchRepo.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        private static bool CreateRepoSchema(SqlConnection cnArchRepo, SqlConnection cnDestinationstring, OldArchiveTableConfig archiveTableConfig)
        {
            bool retVal = false;
            try
            {
                string str = $@"
                    CREATE SCHEMA [{archiveTableConfig.SchemaName}]";
                SqlCommand cmd = new SqlCommand(str, cnArchRepo);
                Logger.Info(Logging.DbCmdString(cmd));
                cmd.ExecuteNonQuery();
                retVal = true;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {cnArchRepo.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        private static bool CheckRepoSchemaExists(SqlConnection cnArchRepo, OldArchiveTableConfig archiveTableConfig)
        {
            bool retVal = false;
            try
            {
                string str = $@"
                    SELECT 1 FROM sys.schemas WHERE name = @SchemaName";
                SqlCommand cmd = new SqlCommand(str, cnArchRepo);
                cmd.Parameters.Add("@SchemaName", SqlDbType.VarChar).Value = archiveTableConfig.SchemaName;
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnArchRepo.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        private static bool CreateRepoDatabase(SqlConnection cnArchRepoMaster, string repoDBName)
        {
            bool retVal = false;
            try
            {
                string str = $@"
                    CREATE DATABASE [{repoDBName}]";
                SqlCommand cmd = new SqlCommand(str, cnArchRepoMaster);
                Logger.Info(Logging.DbCmdString(cmd));
                var x = cmd.ExecuteNonQuery();
                retVal = true;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {cnArchRepoMaster.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        private static bool CheckTargetTableExists(SqlConnection cnDestination, OldArchiveTableConfig archiveTableConfig)
        {
            bool retVal = false;
            try
            {
                string str = $@"
                    SELECT 1 
                    FROM sys.tables t
                    INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
                    WHERE s.name = @SchemaName 
                    AND t.name = @TableName ";
                SqlCommand cmd = new SqlCommand(str, cnDestination);
                cmd.Parameters.Add("@SchemaName", SqlDbType.VarChar).Value = archiveTableConfig.SchemaName;
                cmd.Parameters.Add("@TableName", SqlDbType.VarChar).Value = archiveTableConfig.TableName;
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        private static bool CheckRepoDatabaseExists(SqlConnection cnArchRepoMaster, string repoDBName)
        {
            bool retVal = false;
            try
            {
                string str = $@"
                    SELECT 1 FROM sys.databases WHERE Name = @RepoDBName";
                SqlCommand cmd = new SqlCommand(str, cnArchRepoMaster);
                cmd.Parameters.Add("@RepoDBName", SqlDbType.VarChar).Value = repoDBName;
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnArchRepoMaster.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        public static long GetRowCount(SqlConnection cnDestination, OldArchiveTableConfig archiveTableConfig, string whereClause = null)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            long rowCount;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnDestination,
                    CommandType = CommandType.Text,
                    CommandText = $@"
                        SELECT COUNT(*) 
                        FROM {archiveTableConfig.FullyQualifiedName} WITH (NOLOCK) 
                        {whereClause}"
                };
                Logger.Info(Logging.DbCmdString(cmd));
                rowCount = Convert.ToInt64(cmd.ExecuteScalar());
                Logger.Info("ROWS: " + rowCount);
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return rowCount;
        }

        public static bool CheckServerExists(SqlConnection cnDestination, Table targetTable)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            var isExist = false;
            var rowCount = 0;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnDestination,
                    CommandType = CommandType.Text,
                    CommandText = $@"
                       SELECT [name]
                        FROM  sys.databases
                        WHERE [name] = '{targetTable.DatabaseName}' 
                        "
                };
                Logger.Trace(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        isExist = true;
                        rowCount += 1;
                    }

                    reader.Close();
                    Logger.Trace("ROWS: " + rowCount);
                }
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }

            return isExist;
        }

        public static bool CheckDatabaseExists(SqlConnection cnDestination, Table targetTable)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            var isExist = false;
            var rowCount = 0;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnDestination,
                    CommandType = CommandType.Text,
                    CommandText = $@"
                       SELECT [name]
                        FROM  sys.databases
                        WHERE [name] = '{targetTable.DatabaseName}' 
                        "
                };
                Logger.Trace(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        isExist = true;
                        rowCount += 1;
                    }

                    reader.Close();
                    Logger.Trace("ROWS: " + rowCount);
                }
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }

            return isExist;
        }

        public static bool CheckSchemaExists(SqlConnection cnDestination, Table targetTable)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            var isExist = false;
            var rowCount = 0;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnDestination,
                    CommandType = CommandType.Text,
                    CommandText = $@"
                       SELECT [TABLE_SCHEMA]
                        FROM  [{targetTable.DatabaseName}].[INFORMATION_SCHEMA].[TABLES]
                        WHERE [TABLE_CATALOG] = '{targetTable.DatabaseName}' AND
                              [TABLE_SCHEMA] = '{targetTable.SchemaName}' 
                        "
                };
                Logger.Trace(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        isExist = true;
                        rowCount += 1;
                    }

                    reader.Close();
                    Logger.Trace("ROWS: " + rowCount);
                }
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }

            return isExist;
        }

        public static bool CheckTableExists(SqlConnection cnDestination, Table targetTable)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            var isExist = false;
            var rowCount = 0;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnDestination,
                    CommandType = CommandType.Text,
                    CommandText = $@"
                       SELECT [TABLE_NAME]
                        FROM  [{targetTable.DatabaseName}].[INFORMATION_SCHEMA].[TABLES]
                        WHERE [TABLE_CATALOG] = '{targetTable.DatabaseName}' AND
                              [TABLE_SCHEMA] = '{targetTable.SchemaName}' AND 
                              [TABLE_NAME] = '{targetTable.TableName}' 
                        "
                };
                Logger.Trace(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        isExist = true;
                        rowCount += 1;
                    }

                    reader.Close();
                    Logger.Trace("ROWS: " + rowCount);
                }
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }

            return isExist;
        }

        public static long CountTableRows(SqlConnection cnDestination, Table targetTable)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            long rowCount;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnDestination,
                    CommandType = CommandType.Text,
                    CommandText = $@"
                        SELECT  COUNT(*)
                        FROM    [{targetTable.SchemaName}].[{targetTable.TableName}]"
                };
                Logger.Trace(Logging.DbCmdString(cmd));
                rowCount = (long)cmd.ExecuteScalar();
                Logger.Trace($@"ROWS: {rowCount}");
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return rowCount;
        }
        public static bool CheckTriggerExists(SqlConnection cnDestination, Table targetTable)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            var isExist = false;
            var rowCount = 0;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnDestination,
                    CommandType = CommandType.Text,
                    CommandText = $@"
                       select *
                        from sys.triggers
                        where object_id = object_id(N'[Ledger].[Trig_Insert_{targetTable.TableName}]')"
                };
                Logger.Trace(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        isExist = true;
                        rowCount += 1;
                    }

                    reader.Close();
                    Logger.Trace("ROWS: " + rowCount);
                }
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }

            return isExist;
        }

    }
}
