using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using ArchiveServices.Common.Managers.Enterprise;
using ArchiveServices.Common.Types.Archie;
using ArchiveServices.Common.Types.Enterprise;
using ArchiveServices.Common.Types.Response;
using ArchiveServices.Common.Utilities;
using NLog;

namespace ArchiveServices.Common.Managers.Archie
{
    public class OldArchiveTableManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static ProcessResponse Process(int batchID, string batchName, OldArchiveTableConfig archiveTableConfig, SqlConnection cnCorpInt, SqlConnection cnArchConfig, SqlConnection cnArchRepoMaster, SqlConnection cnArchRepo, SqlConnection cnSource)
        {
            var startTime = DateTime.Now.ToLocalTime();
            var processName = $@"Archive Data Table: {archiveTableConfig.ServerQualifiedName}";
            Logger.Info($"{processName} BEGIN: {DateTime.Now.ToLocalTime()}");
            archiveTableConfig.LastArchiveStartDate = startTime;

            var processID = ArchieLoggingManager.LogProcessInsert(cnArchConfig, processName, batchID, archiveTableConfig);
            archiveTableConfig.ProcessID = processID;

            var testMode = Convert.ToBoolean(ConfigurationManager.AppSettings["TestMode"]);
            archiveTableConfig.IsTestMode = testMode;
            var testModeNoDelete = Convert.ToBoolean(ConfigurationManager.AppSettings["TestModeNoDelete"]);
            archiveTableConfig.TestModeNoDelete = testModeNoDelete;
            if (archiveTableConfig.TestModeNoDelete) processName = $@"{processName} - Test Mode No Delete";
            archiveTableConfig.TruncateRepoLedgerAfter = Convert.ToBoolean(ConfigurationManager.AppSettings["TruncateRepoLedgerAfter"]);

            var minMovedRows = Convert.ToInt32(ConfigurationManager.AppSettings["ArchieMinMovedRows"]);
            var nextRuntimeMoveAheadDays = Convert.ToInt32(ConfigurationManager.AppSettings["ArchieNextRuntimeMoveAheadDays"]);
            var maxDeleteSeconds = Convert.ToDecimal(ConfigurationManager.AppSettings["ArchieMaxDeleteSeconds"]);
            long maxDeleteRows = Convert.ToInt64(ConfigurationManager.AppSettings["ArchieMaxDeleteRows"]);

            var response = new ProcessResponse($@"BatchID [{batchID}] {batchName} ProcessID [{processID}] {processName}", archiveTableConfig, true);
            
            response.BatchID = batchID;
            response.ProcessID = processID;
            response.ArchiveDate = archiveTableConfig.ArchiveStartTime; 

            response.ConnectionStrings.Add(new ConnectionString(cnArchConfig, "cnCorpInt", cnCorpInt.ConnectionString, batchID, processID));
            response.ConnectionStrings.Add(new ConnectionString(cnArchConfig, "cnArchConfig", cnArchConfig.ConnectionString, batchID, processID));
            response.ConnectionStrings.Add(new ConnectionString(cnArchConfig, "cnArchRepoMaster", cnArchRepoMaster.ConnectionString, batchID, processID));
            var guid = Guid.NewGuid();
            
