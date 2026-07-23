using System;
using System.Configuration;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text;
using ArchiveServices.Common.Types.Archie;
using ArchiveServices.Common.Types.Enterprise;
using ArchiveServices.Common.Types.Response;
using ArchiveServices.Common.Utilities;
using NLog;

namespace ArchiveServices.Common.Managers.Archie
{
    public class MergeAndTailTableManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static ProcessResponse Process(int batchID, string batchName, ArchiveAndTailTableConfig archiveAndTailTableConfig, SqlConnection cnCorpInt, SqlConnection cnArchConfig, SqlConnection cnSource, SqlConnection cnDestination, bool statsOnly)
        {
            var startTime = DateTime.Now.ToLocalTime();
            var processName = $@"Archive And Tail Data Table: {archiveAndTailTableConfig.ServerQualifiedDestinationName}";
            Logger.Info($"{processName} BEGIN: {DateTime.Now.ToLocalTime()}");
            archiveAndTailTableConfig.LastRunStartDate = startTime;

            var processID = ArchieLoggingManager.LogProcessInsert(cnArchConfig, processName, batchID, archiveAndTailTableConfig);
            archiveAndTailTableConfig.ProcessID = processID;

            var testMode = Convert.ToBoolean(ConfigurationManager.AppSettings["TestMode"]);
            archiveAndTailTableConfig.IsTestMode = testMode;
            var testModeNoDelete = Convert.ToBoolean(ConfigurationManager.AppSettings["TestModeNoDelete"]);
            archiveAndTailTableConfig.TestModeNoDelete = testModeNoDelete;
            if (archiveAndTailTableConfig.TestModeNoDelete) processName = $@"{processName} - Test Mode No Delete";

            var response = new ProcessResponse($@"BatchID [{batchID}] {batchName} ProcessID [{processID}] {processName}", archiveAndTailTableConfig, true);

            response.BatchID = batchID;
            response.ProcessID = processID;
            response.ArchiveDate = archiveAndTailTableConfig.LastRunStartDate;

            response.ConnectionStrings.Add(new ConnectionString(cnArchConfig, "cnCorpInt", cnCorpInt.ConnectionString, batchID, processID));
            response.ConnectionStrings.Add(new ConnectionString(cnArchConfig, "cnArchConfig", cnArchConfig.ConnectionString, batchID, processID));
            var guid = Guid.NewGuid();

