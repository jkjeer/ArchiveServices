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
using System.Globalization;
using System.Linq;
using System.Text;

namespace ArchiveServices.Common.Managers.Archie
{
    public class TailTableWithVerifyManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private const string _ConfigTableName = "cfgTailTableWithVerify";

        public static ProcessResponse Process(long batchID, string batchName, TailTableWithVerifyConfig tailTableWithVerifyConfig, SqlConnection cnArchConfig, SqlConnection cnSource, SqlConnection cnDestination, bool statsOnly)
        {
            tailTableWithVerifyConfig.LastRunStartDate = DateTime.Now.ToLocalTime();
            tailTableWithVerifyConfig.LastRunEndDate = tailTableWithVerifyConfig.LastRunStartDate;
            var processName = $@"Archive Table: {tailTableWithVerifyConfig.ServerQualifiedSourceName}";
            Logger.Debug($"{processName} BEGIN: {DateTime.Now.ToLocalTime()}");
            tailTableWithVerifyConfig.BatchID = batchID;
            tailTableWithVerifyConfig.ProcessID = LogProcessInsert(cnArchConfig, processName, batchID, tailTableWithVerifyConfig);
            tailTableWithVerifyConfig.AutoConfigDisable = Convert.ToBoolean(ConfigurationManager.AppSettings["AutoConfigDisable"]);
            tailTableWithVerifyConfig.AutoConfigDeleteRowsAdjust = Convert.ToBoolean(ConfigurationManager.AppSettings["AutoConfigDeleteRowsAdjust"]);
            tailTableWithVerifyConfig.TestModeConfig = Convert.ToBoolean(ConfigurationManager.AppSettings["TestModeConfig"]);
            tailTableWithVerifyConfig.TestModeNoDelete = Convert.ToBoolean(ConfigurationManager.AppSettings["TestModeNoDelete"]);
            tailTableWithVerifyConfig.StatsOnly = statsOnly;
            tailTableWithVerifyConfig.MaxStatsRows = Convert.ToInt64(ConfigurationManager.AppSettings["MaxStatsRows"]);
            if (tailTableWithVerifyConfig.TestModeNoDelete) processName = $@"{processName} - Test Mode";
            var nextRuntimeMoveAheadDays = Convert.ToInt32(ConfigurationManager.AppSettings["ArchieNextRuntimeMoveAheadDays"]);
            var maxDeleteSeconds = Convert.ToDecimal(ConfigurationManager.AppSettings["ArchieMaxDeleteSeconds"]);
            long maxDeleteRows = Convert.ToInt64(ConfigurationManager.AppSettings["ArchieMaxDeleteRows"]);
            //long maxStatsRows = Convert.ToInt64(ConfigurationManager.AppSettings["ArchieMaxStatsRows"]);
            LogProcessUpdate(cnArchConfig, tailTableWithVerifyConfig);
            var response = new ProcessResponse($@"BatchID [{batchID}] {batchName} ProcessID [{tailTableWithVerifyConfig.ProcessID}] {processName}", tailTableWithVerifyConfig, true);
            response.BatchID = batchID;
            response.ProcessID = tailTableWithVerifyConfig.ProcessID;
            response.ArchiveDate = tailTableWithVerifyConfig.LastRunStartDate;
            response.ConnectionStrings.Add(new ConnectionString(cnArchConfig, "cnArchConfig", cnArchConfig.ConnectionString, tailTableWithVerifyConfig.BatchID, tailTableWithVerifyConfig.ProcessID));
            response.ConnectionStrings.Add(new ConnectionString("smtp", ConfigurationManager.AppSettings["SmtpHost"]));
            try
            {
                var stepValidateTable = new ProcessStep(cnArchConfig, tailTableWithVerifyConfig.ProcessID, $@"Validate configuration in [Archiver].[dbo].[{_ConfigTableName}] for {tailTableWithVerifyConfig.ServerQualifiedSourceName}");
                try
                {
                    tailTableWithVerifyConfig.ValidationErrors = ValidateConfig(stepValidateTable.StepID, tailTableWithVerifyConfig, cnArchConfig, cnSource, cnDestination);
                    stepValidateTable.RowsAffected = 1;
                }
                catch (Exception e)
                {
                    stepValidateTable.Errors.Add(new Error(e));
                }
                stepValidateTable.Done(cnArchConfig);
                response.ProcessSteps.Add(stepValidateTable);

                if (tailTableWithVerifyConfig.ValidationErrors.Any())
                {
                    tailTableWithVerifyConfig.LastRunStatus = "Error";
                    LogProcessUpdate(cnArchConfig, tailTableWithVerifyConfig);
                    foreach (var validationError in tailTableWithVerifyConfig.ValidationErrors)
                    {
                        if (!response.Errors.Contains(new Error(validationError.Message))) response.Errors.Add(new Error(validationError.Message));
                    }
                    if (tailTableWithVerifyConfig.AutoConfigDisable && tailTableWithVerifyConfig.StatsOnly == false)
                    {
                        var stepUpdatetailTableWithVerifyConfigDisable = new ProcessStep(cnArchConfig, response.ProcessID, $@"Set [Archiver].[dbo].[{_ConfigTableName}].[IsEnabled] = FALSE for {tailTableWithVerifyConfig.ServerQualifiedSourceName}");
                        try
                        {
                            stepUpdatetailTableWithVerifyConfigDisable.RowsAffected = UpdateTailTableWithVerifyConfigIsEnabled(cnArchConfig, tailTableWithVerifyConfig, false);
                        }
                        catch (Exception e)
                        {
                            stepUpdatetailTableWithVerifyConfigDisable.Errors.Add(new Error(e));
                        }
                        stepUpdatetailTableWithVerifyConfigDisable.Done(cnArchConfig);
                        response.ProcessSteps.Add(stepUpdatetailTableWithVerifyConfigDisable);
                    }
                }

                if (!response.Errors.Any())
                {
                    var stepGetSourceSqlConnection = new ProcessStep(cnArchConfig, tailTableWithVerifyConfig.ProcessID, $@"Get SQL connection for Source table {tailTableWithVerifyConfig.ServerQualifiedSourceName}");
                    try
                    {
                        // Get Source SQL connection
                        cnSource = GetSourceSqlConnection(tailTableWithVerifyConfig.SourceServerName, tailTableWithVerifyConfig.SourceDatabaseName);
                        response.ConnectionStrings.Add(new ConnectionString(cnArchConfig, "cnSource", cnSource.ConnectionString, tailTableWithVerifyConfig.BatchID, tailTableWithVerifyConfig.ProcessID));
                        if (cnSource.State == ConnectionState.Open)
                        {
                            stepGetSourceSqlConnection.RowsAffected = 1;
                        }
                        else
                        {
                            var validationError = new ValidationError($@"ERROR: Source server [{tailTableWithVerifyConfig.SourceServerName}].[{tailTableWithVerifyConfig.SourceDatabaseName}] ConnectionState is not Open.", "Validation", "Fatal", true);
                            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepGetSourceSqlConnection.StepID, validationError.Message, "");
                            tailTableWithVerifyConfig.ValidationErrors.Add(validationError);
                            tailTableWithVerifyConfig.ValidationStatus = validationError.Message;
                            tailTableWithVerifyConfig.LastRunStatus = "Error";
                            stepGetSourceSqlConnection.Errors.Add(new Error(validationError.Message));
                            if (!response.Errors.Contains(new Error(validationError.Message))) response.Errors.Add(new Error(validationError.Message));
                            Logger.Error(validationError.Message);
                        }
                    }
                    catch (Exception e)
                    {
                        stepGetSourceSqlConnection.Errors.Add(new Error(e));
                    }
                    stepGetSourceSqlConnection.Done(cnArchConfig);
                    response.ProcessSteps.Add(stepGetSourceSqlConnection);

                    var stepGetDestinationSqlConnection = new ProcessStep(cnArchConfig, tailTableWithVerifyConfig.ProcessID, $@"Get SQL connection for Destination table {tailTableWithVerifyConfig.ServerQualifiedDestinationName}");
                    try
                    {
                        // Get Destination Table connection
                        cnDestination = GetDestinationSqlConnection(tailTableWithVerifyConfig.DestinationServerName, tailTableWithVerifyConfig.DestinationDatabaseName);
                        response.ConnectionStrings.Add(new ConnectionString(cnArchConfig, "cnDestination", cnDestination.ConnectionString, tailTableWithVerifyConfig.BatchID, tailTableWithVerifyConfig.ProcessID));
                        if (cnDestination.State == ConnectionState.Open)
                        { 
                            stepGetDestinationSqlConnection.RowsAffected = 1;
                        }
                        else 
                        {
                            var validationError = new ValidationError($@"ERROR: Destination server [{tailTableWithVerifyConfig.DestinationServerName}].[{tailTableWithVerifyConfig.DestinationDatabaseName}] ConnectionState is not Open.", "Validation", "Fatal", true);
                            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepGetDestinationSqlConnection.StepID, validationError.Message, "");
                            tailTableWithVerifyConfig.ValidationErrors.Add(validationError);
                            tailTableWithVerifyConfig.ValidationStatus = validationError.Message;
                            tailTableWithVerifyConfig.LastRunStatus = "Error";
                            stepGetDestinationSqlConnection.Errors.Add(new Error(validationError.Message));
                            if (!response.Errors.Contains(new Error(validationError.Message))) response.Errors.Add(new Error(validationError.Message));
                            Logger.Error(validationError.Message);
                        }
                    }
                    catch (Exception e)
                    {
                        stepGetDestinationSqlConnection.Errors.Add(new Error(e));
                    }
                    stepGetDestinationSqlConnection.Done(cnArchConfig);
                    response.ProcessSteps.Add(stepGetDestinationSqlConnection);
                }

                if (!response.Errors.Any())
                {
                    var stepCheckSourceDatabaseExists = new ProcessStep(cnArchConfig, tailTableWithVerifyConfig.ProcessID, $@"Check that Source database [{tailTableWithVerifyConfig.SourceServerName}].[{tailTableWithVerifyConfig.SourceDatabaseName}] exists");
                    try
                    {
                        // See if Source database exists
                        if (CheckSourceDatabaseExists(cnSource, tailTableWithVerifyConfig))
                        {
                            stepCheckSourceDatabaseExists.RowsAffected = 1;
                        }
                        else
                        {
                            var validationError = new ValidationError($@"ERROR: {cnSource.ConnectionString} Source database [{tailTableWithVerifyConfig.SourceServerName}].[{tailTableWithVerifyConfig.SourceDatabaseName}] not found.", "Validation", "Fatal", true);
                            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepCheckSourceDatabaseExists.StepID, validationError.Message, "");
                            tailTableWithVerifyConfig.ValidationErrors.Add(validationError);
                            tailTableWithVerifyConfig.ValidationStatus = validationError.Message;
                            tailTableWithVerifyConfig.LastRunStatus = "Error";
                            stepCheckSourceDatabaseExists.Errors.Add(new Error(validationError.Message));
                            if (!response.Errors.Contains(new Error(validationError.Message))) response.Errors.Add(new Error(validationError.Message));
                            Logger.Error(validationError.Message);
                        }
                    }
                    catch (Exception e)
                    {
                        stepCheckSourceDatabaseExists.Errors.Add(new Error(e));
                    }
                    stepCheckSourceDatabaseExists.Done(cnArchConfig);
                    response.ProcessSteps.Add(stepCheckSourceDatabaseExists);