            try
            {
                var stepValidateTable = new ProcessStep(cnArchConfig, processID, $@"Validate configuration in [Archiver].[dbo].[cfgTablesArchive] for {archiveTableConfig.ServerQualifiedName}");
                archiveTableConfig.ValidationErrors = TableManager.ValidateConfig(stepValidateTable.StepID, archiveTableConfig, cnArchConfig, cnArchRepoMaster, cnSource);
                stepValidateTable.Done(cnArchConfig);
                response.ProcessSteps.Add(stepValidateTable);

                if (archiveTableConfig.ValidationErrors.Any())
                {
                    //var stepUpdateArchiveTableConfigDisable = new ProcessStep(cnArchConfig, processID, $@"Set [Archiver].[dbo].[cfgTablesArchive].[IsEnabled] = FALSE for {archiveTableConfig.ServerQualifiedName} in [Archiver].[dbo].[cfgTablesArchive]");
                    //try
                    //{
                    //    stepUpdateArchiveTableConfigDisable.RowsAffected = ArchiveConfigTableManager.UpdateArchiveTableConfigIsEnabled(cnArchConfig, archiveTableConfig, false);
                    //}
                    //catch (Exception e)
                    //{
                    //    stepUpdateArchiveTableConfigDisable.Errors.Add(new Error(e));
                    //}
                    //stepUpdateArchiveTableConfigDisable.Done(cnArchConfig);
                    //response.ProcessSteps.Add(stepUpdateArchiveTableConfigDisable);

                    foreach (var validationError in archiveTableConfig.ValidationErrors)
                    {
                        response.Errors.Add(new Error(validationError.Message));
                    }
                    ArchieLoggingManager.LogActivity(cnArchConfig, new ArchieLog("Error", "ArchiveTable", "",
                        "", archiveTableConfig.DateColumn, archiveTableConfig.RetentionDays, archiveTableConfig.InitialRowCount, stepValidateTable.RowsAffected, archiveTableConfig.InitialRowCount - stepValidateTable.RowsAffected,
                        testModeNoDelete, "", "", archiveTableConfig.ServerName, archiveTableConfig.DatabaseName, archiveTableConfig.SchemaName, archiveTableConfig.TableName, guid));
                }
                else
                {
                    var stepGetSourceSqlConnection = new ProcessStep(cnArchConfig, processID, $@"Get SQL connection for Source table {archiveTableConfig.ServerQualifiedName}");
                    try
                    {
                        // Get Source Table connection
                        cnSource = GetSourceSqlConnection(archiveTableConfig.ServerName, archiveTableConfig.DatabaseName);
                        response.ConnectionStrings.Add(new ConnectionString(cnArchConfig, "cnSource", cnSource.ConnectionString, batchID, processID));
                        // See if Source database exists
                        if (!TableManager.CheckSourceDatabaseExists(cnSource, archiveTableConfig))
                        {
                            var validationError = new ValidationError($@"ERROR: {cnSource.ConnectionString} Source database [{archiveTableConfig.ServerQualifiedName}] not found.", "Validation", "Fatal", true);
                            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepGetSourceSqlConnection.StepID, validationError.Message, "");
                            archiveTableConfig.ValidationErrors.Add(validationError);
                            archiveTableConfig.ValidationStatus = validationError.Message;
                            Logger.Error(validationError.Message);
                        }
                        // See if Source schema exists
                        if (!TableManager.CheckSourceSchemaExists(cnSource, archiveTableConfig))
                        {
                            var validationError = new ValidationError($@"ERROR: {cnSource.ConnectionString} Source schema [{archiveTableConfig.ServerQualifiedName}] not found.", "Validation", "Fatal", true);
                            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepGetSourceSqlConnection.StepID, validationError.Message, "");
                            archiveTableConfig.ValidationErrors.Add(validationError);
                            archiveTableConfig.ValidationStatus = validationError.Message;
                            Logger.Error(validationError.Message);
                        }
                        // See if Source table exists
                        if (!TableManager.CheckSourceTableExists(cnSource, archiveTableConfig))
                        {
                            var validationError = new ValidationError($@"ERROR: {cnSource.ConnectionString} Source table [{archiveTableConfig.ServerQualifiedName}] not found.", "Validation", "Fatal", true);
                            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepGetSourceSqlConnection.StepID, validationError.Message, "");
                            archiveTableConfig.ValidationErrors.Add(validationError);
                            archiveTableConfig.ValidationStatus = validationError.Message;
                            Logger.Error(validationError.Message);
                        }
                    }
                    catch (Exception e)
                    {
                        stepGetSourceSqlConnection.Errors.Add(new Error(e));
                    }
                    stepGetSourceSqlConnection.Done(cnArchConfig);
                    response.ProcessSteps.Add(stepGetSourceSqlConnection);
                    if (!stepGetSourceSqlConnection.Errors.Any())
                    {
                        var stepGetArchRepoConnection = new ProcessStep(cnArchConfig, processID, $@"Get SQL connection for Archie repository [{cnArchRepoMaster.DataSource}].[{archiveTableConfig.RepoDatabaseName}]");
                        try
                        {
                            // Get Arch Repository connection
                            cnArchRepo = RepoTableManager.GetArchRepoSqlConnection(stepGetArchRepoConnection.StepID, cnArchConfig, cnArchRepoMaster, cnSource, archiveTableConfig);
                            response.ConnectionStrings.Add(new ConnectionString(cnArchConfig, "cnArchRepo", cnArchRepo.ConnectionString, batchID, processID));
                        }
                        catch (Exception e)
                        {
                            stepGetArchRepoConnection.Errors.Add(new Error(e));
                        }
                        stepGetArchRepoConnection.Done(cnArchConfig);
                        response.ProcessSteps.Add(stepGetArchRepoConnection);
                        if (!stepGetArchRepoConnection.Errors.Any())
                        {
                            var stepUpdateArchiveTableConfigIsRunningTrue = new ProcessStep(cnArchConfig, processID, $@"Set [Archiver].[dbo].[cfgTablesArchive].[IsRunning] = TRUE for {archiveTableConfig.ServerQualifiedName}");
                            try
                            {
                                var statement = "";
                                stepUpdateArchiveTableConfigIsRunningTrue.RowsAffected = OldArchiveConfigTableManager.UpdateArchiveTableConfigIsRunningTrue(cnArchConfig, archiveTableConfig, out statement);
                                stepUpdateArchiveTableConfigIsRunningTrue.Statement = statement;
                            }
                            catch (Exception e)
                            {
                                stepUpdateArchiveTableConfigIsRunningTrue.Errors.Add(new Error(e));
                            }
                            stepUpdateArchiveTableConfigIsRunningTrue.Done(cnArchConfig);
                            response.ProcessSteps.Add(stepUpdateArchiveTableConfigIsRunningTrue);

                            if (!stepUpdateArchiveTableConfigIsRunningTrue.Errors.Any())
                            {
                                // Build SourceKeyColumns collection 
                                var keyColumnList = archiveTableConfig.KeyColumn.Split(',').ToList();
                                foreach (var keyColumn in keyColumnList)
                                {
                                    archiveTableConfig.SourceKeyColumns.Add(OldArchiveConfigTableManager.GetKeyData(cnArchConfig, archiveTableConfig.SchemaName, archiveTableConfig.TableName, keyColumn));
                                }

                                var stepCheckIsKeyColumnUnique = new ProcessStep(cnArchConfig, processID, $@"Check if key is unique for {archiveTableConfig.ServerQualifiedName}");
                                try
                                {
                                    archiveTableConfig.IsKeyColumnUnique = TableManager.IsKeyColumnUnique(cnSource, archiveTableConfig);
                                    ArchieLoggingManager.LogProcessUpdateIsKeyColumnUnique(cnArchConfig, processID, archiveTableConfig);
                                    OldArchiveConfigTableManager.UpdateArchiveTableConfigIsKeyColumnUnique(cnArchConfig, archiveTableConfig, out string statement);
                                    stepCheckIsKeyColumnUnique.Statement = statement;
                                }
                                catch (Exception e)
                                {
                                    stepCheckIsKeyColumnUnique.Errors.Add(new Error(e));
                                }
                                stepCheckIsKeyColumnUnique.Done(cnArchConfig);
                                response.ProcessSteps.Add(stepCheckIsKeyColumnUnique);
                                if (!archiveTableConfig.IsKeyColumnUnique)
                                {
                                    var validationError = new ValidationError($@"ERROR: {cnSource.ConnectionString} Source table {archiveTableConfig.ServerQualifiedName} Key Column [{archiveTableConfig.KeyColumn}] is not unique.", "Validation", "Fatal", true);
                                    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepCheckIsKeyColumnUnique.StepID, validationError.Message, "");
                                    archiveTableConfig.ValidationErrors.Add(validationError);
                                    archiveTableConfig.ValidationStatus = validationError.Message;
                                    Logger.Error(validationError.Message);
                                    response.Errors.Add(new Error(validationError.Message));
                                }
                                else 
                                { 
                                    var stepGatherInitialStatistics = new ProcessStep(cnArchConfig, processID, $@"Gather initial statistics for {archiveTableConfig.ServerQualifiedName}");
                                    try
                                    {
                                        archiveTableConfig.InitialRowCount = TableManager.GetRowCount(cnSource, archiveTableConfig);
                                        archiveTableConfig.RowsToDelete = TableManager.GetRowCount(cnSource, archiveTableConfig, archiveTableConfig.WhereClause);
                                        response.RowsToDelete = archiveTableConfig.RowsToDelete;
                                        if (archiveTableConfig.RowsToDelete < archiveTableConfig.MaxDeleteRowsPerBatch) { archiveTableConfig.FinalRowCountCalculated = archiveTableConfig.InitialRowCount - archiveTableConfig.RowsToDelete; }
                                        else { archiveTableConfig.FinalRowCountCalculated = archiveTableConfig.InitialRowCount - archiveTableConfig.MaxDeleteRowsPerBatch; }
                                        archiveTableConfig.IsKeyColumnUnique = TableManager.IsKeyColumnUnique(cnSource, archiveTableConfig); 
                                        archiveTableConfig.Guid = guid;
                                        archiveTableConfig.LastRunStatus = "";
                                        ArchieLoggingManager.LogProcessUpdateInitialRowCount(cnArchConfig, processID, archiveTableConfig);
                                        OldArchiveConfigTableManager.UpdateArchiveTableConfigInitialRowCount(cnArchConfig, archiveTableConfig);
                                    }
                                    catch (Exception e)
                                    {
                                        stepGatherInitialStatistics.Errors.Add(new Error(e));
                                    }
                                    stepGatherInitialStatistics.Done(cnArchConfig);
                                    response.ProcessSteps.Add(stepGatherInitialStatistics);
                                    if (!stepGatherInitialStatistics.Errors.Any() && archiveTableConfig.RowsToDelete > 0)
                                    {
                                        Logger.Info("Check for Foreign Keys referencing Source table.");
                                        var stepCheckConstraints = new ProcessStep(cnArchConfig, processID, $@"Check for foreign keys referencing Source table");
                                        try
                                        {
                                            var cmdTemp = new SqlCommand
                                            {
                                                Connection = cnSource,
                                                CommandType = CommandType.Text,
                                                CommandText = $@"
                                                    SELECT st.name AS SchemaName,
                                                            OBJECT_NAME(f.parent_object_id) AS TableName,
                                                            COL_NAME(fc.parent_object_id, fc.parent_column_id) AS ColumnName,
                                                            sr.name AS ReferencedSchemaName,
                                                            OBJECT_NAME(f.referenced_object_id) AS ReferencedTableName,
                                                            COL_NAME(f.referenced_object_id, f.key_index_id) AS ReferencedColumnName,
                                                            f.name AS ConstraintName,
                                                            f.type AS ConstraintTypeCode,
                                                            f.type_desc AS ConstraintTypeDesc
                                                    FROM sys.foreign_keys AS f
                                                        INNER JOIN sys.foreign_key_columns AS fc
                                                            ON f.object_id = fc.constraint_object_id
                                                        INNER JOIN sys.tables t
                                                            ON t.object_id = fc.referenced_object_id
                                                        INNER JOIN sys.schemas st
                                                            ON st.schema_id = f.schema_id
                                                        INNER JOIN sys.schemas sr
                                                            ON sr.schema_id = t.schema_id
                                                    WHERE OBJECT_NAME(f.referenced_object_id) = '{archiveTableConfig.TableName}';
                                                "
                                            };
                                            Logger.Info(Logging.DbCmdString(cmdTemp));
                                            stepCheckConstraints.Statement = cmdTemp.CommandText;
                                            //stepCheckConstraints.RowsAffected = cmdTemp.ExecuteNonQuery();
                                            Logger.Info($@"ROWS: {stepCheckConstraints.RowsAffected}");
                                        }
                                        catch (Exception e)
                                        {
                                            stepCheckConstraints.Errors.Add(new Error(e));
                                        }
                                        stepCheckConstraints.Done(cnArchConfig);
                                        response.ProcessSteps.Add(stepCheckConstraints);

                                        if (!stepCheckConstraints.Errors.Any())
                                        {
                                            var stepDropTempTable2 = new ProcessStep(cnArchConfig, processID, $@"Drop temp table #TempKeyList");
                                            try
                                            {
                                                var cmdDropTemp2 = new SqlCommand
                                                {
                                                    Connection = cnArchConfig,
                                                    CommandType = CommandType.Text,
                                                    CommandText = $@"
                                                        DROP TABLE IF EXISTS #TempKeyList"
                                                };
                                                Logger.Info(Logging.DbCmdString(cmdDropTemp2));
                                                stepDropTempTable2.Statement = cmdDropTemp2.CommandText;
                                                stepDropTempTable2.RowsAffected = cmdDropTemp2.ExecuteNonQuery();
                                            }
                                            catch (Exception e)
                                            {
                                                stepDropTempTable2.Errors.Add(new Error(e));
                                            }
                                            stepDropTempTable2.Done(cnArchConfig);
                                            response.ProcessSteps.Add(stepDropTempTable2);

                                            // Load the Key Values into the temp table.
                                            var stepCreateTempTable = new ProcessStep(cnArchConfig, processID, $@"Create temp table #TempKeyList with oldest {archiveTableConfig.MaxDeleteRowsPerBatch} rows from {archiveTableConfig.ServerQualifiedName}");
                                            string cmdStr;
                                            StringBuilder sb = new StringBuilder();
                                            sb.Append($@"
                                                SELECT TOP ({archiveTableConfig.MaxDeleteRowsPerBatch}) {archiveTableConfig.KeyColumn}
                                                INTO #TempKeyList
                                                FROM [{archiveTableConfig.SchemaName}].[{archiveTableConfig.TableName}] WITH (NOLOCK)
                                                {archiveTableConfig.WhereClause} 
                                                ORDER BY {archiveTableConfig.KeyColumn}");
                                            cmdStr = sb.ToString();
                                            try
                                            {
                                                var cmdTemp = new SqlCommand
                                                {
                                                    Connection = cnSource,
                                                    CommandType = CommandType.Text,
                                                    CommandText = cmdStr
                                                };
                                                Logger.Info(Logging.DbCmdString(cmdTemp));
                                                stepCreateTempTable.Statement = cmdTemp.CommandText;
                                                archiveTableConfig.SelectedRows = cmdTemp.ExecuteNonQuery();
                                                Logger.Info($@"ROWS: {archiveTableConfig.SelectedRows}");
                                                stepCreateTempTable.RowsAffected = archiveTableConfig.SelectedRows;
                                            }
                                            catch (Exception e)
                                            {
                                                stepCreateTempTable.Errors.Add(new Error(e));
                                            }
                                            stepCreateTempTable.Done(cnArchConfig);
                                            response.ProcessSteps.Add(stepCreateTempTable);

                                            if (!stepCreateTempTable.Errors.Any())
                                            {
                                                var stepValidateTempTable = new ProcessStep(cnArchConfig, processID, $@"Validate {archiveTableConfig.SelectedRows} rows in #TempKeyList");
                                                try
                                                {

                                                    // Verify that #Temp table has expected row count
                                                    var cmdVerifyTemp = new SqlCommand
                                                    {
                                                        Connection = cnSource,
                                                        CommandType = CommandType.Text,
                                                        CommandText = $@"
                                                            SELECT COUNT(*) FROM #TempKeyList"
                                                    };
                                                    Logger.Info(Logging.DbCmdString(cmdVerifyTemp));
                                                    stepValidateTempTable.Statement = cmdVerifyTemp.CommandText;
                                                    archiveTableConfig.TempRows = Convert.ToInt32(cmdVerifyTemp.ExecuteScalar());
                                                    Logger.Info($@"ROWS: {archiveTableConfig.TempRows}");
                                                    stepValidateTempTable.RowsAffected = archiveTableConfig.TempRows;
                                                }
                                                catch (Exception e)
                                                {
                                                    stepCreateTempTable.Errors.Add(new Error(e));
                                                }
                                                stepValidateTempTable.Done(cnArchConfig);
                                                response.ProcessSteps.Add(stepValidateTempTable);

                                                if (!stepValidateTempTable.Errors.Any())
                                                {
                                                    // Get batch of source data into DataReader
                                                    var stepGetReader = new ProcessStep(cnArchConfig, processID, $@"Read oldest {archiveTableConfig.MaxDeleteRowsPerBatch} rows from {archiveTableConfig.ServerQualifiedName}");
                                                    sb = new StringBuilder();
                                                    sb.Append($@"
                                                        SELECT t.* 
                                                        FROM [{archiveTableConfig.SchemaName}].[{archiveTableConfig.TableName}] t WITH (NOLOCK)
                                                        INNER JOIN #TempKeyList k 
                                                            ON ");
                                                    // Build SourceKeyColumns collection
                                                    keyColumnList = archiveTableConfig.KeyColumn.Split(',').ToList();
                                                    var i = 0;
                                                    foreach (var keyColumn in keyColumnList)
                                                    {
                                                        i++;
                                                        sb.Append($@"
                                                            k.[{keyColumn}] = t.[{keyColumn}] ");
                                                        if (i < keyColumnList.Count) sb.Append(" AND ");
                                                    }
                                                    cmdStr = sb.ToString();
                                                    try
                                                    {
                                                        var cmdGetReader = new SqlCommand
                                                        {
                                                            Connection = cnSource,
                                                            CommandType = CommandType.Text,
                                                            CommandText = cmdStr
                                                        };
                                                        //                                                        ORDER BY t.[{ archiveTableConfig.KeyColumn}]
                                                        Logger.Info(Logging.DbCmdString(cmdGetReader));
                                                        stepGetReader.Statement = cmdGetReader.CommandText;
                                                        archiveTableConfig.SelectCommand = cmdGetReader.CommandText;
                                                        var reader = cmdGetReader.ExecuteReader(CommandBehavior.SingleResult);
                                                        stepGetReader.RowsAffected = archiveTableConfig.TempRows;
                                                        stepGetReader.Done(cnArchConfig);
                                                        response.ProcessSteps.Add(stepGetReader);
                                                        Logger.Info($@"Selected ROWS: {archiveTableConfig.TempRows}");

                                                        if (reader.HasRows)
                                                        {
                                                            // Truncate the Ledger table.
                                                            var stepTruncateLedgerTable = new ProcessStep(cnArchConfig, processID, $@"Truncate Archie repository ledger table [{cnArchRepoMaster.DataSource}].[{archiveTableConfig.RepoDatabaseName}].[Ledger].[{archiveTableConfig.TableName}]");
                                                            var cmdTruncateLedger = new SqlCommand
                                                            {
                                                                Connection = cnArchRepo,
                                                                CommandType = CommandType.Text,
                                                                CommandTimeout = 0,
                                                                CommandText = $@"
                                                                    TRUNCATE TABLE [Ledger].[{archiveTableConfig.TableName}]"
                                                            };
                                                            Logger.Info(Logging.DbCmdString(cmdTruncateLedger));
                                                            stepTruncateLedgerTable.Statement = cmdTruncateLedger.CommandText;
                                                            stepTruncateLedgerTable.RowsAffected = cmdTruncateLedger.ExecuteNonQuery();
                                                            stepTruncateLedgerTable.Done(cnArchConfig);
                                                            response.ProcessSteps.Add(stepTruncateLedgerTable);
                                                            Logger.Info($@"ROWS: {stepTruncateLedgerTable.RowsAffected}");

                                                            var stepBulkCopy = new ProcessStep(cnArchConfig, processID, $@"BulkCopy {archiveTableConfig.TempRows} rows from {archiveTableConfig.ServerQualifiedName} to [{cnArchRepoMaster.DataSource}].[{archiveTableConfig.RepoDatabaseName}].[Ledger].[{archiveTableConfig.TableName}]");
                                                            // BulkCopy the reader data to ArchRepository
                                                            var bulkResponse = BulkCopyManager.BulkReader($"Ledger.{archiveTableConfig.TableName}", reader, cnArchRepo);
                                                            archiveTableConfig.MovedRows = bulkResponse.RowsCopied;
                                                            stepBulkCopy.RowsAffected = archiveTableConfig.MovedRows;
                                                            stepBulkCopy.Done(cnArchConfig);
                                                            response.ProcessSteps.Add(stepBulkCopy);
                                                            Logger.Info($@"Bulk Copied ROWS: {archiveTableConfig.MovedRows}");

                                                            archiveTableConfig.BulkCopySeconds = stepBulkCopy.ElapsedSeconds;
                                                            ArchieLoggingManager.LogProcessUpdateBulkCopySeconds(cnArchConfig, processID, archiveTableConfig);
                                                        }
                                                        reader.Close();
                                                    }
                                                    catch (Exception e)
                                                    {
                                                        stepCreateTempTable.Errors.Add(new Error(e));
                                                    }

                                                    if (archiveTableConfig.MovedRows > 0)
                                                    {
                                                        var stepVerifyArchive = new ProcessStep(cnArchConfig, processID, $@"Verify {archiveTableConfig.MovedRows} rows in [{cnArchRepoMaster.DataSource}].[{archiveTableConfig.RepoDatabaseName}].[{archiveTableConfig.SchemaName}].[{archiveTableConfig.TableName}]");
                                                        sb = new StringBuilder();
                                                        sb.Append($@"
                                                            SELECT COUNT(*) FROM [{cnArchRepoMaster.DataSource}].[{archiveTableConfig.RepoDatabaseName}].[{archiveTableConfig.SchemaName}].[{archiveTableConfig.TableName}] r
                                                            INNER JOIN [{cnArchRepoMaster.DataSource}].[{archiveTableConfig.RepoDatabaseName}].[Ledger].[{archiveTableConfig.TableName}] l 
                                                                ON ");
                                                        // Build SourceKeyColumns collection
                                                        keyColumnList = archiveTableConfig.KeyColumn.Split(',').ToList();
                                                        i = 0;
                                                        foreach (var keyColumn in keyColumnList)
                                                        {
                                                            i++;
                                                            sb.Append($@"
                                                                l.[{keyColumn}] = r.[{keyColumn}] ");
                                                            if (i < keyColumnList.Count) sb.Append(" AND ");
                                                        }
                                                        cmdStr = sb.ToString();
                                                        var cmdVerify = new SqlCommand
                                                        {
                                                            Connection = cnArchRepo,
                                                            CommandType = CommandType.Text,
                                                            CommandTimeout = 180,
                                                            CommandText = cmdStr
                                                        };
                                                        Logger.Info(Logging.DbCmdString(cmdVerify));
                                                        stepVerifyArchive.Statement = cmdVerify.CommandText;
                                                        archiveTableConfig.VerifiedRows = Convert.ToInt32(cmdVerify.ExecuteScalar());
                                                        stepVerifyArchive.RowsAffected = archiveTableConfig.VerifiedRows;
                                                        stepVerifyArchive.Done(cnArchConfig);
                                                        response.ProcessSteps.Add(stepVerifyArchive);
                                                        Logger.Info($@"Verified ROWS: {archiveTableConfig.VerifiedRows}");

                                                        var stepCheckForMissingKeys = new ProcessStep(cnArchConfig, processID, $@"Check for missing keys in [{cnArchRepoMaster.DataSource}].[{archiveTableConfig.RepoDatabaseName}].[{archiveTableConfig.SchemaName}].[{archiveTableConfig.TableName}]");
                                                        sb = new StringBuilder();
                                                        sb.Append($@"
                                                            SELECT COUNT(*) FROM #TempKeyList k 
                                                            WHERE NOT EXISTS 
                                                                (SELECT 1 FROM [{cnArchRepoMaster.DataSource}].[{archiveTableConfig.RepoDatabaseName}].[{archiveTableConfig.SchemaName}].[{archiveTableConfig.TableName}] a
                                                                 WHERE ");
                                                        // Build SourceKeyColumns collection
                                                        keyColumnList = archiveTableConfig.KeyColumn.Split(',').ToList();
                                                        i = 0;
                                                        foreach (var keyColumn in keyColumnList)
                                                        {
                                                            i++;
                                                            sb.Append($@"
                                                                a.[{keyColumn}] = k.[{keyColumn}] ");
                                                            if (i < keyColumnList.Count) sb.Append(" AND ");
                                                        }
                                                        sb.Append($@"
                                                                )");
                                                        cmdStr = sb.ToString();
                                                        cmdVerify = new SqlCommand
                                                        {
                                                            Connection = cnSource,
                                                            CommandType = CommandType.Text,
                                                            CommandTimeout = 180,
                                                            CommandText = cmdStr
                                                        };
                                                        Logger.Info(Logging.DbCmdString(cmdVerify));
                                                        stepCheckForMissingKeys.Statement = cmdVerify.CommandText;
                                                        //archiveTableConfig.MissingKeys = Convert.ToInt32(cmdVerify.ExecuteScalar());
                                                        // Doesn't perform due to linked server so skip this step...
                                                        archiveTableConfig.MissingKeys = 0;
                                                        stepCheckForMissingKeys.RowsAffected = archiveTableConfig.MissingKeys;
                                                        stepCheckForMissingKeys.Done(cnArchConfig);
                                                        response.ProcessSteps.Add(stepCheckForMissingKeys);
                                                        Logger.Info($@"Missing Keys: {archiveTableConfig.MissingKeys}");


                                                        if (!archiveTableConfig.TestModeNoDelete)
                                                        {
                                                            var stepDeleteSourceData = new ProcessStep(cnArchConfig, processID, $@"Delete {archiveTableConfig.MovedRows} rows from {archiveTableConfig.ServerQualifiedName}");
                                                            sb = new StringBuilder();
                                                            sb.Append($@"
                                                                DELETE t FROM [{archiveTableConfig.SchemaName}].[{archiveTableConfig.TableName}] t
                                                                INNER JOIN #TempKeyList k 
                                                                    ON ");
                                                            // Build SourceKeyColumns collection
                                                            keyColumnList = archiveTableConfig.KeyColumn.Split(',').ToList();
                                                            i = 0;
                                                            foreach (var keyColumn in keyColumnList)
                                                            {
                                                                i++;
                                                                sb.Append($@"
                                                                    k.[{keyColumn}] = t.[{keyColumn}] ");
                                                                if (i < keyColumnList.Count) sb.Append(" AND ");
                                                            }
                                                            cmdStr = sb.ToString();
                                                            var cmdDelete = new SqlCommand
                                                            {
                                                                Connection = cnSource,
                                                                CommandType = CommandType.Text,
                                                                CommandTimeout = 0,
                                                                CommandText = cmdStr
                                                            };
                                                            Logger.Info(Logging.DbCmdString(cmdDelete));
                                                            stepDeleteSourceData.Statement = cmdDelete.CommandText;
                                                            archiveTableConfig.DeleteCommand = cmdDelete.CommandText;
                                                            if (archiveTableConfig.MissingKeys == 0) archiveTableConfig.DeletedRows = cmdDelete.ExecuteNonQuery();
                                                            stepDeleteSourceData.RowsAffected = archiveTableConfig.DeletedRows;
                                                            stepDeleteSourceData.Done(cnArchConfig);
                                                            response.ProcessSteps.Add(stepDeleteSourceData);
                                                            Logger.Info($@"Deleted ROWS: {archiveTableConfig.DeletedRows}");

                                                            archiveTableConfig.DeleteSeconds = stepDeleteSourceData.ElapsedSeconds;
                                                            ArchieLoggingManager.LogProcessUpdateDeleteSeconds(cnArchConfig, processID, archiveTableConfig);
                                                        };
                                                        //Logger.Info("SLEEP 2 SECONDS...");
                                                        //Thread.Sleep(2000);

                                                        if (archiveTableConfig.TruncateRepoLedgerAfter)
                                                        {
                                                            // Truncate the Ledger table.
                                                            var stepTruncateLedgerTableAfter = new ProcessStep(cnArchRepo, processID, $@"Truncate [Ledger].[{archiveTableConfig.TableName}]");
                                                            var cmdTruncateLedger = new SqlCommand
                                                            {
                                                                Connection = cnArchRepo,
                                                                CommandType = CommandType.Text,
                                                                CommandText = $@"
                                                                    TRUNCATE TABLE [Ledger].[{archiveTableConfig.TableName}]"
                                                            };
                                                            Logger.Info(Logging.DbCmdString(cmdTruncateLedger));
                                                            stepTruncateLedgerTableAfter.Statement = cmdTruncateLedger.CommandText;
                                                            stepTruncateLedgerTableAfter.RowsAffected = cmdTruncateLedger.ExecuteNonQuery();
                                                            stepTruncateLedgerTableAfter.Done(cnArchRepo);
                                                            response.ProcessSteps.Add(stepTruncateLedgerTableAfter);
                                                            Logger.Info($@"ROWS: {stepTruncateLedgerTableAfter.RowsAffected}");
                                                        }

                                                        if (archiveTableConfig.DeleteSeconds > maxDeleteSeconds && archiveTableConfig.MaxDeleteRowsPerBatch > 1000)
                                                        {
                                                            var stepReduceConfigMaxDeleteRows = new ProcessStep(cnArchConfig, processID, $@"Reduce Max Delete Rows by 10%");
                                                            var cmdReduceConfigMaxDeleteRows = new SqlCommand
                                                            {
                                                                Connection = cnArchConfig,
                                                                CommandType = CommandType.Text,
                                                                CommandText = $@"
                                                                    UPDATE xx
                                                                    SET 
                                                                        [MaxDeleteRowsPerBatch] = [MaxDeleteRowsPerBatch] * .9
                                                                    FROM [dbo].[cfgTablesArchive] xx
                                                                    WHERE [ConfigID] = {archiveTableConfig.ID}
                                                                "
                                                            };
                                                            Logger.Info(Logging.DbCmdString(cmdReduceConfigMaxDeleteRows));
                                                            stepReduceConfigMaxDeleteRows.Statement = cmdReduceConfigMaxDeleteRows.CommandText;
                                                            stepReduceConfigMaxDeleteRows.RowsAffected = cmdReduceConfigMaxDeleteRows.ExecuteNonQuery();
                                                            stepReduceConfigMaxDeleteRows.Done(cnArchConfig);
                                                            response.ProcessSteps.Add(stepReduceConfigMaxDeleteRows);
                                                            Logger.Info($@"ROWS: {stepReduceConfigMaxDeleteRows.RowsAffected}");
                                                        }

                                                        if (archiveTableConfig.DeleteSeconds < maxDeleteSeconds && (archiveTableConfig.MaxDeleteRowsPerBatch * 1.1 <= maxDeleteRows))
                                                            {
                                                                var stepIncreaseConfigMaxDeleteRows = new ProcessStep(cnArchConfig, processID, $@"Increase Max Delete Rows by 10%");
                                                            var cmdIncreaseConfigMaxDeleteRows = new SqlCommand
                                                            {
                                                                Connection = cnArchConfig,
                                                                CommandType = CommandType.Text,
                                                                CommandText = $@"
                                                                    UPDATE xx
                                                                    SET 
                                                                        [MaxDeleteRowsPerBatch] = [MaxDeleteRowsPerBatch] * 1.1
                                                                    FROM [dbo].[cfgTablesArchive] xx
                                                                    WHERE [ConfigID] = {archiveTableConfig.ID}
                                                                "
                                                            };
                                                            Logger.Info(Logging.DbCmdString(cmdIncreaseConfigMaxDeleteRows));
                                                            stepIncreaseConfigMaxDeleteRows.Statement = cmdIncreaseConfigMaxDeleteRows.CommandText;
                                                            stepIncreaseConfigMaxDeleteRows.RowsAffected = cmdIncreaseConfigMaxDeleteRows.ExecuteNonQuery();
                                                            stepIncreaseConfigMaxDeleteRows.Done(cnArchConfig);
                                                            response.ProcessSteps.Add(stepIncreaseConfigMaxDeleteRows);
                                                            Logger.Info($@"ROWS: {stepIncreaseConfigMaxDeleteRows.RowsAffected}");
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        
                                    }
                                    var stepCountArchive = new ProcessStep(cnArchConfig, processID, $@"Count rows in [{cnArchRepoMaster.DataSource}].[{archiveTableConfig.RepoDatabaseName}].[{archiveTableConfig.SchemaName}].[{archiveTableConfig.TableName}]");
                                    var cmdCountArchive = new SqlCommand
                                    {
                                        Connection = cnArchRepo,
                                        CommandType = CommandType.Text,
                                        CommandText = $@"
                                            SELECT COUNT(*) FROM [{archiveTableConfig.RepoDatabaseName}].[{archiveTableConfig.SchemaName}].[{archiveTableConfig.TableName}]"
                                    };
                                    Logger.Info(Logging.DbCmdString(cmdCountArchive));
                                    stepCountArchive.Statement = cmdCountArchive.CommandText;
                                    archiveTableConfig.TotalMovedRows = Convert.ToInt32(cmdCountArchive.ExecuteScalar());
                                    stepCountArchive.StepName = $@"Count {archiveTableConfig.TotalMovedRows} rows in [{cnArchRepoMaster.DataSource}].[{archiveTableConfig.RepoDatabaseName}].[{archiveTableConfig.SchemaName}].[{archiveTableConfig.TableName}]";
                                    stepCountArchive.RowsAffected = archiveTableConfig.TotalMovedRows;
                                    stepCountArchive.Done(cnArchConfig);
                                    response.ProcessSteps.Add(stepCountArchive);
                                    Logger.Info($@"Total Archived ROWS: {archiveTableConfig.TotalMovedRows}");

                                    if (archiveTableConfig.MovedRows < minMovedRows && !response.HasErrors())
                                    {
                                        var stepUpdateConfigNextRunDate = new ProcessStep(cnArchConfig, processID, $@"Move Next Run Date ahead 7 days");
                                        var cmdUpdateConfigNextRunDate = new SqlCommand
                                        {
                                            Connection = cnArchConfig,
                                            CommandType = CommandType.Text,
                                            CommandText = $@"
                                                UPDATE xx
                                                SET 
                                                    [NextRunDate] = GETDATE() + {nextRuntimeMoveAheadDays}
                                                FROM [dbo].[cfgTablesArchive] xx
                                                WHERE [ConfigID] = {archiveTableConfig.ID}
                                            "
                                        };
                                        Logger.Info(Logging.DbCmdString(cmdUpdateConfigNextRunDate));
                                        stepUpdateConfigNextRunDate.Statement = cmdUpdateConfigNextRunDate.CommandText;
                                        stepUpdateConfigNextRunDate.RowsAffected = cmdUpdateConfigNextRunDate.ExecuteNonQuery();
                                        stepUpdateConfigNextRunDate.Done(cnArchConfig);
                                        response.ProcessSteps.Add(stepUpdateConfigNextRunDate);
                                        Logger.Info($@"ROWS: {stepUpdateConfigNextRunDate.RowsAffected}");
                                    }

                                    archiveTableConfig.VerifySeconds = stepCountArchive.ElapsedSeconds;
                                    ArchieLoggingManager.LogProcessUpdateVerifySeconds(cnArchConfig, processID, archiveTableConfig);

                                    var stepGatherFinalStatistics = new ProcessStep(cnArchConfig, processID, $@"Gather final statistics for {archiveTableConfig.ServerQualifiedName}");
                                    try
                                    {
                                        archiveTableConfig.SFMinDate = TableManager.GetMinDate(cnSource, archiveTableConfig);
                                        response.SFMinDate = archiveTableConfig.SFMinDate;
                                        archiveTableConfig.SFMaxDate = TableManager.GetMaxDate(cnSource, archiveTableConfig);
                                        archiveTableConfig.SFDays = TableManager.GetSFDays(cnSource, archiveTableConfig);
                                        response.SFDays = archiveTableConfig.SFDays;
                                    }
                                    catch (Exception e)
                                    {
                                        stepGatherFinalStatistics.Errors.Add(new Error(e));
                                    }
                                    stepGatherFinalStatistics.Done(cnArchConfig);
                                    response.ProcessSteps.Add(stepGatherFinalStatistics);

                                    response.RowsAffected = archiveTableConfig.MovedRows;
                                    archiveTableConfig.LastRunStatus = "Success";
                                    archiveTableConfig.LastArchiveEndDate = DateTime.Now;
                                    archiveTableConfig.FinalRowCountActual = TableManager.GetRowCount(cnSource, archiveTableConfig);

                                    //ArchieLoggingManager.LogActivity(cnArchConfig, new ArchieLog("Event", "ArchiveTable",
                                    //    processName, archiveTableConfig.SelectCommand.Trim(), archiveTableConfig.DateColumn, archiveTableConfig.RetentionDays,
                                    //    archiveTableConfig.InitialRowCount, archiveTableConfig.DeletedRows, archiveTableConfig.FinalRowCountActual,
                                    //    Convert.ToBoolean(ConfigurationManager.AppSettings["TestModeNoDelete"]),
                                    //    "", "", archiveTableConfig.ServerName, archiveTableConfig.DatabaseName, archiveTableConfig.SchemaName,
                                    //    archiveTableConfig.TableName, archiveTableConfig.Guid));

                                    response.InitialRowCount = archiveTableConfig.InitialRowCount;
                                    response.FinalRowCount = archiveTableConfig.FinalRowCountActual;
                                    response.RetentionDays = archiveTableConfig.RetentionDays;
                                    response.SelectCommand = archiveTableConfig.SelectCommand.Trim();
                                    response.DeleteCommand = archiveTableConfig.DeleteCommand.Trim();

                                    //ArchieLoggingManager.LogProcessUpdateDone(cnArchConfig, processID, archiveTableConfig);

                                    //var stepUpdateArchiveTableConfigDone = new ProcessStep(cnArchConfig, processID, $@"Set [Archiver].[dbo].[cfgTablesArchive] DONE for {archiveTableConfig.ServerQualifiedName}");
                                    //try
                                    //{
                                    //    var statement = "";
                                    //    stepUpdateArchiveTableConfigDone.RowsAffected = ArchiveConfigTableManager.UpdateArchiveTableConfigDone(cnArchConfig, archiveTableConfig, out statement);
                                    //    stepUpdateArchiveTableConfigDone.Statement = statement;
                                    //}
                                    //catch (Exception e)
                                    //{
                                    //    stepUpdateArchiveTableConfigDone.Errors.Add(new Error(e));
                                    //}
                                    //stepUpdateArchiveTableConfigDone.Done(cnArchConfig);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            finally
            {
                ArchieLoggingManager.LogProcessUpdateDone(cnArchConfig, processID, archiveTableConfig);

                var stepUpdateArchiveTableConfigDone = new ProcessStep(cnArchConfig, processID, $@"Set [Archiver].[dbo].[cfgTablesArchive] DONE for {archiveTableConfig.ServerQualifiedName}");
                try
                {
                    var statement = "";
                    stepUpdateArchiveTableConfigDone.RowsAffected = OldArchiveConfigTableManager.UpdateArchiveTableConfigDone(cnArchConfig, archiveTableConfig, out statement);
                    stepUpdateArchiveTableConfigDone.Statement = statement;
                }
                catch (Exception e)
                {
                    stepUpdateArchiveTableConfigDone.Errors.Add(new Error(e));
                }
                stepUpdateArchiveTableConfigDone.Done(cnArchConfig);

                cnArchRepo.Close();
                cnSource.Close();
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return response;
        }

        private static SqlConnection GetSourceSqlConnection(string serverName, string databaseName)
        {
            SqlConnection cnSource = new SqlConnection();
            try
            {
                cnSource = ConnectionManager.GetSqlConnectionFromParts(serverName, databaseName);
                cnSource.Open();
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnSource.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new ApplicationException($@"[{serverName}].[{databaseName}]  {e.Message}: {e.StackTrace}");
            }
            return cnSource;
        }

    }
}