            try
            {
                var stepValidateTable = new ProcessStep(cnArchConfig, processID, $@"Validate configuration in [Archiver].[dbo].[cfgTablesArchiveAndTail] for {archiveAndTailTableConfig.ServerQualifiedDestinationName}");
                archiveAndTailTableConfig.ValidationErrors = TableManager.ValidateConfig(stepValidateTable.StepID, archiveAndTailTableConfig, cnArchConfig, cnSource, cnDestination);
                stepValidateTable.RowsAffected = 1;
                stepValidateTable.Done(cnArchConfig);
                response.ProcessSteps.Add(stepValidateTable);

                if (archiveAndTailTableConfig.ValidationErrors.Any())
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

                    foreach (var validationError in archiveAndTailTableConfig.ValidationErrors)
                    {
                        response.Errors.Add(new Error(validationError.Message));
                    }
                    ArchieLoggingManager.LogActivity(cnArchConfig, new ArchieLog("Error", "MergeAndTailTable", "",
                        "", archiveAndTailTableConfig.DateColumn, 0, archiveAndTailTableConfig.SourceRowCount, stepValidateTable.RowsAffected, archiveAndTailTableConfig.SourceRowCount - stepValidateTable.RowsAffected,
                        testModeNoDelete, "", "", archiveAndTailTableConfig.SourceServerName, archiveAndTailTableConfig.SourceDatabaseName, archiveAndTailTableConfig.SourceSchemaName, archiveAndTailTableConfig.SourceTableName, guid));
                }
                else
                {
                    var stepGetSourceSqlConnection = new ProcessStep(cnArchConfig, processID, $@"Get SQL connection for Source table {archiveAndTailTableConfig.ServerQualifiedSourceName}");
                    try
                    {
                        // Get Source SQL connection
                        cnSource = GetSourceSqlConnection(archiveAndTailTableConfig.SourceServerName, archiveAndTailTableConfig.SourceDatabaseName);
                        response.ConnectionStrings.Add(new ConnectionString(cnArchConfig, "cnSource", cnSource.ConnectionString, batchID, processID));
                        // See if Source database exists
                        if (!TableManager.CheckSourceDatabaseExists(cnSource, archiveAndTailTableConfig))
                        {
                            var validationError = new ValidationError($@"ERROR: {cnSource.ConnectionString} Source database [{archiveAndTailTableConfig.ServerQualifiedSourceName}] not found.", "Validation", "Fatal", true);
                            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepGetSourceSqlConnection.StepID, validationError.Message, "");
                            archiveAndTailTableConfig.ValidationErrors.Add(validationError);
                            archiveAndTailTableConfig.ValidationStatus = validationError.Message;
                            Logger.Error(validationError.Message);
                        }
                        // See if Source schema exists
                        if (!TableManager.CheckSourceSchemaExists(cnSource, archiveAndTailTableConfig))
                        {
                            var validationError = new ValidationError($@"ERROR: {cnSource.ConnectionString} Source schema [{archiveAndTailTableConfig.ServerQualifiedSourceName}] not found.", "Validation", "Fatal", true);
                            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepGetSourceSqlConnection.StepID, validationError.Message, "");
                            archiveAndTailTableConfig.ValidationErrors.Add(validationError);
                            archiveAndTailTableConfig.ValidationStatus = validationError.Message;
                            Logger.Error(validationError.Message);
                        }
                        // See if Source table exists
                        if (TableManager.CheckSourceTableExists(cnSource, archiveAndTailTableConfig))
                        {
                            // Build SourceKeyColumns collection 
                            var keyColumnList = archiveAndTailTableConfig.SourceKey.Split(',').ToList();
                            foreach (var keyColumn in keyColumnList)
                            {
                                archiveAndTailTableConfig.SourceKeyColumns.Add(ArchiveTableConfigManager.GetKeyData(cnSource, archiveAndTailTableConfig.SourceSchemaName, archiveAndTailTableConfig.SourceTableName, keyColumn));
                            }
                            stepGetSourceSqlConnection.RowsAffected = 1;
                        }
                        else
                        {
                            var validationError = new ValidationError($@"ERROR: {cnSource.ConnectionString} Source table [{archiveAndTailTableConfig.ServerQualifiedSourceName}] not found.", "Validation", "Fatal", true);
                            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepGetSourceSqlConnection.StepID, validationError.Message, "");
                            archiveAndTailTableConfig.ValidationErrors.Add(validationError);
                            archiveAndTailTableConfig.ValidationStatus = validationError.Message;
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
                        var stepGetDestinationSqlConnection = new ProcessStep(cnArchConfig, processID, $@"Get SQL connection for Destination table {archiveAndTailTableConfig.ServerQualifiedDestinationName}");
                        try
                        {
                            // Get Destination Table connection
                            cnDestination = GetDestinationSqlConnection(archiveAndTailTableConfig.DestinationServerName, archiveAndTailTableConfig.DestinationDatabaseName);
                            response.ConnectionStrings.Add(new ConnectionString(cnArchConfig, "cnDestination", cnDestination.ConnectionString, batchID, processID));
                            // See if Destination database exists
                            if (!TableManager.CheckDestinationDatabaseExists(cnDestination, archiveAndTailTableConfig))
                            {
                                var stepCreateDestinationDatabase = new ProcessStep(cnArchConfig, processID, $@"Create Destination Database {archiveAndTailTableConfig.DestinationDatabaseName}");
                                try
                                {
                                    var statement = "";
                                    CreateDestinationDatabase(cnDestination, archiveAndTailTableConfig.DestinationDatabaseName, out statement);
                                    stepCreateDestinationDatabase.RowsAffected = 1;
                                    stepCreateDestinationDatabase.Statement = statement;
                                }
                                catch (Exception e)
                                {
                                    stepCreateDestinationDatabase.Errors.Add(new Error(e));
                                }
                                stepCreateDestinationDatabase.Done(cnArchConfig);
                                response.ProcessSteps.Add(stepCreateDestinationDatabase);
                            }
                            // See if Destination schema exists
                            if (!TableManager.CheckDestinationSchemaExists(cnDestination, archiveAndTailTableConfig))
                            {
                                var stepCreateDestinationSchema = new ProcessStep(cnArchConfig, processID, $@"Create Destination Schema {archiveAndTailTableConfig.DestinationSchemaName}");
                                try
                                {
                                    var statement = "";
                                    TableManager.CreateDestinationSchema(cnDestination, archiveAndTailTableConfig, out statement);
                                    stepCreateDestinationSchema.RowsAffected = 1;
                                    stepCreateDestinationSchema.Statement = statement;
                                }
                                catch (Exception e)
                                {
                                    stepCreateDestinationSchema.Errors.Add(new Error(e));
                                }
                                stepCreateDestinationSchema.Done(cnArchConfig);
                            }
                            // See if Destination table exists
                            if (!TableManager.CheckDestinationTableExists(cnDestination, archiveAndTailTableConfig))
                            {
                                var stepCreateDestinationTable = new ProcessStep(cnArchConfig, processID, $@"Create Destination Table {archiveAndTailTableConfig.DestinationTableName}");
                                try
                                {
                                    var statement = "";
                                    TableManager.CreateDestinationTable(cnSource, cnDestination, archiveAndTailTableConfig, out statement);
                                    stepCreateDestinationTable.RowsAffected = 1;
                                    stepCreateDestinationTable.Statement = statement;
                                }
                                catch (Exception e)
                                {
                                    stepCreateDestinationTable.Errors.Add(new Error(e));
                                }
                                stepCreateDestinationTable.Done(cnArchConfig);
                            }
                            // See if Destination table structure matches Source table
                            if (TableManager.CheckDestinationTableStructureMissingColumns(cnDestination, archiveAndTailTableConfig))
                            {
                                var stepSyncDestinationTableStructureMissingColumns = new ProcessStep(cnArchConfig, processID, $@"Sync Destination Table Structure Missing Columns {archiveAndTailTableConfig.DestinationTableName}");
                                try
                                {
                                    var statement = "";
                                    TableManager.SyncDestinationTableStructureMissingColumns(cnSource, cnDestination, archiveAndTailTableConfig, out statement);
                                    stepSyncDestinationTableStructureMissingColumns.RowsAffected = 1;
                                    stepSyncDestinationTableStructureMissingColumns.Statement = statement;
                                }
                                catch (Exception e)
                                {
                                    stepSyncDestinationTableStructureMissingColumns.Errors.Add(new Error(e));
                                }
                                stepSyncDestinationTableStructureMissingColumns.Done(cnArchConfig);
                            }
                            if (!TableManager.CheckDestinationTableStructureDifferentColumns(cnDestination, archiveAndTailTableConfig))
                            {
                                var stepSyncDestinationTableStructureDifferentColumns = new ProcessStep(cnArchConfig, processID, $@"Sync Destination Table Structure Different Columns {archiveAndTailTableConfig.DestinationTableName}");
                                try
                                {
                                    var statement = "";
                                    TableManager.SyncDestinationTableStructureDifferentColumns(cnSource, cnDestination, archiveAndTailTableConfig, out statement);
                                    stepSyncDestinationTableStructureDifferentColumns.RowsAffected = 1;
                                    stepSyncDestinationTableStructureDifferentColumns.Statement = statement;
                                }
                                catch (Exception e)
                                {
                                    stepSyncDestinationTableStructureDifferentColumns.Errors.Add(new Error(e));
                                }
                                stepSyncDestinationTableStructureDifferentColumns.Done(cnArchConfig);
                            }
                            // Build DestinationKeyColumns collection
                            var keyColumnList = archiveAndTailTableConfig.DestinationKey.Split(',').ToList();
                            foreach (var keyColumn in keyColumnList)
                            {
                                archiveAndTailTableConfig.DestinationKeyColumns.Add(ArchiveTableConfigManager.GetKeyData(cnDestination, archiveAndTailTableConfig.DestinationSchemaName, archiveAndTailTableConfig.DestinationTableName, keyColumn));
                            }
                            // See if Destination table has a unique index on DestinationKeyColumns (business key)
                            if (!TableManager.CheckDestinationTableUniqueIndex(cnDestination, archiveAndTailTableConfig))
                            {
                                var stepCreateDestinationTableIndex = new ProcessStep(cnArchConfig, processID, $@"Create Destination Table Index {archiveAndTailTableConfig.DestinationTableName}");
                                try
                                {
                                    var statement = "";
                                    TableManager.DropDestinationTableIndex(cnDestination, archiveAndTailTableConfig);
                                    TableManager.CreateDestinationTableIndex(cnDestination, archiveAndTailTableConfig, out statement);
                                    stepCreateDestinationTableIndex.RowsAffected = 1;
                                    stepCreateDestinationTableIndex.Statement = statement;
                                }
                                catch (Exception e)
                                {
                                    stepCreateDestinationTableIndex.Errors.Add(new Error(e));
                                }
                                stepCreateDestinationTableIndex.Done(cnArchConfig);
                            }
                            stepGetDestinationSqlConnection.RowsAffected = 1;
                        }
                        catch (Exception e)
                        {
                            stepGetDestinationSqlConnection.Errors.Add(new Error(e));
                        }
                        stepGetDestinationSqlConnection.Done(cnArchConfig);
                        response.ProcessSteps.Add(stepGetDestinationSqlConnection);

