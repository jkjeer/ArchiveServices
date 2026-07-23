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
    public class ArchiveAndTailTableManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static ProcessResponse Process(int batchID, string batchName, ArchiveAndTailTableConfig archiveAndTailTableConfig, SqlConnection cnCorpInt, SqlConnection cnArchConfig, SqlConnection cnSource, SqlConnection cnDestination, bool statsOnly)
        {
            archiveAndTailTableConfig.LastRunStartDate = DateTime.Now.ToLocalTime();
            var processName = $@"Archive and Tail Table: {archiveAndTailTableConfig.ServerQualifiedSourceName}";
            Logger.Info($"{processName} BEGIN: {DateTime.Now.ToLocalTime()}");
            archiveAndTailTableConfig.BatchID = batchID;
            archiveAndTailTableConfig.ProcessID = ArchieLoggingManager.LogProcessInsert(cnArchConfig, processName, batchID, archiveAndTailTableConfig);
            archiveAndTailTableConfig.AutoConfigDisable = Convert.ToBoolean(ConfigurationManager.AppSettings["AutoConfigDisable"]);
            archiveAndTailTableConfig.TestModeConfig = Convert.ToBoolean(ConfigurationManager.AppSettings["TestModeConfig"]);
            archiveAndTailTableConfig.TestModeNoDelete = Convert.ToBoolean(ConfigurationManager.AppSettings["TestModeNoDelete"]);
            archiveAndTailTableConfig.StatsOnly = statsOnly;
            if (archiveAndTailTableConfig.TestModeNoDelete) processName = $@"{processName} - Test Mode";
            ArchieLoggingManager.LogProcessUpdate(cnArchConfig, archiveAndTailTableConfig);
            var response = new ProcessResponse($@"BatchID [{batchID}] {batchName} ProcessID [{archiveAndTailTableConfig.ProcessID}] {processName}", archiveAndTailTableConfig, true);
            response.BatchID = batchID;
            response.ProcessID = archiveAndTailTableConfig.ProcessID;
            response.ArchiveDate = archiveAndTailTableConfig.LastRunStartDate;
            response.ConnectionStrings.Add(new ConnectionString(cnArchConfig, "cnCorpInt", cnCorpInt.ConnectionString, archiveAndTailTableConfig.BatchID, archiveAndTailTableConfig.ProcessID));
            response.ConnectionStrings.Add(new ConnectionString(cnArchConfig, "cnArchConfig", cnArchConfig.ConnectionString, archiveAndTailTableConfig.BatchID, archiveAndTailTableConfig.ProcessID));
            response.ConnectionStrings.Add(new ConnectionString("smtp", ConfigurationManager.AppSettings["SmtpHost"]));
            try
            {
                var stepValidateTable = new ProcessStep(cnArchConfig, archiveAndTailTableConfig.ProcessID, $@"Validate configuration in [Archiver].[dbo].[cfgArchiveAndTailTable] for {archiveAndTailTableConfig.ServerQualifiedSourceName}");
                try
                {
                    archiveAndTailTableConfig.ValidationErrors = ArchiveAndTailTableConfigManager.ValidateConfig(stepValidateTable.StepID, archiveAndTailTableConfig, cnArchConfig, cnSource, cnDestination);
                    stepValidateTable.RowsAffected = 1;
                }
                catch (Exception e)
                {
                    stepValidateTable.Errors.Add(new Error(e));
                }
                stepValidateTable.Done(cnArchConfig);
                response.ProcessSteps.Add(stepValidateTable);

                if (archiveAndTailTableConfig.ValidationErrors.Any())
                {
                    archiveAndTailTableConfig.LastRunStatus = "Error";
                    ArchieLoggingManager.LogProcessUpdate(cnArchConfig, archiveAndTailTableConfig);
                    foreach (var validationError in archiveAndTailTableConfig.ValidationErrors)
                    {
                        if (!response.Errors.Contains(new Error(validationError.Message))) response.Errors.Add(new Error(validationError.Message));
                    }
                    if (archiveAndTailTableConfig.AutoConfigDisable && archiveAndTailTableConfig.StatsOnly == false)
                    {
                        var stepUpdateArchiveTableConfigDisable = new ProcessStep(cnArchConfig, response.ProcessID, $@"Set [Archiver].[dbo].[cfgArchiveAndTailTable].[IsEnabled] = FALSE for {archiveAndTailTableConfig.ServerQualifiedSourceName}");
                        try
                        {
                            stepUpdateArchiveTableConfigDisable.RowsAffected = ArchiveAndTailTableConfigManager.UpdateArchiveAndTailTableConfigIsEnabled(cnArchConfig, archiveAndTailTableConfig, false);
                        }
                        catch (Exception e)
                        {
                            stepUpdateArchiveTableConfigDisable.Errors.Add(new Error(e));
                        }
                        stepUpdateArchiveTableConfigDisable.Done(cnArchConfig);
                        response.ProcessSteps.Add(stepUpdateArchiveTableConfigDisable);
                    }
                }

                if (!response.Errors.Any())
                {
                    var stepGetSourceSqlConnection = new ProcessStep(cnArchConfig, archiveAndTailTableConfig.ProcessID, $@"Get SQL connection for Source table {archiveAndTailTableConfig.ServerQualifiedSourceName}");
                    try
                    {
                        // Get Source SQL connection
                        cnSource = GetSourceSqlConnection(archiveAndTailTableConfig.SourceServerName, archiveAndTailTableConfig.SourceDatabaseName);
                        response.ConnectionStrings.Add(new ConnectionString(cnArchConfig, "cnSource", cnSource.ConnectionString, archiveAndTailTableConfig.BatchID, archiveAndTailTableConfig.ProcessID));
                        if (cnSource.State != ConnectionState.Open)
                        {
                            var validationError = new ValidationError($@"ERROR: Source server [{archiveAndTailTableConfig.SourceServerName}].[{archiveAndTailTableConfig.SourceDatabaseName}] ConnectionState is not Open.", "Validation", "Fatal", true);
                            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepGetSourceSqlConnection.StepID, validationError.Message, "");
                            archiveAndTailTableConfig.ValidationErrors.Add(validationError);
                            archiveAndTailTableConfig.ValidationStatus = validationError.Message;
                            archiveAndTailTableConfig.LastRunStatus = "Error";
                            stepGetSourceSqlConnection.Errors.Add(new Error(validationError.Message));
                            if (!response.Errors.Contains(new Error(validationError.Message))) response.Errors.Add(new Error(validationError.Message));
                            Logger.Error(validationError.Message);
                        }
                        else
                        {
                            // See if Source database exists
                            if (!TableManager.CheckSourceDatabaseExists(cnSource, archiveAndTailTableConfig))
                            {
                                var validationError = new ValidationError($@"ERROR: {cnSource.ConnectionString} Source database {archiveAndTailTableConfig.ServerQualifiedSourceName} not found.", "Validation", "Fatal", true);
                                var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepGetSourceSqlConnection.StepID, validationError.Message, "");
                                archiveAndTailTableConfig.ValidationErrors.Add(validationError);
                                archiveAndTailTableConfig.ValidationStatus = validationError.Message;
                                archiveAndTailTableConfig.LastRunStatus = "Error";
                                stepGetSourceSqlConnection.Errors.Add(new Error(validationError.Message));
                                if (!response.Errors.Contains(new Error(validationError.Message))) response.Errors.Add(new Error(validationError.Message));
                                Logger.Error(validationError.Message);
                            }
                            // See if Source schema exists
                            if (!TableManager.CheckSourceSchemaExists(cnSource, archiveAndTailTableConfig))
                            {
                                var validationError = new ValidationError($@"ERROR: {cnSource.ConnectionString} Source schema {archiveAndTailTableConfig.ServerQualifiedSourceName} not found.", "Validation", "Fatal", true);
                                var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepGetSourceSqlConnection.StepID, validationError.Message, "");
                                archiveAndTailTableConfig.ValidationErrors.Add(validationError);
                                archiveAndTailTableConfig.ValidationStatus = validationError.Message;
                                archiveAndTailTableConfig.LastRunStatus = "Error";
                                stepGetSourceSqlConnection.Errors.Add(new Error(validationError.Message));
                                if (!response.Errors.Contains(new Error(validationError.Message))) response.Errors.Add(new Error(validationError.Message));
                                Logger.Error(validationError.Message);
                            }
                            // See if Source table exists
                            if (TableManager.CheckSourceTableExists(cnSource, archiveAndTailTableConfig))
                            {
                                // Build SourceKeyColumns collection 
                                var keyColumnList = archiveAndTailTableConfig.SourceKey.Split(',').ToList();
                                foreach (var keyColumn in keyColumnList)
                                {
                                    archiveAndTailTableConfig.SourceKeyColumns.Add(ArchiveAndTailTableConfigManager.GetKeyData(cnSource, archiveAndTailTableConfig.SourceSchemaName, archiveAndTailTableConfig.SourceTableName, keyColumn));
                                }
                                stepGetSourceSqlConnection.RowsAffected = 1;
                                // See if Source table key is unique
                                var stepCheckIsSourceKeyColumnUnique = new ProcessStep(cnArchConfig, archiveAndTailTableConfig.ProcessID, $@"Check if Source key is unique for {archiveAndTailTableConfig.ServerQualifiedSourceName}");
                                try
                                {
                                    archiveAndTailTableConfig.IsSourceKeyUnique = TableManager.IsSourceKeyUnique(cnSource, archiveAndTailTableConfig);
                                    stepCheckIsSourceKeyColumnUnique.RowsAffected = 1;
                                }
                                catch (Exception e)
                                {
                                    stepCheckIsSourceKeyColumnUnique.Errors.Add(new Error(e));
                                }
                                stepCheckIsSourceKeyColumnUnique.Done(cnArchConfig);
                                response.ProcessSteps.Add(stepCheckIsSourceKeyColumnUnique);
                                if (!archiveAndTailTableConfig.IsSourceKeyUnique)
                                {
                                    var validationError = new ValidationError($@"ERROR: {cnDestination.ConnectionString} Source table {archiveAndTailTableConfig.ServerQualifiedSourceName} Key [{archiveAndTailTableConfig.SourceKey}] is not unique.", "Validation", "Fatal", true);
                                    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepCheckIsSourceKeyColumnUnique.StepID, validationError.Message, "");
                                    archiveAndTailTableConfig.ValidationErrors.Add(validationError);
                                    archiveAndTailTableConfig.ValidationStatus = validationError.Message;
                                    archiveAndTailTableConfig.LastRunStatus = "Error";
                                    if (!response.Errors.Contains(new Error(validationError.Message))) response.Errors.Add(new Error(validationError.Message));
                                    Logger.Error(validationError.Message);
                                }
                            }
                            else
                            {
                                var validationError = new ValidationError($@"ERROR: {cnSource.ConnectionString} Source table {archiveAndTailTableConfig.ServerQualifiedSourceName} not found.", "Validation", "Fatal", true);
                                var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepGetSourceSqlConnection.StepID, validationError.Message, "");
                                archiveAndTailTableConfig.ValidationErrors.Add(validationError);
                                archiveAndTailTableConfig.ValidationStatus = validationError.Message;
                                archiveAndTailTableConfig.LastRunStatus = "Error";
                                stepGetSourceSqlConnection.Errors.Add(new Error(validationError.Message));
                                if (!response.Errors.Contains(new Error(validationError.Message))) response.Errors.Add(new Error(validationError.Message));
                                Logger.Error(validationError.Message);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        stepGetSourceSqlConnection.Errors.Add(new Error(e));
                    }
                    stepGetSourceSqlConnection.Done(cnArchConfig);
                    response.ProcessSteps.Add(stepGetSourceSqlConnection);
                }

                if (!response.Errors.Any())
                {
                    var stepGetDestinationSqlConnection = new ProcessStep(cnArchConfig, archiveAndTailTableConfig.ProcessID, $@"Get SQL connection for Destination table {archiveAndTailTableConfig.ServerQualifiedDestinationName}");
                    try
                    {
                        // Get Destination Table connection
                        cnDestination = GetDestinationSqlConnection(archiveAndTailTableConfig.DestinationServerName, archiveAndTailTableConfig.DestinationDatabaseName);
                        response.ConnectionStrings.Add(new ConnectionString(cnArchConfig, "cnDestination", cnDestination.ConnectionString, archiveAndTailTableConfig.BatchID, archiveAndTailTableConfig.ProcessID));
                        if (cnDestination.State != ConnectionState.Open)
                        {
                            var validationError = new ValidationError($@"ERROR: Destination server [{archiveAndTailTableConfig.DestinationServerName}].[{archiveAndTailTableConfig.DestinationDatabaseName}] ConnectionState is not Open.", "Validation", "Fatal", true);
                            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepGetDestinationSqlConnection.StepID, validationError.Message, "");
                            archiveAndTailTableConfig.ValidationErrors.Add(validationError);
                            archiveAndTailTableConfig.ValidationStatus = validationError.Message;
                            archiveAndTailTableConfig.LastRunStatus = "Error";
                            stepGetDestinationSqlConnection.Errors.Add(new Error(validationError.Message));
                            if (!response.Errors.Contains(new Error(validationError.Message))) response.Errors.Add(new Error(validationError.Message));
                            Logger.Error(validationError.Message);
                        }
                        else
                        {
                            // See if Destination database exists
                            if (!TableManager.CheckDestinationDatabaseExists(cnDestination, archiveAndTailTableConfig))
                            {
                                var stepCreateDestinationDatabase = new ProcessStep(cnArchConfig, archiveAndTailTableConfig.ProcessID, $@"Create Destination Database {archiveAndTailTableConfig.DestinationDatabaseName}");
                                try
                                {
                                    CreateDestinationDatabase(cnDestination, archiveAndTailTableConfig.DestinationDatabaseName, out string statement);
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
                                var stepCreateDestinationSchema = new ProcessStep(cnArchConfig, archiveAndTailTableConfig.ProcessID, $@"Create Destination Schema {archiveAndTailTableConfig.DestinationSchemaName}");
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
                                var stepCreateDestinationTable = new ProcessStep(cnArchConfig, archiveAndTailTableConfig.ProcessID, $@"Create Destination Table {archiveAndTailTableConfig.DestinationTableName}");
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
                        }
                    }
                    catch (Exception e)
                    {
                        stepGetDestinationSqlConnection.Errors.Add(new Error(e));
                    }
                    stepGetDestinationSqlConnection.RowsAffected = 1;
                    stepGetDestinationSqlConnection.Done(cnArchConfig);
                    response.ProcessSteps.Add(stepGetDestinationSqlConnection);
                }

                if (!response.Errors.Any())
                {
                    var stepCheckDestinationTableStructure = new ProcessStep(cnArchConfig, archiveAndTailTableConfig.ProcessID, $@"Check Destination Table Structure {archiveAndTailTableConfig.DestinationTableName}");
                    try
                    {
                        // See if Destination table structure matches Source table
                        if (TableManager.CheckDestinationTableStructureMissingColumns(cnDestination, archiveAndTailTableConfig))
                        {
                            var stepSyncDestinationTableStructureMissingColumns = new ProcessStep(cnArchConfig, archiveAndTailTableConfig.ProcessID, $@"Sync Destination Table Structure Missing Columns {archiveAndTailTableConfig.DestinationTableName}");
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
                            var stepSyncDestinationTableStructureDifferentColumns = new ProcessStep(cnArchConfig, archiveAndTailTableConfig.ProcessID, $@"Sync Destination Table Structure Different Columns {archiveAndTailTableConfig.DestinationTableName}");
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
                        stepCheckDestinationTableStructure.RowsAffected = 1;
                    }
                    catch (Exception e)
                    {
                        stepCheckDestinationTableStructure.Errors.Add(new Error(e));
                    }
                    stepCheckDestinationTableStructure.Done(cnArchConfig);
                    response.ProcessSteps.Add(stepCheckDestinationTableStructure);
                }

                if (!response.Errors.Any())
                {
                    // Build DestinationKeyColumns collection
                    var keyColumnList = archiveAndTailTableConfig.DestinationKey.Split(',').ToList();
                    foreach (var keyColumn in keyColumnList)
                    {
                        archiveAndTailTableConfig.DestinationKeyColumns.Add(ArchiveAndTailTableConfigManager.GetKeyData(cnDestination, archiveAndTailTableConfig.DestinationSchemaName, archiveAndTailTableConfig.DestinationTableName, keyColumn));
                    }
                    // See if Destination table has a unique index on DestinationKeyColumns (business key)
                    if (!TableManager.CheckDestinationTableUniqueIndex(cnDestination, archiveAndTailTableConfig))
                    {
                        var stepCreateDestinationTableIndex = new ProcessStep(cnArchConfig, archiveAndTailTableConfig.ProcessID, $@"Create Destination Table Index {archiveAndTailTableConfig.DestinationTableName}");
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
                }

                if (!response.Errors.Any())
                {
                    var stepCheckIsDestinationKeyColumnUnique = new ProcessStep(cnArchConfig, archiveAndTailTableConfig.ProcessID, $@"Check if Destination key is unique for {archiveAndTailTableConfig.ServerQualifiedSourceName}");
                    try
                    {
                        archiveAndTailTableConfig.IsDestinationKeyUnique = TableManager.IsDestinationKeyUnique(cnDestination, archiveAndTailTableConfig);
                        stepCheckIsDestinationKeyColumnUnique.RowsAffected = 1;
                    }
                    catch (Exception e)
                    {
                        stepCheckIsDestinationKeyColumnUnique.Errors.Add(new Error(e));
                    }
                    stepCheckIsDestinationKeyColumnUnique.Done(cnArchConfig);
                    response.ProcessSteps.Add(stepCheckIsDestinationKeyColumnUnique);
                    if (!archiveAndTailTableConfig.IsDestinationKeyUnique)
                    {
                        var validationError = new ValidationError($@"ERROR: {cnDestination.ConnectionString} Source table {archiveAndTailTableConfig.ServerQualifiedSourceName} Destination Key [{archiveAndTailTableConfig.DestinationKey}] is not unique.", "Validation", "Fatal", true);
                        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepCheckIsDestinationKeyColumnUnique.StepID, validationError.Message, "");
                        archiveAndTailTableConfig.ValidationErrors.Add(validationError);
                        archiveAndTailTableConfig.ValidationStatus = validationError.Message;
                        Logger.Error(validationError.Message);
                        if (!response.Errors.Contains(new Error(validationError.Message))) response.Errors.Add(new Error(validationError.Message));
                    }
                }

                if (!response.Errors.Any())
                {
                    var stepUpdateArchiveTableConfigIsRunningTrue = new ProcessStep(cnArchConfig, archiveAndTailTableConfig.ProcessID, $@"Set [Archiver].[dbo].[cfgArchiveAndTailTable].[IsRunning] = TRUE for {archiveAndTailTableConfig.ServerQualifiedSourceName}");
                    try
                    {
                        archiveAndTailTableConfig.IsRunning = true;
                        ArchiveAndTailTableConfigManager.UpdateArchiveAndTailTableConfigIsRunning(cnArchConfig, archiveAndTailTableConfig, true);
                        ArchieLoggingManager.LogProcessUpdate(cnArchConfig, archiveAndTailTableConfig);
                        stepUpdateArchiveTableConfigIsRunningTrue.RowsAffected = 1;
                    }
                    catch (Exception e)
                    {
                        stepUpdateArchiveTableConfigIsRunningTrue.Errors.Add(new Error(e));
                    }
                    stepUpdateArchiveTableConfigIsRunningTrue.Done(cnArchConfig);
                    response.ProcessSteps.Add(stepUpdateArchiveTableConfigIsRunningTrue);
                }

                if (!response.Errors.Any())
                {
                    var stepGatherInitialStatistics = new ProcessStep(cnArchConfig, archiveAndTailTableConfig.ProcessID, $@"Gather initial statistics for {archiveAndTailTableConfig.ServerQualifiedSourceName}");
                    try
                    {
                        archiveAndTailTableConfig.SourceInitialRowCount = TableManager.GetRowCountSource(cnSource, archiveAndTailTableConfig);
                        archiveAndTailTableConfig.DestinationInitialRowCount = TableManager.GetRowCountDestination(cnDestination, archiveAndTailTableConfig);
                        archiveAndTailTableConfig.LastRunStatus = "";
                        stepGatherInitialStatistics.RowsAffected = 1;
                    }
                    catch (Exception e)
                    {
                        stepGatherInitialStatistics.Errors.Add(new Error(e));
                    }
                    stepGatherInitialStatistics.Done(cnArchConfig);
                    response.ProcessSteps.Add(stepGatherInitialStatistics);
                }

                if (!response.Errors.Any() && archiveAndTailTableConfig.StatsOnly == false)
                {
                    var stepMergeTable = new ProcessStep(cnArchConfig, archiveAndTailTableConfig.ProcessID, $@"Merge table {archiveAndTailTableConfig.ServerQualifiedSourceName}");
                    try
                    {
                        archiveAndTailTableConfig.RowsAffected = MergeTable(cnSource, archiveAndTailTableConfig);
                        stepMergeTable.Statement = archiveAndTailTableConfig.Statement;
                        stepMergeTable.RowsAffected = archiveAndTailTableConfig.RowsAffected;
                        archiveAndTailTableConfig.LastRunStatus = "Success";
                    }
                    catch (Exception e)
                    {
                        stepMergeTable.Errors.Add(new Error(e));
                    }
                    stepMergeTable.Done(cnArchConfig);
                    response.ProcessSteps.Add(stepMergeTable);
                }

                if (!archiveAndTailTableConfig.ValidationErrors.Any())
                {
                    var stepGatherFinalStatistics = new ProcessStep(cnArchConfig, archiveAndTailTableConfig.ProcessID, $@"Gather final statistics for {archiveAndTailTableConfig.ServerQualifiedSourceName}");
                    try
                    {
                        archiveAndTailTableConfig.HighwaterMarkDate = TableManager.GetHighWaterMarkDate(cnSource, archiveAndTailTableConfig);
                        archiveAndTailTableConfig.HighwaterMarkDateString = archiveAndTailTableConfig.HighwaterMarkDate.ToString("M/dd/yyyy hh:mm:ss tt", CultureInfo.CreateSpecificCulture("en-US"));
                        archiveAndTailTableConfig.RowsRemaining = TableManager.GetRowsRemaining(cnSource, archiveAndTailTableConfig);
                        archiveAndTailTableConfig.HighwaterMarkID = TableManager.GetMaxDestinationID(cnDestination, archiveAndTailTableConfig);
                        archiveAndTailTableConfig.SourceFinalRowCount = TableManager.GetRowCountSource(cnSource, archiveAndTailTableConfig);
                        archiveAndTailTableConfig.SourceMinDate = TableManager.GetMinSourceDate(cnSource, archiveAndTailTableConfig);
                        archiveAndTailTableConfig.SourceMaxDate = TableManager.GetMaxSourceDate(cnSource, archiveAndTailTableConfig);
                        archiveAndTailTableConfig.SourceMaxDateString = archiveAndTailTableConfig.SourceMaxDate.ToString("M/dd/yyyy hh:mm:ss tt", CultureInfo.CreateSpecificCulture("en-US"));
                        archiveAndTailTableConfig.DestinationFinalRowCount = TableManager.GetRowCountDestination(cnDestination, archiveAndTailTableConfig);
                        archiveAndTailTableConfig.DestinationMinDate = TableManager.GetMinDestinationDate(cnDestination, archiveAndTailTableConfig);
                        archiveAndTailTableConfig.DestinationMaxDate = TableManager.GetMaxDestinationDate(cnDestination, archiveAndTailTableConfig);
                        archiveAndTailTableConfig.DestinationMaxDateString = archiveAndTailTableConfig.DestinationMaxDate.ToString("M/dd/yyyy hh:mm:ss tt", CultureInfo.CreateSpecificCulture("en-US"));
                        //archiveAndTailTableConfig.SourceFinalRowCount = archiveAndTailTableConfig.DestinationRowCount;
                        if (statsOnly == true) archiveAndTailTableConfig.LastRunStatus = "Success";
                        else
                        {
                            var stepUpdateHighWaterMark = new ProcessStep(cnArchConfig, archiveAndTailTableConfig.ProcessID, $@"Update HighWaterMark for {archiveAndTailTableConfig.ServerQualifiedSourceName}");
                            try
                            {
                                ArchiveAndTailTableConfigManager.UpdateArchiveAndTailTableConfigHighwaterMarks(cnArchConfig, archiveAndTailTableConfig);
                                stepUpdateHighWaterMark.RowsAffected = 1;
                            }
                            catch (Exception e)
                            {
                                stepUpdateHighWaterMark.Errors.Add(new Error(e));
                            }
                            stepUpdateHighWaterMark.Done(cnArchConfig);
                            response.ProcessSteps.Add(stepUpdateHighWaterMark);
                        }
                        response.RowsAffected = archiveAndTailTableConfig.RowsAffected;
                        response.InitialRowCount = archiveAndTailTableConfig.DestinationInitialRowCount;
                        response.FinalRowCount = archiveAndTailTableConfig.DestinationFinalRowCount;
                        response.SourceRowCount = archiveAndTailTableConfig.SourceRowCount;
                        //response.DestinationRowCount = archiveAndTailTableConfig.DestinationRowCount;
                        response.SFMinDate = archiveAndTailTableConfig.SourceMinDate;
                        response.SFDays = archiveAndTailTableConfig.SourceDays;
                        response.SourceMaxDate = archiveAndTailTableConfig.SourceMaxDate;
                        //response.DestinationMaxDate = archiveAndTailTableConfig.DestinationMaxDate;
                        stepGatherFinalStatistics.RowsAffected = 1;
                    }
                    catch (Exception e)
                    {
                        stepGatherFinalStatistics.Errors.Add(new Error(e));
                    }
                    stepGatherFinalStatistics.Done(cnArchConfig);
                    response.ProcessSteps.Add(stepGatherFinalStatistics);
                }
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            finally
            {
                archiveAndTailTableConfig.IsRunning = false;
                archiveAndTailTableConfig.LastRunEndDate = DateTime.Now;
                var stepUpdateArchiveTableConfigDone = new ProcessStep(cnArchConfig, archiveAndTailTableConfig.ProcessID, $@"Set [Archiver].[dbo].[cfgArchiveAndTailTable] DONE for {archiveAndTailTableConfig.ServerQualifiedSourceName}");
                ArchiveAndTailTableConfigManager.UpdateArchiveAndTailTableConfigIsRunning(cnArchConfig, archiveAndTailTableConfig, false);
                ArchieLoggingManager.LogProcessUpdate(cnArchConfig, archiveAndTailTableConfig);
                stepUpdateArchiveTableConfigDone.RowsAffected = ArchiveAndTailTableConfigManager.UpdateArchiveAndTailTableConfig(cnArchConfig, archiveAndTailTableConfig);
                stepUpdateArchiveTableConfigDone.Done(cnArchConfig);
                Logger.Info("END: " + Convert.ToDecimal((archiveAndTailTableConfig.LastRunEndDate - archiveAndTailTableConfig.LastRunStartDate).TotalSeconds) + " SECONDS");
            }
            return response;
        }

        public static long MergeTable(SqlConnection cnSource, ArchiveAndTailTableConfig archiveAndTailTableConfig)
        {
            long rowCount = 0;
            try
            {
                archiveAndTailTableConfig.Statement = BuildMergeTableString(cnSource, archiveAndTailTableConfig);
                SqlCommand cmd = new SqlCommand(archiveAndTailTableConfig.Statement, cnSource);
                cmd.CommandTimeout = 0;
                Logger.Info(Logging.DbCmdString(cmd));
                rowCount = Convert.ToInt64(cmd.ExecuteNonQuery());
                Logger.Info($@"rowCount = {rowCount}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {cnSource.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return rowCount;
        }

        private static String BuildMergeTableString(SqlConnection cnSource, ArchiveAndTailTableConfig archiveAndTailTableConfig)
        {
            string str;
            try
            {
                var cmd = new SqlCommand();
                cmd = GetSourceTableMetadata(archiveAndTailTableConfig);
                cmd.Connection = cnSource;
                Logger.Info(Logging.DbCmdString(cmd));

                string prefix = archiveAndTailTableConfig.DestinationTableName.Substring(0, 1).ToLower();
                StringBuilder sb = new StringBuilder();
                sb.Append($@"MERGE [{archiveAndTailTableConfig.DestinationDatabaseName}].[{archiveAndTailTableConfig.DestinationSchemaName}].[{archiveAndTailTableConfig.DestinationTableName}] AS DEST ");
                sb.Append(Environment.NewLine);
                sb.Append("USING ");
                sb.Append(Environment.NewLine);
                sb.Append("( ");
                sb.Append(Environment.NewLine);
                sb.Append($@"SELECT TOP ({archiveAndTailTableConfig.MaxRowsToMove})");
                sb.Append(Environment.NewLine);
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        var colName = CastDbNull.To(reader["ColName"], string.Empty).Trim();
                        if (!archiveAndTailTableConfig.ExcludeColumn.Contains(colName.Replace("[", "").Replace("]", "").Replace(",", "")))
                        {
                            sb.Append($@"{prefix}.{colName}");
                            sb.Append(Environment.NewLine);
                        }
                    }
                    reader.Close();
                }
                sb.Append($@"FROM [{archiveAndTailTableConfig.SourceDatabaseName}].[{archiveAndTailTableConfig.SourceSchemaName}].[{archiveAndTailTableConfig.SourceTableName}] AS {prefix} WITH (NOLOCK) ");
                sb.Append(Environment.NewLine);
                if (archiveAndTailTableConfig.UseHighwaterMark)
                {
                    sb.Append($@"WHERE {prefix}.{archiveAndTailTableConfig.DateColumn} >= '{archiveAndTailTableConfig.OrigHighwaterMarkDate}' ");
                }
                else
                {
                    sb.Append($@"WHERE {prefix}.{archiveAndTailTableConfig.DateColumn} >= '{archiveAndTailTableConfig.SelectStartDate}' ");
                }
                sb.Append(Environment.NewLine);
                sb.Append($@"ORDER BY {archiveAndTailTableConfig.DateColumn}");
                sb.Append(Environment.NewLine);
                sb.Append(") AS SRC");
                sb.Append(Environment.NewLine);
                sb.Append("ON ( ");
                var keyColumnList = archiveAndTailTableConfig.DestinationKey.Split(',').ToList();
                var i = 0;
                foreach (var keyColumn in keyColumnList)
                {
                    i++;
                    sb.Append($@"DEST.[{keyColumn}] = SRC.[{keyColumn}]");
                    sb.Append(Environment.NewLine);
                    if (i < keyColumnList.Count) sb.Append("AND ");
                }
                sb.Append(") ");
                sb.Append(Environment.NewLine);
                sb.Append($@"WHEN MATCHED AND DEST.[{archiveAndTailTableConfig.DateColumn}] <> SRC.[{archiveAndTailTableConfig.DateColumn}] THEN ");
                sb.Append(Environment.NewLine);
                sb.Append("UPDATE SET ");
                sb.Append(Environment.NewLine);
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        var colName = CastDbNull.To(reader["ColName"], string.Empty).Trim();
                        if (!archiveAndTailTableConfig.ExcludeColumn.Contains(colName.Replace("[", "").Replace("]", "").Replace(",", "")))
                        {
                            sb.Append($@"DEST.{colName.Replace(",", "")} = SRC.{colName}");
                            sb.Append(Environment.NewLine);
                        }
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
                        if (!archiveAndTailTableConfig.ExcludeColumn.Contains(colName.Replace("[", "").Replace("]", "").Replace(",", "")))
                        {
                            sb.Append($@"{colName}");
                            sb.Append(Environment.NewLine);
                        }
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
                        if (!archiveAndTailTableConfig.ExcludeColumn.Contains(colName.Replace("[", "").Replace("]", "").Replace(",", "")))
                        {
                            sb.Append($@"SRC.{colName}");
                            sb.Append(Environment.NewLine);
                        }
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

        public static int GetArchiveRowCount(SqlConnection cnSource, ArchiveAndTailTableConfig archiveAndTailTableConfig)
        {
            int retVal = 0;
            try
            {
                archiveAndTailTableConfig.Statement = BuildMergeRowCountString(cnSource, archiveAndTailTableConfig);
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

        private static String BuildMergeRowCountString(SqlConnection cnSource, ArchiveAndTailTableConfig archiveAndTailTableConfig)
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
                var errMsg = $@"Unable to open a connection to Destination Database [{serverName}].[{databaseName}]";
                Logger.Error($"ERROR: {errMsg}");
                //Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
                //throw new ApplicationException($@"[{serverName}].[{databaseName}]  {e.Message}: {e.StackTrace}");
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
