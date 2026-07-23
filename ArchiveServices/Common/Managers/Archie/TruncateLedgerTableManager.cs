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
    public class TruncateLedgerTableManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private const string _ConfigTableName = "cfgTruncateLedgerTable";

        public static ProcessResponse Process(long batchID, string batchName, TruncateLedgerTableConfig truncateLedgerTableConfig, SqlConnection cnArchConfig, SqlConnection cnSource, bool statsOnly)
        {
            truncateLedgerTableConfig.LastRunStartDate = DateTime.Now.ToLocalTime();
            truncateLedgerTableConfig.LastRunEndDate = truncateLedgerTableConfig.LastRunStartDate;
            var processName = $@"Archive Table: {truncateLedgerTableConfig.ServerQualifiedSourceName}";
            Logger.Debug($"{processName} BEGIN: {DateTime.Now.ToLocalTime()}");
            truncateLedgerTableConfig.BatchID = batchID;
            truncateLedgerTableConfig.ProcessID = LogProcessInsert(cnArchConfig, processName, batchID, truncateLedgerTableConfig);
            truncateLedgerTableConfig.AutoConfigDisable = Convert.ToBoolean(ConfigurationManager.AppSettings["AutoConfigDisable"]);
            truncateLedgerTableConfig.TestModeConfig = Convert.ToBoolean(ConfigurationManager.AppSettings["TestModeConfig"]);
            truncateLedgerTableConfig.TestModeNoDelete = Convert.ToBoolean(ConfigurationManager.AppSettings["TestModeNoDelete"]);
            truncateLedgerTableConfig.StatsOnly = statsOnly;
            if (truncateLedgerTableConfig.TestModeNoDelete) processName = $@"{processName} - Test Mode";
            var nextRuntimeMoveAheadDays = Convert.ToInt32(ConfigurationManager.AppSettings["ArchieNextRuntimeMoveAheadDays"]);
            var maxDeleteSeconds = Convert.ToDecimal(ConfigurationManager.AppSettings["ArchieMaxDeleteSeconds"]);
            long maxDeleteRows = Convert.ToInt64(ConfigurationManager.AppSettings["ArchieMaxDeleteRows"]);
            LogProcessUpdate(cnArchConfig, truncateLedgerTableConfig);
            var response = new ProcessResponse($@"BatchID [{batchID}] {batchName} ProcessID [{truncateLedgerTableConfig.ProcessID}] {processName}", truncateLedgerTableConfig, true);
            response.BatchID = batchID;
            response.ProcessID = truncateLedgerTableConfig.ProcessID;
            response.ArchiveDate = truncateLedgerTableConfig.LastRunStartDate;
            response.ConnectionStrings.Add(new ConnectionString(cnArchConfig, "cnArchConfig", cnArchConfig.ConnectionString, truncateLedgerTableConfig.BatchID, truncateLedgerTableConfig.ProcessID));
            response.ConnectionStrings.Add(new ConnectionString("smtp", ConfigurationManager.AppSettings["SmtpHost"]));
            try
            {
                var stepValidateTable = new ProcessStep(cnArchConfig, truncateLedgerTableConfig.ProcessID, $@"Validate configuration in [Archiver].[dbo].[{_ConfigTableName}] for {truncateLedgerTableConfig.ServerQualifiedSourceName}");
                try
                {
                    truncateLedgerTableConfig.ValidationErrors = ValidateConfig(stepValidateTable.StepID, truncateLedgerTableConfig, cnArchConfig, cnSource);
                    stepValidateTable.RowsAffected = 1;
                }
                catch (Exception e)
                {
                    stepValidateTable.Errors.Add(new Error(e));
                    Logger.Error($"ERROR: {e.Message}: {e.StackTrace}");
                }
                stepValidateTable.Done(cnArchConfig);
                response.ProcessSteps.Add(stepValidateTable);

                if (truncateLedgerTableConfig.ValidationErrors.Any())
                {
                    truncateLedgerTableConfig.LastRunStatus = "Error";
                    LogProcessUpdate(cnArchConfig, truncateLedgerTableConfig);
                    foreach (var validationError in truncateLedgerTableConfig.ValidationErrors)
                    {
                        if (!response.Errors.Contains(new Error(validationError.Message))) response.Errors.Add(new Error(validationError.Message));
                    }
                    if (truncateLedgerTableConfig.AutoConfigDisable && truncateLedgerTableConfig.StatsOnly == false)
                    {
                        var stepUpdatetruncateLedgerTableConfigDisable = new ProcessStep(cnArchConfig, response.ProcessID, $@"Set [Archiver].[dbo].[{_ConfigTableName}].[IsEnabled] = FALSE for {truncateLedgerTableConfig.ServerQualifiedSourceName}");
                        try
                        {
                            stepUpdatetruncateLedgerTableConfigDisable.RowsAffected = UpdateTruncateLedgerTableConfigIsEnabled(cnArchConfig, truncateLedgerTableConfig, false);
                        }
                        catch (Exception e)
                        {
                            stepUpdatetruncateLedgerTableConfigDisable.Errors.Add(new Error(e));
                            Logger.Error($"ERROR: {e.Message}: {e.StackTrace}");
                        }
                        stepUpdatetruncateLedgerTableConfigDisable.Done(cnArchConfig);
                        response.ProcessSteps.Add(stepUpdatetruncateLedgerTableConfigDisable);
                    }
                }

                if (!response.Errors.Any())
                {
                    var stepGetSourceSqlConnection = new ProcessStep(cnArchConfig, truncateLedgerTableConfig.ProcessID, $@"Get SQL connection for Source table {truncateLedgerTableConfig.ServerQualifiedSourceName}");
                    try
                    {
                        // Get Source SQL connection
                        cnSource = GetSourceSqlConnection(truncateLedgerTableConfig.SourceServerName, truncateLedgerTableConfig.SourceDatabaseName);
                        response.ConnectionStrings.Add(new ConnectionString(cnArchConfig, "cnSource", cnSource.ConnectionString, truncateLedgerTableConfig.BatchID, truncateLedgerTableConfig.ProcessID));
                        if (cnSource.State == ConnectionState.Open)
                        {
                            stepGetSourceSqlConnection.RowsAffected = 1;
                        }
                        else
                        {
                            var validationError = new ValidationError($@"ERROR: Source server [{truncateLedgerTableConfig.SourceServerName}].[{truncateLedgerTableConfig.SourceDatabaseName}] ConnectionState is not Open.", "Validation", "Fatal", true);
                            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepGetSourceSqlConnection.StepID, validationError.Message, "");
                            truncateLedgerTableConfig.ValidationErrors.Add(validationError);
                            truncateLedgerTableConfig.ValidationStatus = validationError.Message;
                            truncateLedgerTableConfig.LastRunStatus = "Error";
                            stepGetSourceSqlConnection.Errors.Add(new Error(validationError.Message));
                            if (!response.Errors.Contains(new Error(validationError.Message))) response.Errors.Add(new Error(validationError.Message));
                            Logger.Error(validationError.Message);
                        }
                    }
                    catch (Exception e)
                    {
                        stepGetSourceSqlConnection.Errors.Add(new Error(e));
                        Logger.Error($"ERROR: {e.Message}: {e.StackTrace}");
                    }
                    stepGetSourceSqlConnection.Done(cnArchConfig);
                    response.ProcessSteps.Add(stepGetSourceSqlConnection);
                }

                if (!response.Errors.Any())
                {
                    var stepCheckSourceDatabaseExists = new ProcessStep(cnArchConfig, truncateLedgerTableConfig.ProcessID, $@"Check that Source database [{truncateLedgerTableConfig.SourceServerName}].[{truncateLedgerTableConfig.SourceDatabaseName}] exists");
                    try
                    {
                        // See if Source database exists
                        if (CheckSourceDatabaseExists(cnSource, truncateLedgerTableConfig))
                        {
                            stepCheckSourceDatabaseExists.RowsAffected = 1;
                        }
                        else
                        {
                            var validationError = new ValidationError($@"ERROR: {cnSource.ConnectionString} Source database [{truncateLedgerTableConfig.SourceServerName}].[{truncateLedgerTableConfig.SourceDatabaseName}] not found.", "Validation", "Fatal", true);
                            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepCheckSourceDatabaseExists.StepID, validationError.Message, "");
                            truncateLedgerTableConfig.ValidationErrors.Add(validationError);
                            truncateLedgerTableConfig.ValidationStatus = validationError.Message;
                            truncateLedgerTableConfig.LastRunStatus = "Error";
                            stepCheckSourceDatabaseExists.Errors.Add(new Error(validationError.Message));
                            if (!response.Errors.Contains(new Error(validationError.Message))) response.Errors.Add(new Error(validationError.Message));
                            Logger.Error(validationError.Message);
                        }
                    }
                    catch (Exception e)
                    {
                        stepCheckSourceDatabaseExists.Errors.Add(new Error(e));
                        Logger.Error($"ERROR: {e.Message}: {e.StackTrace}");
                    }
                    stepCheckSourceDatabaseExists.Done(cnArchConfig);
                    response.ProcessSteps.Add(stepCheckSourceDatabaseExists);

                    var stepCheckSourceSchemaExists = new ProcessStep(cnArchConfig, truncateLedgerTableConfig.ProcessID, $@"Check that Source schema [{truncateLedgerTableConfig.SourceServerName}].[{truncateLedgerTableConfig.SourceDatabaseName}].[{truncateLedgerTableConfig.SourceSchemaName}] exists");
                    try
                    {
                        // See if Source schema exists
                        if (CheckSourceSchemaExists(cnSource, truncateLedgerTableConfig))
                        {
                            stepCheckSourceSchemaExists.RowsAffected = 1;
                        }
                        else
                        {
                            var validationError = new ValidationError($@"ERROR: {cnSource.ConnectionString} Source schema [{truncateLedgerTableConfig.SourceServerName}].[{truncateLedgerTableConfig.SourceDatabaseName}].[{truncateLedgerTableConfig.SourceSchemaName}] not found.", "Validation", "Fatal", true);
                            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepCheckSourceSchemaExists.StepID, validationError.Message, "");
                            truncateLedgerTableConfig.ValidationErrors.Add(validationError);
                            truncateLedgerTableConfig.ValidationStatus = validationError.Message;
                            truncateLedgerTableConfig.LastRunStatus = "Error";
                            stepCheckSourceSchemaExists.Errors.Add(new Error(validationError.Message));
                            if (!response.Errors.Contains(new Error(validationError.Message))) response.Errors.Add(new Error(validationError.Message));
                            Logger.Error(validationError.Message);
                        }
                    }
                    catch (Exception e)
                    {
                        stepCheckSourceSchemaExists.Errors.Add(new Error(e));
                        Logger.Error($"ERROR: {e.Message}: {e.StackTrace}");
                    }
                    stepCheckSourceSchemaExists.Done(cnArchConfig);
                    response.ProcessSteps.Add(stepCheckSourceSchemaExists);

                    var stepCheckSourceTableExists = new ProcessStep(cnArchConfig, truncateLedgerTableConfig.ProcessID, $@"Check that Source Table  {truncateLedgerTableConfig.ServerQualifiedSourceName} exists");
                    try
                    {
                        // See if Source table exists
                        if (CheckSourceTableExists(cnSource, truncateLedgerTableConfig))
                        {
                            stepCheckSourceTableExists.RowsAffected = 1;
                        }
                        else
                        {
                            var validationError = new ValidationError($@"ERROR: {cnSource.ConnectionString} Source table {truncateLedgerTableConfig.ServerQualifiedSourceName} not found.", "Validation", "Fatal", true);
                            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepCheckSourceTableExists.StepID, validationError.Message, "");
                            truncateLedgerTableConfig.ValidationErrors.Add(validationError);
                            truncateLedgerTableConfig.ValidationStatus = validationError.Message;
                            truncateLedgerTableConfig.LastRunStatus = "Error";
                            stepCheckSourceTableExists.Errors.Add(new Error(validationError.Message));
                            if (!response.Errors.Contains(new Error(validationError.Message))) response.Errors.Add(new Error(validationError.Message));
                            Logger.Error(validationError.Message);
                        }
                    }
                    catch (Exception e)
                    {
                        stepCheckSourceTableExists.Errors.Add(new Error(e));
                        Logger.Error($"ERROR: {e.Message}: {e.StackTrace}");
                    }
                    stepCheckSourceTableExists.Done(cnArchConfig);
                    response.ProcessSteps.Add(stepCheckSourceTableExists);
                }

                if (!response.Errors.Any())
                {
                    var stepUpdatetruncateLedgerTableConfigIsRunningTrue = new ProcessStep(cnArchConfig, truncateLedgerTableConfig.ProcessID, $@"Set [Archiver].[dbo].[{_ConfigTableName}].[IsRunning] = TRUE for {truncateLedgerTableConfig.ServerQualifiedSourceName}");
                    try
                    {
                        truncateLedgerTableConfig.IsRunning = true;
                        UpdateTruncateLedgerTableConfigIsRunning(cnArchConfig, truncateLedgerTableConfig);
                        LogProcessUpdate(cnArchConfig, truncateLedgerTableConfig);
                        stepUpdatetruncateLedgerTableConfigIsRunningTrue.RowsAffected = 1;
                    }
                    catch (Exception e)
                    {
                        stepUpdatetruncateLedgerTableConfigIsRunningTrue.Errors.Add(new Error(e));
                        Logger.Error($"ERROR: {e.Message}: {e.StackTrace}");
                    }
                    stepUpdatetruncateLedgerTableConfigIsRunningTrue.Done(cnArchConfig);
                    response.ProcessSteps.Add(stepUpdatetruncateLedgerTableConfigIsRunningTrue);
                }

                if (!response.Errors.Any())
                {
                    var stepGatherInitialStatistics = new ProcessStep(cnArchConfig, truncateLedgerTableConfig.ProcessID, $@"Gather initial statistics for {truncateLedgerTableConfig.ServerQualifiedSourceName}");
                    try
                    {
                        truncateLedgerTableConfig.SourceInitialRowCount = GetRowCountSource(cnSource, truncateLedgerTableConfig);
                        truncateLedgerTableConfig.SelectedRows = truncateLedgerTableConfig.SourceInitialRowCount;
                        truncateLedgerTableConfig.LastRunStatus = "";
                        truncateLedgerTableConfig.SourceMinDate = GetMinSourceDate(cnSource, truncateLedgerTableConfig);
                        truncateLedgerTableConfig.SourceMaxDate = GetMaxSourceDate(cnSource, truncateLedgerTableConfig);
                        truncateLedgerTableConfig.SourceMaxDateString = truncateLedgerTableConfig.SourceMaxDate.ToString("M/dd/yyyy hh:mm:ss tt", CultureInfo.CreateSpecificCulture("en-US"));
                        UpdateTruncateLedgerTableConfigInitialRowCount(cnArchConfig, truncateLedgerTableConfig);
                        stepGatherInitialStatistics.RowsAffected = truncateLedgerTableConfig.SourceInitialRowCount > 0 ? truncateLedgerTableConfig.SourceInitialRowCount : 1;
                    }
                    catch (Exception e)
                    {
                        stepGatherInitialStatistics.Errors.Add(new Error(e));
                        Logger.Error($"ERROR: {e.Message}: {e.StackTrace}");
                    }
                    stepGatherInitialStatistics.Done(cnArchConfig);
                    response.ProcessSteps.Add(stepGatherInitialStatistics);
                }

                //--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

                if (!response.Errors.Any() && truncateLedgerTableConfig.StatsOnly == false)
                {
                    if (!response.Errors.Any())
                    {
                        var stepDeleteSourceData = new ProcessStep(cnArchConfig, truncateLedgerTableConfig.ProcessID, $@"Truncate {truncateLedgerTableConfig.SelectedRows} rows from {truncateLedgerTableConfig.ServerQualifiedSourceName}");
                        var cmdStr = $@"
                                                        TRUNCATE TABLE {truncateLedgerTableConfig.FullyQualifiedSourceName}
                                 ";
                        var cmdTruncate = new SqlCommand
                        {
                            Connection = cnSource,
                            CommandType = CommandType.Text,
                            CommandTimeout = 0,
                            CommandText = cmdStr
                        };
                        Logger.Debug(Logging.DbCmdString(cmdTruncate));
                        stepDeleteSourceData.Statement = cmdTruncate.CommandText;
                        truncateLedgerTableConfig.TruncateCommand = cmdTruncate.CommandText;
                        //===============================================================================================
                        try
                        {
                            if (!truncateLedgerTableConfig.TestModeNoDelete && !truncateLedgerTableConfig.IsTestMode)
                            {
                                truncateLedgerTableConfig.TruncateStartTime = DateTime.Now;
                                truncateLedgerTableConfig.RowsTruncated = cmdTruncate.ExecuteNonQuery();
                                if (truncateLedgerTableConfig.RowsTruncated == -1) truncateLedgerTableConfig.RowsTruncated = truncateLedgerTableConfig.SelectedRows;
                                stepDeleteSourceData.RowsAffected = truncateLedgerTableConfig.RowsTruncated;
                                truncateLedgerTableConfig.TruncateEndTime = DateTime.Now;
                                Logger.Debug($@"Truncated ROWS: {truncateLedgerTableConfig.RowsTruncated}");
                                Logger.Debug($@"TruncateSeconds: {truncateLedgerTableConfig.TruncateSeconds}");
                                UpdateTruncateLedgerTableConfigDeletedRows(cnArchConfig, truncateLedgerTableConfig);
                            }
                            //===============================================================================================
                            LogProcessUpdateDeleteSeconds(cnArchConfig, truncateLedgerTableConfig);
                            stepDeleteSourceData.RowsAffected = truncateLedgerTableConfig.RowsTruncated;
                            truncateLedgerTableConfig.TruncateSeconds = stepDeleteSourceData.ElapsedSeconds;
                        }
                        catch (Exception e)
                        {
                            stepDeleteSourceData.Errors.Add(new Error(e));
                            Logger.Error("ERROR: " + cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                            //throw new ApplicationException(cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                        }
                        stepDeleteSourceData.Done(cnArchConfig);
                        response.ProcessSteps.Add(stepDeleteSourceData);
                    }
                    //Logger.Debug("SLEEP 2 SECONDS...");
                    //Thread.Sleep(2000);
                }

                //--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

                if (!truncateLedgerTableConfig.ValidationErrors.Any() && !response.Errors.Any())
                {
                    var stepGatherFinalStatistics = new ProcessStep(cnArchConfig, truncateLedgerTableConfig.ProcessID, $@"Gather final statistics for {truncateLedgerTableConfig.ServerQualifiedSourceName}");
                    try
                    {
                        truncateLedgerTableConfig.RowsRemaining = GetRowsRemaining(cnSource, truncateLedgerTableConfig);
                        truncateLedgerTableConfig.SourceFinalRowCount = GetRowCountSource(cnSource, truncateLedgerTableConfig);
                        if (statsOnly == true) truncateLedgerTableConfig.LastRunStatus = "Success";
                        var stepUpdateHighWaterMark = new ProcessStep(cnArchConfig, truncateLedgerTableConfig.ProcessID, $@"Update HighWaterMark for {truncateLedgerTableConfig.ServerQualifiedSourceName}");
                        try
                        {
                            UpdateTruncateLedgerTableConfigHighwaterMarks(cnArchConfig, truncateLedgerTableConfig);
                            stepUpdateHighWaterMark.RowsAffected = 1;
                        }
                        catch (Exception e)
                        {
                            stepUpdateHighWaterMark.Errors.Add(new Error(e));
                            Logger.Error("ERROR: " + cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                            //throw new ApplicationException(cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                        }
                        stepUpdateHighWaterMark.Done(cnArchConfig);
                        response.ProcessSteps.Add(stepUpdateHighWaterMark);
                        response.RowsAffected = truncateLedgerTableConfig.RowsAffected;
                        response.SourceRowCount = truncateLedgerTableConfig.SourceRowCount;
                        response.SFMinDate = truncateLedgerTableConfig.SourceMinDate;
                        response.SFDays = truncateLedgerTableConfig.SourceDays;
                        response.SourceMaxDate = truncateLedgerTableConfig.SourceMaxDate;
                        stepGatherFinalStatistics.RowsAffected = 1;
                    }
                    catch (Exception e)
                    {
                        stepGatherFinalStatistics.Errors.Add(new Error(e));
                        Logger.Error("ERROR: " + cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                        throw new ApplicationException(cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
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
                truncateLedgerTableConfig.IsRunning = false;
                truncateLedgerTableConfig.LastRunEndDate = DateTime.Now;
                var stepUpdatetruncateLedgerTableConfigDone = new ProcessStep(cnArchConfig, truncateLedgerTableConfig.ProcessID, $@"Set [Archiver].[dbo].[{_ConfigTableName}] DONE for {truncateLedgerTableConfig.ServerQualifiedSourceName}");
                UpdateTruncateLedgerTableConfigIsRunning(cnArchConfig, truncateLedgerTableConfig);
                LogProcessUpdate(cnArchConfig, truncateLedgerTableConfig);
                stepUpdatetruncateLedgerTableConfigDone.RowsAffected = UpdateTruncateLedgerTableConfig(cnArchConfig, truncateLedgerTableConfig);
                stepUpdatetruncateLedgerTableConfigDone.Done(cnArchConfig);
                Logger.Debug("END: " + Convert.ToDecimal((truncateLedgerTableConfig.LastRunEndDate - truncateLedgerTableConfig.LastRunStartDate).TotalSeconds) + " SECONDS");
            }
            return response;
        }

        private static ValidationErrors ValidateConfig(int stepID, TruncateLedgerTableConfig truncateLedgerTableConfig, SqlConnection cnArchConfig, SqlConnection cnSource)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Debug($"BEGIN: {startTime}");
            var validationErrors = new ValidationErrors();
            try
            {
                if (string.IsNullOrEmpty(truncateLedgerTableConfig.SourceTableName))
                {
                    var validationError = new ValidationError($"{truncateLedgerTableConfig.ServerQualifiedSourceName} is not found in [Archiver].[dbo].[{_ConfigTableName}].", "Validation", "Fatal", true);
                    validationErrors.Add(validationError);
                    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                    truncateLedgerTableConfig.ValidationStatus = validationError.Message;
                    Logger.Error(validationError.Message);
                }
                else if (!truncateLedgerTableConfig.SourceTableName.ToUpper().Contains("LEDGER"))
                {
                    var validationError = new ValidationError($"{truncateLedgerTableConfig.ServerQualifiedSourceName} is not a Ledger.", "Validation", "Fatal", true);
                    validationErrors.Add(validationError);
                    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                    truncateLedgerTableConfig.ValidationStatus = validationError.Message;
                    Logger.Error(validationError.Message);
                }
                else
                {
                    if (truncateLedgerTableConfig.IsRunning)
                    {
                        var validationError = new ValidationError($"Archive of {truncateLedgerTableConfig.ServerQualifiedSourceName} is already Running.", "Validation", "Fatal", true);
                        validationErrors.Add(validationError);
                        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                        truncateLedgerTableConfig.ValidationStatus = validationError.Message;
                    }
                    if (truncateLedgerTableConfig.IsTruncate == false)
                    {
                        var validationError = new ValidationError($"{truncateLedgerTableConfig.ServerQualifiedSourceName} is not configured for Truncate.", "Validation", "Fatal", true);
                        validationErrors.Add(validationError);
                        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                        truncateLedgerTableConfig.ValidationStatus = validationError.Message;
                        Logger.Error(validationError.Message);
                    }
                    if (truncateLedgerTableConfig.IsActive == false)
                    {
                        var validationError = new ValidationError($"{truncateLedgerTableConfig.ServerQualifiedSourceName} is not Active.", "Validation", "Fatal", true);
                        validationErrors.Add(validationError);
                        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                        truncateLedgerTableConfig.ValidationStatus = validationError.Message;
                        Logger.Error(validationError.Message);
                    }
                    if (truncateLedgerTableConfig.IsEnabled == false && truncateLedgerTableConfig.StatsOnly == false)
                    {
                        var validationError = new ValidationError($"{truncateLedgerTableConfig.ServerQualifiedSourceName} is not Enabled.", "Validation", "Fatal", true);
                        validationErrors.Add(validationError);
                        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                        truncateLedgerTableConfig.ValidationStatus = validationError.Message;
                        Logger.Error(validationError.Message);
                    }
                    if (truncateLedgerTableConfig.TestModeConfig == true && truncateLedgerTableConfig.IsTestMode == false && truncateLedgerTableConfig.StatsOnly == false)
                    {
                        var validationError = new ValidationError($"{truncateLedgerTableConfig.ServerQualifiedSourceName} is not configured for Test Mode but web.config is in Test Mode.", "Validation", "Fatal", true);
                        validationErrors.Add(validationError);
                        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                        truncateLedgerTableConfig.ValidationStatus = validationError.Message;
                        Logger.Error(validationError.Message);
                    }
                }
                //if (!truncateLedgerTableConfig.IsTestMode)
                //{
                //    if (!truncateLedgerTableConfig.IsActive)
                //    {
                //        var validationError = new ValidationError($"{truncateLedgerTableConfig.ServerQualifiedDestinationName} is Inactive.", "Validation", "Fatal", true);
                //        validationErrors.Add(validationError);
                //        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                //        truncateLedgerTableConfig.ValidationStatus = validationError.Message;
                //        Logger.Error(validationError.Message);
                //    }
                //    if (!truncateLedgerTableConfig.IsEnabled)
                //    {
                //        var validationError = new ValidationError($"{truncateLedgerTableConfig.ServerQualifiedDestinationName} is Disabled.", "Validation", "Fatal", true);
                //        validationErrors.Add(validationError);
                //        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                //        truncateLedgerTableConfig.ValidationStatus = validationError.Message;
                //        Logger.Error(validationError.Message);
                //    }
                //}
            }
            catch (Exception e)
            {
                Logger.Error($@"ERROR: {cnSource.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new ApplicationException($@"{cnSource.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            finally
            {
                if (!validationErrors.Any()) truncateLedgerTableConfig.ValidationStatus = "Valid";
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

        private static bool CheckSourceDatabaseExists(SqlConnection cnSource, TruncateLedgerTableConfig truncateLedgerTableConfig)
        {
            bool retVal = false;
            try
            {
                string str = string.Format($@"SELECT 1 FROM sys.databases WHERE Name = '{truncateLedgerTableConfig.SourceDatabaseName}'");
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

        private static bool CheckSourceSchemaExists(SqlConnection cnSource, TruncateLedgerTableConfig truncateLedgerTableConfig)
        {
            bool retVal = false;
            try
            {
                string str = string.Format($@"SELECT 1 FROM sys.schemas WHERE name = '{truncateLedgerTableConfig.SourceSchemaName}'");
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

        private static bool CheckSourceTableExists(SqlConnection cnSource, TruncateLedgerTableConfig truncateLedgerTableConfig)
        {
            bool retVal = false;
            try
            {
                string str = string.Format($@"
                    SELECT 1 
                    FROM {truncateLedgerTableConfig.SourceDatabaseName}.sys.tables t
                    INNER JOIN {truncateLedgerTableConfig.SourceDatabaseName}.sys.schemas s ON s.schema_id = t.schema_id
                    WHERE s.name = '{truncateLedgerTableConfig.SourceSchemaName}'
                    AND t.name = '{truncateLedgerTableConfig.SourceTableName}'
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

        private static long UpdateTruncateLedgerTableConfigIsRunning(SqlConnection cnArchConfig, TruncateLedgerTableConfig truncateLedgerTableConfig)
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
                            [IsRunning] = {Convert.ToInt32(truncateLedgerTableConfig.IsRunning)}
                            ,[ProcessID] = {truncateLedgerTableConfig.ProcessID}
                            ,[LastRunStatus] = '{truncateLedgerTableConfig.LastRunStatus}'
                            ,[LastRunStartDate] = '{truncateLedgerTableConfig.LastRunStartDate}'
                            ,[LastRunEndDate] = '{truncateLedgerTableConfig.LastRunEndDate}'
                           ,[BITimeStamp] = GETDATE()
                        FROM [dbo].[{_ConfigTableName}] xx
                        WHERE [ConfigID] = {truncateLedgerTableConfig.ConfigID}
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

        private static long GetRowCountSource(SqlConnection cnSource, TruncateLedgerTableConfig truncateLedgerTableConfig, string whereClause = null)
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
                                                part.rows as SourceInitialRowCount
                                            from 
                                                {truncateLedgerTableConfig.SourceDatabaseName}.sys.tables tbl
                                                INNER JOIN {truncateLedgerTableConfig.SourceDatabaseName}.sys.schemas sch ON sch.schema_id = tbl.schema_id
                                                INNER JOIN {truncateLedgerTableConfig.SourceDatabaseName}.sys.partitions part ON part.object_id = tbl.object_id
                                            WHERE 
                                                part.index_id IN (0, 1) -- 0 for heaps, 1 for clustered indexes (i.e., the table itself)
                                                and sch.name = '{truncateLedgerTableConfig.SourceSchemaName}' 
                                                and tbl.name = '{truncateLedgerTableConfig.SourceTableName}'
                    "
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

        private static long GetRowsRemaining(SqlConnection cnDestination, TruncateLedgerTableConfig truncateLedgerTableConfig)
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
                    CommandText = BuildRowsRemainingString(truncateLedgerTableConfig)
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

        private static String BuildRowsRemainingString(TruncateLedgerTableConfig truncateLedgerTableConfig)
        {
            string str;
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($@"SELECT COUNT(*) ");
                sb.Append(Environment.NewLine);
                sb.Append($@"FROM {truncateLedgerTableConfig.FullyQualifiedSourceName} WITH (NOLOCK) ");
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

        private static DateTime GetMinSourceDate(SqlConnection cnDestination, TruncateLedgerTableConfig truncateLedgerTableConfig)
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
                        SELECT ISNULL(MIN([{truncateLedgerTableConfig.DateColumn}]), '1900-01-01')  
                        FROM {truncateLedgerTableConfig.FullyQualifiedSourceName} WITH (NOLOCK) 
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

        private static DateTime GetMaxSourceDate(SqlConnection cnSource, TruncateLedgerTableConfig truncateLedgerTableConfig)
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
                        SELECT ISNULL(MAX([{truncateLedgerTableConfig.DateColumn}]), '1900-01-01')  
                        FROM {truncateLedgerTableConfig.FullyQualifiedSourceName} WITH (NOLOCK) 
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

        private static long UpdateTruncateLedgerTableConfigHighwaterMarks(SqlConnection cnArchConfig, TruncateLedgerTableConfig truncateLedgerTableConfig)
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
                        [SourceInitialRowCount] = {truncateLedgerTableConfig.SourceInitialRowCount}
                        ,[SourceFinalRowCount] = {truncateLedgerTableConfig.SourceFinalRowCount}
                        ,[RowsTruncated] = {truncateLedgerTableConfig.RowsTruncated}
                        ,[BITimeStamp] = GETDATE()
                    FROM [dbo].[{_ConfigTableName}] xx
                    WHERE [ConfigID] = {truncateLedgerTableConfig.ConfigID}
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

        private static long UpdateTruncateLedgerTableConfigDeletedRows(SqlConnection cnArchConfig, TruncateLedgerTableConfig truncateLedgerTableConfig)
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
                        [RowsTruncated] = {truncateLedgerTableConfig.RowsTruncated}
                        ,[TruncateSeconds] = {truncateLedgerTableConfig.TruncateSeconds}
                        ,[BITimeStamp] = GETDATE()
                    FROM [dbo].[{_ConfigTableName}] xx
                    WHERE [ConfigID] = {truncateLedgerTableConfig.ConfigID}
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

        private static long UpdateTruncateLedgerTableConfig(SqlConnection cnArchConfig, TruncateLedgerTableConfig truncateLedgerTableConfig)
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
                            [IsRunning] = {Convert.ToInt32(truncateLedgerTableConfig.IsRunning)}
                            ,[LastRunStatus] = '{truncateLedgerTableConfig.LastRunStatus}'
                            ,[SourceInitialRowCount] = {truncateLedgerTableConfig.SourceInitialRowCount}
                            ,[SourceFinalRowCount] = {truncateLedgerTableConfig.SourceFinalRowCount}
                            ,[BITimeStamp] = GETDATE()
                        FROM [dbo].[{_ConfigTableName}] xx
                        WHERE [ConfigID] = {truncateLedgerTableConfig.ConfigID}
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

        private static long UpdateTruncateLedgerTableConfigIsEnabled(SqlConnection cnArchConfig, TruncateLedgerTableConfig truncateLedgerTableConfig, bool isEnabled)
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
                            ,[AutoConfigDisable] = {(truncateLedgerTableConfig.AutoConfigDisable == true ? 1 : 0)}
                            ,[BITimeStamp] = GETDATE()
                        FROM [dbo].[{_ConfigTableName}] xx
                        WHERE [ConfigID] = {truncateLedgerTableConfig.ConfigID}
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

        private static long UpdateTruncateLedgerTableConfigInitialRowCount(SqlConnection cnArchConfig, TruncateLedgerTableConfig truncateLedgerTableConfig)
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
                        [SourceInitialRowCount] = CASE WHEN [SourceInitialRowCount] = 0 THEN {truncateLedgerTableConfig.SourceInitialRowCount} ELSE [SourceInitialRowCount] END
                        ,[SourceMinDate] = '{truncateLedgerTableConfig.SourceMinDate}'
                        ,[SourceMaxDate] = '{truncateLedgerTableConfig.SourceMaxDate}'
                        ,[BITimeStamp] = GETDATE()
                    FROM [dbo].[{_ConfigTableName}] xx
                    WHERE [ConfigID] = {truncateLedgerTableConfig.ConfigID}
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

        public static int LogProcessInsert(SqlConnection cnArchConfig, string processName, long batchID, TruncateLedgerTableConfig truncateLedgerTableConfig)
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
                           ,[TestModeNoDelete]
                        )
	                    VALUES 
	                    (
                            '{processName}'
                           ,{batchID}
                           ,'{DateTime.Now}'
                           ,{truncateLedgerTableConfig.ConfigID}
                           ,'{truncateLedgerTableConfig.SourceServerName}'
                           ,'{truncateLedgerTableConfig.SourceDatabaseName}'
                           ,'{truncateLedgerTableConfig.SourceSchemaName}'
                           ,'{truncateLedgerTableConfig.SourceTableName}'
                           ,'{truncateLedgerTableConfig.TestModeNoDelete}'
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

        public static long LogProcessUpdate(SqlConnection cnArchConfig, TruncateLedgerTableConfig truncateLedgerTableConfig)
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
                        SET [EndDate] = '{truncateLedgerTableConfig.LastRunEndDate}'
                          ,[LastRunStatus] = '{truncateLedgerTableConfig.LastRunStatus}'
                          ,[ConfigID] = {truncateLedgerTableConfig.ConfigID}
                          ,[AutoConfigDisable] = {Convert.ToInt32(truncateLedgerTableConfig.AutoConfigDisable)}
                          ,[IsTruncate] = {Convert.ToInt32(truncateLedgerTableConfig.IsTruncate)}
                          ,[IsActive] = {Convert.ToInt32(truncateLedgerTableConfig.IsActive)}
                          ,[IsEnabled] = {Convert.ToInt32(truncateLedgerTableConfig.IsEnabled)}
                          ,[IsTestMode] = {Convert.ToInt32(truncateLedgerTableConfig.IsTestMode)}
                          ,[TestModeNoDelete] = {Convert.ToInt32(truncateLedgerTableConfig.TestModeNoDelete)}
                          ,[SourceServerName] = '{truncateLedgerTableConfig.SourceServerName}'
                          ,[SourceDatabaseName] = '{truncateLedgerTableConfig.SourceDatabaseName}'
                          ,[SourceSchemaName] = '{truncateLedgerTableConfig.SourceSchemaName}'
                          ,[SourceTableName] = '{truncateLedgerTableConfig.SourceTableName}'
                          ,[ValidationStatus] = '{truncateLedgerTableConfig.ValidationStatus}'
                          ,[RowsRemaining] = {truncateLedgerTableConfig.RowsRemaining}
                          ,[StatsOnly] = {Convert.ToInt32(truncateLedgerTableConfig.StatsOnly)}
                          ,[SourceInitialRowCount] = {truncateLedgerTableConfig.SourceInitialRowCount}
                          ,[SourceFinalRowCount] = {truncateLedgerTableConfig.SourceFinalRowCount}
                        WHERE [ProcessID] = {truncateLedgerTableConfig.ProcessID}
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

        public static long LogProcessUpdateDeleteSeconds(SqlConnection cnArchConfig, TruncateLedgerTableConfig tailTableConfig)
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
                           [DeleteSeconds] = '{tailTableConfig.TruncateSeconds}'
                           ,[RowsDeleted] = '{tailTableConfig.RowsTruncated}'
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

        public static long LogProcessUpdateDone(SqlConnection cnArchConfig, int processID, TruncateLedgerTableConfig tailTableConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            long rowCount;
            try
            {
                DateTime dt1 = DateTime.Parse("01/01/1900");
                if (tailTableConfig.SourceMinDate < dt1) tailTableConfig.SourceMinDate = dt1;
                if (tailTableConfig.SourceMaxDate < dt1) tailTableConfig.SourceMaxDate = dt1;
                var cmd = new SqlCommand
                {
                    Connection = cnArchConfig,
                    CommandType = CommandType.Text,
                    CommandText = $@"
                        UPDATE [Archiver].[LOG].[Process] 
	                    SET 
                            [EndDate] = GETDATE()
                            ,[LastRunStatus] = '{tailTableConfig.LastRunStatus}'
                            ,[RowsDeleted] = {tailTableConfig.RowsTruncated}
                            ,[FinalRowCountCalculated] = {tailTableConfig.SourceFinalRowCount}
                            ,[FinalRowCount] = {tailTableConfig.SourceFinalRowCount}
                            ,[SFMinDate] = '{tailTableConfig.SourceMinDate}'
                            ,[SFMaxDate] = '{tailTableConfig.SourceMaxDate}'
                            ,[ArchiveStartTime] = '{tailTableConfig.LastRunStartDate}'        
	                    WHERE [ProcessID] = {processID}
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

        public static long LogProcessUpdateInitialRowCount(SqlConnection cnArchConfig, int processID, TruncateLedgerTableConfig tailTableConfig)
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
                            [ValidationStatus] = '{tailTableConfig.ValidationStatus}'
                           ,[InitialRowCount] = {tailTableConfig.SourceInitialRowCount} 
                           ,[RowsToDelete] = {tailTableConfig.SourceInitialRowCount}
	                    WHERE [ProcessID] = {processID}
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

        public static TruncateLedgerTableConfigs GetTruncateLedgerTableConfigs(SqlConnection cnArchConfig, string groupName, string plantCode, string serverName, string databaseName, string schemaName, string tableName, bool useHighwaterMark, bool statsOnly)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Debug($"BEGIN: {DateTime.Now.ToLocalTime()}");
            var testMode = Convert.ToBoolean(ConfigurationManager.AppSettings["TestMode"]);
            var testModeNoDelete = Convert.ToBoolean(ConfigurationManager.AppSettings["TestModeNoDelete"]);
            var configs = new TruncateLedgerTableConfigs();
            var filters = new List<string>();
            var parameters = new List<SqlParameter>();
            try
            {
                if (string.IsNullOrEmpty(tableName))
                {
                    filters.Add("[IsTruncate] = 1 AND \r\n");
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
                var cmd = GetTruncateLedgerTableConfigs(filters);
                foreach (var parameter in parameters) cmd.Parameters.Add(parameter);
                cmd.Connection = cnArchConfig;
                Logger.Debug(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        var config = new TruncateLedgerTableConfig();
                        config.ConfigID = reader.Get<int>("ConfigID");
                        config.IsTruncate = reader.Get<bool>("IsTruncate");
                        config.IsActive = reader.Get<bool>("IsActive");
                        config.IsEnabled = reader.Get<bool>("IsEnabled");
                        config.IsTestMode = reader.Get<bool>("IsTestMode");
                        config.IsRunning = reader.Get<bool>("IsRunning");
                        config.IsAutoDisable = reader.Get<bool>("IsAutoDisable");
                        config.SourceServerName = reader.Get<string>("SourceServerName");
                        config.SourceDatabaseName = reader.Get<string>("SourceDatabaseName");
                        config.SourceSchemaName = reader.Get<string>("SourceSchemaName");
                        config.SourceTableName = reader.Get<string>("SourceTableName");
                        config.DateColumn = reader.Get<string>("DateColumn");
                        config.RunFrequencyDays = reader.Get<int>("RunFrequencyDays");
                        config.NextRunDate = reader.Get<DateTime>("NextRunDate");
                        config.CreatedBy = reader.Get<string>("CreatedBy");
                        config.LastEditBy = reader.Get<string>("LastEditBy");
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

        private static SqlCommand GetTruncateLedgerTableConfigs(List<string> filters)
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
                          ,[IsTruncate]
                          ,[IsActive]
                          ,[IsEnabled]
                          ,[IsTestMode]
                          ,[IsRunning]
                          ,[IsAutoDisable]
                          ,[GroupName]
                          ,[PlantCode]
                          ,[SourceServerName]
                          ,[SourceDatabaseName]
                          ,[SourceSchemaName]
                          ,[SourceTableName]
                          ,[DateColumn]
                          ,[RunFrequencyDays]
                          ,[NextRunDate]
                          ,[LastRunStatus]
                          ,[LastRunStartDate]
                          ,[LastRunEndDate]
                          ,[RowsTruncated]
                          ,[TruncateSeconds]
                          ,[ProcessID]
                          ,[SourceInitialRowCount]
                          ,[SourceFinalRowCount]
                          ,[SourceMinDate]
                          ,[SourceMaxDate]
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