                        if (!stepGetDestinationSqlConnection.Errors.Any())
                        {
                            var stepUpdateArchiveAndTailTableConfigIsRunningTrue = new ProcessStep(cnArchConfig, processID, $@"Set [Archiver].[dbo].[cfgTablesArchiveAndTail].[IsRunning] = TRUE for {archiveAndTailTableConfig.ServerQualifiedSourceName}");
                            try
                            {
                                var statement = "";
                                stepUpdateArchiveAndTailTableConfigIsRunningTrue.RowsAffected = ArchiveAndTailTableConfigManager.UpdateArchiveAndTailTableConfigIsRunning(cnArchConfig, archiveAndTailTableConfig, true);
                                stepUpdateArchiveAndTailTableConfigIsRunningTrue.Statement = statement;
                            }
                            catch (Exception e)
                            {
                                stepUpdateArchiveAndTailTableConfigIsRunningTrue.Errors.Add(new Error(e));
                            }
                            stepUpdateArchiveAndTailTableConfigIsRunningTrue.Done(cnArchConfig);
                            response.ProcessSteps.Add(stepUpdateArchiveAndTailTableConfigIsRunningTrue);

                            if (!stepUpdateArchiveAndTailTableConfigIsRunningTrue.Errors.Any())
                            {
                                var stepCheckIsSourceKeyColumnUnique = new ProcessStep(cnArchConfig, processID, $@"Check if Source key is unique for {archiveAndTailTableConfig.ServerQualifiedSourceName}");
                                try
                                {
                                    archiveAndTailTableConfig.IsSourceKeyUnique = TableManager.IsSourceKeyUnique(cnSource, archiveAndTailTableConfig);
                                    ArchieLoggingManager.LogProcessUpdateIsKeyColumnUnique(cnArchConfig, processID, archiveAndTailTableConfig);
                                    ArchiveAndTailTableConfigManager.UpdateArchiveAndTailTableConfigIsKeyColumnUnique(cnArchConfig, archiveAndTailTableConfig, out string statement);
                                    stepCheckIsSourceKeyColumnUnique.RowsAffected = 1;
                                    stepCheckIsSourceKeyColumnUnique.Statement = statement;
                                }
                                catch (Exception e)
                                {
                                    stepCheckIsSourceKeyColumnUnique.Errors.Add(new Error(e));
                                }
                                stepCheckIsSourceKeyColumnUnique.Done(cnArchConfig);
                                response.ProcessSteps.Add(stepCheckIsSourceKeyColumnUnique);

                                var stepCheckIsDestinationKeyColumnUnique = new ProcessStep(cnArchConfig, processID, $@"Check if Destination key is unique for {archiveAndTailTableConfig.ServerQualifiedDestinationName}");
                                try
                                {
                                    archiveAndTailTableConfig.IsDestinationKeyUnique = TableManager.IsDestinationKeyUnique(cnSource, archiveAndTailTableConfig);
                                    ArchieLoggingManager.LogProcessUpdateIsKeyColumnUnique(cnArchConfig, processID, archiveAndTailTableConfig);
                                    ArchiveAndTailTableConfigManager.UpdateArchiveAndTailTableConfigIsKeyColumnUnique(cnArchConfig, archiveAndTailTableConfig, out string statement);
                                    stepCheckIsDestinationKeyColumnUnique.RowsAffected = 1;
                                    stepCheckIsDestinationKeyColumnUnique.Statement = statement;
                                }
                                catch (Exception e)
                                {
                                    stepCheckIsDestinationKeyColumnUnique.Errors.Add(new Error(e));
                                }
                                stepCheckIsDestinationKeyColumnUnique.Done(cnArchConfig);
                                response.ProcessSteps.Add(stepCheckIsDestinationKeyColumnUnique);

                                if (!archiveAndTailTableConfig.IsSourceKeyUnique)
                                {
                                    var validationError = new ValidationError($@"ERROR: {cnDestination.ConnectionString} Source table {archiveAndTailTableConfig.ServerQualifiedSourceName} Key [{archiveAndTailTableConfig.SourceKey}] is not unique.", "Validation", "Fatal", true);
                                    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepCheckIsSourceKeyColumnUnique.StepID, validationError.Message, "");
                                    archiveAndTailTableConfig.ValidationErrors.Add(validationError);
                                    archiveAndTailTableConfig.ValidationStatus = validationError.Message;
                                    Logger.Error(validationError.Message);
                                    response.Errors.Add(new Error(validationError.Message));
                                }
                                else if (!archiveAndTailTableConfig.IsDestinationKeyUnique)
                                {
                                    var validationError = new ValidationError($@"ERROR: {cnDestination.ConnectionString} Destination table {archiveAndTailTableConfig.ServerQualifiedDestinationName} Destination Key [{archiveAndTailTableConfig.DestinationKey}] is not unique.", "Validation", "Fatal", true);
                                    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepCheckIsDestinationKeyColumnUnique.StepID, validationError.Message, "");
                                    archiveAndTailTableConfig.ValidationErrors.Add(validationError);
                                    archiveAndTailTableConfig.ValidationStatus = validationError.Message;
                                    Logger.Error(validationError.Message);
                                    response.Errors.Add(new Error(validationError.Message));
                                }
                                else
                                {
                                    var stepGatherInitialStatistics = new ProcessStep(cnArchConfig, processID, $@"Gather initial statistics for Destination table {archiveAndTailTableConfig.ServerQualifiedDestinationName}");
                                    try
                                    {
                                        archiveAndTailTableConfig.DestinationInitialRowCount = TableManager.GetRowCountDestination(cnDestination, archiveAndTailTableConfig);
                                        archiveAndTailTableConfig.LastRunStatus = "";
                                        ArchieLoggingManager.LogProcessUpdateInitialRowCount(cnArchConfig, processID, archiveAndTailTableConfig);
                                        ArchiveAndTailTableConfigManager.UpdateArchiveAndTailTableConfigInitialRowCount(cnArchConfig, archiveAndTailTableConfig);
                                        stepGatherInitialStatistics.RowsAffected = 1;
                                    }
                                    catch (Exception e)
                                    {
                                        stepGatherInitialStatistics.Errors.Add(new Error(e));
                                    }
                                    stepGatherInitialStatistics.Done(cnArchConfig);
                                    response.ProcessSteps.Add(stepGatherInitialStatistics);

                                    if (!stepGatherInitialStatistics.Errors.Any())
                                    {
                                        var stepMergeTable = new ProcessStep(cnArchConfig, processID, $@"Archive And Tail table {archiveAndTailTableConfig.ServerQualifiedSourceName}");
                                        try
                                        {
                                            archiveAndTailTableConfig.RowsAffected = GetMergeAndTailRowCount(cnSource, archiveAndTailTableConfig);
                                            MergeAndTailTable(cnSource, archiveAndTailTableConfig);
                                            stepMergeTable.Statement = archiveAndTailTableConfig.Statement;
                                            stepMergeTable.RowsAffected = archiveAndTailTableConfig.RowsAffected;
                                            archiveAndTailTableConfig.HighwaterMarkDate = TableManager.GetMaxDestinationDate(cnDestination, archiveAndTailTableConfig);
                                            archiveAndTailTableConfig.HighwaterMarkID = TableManager.GetMaxDestinationID(cnDestination, archiveAndTailTableConfig);
                                            archiveAndTailTableConfig.SourceRowCount = TableManager.GetRowCount(cnSource, archiveAndTailTableConfig);
                                            archiveAndTailTableConfig.SourceMinDate = TableManager.GetMinSourceDate(cnSource, archiveAndTailTableConfig);
                                            archiveAndTailTableConfig.SourceMaxDate = TableManager.GetMaxSourceDate(cnSource, archiveAndTailTableConfig);
                                            archiveAndTailTableConfig.SourceMaxDateString = archiveAndTailTableConfig.SourceMaxDate.ToString("M/dd/yyyy hh:mm:ss tt", CultureInfo.CreateSpecificCulture("en-US"));
                                            archiveAndTailTableConfig.DestinationInitialRowCount = TableManager.GetRowCountDestination(cnDestination, archiveAndTailTableConfig);
                                            archiveAndTailTableConfig.DestinationMinDate = TableManager.GetMinDestinationDate(cnDestination, archiveAndTailTableConfig);
                                            archiveAndTailTableConfig.DestinationMaxDate = TableManager.GetMaxDestinationDate(cnDestination, archiveAndTailTableConfig);
                                            archiveAndTailTableConfig.DestinationMaxDateString = archiveAndTailTableConfig.DestinationMaxDate.ToString("M/dd/yyyy hh:mm:ss tt", CultureInfo.CreateSpecificCulture("en-US"));
                                            archiveAndTailTableConfig.DestinationFinalRowCount = archiveAndTailTableConfig.DestinationFinalRowCount;
                                            archiveAndTailTableConfig.LastRunStatus = "Success";
                                            archiveAndTailTableConfig.LastRunEndDate = DateTime.Now.ToLocalTime();
                                            ArchiveAndTailTableConfigManager.UpdateArchiveAndTailTableConfigHighwaterMarks(cnArchConfig, archiveAndTailTableConfig);
                                            response.RowsAffected = archiveAndTailTableConfig.RowsAffected;
                                            response.InitialRowCount = archiveAndTailTableConfig.SourceRowCount;
                                            response.FinalRowCount = archiveAndTailTableConfig.SourceFinalRowCount;
                                            response.SourceRowCount = archiveAndTailTableConfig.SourceRowCount;
                                            response.DestinationRowCount = archiveAndTailTableConfig.DestinationFinalRowCount;
                                            response.SFMinDate = archiveAndTailTableConfig.SourceMinDate;
                                            response.SFDays = archiveAndTailTableConfig.SourceDays;
                                            response.SourceMaxDate = archiveAndTailTableConfig.SourceMaxDate;
                                            response.DestinationMaxDate = archiveAndTailTableConfig.DestinationMaxDate;
                                        }
                                        catch (Exception e)
                                        {
                                            stepMergeTable.Errors.Add(new Error(e));
                                        }
                                        stepMergeTable.Done(cnArchConfig);
                                        response.ProcessSteps.Add(stepMergeTable);
                                    }
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
                ArchieLoggingManager.LogProcessUpdateDone(cnArchConfig, processID, archiveAndTailTableConfig);

                var stepUpdateArchiveTableConfigDone = new ProcessStep(cnArchConfig, processID, $@"Set [Archiver].[dbo].[cfgTablesMergeAndTail] DONE for {archiveAndTailTableConfig.ServerQualifiedSourceName}");
                try
                {
                    var statement = "";
                    stepUpdateArchiveTableConfigDone.RowsAffected = ArchiveAndTailTableConfigManager.UpdateArchiveAndTailTableConfigDone(cnArchConfig, archiveAndTailTableConfig, out statement);
                    stepUpdateArchiveTableConfigDone.Statement = statement;
                }
                catch (Exception e)
                {
                    stepUpdateArchiveTableConfigDone.Errors.Add(new Error(e));
                }
                stepUpdateArchiveTableConfigDone.Done(cnArchConfig);
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return response;
        }