                    var stepCheckSourceSchemaExists = new ProcessStep(cnArchConfig, tailTableWithVerifyConfig.ProcessID, $@"Check that Source schema [{tailTableWithVerifyConfig.SourceServerName}].[{tailTableWithVerifyConfig.SourceDatabaseName}].[{tailTableWithVerifyConfig.SourceSchemaName}] exists");
                    try
                    {
                        // See if Source schema exists
                        if (CheckSourceSchemaExists(cnSource, tailTableWithVerifyConfig))
                        {   
                            stepCheckSourceSchemaExists.RowsAffected = 1;
                        }
                        else
                        {
                            var validationError = new ValidationError($@"ERROR: {cnSource.ConnectionString} Source schema [{tailTableWithVerifyConfig.SourceServerName}].[{tailTableWithVerifyConfig.SourceDatabaseName}].[{tailTableWithVerifyConfig.SourceSchemaName}] not found.", "Validation", "Fatal", true);
                            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepCheckSourceSchemaExists.StepID, validationError.Message, "");
                            tailTableWithVerifyConfig.ValidationErrors.Add(validationError);
                            tailTableWithVerifyConfig.ValidationStatus = validationError.Message;
                            tailTableWithVerifyConfig.LastRunStatus = "Error";
                            stepCheckSourceSchemaExists.Errors.Add(new Error(validationError.Message));
                            if (!response.Errors.Contains(new Error(validationError.Message))) response.Errors.Add(new Error(validationError.Message));
                            Logger.Error(validationError.Message);
                        }
                    }
                    catch (Exception e)
                    {
                        stepCheckSourceSchemaExists.Errors.Add(new Error(e));
                    }
                    stepCheckSourceSchemaExists.Done(cnArchConfig);
                    response.ProcessSteps.Add(stepCheckSourceSchemaExists);

                    var stepCheckSourceTableExists = new ProcessStep(cnArchConfig, tailTableWithVerifyConfig.ProcessID, $@"Check that Source Table [{tailTableWithVerifyConfig.SourceServerName}].[{tailTableWithVerifyConfig.SourceDatabaseName}].[{tailTableWithVerifyConfig.SourceSchemaName}].[{tailTableWithVerifyConfig.SourceTableName}] exists");
                    try
                    {
                        // See if Source table exists
                        if (CheckSourceTableExists(cnSource, tailTableWithVerifyConfig))
                        {
                            stepCheckSourceTableExists.RowsAffected = 1;
                        }
                        else
                        {
                            var validationError = new ValidationError($@"ERROR: {cnSource.ConnectionString} Source table [{tailTableWithVerifyConfig.SourceServerName}].[{tailTableWithVerifyConfig.SourceDatabaseName}].[{tailTableWithVerifyConfig.SourceSchemaName}].[{tailTableWithVerifyConfig.SourceTableName}] not found.", "Validation", "Fatal", true);
                            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepCheckSourceTableExists.StepID, validationError.Message, "");
                            tailTableWithVerifyConfig.ValidationErrors.Add(validationError);
                            tailTableWithVerifyConfig.ValidationStatus = validationError.Message;
                            tailTableWithVerifyConfig.LastRunStatus = "Error";
                            stepCheckSourceTableExists.Errors.Add(new Error(validationError.Message));
                            if (!response.Errors.Contains(new Error(validationError.Message))) response.Errors.Add(new Error(validationError.Message));
                            Logger.Error(validationError.Message);
                        }
                    }
                    catch (Exception e)
                    {
                        stepCheckSourceTableExists.Errors.Add(new Error(e));
                    }
                    stepCheckSourceTableExists.Done(cnArchConfig);
                    response.ProcessSteps.Add(stepCheckSourceTableExists);

                    var stepCheckIsSourceKeyColumnUnique = new ProcessStep(cnArchConfig, tailTableWithVerifyConfig.ProcessID, $@"Check if Source key is unique for {tailTableWithVerifyConfig.ServerQualifiedSourceName}");
                    try
                    {
                        // Build SourceKeyColumns collection 
                        var keyColumnList = tailTableWithVerifyConfig.SourceKey.Split(',').ToList();
                        foreach (var keyColumn in keyColumnList)
                        {
                            tailTableWithVerifyConfig.SourceKeyColumns.Add(GetKeyData(cnDestination, tailTableWithVerifyConfig.SourceSchemaName, tailTableWithVerifyConfig.SourceTableName, keyColumn));
                        }
                        stepCheckIsSourceKeyColumnUnique.RowsAffected = 1;
                        // See if Source table key is unique
                        tailTableWithVerifyConfig.IsSourceKeyUnique = IsSourceKeyUnique(cnDestination, tailTableWithVerifyConfig);
                        UpdateTailTableWithVerifyConfigIsKeyUnique(cnArchConfig, tailTableWithVerifyConfig);
                        stepCheckIsSourceKeyColumnUnique.RowsAffected = 1;
                        if (!tailTableWithVerifyConfig.IsSourceKeyUnique)
                        {
                            var validationError = new ValidationError($@"ERROR: {cnDestination.ConnectionString} Source table {tailTableWithVerifyConfig.ServerQualifiedSourceName} Key [{tailTableWithVerifyConfig.SourceKey}] is not unique.", "Validation", "Fatal", true);
                            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepCheckIsSourceKeyColumnUnique.StepID, validationError.Message, "");
                            tailTableWithVerifyConfig.ValidationErrors.Add(validationError);
                            tailTableWithVerifyConfig.ValidationStatus = validationError.Message;
                            tailTableWithVerifyConfig.LastRunStatus = "Error";
                            stepCheckIsSourceKeyColumnUnique.Errors.Add(new Error(validationError.Message));
                            if (!response.Errors.Contains(new Error(validationError.Message))) response.Errors.Add(new Error(validationError.Message));
                            Logger.Error(validationError.Message);
                        }
                    }
                    catch (Exception e)
                    {
                        stepCheckIsSourceKeyColumnUnique.Errors.Add(new Error(e));
                    }
                    stepCheckIsSourceKeyColumnUnique.Done(cnArchConfig);
                    response.ProcessSteps.Add(stepCheckIsSourceKeyColumnUnique);
                }

                if (!response.Errors.Any())
                {
                    var stepCheckDestinationDatabaseExists = new ProcessStep(cnArchConfig, tailTableWithVerifyConfig.ProcessID, $@"Check that Destination database [{tailTableWithVerifyConfig.DestinationServerName}].[{tailTableWithVerifyConfig.DestinationDatabaseName}] exists");
                    try
                    {
                        // See if Destination database exists
                        if (CheckDestinationDatabaseExists(cnDestination, tailTableWithVerifyConfig))
                        {
                            stepCheckDestinationDatabaseExists.RowsAffected = 1;
                        }
                        else
                        {
                            var validationError = new ValidationError($@"ERROR: Destination database [{tailTableWithVerifyConfig.DestinationServerName}].[{tailTableWithVerifyConfig.DestinationDatabaseName}] not found.", "Validation", "Fatal", true);
                            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepCheckDestinationDatabaseExists.StepID, validationError.Message, "");
                            tailTableWithVerifyConfig.ValidationErrors.Add(validationError);
                            tailTableWithVerifyConfig.ValidationStatus = validationError.Message;
                            tailTableWithVerifyConfig.LastRunStatus = "Error";
                            stepCheckDestinationDatabaseExists.Errors.Add(new Error(validationError.Message));
                            if (!response.Errors.Contains(new Error(validationError.Message))) response.Errors.Add(new Error(validationError.Message));
                            Logger.Error(validationError.Message);
                        }
                    }
                    catch (Exception e)
                    {
                        stepCheckDestinationDatabaseExists.Errors.Add(new Error(e));
                    }
                    stepCheckDestinationDatabaseExists.Done(cnArchConfig);
                    response.ProcessSteps.Add(stepCheckDestinationDatabaseExists);

                    var stepCheckDestinationSchemaExists = new ProcessStep(cnArchConfig, tailTableWithVerifyConfig.ProcessID, $@"Check that Destination schema [{tailTableWithVerifyConfig.DestinationServerName}].[{tailTableWithVerifyConfig.DestinationDatabaseName}].[{tailTableWithVerifyConfig.DestinationSchemaName}] exists");
                    try
                    {
                        // See if Destination schema exists
                        if (CheckDestinationSchemaExists(cnDestination, tailTableWithVerifyConfig))
                        {
                           stepCheckDestinationSchemaExists.RowsAffected = 1;
                        }
                        else
                        {
                            var validationError = new ValidationError($@"ERROR: Destination schema [{tailTableWithVerifyConfig.DestinationServerName}].[{tailTableWithVerifyConfig.DestinationDatabaseName}].[{tailTableWithVerifyConfig.DestinationSchemaName}] not found.", "Validation", "Fatal", true);
                            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepCheckDestinationSchemaExists.StepID, validationError.Message, "");
                            tailTableWithVerifyConfig.ValidationErrors.Add(validationError);
                            tailTableWithVerifyConfig.ValidationStatus = validationError.Message;
                            tailTableWithVerifyConfig.LastRunStatus = "Error";
                            stepCheckDestinationSchemaExists.Errors.Add(new Error(validationError.Message));
                            if (!response.Errors.Contains(new Error(validationError.Message))) response.Errors.Add(new Error(validationError.Message));
                            Logger.Error(validationError.Message);
                        }
                    }
                    catch (Exception e)
                    {
                        stepCheckDestinationSchemaExists.Errors.Add(new Error(e));
                    }
                    stepCheckDestinationSchemaExists.Done(cnArchConfig);
                    response.ProcessSteps.Add(stepCheckDestinationSchemaExists);

                    var stepCheckDestinationTableExists = new ProcessStep(cnArchConfig, tailTableWithVerifyConfig.ProcessID, $@"Check that Destination Table [{tailTableWithVerifyConfig.DestinationServerName}].[{tailTableWithVerifyConfig.DestinationDatabaseName}].[{tailTableWithVerifyConfig.DestinationSchemaName}].[{tailTableWithVerifyConfig.DestinationTableName}] exists");
                    try
                    {
                        // See if Destination table exists
                        if (CheckDestinationTableExists(cnDestination, tailTableWithVerifyConfig))
                        {
                            stepCheckDestinationTableExists.RowsAffected = 1;
                        }
                        else
                        {
                            var validationError = new ValidationError($@"ERROR: Destination table [{tailTableWithVerifyConfig.DestinationServerName}].[{tailTableWithVerifyConfig.DestinationDatabaseName}].[{tailTableWithVerifyConfig.DestinationSchemaName}].[{tailTableWithVerifyConfig.DestinationTableName}] not found.", "Validation", "Fatal", true);
                            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepCheckDestinationTableExists.StepID, validationError.Message, "");
                            tailTableWithVerifyConfig.ValidationErrors.Add(validationError);
                            tailTableWithVerifyConfig.ValidationStatus = validationError.Message;
                            tailTableWithVerifyConfig.LastRunStatus = "Error";
                            stepCheckDestinationTableExists.Errors.Add(new Error(validationError.Message));
                            if (!response.Errors.Contains(new Error(validationError.Message))) response.Errors.Add(new Error(validationError.Message));
                            Logger.Error(validationError.Message);
                        }
                    }
                    catch (Exception e)
                    {
                        stepCheckDestinationTableExists.Errors.Add(new Error(e));
                    }
                    stepCheckDestinationTableExists.Done(cnArchConfig);
                    response.ProcessSteps.Add(stepCheckDestinationTableExists);
                }

                if (!response.Errors.Any())
                {
                    var stepCheckDestinationTableStructure = new ProcessStep(cnArchConfig, tailTableWithVerifyConfig.ProcessID, $@"Check Destination Table Structure for {tailTableWithVerifyConfig.DestinationTableName}");
                    try
                    {
                        // See if Destination table structure matches Source table
                        if (!CheckDestinationTableStructureMissingColumns(cnDestination, tailTableWithVerifyConfig))
                        {
                            stepCheckDestinationTableStructure.RowsAffected = 1;
                        }
                        else 
                        {
                            var validationError = new ValidationError($@"ERROR: Destination table [{tailTableWithVerifyConfig.DestinationServerName}].[{tailTableWithVerifyConfig.DestinationDatabaseName}].[{tailTableWithVerifyConfig.DestinationSchemaName}].[{tailTableWithVerifyConfig.DestinationTableName}] structure does not match Source.", "Validation", "Fatal", true);
                            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepCheckDestinationTableStructure.StepID, validationError.Message, "");
                            tailTableWithVerifyConfig.ValidationErrors.Add(validationError);
                            tailTableWithVerifyConfig.ValidationStatus = validationError.Message;
                            tailTableWithVerifyConfig.LastRunStatus = "Error";
                            stepCheckDestinationTableStructure.Errors.Add(new Error(validationError.Message));
                            if (!response.Errors.Contains(new Error(validationError.Message))) response.Errors.Add(new Error(validationError.Message));
                            Logger.Error(validationError.Message);
                        }
                        if (!CheckDestinationTableStructureDifferentColumns(cnDestination, tailTableWithVerifyConfig))
                        { 
                            stepCheckDestinationTableStructure.RowsAffected = 1;
                        }
                        else
                        {
                            var validationError = new ValidationError($@"ERROR: Destination table [{tailTableWithVerifyConfig.DestinationServerName}].[{tailTableWithVerifyConfig.DestinationDatabaseName}].[{tailTableWithVerifyConfig.DestinationSchemaName}].[{tailTableWithVerifyConfig.DestinationTableName}] structure does not match Source.", "Validation", "Fatal", true);
                            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepCheckDestinationTableStructure.StepID, validationError.Message, "");
                            tailTableWithVerifyConfig.ValidationErrors.Add(validationError);
                            tailTableWithVerifyConfig.ValidationStatus = validationError.Message;
                            tailTableWithVerifyConfig.LastRunStatus = "Error";
                            stepCheckDestinationTableStructure.Errors.Add(new Error(validationError.Message));
                            if (!response.Errors.Contains(new Error(validationError.Message))) response.Errors.Add(new Error(validationError.Message));
                            Logger.Error(validationError.Message);
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
                    var stepCheckDestinationTableUniqueIndex = new ProcessStep(cnArchConfig, tailTableWithVerifyConfig.ProcessID, $@"Check if Destination table has a unique index on {tailTableWithVerifyConfig.ServerQualifiedDestinationName}");
                    try
                    {
                        // Build DestinationKeyColumns collection
                        var keyColumnList = tailTableWithVerifyConfig.DestinationKey.Split(',').ToList();
                        foreach (var keyColumn in keyColumnList)
                        {
                            tailTableWithVerifyConfig.DestinationKeyColumns.Add(GetKeyData(cnDestination, tailTableWithVerifyConfig.DestinationSchemaName, tailTableWithVerifyConfig.DestinationTableName, keyColumn));
                        }
                        // See if Destination table has a unique index on DestinationKeyColumns (business key) 
                        if (CheckDestinationTableUniqueIndex(cnDestination, tailTableWithVerifyConfig))
                        {   
                            stepCheckDestinationTableUniqueIndex.RowsAffected = 1;
                        }
                        else
                        {
                            var validationError = new ValidationError($@"ERROR: {cnDestination.ConnectionString} Destination table {tailTableWithVerifyConfig.ServerQualifiedDestinationName} unique index does not match [Archiver].[dbo].[{_ConfigTableName}] .", "Validation", "Fatal", true);
                            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepCheckDestinationTableUniqueIndex.StepID, validationError.Message, "");
                            tailTableWithVerifyConfig.ValidationErrors.Add(validationError);
                            tailTableWithVerifyConfig.ValidationStatus = validationError.Message;
                            tailTableWithVerifyConfig.LastRunStatus = "Error";
                            if (!response.Errors.Contains(new Error(validationError.Message))) response.Errors.Add(new Error(validationError.Message));
                            Logger.Error(validationError.Message);
                        }
                    }
                    catch (Exception e)
                    {
                        stepCheckDestinationTableUniqueIndex.Errors.Add(new Error(e));
                    }
                    stepCheckDestinationTableUniqueIndex.Done(cnArchConfig);
                    response.ProcessSteps.Add(stepCheckDestinationTableUniqueIndex);
                }

                if (!response.Errors.Any())
                {
                    var stepCheckIsDestinationKeyColumnUnique = new ProcessStep(cnArchConfig, tailTableWithVerifyConfig.ProcessID, $@"Check if Destination key is unique for {tailTableWithVerifyConfig.ServerQualifiedSourceName}");
                    try
                    {
                        tailTableWithVerifyConfig.IsDestinationKeyUnique = IsDestinationKeyUnique(cnDestination, tailTableWithVerifyConfig);
                        UpdateTailTableWithVerifyConfigIsKeyUnique(cnArchConfig, tailTableWithVerifyConfig);
                        stepCheckIsDestinationKeyColumnUnique.RowsAffected = 1;
                    }
                    catch (Exception e)
                    {
                        stepCheckIsDestinationKeyColumnUnique.Errors.Add(new Error(e));
                    }
                    stepCheckIsDestinationKeyColumnUnique.Done(cnArchConfig);
                    response.ProcessSteps.Add(stepCheckIsDestinationKeyColumnUnique);
                    if (tailTableWithVerifyConfig.IsDestinationKeyUnique)
                    {   
                        stepCheckIsDestinationKeyColumnUnique.RowsAffected = 1;
                    }
                    else
                    {
                        var validationError = new ValidationError($@"ERROR: {cnDestination.ConnectionString} Destination table {tailTableWithVerifyConfig.ServerQualifiedDestinationName} Destination Key [{tailTableWithVerifyConfig.DestinationKey}] is not unique.", "Validation", "Fatal", true);
                        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepCheckIsDestinationKeyColumnUnique.StepID, validationError.Message, "");
                        tailTableWithVerifyConfig.ValidationErrors.Add(validationError);
                        tailTableWithVerifyConfig.ValidationStatus = validationError.Message;
                        Logger.Error(validationError.Message);
                        if (!response.Errors.Contains(new Error(validationError.Message))) response.Errors.Add(new Error(validationError.Message));
                    }
                }

                if (!response.Errors.Any())
                {
                    var stepUpdatetailTableWithVerifyConfigIsRunningTrue = new ProcessStep(cnArchConfig, tailTableWithVerifyConfig.ProcessID, $@"Set [Archiver].[dbo].[{_ConfigTableName}].[IsRunning] = TRUE for {tailTableWithVerifyConfig.ServerQualifiedSourceName}");
                    try
                    {
                        tailTableWithVerifyConfig.IsRunning = true;
                        UpdateTailTableWithVerifyConfigIsRunning(cnArchConfig, tailTableWithVerifyConfig);
                        LogProcessUpdate(cnArchConfig, tailTableWithVerifyConfig);
                        stepUpdatetailTableWithVerifyConfigIsRunningTrue.RowsAffected = 1;
                    }
                    catch (Exception e)
                    {
                        stepUpdatetailTableWithVerifyConfigIsRunningTrue.Errors.Add(new Error(e));
                    }
                    stepUpdatetailTableWithVerifyConfigIsRunningTrue.Done(cnArchConfig);
                    response.ProcessSteps.Add(stepUpdatetailTableWithVerifyConfigIsRunningTrue);
                }

                if (!response.Errors.Any())
                {
                    var stepGatherInitialStatistics = new ProcessStep(cnArchConfig, tailTableWithVerifyConfig.ProcessID, $@"Gather initial statistics for {tailTableWithVerifyConfig.ServerQualifiedSourceName}");
                    try
                    {
                        tailTableWithVerifyConfig.SourceInitialRowCount = GetRowCountSource(cnDestination, tailTableWithVerifyConfig);
                        tailTableWithVerifyConfig.DestinationInitialRowCount = GetRowCountDestination(cnDestination, tailTableWithVerifyConfig);
                        tailTableWithVerifyConfig.LastRunStatus = "";
                        UpdateTailTableWithVerifyConfigInitialRowCount(cnArchConfig, tailTableWithVerifyConfig);
                        stepGatherInitialStatistics.RowsAffected = 1;
                    }
                    catch (Exception e)
                    {
                        stepGatherInitialStatistics.Errors.Add(new Error(e));
                    }
                    stepGatherInitialStatistics.Done(cnArchConfig);
                    response.ProcessSteps.Add(stepGatherInitialStatistics);
                }

                //--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

                if (!response.Errors.Any() && tailTableWithVerifyConfig.StatsOnly == false)
                {
                    var stepDropTempTableDest = new ProcessStep(cnArchConfig, tailTableWithVerifyConfig.ProcessID, $@"Drop destination temp table #TempKeyList");
                    try
                    {
                        var cmdDropTemp2 = new SqlCommand
                        {
                            Connection = cnDestination,
                            CommandType = CommandType.Text,
                            CommandText = $@"
                                                        DROP TABLE IF EXISTS #TempKeyList"
                        };
                        Logger.Debug(Logging.DbCmdString(cmdDropTemp2));
                        stepDropTempTableDest.Statement = cmdDropTemp2.CommandText;
                        stepDropTempTableDest.RowsAffected = cmdDropTemp2.ExecuteNonQuery();
                    }
                    catch (Exception e)
                    {
                        stepDropTempTableDest.Errors.Add(new Error(e));
                    }
                    stepDropTempTableDest.Done(cnArchConfig);
                    response.ProcessSteps.Add(stepDropTempTableDest);

                    if (!response.Errors.Any())
                    {
                        // Load the Key Values into the temp table.
                        var stepCreateDestinationTempTable = new ProcessStep(cnArchConfig, tailTableWithVerifyConfig.ProcessID, $@"Create destination temp table #TempKeyList with oldest {tailTableWithVerifyConfig.MaxDeleteRowsPerBatch} rows from {tailTableWithVerifyConfig.ServerQualifiedDestinationName}");
                        string cmdStr;
                        StringBuilder sb = new StringBuilder();
                        sb.Append($@"
                                                    SELECT TOP ({tailTableWithVerifyConfig.MaxDeleteRowsPerBatch}) {tailTableWithVerifyConfig.SourceKey}
                                                    INTO #TempKeyList
                                                    FROM [{tailTableWithVerifyConfig.SourceDatabaseName}].[{tailTableWithVerifyConfig.SourceSchemaName}].[{tailTableWithVerifyConfig.SourceTableName}] WITH (NOLOCK)
                                                    {tailTableWithVerifyConfig.WhereClause} 
                                                    ORDER BY {tailTableWithVerifyConfig.DateColumn}, {tailTableWithVerifyConfig.SourceKey}");
                        cmdStr = sb.ToString();
                        try
                        {
                            var cmdTemp = new SqlCommand
                            {
                                Connection = cnDestination,
                                CommandType = CommandType.Text,
                                CommandText = cmdStr,
                                CommandTimeout = 0
                            };
                            Logger.Info(Logging.DbCmdString(cmdTemp));
                            stepCreateDestinationTempTable.Statement = cmdTemp.CommandText;
                            tailTableWithVerifyConfig.SelectedRows = cmdTemp.ExecuteNonQuery();
                            Logger.Info($@"ROWS: {tailTableWithVerifyConfig.SelectedRows}");
                            stepCreateDestinationTempTable.RowsAffected = tailTableWithVerifyConfig.SelectedRows;
                        }
                        catch (Exception e)
                        {
                            stepCreateDestinationTempTable.Errors.Add(new Error(e));
                        }
                        stepCreateDestinationTempTable.Done(cnArchConfig);
                        response.ProcessSteps.Add(stepCreateDestinationTempTable);
                    }

                    if (!response.Errors.Any())
                    {
                        var stepVerifyDestTempTable = new ProcessStep(cnArchConfig, tailTableWithVerifyConfig.ProcessID, $@"Verify {tailTableWithVerifyConfig.SelectedRows} rows in #TempKeyList");
                        try
                        {
                            // Verify that #Temp table has expected row count
                            var cmdVerifyTemp = new SqlCommand
                            {
                                Connection = cnDestination,
                                CommandType = CommandType.Text,
                                CommandText = $@"
                                                                SELECT COUNT(*) FROM #TempKeyList
                                "
                            };
                            Logger.Debug(Logging.DbCmdString(cmdVerifyTemp));
                            stepVerifyDestTempTable.Statement = cmdVerifyTemp.CommandText;
                            tailTableWithVerifyConfig.TempRowsDest = Convert.ToInt32(cmdVerifyTemp.ExecuteScalar());
                            Logger.Debug($@"ROWS: {tailTableWithVerifyConfig.TempRowsDest}");
                            stepVerifyDestTempTable.RowsAffected = tailTableWithVerifyConfig.TempRowsDest;
                        }
                        catch (Exception e)
                        {
                            stepVerifyDestTempTable.Errors.Add(new Error(e));
                        }
                        stepVerifyDestTempTable.Done(cnArchConfig);
                        response.ProcessSteps.Add(stepVerifyDestTempTable);
                    }

                    if (!response.Errors.Any())
                    {
                        var stepCheckForMissingKeys = new ProcessStep(cnArchConfig, tailTableWithVerifyConfig.ProcessID, $@"Check for missing keys in [{cnDestination.DataSource}].[{tailTableWithVerifyConfig.DestinationDatabaseName}].[{tailTableWithVerifyConfig.DestinationSchemaName}].[{tailTableWithVerifyConfig.DestinationTableName}]");
                        string cmdStr;
                        StringBuilder sb = new StringBuilder();
                        sb.Append($@"
                                                        SELECT COUNT(*) FROM #TempKeyList k 
                                                        WHERE NOT EXISTS 
                                                            (SELECT 1 FROM [{tailTableWithVerifyConfig.DestinationDatabaseName}].[{tailTableWithVerifyConfig.DestinationSchemaName}].[{tailTableWithVerifyConfig.DestinationTableName}] a
                                                                WHERE ");
                        // Build SourceKeyColumns collection
                        var keyColumnList = tailTableWithVerifyConfig.SourceKey.Split(',').ToList();
                        var i = 0;
                        foreach (var keyColumn in keyColumnList)
                        {
                            i++;
                            sb.Append($@"
                                                                a.[{keyColumn}] = k.[{keyColumn}] ");
                            if (i < keyColumnList.Count) sb.Append(" AND ");
                        }
                        sb.Append($@"
                                                            )");
                        var cmdVerify = new SqlCommand
                        {
                            Connection = cnDestination,
                            CommandType = CommandType.Text,
                            CommandTimeout = 0,
                            CommandText = sb.ToString()
                        };
                        Logger.Debug(Logging.DbCmdString(cmdVerify));
                        stepCheckForMissingKeys.Statement = cmdVerify.CommandText;
                        tailTableWithVerifyConfig.MissingKeys = Convert.ToInt32(cmdVerify.ExecuteScalar());
                        stepCheckForMissingKeys.RowsAffected = tailTableWithVerifyConfig.MissingKeys;
                        stepCheckForMissingKeys.Done(cnArchConfig);
                        Logger.Debug($@"Missing Keys: {tailTableWithVerifyConfig.MissingKeys}");

                        if (tailTableWithVerifyConfig.MissingKeys != 0)
                        {
                            var e = "Missing Keys";
                            stepCheckForMissingKeys.Errors.Add(new Error(e));
                            Logger.Error($"ERROR: {e}");
                        }
                        response.ProcessSteps.Add(stepCheckForMissingKeys);

                        if (!response.Errors.Any() && tailTableWithVerifyConfig.MissingKeys == 0)
                        {
                            var stepDropTempTableSource = new ProcessStep(cnArchConfig, tailTableWithVerifyConfig.ProcessID, $@"Drop source temp table #TempKeyList");
                            try
                            {
                                var cmdDropTemp2 = new SqlCommand
                                {
                                    Connection = cnSource,
                                    CommandType = CommandType.Text,
                                    CommandText = $@"
                                                        DROP TABLE IF EXISTS #TempKeyList"
                                };
                                Logger.Debug(Logging.DbCmdString(cmdDropTemp2));
                                stepDropTempTableSource.Statement = cmdDropTemp2.CommandText;
                                stepDropTempTableSource.RowsAffected = cmdDropTemp2.ExecuteNonQuery();
                            }
                            catch (Exception e)
                            {
                                stepDropTempTableSource.Errors.Add(new Error(e));
                            }
                            stepDropTempTableSource.Done(cnArchConfig);
                            response.ProcessSteps.Add(stepDropTempTableSource);

                            if (!response.Errors.Any())
                            {
                                // Load the Key Values into the temp table.
                                var stepCreateSourceTempTable = new ProcessStep(cnArchConfig, tailTableWithVerifyConfig.ProcessID, $@"Create source temp table #TempKeyList with oldest {tailTableWithVerifyConfig.MaxDeleteRowsPerBatch} rows from {tailTableWithVerifyConfig.ServerQualifiedSourceName}");
                                //string cmdStr;
                                sb = new StringBuilder();
                                sb.Append($@"
                                                    SELECT TOP ({tailTableWithVerifyConfig.MaxDeleteRowsPerBatch}) {tailTableWithVerifyConfig.SourceKey}
                                                    INTO #TempKeyList
                                                    FROM [{tailTableWithVerifyConfig.SourceDatabaseName}].[{tailTableWithVerifyConfig.SourceSchemaName}].[{tailTableWithVerifyConfig.SourceTableName}] WITH (NOLOCK)
                                                    {tailTableWithVerifyConfig.WhereClause} 
                                                    ORDER BY {tailTableWithVerifyConfig.DateColumn}, {tailTableWithVerifyConfig.SourceKey}");
                                cmdStr = sb.ToString();
                                try
                                {
                                    var cmdTemp = new SqlCommand
                                    {
                                        Connection = cnSource,
                                        CommandType = CommandType.Text,
                                        CommandText = cmdStr,
                                        CommandTimeout = 0
                                    };
                                    Logger.Info(Logging.DbCmdString(cmdTemp));
                                    stepCreateSourceTempTable.Statement = cmdTemp.CommandText;
                                    tailTableWithVerifyConfig.SelectedRows = cmdTemp.ExecuteNonQuery();
                                    Logger.Info($@"ROWS: {tailTableWithVerifyConfig.SelectedRows}");
                                    stepCreateSourceTempTable.RowsAffected = tailTableWithVerifyConfig.SelectedRows;
                                }
                                catch (Exception e)
                                {
                                    stepCreateSourceTempTable.Errors.Add(new Error(e));
                                }
                                stepCreateSourceTempTable.Done(cnArchConfig);
                                response.ProcessSteps.Add(stepCreateSourceTempTable);
                            }

                            if (!response.Errors.Any() && tailTableWithVerifyConfig.SelectedRows > 0)
                            {
                                var stepVerifySourceTempTable = new ProcessStep(cnArchConfig, tailTableWithVerifyConfig.ProcessID, $@"Verify {tailTableWithVerifyConfig.SelectedRows} rows in #TempKeyList");
                                try
                                {

                                    // Verify that #Temp table has expected row count
                                    var cmdVerifyTemp = new SqlCommand
                                    {
                                        Connection = cnSource,
                                        CommandType = CommandType.Text,
                                        CommandText = $@"
                                                                SELECT COUNT(*) FROM #TempKeyList
                                        "
                                    };
                                    Logger.Debug(Logging.DbCmdString(cmdVerifyTemp));
                                    stepVerifySourceTempTable.Statement = cmdVerifyTemp.CommandText;
                                    tailTableWithVerifyConfig.TempRowsSource = Convert.ToInt32(cmdVerifyTemp.ExecuteScalar());
                                    Logger.Debug($@"ROWS: {tailTableWithVerifyConfig.TempRowsSource}");
                                    stepVerifySourceTempTable.RowsAffected = tailTableWithVerifyConfig.TempRowsSource;
                                }
                                catch (Exception e)
                                {
                                    stepVerifySourceTempTable.Errors.Add(new Error(e));
                                }
                                stepVerifySourceTempTable.Done(cnArchConfig);

                                if (tailTableWithVerifyConfig.TempRowsSource != tailTableWithVerifyConfig.TempRowsDest)
                                {
                                    var e = $"#TempKeyListSource ({tailTableWithVerifyConfig.TempRowsSource} rows) does not match #TempRowsDest ({tailTableWithVerifyConfig.TempRowsDest} rows)";
                                    stepCheckForMissingKeys.Errors.Add(new Error(e));
                                    Logger.Debug($"ERROR: {e}");
                                }
                                response.ProcessSteps.Add(stepVerifySourceTempTable);
                            }

                            if (!response.Errors.Any() && tailTableWithVerifyConfig.TempRowsSource == tailTableWithVerifyConfig.TempRowsDest)
                            {
                                var stepDeleteSourceData = new ProcessStep(cnArchConfig, tailTableWithVerifyConfig.ProcessID, $@"Delete {tailTableWithVerifyConfig.TempRowsSource} rows from {tailTableWithVerifyConfig.ServerQualifiedSourceName}");
                                sb = new StringBuilder();
                                sb.Append($@"
                                                                DELETE t FROM [{tailTableWithVerifyConfig.SourceSchemaName}].[{tailTableWithVerifyConfig.SourceTableName}] t
                                                                INNER JOIN #TempKeyList k 
                                                                    ON ");
                                // Build SourceKeyColumns collection
                                keyColumnList = tailTableWithVerifyConfig.SourceKey.Split(',').ToList();
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
                                Logger.Debug(Logging.DbCmdString(cmdDelete));
                                stepDeleteSourceData.Statement = cmdDelete.CommandText;
                                tailTableWithVerifyConfig.DeleteCommand = cmdDelete.CommandText;
                                //===============================================================================================
                                if (!tailTableWithVerifyConfig.TestModeNoDelete && !tailTableWithVerifyConfig.IsTestMode)
                                {
                                    tailTableWithVerifyConfig.DeleteStartTime = DateTime.Now;
                                    tailTableWithVerifyConfig.RowsDeleted = cmdDelete.ExecuteNonQuery();
                                    tailTableWithVerifyConfig.DeleteEndTime = DateTime.Now;
                                    Logger.Debug($@"Deleted ROWS: {tailTableWithVerifyConfig.RowsDeleted}");
                                    Logger.Debug($@"DeleteSeconds: {tailTableWithVerifyConfig.DeleteSeconds}");
                                    UpdateTailTableWithVerifyConfigDeletedRows(cnArchConfig, tailTableWithVerifyConfig);
                                }
                                //===============================================================================================
                                LogProcessUpdateDeleteSeconds(cnArchConfig, tailTableWithVerifyConfig);
                                stepDeleteSourceData.RowsAffected = tailTableWithVerifyConfig.RowsDeleted;
                                stepDeleteSourceData.Done(cnArchConfig);
                                response.ProcessSteps.Add(stepDeleteSourceData);
                            }
                        }
                        //Logger.Debug("SLEEP 2 SECONDS...");
                        //Thread.Sleep(2000);

                        if (!response.Errors.Any())
                        {
                            if (tailTableWithVerifyConfig.AutoConfigDeleteRowsAdjust && tailTableWithVerifyConfig.DeleteSeconds > maxDeleteSeconds && tailTableWithVerifyConfig.MaxDeleteRowsPerBatch > 1000)
                            {
                                var stepReduceConfigMaxDeleteRows = new ProcessStep(cnArchConfig, tailTableWithVerifyConfig.ProcessID, $@"Reduce Max Delete Rows by 10%");
                                var cmdReduceConfigMaxDeleteRows = new SqlCommand
                                {
                                    Connection = cnArchConfig,
                                    CommandType = CommandType.Text,
                                    CommandText = $@"
                                                                        UPDATE xx
                                                                        SET 
                                                                            [MaxDeleteRowsPerBatch] = [MaxDeleteRowsPerBatch] * .9
                                                                        FROM [dbo].[{_ConfigTableName}] xx
                                                                        WHERE [ConfigID] = {tailTableWithVerifyConfig.ConfigID}
                                        "
                                };
                                Logger.Debug(Logging.DbCmdString(cmdReduceConfigMaxDeleteRows));
                                stepReduceConfigMaxDeleteRows.Statement = cmdReduceConfigMaxDeleteRows.CommandText;
                                stepReduceConfigMaxDeleteRows.RowsAffected = cmdReduceConfigMaxDeleteRows.ExecuteNonQuery();
                                stepReduceConfigMaxDeleteRows.Done(cnArchConfig);
                                response.ProcessSteps.Add(stepReduceConfigMaxDeleteRows);
                                Logger.Debug($@"ROWS: {stepReduceConfigMaxDeleteRows.RowsAffected}");
                            }

                            if (tailTableWithVerifyConfig.AutoConfigDeleteRowsAdjust && tailTableWithVerifyConfig.DeleteSeconds < maxDeleteSeconds && (tailTableWithVerifyConfig.MaxDeleteRowsPerBatch * 1.1 <= maxDeleteRows))
                            {
                                var stepIncreaseConfigMaxDeleteRows = new ProcessStep(cnArchConfig, tailTableWithVerifyConfig.ProcessID, $@"Increase Max Delete Rows by 10%");
                                var cmdIncreaseConfigMaxDeleteRows = new SqlCommand
                                {
                                    Connection = cnArchConfig,
                                    CommandType = CommandType.Text,
                                    CommandText = $@"
                                                                        UPDATE xx
                                                                        SET 
                                                                            [MaxDeleteRowsPerBatch] = [MaxDeleteRowsPerBatch] * 1.1
                                                                        FROM [dbo].[{_ConfigTableName}] xx
                                                                        WHERE [ConfigID] = {tailTableWithVerifyConfig.ConfigID}
                                        "
                                };
                                Logger.Debug(Logging.DbCmdString(cmdIncreaseConfigMaxDeleteRows));
                                stepIncreaseConfigMaxDeleteRows.Statement = cmdIncreaseConfigMaxDeleteRows.CommandText;
                                stepIncreaseConfigMaxDeleteRows.RowsAffected = cmdIncreaseConfigMaxDeleteRows.ExecuteNonQuery();
                                stepIncreaseConfigMaxDeleteRows.Done(cnArchConfig);
                                response.ProcessSteps.Add(stepIncreaseConfigMaxDeleteRows);
                                Logger.Debug($@"ROWS: {stepIncreaseConfigMaxDeleteRows.RowsAffected}");
                            }
                        }
                    }
                }

                //--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

                if (!tailTableWithVerifyConfig.ValidationErrors.Any() && !response.Errors.Any())
                {
                    var stepGatherFinalStatistics = new ProcessStep(cnArchConfig, tailTableWithVerifyConfig.ProcessID, $@"Gather final statistics for {tailTableWithVerifyConfig.ServerQualifiedSourceName}");
                    try
                    {
                        tailTableWithVerifyConfig.SourceFinalRowCount = GetRowCountSource(cnSource, tailTableWithVerifyConfig);
                        tailTableWithVerifyConfig.DestinationFinalRowCount = GetRowCountDestination(cnDestination, tailTableWithVerifyConfig);
                        if (tailTableWithVerifyConfig.SourceFinalRowCount < tailTableWithVerifyConfig.MaxStatsRows && tailTableWithVerifyConfig.DestinationFinalRowCount < tailTableWithVerifyConfig.MaxStatsRows)
                        {
                            //tailTableWithVerifyConfig.HighwaterMarkDate = GetHighWaterMarkDate(cnDestination, tailTableWithVerifyConfig);
                            //tailTableWithVerifyConfig.HighwaterMarkDateString = tailTableWithVerifyConfig.HighwaterMarkDate.ToString("M/dd/yyyy hh:mm:ss tt", CultureInfo.CreateSpecificCulture("en-US"));
                            //tailTableWithVerifyConfig.HighwaterMarkID = GetMaxDestinationID(cnDestination, tailTableWithVerifyConfig);
                            tailTableWithVerifyConfig.RowsRemaining = GetRowsRemaining(cnDestination, tailTableWithVerifyConfig);
                            tailTableWithVerifyConfig.SourceMinDate = GetMinSourceDate(cnDestination, tailTableWithVerifyConfig);
                            tailTableWithVerifyConfig.SourceMaxDate = GetMaxSourceDate(cnDestination, tailTableWithVerifyConfig);
                            tailTableWithVerifyConfig.SourceMaxDateString = tailTableWithVerifyConfig.SourceMaxDate.ToString("M/dd/yyyy hh:mm:ss tt", CultureInfo.CreateSpecificCulture("en-US"));
                            tailTableWithVerifyConfig.DestinationMinDate = GetMinDestinationDate(cnDestination, tailTableWithVerifyConfig);
                            tailTableWithVerifyConfig.DestinationMaxDate = GetMaxDestinationDate(cnDestination, tailTableWithVerifyConfig);
                            tailTableWithVerifyConfig.DestinationMaxDateString = tailTableWithVerifyConfig.DestinationMaxDate.ToString("M/dd/yyyy hh:mm:ss tt", CultureInfo.CreateSpecificCulture("en-US"));
                        }
                        else
                        {
                            tailTableWithVerifyConfig.RowsRemaining = 0;
                            tailTableWithVerifyConfig.SourceMinDate = DateTime.Parse("1900-01-01");  //GetMinSourceDate(cnDestination, tailTableWithVerifyConfig);
                            tailTableWithVerifyConfig.SourceMaxDate = DateTime.Parse("1900-01-01");  // GetMaxSourceDate(cnDestination, tailTableWithVerifyConfig);
                            tailTableWithVerifyConfig.SourceMaxDateString = tailTableWithVerifyConfig.SourceMaxDate.ToString("M/dd/yyyy hh:mm:ss tt", CultureInfo.CreateSpecificCulture("en-US"));
                            tailTableWithVerifyConfig.DestinationMinDate = DateTime.Parse("1900-01-01");  //GetMinDestinationDate(cnDestination, tailTableWithVerifyConfig);
                            tailTableWithVerifyConfig.DestinationMaxDate = DateTime.Parse("1900-01-01");  //GetMaxDestinationDate(cnDestination, tailTableWithVerifyConfig);
                            tailTableWithVerifyConfig.DestinationMaxDateString = tailTableWithVerifyConfig.DestinationMaxDate.ToString("M/dd/yyyy hh:mm:ss tt", CultureInfo.CreateSpecificCulture("en-US"));
                        }
                        if (statsOnly == true) tailTableWithVerifyConfig.LastRunStatus = "Success";
                        var stepUpdateHighWaterMark = new ProcessStep(cnArchConfig, tailTableWithVerifyConfig.ProcessID, $@"Update HighWaterMark for {tailTableWithVerifyConfig.ServerQualifiedSourceName}");
                        try
                        {
                            UpdateTailTableWithVerifyConfigHighwaterMarks(cnArchConfig, tailTableWithVerifyConfig);
                            stepUpdateHighWaterMark.RowsAffected = 1;
                        }
                        catch (Exception e)
                        {
                            stepUpdateHighWaterMark.Errors.Add(new Error(e));
                        }
                        stepUpdateHighWaterMark.Done(cnArchConfig);
                        response.ProcessSteps.Add(stepUpdateHighWaterMark);
                        response.RowsAffected = tailTableWithVerifyConfig.RowsAffected;
                        response.InitialRowCount = tailTableWithVerifyConfig.DestinationInitialRowCount;
                        response.FinalRowCount = tailTableWithVerifyConfig.DestinationFinalRowCount;
                        response.SourceRowCount = tailTableWithVerifyConfig.SourceRowCount;
                        response.DestinationRowCount = tailTableWithVerifyConfig.DestinationFinalRowCount;
                        response.SFMinDate = tailTableWithVerifyConfig.SourceMinDate;
                        response.SFDays = tailTableWithVerifyConfig.SourceDays;
                        response.SourceMaxDate = tailTableWithVerifyConfig.SourceMaxDate;
                        response.DestinationMaxDate = tailTableWithVerifyConfig.DestinationMaxDate;
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
                tailTableWithVerifyConfig.IsRunning = false;
                tailTableWithVerifyConfig.LastRunEndDate = DateTime.Now;
                var stepUpdatetailTableWithVerifyConfigDone = new ProcessStep(cnArchConfig, tailTableWithVerifyConfig.ProcessID, $@"Set [Archiver].[dbo].[{_ConfigTableName}] DONE for {tailTableWithVerifyConfig.ServerQualifiedSourceName}");
                UpdateTailTableWithVerifyConfigIsRunning(cnArchConfig, tailTableWithVerifyConfig);
                LogProcessUpdate(cnArchConfig, tailTableWithVerifyConfig);
                stepUpdatetailTableWithVerifyConfigDone.RowsAffected = UpdateTailTableWithVerifyConfig(cnArchConfig, tailTableWithVerifyConfig);
                stepUpdatetailTableWithVerifyConfigDone.Done(cnArchConfig);
                Logger.Debug("END: " + Convert.ToDecimal((tailTableWithVerifyConfig.LastRunEndDate - tailTableWithVerifyConfig.LastRunStartDate).TotalSeconds) + " SECONDS");
            }
            return response;
        }

        private static ValidationErrors ValidateConfig(int stepID, TailTableWithVerifyConfig tailTableWithVerifyConfig, SqlConnection cnArchConfig, SqlConnection cnSource, SqlConnection cnDestination)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Debug($"BEGIN: {startTime}");
            var validationErrors = new ValidationErrors();
            try
            {
                if (string.IsNullOrEmpty(tailTableWithVerifyConfig.SourceTableName))
                {
                    var validationError = new ValidationError($"{tailTableWithVerifyConfig.ServerQualifiedSourceName} is not found in [Archiver].[dbo].[{_ConfigTableName}].", "Validation", "Fatal", true);
                    validationErrors.Add(validationError);
                    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                    tailTableWithVerifyConfig.ValidationStatus = validationError.Message;
                    Logger.Error(validationError.Message);
                }
                else
                {
                    if (tailTableWithVerifyConfig.IsRunning)
                    {
                        var validationError = new ValidationError($"Archive of {tailTableWithVerifyConfig.ServerQualifiedSourceName} is already Running.", "Validation", "Fatal", true);
                        validationErrors.Add(validationError);
                        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                        tailTableWithVerifyConfig.ValidationStatus = validationError.Message;
                    }
                    if (tailTableWithVerifyConfig.IsTail == false)
                    {
                        var validationError = new ValidationError($"{tailTableWithVerifyConfig.ServerQualifiedSourceName} is not configured for Tailing.", "Validation", "Fatal", true);
                        validationErrors.Add(validationError);
                        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                        tailTableWithVerifyConfig.ValidationStatus = validationError.Message;
                        Logger.Error(validationError.Message);
                    }
                    if (tailTableWithVerifyConfig.IsActive == false)
                    {
                        var validationError = new ValidationError($"{tailTableWithVerifyConfig.ServerQualifiedSourceName} is not Active.", "Validation", "Fatal", true);
                        validationErrors.Add(validationError);
                        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                        tailTableWithVerifyConfig.ValidationStatus = validationError.Message;
                        Logger.Error(validationError.Message);
                    }
                    if (tailTableWithVerifyConfig.IsEnabled == false && tailTableWithVerifyConfig.StatsOnly == false)
                    {
                        var validationError = new ValidationError($"{tailTableWithVerifyConfig.ServerQualifiedSourceName} is not Enabled.", "Validation", "Fatal", true);
                        validationErrors.Add(validationError);
                        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                        tailTableWithVerifyConfig.ValidationStatus = validationError.Message;
                        Logger.Error(validationError.Message);
                    }
                    if (tailTableWithVerifyConfig.TestModeConfig == true && tailTableWithVerifyConfig.IsTestMode == false && tailTableWithVerifyConfig.StatsOnly == false)
                    {
                        var validationError = new ValidationError($"{tailTableWithVerifyConfig.ServerQualifiedSourceName} is not configured for Test Mode but web.config is in Test Mode.", "Validation", "Fatal", true);
                        validationErrors.Add(validationError);
                        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                        tailTableWithVerifyConfig.ValidationStatus = validationError.Message;
                        Logger.Error(validationError.Message);
                    }
                }
                //if (!tailTableWithVerifyConfig.IsTestMode)
                //{
                //    if (!tailTableWithVerifyConfig.IsActive)
                //    {
                //        var validationError = new ValidationError($"{tailTableWithVerifyConfig.ServerQualifiedDestinationName} is Inactive.", "Validation", "Fatal", true);
                //        validationErrors.Add(validationError);
                //        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                //        tailTableWithVerifyConfig.ValidationStatus = validationError.Message;
                //        Logger.Error(validationError.Message);
                //    }
                //    if (!tailTableWithVerifyConfig.IsEnabled)
                //    {
                //        var validationError = new ValidationError($"{tailTableWithVerifyConfig.ServerQualifiedDestinationName} is Disabled.", "Validation", "Fatal", true);
                //        validationErrors.Add(validationError);
                //        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                //        tailTableWithVerifyConfig.ValidationStatus = validationError.Message;
                //        Logger.Error(validationError.Message);
                //    }
                //}
                //if (tailTableWithVerifyConfig.DestinationDatabaseName == "PlantArchive" && (tailTableWithVerifyConfig.SourceServerName != tailTableWithVerifyConfig.DestinationServerName))
                //{
                //    var validationError = new ValidationError($"{tailTableWithVerifyConfig.ServerQualifiedDestinationName} Source database must reside on the same server as the Destination.", "Validation", "Fatal", true);
                //    validationErrors.Add(validationError);
                //    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                //    tailTableWithVerifyConfig.ValidationStatus = validationError.Message;
                //    Logger.Error(validationError.Message);
                //}
                if (string.IsNullOrEmpty(tailTableWithVerifyConfig.SourceKey))
                {
                    var validationError = new ValidationError($"{tailTableWithVerifyConfig.ServerQualifiedSourceName} is not configured for SourceKey.", "Validation", "Fatal", true);
                    validationErrors.Add(validationError);
                    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                    tailTableWithVerifyConfig.ValidationStatus = validationError.Message;
                    Logger.Error(validationError.Message);
                }
                if (string.IsNullOrEmpty(tailTableWithVerifyConfig.DestinationKey))
                {
                    var validationError = new ValidationError($"{tailTableWithVerifyConfig.ServerQualifiedSourceName} is not configured for DestinationKey.", "Validation", "Fatal", true);
                    validationErrors.Add(validationError);
                    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                    tailTableWithVerifyConfig.ValidationStatus = validationError.Message;
                    Logger.Error(validationError.Message);
                }
                if (string.IsNullOrEmpty(tailTableWithVerifyConfig.DateColumn))
                {
                    var validationError = new ValidationError($"{tailTableWithVerifyConfig.ServerQualifiedSourceName} is not configured for DateColumn.", "Validation", "Fatal", true);
                    validationErrors.Add(validationError);
                    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                    tailTableWithVerifyConfig.ValidationStatus = validationError.Message;
                    Logger.Error(validationError.Message);
                }
                if (tailTableWithVerifyConfig.IDColumn.Contains(","))
                {
                    var validationError = new ValidationError($"IDColumn must be a single value for {tailTableWithVerifyConfig.ServerQualifiedSourceName}.", "Validation", "Fatal", true);
                    validationErrors.Add(validationError);
                    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                    tailTableWithVerifyConfig.ValidationStatus = validationError.Message;
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
                if (!validationErrors.Any()) tailTableWithVerifyConfig.ValidationStatus = "Valid";
                Logger.Debug($"END: {Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds)} SECONDS");
            }
            return validationErrors;
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

        private static bool CheckSourceDatabaseExists(SqlConnection cnSource, TailTableWithVerifyConfig tailTableWithVerifyConfig)
        {
            bool retVal = false;
            try
            {
                string str = string.Format($@"SELECT 1 FROM sys.databases WHERE Name = '{tailTableWithVerifyConfig.SourceDatabaseName}'");
                SqlCommand cmd = new SqlCommand(str, cnSource);
                Logger.Debug(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                Logger.Debug($@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnSource.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new ApplicationException(cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            return retVal;
        }

        private static bool CheckSourceSchemaExists(SqlConnection cnSource, TailTableWithVerifyConfig tailTableWithVerifyConfig)
        {
            bool retVal = false;
            try
            {
                string str = string.Format($@"SELECT 1 FROM sys.schemas WHERE name = '{tailTableWithVerifyConfig.SourceSchemaName}'");
                SqlCommand cmd = new SqlCommand(str, cnSource);
                Logger.Debug(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                Logger.Debug($@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnSource.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new ApplicationException(cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            return retVal;
        }

        private static bool CheckSourceTableExists(SqlConnection cnSource, TailTableWithVerifyConfig tailTableWithVerifyConfig)
        {
            bool retVal = false;
            try
            {
                string str = string.Format($@"
                    SELECT 1 
                    FROM {tailTableWithVerifyConfig.SourceDatabaseName}.sys.tables t
                    INNER JOIN {tailTableWithVerifyConfig.SourceDatabaseName}.sys.schemas s ON s.schema_id = t.schema_id
                    WHERE s.name = '{tailTableWithVerifyConfig.SourceSchemaName}'
                    AND t.name = '{tailTableWithVerifyConfig.SourceTableName}'
                ");
                SqlCommand cmd = new SqlCommand(str, cnSource);
                Logger.Debug(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                Logger.Debug(@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnSource.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new ApplicationException(cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            return retVal;
        }

        private static KeyColumn GetKeyData(SqlConnection cn, string schemaName, string tableName, string columnName)
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
                cmd.CommandTimeout = 0;
                Logger.Debug(Logging.DbCmdString(cmd));
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
                //Logger.Debug($@"columnType = {DataType}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {cn.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new ApplicationException(cn.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
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

        private static bool IsSourceKeyUnique(SqlConnection cnSource, TailTableWithVerifyConfig tailTableWithVerifyConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Debug($"BEGIN: {DateTime.Now.ToLocalTime()}");
            bool retVal = false;
            var rowCount = 0;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnSource,
                    CommandType = CommandType.Text,
                    CommandTimeout = 0,
                    CommandText = $@"
                        SELECT  [{tailTableWithVerifyConfig.SourceKey.Replace(",", "], [")}], COUNT(*)
                        FROM    [{tailTableWithVerifyConfig.SourceSchemaName}].[{tailTableWithVerifyConfig.SourceTableName}] WITH (NOLOCK) 
                        GROUP BY [{tailTableWithVerifyConfig.SourceKey.Replace(",", "], [")}]
                        HAVING COUNT(*) > 1
                    "
                };
                Logger.Trace(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        rowCount += 1;
                    }

                    reader.Close();
                    Logger.Trace("ROWS: " + rowCount);
                }
                if (rowCount == 0) retVal = true;
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
            return retVal;
        }

        private static bool CheckDestinationDatabaseExists(SqlConnection cnDestination, TailTableWithVerifyConfig tailTableWithVerifyConfig)
        {
            bool retVal = false;
            try
            {
                string str = string.Format($@"SELECT 1 FROM sys.databases WHERE Name = '{tailTableWithVerifyConfig.DestinationDatabaseName}'");
                SqlCommand cmd = new SqlCommand(str, cnDestination);
                Logger.Debug(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                Logger.Debug($@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new ApplicationException(cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            return retVal;
        }

        private static bool CheckDestinationSchemaExists(SqlConnection cnDestination, TailTableWithVerifyConfig tailTableWithVerifyConfig)
        {
            bool retVal = false;
            try
            {
                string str = string.Format($@"SELECT 1 FROM sys.schemas WHERE name = '{tailTableWithVerifyConfig.DestinationSchemaName}'");
                SqlCommand cmd = new SqlCommand(str, cnDestination);
                Logger.Debug(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                Logger.Debug($@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new ApplicationException(cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            return retVal;
        }

        private static bool CheckDestinationTableExists(SqlConnection cnDestination, TailTableWithVerifyConfig tailTableWithVerifyConfig)
        {
            bool retVal = false;
            try
            {
                string str = string.Format($@"
                    SELECT 1 
                    FROM {tailTableWithVerifyConfig.DestinationDatabaseName}.sys.tables t
                    INNER JOIN {tailTableWithVerifyConfig.DestinationDatabaseName}.sys.schemas s ON s.schema_id = t.schema_id
                    WHERE s.name = '{tailTableWithVerifyConfig.DestinationSchemaName}'
                    AND t.name = '{tailTableWithVerifyConfig.DestinationTableName}'
                ");
                SqlCommand cmd = new SqlCommand(str, cnDestination);
                Logger.Debug(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                Logger.Debug(@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new ApplicationException(cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            return retVal;
        }

        private static bool CheckDestinationTableStructureMissingColumns(SqlConnection cnDestination, TailTableWithVerifyConfig tailTableWithVerifyConfig)
        {
            bool retVal = false;
            try
            {
                string str = $@"
                    SELECT  COUNT(*)
                    FROM    ( SELECT    *
                              FROM      {tailTableWithVerifyConfig.SourceDatabaseName}.information_schema.columns
                              WHERE     [TABLE_SCHEMA] = '{tailTableWithVerifyConfig.SourceSchemaName}'
                                        AND [TABLE_NAME] = '{tailTableWithVerifyConfig.SourceTableName}'
                            ) src
                            FULL JOIN ( SELECT  *
                                        FROM    {tailTableWithVerifyConfig.DestinationDatabaseName}.information_schema.COLUMNS
                                        WHERE   [TABLE_SCHEMA] = '{tailTableWithVerifyConfig.DestinationSchemaName}'
                                                AND [TABLE_NAME] = '{tailTableWithVerifyConfig.DestinationTableName}'
                                      ) targ ON targ.[COLUMN_NAME] = src.[COLUMN_NAME]
                    WHERE targ.[DATA_TYPE] IS NULL
                ";
                SqlCommand cmd = new SqlCommand(str, cnDestination);
                Logger.Debug(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                Logger.Debug($@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new ApplicationException(cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            return retVal;
        }

        private static bool CheckDestinationTableStructureDifferentColumns(SqlConnection cnDestination, TailTableWithVerifyConfig tailTableWithVerifyConfig)
        {
            bool retVal = false;
            try
            {
                string str = string.Format($@"
                    SELECT  COUNT(*)
                    FROM    ( SELECT    *
                              FROM      {tailTableWithVerifyConfig.SourceDatabaseName}.information_schema.columns
                              WHERE     [TABLE_SCHEMA] = '{tailTableWithVerifyConfig.SourceSchemaName}'
                                        AND [TABLE_NAME] = '{tailTableWithVerifyConfig.SourceTableName}'
                            ) src
                            FULL JOIN ( SELECT  *
                                        FROM    {tailTableWithVerifyConfig.DestinationDatabaseName}.information_schema.COLUMNS
                                        WHERE   [TABLE_SCHEMA] = '{tailTableWithVerifyConfig.DestinationSchemaName}'
                                                AND [TABLE_NAME] = '{tailTableWithVerifyConfig.DestinationTableName}'
                                      ) targ ON targ.[COLUMN_NAME] = src.[COLUMN_NAME]
                    WHERE targ.[DATA_TYPE] <> src.[DATA_TYPE]
					OR targ.[CHARACTER_MAXIMUM_LENGTH] < src.[CHARACTER_MAXIMUM_LENGTH]
					OR targ.[NUMERIC_PRECISION] < src.[NUMERIC_PRECISION]
					OR targ.[NUMERIC_SCALE] < src.[NUMERIC_SCALE]
                ");
                SqlCommand cmd = new SqlCommand(str, cnDestination);
                Logger.Debug(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                Logger.Debug($@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new ApplicationException(cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            return retVal;
        }

        private static bool CheckDestinationTableUniqueIndex(SqlConnection cnDestination, TailTableWithVerifyConfig tailTableWithVerifyConfig)
        {
            bool retVal = false;
            try
            {
                //var x = string.Join("','", mergeTableConfig.DestinationKeyColumns);
                var columnsIn = "";
                var keyColumnList = tailTableWithVerifyConfig.DestinationKey.Split(',').ToList();
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
                        WHERE OBJECT_SCHEMA_NAME(i.object_id) = '{tailTableWithVerifyConfig.DestinationSchemaName}'
                              AND OBJECT_NAME(i.object_id) = '{tailTableWithVerifyConfig.DestinationTableName}'
                              AND i.is_unique = 1
                              AND ic.is_included_column = 0
                    ) a
                    WHERE ColumnName IS NOT NULL
                ";
                SqlCommand cmd = new SqlCommand(cmdString, cnDestination);
                cmd.CommandTimeout = 0;
                Logger.Debug(Logging.DbCmdString(cmd));
                var rowCount = Convert.ToInt32(cmd.ExecuteScalar());
                retVal = rowCount == keyColumnList.Count();
                Logger.Debug($@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new ApplicationException(cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            return retVal;
        }

        private static bool IsDestinationKeyUnique(SqlConnection cnDestination, TailTableWithVerifyConfig tailTableWithVerifyConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Debug($"BEGIN: {DateTime.Now.ToLocalTime()}");
            bool retVal = false;
            var rowCount = 0;
            try
            {
                if (cnDestination.State != ConnectionState.Open) cnDestination.Open();
                var cmd = new SqlCommand
                {
                    Connection = cnDestination,
                    CommandType = CommandType.Text,
                    CommandTimeout = 0,
                    CommandText = $@"
                        SELECT  [{tailTableWithVerifyConfig.DestinationKey.Replace(",", "], [")}], COUNT(*)
                        FROM    [{tailTableWithVerifyConfig.DestinationSchemaName}].[{tailTableWithVerifyConfig.DestinationTableName}] WITH (NOLOCK) 
                        GROUP BY [{tailTableWithVerifyConfig.DestinationKey.Replace(",", "], [")}]
                        HAVING COUNT(*) > 1
                    "
                };
                Logger.Trace(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        rowCount += 1;
                    }

                    reader.Close();
                    Logger.Trace("ROWS: " + rowCount);
                }
                retVal = (rowCount == 0);
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
            return retVal;
        }

        private static long UpdateTailTableWithVerifyConfigIsRunning(SqlConnection cnArchConfig, TailTableWithVerifyConfig tailTableWithVerifyConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Debug($"BEGIN: {DateTime.Now.ToLocalTime()}");
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
                            [IsRunning] = {Convert.ToInt32(tailTableWithVerifyConfig.IsRunning)}
                            ,[ProcessID] = {tailTableWithVerifyConfig.ProcessID}
                            ,[LastRunStatus] = '{tailTableWithVerifyConfig.LastRunStatus}'
                            ,[LastRunStartDate] = '{tailTableWithVerifyConfig.LastRunStartDate}'
                            ,[LastRunEndDate] = '{tailTableWithVerifyConfig.LastRunEndDate}'
                            ,[IsSourceKeyUnique] = '{tailTableWithVerifyConfig.IsSourceKeyUnique}'
                            ,[IsDestinationKeyUnique] = '{tailTableWithVerifyConfig.IsDestinationKeyUnique}'
                            ,[BITimeStamp] = GETDATE()
                        FROM [dbo].[{_ConfigTableName}] xx
                        WHERE [ConfigID] = {tailTableWithVerifyConfig.ConfigID}
                    "
                };
                Logger.Debug(Logging.DbCmdString(cmd));
                rowCount = Convert.ToInt64(cmd.ExecuteNonQuery());
                Logger.Debug("ROWS: " + rowCount);
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnArchConfig.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new Exception($"{cnArchConfig.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            finally
            {
                Logger.Debug("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return rowCount;
        }

        private static long GetRowCountSource(SqlConnection cnSource, TailTableWithVerifyConfig tailTableWithVerifyConfig, string whereClause = null)
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
                                                {tailTableWithVerifyConfig.SourceDatabaseName}.sys.tables tbl
                                                INNER JOIN {tailTableWithVerifyConfig.SourceDatabaseName}.sys.schemas sch ON sch.schema_id = tbl.schema_id
                                                INNER JOIN {tailTableWithVerifyConfig.SourceDatabaseName}.sys.partitions part ON part.object_id = tbl.object_id
                                            WHERE 
                                                part.index_id IN (0, 1) -- 0 for heaps, 1 for clustered indexes (i.e., the table itself)
                                                and sch.name = '{tailTableWithVerifyConfig.SourceSchemaName}' 
                                                and tbl.name = '{tailTableWithVerifyConfig.SourceTableName}'
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

        private static long GetRowCountDestination(SqlConnection cnDestination, TailTableWithVerifyConfig tailTableWithVerifyConfig, string whereClause = null)
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
                                                {tailTableWithVerifyConfig.SourceDatabaseName}.sys.tables tbl
                                                INNER JOIN {tailTableWithVerifyConfig.SourceDatabaseName}.sys.schemas sch ON sch.schema_id = tbl.schema_id
                                                INNER JOIN {tailTableWithVerifyConfig.SourceDatabaseName}.sys.partitions part ON part.object_id = tbl.object_id
                                            WHERE 
                                                part.index_id IN (0, 1) -- 0 for heaps, 1 for clustered indexes (i.e., the table itself)
                                                and sch.name = '{tailTableWithVerifyConfig.SourceSchemaName}' 
                                                and tbl.name = '{tailTableWithVerifyConfig.SourceTableName}'
                    "
                    //CommandText = $@"
                    //    SELECT COUNT(*) 
                    //    FROM {tailTableWithVerifyConfig.FullyQualifiedDestinationName} WITH (NOLOCK) 
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

        private static DateTime GetHighWaterMarkDate(SqlConnection cnDestination, TailTableWithVerifyConfig tailTableWithVerifyConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Debug($"BEGIN: {DateTime.Now.ToLocalTime()}");
            DateTime retVal;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnDestination,
                    CommandType = CommandType.Text,
                    CommandTimeout = 0,
                    CommandText = BuildHighWaterMarkDateString(tailTableWithVerifyConfig)
                };
                Logger.Debug(Logging.DbCmdString(cmd));
                retVal = Convert.ToDateTime(cmd.ExecuteScalar());
                Logger.Debug("retVal: " + retVal);
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
            return retVal;
        }

        private static String BuildHighWaterMarkDateString(TailTableWithVerifyConfig archiveTableConfig)
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
                //Logger.Debug($@"str = {str}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {e.Message}: {e.StackTrace}");
                str = "";
            }
            return str;
        }

        private static long GetRowsRemaining(SqlConnection cnDestination, TailTableWithVerifyConfig tailTableWithVerifyConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Debug($"BEGIN: {DateTime.Now.ToLocalTime()}");
            long retVal;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnDestination,
                    CommandType = CommandType.Text,
                    CommandTimeout = 0,
                    CommandText = BuildRowsRemainingString(tailTableWithVerifyConfig)
                };
                Logger.Debug(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt64(cmd.ExecuteScalar());
                Logger.Debug("retVal: " + retVal);
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
            return retVal;
        }

        private static String BuildRowsRemainingString(TailTableWithVerifyConfig tailTableWithVerifyConfig)
        {
            string str;
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($@"SELECT COUNT(*) ");
                sb.Append(Environment.NewLine);
                sb.Append($@"FROM [{tailTableWithVerifyConfig.SourceDatabaseName}].[{tailTableWithVerifyConfig.SourceSchemaName}].[{tailTableWithVerifyConfig.SourceTableName}] AS SRC WITH (NOLOCK) ");
                sb.Append(Environment.NewLine);
                sb.Append($@"WHERE NOT EXISTS (SELECT 1 FROM [{tailTableWithVerifyConfig.DestinationDatabaseName}].[{tailTableWithVerifyConfig.DestinationSchemaName}].[{tailTableWithVerifyConfig.DestinationTableName}] DEST WITH (NOLOCK) ");
                sb.Append(Environment.NewLine);
                sb.Append("                  WHERE ");
                var keyColumnList = tailTableWithVerifyConfig.DestinationKey.Split(',').ToList();
                var i = 0;
                foreach (var keyColumn in keyColumnList)
                {
                    i++;
                    sb.Append($@"DEST.[{keyColumn}] = SRC.[{keyColumn}]");
                    sb.Append(Environment.NewLine);
                    if (i < keyColumnList.Count) sb.Append("                AND ");
                }
                //sb.Append($@"                AND DEST.{tailTableWithVerifyConfig.DateColumn} = SRC.{tailTableWithVerifyConfig.DateColumn} ");
                sb.Append(") ");
                str = sb.ToString();
                //Logger.Debug($@"str = {str}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {e.Message}: {e.StackTrace}");
                str = "";
            }
            return str;
        }

        private static Int64 GetMaxDestinationID(SqlConnection cnDestination, TailTableWithVerifyConfig tailTableWithVerifyConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Debug($"BEGIN: {DateTime.Now.ToLocalTime()}");
            Int64 retVal = 0;
            try
            {
                if (!string.IsNullOrEmpty(tailTableWithVerifyConfig.IDColumn))
                {
                    var cmd = new SqlCommand
                    {
                        Connection = cnDestination,
                        CommandType = CommandType.Text,
                        CommandTimeout = 0,
                        CommandText = $@"
                            SELECT ISNULL(MAX([{tailTableWithVerifyConfig.IDColumn}]), 0)  
                            FROM [{tailTableWithVerifyConfig.DestinationSchemaName}].[{tailTableWithVerifyConfig.DestinationTableName}] WITH (NOLOCK) 
                    "
                    };
                    Logger.Debug(Logging.DbCmdString(cmd));
                    retVal = Convert.ToInt64(cmd.ExecuteScalar());
                }
                Logger.Debug("ROWS: " + retVal);
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
            return retVal;
        }

        private static DateTime GetMinSourceDate(SqlConnection cnDestination, TailTableWithVerifyConfig tailTableWithVerifyConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Debug($"BEGIN: {DateTime.Now.ToLocalTime()}");
            DateTime retVal;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnDestination,
                    CommandType = CommandType.Text,
                    CommandTimeout = 0,
                    CommandText = $@"
                        SELECT ISNULL(MIN([{tailTableWithVerifyConfig.DateColumn}]), '1900-01-01')  
                        FROM {tailTableWithVerifyConfig.FullyQualifiedSourceName} WITH (NOLOCK) 
                    "
                };
                Logger.Debug(Logging.DbCmdString(cmd));
                retVal = Convert.ToDateTime(cmd.ExecuteScalar());
                Logger.Debug("ROWS: " + retVal);
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
            return retVal;
        }

        private static DateTime GetMaxSourceDate(SqlConnection cnSource, TailTableWithVerifyConfig tailTableWithVerifyConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Debug($"BEGIN: {DateTime.Now.ToLocalTime()}");
            DateTime retVal;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnSource,
                    CommandType = CommandType.Text,
                    CommandTimeout = 0,
                    CommandText = $@"
                        SELECT ISNULL(MAX([{tailTableWithVerifyConfig.DateColumn}]), '1900-01-01')  
                        FROM {tailTableWithVerifyConfig.FullyQualifiedSourceName} WITH (NOLOCK) 
                    "
                };
                Logger.Debug(Logging.DbCmdString(cmd));
                retVal = Convert.ToDateTime(cmd.ExecuteScalar());
                Logger.Debug("ROWS: " + retVal);
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
            return retVal;
        }

        private static DateTime GetMinDestinationDate(SqlConnection cnDestination, TailTableWithVerifyConfig tailTableWithVerifyConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Debug($"BEGIN: {DateTime.Now.ToLocalTime()}");
            DateTime retVal;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnDestination,
                    CommandType = CommandType.Text,
                    CommandTimeout = 0,
                    CommandText = $@"
                        SELECT ISNULL(MIN([{tailTableWithVerifyConfig.DateColumn}]), '1900-01-01')  
                        FROM {tailTableWithVerifyConfig.FullyQualifiedDestinationName} WITH (NOLOCK) 
                    "
                };
                Logger.Debug(Logging.DbCmdString(cmd));
                retVal = Convert.ToDateTime(cmd.ExecuteScalar());
                Logger.Debug("ROWS: " + retVal);
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
            return retVal;
        }

        private static DateTime GetMaxDestinationDate(SqlConnection cnDestination, TailTableWithVerifyConfig tailTableWithVerifyConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Debug($"BEGIN: {DateTime.Now.ToLocalTime()}");
            DateTime retVal;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnDestination,
                    CommandType = CommandType.Text,
                    CommandTimeout = 0,
                    CommandText = $@"
                        SELECT ISNULL(MAX([{tailTableWithVerifyConfig.DateColumn}]), '1900-01-01')  
                        FROM {tailTableWithVerifyConfig.FullyQualifiedDestinationName} WITH (NOLOCK) 
                    "
                };
                Logger.Debug(Logging.DbCmdString(cmd));
                retVal = Convert.ToDateTime(cmd.ExecuteScalar());
                Logger.Debug("ROWS: " + retVal);
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
            return retVal;
        }

        private static long UpdateTailTableWithVerifyConfigHighwaterMarks(SqlConnection cnArchConfig, TailTableWithVerifyConfig tailTableWithVerifyConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Debug($"BEGIN: {DateTime.Now.ToLocalTime()}");
            long rowCount;
            try
            {
                var cmd = new SqlCommand();
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = $@"
                    UPDATE xx
                    SET 
                        [HighwaterMarkDate] = '{tailTableWithVerifyConfig.HighwaterMarkDate}'
                        ,[HighwaterMarkID] = {tailTableWithVerifyConfig.HighwaterMarkID}
                        ,[SourceInitialRowCount] = {tailTableWithVerifyConfig.SourceInitialRowCount}
                        ,[SourceFinalRowCount] = {tailTableWithVerifyConfig.SourceFinalRowCount}
                        ,[SourceMinDate] = '{tailTableWithVerifyConfig.SourceMinDate}'
                        ,[SourceMaxDate] = '{tailTableWithVerifyConfig.SourceMaxDate}'
                        ,[DestinationInitialRowCount] = {tailTableWithVerifyConfig.DestinationInitialRowCount}
                        ,[DestinationFinalRowCount] = {tailTableWithVerifyConfig.DestinationFinalRowCount}
                        ,[DestinationMinDate] = '{tailTableWithVerifyConfig.DestinationMinDate}'
                        ,[DestinationMaxDate] = '{tailTableWithVerifyConfig.DestinationMaxDate}'
                        ,[RowsRemaining] = {tailTableWithVerifyConfig.RowsRemaining}
                        ,[RowsDeleted] = {tailTableWithVerifyConfig.RowsDeleted}
                        ,[BITimeStamp] = GETDATE()
                    FROM [dbo].[{_ConfigTableName}] xx
                    WHERE [ConfigID] = {tailTableWithVerifyConfig.ConfigID}
                ";
                cmd.Connection = cnArchConfig;
                Logger.Debug(Logging.DbCmdString(cmd));
                rowCount = Convert.ToInt64(cmd.ExecuteNonQuery());
                Logger.Debug("ROWS: " + rowCount);
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnArchConfig.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new Exception($"{cnArchConfig.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            finally
            {
                Logger.Debug("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return rowCount;
        }

        private static long UpdateTailTableWithVerifyConfigDeletedRows(SqlConnection cnArchConfig, TailTableWithVerifyConfig tailTableWithVerifyConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Debug($"BEGIN: {DateTime.Now.ToLocalTime()}");
            long rowCount;
            try
            {
                var cmd = new SqlCommand();
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = $@"
                    UPDATE xx
                    SET 
                        [RowsDeleted] = {tailTableWithVerifyConfig.RowsDeleted}
                        ,[DeleteSeconds] = {tailTableWithVerifyConfig.DeleteSeconds}
                        ,[BITimeStamp] = GETDATE()
                    FROM [dbo].[{_ConfigTableName}] xx
                    WHERE [ConfigID] = {tailTableWithVerifyConfig.ConfigID}
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
                Logger.Debug("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return rowCount;
        }

        private static long UpdateTailTableWithVerifyConfig(SqlConnection cnArchConfig, TailTableWithVerifyConfig tailTableWithVerifyConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Debug($"BEGIN: {DateTime.Now.ToLocalTime()}");
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
                            [IsRunning] = {Convert.ToInt32(tailTableWithVerifyConfig.IsRunning)}
                            ,[LastRunStatus] = '{tailTableWithVerifyConfig.LastRunStatus}'
                            ,[SourceInitialRowCount] = {tailTableWithVerifyConfig.SourceInitialRowCount}
                            ,[SourceFinalRowCount] = {tailTableWithVerifyConfig.SourceFinalRowCount}
                            ,[DestinationInitialRowCount] = {tailTableWithVerifyConfig.DestinationInitialRowCount}
                            ,[DestinationFinalRowCount] = {tailTableWithVerifyConfig.DestinationFinalRowCount}
                            ,[RowsRemaining] = {tailTableWithVerifyConfig.RowsRemaining}
                            ,[BITimeStamp] = GETDATE()
                        FROM [dbo].[{_ConfigTableName}] xx
                        WHERE [ConfigID] = {tailTableWithVerifyConfig.ConfigID}
                    "
                };
                Logger.Debug(Logging.DbCmdString(cmd));
                rowCount = Convert.ToInt64(cmd.ExecuteNonQuery());
                Logger.Debug("ROWS: " + rowCount);
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnArchConfig.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new Exception($"{cnArchConfig.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            finally
            {
                Logger.Debug("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return rowCount;
        }

        private static long UpdateTailTableWithVerifyConfigIsEnabled(SqlConnection cnArchConfig, TailTableWithVerifyConfig tailTableWithVerifyConfig, bool isEnabled)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Debug($"BEGIN: {DateTime.Now.ToLocalTime()}");
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
                            ,[AutoConfigDisable] = {(tailTableWithVerifyConfig.AutoConfigDisable == true ? 1 : 0)}
                            ,[BITimeStamp] = GETDATE()
                        FROM [dbo].[{_ConfigTableName}] xx
                        WHERE [ConfigID] = {tailTableWithVerifyConfig.ConfigID}
                    "
                };
                Logger.Debug(Logging.DbCmdString(cmd));
                rowCount = Convert.ToInt64(cmd.ExecuteNonQuery());
                Logger.Debug("ROWS: " + rowCount);
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnArchConfig.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new Exception($"{cnArchConfig.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            finally
            {
                Logger.Debug("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return rowCount;
        }

        private static long UpdateTailTableWithVerifyConfigInitialRowCount(SqlConnection cnArchConfig, TailTableWithVerifyConfig tailTableWithVerifyConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Debug($"BEGIN: {DateTime.Now.ToLocalTime()}");
            long rowCount;
            try
            {
                var cmd = new SqlCommand();
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = $@"
                    UPDATE xx
                    SET 
                        [SourceInitialRowCount] = CASE WHEN [SourceInitialRowCount] = 0 THEN {tailTableWithVerifyConfig.SourceInitialRowCount} ELSE [SourceInitialRowCount] END
                        ,[DestinationInitialRowCount] = CASE WHEN [DestinationInitialRowCount] = 0 THEN {tailTableWithVerifyConfig.DestinationInitialRowCount} ELSE [DestinationInitialRowCount] END
                        ,[BITimeStamp] = GETDATE()
                    FROM [dbo].[{_ConfigTableName}] xx
                    WHERE [ConfigID] = {tailTableWithVerifyConfig.ConfigID}
                ";
                cmd.Connection = cnArchConfig;
                Logger.Debug(Logging.DbCmdString(cmd));
                rowCount = Convert.ToInt64(cmd.ExecuteNonQuery());
                Logger.Debug("ROWS: " + rowCount);
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnArchConfig.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new Exception($"{cnArchConfig.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            finally
            {
                Logger.Debug("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return rowCount;
        }

        private static long UpdateTailTableWithVerifyConfigIsKeyUnique(SqlConnection cnArchConfig, TailTableWithVerifyConfig tailTableWithVerifyConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Debug($"BEGIN: {DateTime.Now.ToLocalTime()}");
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
                            [IsSourceKeyUnique] = '{tailTableWithVerifyConfig.IsSourceKeyUnique}'
                            ,[IsDestinationKeyUnique] = '{tailTableWithVerifyConfig.IsDestinationKeyUnique}'
                            ,[BITimeStamp] = GETDATE()
                        FROM [dbo].[{_ConfigTableName}] xx
                        WHERE [ConfigID] = {tailTableWithVerifyConfig.ConfigID}
                    "
                };
                Logger.Debug(Logging.DbCmdString(cmd));
                rowCount = Convert.ToInt64(cmd.ExecuteNonQuery());
                Logger.Debug("ROWS: " + rowCount);
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnArchConfig.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new Exception($"{cnArchConfig.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            finally
            {
                Logger.Debug("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return rowCount;
        }

        public static int LogProcessInsert(SqlConnection cnArchConfig, string processName, long batchID, TailTableWithVerifyConfig tailTableConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            int newID;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnArchConfig,
                    CommandType = CommandType.Text,
                    CommandText = $@"
                        INSERT INTO [Archiver].[LOG].[Process]
                        (
                            [ProcessName]
                           ,[BatchID]
                           ,[StartDate]
                           ,[ConfigID]
                           ,[SourceServerName]
                           ,[SourceDatabaseName]
                           ,[SourceSchemaName]
                           ,[SourceTableName]
                          ,[DestinationServerName]
                          ,[DestinationDatabaseName]
                          ,[DestinationSchemaName]
                          ,[DestinationTableName]
                           ,[MaxDeleteRowsPerBatch]
                           ,[TestModeNoDelete]
                           ,[SourceKey]
                           ,[DateColumn]
                           ,[RetentionDays]
                           ,[OtherCondition]
                        )
	                    VALUES 
	                    (
                            '{processName}'
                           ,{batchID}
                           ,'{DateTime.Now}'
                           ,{tailTableConfig.ConfigID}
                           ,'{tailTableConfig.SourceServerName}'
                           ,'{tailTableConfig.SourceDatabaseName}'
                           ,'{tailTableConfig.SourceSchemaName}'
                           ,'{tailTableConfig.SourceTableName}'
                           ,'{tailTableConfig.DestinationServerName}'
                           ,'{tailTableConfig.DestinationDatabaseName}'
                           ,'{tailTableConfig.DestinationSchemaName}'
                           ,'{tailTableConfig.DestinationTableName}'
                           ,{tailTableConfig.MaxDeleteRowsPerBatch}
                           ,'{tailTableConfig.TestModeNoDelete}'
                           ,'{tailTableConfig.SourceKey}'
                           ,'{tailTableConfig.DateColumn}'
                           ,{tailTableConfig.RetentionDays}
                           ,'{tailTableConfig.OtherCondition}'
                        );
                        SELECT SCOPE_IDENTITY();
                    "
                };
                Logger.Info(Logging.DbCmdString(cmd));
                newID = Convert.ToInt32(cmd.ExecuteScalar());
                Logger.Info($"New ProcessID: {newID}");
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cnArchConfig.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cnArchConfig.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }

            return newID;
        }

        public static long LogProcessUpdate(SqlConnection cnArchConfig, TailTableWithVerifyConfig tailTableConfig)
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
                        UPDATE [Archiver].[LOG].[Process]
                        SET [EndDate] = '{tailTableConfig.LastRunEndDate}'
                          ,[LastRunStatus] = '{tailTableConfig.LastRunStatus}'
                          ,[ConfigID] = {tailTableConfig.ConfigID}
                          ,[AutoConfigDisable] = {Convert.ToInt32(tailTableConfig.AutoConfigDisable)}
                          ,[IsArchive] = {Convert.ToInt32(tailTableConfig.IsArchive)}
                          ,[IsTail] = {Convert.ToInt32(tailTableConfig.IsTail)}
                          ,[IsVerify] = {Convert.ToInt32(tailTableConfig.IsVerify)}
                          ,[IsActive] = {Convert.ToInt32(tailTableConfig.IsActive)}
                          ,[IsEnabled] = {Convert.ToInt32(tailTableConfig.IsEnabled)}
                          ,[IsTestMode] = {Convert.ToInt32(tailTableConfig.IsTestMode)}
                          ,[IsHammerMode] = {Convert.ToInt32(tailTableConfig.IsHammerMode)}
                          ,[TestModeNoDelete] = {Convert.ToInt32(tailTableConfig.TestModeNoDelete)}
                          ,[MaxDeleteRowsPerBatch] = {tailTableConfig.MaxDeleteRowsPerBatch}
                          ,[SourceServerName] = '{tailTableConfig.SourceServerName}'
                          ,[SourceDatabaseName] = '{tailTableConfig.SourceDatabaseName}'
                          ,[SourceSchemaName] = '{tailTableConfig.SourceSchemaName}'
                          ,[SourceTableName] = '{tailTableConfig.SourceTableName}'
                          ,[DestinationServerName] = '{tailTableConfig.DestinationServerName}'
                          ,[DestinationDatabaseName] = '{tailTableConfig.DestinationDatabaseName}'
                          ,[DestinationSchemaName] = '{tailTableConfig.DestinationSchemaName}'
                          ,[DestinationTableName] = '{tailTableConfig.DestinationTableName}'
                          ,[IDColumn] = '{tailTableConfig.IDColumn}'
                          ,[SourceKey] = '{tailTableConfig.SourceKey}'
                          ,[IsSourceKeyUnique] = {Convert.ToInt32(tailTableConfig.IsSourceKeyUnique)}
                          ,[DestinationKey] = '{tailTableConfig.DestinationKey}'
                          ,[IsDestinationKeyUnique] = {Convert.ToInt32(tailTableConfig.IsDestinationKeyUnique)}
                          ,[DateColumn] = '{tailTableConfig.DateColumn}'
                          ,[SelectStartDate] = '{tailTableConfig.SelectStartDate}'
                          ,[HighwaterMarkDate] = '{tailTableConfig.HighwaterMarkDate}'
                          ,[HighwaterMarkID] = {tailTableConfig.HighwaterMarkID}
                          ,[OtherCondition] = '{tailTableConfig.OtherCondition.Replace("'", "''")}'
                          ,[ValidationStatus] = '{tailTableConfig.ValidationStatus}'
                          ,[RowsRemaining] = {tailTableConfig.RowsRemaining}
                          ,[StatsOnly] = {Convert.ToInt32(tailTableConfig.StatsOnly)}
                          ,[SourceInitialRowCount] = {tailTableConfig.SourceInitialRowCount}
                          ,[SourceFinalRowCount] = {tailTableConfig.SourceFinalRowCount}
                          ,[DestinationInitialRowCount] = {tailTableConfig.DestinationInitialRowCount}
                          ,[DestinationFinalRowCount] = {tailTableConfig.DestinationFinalRowCount}
                        WHERE [ProcessID] = {tailTableConfig.ProcessID}
                    "
                };
                Logger.Info(Logging.DbCmdString(cmd));
                rowCount = cmd.ExecuteNonQuery();
                Logger.Info("ROWS: " + rowCount);
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cnArchConfig.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cnArchConfig.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return rowCount;
        }

        public static long LogProcessUpdateDeleteSeconds(SqlConnection cnArchConfig, TailTableWithVerifyConfig tailTableConfig)
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
                        UPDATE [Archiver].[LOG].[Process] 
	                    SET 
                           [DeleteSeconds] = '{tailTableConfig.DeleteSeconds}'
                           ,[RowsDeleted] = '{tailTableConfig.RowsDeleted}'
	                    WHERE [ProcessID] = {tailTableConfig.ProcessID}
                    "
                };
                Logger.Info(Logging.DbCmdString(cmd));
                rowCount = cmd.ExecuteNonQuery();
                Logger.Info("ROWS: " + rowCount);
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cnArchConfig.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cnArchConfig.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return rowCount;
        }

        public static TailTableWithVerifyConfigs GetTailTableWithVerifyConfigs(SqlConnection cnArchConfig, string groupName, string plantCode, string serverName, string databaseName, string schemaName, string tableName, bool useHighwaterMark, bool statsOnly)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Debug($"BEGIN: {DateTime.Now.ToLocalTime()}");
            var testMode = Convert.ToBoolean(ConfigurationManager.AppSettings["TestMode"]);
            var testModeNoDelete = Convert.ToBoolean(ConfigurationManager.AppSettings["TestModeNoDelete"]);
            var configs = new TailTableWithVerifyConfigs();
            var filters = new List<string>();
            var parameters = new List<SqlParameter>();
            try
            {
                if (string.IsNullOrEmpty(tableName))
                {
                    filters.Add("[IsTail] = 1 AND \r\n");
                    filters.Add("[IsVerify] = 1 AND \r\n");
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
                var cmd = GetTailTableWithVerifyConfigs(filters);
                foreach (var parameter in parameters) cmd.Parameters.Add(parameter);
                cmd.Connection = cnArchConfig;
                Logger.Debug(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        var config = new TailTableWithVerifyConfig();
                        config.ConfigID = reader.Get<int>("ConfigID");
                        config.IsArchive = reader.Get<bool>("IsArchive");
                        config.IsTail = reader.Get<bool>("IsTail");
                        config.IsVerify = reader.Get<bool>("IsVerify");
                        config.IsActive = reader.Get<bool>("IsActive");
                        config.IsEnabled = reader.Get<bool>("IsEnabled");
                        config.IsTestMode = reader.Get<bool>("IsTestMode");
                        config.IsHammerMode = reader.Get<bool>("IsHammerMode");
                        config.IsRunning = reader.Get<bool>("IsRunning");
                        config.IsAutoDisable = reader.Get<bool>("IsAutoDisable");
                        config.IsAutoDeleteRowsAdjust = reader.Get<bool>("IsAutoDeleteRowsAdjust");
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
                        config.RunFrequencyDays = reader.Get<int>("RunFrequencyDays");
                        config.NextRunDate = reader.Get<DateTime>("NextRunDate");
                        config.MaxDeleteRowsPerBatch = reader.Get<int>("MaxDeleteRowsPerBatch");
                        config.RetentionDays = reader.Get<int>("RetentionDays");
                        config.RetentionDate = reader.Get<DateTime>("RetentionDate");
                        config.UseStaticRetentionDate = reader.Get<bool>("UseStaticRetentionDate");
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
                        configs.Add(config);
                    }
                    reader.Close();
                }
                Logger.Debug($@"ROWS: {configs.Count}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnArchConfig.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new Exception($"{cnArchConfig.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            finally
            {
                Logger.Debug($"END: {Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds)} SECONDS");
            }
            return configs;
        }

        private static SqlCommand GetTailTableWithVerifyConfigs(List<string> filters)
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
                          ,[IsAutoDisable]
                          ,[IsAutoDeleteRowsAdjust]
                          ,[MaxDeleteRowsPerBatch]
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
                          ,[IDColumn]
                          ,[SourceKey]
                          ,[IsSourceKeyUnique]
                          ,[DestinationKey]
                          ,[IsDestinationKeyUnique]
                          ,[DateColumn]
                          ,[SelectStartDate]
                          ,[HighwaterMarkDate]
                          ,[HighwaterMarkID]
                          ,[OtherCondition]
                          ,[RetentionDays]
,[RetentionDate]
,[UseStaticRetentionDate]
                          ,[RowsRemaining]
                          ,[RunFrequencyDays]
                          ,[NextRunDate]
                          ,[LastRunStatus]
                          ,[LastRunStartDate]
                          ,[LastRunEndDate]
                          ,[RowsDeleted]
                          ,[ProcessID]
                          ,[SourceInitialRowCount]
                          ,[DestinationInitialRowCount]
                          ,[SourceFinalRowCount]
                          ,[DestinationFinalRowCount]
                          ,[SourceMinDate]
                          ,[SourceMaxDate]
                          ,[DestinationRowCount]
                          ,[DestinationMinDate]
                          ,[DestinationMaxDate]
                          ,[ConstraintTypeCode]
                          ,[ConstraintTypeDesc]
                          ,[ConstraintName]
                          ,[ReferencedSchemaName]
                          ,[ReferencedTableName]
                          ,[ReferencedColumnName]
                          ,[CreatedDate]
                          ,[CreatedBy]
                          ,[LastEditBy]
                          ,[BITimeStamp]
                          ,[Notes]
                    FROM [dbo].[{_ConfigTableName}]
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
    }
}
