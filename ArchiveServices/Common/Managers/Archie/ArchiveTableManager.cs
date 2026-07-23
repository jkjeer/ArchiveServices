using ArchiveServices.Common.Types.Archie;
using ArchiveServices.Common.Types.Enterprise;
using ArchiveServices.Common.Types.Response;
using ArchiveServices.Common.Utilities;
using Microsoft.Data.SqlClient;
using NLog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ArchiveServices.Common.Managers.Archie
{
    public class ArchiveTableManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly string _ConfigTableName = "cfgArchiveTable";

        public static ProcessResponse Process(long batchID, string batchName, ArchiveTableConfig archiveTableConfig, SqlConnection cnCorpInt, SqlConnection cnArchConfig, SqlConnection cnSource, SqlConnection cnDestination, bool statsOnly)
        {
            archiveTableConfig.LastRunStartDate = DateTime.Now.ToLocalTime();
            var processName = $@"Archive Table: {archiveTableConfig.ServerQualifiedSourceName}";
            Logger.Info($"{processName} BEGIN: {DateTime.Now.ToLocalTime()}");
            archiveTableConfig.BatchID = batchID;
            archiveTableConfig.ProcessID = ArchieLoggingManager.LogProcessInsert(cnArchConfig, processName, batchID, archiveTableConfig);
            archiveTableConfig.AutoConfigDisable = Convert.ToBoolean(ConfigurationManager.AppSettings["AutoConfigDisable"]);
            archiveTableConfig.TestModeConfig = Convert.ToBoolean(ConfigurationManager.AppSettings["TestModeConfig"]);
            archiveTableConfig.TestModeNoDelete = Convert.ToBoolean(ConfigurationManager.AppSettings["TestModeNoDelete"]);
            archiveTableConfig.StatsOnly = statsOnly;
            archiveTableConfig. MaxStatsRows = Convert.ToInt64(ConfigurationManager.AppSettings["MaxStatsRows"]);
            if (archiveTableConfig.TestModeNoDelete) processName = $@"{processName} - Test Mode";
            ArchieLoggingManager.LogProcessUpdate(cnArchConfig, archiveTableConfig);
            var response = new ProcessResponse($@"BatchID [{batchID}] {batchName} ProcessID [{archiveTableConfig.ProcessID}] {processName}", archiveTableConfig, true);
            response.BatchID = batchID;
            response.ProcessID = archiveTableConfig.ProcessID;
            response.ArchiveDate = archiveTableConfig.LastRunStartDate;
            response.ConnectionStrings.Add(new ConnectionString(cnArchConfig, "cnCorpInt", cnCorpInt.ConnectionString, archiveTableConfig.BatchID, archiveTableConfig.ProcessID));
            response.ConnectionStrings.Add(new ConnectionString(cnArchConfig, "cnArchConfig", cnArchConfig.ConnectionString, archiveTableConfig.BatchID, archiveTableConfig.ProcessID));
            response.ConnectionStrings.Add(new ConnectionString("smtp", ConfigurationManager.AppSettings["SmtpHost"]));
            try
            {
                var stepValidateTable = new ProcessStep(cnArchConfig, archiveTableConfig.ProcessID, $@"Validate configuration in [Archiver].[dbo].[{_ConfigTableName}] for {archiveTableConfig.ServerQualifiedSourceName}");
                try
                {
                    archiveTableConfig.ValidationErrors = ValidateConfig(stepValidateTable.StepID, archiveTableConfig, cnArchConfig, cnSource, cnDestination);
                    stepValidateTable.RowsAffected = 1;
                }
                catch (Exception e)
                {
                    stepValidateTable.Errors.Add(new Error(e));
                }
                stepValidateTable.Done(cnArchConfig);
                response.ProcessSteps.Add(stepValidateTable);

                if (archiveTableConfig.ValidationErrors.Any())
                {
                    archiveTableConfig.LastRunStatus = "Error";
                    ArchieLoggingManager.LogProcessUpdate(cnArchConfig, archiveTableConfig);
                    foreach (var validationError in archiveTableConfig.ValidationErrors)
                    {
                        if (!response.Errors.Contains(new Error(validationError.Message))) response.Errors.Add(new Error(validationError.Message));
                    }
                    if (archiveTableConfig.AutoConfigDisable && archiveTableConfig.StatsOnly == false)
                    {
                        var stepUpdateArchiveTableConfigDisable = new ProcessStep(cnArchConfig, response.ProcessID, $@"Set [Archiver].[dbo].[{_ConfigTableName}].[IsEnabled] = FALSE for {archiveTableConfig.ServerQualifiedSourceName}");
                        try
                        {
                            stepUpdateArchiveTableConfigDisable.RowsAffected = UpdateArchiveTableConfigIsEnabled(cnArchConfig, archiveTableConfig, false);
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
                    var stepGetSourceSqlConnection = new ProcessStep(cnArchConfig, archiveTableConfig.ProcessID, $@"Get SQL connection for Source table {archiveTableConfig.ServerQualifiedSourceName}");
                    try
                    {
                        // Get Source SQL connection
                        cnSource = GetSourceSqlConnection(archiveTableConfig.SourceServerName, archiveTableConfig.SourceDatabaseName);
                        response.ConnectionStrings.Add(new ConnectionString(cnArchConfig, "cnSource", cnSource.ConnectionString, archiveTableConfig.BatchID, archiveTableConfig.ProcessID));
                        if (cnSource.State != ConnectionState.Open)
                        {
                            var validationError = new ValidationError($@"ERROR: Source server [{archiveTableConfig.SourceServerName}].[{archiveTableConfig.SourceDatabaseName}] ConnectionState is not Open.", "Validation", "Fatal", true);
                            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepGetSourceSqlConnection.StepID, validationError.Message, "");
                            archiveTableConfig.ValidationErrors.Add(validationError);
                            archiveTableConfig.ValidationStatus = validationError.Message;
                            archiveTableConfig.LastRunStatus = "Error";
                            stepGetSourceSqlConnection.Errors.Add(new Error(validationError.Message));
                            if (!response.Errors.Contains(new Error(validationError.Message))) response.Errors.Add(new Error(validationError.Message));
                            Logger.Error(validationError.Message);
                        }
                        else
                        {
                            // See if Source database exists
                            if (!TableManager.CheckSourceDatabaseExists(cnSource, archiveTableConfig))
                            {
                                var validationError = new ValidationError($@"ERROR: {cnSource.ConnectionString} Source database {archiveTableConfig.ServerQualifiedSourceName} not found.", "Validation", "Fatal", true);
                                var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepGetSourceSqlConnection.StepID, validationError.Message, "");
                                archiveTableConfig.ValidationErrors.Add(validationError);
                                archiveTableConfig.ValidationStatus = validationError.Message;
                                archiveTableConfig.LastRunStatus = "Error";
                                stepGetSourceSqlConnection.Errors.Add(new Error(validationError.Message));
                                if (!response.Errors.Contains(new Error(validationError.Message))) response.Errors.Add(new Error(validationError.Message));
                                Logger.Error(validationError.Message);
                            }
                            // See if Source schema exists
                            if (!TableManager.CheckSourceSchemaExists(cnSource, archiveTableConfig))
                            {
                                var validationError = new ValidationError($@"ERROR: {cnSource.ConnectionString} Source schema {archiveTableConfig.ServerQualifiedSourceName} not found.", "Validation", "Fatal", true);
                                var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepGetSourceSqlConnection.StepID, validationError.Message, "");
                                archiveTableConfig.ValidationErrors.Add(validationError);
                                archiveTableConfig.ValidationStatus = validationError.Message;
                                archiveTableConfig.LastRunStatus = "Error";
                                stepGetSourceSqlConnection.Errors.Add(new Error(validationError.Message));
                                if (!response.Errors.Contains(new Error(validationError.Message))) response.Errors.Add(new Error(validationError.Message));
                                Logger.Error(validationError.Message);
                            }
                            // See if Source table exists
                            if (TableManager.CheckSourceTableExists(cnSource, archiveTableConfig))
                            {
                                // Build SourceKeyColumns collection 
                                archiveTableConfig.SourceKeyColumns = new KeyColumns(); 
                                var keyColumnList = archiveTableConfig.SourceKey.Split(',').ToList();
                                foreach (var keyColumn in keyColumnList)
                                {
                                    archiveTableConfig.SourceKeyColumns.Add(GetKeyData(cnSource, archiveTableConfig.SourceSchemaName, archiveTableConfig.SourceTableName, keyColumn));
                                }
                                stepGetSourceSqlConnection.RowsAffected = keyColumnList.Count();
                                // See if Source table key is unique
                                var stepCheckIsSourceKeyColumnUnique = new ProcessStep(cnArchConfig, archiveTableConfig.ProcessID, $@"Check if Source key is unique for {archiveTableConfig.ServerQualifiedSourceName}");
                                try
                                {
                                    archiveTableConfig.IsSourceKeyUnique = TableManager.IsSourceKeyUnique(cnSource, archiveTableConfig);
                                    stepCheckIsSourceKeyColumnUnique.RowsAffected = 1;
                                }
                                catch (Exception e)
                                {
                                    stepCheckIsSourceKeyColumnUnique.Errors.Add(new Error(e));
                                }
                                stepCheckIsSourceKeyColumnUnique.Done(cnArchConfig);
                                response.ProcessSteps.Add(stepCheckIsSourceKeyColumnUnique);
                                if (!archiveTableConfig.IsSourceKeyUnique)
                                {
                                    var validationError = new ValidationError($@"ERROR: {cnDestination.ConnectionString} Source table {archiveTableConfig.ServerQualifiedSourceName} Key [{archiveTableConfig.SourceKey}] is not unique.", "Validation", "Fatal", true);
                                    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepCheckIsSourceKeyColumnUnique.StepID, validationError.Message, "");
                                    archiveTableConfig.ValidationErrors.Add(validationError);
                                    archiveTableConfig.ValidationStatus = validationError.Message;
                                    archiveTableConfig.LastRunStatus = "Error";
                                    if (!response.Errors.Contains(new Error(validationError.Message))) response.Errors.Add(new Error(validationError.Message));
                                    Logger.Error(validationError.Message);
                                }
                            }
                            else
                            {
                                var validationError = new ValidationError($@"ERROR: {cnSource.ConnectionString} Source table {archiveTableConfig.ServerQualifiedSourceName} not found.", "Validation", "Fatal", true);
                                var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepGetSourceSqlConnection.StepID, validationError.Message, "");
                                archiveTableConfig.ValidationErrors.Add(validationError);
                                archiveTableConfig.ValidationStatus = validationError.Message;
                                archiveTableConfig.LastRunStatus = "Error";
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
                    var stepGetDestinationSqlConnection = new ProcessStep(cnArchConfig, archiveTableConfig.ProcessID, $@"Get SQL connection for Destination table {archiveTableConfig.ServerQualifiedDestinationName}");
                    try
                    {
                        // Get Destination Table connection
                        cnDestination = GetDestinationSqlConnection(archiveTableConfig.DestinationServerName, archiveTableConfig.DestinationDatabaseName);
                        response.ConnectionStrings.Add(new ConnectionString(cnArchConfig, "cnDestination", cnDestination.ConnectionString, archiveTableConfig.BatchID, archiveTableConfig.ProcessID));
                        if (cnDestination.State != ConnectionState.Open)
                        {
                            var validationError = new ValidationError($@"ERROR: Destination server [{archiveTableConfig.DestinationServerName}].[{archiveTableConfig.DestinationDatabaseName}] ConnectionState is not Open.", "Validation", "Fatal", true);
                            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepGetDestinationSqlConnection.StepID, validationError.Message, "");
                            archiveTableConfig.ValidationErrors.Add(validationError);
                            archiveTableConfig.ValidationStatus = validationError.Message;
                            archiveTableConfig.LastRunStatus = "Error";
                            stepGetDestinationSqlConnection.Errors.Add(new Error(validationError.Message));
                            if (!response.Errors.Contains(new Error(validationError.Message))) response.Errors.Add(new Error(validationError.Message));
                            Logger.Error(validationError.Message);
                        }
                        else
                        {
                            // See if Destination database exists
                            if (!TableManager.CheckDestinationDatabaseExists(cnDestination, archiveTableConfig))
                            {
                                var stepCreateDestinationDatabase = new ProcessStep(cnArchConfig, archiveTableConfig.ProcessID, $@"Create Destination Database {archiveTableConfig.DestinationDatabaseName}");
                                try
                                {
                                    CreateDestinationDatabase(cnDestination, archiveTableConfig.DestinationDatabaseName, out string statement);
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
                            if (!TableManager.CheckDestinationSchemaExists(cnDestination, archiveTableConfig))
                            {
                                var stepCreateDestinationSchema = new ProcessStep(cnArchConfig, archiveTableConfig.ProcessID, $@"Create Destination Schema {archiveTableConfig.DestinationSchemaName}");
                                try
                                {
                                    var statement = "";
                                    TableManager.CreateDestinationSchema(cnDestination, archiveTableConfig, out statement);
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
                            if (!TableManager.CheckDestinationTableExists(cnDestination, archiveTableConfig))
                            {
                                var stepCreateDestinationTable = new ProcessStep(cnArchConfig, archiveTableConfig.ProcessID, $@"Create Destination Table {archiveTableConfig.DestinationTableName}");
                                try
                                {
                                    var statement = "";
                                    TableManager.CreateDestinationTable(cnSource, cnDestination, archiveTableConfig, out statement);
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
                    var stepCheckDestinationTableStructure = new ProcessStep(cnArchConfig, archiveTableConfig.ProcessID, $@"Check Destination Table Structure {archiveTableConfig.DestinationTableName}");
                    try
                    {
                        // See if Destination table structure matches Source table
                        if (TableManager.CheckDestinationTableStructureMissingColumns(cnDestination, archiveTableConfig))
                        {
                            var stepSyncDestinationTableStructureMissingColumns = new ProcessStep(cnArchConfig, archiveTableConfig.ProcessID, $@"Sync Destination Table Structure Missing Columns {archiveTableConfig.DestinationTableName}");
                            try
                            {
                                var statement = "";
                                TableManager.SyncDestinationTableStructureMissingColumns(cnSource, cnDestination, archiveTableConfig, out statement);
                                stepSyncDestinationTableStructureMissingColumns.RowsAffected = 1;
                                stepSyncDestinationTableStructureMissingColumns.Statement = statement;
                            }
                            catch (Exception e)
                            {
                                stepSyncDestinationTableStructureMissingColumns.Errors.Add(new Error(e));
                            }
                            stepSyncDestinationTableStructureMissingColumns.Done(cnArchConfig);
                        }
                        if (!TableManager.CheckDestinationTableStructureDifferentColumns(cnDestination, archiveTableConfig))
                        {
                            var stepSyncDestinationTableStructureDifferentColumns = new ProcessStep(cnArchConfig, archiveTableConfig.ProcessID, $@"Sync Destination Table Structure Different Columns {archiveTableConfig.DestinationTableName}");
                            try
                            {
                                var statement = "";
                                TableManager.SyncDestinationTableStructureDifferentColumns(cnSource, cnDestination, archiveTableConfig, out statement);
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

                //if (!response.Errors.Any())
                //{
                //    // Build DestinationKeyColumns collection
                //    var keyColumnList = archiveTableConfig.DestinationKey.Split(',').ToList();
                //    foreach (var keyColumn in keyColumnList)
                //    {
                //        archiveTableConfig.DestinationKeyColumns.Add(GetKeyData(cnDestination, archiveTableConfig.DestinationSchemaName, archiveTableConfig.DestinationTableName, keyColumn));
                //    }
                //    // See if Destination table has a unique index on DestinationKeyColumns (business key)
                //    if (!TableManager.CheckDestinationTableUniqueIndex(cnDestination, archiveTableConfig))
                //    {
                //        var stepCreateDestinationTableIndex = new ProcessStep(cnArchConfig, archiveTableConfig.ProcessID, $@"Create Destination Table Index {archiveTableConfig.DestinationTableName}");
                //        try
                //        {
                //            var statement = "";
                //            TableManager.DropDestinationTableIndex(cnDestination, archiveTableConfig);
                //            TableManager.CreateDestinationTableIndex(cnDestination, archiveTableConfig, out statement);
                //            stepCreateDestinationTableIndex.RowsAffected = 1;
                //            stepCreateDestinationTableIndex.Statement = statement;
                //        }
                //        catch (Exception e)
                //        {
                //            stepCreateDestinationTableIndex.Errors.Add(new Error(e));
                //        }
                //        stepCreateDestinationTableIndex.Done(cnArchConfig);
                //    }
                //}


                if (!response.Errors.Any())
                {
                    archiveTableConfig.DestinationKeyColumns = new KeyColumns(); 
                    // Build DestinationKeyColumns collection
                    var keyColumnList = archiveTableConfig.DestinationKey.Split(',').ToList();
                    foreach (var keyColumn in keyColumnList)
                    {
                        archiveTableConfig.DestinationKeyColumns.Add(GetKeyData(cnDestination, archiveTableConfig.DestinationSchemaName, archiveTableConfig.DestinationTableName, keyColumn));
                    }
                    // See if Destination table has an Archie unique nonclustered index
                    if (CheckDestinationTableArchieUniqueIndexExists(cnDestination, archiveTableConfig))
                    {
                        var stepDropDestinationTableArchieUniqueIndex = new ProcessStep(cnArchConfig, archiveTableConfig.ProcessID, $@"Drop Destination Table {archiveTableConfig.DestinationTableName} Archie Unique Index");
                        try
                        {
                            var statement = "";
                            DropDestinationTableArchieUniqueIndex(cnDestination, archiveTableConfig, out statement);
                            stepDropDestinationTableArchieUniqueIndex.RowsAffected = 1;
                            stepDropDestinationTableArchieUniqueIndex.Statement = statement;
                        }
                        catch (Exception e)
                        {
                            stepDropDestinationTableArchieUniqueIndex.Errors.Add(new Error(e));
                        }
                        stepDropDestinationTableArchieUniqueIndex.Done(cnArchConfig);
                    }
                }

                if (!response.Errors.Any())
                {
                    // Make sure Destination table has a clustered PK
                    if (!CheckDestinationTableClusteredPKExists(cnDestination, archiveTableConfig))
                    {
                        var stepCheckDestinationTableClusteredPK = new ProcessStep(cnArchConfig, archiveTableConfig.ProcessID, $@"Check Destination Table {archiveTableConfig.DestinationTableName} Clustered PK");
                        try
                        {
                            var statement = "";
                            foreach (var keyColumn in archiveTableConfig.DestinationKeyColumns)
                            {
                                if (keyColumn.IsNullable) SetDestinationColumnNotNullable(cnDestination, archiveTableConfig, keyColumn, out statement);
                            }
                            CreateDestinationClusteredPK(cnDestination, archiveTableConfig, out statement);
                            stepCheckDestinationTableClusteredPK.RowsAffected = archiveTableConfig.DestinationKeyColumns.Count();
                            stepCheckDestinationTableClusteredPK.Statement = statement;
                        }
                        catch (Exception e)
                        {
                            stepCheckDestinationTableClusteredPK.Errors.Add(new Error(e));
                        }
                        stepCheckDestinationTableClusteredPK.Done(cnArchConfig);
                    }
                }


                //if (!response.Errors.Any())
                //{
                //    var stepCheckIsDestinationKeyColumnUnique = new ProcessStep(cnArchConfig, archiveTableConfig.ProcessID, $@"Check if Destination key is unique for {archiveTableConfig.ServerQualifiedSourceName}");
                //    try
                //    {
                //        archiveTableConfig.IsDestinationKeyUnique = TableManager.IsDestinationKeyUnique(cnDestination, archiveTableConfig);
                //        stepCheckIsDestinationKeyColumnUnique.RowsAffected = 1;
                //    }
                //    catch (Exception e)
                //    {
                //        stepCheckIsDestinationKeyColumnUnique.Errors.Add(new Error(e));
                //    }
                //    stepCheckIsDestinationKeyColumnUnique.Done(cnArchConfig);
                //    response.ProcessSteps.Add(stepCheckIsDestinationKeyColumnUnique);
                //    if (!archiveTableConfig.IsDestinationKeyUnique)
                //    {
                //        var validationError = new ValidationError($@"ERROR: {cnDestination.ConnectionString} Source table {archiveTableConfig.ServerQualifiedSourceName} Destination Key [{archiveTableConfig.DestinationKey}] is not unique.", "Validation", "Fatal", true);
                //        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepCheckIsDestinationKeyColumnUnique.StepID, validationError.Message, "");
                //        archiveTableConfig.ValidationErrors.Add(validationError);
                //        archiveTableConfig.ValidationStatus = validationError.Message;
                //        Logger.Error(validationError.Message);
                //        if (!response.Errors.Contains(new Error(validationError.Message))) response.Errors.Add(new Error(validationError.Message));
                //    }
                //}

                if (!response.Errors.Any())
                {
                    var stepUpdateArchiveTableConfigIsRunningTrue = new ProcessStep(cnArchConfig, archiveTableConfig.ProcessID, $@"Set [Archiver].[dbo].[{_ConfigTableName}].[IsRunning] = TRUE for {archiveTableConfig.ServerQualifiedSourceName}");
                    try
                    {
                        archiveTableConfig.IsRunning = true;
                        UpdateArchiveTableConfigIsRunning(cnArchConfig, archiveTableConfig, true);
                        ArchieLoggingManager.LogProcessUpdate(cnArchConfig, archiveTableConfig);
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
                    var stepGatherInitialStatistics = new ProcessStep(cnArchConfig, archiveTableConfig.ProcessID, $@"Gather initial statistics for {archiveTableConfig.ServerQualifiedSourceName}");
                    try
                    {
                        archiveTableConfig.SourceInitialRowCount = GetRowCountSource(cnSource, archiveTableConfig);
                        archiveTableConfig.DestinationInitialRowCount = GetRowCountDestination(cnDestination, archiveTableConfig);
                        archiveTableConfig.LastRunStatus = "";
                        stepGatherInitialStatistics.RowsAffected = 1;
                    }
                    catch (Exception e)
                    {
                        stepGatherInitialStatistics.Errors.Add(new Error(e));
                    }
                    stepGatherInitialStatistics.Done(cnArchConfig);
                    response.ProcessSteps.Add(stepGatherInitialStatistics);
                }

                if (!response.Errors.Any() && archiveTableConfig.StatsOnly == false)
                {
                    var stepMergeTable = new ProcessStep(cnArchConfig, archiveTableConfig.ProcessID, $@"Merge table {archiveTableConfig.ServerQualifiedSourceName}");
                    try
                    {
                        archiveTableConfig.RowsAffected = MergeTable(cnSource, archiveTableConfig);
                        stepMergeTable.Statement = archiveTableConfig.Statement;
                        stepMergeTable.RowsAffected = archiveTableConfig.RowsAffected;
                        archiveTableConfig.LastRunStatus = "Success";
                    }
                    catch (Exception e)
                    {
                        stepMergeTable.Errors.Add(new Error(e));
                    }
                    stepMergeTable.Done(cnArchConfig);
                    response.ProcessSteps.Add(stepMergeTable);
                }

                if (!archiveTableConfig.ValidationErrors.Any())
                {
                    var stepGatherFinalStatistics = new ProcessStep(cnArchConfig, archiveTableConfig.ProcessID, $@"Gather final statistics for {archiveTableConfig.ServerQualifiedSourceName}");
                    try
                    {
                        archiveTableConfig.SourceFinalRowCount = GetRowCountSource(cnSource, archiveTableConfig);
                        archiveTableConfig.DestinationFinalRowCount = GetRowCountDestination(cnDestination, archiveTableConfig);
                        if (archiveTableConfig.SourceFinalRowCount < archiveTableConfig.MaxStatsRows && archiveTableConfig.DestinationFinalRowCount < archiveTableConfig.MaxStatsRows)
                        {
                            archiveTableConfig.HighwaterMarkDate = GetHighWaterMarkDate(cnSource, archiveTableConfig);
                            archiveTableConfig.HighwaterMarkDateString = archiveTableConfig.HighwaterMarkDate.ToString("M/dd/yyyy hh:mm:ss tt", CultureInfo.CreateSpecificCulture("en-US"));
                            archiveTableConfig.RowsRemaining = GetRowsRemaining(cnSource, archiveTableConfig);
                            archiveTableConfig.HighwaterMarkID = GetMaxDestinationID(cnDestination, archiveTableConfig);
                            archiveTableConfig.SourceMinDate = GetMinSourceDate(cnSource, archiveTableConfig);
                            archiveTableConfig.SourceMaxDate = GetMaxSourceDate(cnSource, archiveTableConfig);
                            archiveTableConfig.SourceMaxDateString = archiveTableConfig.SourceMaxDate.ToString("M/dd/yyyy hh:mm:ss tt", CultureInfo.CreateSpecificCulture("en-US"));
                            archiveTableConfig.DestinationMinDate = GetMinDestinationDate(cnDestination, archiveTableConfig);
                            archiveTableConfig.DestinationMaxDate = GetMaxDestinationDate(cnDestination, archiveTableConfig);
                            archiveTableConfig.DestinationMaxDateString = archiveTableConfig.DestinationMaxDate.ToString("M/dd/yyyy hh:mm:ss tt", CultureInfo.CreateSpecificCulture("en-US"));
                            //archiveTableConfig.SourceFinalRowCount = archiveTableConfig.DestinationRowCount;
                        }
                        else
                        {
                            archiveTableConfig.HighwaterMarkDate = DateTime.Parse("1900-01-01");  //GetHighWaterMarkDate(cnSource, archiveTableConfig);
                            archiveTableConfig.HighwaterMarkDateString = archiveTableConfig.HighwaterMarkDate.ToString("M/dd/yyyy hh:mm:ss tt", CultureInfo.CreateSpecificCulture("en-US"));
                            archiveTableConfig.RowsRemaining = 0; // GetRowsRemaining(cnSource, archiveTableConfig);
                            archiveTableConfig.HighwaterMarkID = 0; //GetMaxDestinationID(cnDestination, archiveTableConfig);
                            archiveTableConfig.SourceMinDate = DateTime.Parse("1900-01-01");  //GetMinSourceDate(cnSource, archiveTableConfig);
                            archiveTableConfig.SourceMaxDate = DateTime.Parse("1900-01-01");  //GetMaxSourceDate(cnSource, archiveTableConfig);
                            archiveTableConfig.SourceMaxDateString = archiveTableConfig.SourceMaxDate.ToString("M/dd/yyyy hh:mm:ss tt", CultureInfo.CreateSpecificCulture("en-US"));
                            archiveTableConfig.DestinationMinDate = DateTime.Parse("1900-01-01");  //GetMinDestinationDate(cnDestination, archiveTableConfig);
                            archiveTableConfig.DestinationMaxDate = DateTime.Parse("1900-01-01");  //GetMaxDestinationDate(cnDestination, archiveTableConfig);
                            archiveTableConfig.DestinationMaxDateString = archiveTableConfig.DestinationMaxDate.ToString("M/dd/yyyy hh:mm:ss tt", CultureInfo.CreateSpecificCulture("en-US"));
                        }
                        if (statsOnly == true) archiveTableConfig.LastRunStatus = "Success";
                            else
                            {
                                var stepUpdateHighWaterMark = new ProcessStep(cnArchConfig, archiveTableConfig.ProcessID, $@"Update HighWaterMark for {archiveTableConfig.ServerQualifiedSourceName}");
                                try
                                {
                                    UpdateArchiveTableConfigHighwaterMarks(cnArchConfig, archiveTableConfig);
                                    stepUpdateHighWaterMark.RowsAffected = 1;
                                }
                                catch (Exception e)
                                {
                                    stepUpdateHighWaterMark.Errors.Add(new Error(e));
                                }
                                stepUpdateHighWaterMark.Done(cnArchConfig);
                                response.ProcessSteps.Add(stepUpdateHighWaterMark);
                            }
                        response.RowsAffected = archiveTableConfig.RowsAffected;
                        response.InitialRowCount = archiveTableConfig.DestinationInitialRowCount;
                        response.FinalRowCount = archiveTableConfig.DestinationFinalRowCount;
                        response.SourceRowCount = archiveTableConfig.SourceRowCount;
                        //response.DestinationRowCount = archiveTableConfig.DestinationRowCount;
                        response.SFMinDate = archiveTableConfig.SourceMinDate;
                        response.SFDays = archiveTableConfig.SourceDays;
                        response.SourceMaxDate = archiveTableConfig.SourceMaxDate;
                        //response.DestinationMaxDate = archiveTableConfig.DestinationMaxDate;
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
                archiveTableConfig.IsRunning = false;
                archiveTableConfig.LastRunEndDate = DateTime.Now;
                var stepUpdateArchiveTableConfigDone = new ProcessStep(cnArchConfig, archiveTableConfig.ProcessID, $@"Set [Archiver].[dbo].[{_ConfigTableName}] DONE for {archiveTableConfig.ServerQualifiedSourceName}");
                UpdateArchiveTableConfigIsRunning(cnArchConfig, archiveTableConfig, false);
                ArchieLoggingManager.LogProcessUpdate(cnArchConfig, archiveTableConfig);
                stepUpdateArchiveTableConfigDone.RowsAffected = UpdateArchiveTableConfig(cnArchConfig, archiveTableConfig);
                stepUpdateArchiveTableConfigDone.Done(cnArchConfig);
                Logger.Info("END: " + Convert.ToDecimal((archiveTableConfig.LastRunEndDate - archiveTableConfig.LastRunStartDate).TotalSeconds) + " SECONDS");
            }
            return response;
        }

        public static bool CheckDestinationTableArchieUniqueIndexExists(SqlConnection cnDestination, ArchiveTableConfig archiveTableConfig)
        {
            bool retVal = false;
            try
            {
                //var x = string.Join("','", mergeTableConfig.DestinationKeyColumns);
                var columnsIn = "";
                var keyColumnList = archiveTableConfig.DestinationKey.Split(',').ToList();
                foreach (var keyColumn in keyColumnList)
                {
                    columnsIn += $@"'{keyColumn}',";
                }
                columnsIn = columnsIn.Remove(columnsIn.LastIndexOf(","));
                string cmdString = $@"
                    SELECT COUNT(*)
                    FROM
                    (
                        SELECT i.index_id,
                               i.object_id,
                               i.name AS IndexName,
                               OBJECT_SCHEMA_NAME(i.object_id) AS SchemaName,
                               OBJECT_NAME(i.object_id) AS TableName,
                               (
                                   SELECT c.name
                                   FROM sys.columns c
                                   WHERE object_id = ic.object_id
                                         AND c.column_id = ic.column_id
                                         AND c.name IN ({columnsIn})
                               ) AS ColumnName,
                               ic.index_column_id
                        FROM sys.index_columns ic
                            INNER JOIN sys.indexes i
                                ON i.object_id = ic.object_id
                                   AND i.index_id = ic.index_id
                        WHERE OBJECT_SCHEMA_NAME(i.object_id) = '{archiveTableConfig.DestinationSchemaName}'
                              AND OBJECT_NAME(i.object_id) = '{archiveTableConfig.DestinationTableName}'
                              AND i.name = 'IDX_Archie'
                              AND i.type = 2 -- Nonclustered rowstore (B-tree) 
                              AND i.is_unique = 1
                    ) a
                    WHERE ColumnName IS NOT NULL
                ";
                SqlCommand cmd = new SqlCommand(cmdString, cnDestination);
                Logger.Info(Logging.DbCmdString(cmd));
                var rowCount = Convert.ToInt32(cmd.ExecuteScalar());
                retVal = rowCount == keyColumnList.Count();
                Logger.Info($@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        public static bool DropDestinationTableArchieUniqueIndex(SqlConnection cnDestination, ArchiveTableConfig archiveTableConfig, out string statement)
        {
            bool retVal = false;
            statement = $@"drop index [IDX_Archie] on [{archiveTableConfig.DestinationSchemaName}].[{archiveTableConfig.DestinationTableName}]";
            try
            {
                SqlCommand cmd = new SqlCommand(statement, cnDestination);
                Logger.Info(Logging.DbCmdString(cmd));
                var x = cmd.ExecuteNonQuery();
                retVal = true;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        public static bool SetDestinationColumnNotNullable(SqlConnection cnDestination, ArchiveTableConfig archiveTableConfig, KeyColumn keyColumn,out string statement)
        {
            bool retVal = false;
            statement = $@"ALTER TABLE [{archiveTableConfig.DestinationSchemaName}].[{archiveTableConfig.DestinationTableName}] alter column [{keyColumn.ColumnName}] {keyColumn.AlterDataType} not null;";
            try
            {
                SqlCommand cmd = new SqlCommand(statement, cnDestination);
                cmd.CommandTimeout = 0;
                Logger.Info(Logging.DbCmdString(cmd));
                var x = cmd.ExecuteNonQuery();
                retVal = true;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        public static bool CreateDestinationClusteredPK(SqlConnection cnDestination, ArchiveTableConfig mergeTableConfig, out string statement)
        {
            bool retVal = false;
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($@"alter table [{mergeTableConfig.DestinationSchemaName}].[{mergeTableConfig.DestinationTableName}]");
                sb.Append(Environment.NewLine);
                sb.Append($@"add  constraint [PK_{mergeTableConfig.DestinationTableName}] primary key clustered ");
                sb.Append("(");
                var keyColumnList = mergeTableConfig.DestinationKey.Split(',').ToList();
                foreach (var keyColumn in keyColumnList)
                {
                    sb.Append($@"[{keyColumn}] ASC,");
                    sb.Append(Environment.NewLine);
                }
                statement = sb.ToString();
                statement = statement.Remove(statement.LastIndexOf(","));
                statement += $@")with (pad_index = off, statistics_norecompute = off, sort_in_tempdb = off, ignore_dup_key = off, online = off, allow_row_locks = on, allow_page_locks = on, optimize_for_sequential_key = off, data_compression = page) on [PRIMARY]";
                SqlCommand cmd = new SqlCommand(statement, cnDestination);
                cmd.CommandTimeout = 0;
                Logger.Info(Logging.DbCmdString(cmd));
                cmd.ExecuteNonQuery();
                retVal = true;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
                statement = "";
            }
            return retVal;
        }



        public static bool CheckDestinationTableClusteredPKExists(SqlConnection cnDestination, ArchiveTableConfig archiveTableConfig)
        {
            bool retVal = false;
            try
            {
                //var x = string.Join("','", mergeTableConfig.DestinationKeyColumns);
                var columnsIn = "";
                var keyColumnList = archiveTableConfig.DestinationKey.Split(',').ToList();
                foreach (var keyColumn in keyColumnList)
                {
                    columnsIn += $@"'{keyColumn}',";
                }
                columnsIn = columnsIn.Remove(columnsIn.LastIndexOf(","));
                string cmdString = $@"
                    SELECT COUNT(*)
                    FROM
                    (
                        SELECT i.index_id,
                               i.object_id,
                               i.name AS IndexName,
                               OBJECT_SCHEMA_NAME(i.object_id) AS SchemaName,
                               OBJECT_NAME(i.object_id) AS TableName,
                               (
                                   SELECT c.name
                                   FROM sys.columns c
                                   WHERE object_id = ic.object_id
                                         AND c.column_id = ic.column_id
                                         AND c.name IN ({columnsIn})
                               ) AS ColumnName,
                               ic.index_column_id
                        FROM sys.index_columns ic
                            INNER JOIN sys.indexes i
                                ON i.object_id = ic.object_id
                                   AND i.index_id = ic.index_id
                        WHERE OBJECT_SCHEMA_NAME(i.object_id) = '{archiveTableConfig.DestinationSchemaName}'
                              AND OBJECT_NAME(i.object_id) = '{archiveTableConfig.DestinationTableName}'
                              AND i.name = 'PK_{archiveTableConfig.DestinationTableName}'
                              AND i.type = 1 -- Clustered rowstore (B-tree)
                              AND i.is_unique = 1
                    ) a
                    WHERE ColumnName IS NOT NULL
                ";
                SqlCommand cmd = new SqlCommand(cmdString, cnDestination);
                Logger.Info(Logging.DbCmdString(cmd));
                var rowCount = Convert.ToInt32(cmd.ExecuteScalar());
                retVal = rowCount == keyColumnList.Count();
                Logger.Info($@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        public static bool CheckDestinationTableUniqueIndex(SqlConnection cnDestination, ArchiveTableConfig mergeTableConfig)
        {
            bool retVal = false;
            try
            {
                //var x = string.Join("','", mergeTableConfig.DestinationKeyColumns);
                var columnsIn = "";
                var keyColumnList = mergeTableConfig.DestinationKey.Split(',').ToList();
                foreach (var keyColumn in keyColumnList)
                {
                    columnsIn += $@"'{keyColumn}',";
                }
                columnsIn = columnsIn.Remove(columnsIn.LastIndexOf(","));
                string cmdString = $@"
                    SELECT COUNT(*)
                    FROM
                    (
                        SELECT i.index_id,
                               i.object_id,
                               i.name AS IndexName,
                               OBJECT_SCHEMA_NAME(i.object_id) AS SchemaName,
                               OBJECT_NAME(i.object_id) AS TableName,
                               (
                                   SELECT c.name
                                   FROM sys.columns c
                                   WHERE object_id = ic.object_id
                                         AND c.column_id = ic.column_id
                                         AND c.name IN ({columnsIn})
                               ) AS ColumnName,
                               ic.index_column_id,
                               ic.is_included_column
                        FROM sys.index_columns ic
                            INNER JOIN sys.indexes i
                                ON i.object_id = ic.object_id
                                   AND i.index_id = ic.index_id
                        WHERE OBJECT_SCHEMA_NAME(i.object_id) = '{mergeTableConfig.DestinationSchemaName}'
                              AND OBJECT_NAME(i.object_id) = '{mergeTableConfig.DestinationTableName}'
                              AND i.is_unique = 1
                              AND ic.is_included_column = 0
                    ) a
                    WHERE ColumnName IS NOT NULL
                ";
                SqlCommand cmd = new SqlCommand(cmdString, cnDestination);
                Logger.Info(Logging.DbCmdString(cmd));
                var rowCount = Convert.ToInt32(cmd.ExecuteScalar());
                retVal = rowCount == keyColumnList.Count();
                Logger.Info($@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }


        public static long MergeTable(SqlConnection cnSource, ArchiveTableConfig archiveTableConfig)
        {
            long rowCount = 0;
            try
            {
                archiveTableConfig.Statement = BuildMergeTableString(cnSource, archiveTableConfig);
                SqlCommand cmd = new SqlCommand(archiveTableConfig.Statement, cnSource);
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

        private static String BuildMergeTableString(SqlConnection cnSource, ArchiveTableConfig archiveTableConfig)
        {
            string str;
            try
            {
                var cmd = new SqlCommand();
                cmd = GetSourceTableMetadata(archiveTableConfig);
                cmd.Connection = cnSource;
                Logger.Info(Logging.DbCmdString(cmd));

                string prefix = archiveTableConfig.DestinationTableName.Substring(0,1).ToLower();
                StringBuilder sb = new StringBuilder();
                sb.Append($@"MERGE [{archiveTableConfig.DestinationDatabaseName}].[{archiveTableConfig.DestinationSchemaName}].[{archiveTableConfig.DestinationTableName}] AS DEST ");
                sb.Append(Environment.NewLine);
                sb.Append("USING ");
                sb.Append(Environment.NewLine);
                sb.Append("( ");
                sb.Append(Environment.NewLine);
                sb.Append($@"SELECT TOP ({archiveTableConfig.MaxRowsToMove})");
                sb.Append(Environment.NewLine);
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        var colName = CastDbNull.To(reader["ColName"], string.Empty).Trim();
                        if (!archiveTableConfig.ExcludeColumn.Contains(colName.Replace("[","").Replace("]","").Replace(",","")))
                        {
                            sb.Append($@"{prefix}.{colName}");
                            sb.Append(Environment.NewLine);
                        }
                    }
                    reader.Close();
                }
                sb.Append($@"FROM [{archiveTableConfig.SourceDatabaseName}].[{archiveTableConfig.SourceSchemaName}].[{archiveTableConfig.SourceTableName}] AS {prefix} WITH (NOLOCK) ");
                sb.Append(Environment.NewLine);
                if (archiveTableConfig.UseHighwaterMark)
                {
                    sb.Append($@"WHERE {prefix}.{archiveTableConfig.DateColumn} >= '{archiveTableConfig.OrigHighwaterMarkDate}' ");
                }
                else
                {
                    sb.Append($@"WHERE {prefix}.{archiveTableConfig.DateColumn} >= '{archiveTableConfig.SelectStartDate}' ");
                }
                sb.Append(Environment.NewLine);
                sb.Append($@"ORDER BY {archiveTableConfig.DateColumn}");
                sb.Append(Environment.NewLine);
                sb.Append(") AS SRC");
                sb.Append(Environment.NewLine);
                sb.Append("ON ( ");
                var keyColumnList = archiveTableConfig.DestinationKey.Split(',').ToList();
                var i = 0;
                foreach (var keyColumn in keyColumnList)
                {
                    i++;
                    sb.Append($@"DEST.[{keyColumn}] = SRC.[{keyColumn}]");
                    sb.Append(Environment.NewLine);
                    if (i < keyColumnList.Count)  sb.Append("AND "); 
                }
                sb.Append(") ");
                sb.Append(Environment.NewLine);
                sb.Append($@"WHEN MATCHED AND DEST.[{archiveTableConfig.DateColumn}] <> SRC.[{archiveTableConfig.DateColumn}] THEN ");
                sb.Append(Environment.NewLine);
                sb.Append("UPDATE SET ");
                sb.Append(Environment.NewLine);
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        var colName = CastDbNull.To(reader["ColName"], string.Empty).Trim();
                        if (!archiveTableConfig.ExcludeColumn.Contains(colName.Replace("[", "").Replace("]", "").Replace(",", "")))
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
                        if (!archiveTableConfig.ExcludeColumn.Contains(colName.Replace("[", "").Replace("]", "").Replace(",", "")))
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
                        if (!archiveTableConfig.ExcludeColumn.Contains(colName.Replace("[", "").Replace("]", "").Replace(",", "")))
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

        public static int GetArchiveRowCount(SqlConnection cnSource, ArchiveTableConfig archiveTableConfig)
        {
            int retVal = 0;
            try
            {
                archiveTableConfig.Statement = BuildMergeRowCountString(cnSource, archiveTableConfig);
                SqlCommand cmd = new SqlCommand(archiveTableConfig.Statement, cnSource);
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

        private static String BuildMergeRowCountString(SqlConnection cnSource, ArchiveTableConfig archiveTableConfig)
        {
            string str;
            try
            {
                var cmd = new SqlCommand();
                cmd = GetSourceTableMetadata(archiveTableConfig);
                cmd.Connection = cnSource;
                Logger.Info(Logging.DbCmdString(cmd));

                string prefix = archiveTableConfig.SourceTableName.Substring(0, 1).ToLower();
                StringBuilder sb = new StringBuilder();
                sb.Append($@"SELECT COUNT(*) ");
                sb.Append(Environment.NewLine);
                sb.Append($@"FROM [{archiveTableConfig.SourceDatabaseName}].[{archiveTableConfig.SourceSchemaName}].[{archiveTableConfig.SourceTableName}] AS {prefix} WITH (NOLOCK) ");
                sb.Append(Environment.NewLine);
                if (archiveTableConfig.UseHighwaterMark)
                {
                    sb.Append($@"WHERE {prefix}.[{archiveTableConfig.DateColumn}] >= '{archiveTableConfig.HighwaterMarkDate}' ");
                }
                else
                {
                    sb.Append($@"WHERE {prefix}.[{archiveTableConfig.DateColumn}] >= '{archiveTableConfig.SelectStartDate}' ");
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

        private static SqlCommand GetSourceTableMetadata(ArchiveTableConfig archiveTableConfig)
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
                    WHERE s.name = '{archiveTableConfig.SourceSchemaName}'
                        AND tb.name = '{archiveTableConfig.SourceTableName}'
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

        public static ArchiveTableConfigs GetArchiveTableConfigs(SqlConnection cnArchConfig, string groupName, string plantCode, string serverName, string databaseName, string schemaName, string tableName, bool useHighwaterMark, bool statsOnly)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            var testMode = Convert.ToBoolean(ConfigurationManager.AppSettings["TestMode"]);
            var testModeNoDelete = Convert.ToBoolean(ConfigurationManager.AppSettings["TestModeNoDelete"]);
            var configs = new ArchiveTableConfigs();
            var filters = new List<string>();
            var parameters = new List<SqlParameter>();
            try
            {
                if (string.IsNullOrEmpty(tableName))
                {
                    filters.Add("[IsArchive] = 1 AND \r\n");
                    if (statsOnly)
                    {
                    }
                    else if (testMode || testModeNoDelete)
                    {
                        filters.Add("[IsTestMode] = 1 AND \r\n");
                    }
                    else
                    {
                        filters.Add("[IsActive] = 1 AND \r\n");
                        //filters.Add("[IsEnabled] = 1 AND \r\n");
                        filters.Add("[IsTestMode] = 0 AND \r\n");
                    }
                }

                if (!string.IsNullOrEmpty(groupName))
                {
                    filters.Add("[GroupName] = @groupName AND \r\n");
                    parameters.Add(new SqlParameter("@groupName", SqlDbType.VarChar));
                    parameters.Last().Value = groupName;
                }
                if (!string.IsNullOrEmpty(plantCode))
                {
                    filters.Add("[PlantCode] = @plantCode AND \r\n");
                    parameters.Add(new SqlParameter("@plantCode", SqlDbType.VarChar));
                    parameters.Last().Value = plantCode;
                }
                if (!string.IsNullOrEmpty(serverName))
                {
                    filters.Add("[SourceServerName] = @sourceServerName AND \r\n");
                    parameters.Add(new SqlParameter("@sourceServerName", SqlDbType.VarChar));
                    parameters.Last().Value = serverName;
                }
                if (!string.IsNullOrEmpty(databaseName))
                {
                    filters.Add("[SourceDatabaseName] = @sourceDatabaseName AND \r\n");
                    parameters.Add(new SqlParameter("@sourceDatabaseName", SqlDbType.VarChar));
                    parameters.Last().Value = databaseName;
                }
                if (!string.IsNullOrEmpty(schemaName))
                {
                    filters.Add("[SourceSchemaName] = @sourceSchemaName AND \r\n");
                    parameters.Add(new SqlParameter("@sourceSchemaName", SqlDbType.VarChar));
                    parameters.Last().Value = schemaName;
                }
                if (!string.IsNullOrEmpty(tableName))
                {
                    filters.Add("[SourceTableName] = @sourceTableName AND \r\n");
                    parameters.Add(new SqlParameter("@sourceTableName", SqlDbType.VarChar));
                    parameters.Last().Value = tableName;
                }
                filters[filters.Count - 1] = filters.Last().Replace(" AND \r\n", "");
                var cmd = GetArchiveTableConfigs(filters);
                foreach (var parameter in parameters) cmd.Parameters.Add(parameter);
                cmd.Connection = cnArchConfig;
                Logger.Info(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        var config = new ArchiveTableConfig();
                        config.ConfigID = reader.Get<int>("ConfigID");
                        config.IsArchive = reader.Get<bool>("IsArchive");
                        config.IsTail = reader.Get<bool>("IsTail");
                        config.IsVerify = reader.Get<bool>("IsVerify");
                        config.IsActive = reader.Get<bool>("IsActive");
                        config.IsEnabled = reader.Get<bool>("IsEnabled");
                        config.IsTestMode = reader.Get<bool>("IsTestMode");
                        config.IsHammerMode = reader.Get<bool>("IsHammerMode");
                        config.IsRunning = reader.Get<bool>("IsRunning");
                        config.SourceServerName = reader.Get<string>("SourceServerName");
                        config.SourceDatabaseName = reader.Get<string>("SourceDatabaseName");
                        config.SourceSchemaName = reader.Get<string>("SourceSchemaName");
                        config.SourceTableName = reader.Get<string>("SourceTableName");
                        config.DestinationServerName = reader.Get<string>("DestinationServerName");
                        config.DestinationDatabaseName = reader.Get<string>("DestinationDatabaseName");
                        config.DestinationSchemaName = reader.Get<string>("DestinationSchemaName");
                        config.DestinationTableName = reader.Get<string>("DestinationTableName");
                        config.IDColumn = reader.Get<string>("IDColumn");
                        config.SourceKey = reader.Get<string>("SourceKey").Replace(" ", "");
                        config.DestinationKey = reader.Get<string>("DestinationKey").Replace(" ", "");
                        config.DateColumn = reader.Get<string>("DateColumn");
                        config.UseHighwaterMark = useHighwaterMark;
                        config.SelectStartDate = reader.Get<DateTime>("SelectStartDate");
                        config.HighwaterMarkDate = reader.Get<DateTime>("HighwaterMarkDate");
                        config.OrigHighwaterMarkDate = reader.Get<DateTime>("HighwaterMarkDate");
                        config.OrigHighwaterMarkDateString = config.HighwaterMarkDate.ToString("M/dd/yyyy hh:mm:ss tt", CultureInfo.CreateSpecificCulture("en-US"));
                        config.HighwaterMarkID = reader.Get<long>("HighwaterMarkID");
                        config.OtherCondition = reader.Get<string>("OtherCondition");
                        config.ExcludeColumn = reader.Get<string>("ExcludeColumn");
                        config.RunFrequencyDays = reader.Get<int>("RunFrequencyDays");
                        config.NextRunDate = reader.Get<DateTime>("NextRunDate");
                        config.MaxRowsToMove = reader.Get<int>("MaxRowsToMove");
                        config.CreatedBy = reader.Get<string>("CreatedBy");
                        config.LastEditBy = reader.Get<string>("LastEditBy");
                        foreach (var keyColumn in config.SourceKey.Split(',').ToList())
                        {
                            config.SourceKeyColumns.Add(new KeyColumn(keyColumn));
                        }
                        foreach (var keyColumn in config.DestinationKey.Split(',').ToList())
                        {
                            config.DestinationKeyColumns.Add(new KeyColumn(keyColumn));
                        }
                        if (!string.IsNullOrEmpty(config.ExcludeColumn))
                        {
                            foreach (var keyColumn in config.ExcludeColumn.Split(',').ToList())
                            {
                                config.ExcludeColumns.Add(new KeyColumn(keyColumn));
                            }
                        }
                        configs.Add(config);
                    }
                    reader.Close();
                }
                Logger.Info($@"ROWS: {configs.Count}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnArchConfig.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new Exception($"{cnArchConfig.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            finally
            {
                Logger.Info($"END: {Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds)} SECONDS");
            }
            return configs;
        }

        public static ValidationErrors ValidateConfig(int stepID, ArchiveTableConfig archiveTableConfig, SqlConnection cnArchConfig, SqlConnection cnSource, SqlConnection cnDestination)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {startTime}");
            var validationErrors = new ValidationErrors();
            try
            {
                //int maxMergeRows = Convert.ToInt32(ConfigurationManager.AppSettings["ArchieMaxMergeRows"]);

                if (string.IsNullOrEmpty(archiveTableConfig.SourceTableName))
                {
                    var validationError = new ValidationError($"{archiveTableConfig.ServerQualifiedDestinationName} is not found in [Archiver].[dbo].[cfgArchiveTable].", "Validation", "Fatal", true);
                    validationErrors.Add(validationError);
                    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                    archiveTableConfig.ValidationStatus = validationError.Message;
                    Logger.Error(validationError.Message);
                }
                else
                {
                    if (archiveTableConfig.IsRunning)
                    {
                        var validationError = new ValidationError($"Archive of {archiveTableConfig.ServerQualifiedDestinationName} is already Running.", "Validation", "Fatal", true);
                        validationErrors.Add(validationError);
                        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                        archiveTableConfig.ValidationStatus = validationError.Message;
                    }
                    if (archiveTableConfig.IsArchive == false)
                    {
                        var validationError = new ValidationError($"{archiveTableConfig.ServerQualifiedDestinationName} is not configured for Archiving.", "Validation", "Fatal", true);
                        validationErrors.Add(validationError);
                        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                        archiveTableConfig.ValidationStatus = validationError.Message;
                        Logger.Error(validationError.Message);
                    }
                    if (archiveTableConfig.IsActive == false)
                    {
                        var validationError = new ValidationError($"{archiveTableConfig.ServerQualifiedDestinationName} is not Active.", "Validation", "Fatal", true);
                        validationErrors.Add(validationError);
                        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                        archiveTableConfig.ValidationStatus = validationError.Message;
                        Logger.Error(validationError.Message);
                    }
                    if (archiveTableConfig.IsEnabled == false && archiveTableConfig.StatsOnly == false)
                    {
                        var validationError = new ValidationError($"{archiveTableConfig.ServerQualifiedDestinationName} is not Enabled.", "Validation", "Fatal", true);
                        validationErrors.Add(validationError);
                        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                        archiveTableConfig.ValidationStatus = validationError.Message;
                        Logger.Error(validationError.Message);
                    }
                    if (archiveTableConfig.TestModeConfig == true && archiveTableConfig.IsTestMode == false && archiveTableConfig.StatsOnly == false)
                    {
                        var validationError = new ValidationError($"{archiveTableConfig.ServerQualifiedDestinationName} is not configured for Test Mode but web.config is in Test Mode.", "Validation", "Fatal", true);
                        validationErrors.Add(validationError);
                        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                        archiveTableConfig.ValidationStatus = validationError.Message;
                        Logger.Error(validationError.Message);
                    }
                }
                //if (!archiveTableConfig.IsTestMode)
                //{
                //    if (!archiveTableConfig.IsActive)
                //    {
                //        var validationError = new ValidationError($"{archiveTableConfig.ServerQualifiedDestinationName} is Inactive.", "Validation", "Fatal", true);
                //        validationErrors.Add(validationError);
                //        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                //        archiveTableConfig.ValidationStatus = validationError.Message;
                //        Logger.Error(validationError.Message);
                //    }
                //    if (!archiveTableConfig.IsEnabled)
                //    {
                //        var validationError = new ValidationError($"{archiveTableConfig.ServerQualifiedDestinationName} is Disabled.", "Validation", "Fatal", true);
                //        validationErrors.Add(validationError);
                //        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                //        archiveTableConfig.ValidationStatus = validationError.Message;
                //        Logger.Error(validationError.Message);
                //    }
                //}
                //if (archiveTableConfig.DestinationDatabaseName == "PlantArchive" && (archiveTableConfig.SourceServerName != archiveTableConfig.DestinationServerName))
                //{
                //    var validationError = new ValidationError($"{archiveTableConfig.ServerQualifiedDestinationName} Source database must reside on the same server as the Destination.", "Validation", "Fatal", true);
                //    validationErrors.Add(validationError);
                //    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                //    archiveTableConfig.ValidationStatus = validationError.Message;
                //    Logger.Error(validationError.Message);
                //}
                if (string.IsNullOrEmpty(archiveTableConfig.SourceKey))
                {
                    var validationError = new ValidationError($"{archiveTableConfig.ServerQualifiedDestinationName} is not configured for SourceKey.", "Validation", "Fatal", true);
                    validationErrors.Add(validationError);
                    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                    archiveTableConfig.ValidationStatus = validationError.Message;
                    Logger.Error(validationError.Message);
                }
                if (string.IsNullOrEmpty(archiveTableConfig.DestinationKey))
                {
                    var validationError = new ValidationError($"{archiveTableConfig.ServerQualifiedDestinationName} is not configured for DestinationKey.", "Validation", "Fatal", true);
                    validationErrors.Add(validationError);
                    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                    archiveTableConfig.ValidationStatus = validationError.Message;
                    Logger.Error(validationError.Message);
                }
                if (string.IsNullOrEmpty(archiveTableConfig.DateColumn))
                {
                    var validationError = new ValidationError($"{archiveTableConfig.ServerQualifiedDestinationName} is not configured for DateColumn.", "Validation", "Fatal", true);
                    validationErrors.Add(validationError);
                    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                    archiveTableConfig.ValidationStatus = validationError.Message;
                    Logger.Error(validationError.Message);
                }
                if (archiveTableConfig.IDColumn.Contains(","))
                {
                    var validationError = new ValidationError($"IDColumn must be a single value for {archiveTableConfig.ServerQualifiedDestinationName}.", "Validation", "Fatal", true);
                    validationErrors.Add(validationError);
                    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                    archiveTableConfig.ValidationStatus = validationError.Message;
                    Logger.Error(validationError.Message);
                }
            }
            catch (Exception e)
            {
                Logger.Error($@"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new ApplicationException($@"{cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            finally
            {
                if (!validationErrors.Any()) archiveTableConfig.ValidationStatus = "Valid";
                Logger.Info($"END: {Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds)} SECONDS");
            }
            return validationErrors;
        }


        private static SqlCommand GetArchiveTableConfigs(List<string> filters)
        {
            var filterString = new StringBuilder();
            foreach (var filter in filters) filterString.Append(filter);
            var cmd = new SqlCommand();
            try
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = $@"
                    SELECT
                        [ConfigID]
                        ,[IsArchive]
                        ,[IsTail]
                        ,[IsVerify]
                        ,[IsActive]
                        ,[IsEnabled]
                        ,[IsTestMode]
                        ,[IsHammerMode]
                        ,[IsRunning]
                        ,[GroupName]
                        ,[PlantCode]
                        ,[SourceServerName]
                        ,[SourceDatabaseName]
                        ,[SourceSchemaName]
                        ,[SourceTableName]
                        ,[DestinationServerName]
                        ,[DestinationDatabaseName]
                        ,[DestinationSchemaName]
                        ,[DestinationTableName]
                        ,[SourceKey]
                        ,[IsSourceKeyUnique]
                        ,[DestinationKey]
                        ,[IsDestinationKeyUnique]
                        ,[DateColumn]
                        ,[SelectStartDate]
                        ,[IDColumn]
                        ,[HighwaterMarkDate]
                        ,[HighwaterMarkID]
                        ,[OtherCondition]
                        ,[ExcludeColumn]
                        ,[RunFrequencyDays]
                        ,[NextRunDate]
                          ,[MaxRowsToMove]
                          ,[SourceInitialRowCount]
                          ,[DestinationInitialRowCount]
                        ,[CreatedBy]
                        ,[LastEditBy]
                    FROM [dbo].[cfgArchiveTable]
                    WHERE
                       {filterString} 
                    ORDER BY [GroupName]
                        ,[PlantCode]
                        ,[SourceServerName]
                        ,[SourceDatabaseName]
                        ,[SourceSchemaName]
                        ,[SourceTableName]
                ";
            }
            catch (Exception e)
            {
                throw new Exception(e.ToString());
            }
            return cmd;
        }

        public static KeyColumn GetKeyData(SqlConnection cn, string schemaName, string tableName, string columnName)
        {
            KeyColumn keyColumn = new KeyColumn(columnName);
            try
            {
                var cmd = new SqlCommand();
                cmd = GetKeyColumnMetadata();
                cmd.Parameters["@SchemaName"].Value = schemaName;
                cmd.Parameters["@TableName"].Value = tableName;
                cmd.Parameters["@ColumnName"].Value = columnName;
                cmd.Connection = cn;
                Logger.Info(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        keyColumn.FieldDef = CastDbNull.To<string>(reader["FieldDef"], string.Empty).Trim();
                        keyColumn.AlterDataType = CastDbNull.To<string>(reader["AlterDataType"], string.Empty).Trim();
                        keyColumn.DataType = CastDbNull.To<string>(reader["DataType"], string.Empty).Trim();
                        keyColumn.MaxLength = CastDbNull.To<Int16>(reader["MaxLength"], 0);
                        keyColumn.Precision = CastDbNull.To<Byte>(reader["Precision"], 0);
                        keyColumn.Scale = CastDbNull.To<Byte>(reader["Scale"], 0);
                        keyColumn.IsNullable = CastDbNull.To<bool>(reader["IsNullable"], false);
                    }
                    reader.Close();
                }
                //Logger.Info($@"columnType = {DataType}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {cn.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return keyColumn;
        }

        private static SqlCommand GetKeyColumnMetadata()
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
                           '[' + UPPER(t.name) + ']' 
                           + case
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
                           END AS AlterDataType,
                           c.column_id,
                           x.MaxColId,
                           c.max_length AS MaxLength,
                           c.precision AS Precision,
                           c.scale AS Scale,
                           t.name AS DataType,
                           t.schema_id,
                           t.max_length AS t_max_length,
                           t.precision AS t_precision,
                           t.scale AS t_scale,
                           c.is_nullable as IsNullable
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
                        AND c.name = @ColumnName
                    ORDER BY c.column_id;
                ";
            }
            catch (Exception e)
            {
                throw new Exception(e.ToString());
            }
            cmd.Parameters.Add(new SqlParameter("@SchemaName", SqlDbType.VarChar));
            cmd.Parameters.Add(new SqlParameter("@TableName", SqlDbType.VarChar));
            cmd.Parameters.Add(new SqlParameter("@ColumnName", SqlDbType.VarChar));
            return cmd;
        }

        public static long UpdateArchiveTableConfigIsRunning(SqlConnection cnArchConfig, ArchiveTableConfig archiveTableConfig, bool isRunning)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            long rowCount;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnArchConfig,
                    CommandType = CommandType.Text,
                    CommandText = $@"
                        UPDATE xx
                        SET 
                            [IsRunning] = {Convert.ToInt32(isRunning)}
                            ,[ProcessID] = {archiveTableConfig.ProcessID}
                            ,[LastRunStatus] = '{archiveTableConfig.LastRunStatus}'
                            ,[LastRunStartDate] = '{archiveTableConfig.LastRunStartDate}'
                            ,[LastRunEndDate] = '{archiveTableConfig.LastRunEndDate}'
                            ,[IsSourceKeyUnique] = '{archiveTableConfig.IsSourceKeyUnique}'
                            ,[IsDestinationKeyUnique] = '{archiveTableConfig.IsDestinationKeyUnique}'
                            ,[BITimeStamp] = GETDATE()
                        FROM [dbo].[cfgArchiveTable] xx
                        WHERE [ConfigID] = {archiveTableConfig.ConfigID}
                    "
                };
                Logger.Info(Logging.DbCmdString(cmd));
                rowCount = Convert.ToInt64(cmd.ExecuteNonQuery());
                Logger.Info("ROWS: " + rowCount);
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnArchConfig.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new Exception($"{cnArchConfig.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return rowCount;
        }

        public static DateTime GetHighWaterMarkDate(SqlConnection cnSource, ArchiveTableConfig archiveTableConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            DateTime retVal;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnSource,
                    CommandType = CommandType.Text,
                    CommandTimeout = 0,
                    CommandText = BuildHighWaterMarkDateString(archiveTableConfig)
                };
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToDateTime(cmd.ExecuteScalar());
                Logger.Info("retVal: " + retVal);
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return retVal;
        }

        private static String BuildHighWaterMarkDateString(ArchiveTableConfig archiveTableConfig)
        {
            string str;
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($@"SELECT ISNULL(ISNULL(MIN([SRC].[{archiveTableConfig.DateColumn}]), (SELECT MAX([{archiveTableConfig.DateColumn}]) FROM [{archiveTableConfig.DestinationDatabaseName}].[{archiveTableConfig.DestinationSchemaName}].[{archiveTableConfig.DestinationTableName}] WITH (NOLOCK))), '1900-01-01') ");
                sb.Append(Environment.NewLine);
                sb.Append($@"FROM [{archiveTableConfig.SourceDatabaseName}].[{archiveTableConfig.SourceSchemaName}].[{archiveTableConfig.SourceTableName}] AS SRC WITH (NOLOCK) ");
                sb.Append(Environment.NewLine);
                sb.Append($@"WHERE NOT EXISTS (SELECT 1 FROM [{archiveTableConfig.DestinationDatabaseName}].[{archiveTableConfig.DestinationSchemaName}].[{archiveTableConfig.DestinationTableName}] DEST WITH (NOLOCK) ");
                sb.Append(Environment.NewLine);
                sb.Append("                  WHERE ");
                var keyColumnList = archiveTableConfig.DestinationKey.Split(',').ToList();
                var i = 0;
                foreach (var keyColumn in keyColumnList)
                {
                    i++;
                    sb.Append($@"DEST.[{keyColumn}] = SRC.[{keyColumn}]");
                    sb.Append(Environment.NewLine);
                    if (i < keyColumnList.Count) sb.Append("AND ");
                }
                sb.Append(") ");
                str = sb.ToString();
                //Logger.Info($@"str = {str}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {e.Message}: {e.StackTrace}");
                str = "";
            }
            return str;
        }

        public static long GetRowsRemaining(SqlConnection cnSource, ArchiveTableConfig archiveTableConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            long retVal;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnSource,
                    CommandType = CommandType.Text,
                    CommandTimeout = 0,
                    CommandText = BuildRowsRemainingString(archiveTableConfig)
                };
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt64(cmd.ExecuteScalar());
                Logger.Info("retVal: " + retVal);
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return retVal;
        }

        private static String BuildRowsRemainingString(ArchiveTableConfig archiveTableConfig)
        {
            string str;
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($@"SELECT COUNT(*) ");
                sb.Append(Environment.NewLine);
                sb.Append($@"FROM [{archiveTableConfig.SourceDatabaseName}].[{archiveTableConfig.SourceSchemaName}].[{archiveTableConfig.SourceTableName}] AS SRC WITH (NOLOCK) ");
                sb.Append(Environment.NewLine);
                sb.Append($@"WHERE NOT EXISTS (SELECT 1 FROM [{archiveTableConfig.DestinationDatabaseName}].[{archiveTableConfig.DestinationSchemaName}].[{archiveTableConfig.DestinationTableName}] DEST WITH (NOLOCK) ");
                sb.Append(Environment.NewLine);
                sb.Append("                  WHERE ");
                var keyColumnList = archiveTableConfig.DestinationKey.Split(',').ToList();
                var i = 0;
                foreach (var keyColumn in keyColumnList)
                {
                    i++;
                    sb.Append($@"DEST.[{keyColumn}] = SRC.[{keyColumn}]");
                    sb.Append(Environment.NewLine);
                    if (i < keyColumnList.Count) sb.Append("                AND ");
                }
                //sb.Append($@"                AND DEST.{archiveTableConfig.DateColumn} = SRC.{archiveTableConfig.DateColumn} ");
                sb.Append(") ");
                str = sb.ToString();
                //Logger.Info($@"str = {str}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {e.Message}: {e.StackTrace}");
                str = "";
            }
            return str;
        }

        public static Int64 GetMaxDestinationID(SqlConnection cnDestination, ArchiveTableConfig archiveTableConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            Int64 retVal = 0;
            try
            {
                if (!string.IsNullOrEmpty(archiveTableConfig.IDColumn))
                {
                    var cmd = new SqlCommand
                    {
                        Connection = cnDestination,
                        CommandType = CommandType.Text,
                        CommandTimeout = 0,
                        CommandText = $@"
                            SELECT ISNULL(MAX([{archiveTableConfig.IDColumn}]), 0)  
                            FROM [{archiveTableConfig.DestinationSchemaName}].[{archiveTableConfig.DestinationTableName}] WITH (NOLOCK) 
                    "
                    };
                    Logger.Info(Logging.DbCmdString(cmd));
                    retVal = Convert.ToInt64(cmd.ExecuteScalar());
                }
                Logger.Info("ROWS: " + retVal);
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
            return retVal;
        }


        private static long GetRowCountSource(SqlConnection cnSource, ArchiveTableConfig archiveTableConfig, string whereClause = null)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Debug($"BEGIN: {DateTime.Now.ToLocalTime()}");
            long rowCount;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnSource,
                    CommandType = CommandType.Text,
                    CommandTimeout = 0,
                    CommandText = $@"
                                            select 
                                                part.rows as SourceRowCount
                                            from 
                                                {archiveTableConfig.SourceDatabaseName}.sys.tables tbl
                                                INNER JOIN {archiveTableConfig.SourceDatabaseName}.sys.schemas sch ON sch.schema_id = tbl.schema_id
                                                INNER JOIN {archiveTableConfig.SourceDatabaseName}.sys.partitions part ON part.object_id = tbl.object_id
                                            WHERE 
                                                part.index_id IN (0, 1) -- 0 for heaps, 1 for clustered indexes (i.e., the table itself)
                                                and sch.name = '{archiveTableConfig.SourceSchemaName}' 
                                                and tbl.name = '{archiveTableConfig.SourceTableName}'
                    "
                    //CommandText = $@"
                    //    SELECT COUNT(*) 
                    //    FROM {tailTableWithVerifyConfig.FullyQualifiedSourceName} WITH (NOLOCK) 
                    //    {whereClause}
                    //"
                };
                Logger.Debug(Logging.DbCmdString(cmd));
                rowCount = Convert.ToInt64(cmd.ExecuteScalar());
                Logger.Debug("ROWS: " + rowCount);
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            finally
            {
                Logger.Debug("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return rowCount;
        }

        public static DateTime GetMinSourceDate(SqlConnection cnSource, ArchiveTableConfig mergeTableConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            DateTime retVal;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnSource,
                    CommandType = CommandType.Text,
                    CommandTimeout = 0,
                    CommandText = $@"
                        SELECT ISNULL(MIN([{mergeTableConfig.DateColumn}]), '1900-01-01')  
                        FROM [{mergeTableConfig.SourceSchemaName}].[{mergeTableConfig.SourceTableName}] WITH (NOLOCK) 
                    "
                };
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToDateTime(cmd.ExecuteScalar());
                Logger.Info("ROWS: " + retVal);
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return retVal;
        }

        public static DateTime GetMaxSourceDate(SqlConnection cnSource, ArchiveTableConfig mergeTableConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            DateTime retVal;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnSource,
                    CommandType = CommandType.Text,
                    CommandTimeout = 0,
                    CommandText = $@"
                        SELECT ISNULL(MAX([{mergeTableConfig.DateColumn}]), '1900-01-01')  
                        FROM [{mergeTableConfig.SourceSchemaName}].[{mergeTableConfig.SourceTableName}] WITH (NOLOCK) 
                    "
                };
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToDateTime(cmd.ExecuteScalar());
                Logger.Info("ROWS: " + retVal);
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return retVal;
        }


        private static long GetRowCountDestination(SqlConnection cnDestination, ArchiveTableConfig archiveTableConfig, string whereClause = null)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Debug($"BEGIN: {DateTime.Now.ToLocalTime()}");
            long rowCount;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnDestination,
                    CommandType = CommandType.Text,
                    CommandTimeout = 0,
                    CommandText = $@"
                                            select 
                                                part.rows as DestinationRowCount
                                            from 
                                                {archiveTableConfig.DestinationDatabaseName}.sys.tables tbl
                                                INNER JOIN {archiveTableConfig.DestinationDatabaseName}.sys.schemas sch ON sch.schema_id = tbl.schema_id
                                                INNER JOIN {archiveTableConfig.DestinationDatabaseName}.sys.partitions part ON part.object_id = tbl.object_id
                                            WHERE 
                                                part.index_id IN (0, 1) -- 0 for heaps, 1 for clustered indexes (i.e., the table itself)
                                                and sch.name = '{archiveTableConfig.DestinationSchemaName}' 
                                                and tbl.name = '{archiveTableConfig.DestinationTableName}'
                    "
                    //CommandText = $@"
                    //    SELECT COUNT(*) 
                    //    FROM {tailTableWithVerifyConfig.FullyQualifiedSourceName} WITH (NOLOCK) 
                    //    {whereClause}
                    //"
                };
                Logger.Debug(Logging.DbCmdString(cmd));
                rowCount = Convert.ToInt64(cmd.ExecuteScalar());
                Logger.Debug("ROWS: " + rowCount);
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            finally
            {
                Logger.Debug("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return rowCount;
        }

        public static DateTime GetMinDestinationDate(SqlConnection cnDestination, ArchiveTableConfig mergeTableConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            DateTime retVal;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnDestination,
                    CommandType = CommandType.Text,
                    CommandTimeout = 0,
                    CommandText = $@"
                        SELECT ISNULL(MIN([{mergeTableConfig.DateColumn}]), '1900-01-01')  
                        FROM [{mergeTableConfig.DestinationSchemaName}].[{mergeTableConfig.DestinationTableName}] WITH (NOLOCK) 
                    "
                };
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToDateTime(cmd.ExecuteScalar());
                Logger.Info("ROWS: " + retVal);
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
            return retVal;
        }

        public static DateTime GetMaxDestinationDate(SqlConnection cnDestination, ArchiveTableConfig mergeTableConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            DateTime retVal;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnDestination,
                    CommandType = CommandType.Text,
                    CommandTimeout = 0,
                    CommandText = $@"
                        SELECT ISNULL(MAX([{mergeTableConfig.DateColumn}]), '1900-01-01')  
                        FROM [{mergeTableConfig.DestinationSchemaName}].[{mergeTableConfig.DestinationTableName}] WITH (NOLOCK) 
                    "
                };
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToDateTime(cmd.ExecuteScalar());
                Logger.Info("ROWS: " + retVal);
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
            return retVal;
        }


        public static long UpdateArchiveTableConfigIsKeyColumnUnique(SqlConnection cnArchConfig, ArchiveTableConfig archiveTableConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            long rowCount;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnArchConfig,
                    CommandType = CommandType.Text,
                    CommandText = $@"
                        UPDATE xx
                        SET 
                            [IsSourceKeyUnique] = '{archiveTableConfig.IsSourceKeyUnique}'
                            ,[IsDestinationKeyUnique] = '{archiveTableConfig.IsDestinationKeyUnique}'
                            ,[BITimeStamp] = GETDATE()
                        FROM [dbo].[cfgArchiveTable] xx
                        WHERE [ConfigID] = {archiveTableConfig.ConfigID}
                    "
                };
                Logger.Info(Logging.DbCmdString(cmd));
                rowCount = Convert.ToInt64(cmd.ExecuteNonQuery());
                Logger.Info("ROWS: " + rowCount);
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnArchConfig.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new Exception($"{cnArchConfig.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return rowCount;
        }

        public static long UpdateArchiveTableConfig(SqlConnection cnArchConfig, ArchiveTableConfig archiveTableConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            long rowCount;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnArchConfig,
                    CommandType = CommandType.Text,
                    CommandText = $@"
                        UPDATE xx
                        SET 
                            [IsRunning] = {Convert.ToInt32(archiveTableConfig.IsRunning)}
                            ,[LastRunStatus] = '{archiveTableConfig.LastRunStatus}'
                            ,[SourceInitialRowCount] = {archiveTableConfig.SourceInitialRowCount}
                            ,[SourceFinalRowCount] = {archiveTableConfig.SourceFinalRowCount}
                            ,[DestinationInitialRowCount] = {archiveTableConfig.DestinationInitialRowCount}
                            ,[DestinationFinalRowCount] = {archiveTableConfig.DestinationFinalRowCount}
                            ,[RowsRemaining] = {archiveTableConfig.RowsRemaining}
                            ,[BITimeStamp] = GETDATE()
                        FROM [dbo].[cfgArchiveTable] xx
                        WHERE [ConfigID] = {archiveTableConfig.ConfigID}
                    "
                };
                Logger.Info(Logging.DbCmdString(cmd));
                rowCount = Convert.ToInt64(cmd.ExecuteNonQuery());
                Logger.Info("ROWS: " + rowCount);
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnArchConfig.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new Exception($"{cnArchConfig.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return rowCount;
        }

        public static long UpdateArchiveTableConfigDone(SqlConnection cnArchConfig, ArchiveTableConfig archiveTableConfig, out string statement)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            long rowCount;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnArchConfig,
                    CommandType = CommandType.Text,
                    CommandText = $@"
                        UPDATE xx
                        SET 
                            [IsRunning] = 'FALSE'
                            ,[LastRunStatus] = '{archiveTableConfig.LastRunStatus}'
                            ,[SourceRowCount] = {archiveTableConfig.SourceRowCount}
                            ,[DestinationFinalRowCount] = {archiveTableConfig.DestinationFinalRowCount}
                            ,[BITimeStamp] = GETDATE()
                        FROM [dbo].[cfgArchiveTable] xx
                        WHERE [ConfigID] = {archiveTableConfig.ConfigID}
                    "
                };
                statement = cmd.CommandText;
                Logger.Info(Logging.DbCmdString(cmd));
                rowCount = Convert.ToInt64(cmd.ExecuteNonQuery());
                Logger.Info("ROWS: " + rowCount);
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnArchConfig.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new Exception($"{cnArchConfig.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return rowCount;
        }

        public static long UpdateArchiveTableConfigIsEnabled(SqlConnection cnArchConfig, ArchiveTableConfig archiveTableConfig, bool isEnabled)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            long rowCount;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnArchConfig,
                    CommandType = CommandType.Text,
                    CommandText = $@"
                        UPDATE xx
                        SET 
                            [IsEnabled] = {Convert.ToInt32(isEnabled)}
                            ,[AutoConfigDisable] = {(archiveTableConfig.AutoConfigDisable == true ? 1 : 0)}
                            ,[BITimeStamp] = GETDATE()
                        FROM [dbo].[cfgArchiveTable] xx
                        WHERE [ConfigID] = {archiveTableConfig.ConfigID}
                    "
                };
                Logger.Info(Logging.DbCmdString(cmd));
                rowCount = Convert.ToInt64(cmd.ExecuteNonQuery());
                Logger.Info("ROWS: " + rowCount);
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnArchConfig.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new Exception($"{cnArchConfig.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return rowCount;
        }

        public static long UpdateArchiveTableConfigInitialRowCount(SqlConnection cnArchConfig, ArchiveTableConfig archiveTableConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            long rowCount;
            try
            {
                var cmd = new SqlCommand();
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = $@"
                    UPDATE xx
                    SET 
                        [DestinationInitialRowCount] = CASE WHEN [DestinationInitialRowCount] = 0 THEN {archiveTableConfig.DestinationInitialRowCount} ELSE [DestinationInitialRowCount] END
                        ,[BITimeStamp] = GETDATE()
                    FROM [dbo].[cfgArchiveTable] xx
                    WHERE [ConfigID] = {archiveTableConfig.ConfigID}
                ";
                cmd.Connection = cnArchConfig;
                Logger.Info(Logging.DbCmdString(cmd));
                rowCount = Convert.ToInt64(cmd.ExecuteNonQuery());
                Logger.Info("ROWS: " + rowCount);
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnArchConfig.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new Exception($"{cnArchConfig.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return rowCount;
        }

        public static long UpdateArchiveTableConfigHighwaterMarks(SqlConnection cnArchConfig, ArchiveTableConfig archiveTableConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            long rowCount;
            try
            {
                var cmd = new SqlCommand();
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = $@"
                    UPDATE xx
                    SET 
                        [HighwaterMarkDate] = '{archiveTableConfig.HighwaterMarkDate}'
                        ,[HighwaterMarkID] = {archiveTableConfig.HighwaterMarkID}
                        ,[SourceInitialRowCount] = {archiveTableConfig.SourceInitialRowCount}
                        ,[SourceFinalRowCount] = {archiveTableConfig.SourceFinalRowCount}
                        ,[SourceMinDate] = '{archiveTableConfig.SourceMinDate}'
                        ,[SourceMaxDate] = '{archiveTableConfig.SourceMaxDate}'
                        ,[DestinationInitialRowCount] = {archiveTableConfig.DestinationInitialRowCount}
                        ,[DestinationFinalRowCount] = {archiveTableConfig.DestinationFinalRowCount}
                        ,[DestinationMinDate] = '{archiveTableConfig.DestinationMinDate}'
                        ,[DestinationMaxDate] = '{archiveTableConfig.DestinationMaxDate}'
                        ,[RowsRemaining] = '{archiveTableConfig.RowsRemaining}'
                        ,[RowsMoved] = {archiveTableConfig.RowsAffected}
                        ,[BITimeStamp] = GETDATE()
                    FROM [dbo].[cfgArchiveTable] xx
                    WHERE [ConfigID] = {archiveTableConfig.ConfigID}
                ";
                cmd.Connection = cnArchConfig;
                Logger.Info(Logging.DbCmdString(cmd));
                rowCount = Convert.ToInt64(cmd.ExecuteNonQuery());
                Logger.Info("ROWS: " + rowCount);
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnArchConfig.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new Exception($"{cnArchConfig.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return rowCount;
        }
    }
}