        public static int MergeAndTailTable(SqlConnection cnSource, ArchiveAndTailTableConfig archiveAndTailTableConfig)
        {
            int retVal = 0;
            try
            {
                archiveAndTailTableConfig.Statement = BuildArchiveAndTailTableString(cnSource, archiveAndTailTableConfig);
                SqlCommand cmd = new SqlCommand(archiveAndTailTableConfig.Statement, cnSource);
                cmd.CommandTimeout = 0;
                Logger.Info(Logging.DbCmdString(cmd));
                var x = cmd.ExecuteNonQuery();
                Logger.Info($@"retVal = {retVal}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {cnSource.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        private static String BuildArchiveAndTailTableString(SqlConnection cnSource, ArchiveAndTailTableConfig archiveAndTailTableConfig)
        {
            string str;
            try
            {
                var cmd = new SqlCommand();
                cmd = GetSourceTableMetadata(archiveAndTailTableConfig);
                cmd.Connection = cnSource;
                Logger.Info(Logging.DbCmdString(cmd));

                string prefix = archiveAndTailTableConfig.SourceTableName.Substring(0, 1).ToLower();
                StringBuilder sb = new StringBuilder();
                sb.Append($@"MERGE [{archiveAndTailTableConfig.DestinationDatabaseName}].[{archiveAndTailTableConfig.DestinationSchemaName}].[{archiveAndTailTableConfig.DestinationTableName}] AS TARG ");
                sb.Append(Environment.NewLine);
                sb.Append("USING ");
                sb.Append(Environment.NewLine);
                sb.Append("( ");
                sb.Append(Environment.NewLine);
                sb.Append($@"SELECT ");
                sb.Append(Environment.NewLine);
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        var colName = CastDbNull.To(reader["ColName"], string.Empty).Trim();
                        sb.Append($@"{prefix}.{colName}");
                        sb.Append(Environment.NewLine);
                    }
                    reader.Close();
                }
                sb.Append($@"FROM [{archiveAndTailTableConfig.SourceDatabaseName}].[{archiveAndTailTableConfig.SourceSchemaName}].[{archiveAndTailTableConfig.SourceTableName}] AS {prefix} WITH (NOLOCK) ");
                sb.Append(Environment.NewLine);
                if (archiveAndTailTableConfig.UseHighwaterMark)
                {
                    sb.Append($@"WHERE {prefix}.[{archiveAndTailTableConfig.DateColumn}] >= '{archiveAndTailTableConfig.HighwaterMarkDate}' ");
                }
                else
                {
                    sb.Append($@"WHERE {prefix}.[{archiveAndTailTableConfig.DateColumn}] >= '{archiveAndTailTableConfig.SelectStartDate}' ");
                }
                sb.Append(Environment.NewLine);
                sb.Append(") AS SRC");
                sb.Append(Environment.NewLine);
                sb.Append("ON ( ");
                var keyColumnList = archiveAndTailTableConfig.DestinationKey.Split(',').ToList();
                var i = 0;
                foreach (var keyColumn in keyColumnList)
                {
                    i++;
                    sb.Append($@"TARG.[{keyColumn}] = SRC.[{keyColumn}]");
                    sb.Append(Environment.NewLine);
                    if (i < keyColumnList.Count) sb.Append("AND ");
                }
                sb.Append(") ");
                sb.Append(Environment.NewLine);
                sb.Append("WHEN MATCHED THEN ");
                sb.Append(Environment.NewLine);
                sb.Append("UPDATE SET ");
                sb.Append(Environment.NewLine);
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        var colName = CastDbNull.To(reader["ColName"], string.Empty).Trim();
                        sb.Append($@"TARG.{colName.Replace(",", "")} = SRC.{colName}");
                        sb.Append(Environment.NewLine);
                    }
                    reader.Close();
                }
                sb.Append("WHEN NOT MATCHED THEN ");
                sb.Append(Environment.NewLine);
                sb.Append("INSERT ");
                sb.Append(Environment.NewLine);
                sb.Append("( ");
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        var colName = CastDbNull.To(reader["ColName"], string.Empty).Trim();
                        sb.Append($@"{colName}");
                        sb.Append(Environment.NewLine);
                    }
                    reader.Close();
                }
                sb.Append(") ");
                sb.Append(Environment.NewLine);
                sb.Append("VALUES");
                sb.Append(Environment.NewLine);
                sb.Append("( ");
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        var colName = CastDbNull.To(reader["ColName"], string.Empty).Trim();
                        sb.Append($@"SRC.{colName}");
                        sb.Append(Environment.NewLine);
                    }
                    reader.Close();
                }
                sb.Append("); ");
                str = sb.ToString();
                //Logger.Info($@"str = {str}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {cnSource.ConnectionString}  {e.Message}: {e.StackTrace}");
                str = "";
            }
            return str;
        }

        public static int GetMergeAndTailRowCount(SqlConnection cnSource, ArchiveAndTailTableConfig archiveAndTailTableConfig)
        {
            int retVal = 0;
            try
            {
                archiveAndTailTableConfig.Statement = BuildArchiveAndTailRowCountString(cnSource, archiveAndTailTableConfig);
                SqlCommand cmd = new SqlCommand(archiveAndTailTableConfig.Statement, cnSource);
                cmd.CommandTimeout = 0;
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt32(cmd.ExecuteScalar());
                Logger.Info($@"retVal = {retVal}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {cnSource.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        private static String BuildArchiveAndTailRowCountString(SqlConnection cnSource, ArchiveAndTailTableConfig archiveAndTailTableConfig)
        {
            string str;
            try
            {
                var cmd = new SqlCommand();
                cmd = GetSourceTableMetadata(archiveAndTailTableConfig);
                cmd.Connection = cnSource;
                Logger.Info(Logging.DbCmdString(cmd));

                string prefix = archiveAndTailTableConfig.SourceTableName.Substring(0, 1).ToLower();
                StringBuilder sb = new StringBuilder();
                sb.Append($@"SELECT COUNT(*) ");
                sb.Append(Environment.NewLine);
                sb.Append($@"FROM [{archiveAndTailTableConfig.SourceDatabaseName}].[{archiveAndTailTableConfig.SourceSchemaName}].[{archiveAndTailTableConfig.SourceTableName}] AS {prefix} WITH (NOLOCK) ");
                sb.Append(Environment.NewLine);
                if (archiveAndTailTableConfig.UseHighwaterMark)
                {
                    sb.Append($@"WHERE {prefix}.[{archiveAndTailTableConfig.DateColumn}] >= '{archiveAndTailTableConfig.HighwaterMarkDate}' ");
                }
                else
                {
                    sb.Append($@"WHERE {prefix}.[{archiveAndTailTableConfig.DateColumn}] >= '{archiveAndTailTableConfig.SelectStartDate}' ");
                }
                str = sb.ToString();
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {cnSource.ConnectionString}  {e.Message}: {e.StackTrace}");
                str = "";
            }
            return str;
        }

        private static SqlCommand GetSourceTableMetadata(ArchiveAndTailTableConfig archiveAndTailTableConfig)
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
                           '[' + c.name + ']' + CASE
                                        WHEN c.column_id = x.MaxColId THEN
                                            ''
                                        ELSE
                                            ','
                                    END AS ColName,
                           c.column_id,
                           x.MaxColId,
                           c.max_length,
                           c.precision,
                           c.scale,
                           t.name AS type,
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
                    WHERE s.name = '{archiveAndTailTableConfig.SourceSchemaName}'
                        AND tb.name = '{archiveAndTailTableConfig.SourceTableName}'
                    ORDER BY c.column_id;
                ";
            }
            catch (Exception e)
            {
                throw new Exception(e.ToString());
            }
            return cmd;
        }


        private static SqlConnection GetSourceSqlConnection(string serverName, string databaseName)
        {
            SqlConnection cnDestination = new SqlConnection();
            try
            {
                cnDestination = ConnectionManager.GetSqlConnectionFromParts(serverName, databaseName);
                cnDestination.Open();
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new ApplicationException($@"[{serverName}].[{databaseName}]  {e.Message}: {e.StackTrace}");
            }
            return cnDestination;
        }

        private static SqlConnection GetDestinationSqlConnection(string serverName, string databaseName)
        {
            SqlConnection cnDestination = new SqlConnection();
            try
            {
                cnDestination = ConnectionManager.GetSqlConnectionFromParts(serverName, databaseName);
                cnDestination.Open();
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new ApplicationException($@"[{serverName}].[{databaseName}]  {e.Message}: {e.StackTrace}");
            }
            return cnDestination;
        }

        private static bool CreateDestinationDatabase(SqlConnection cnSource, string DestinationDBName, out string statement)
        {
            bool retVal = false;
            statement = $@"CREATE DATABASE [{DestinationDBName}]";
            try
            {
                SqlCommand cmd = new SqlCommand(statement, cnSource);
                Logger.Info(Logging.DbCmdString(cmd));
                var x = cmd.ExecuteNonQuery();
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = true;
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {cnSource.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

    }
}
