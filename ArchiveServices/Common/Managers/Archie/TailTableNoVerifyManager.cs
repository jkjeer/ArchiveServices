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
    public class TailTableNoVerifyManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private const string _ConfigTableName = "cfgTailTableNoVerify";

        public static ProcessResponse Process(long batchID, string batchName, TailTableNoVerifyConfig tailTableNoVerifyConfig, SqlConnection cnArchConfig, SqlConnection cnSource, bool statsOnly)
        {
            tailTableNoVerifyConfig.LastRunStartDate = DateTime.Now.ToLocalTime();
            tailTableNoVerifyConfig.LastRunEndDate = tailTableNoVerifyConfig.LastRunStartDate;
            var processName = $@"Archive Table: {tailTableNoVerifyConfig.ServerQualifiedSourceName}";
            Logger.Debug($"{processName} BEGIN: {DateTime.Now.ToLocalTime()}");
            tailTableNoVerifyConfig.BatchID = batchID;
            tailTableNoVerifyConfig.ProcessID = LogProcessInsert(cnArchConfig, processName, batchID, tailTableNoVerifyConfig);
            tailTableNoVerifyConfig.AutoConfigDisable = Convert.ToBoolean(ConfigurationManager.AppSettings["AutoConfigDisable"]);
            tailTableNoVerifyConfig.AutoConfigDeleteRowsAdjust = Convert.ToBoolean(ConfigurationManager.AppSettings["AutoConfigDeleteRowsAdjust"]);
            tailTableNoVerifyConfig.TestModeConfig = Convert.ToBoolean(ConfigurationManager.AppSettings["TestModeConfig"]);
            tailTableNoVerifyConfig.TestModeNoDelete = Convert.ToBoolean(ConfigurationManager.AppSettings["TestModeNoDelete"]);
            tailTableNoVerifyConfig.StatsOnly = statsOnly;
            if (tailTableNoVerifyConfig.TestModeNoDelete) processName = $@"{processName} - Test Mode";
            var nextRuntimeMoveAheadDays = Convert.ToInt32(ConfigurationManager.AppSettings["ArchieNextRuntimeMoveAheadDays"]);
            var maxDeleteSeconds = Convert.ToDecimal(ConfigurationManager.AppSettings["ArchieMaxDeleteSeconds"]);
            long maxDeleteRows = Convert.ToInt64(ConfigurationManager.AppSettings["ArchieMaxDeleteRows"]);
            LogProcessUpdate(cnArchConfig, tailTableNoVerifyConfig);
            var response = new ProcessResponse($@"BatchID [{batchID}] {batchName} ProcessID [{tailTableNoVerifyConfig.ProcessID}] {processName}", tailTableNoVerifyConfig, true);
            response.BatchID = batchID;
            response.ProcessID = tailTableNoVerifyConfig.ProcessID;
            response.ArchiveDate = tailTableNoVerifyConfig.LastRunStartDate;
            response.ConnectionStrings.Add(new ConnectionString(cnArchConfig, "cnArchConfig", cnArchConfig.ConnectionString, tailTableNoVerifyConfig.BatchID, tailTableNoVerifyConfig.ProcessID));
            response.ConnectionStrings.Add(new ConnectionString("smtp", ConfigurationManager.AppSettings["SmtpHost"]));
            try
            {
                var stepValidateTable = new ProcessStep(cnArchConfig, tailTableNoVerifyConfig.ProcessID, $@"Validate configuration in [Archiver].[dbo].[{_ConfigTableName}] for {tailTableNoVerifyConfig.ServerQualifiedSourceName}");
                try
                {
                    tailTableNoVerifyConfig.ValidationErrors = ValidateConfig(stepValidateTable.StepID, tailTableNoVerifyConfig, cnArchConfig, cnSource);
                    stepValidateTable.RowsAffected = 1;
                }
                catch (Exception e)
                {
                    stepValidateTable.Errors.Add(new Error(e));
                }
                stepValidateTable.Done(cnArchConfig);
                response.ProcessSteps.Add(stepValidateTable);

                if (tailTableNoVerifyConfig.ValidationErrors.Any())
                {
                    tailTableNoVerifyConfig.LastRunStatus = "Error";
                    LogProcessUpdate(cnArchConfig, tailTableNoVerifyConfig);
                    foreach (var validationError in tailTableNoVerifyConfig.ValidationErrors)
                    {
                        if (!response.Errors.Contains(new Error(validationError.Message))) response.Errors.Add(new Error(validationError.Message));
                    }
                    if (tailTableNoVerifyConfig.AutoConfigDisable && tailTableNoVerifyConfig.StatsOnly == false)
                    {
                        var stepUpdatetailTableNoVerifyConfigDisable = new ProcessStep(cnArchConfig, response.ProcessID, $@"Set [Archiver].[dbo].[{_ConfigTableName}].[IsEnabled] = FALSE for {tailTableNoVerifyConfig.ServerQualifiedSourceName}");
                        try
                        {
                            stepUpdatetailTableNoVerifyConfigDisable.RowsAffected = UpdateTailTableNoVerifyConfigIsEnabled(cnArchConfig, tailTableNoVerifyConfig, false);
                        }
                        catch (Exception e)
                        {
                            stepUpdatetailTableNoVerifyConfigDisable.Errors.Add(new Error(e));
                        }
                        stepUpdatetailTableNoVerifyConfigDisable.Done(cnArchConfig);
                        response.ProcessSteps.Add(stepUpdatetailTableNoVerifyConfigDisable);
                    }
                }

                if (!response.Errors.Any())
                {
                    var stepGetSourceSqlConnection = new ProcessStep(cnArchConfig, tailTableNoVerifyConfig.ProcessID, $@"Get SQL connection for Source table {tailTableNoVerifyConfig.ServerQualifiedSourceName}");
                    try
                    {
                        // Get Source SQL connection
                        cnSource = GetSourceSqlConnection(tailTableNoVerifyConfig.SourceServerName, tailTableNoVerifyConfig.SourceDatabaseName);
                        response.ConnectionStrings.Add(new ConnectionString(cnArchConfig, "cnSource", cnSource.ConnectionString, tailTableNoVerifyConfig.BatchID, tailTableNoVerifyConfig.ProcessID));
                        if (cnSource.State != ConnectionState.Open)
                        {
                            var validationError = new ValidationError($@"ERROR: Source server [{tailTableNoVerifyConfig.SourceServerName}].[{tailTableNoVerifyConfig.SourceDatabaseName}] ConnectionState is not Open.", "Validation", "Fatal", true);
                            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepGetSourceSqlConnection.StepID, validationError.Message, "");
                            tailTableNoVerifyConfig.ValidationErrors.Add(validationError);
                            tailTableNoVerifyConfig.ValidationStatus = validationError.Message;
                            tailTableNoVerifyConfig.LastRunStatus = "Error";
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
                }

                if (!response.Errors.Any())
                {
                    var stepCheckSourceDatabaseExists = new ProcessStep(cnArchConfig, tailTableNoVerifyConfig.ProcessID, $@"Check that Source database [{tailTableNoVerifyConfig.SourceServerName}].[{tailTableNoVerifyConfig.SourceDatabaseName}] exists");
                    try
                    {
                        // See if Source database exists
                        if (!CheckSourceDatabaseExists(cnSource, tailTableNoVerifyConfig))
                        {
                            var validationError = new ValidationError($@"ERROR: {cnSource.ConnectionString} Source database [{tailTableNoVerifyConfig.SourceServerName}].[{tailTableNoVerifyConfig.SourceDatabaseName}] not found.", "Validation", "Fatal", true);
                            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepCheckSourceDatabaseExists.StepID, validationError.Message, "");
                            tailTableNoVerifyConfig.ValidationErrors.Add(validationError);
                            tailTableNoVerifyConfig.ValidationStatus = validationError.Message;
                            tailTableNoVerifyConfig.LastRunStatus = "Error";
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

                    var stepCheckSourceSchemaExists = new ProcessStep(cnArchConfig, tailTableNoVerifyConfig.ProcessID, $@"Check that Source schema [{tailTableNoVerifyConfig.SourceServerName}].[{tailTableNoVerifyConfig.SourceDatabaseName}].[{tailTableNoVerifyConfig.SourceSchemaName}] exists");
                    try
                    {
                        // See if Source schema exists
                        if (!CheckSourceSchemaExists(cnSource, tailTableNoVerifyConfig))
                        {
                            var validationError = new ValidationError($@"ERROR: {cnSource.ConnectionString} Source schema [{tailTableNoVerifyConfig.SourceServerName}].[{tailTableNoVerifyConfig.SourceDatabaseName}].[{tailTableNoVerifyConfig.SourceSchemaName}] not found.", "Validation", "Fatal", true);
                            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepCheckSourceSchemaExists.StepID, validationError.Message, "");
                            tailTableNoVerifyConfig.ValidationErrors.Add(validationError);
                            tailTableNoVerifyConfig.ValidationStatus = validationError.Message;
                            tailTableNoVerifyConfig.LastRunStatus = "Error";
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

                    var stepCheckSourceTableExists = new ProcessStep(cnArchConfig, tailTableNoVerifyConfig.ProcessID, $@"Check that Source Table  {tailTableNoVerifyConfig.ServerQualifiedSourceName} exists");
                    try
                    {
                        // See if Source table exists
                        if (!CheckSourceTableExists(cnSource, tailTableNoVerifyConfig))
                        {
                            var validationError = new ValidationError($@"ERROR: {cnSource.ConnectionString} Source table {tailTableNoVerifyConfig.ServerQualifiedSourceName} not found.", "Validation", "Fatal", true);
                            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepCheckSourceTableExists.StepID, validationError.Message, "");
                            tailTableNoVerifyConfig.ValidationErrors.Add(validationError);
                            tailTableNoVerifyConfig.ValidationStatus = validationError.Message;
                            tailTableNoVerifyConfig.LastRunStatus = "Error";
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

                    var stepCheckIsSourceKeyColumnUnique = new ProcessStep(cnArchConfig, tailTableNoVerifyConfig.ProcessID, $@"Check if Source key is unique for {tailTableNoVerifyConfig.ServerQualifiedSourceName}");
                    try
                    {
                        // Build SourceKeyColumns collection 
                        var keyColumnList = tailTableNoVerifyConfig.SourceKey.Split(',').ToList();
                        foreach (var keyColumn in keyColumnList)
                        {
                            tailTableNoVerifyConfig.SourceKeyColumns.Add(GetKeyData(cnSource, tailTableNoVerifyConfig.SourceSchemaName, tailTableNoVerifyConfig.SourceTableName, keyColumn));
                        }
                        stepCheckIsSourceKeyColumnUnique.RowsAffected = 1;
                        // See if Source table key is unique
                        tailTableNoVerifyConfig.IsSourceKeyUnique = IsSourceKeyUnique(cnSource, tailTableNoVerifyConfig);
                        UpdateTailTableNoVerifyConfigIsKeyUnique(cnArchConfig, tailTableNoVerifyConfig);
                        stepCheckIsSourceKeyColumnUnique.RowsAffected = 1;
                        if (!tailTableNoVerifyConfig.IsSourceKeyUnique)
                        {
                            var validationError = new ValidationError($@"ERROR: {cnSource.ConnectionString} Source table {tailTableNoVerifyConfig.ServerQualifiedSourceName} Key [{tailTableNoVerifyConfig.SourceKey}] is not unique.", "Validation", "Fatal", true);
                            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepCheckIsSourceKeyColumnUnique.StepID, validationError.Message, "");
                            tailTableNoVerifyConfig.ValidationErrors.Add(validationError);
                            tailTableNoVerifyConfig.ValidationStatus = validationError.Message;
                            tailTableNoVerifyConfig.LastRunStatus = "Error";
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
                    var stepUpdatetailTableNoVerifyConfigIsRunningTrue = new ProcessStep(cnArchConfig, tailTableNoVerifyConfig.ProcessID, $@"Set [Archiver].[dbo].[{_ConfigTableName}].[IsRunning] = TRUE for {tailTableNoVerifyConfig.ServerQualifiedSourceName}");
                    try
                    {
                        tailTableNoVerifyConfig.IsRunning = true;
                        UpdatetailTableNoVerifyConfigIsRunning(cnArchConfig, tailTableNoVerifyConfig);
                        LogProcessUpdate(cnArchConfig, tailTableNoVerifyConfig);
                        stepUpdatetailTableNoVerifyConfigIsRunningTrue.RowsAffected = 1;
                    }
                    catch (Exception e)
                    {
                        stepUpdatetailTableNoVerifyConfigIsRunningTrue.Errors.Add(new Error(e));
                    }
                    stepUpdatetailTableNoVerifyConfigIsRunningTrue.Done(cnArchConfig);
                    response.ProcessSteps.Add(stepUpdatetailTableNoVerifyConfigIsRunningTrue);
                }

                if (!response.Errors.Any())
                {
                    var stepGatherInitialStatistics = new ProcessStep(cnArchConfig, tailTableNoVerifyConfig.ProcessID, $@"Gather initial statistics for {tailTableNoVerifyConfig.ServerQualifiedSourceName}");
                    try
                    {
                        tailTableNoVerifyConfig.SourceInitialRowCount = GetRowCountSource(cnSource, tailTableNoVerifyConfig);
                        tailTableNoVerifyConfig.LastRunStatus = "";
                        UpdateTailTableNoVerifyConfigInitialRowCount(cnArchConfig, tailTableNoVerifyConfig);
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

                if (!response.Errors.Any() && tailTableNoVerifyConfig.StatsOnly == false)
                {
                    var stepDropTempTableSource = new ProcessStep(cnArchConfig, tailTableNoVerifyConfig.ProcessID, $@"Drop source temp table #TempKeyList");
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
                        var stepCreateSourceTempTable = new ProcessStep(cnArchConfig, tailTableNoVerifyConfig.ProcessID, $@"Create source temp table #TempKeyList with oldest {tailTableNoVerifyConfig.MaxDeleteRowsPerBatch} rows from {tailTableNoVerifyConfig.ServerQualifiedSourceName}");
                        //string cmdStr;
                        var sb = new StringBuilder();
                        sb.Append($@"
                                            SELECT TOP ({tailTableNoVerifyConfig.MaxDeleteRowsPerBatch}) {tailTableNoVerifyConfig.SourceKey}
                                            INTO #TempKeyList
                                            FROM [{tailTableNoVerifyConfig.SourceDatabaseName}].[{tailTableNoVerifyConfig.SourceSchemaName}].[{tailTableNoVerifyConfig.SourceTableName}] WITH (NOLOCK)
                                            {tailTableNoVerifyConfig.WhereClause} 
                                            ORDER BY {tailTableNoVerifyConfig.DateColumn}, {tailTableNoVerifyConfig.SourceKey}");
                        var cmdStr = sb.ToString();
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
                            tailTableNoVerifyConfig.SelectedRows = cmdTemp.ExecuteNonQuery();
                            Logger.Info($@"ROWS: {tailTableNoVerifyConfig.SelectedRows}");
                            stepCreateSourceTempTable.RowsAffected = tailTableNoVerifyConfig.SelectedRows;
                        }
                        catch (Exception e)
                        {
                            stepCreateSourceTempTable.Errors.Add(new Error(e));
                        }
                        stepCreateSourceTempTable.Done(cnArchConfig);
                        response.ProcessSteps.Add(stepCreateSourceTempTable);
                    }

                    if (!response.Errors.Any() && tailTableNoVerifyConfig.SelectedRows > 0)
                    {
                        var stepDeleteSourceData = new ProcessStep(cnArchConfig, tailTableNoVerifyConfig.ProcessID, $@"Delete {tailTableNoVerifyConfig.TempRowsSource} rows from {tailTableNoVerifyConfig.ServerQualifiedSourceName}");
                        var sb = new StringBuilder();
                        sb.Append($@"
                                                        DELETE t FROM [{tailTableNoVerifyConfig.SourceSchemaName}].[{tailTableNoVerifyConfig.SourceTableName}] t
                                                        INNER JOIN #TempKeyList k 
                                                            ON ");
                        // Build SourceKeyColumns collection
                        var keyColumnList = tailTableNoVerifyConfig.SourceKey.Split(',').ToList();
                        var i = 0;
                        foreach (var keyColumn in keyColumnList)
                        {
                            i++;
                            sb.Append($@"
                                                            k.[{keyColumn}] = t.[{keyColumn}] ");
                            if (i < keyColumnList.Count) sb.Append(" AND ");
                        }
                        var cmdStr = sb.ToString();
                        var cmdDelete = new SqlCommand
                        {
                            Connection = cnSource,
                            CommandType = CommandType.Text,
                            CommandTimeout = 0,
                            CommandText = cmdStr
                        };
                        Logger.Debug(Logging.DbCmdString(cmdDelete));
                        stepDeleteSourceData.Statement = cmdDelete.CommandText;
                        tailTableNoVerifyConfig.DeleteCommand = cmdDelete.CommandText;
                        //===============================================================================================
                        if (!tailTableNoVerifyConfig.TestModeNoDelete && !tailTableNoVerifyConfig.IsTestMode)
                        {
                            tailTableNoVerifyConfig.DeleteStartTime = DateTime.Now;
                            tailTableNoVerifyConfig.RowsDeleted = cmdDelete.ExecuteNonQuery();
                            tailTableNoVerifyConfig.DeleteEndTime = DateTime.Now;
                            Logger.Debug($@"Deleted ROWS: {tailTableNoVerifyConfig.RowsDeleted}");
                            Logger.Debug($@"DeleteSeconds: {tailTableNoVerifyConfig.DeleteSeconds}");
                            UpdateTailTableNoVerifyConfigDeletedRows(cnArchConfig, tailTableNoVerifyConfig);
                        }
                        //===============================================================================================
                        LogProcessUpdateDeleteSeconds(cnArchConfig, tailTableNoVerifyConfig);
                        stepDeleteSourceData.RowsAffected = tailTableNoVerifyConfig.RowsDeleted;
                        stepDeleteSourceData.Done(cnArchConfig);
                        response.ProcessSteps.Add(stepDeleteSourceData);
                        tailTableNoVerifyConfig.DeleteSeconds = stepDeleteSourceData.ElapsedSeconds;
                    }
                    //Logger.Debug("SLEEP 2 SECONDS...");
                    //Thread.Sleep(2000);

                    if (!response.Errors.Any())
                    {
                        if (tailTableNoVerifyConfig.AutoConfigDeleteRowsAdjust && tailTableNoVerifyConfig.DeleteSeconds > maxDeleteSeconds && tailTableNoVerifyConfig.MaxDeleteRowsPerBatch > 1000)
                        {
                            var stepReduceConfigMaxDeleteRows = new ProcessStep(cnArchConfig, tailTableNoVerifyConfig.ProcessID, $@"Reduce Max Delete Rows by 10%");
                            var cmdReduceConfigMaxDeleteRows = new SqlCommand
                            {
                                Connection = cnArchConfig,
                                CommandType = CommandType.Text,
                                CommandText = $@"
                                                                    UPDATE xx
                                                                    SET 
                                                                        [MaxDeleteRowsPerBatch] = [MaxDeleteRowsPerBatch] * .9
                                                                    FROM [dbo].[{_ConfigTableName}] xx
                                                                    WHERE [ConfigID] = {tailTableNoVerifyConfig.ConfigID}
                                    "
                            };
                            Logger.Debug(Logging.DbCmdString(cmdReduceConfigMaxDeleteRows));
                            stepReduceConfigMaxDeleteRows.Statement = cmdReduceConfigMaxDeleteRows.CommandText;
                            stepReduceConfigMaxDeleteRows.RowsAffected = cmdReduceConfigMaxDeleteRows.ExecuteNonQuery();
                            stepReduceConfigMaxDeleteRows.Done(cnArchConfig);
                            response.ProcessSteps.Add(stepReduceConfigMaxDeleteRows);
                            Logger.Debug($@"ROWS: {stepReduceConfigMaxDeleteRows.RowsAffected}");
                        }

                        if (tailTableNoVerifyConfig.AutoConfigDeleteRowsAdjust && tailTableNoVerifyConfig.DeleteSeconds < maxDeleteSeconds && (tailTableNoVerifyConfig.MaxDeleteRowsPerBatch * 1.1 <= maxDeleteRows))
                        {
                            var stepIncreaseConfigMaxDeleteRows = new ProcessStep(cnArchConfig, tailTableNoVerifyConfig.ProcessID, $@"Increase Max Delete Rows by 10%");
                            var cmdIncreaseConfigMaxDeleteRows = new SqlCommand
                            {
                                Connection = cnArchConfig,
                                CommandType = CommandType.Text,
                                CommandText = $@"
                                                                    UPDATE xx
                                                                    SET 
                                                                        [MaxDeleteRowsPerBatch] = [MaxDeleteRowsPerBatch] * 1.1
                                                                    FROM [dbo].[{_ConfigTableName}] xx
                                                                    WHERE [ConfigID] = {tailTableNoVerifyConfig.ConfigID}
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

                //--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

                if (!tailTableNoVerifyConfig.ValidationErrors.Any() && !response.Errors.Any())
                {
                    var stepGatherFinalStatistics = new ProcessStep(cnArchConfig, tailTableNoVerifyConfig.ProcessID, $@"Gather final statistics for {tailTableNoVerifyConfig.ServerQualifiedSourceName}");
                    try
                    {
                        tailTableNoVerifyConfig.HighwaterMarkDate = GetHighWaterMarkDate(cnSource, tailTableNoVerifyConfig);
                        tailTableNoVerifyConfig.HighwaterMarkDateString = tailTableNoVerifyConfig.HighwaterMarkDate.ToString("M/dd/yyyy hh:mm:ss tt", CultureInfo.CreateSpecificCulture("en-US"));
                        tailTableNoVerifyConfig.RowsRemaining = GetRowsRemaining(cnSource, tailTableNoVerifyConfig);
                        tailTableNoVerifyConfig.SourceFinalRowCount = GetRowCountSource(cnSource, tailTableNoVerifyConfig);
                        tailTableNoVerifyConfig.SourceMinDate = GetMinSourceDate(cnSource, tailTableNoVerifyConfig);
                        tailTableNoVerifyConfig.SourceMaxDate = GetMaxSourceDate(cnSource, tailTableNoVerifyConfig);
                        tailTableNoVerifyConfig.SourceMaxDateString = tailTableNoVerifyConfig.SourceMaxDate.ToString("M/dd/yyyy hh:mm:ss tt", CultureInfo.CreateSpecificCulture("en-US"));
                        if (statsOnly == true) tailTableNoVerifyConfig.LastRunStatus = "Success";
                        var stepUpdateHighWaterMark = new ProcessStep(cnArchConfig, tailTableNoVerifyConfig.ProcessID, $@"Update HighWaterMark for {tailTableNoVerifyConfig.ServerQualifiedSourceName}");
                        try
                        {
                            UpdatetailTableNoVerifyConfigHighwaterMarks(cnArchConfig, tailTableNoVerifyConfig);
                            stepUpdateHighWaterMark.RowsAffected = 1;
                        }
                        catch (Exception e)
                        {
                            stepUpdateHighWaterMark.Errors.Add(new Error(e));
                        }
                        stepUpdateHighWaterMark.Done(cnArchConfig);
                        response.ProcessSteps.Add(stepUpdateHighWaterMark);
                        response.RowsAffected = tailTableNoVerifyConfig.RowsAffected;
                        response.SourceRowCount = tailTableNoVerifyConfig.SourceRowCount;
                        response.SFMinDate = tailTableNoVerifyConfig.SourceMinDate;
                        response.SFDays = tailTableNoVerifyConfig.SourceDays;
                        response.SourceMaxDate = tailTableNoVerifyConfig.SourceMaxDate;
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
                tailTableNoVerifyConfig.IsRunning = false;
                tailTableNoVerifyConfig.LastRunEndDate = DateTime.Now;
                var stepUpdatetailTableNoVerifyConfigDone = new ProcessStep(cnArchConfig, tailTableNoVerifyConfig.ProcessID, $@"Set [Archiver].[dbo].[{_ConfigTableName}] DONE for {tailTableNoVerifyConfig.ServerQualifiedSourceName}");
                UpdatetailTableNoVerifyConfigIsRunning(cnArchConfig, tailTableNoVerifyConfig);
                LogProcessUpdate(cnArchConfig, tailTableNoVerifyConfig);
                stepUpdatetailTableNoVerifyConfigDone.RowsAffected = UpdateTailTableNoVerifyConfig(cnArchConfig, tailTableNoVerifyConfig);
                stepUpdatetailTableNoVerifyConfigDone.Done(cnArchConfig);
                Logger.Debug("END: " + Convert.ToDecimal((tailTableNoVerifyConfig.LastRunEndDate - tailTableNoVerifyConfig.LastRunStartDate).TotalSeconds) + " SECONDS");
            }
            return response;
        }

        private static ValidationErrors ValidateConfig(int stepID, TailTableNoVerifyConfig tailTableNoVerifyConfig, SqlConnection cnArchConfig, SqlConnection cnSource)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Debug($"BEGIN: {startTime}");
            var validationErrors = new ValidationErrors();
            try
            {
                //int maxMergeRows = Convert.ToInt32(ConfigurationManager.AppSettings["ArchieMaxMergeRows"]);

                if (string.IsNullOrEmpty(tailTableNoVerifyConfig.SourceTableName))
                {
                    var validationError = new ValidationError($"{tailTableNoVerifyConfig.ServerQualifiedSourceName} is not found in [Archiver].[dbo].[{_ConfigTableName}].", "Validation", "Fatal", true);
                    validationErrors.Add(validationError);
                    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                    tailTableNoVerifyConfig.ValidationStatus = validationError.Message;
                    Logger.Error(validationError.Message);
                }
                else
                {
                    if (tailTableNoVerifyConfig.IsRunning)
                    {
                        var validationError = new ValidationError($"Archive of {tailTableNoVerifyConfig.ServerQualifiedSourceName} is already Running.", "Validation", "Fatal", true);
                        validationErrors.Add(validationError);
                        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                        tailTableNoVerifyConfig.ValidationStatus = validationError.Message;
                    }
                    if (tailTableNoVerifyConfig.IsTail == false)
                    {
                        var validationError = new ValidationError($"{tailTableNoVerifyConfig.ServerQualifiedSourceName} is not configured for Tailing.", "Validation", "Fatal", true);
                        validationErrors.Add(validationError);
                        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                        tailTableNoVerifyConfig.ValidationStatus = validationError.Message;
                        Logger.Error(validationError.Message);
                    }
                    if (tailTableNoVerifyConfig.IsActive == false)
                    {
                        var validationError = new ValidationError($"{tailTableNoVerifyConfig.ServerQualifiedSourceName} is not Active.", "Validation", "Fatal", true);
                        validationErrors.Add(validationError);
                        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                        tailTableNoVerifyConfig.ValidationStatus = validationError.Message;
                        Logger.Error(validationError.Message);
                    }
                    if (tailTableNoVerifyConfig.IsEnabled == false && tailTableNoVerifyConfig.StatsOnly == false)
                    {
                        var validationError = new ValidationError($"{tailTableNoVerifyConfig.ServerQualifiedSourceName} is not Enabled.", "Validation", "Fatal", true);
                        validationErrors.Add(validationError);
                        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                        tailTableNoVerifyConfig.ValidationStatus = validationError.Message;
                        Logger.Error(validationError.Message);
                    }
                    if (tailTableNoVerifyConfig.TestModeConfig == true && tailTableNoVerifyConfig.IsTestMode == false && tailTableNoVerifyConfig.StatsOnly == false)
                    {
                        var validationError = new ValidationError($"{tailTableNoVerifyConfig.ServerQualifiedSourceName} is not configured for Test Mode but web.config is in Test Mode.", "Validation", "Fatal", true);
                        validationErrors.Add(validationError);
                        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                        tailTableNoVerifyConfig.ValidationStatus = validationError.Message;
                        Logger.Error(validationError.Message);
                    }
                }
                //if (!tailTableNoVerifyConfig.IsTestMode)
                //{
                //    if (!tailTableNoVerifyConfig.IsActive)
                //    {
                //        var validationError = new ValidationError($"{tailTableNoVerifyConfig.ServerQualifiedDestinationName} is Inactive.", "Validation", "Fatal", true);
                //        validationErrors.Add(validationError);
                //        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                //        tailTableNoVerifyConfig.ValidationStatus = validationError.Message;
                //        Logger.Error(validationError.Message);
                //    }
                //    if (!tailTableNoVerifyConfig.IsEnabled)
                //    {
                //        var validationError = new ValidationError($"{tailTableNoVerifyConfig.ServerQualifiedDestinationName} is Disabled.", "Validation", "Fatal", true);
                //        validationErrors.Add(validationError);
                //        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                //        tailTableNoVerifyConfig.ValidationStatus = validationError.Message;
                //        Logger.Error(validationError.Message);
                //    }
                //}
                //if (tailTableNoVerifyConfig.DestinationDatabaseName == "PlantArchive" && (tailTableNoVerifyConfig.SourceServerName != tailTableNoVerifyConfig.DestinationServerName))
                //{
                //    var validationError = new ValidationError($"{tailTableNoVerifyConfig.ServerQualifiedDestinationName} Source database must reside on the same server as the Destination.", "Validation", "Fatal", true);
                //    validationErrors.Add(validationError);
                //    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                //    tailTableNoVerifyConfig.ValidationStatus = validationError.Message;
                //    Logger.Error(validationError.Message);
                //}
                if (string.IsNullOrEmpty(tailTableNoVerifyConfig.SourceKey))
                {
                    var validationError = new ValidationError($"{tailTableNoVerifyConfig.ServerQualifiedSourceName} is not configured for SourceKey.", "Validation", "Fatal", true);
                    validationErrors.Add(validationError);
                    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                    tailTableNoVerifyConfig.ValidationStatus = validationError.Message;
                    Logger.Error(validationError.Message);
                }
                if (string.IsNullOrEmpty(tailTableNoVerifyConfig.DateColumn))
                {
                    var validationError = new ValidationError($"{tailTableNoVerifyConfig.ServerQualifiedSourceName} is not configured for DateColumn.", "Validation", "Fatal", true);
                    validationErrors.Add(validationError);
                    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                    tailTableNoVerifyConfig.ValidationStatus = validationError.Message;
                    Logger.Error(validationError.Message);
                }
                if (tailTableNoVerifyConfig.IDColumn.Contains(","))
                {
                    var validationError = new ValidationError($"IDColumn must be a single value for {tailTableNoVerifyConfig.ServerQualifiedSourceName}.", "Validation", "Fatal", true);
                    validationErrors.Add(validationError);
                    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                    tailTableNoVerifyConfig.ValidationStatus = validationError.Message;
                    Logger.Error(validationError.Message);
                }
            }
            catch (Exception e)
            {
                Logger.Error($@"ERROR: {cnSource.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new ApplicationException($@"{cnSource.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            finally
            {
                if (!validationErrors.Any()) tailTableNoVerifyConfig.ValidationStatus = "Valid";
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

        private static bool CheckSourceDatabaseExists(SqlConnection cnSource, TailTableNoVerifyConfig tailTableNoVerifyConfig)
        {
            bool retVal = false;
            try
            {
                string str = string.Format($@"SELECT 1 FROM sys.databases WHERE Name = '{tailTableNoVerifyConfig.SourceDatabaseName}'");
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

        private static bool CheckSourceSchemaExists(SqlConnection cnSource, TailTableNoVerifyConfig tailTableNoVerifyConfig)
        {
            bool retVal = false;
            try
            {
                string str = string.Format($@"SELECT 1 FROM sys.schemas WHERE name = '{tailTableNoVerifyConfig.SourceSchemaName}'");
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

        private static bool CheckSourceTableExists(SqlConnection cnSource, TailTableNoVerifyConfig tailTableNoVerifyConfig)
        {
            bool retVal = false;
            try
            {
                string str = string.Format($@"
                    SELECT 1 
                    FROM {tailTableNoVerifyConfig.SourceDatabaseName}.sys.tables t
                    INNER JOIN {tailTableNoVerifyConfig.SourceDatabaseName}.sys.schemas s ON s.schema_id = t.schema_id
                    WHERE s.name = '{tailTableNoVerifyConfig.SourceSchemaName}'
                    AND t.name = '{tailTableNoVerifyConfig.SourceTableName}'
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

        private static KeyColumn GetKeyData(SqlConnection cnSource, string schemaName, string tableName, string columnName)
        {
            KeyColumn keyColumn = new KeyColumn(columnName);
            try
            {
                var cmd = new SqlCommand();
                cmd = GetKeyColumnMetadata();
                cmd.Parameters["@SchemaName"].Value = schemaName;
                cmd.Parameters["@TableName"].Value = tableName;
                cmd.Parameters["@ColumnName"].Value = columnName;
                cmd.Connection = cnSource;
                Logger.Debug(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        keyColumn.DataType = CastDbNull.To<string>(reader["DataType"], string.Empty).Trim();
                        keyColumn.MaxLength = CastDbNull.To<Int16>(reader["MaxLength"], 0);
                        keyColumn.Precision = CastDbNull.To<Byte>(reader["Precision"], 0);
                        keyColumn.Scale = CastDbNull.To<Byte>(reader["Scale"], 0);
                    }
                    reader.Close();
                }
                //Logger.Debug($@"columnType = {DataType}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {cnSource.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new ApplicationException(cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
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
                           c.column_id,
                           x.MaxColId,
                           c.max_length AS MaxLength,
                           c.precision AS Precision,
                           c.scale AS Scale,
                           t.name AS DataType,
                           t.schema_id,
                           t.max_length AS t_max_length,
                           t.precision AS t_precision,
                           t.scale AS t_scale
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

        private static bool IsSourceKeyUnique(SqlConnection cnSource, TailTableNoVerifyConfig tailTableNoVerifyConfig)
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
                        SELECT  [{tailTableNoVerifyConfig.SourceKey.Replace(",", "], [")}], COUNT(*)
                        FROM    [{tailTableNoVerifyConfig.SourceSchemaName}].[{tailTableNoVerifyConfig.SourceTableName}] WITH (NOLOCK) 
                        GROUP BY [{tailTableNoVerifyConfig.SourceKey.Replace(",", "], [")}]
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

        private static long UpdatetailTableNoVerifyConfigIsRunning(SqlConnection cnArchConfig, TailTableNoVerifyConfig tailTableNoVerifyConfig)
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
                            [IsRunning] = {Convert.ToInt32(tailTableNoVerifyConfig.IsRunning)}
                            ,[ProcessID] = {tailTableNoVerifyConfig.ProcessID}
                            ,[LastRunStatus] = '{tailTableNoVerifyConfig.LastRunStatus}'
                            ,[LastRunStartDate] = '{tailTableNoVerifyConfig.LastRunStartDate}'
                            ,[LastRunEndDate] = '{tailTableNoVerifyConfig.LastRunEndDate}'
                            ,[IsSourceKeyUnique] = '{tailTableNoVerifyConfig.IsSourceKeyUnique}'
                            ,[BITimeStamp] = GETDATE()
                        FROM [dbo].[{_ConfigTableName}] xx
                        WHERE [ConfigID] = {tailTableNoVerifyConfig.ConfigID}
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

        private static long GetRowCountSource(SqlConnection cnSource, TailTableNoVerifyConfig tailTableNoVerifyConfig, string whereClause = null)
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
                        SELECT COUNT(*) 
                        FROM {tailTableNoVerifyConfig.FullyQualifiedSourceName} WITH (NOLOCK) 
                        {whereClause}
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

        private static DateTime GetHighWaterMarkDate(SqlConnection cnDestination, TailTableNoVerifyConfig tailTableNoVerifyConfig)
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
                    CommandText = BuildHighWaterMarkDateString(tailTableNoVerifyConfig)
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

        private static String BuildHighWaterMarkDateString(TailTableNoVerifyConfig archiveTableConfig)
        {
            string str;
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($@"SELECT ISNULL(ISNULL(MIN([SRC].[{archiveTableConfig.DateColumn}]), (SELECT MAX([{archiveTableConfig.DateColumn}]) FROM {archiveTableConfig.FullyQualifiedSourceName} WITH (NOLOCK))), '1900-01-01') ");
                sb.Append(Environment.NewLine);
                sb.Append($@"FROM [{archiveTableConfig.SourceDatabaseName}].[{archiveTableConfig.SourceSchemaName}].[{archiveTableConfig.SourceTableName}] AS SRC WITH (NOLOCK) ");
                sb.Append(Environment.NewLine);
                sb.Append($@"WHERE NOT EXISTS (SELECT 1 FROM {archiveTableConfig.FullyQualifiedSourceName} DEST WITH (NOLOCK) ");
                sb.Append(Environment.NewLine);
                sb.Append("                  WHERE ");
                var keyColumnList = archiveTableConfig.SourceKey.Split(',').ToList();
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

        private static long GetRowsRemaining(SqlConnection cnDestination, TailTableNoVerifyConfig tailTableNoVerifyConfig)
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
                    CommandText = BuildRowsRemainingString(tailTableNoVerifyConfig)
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

        private static String BuildRowsRemainingString(TailTableNoVerifyConfig tailTableNoVerifyConfig)
        {
            string str;
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($@"SELECT COUNT(*) ");
                sb.Append(Environment.NewLine);
                sb.Append($@"FROM {tailTableNoVerifyConfig.FullyQualifiedSourceName} AS SRC WITH (NOLOCK) ");
                sb.Append(Environment.NewLine);
                sb.Append($@"WHERE NOT EXISTS (SELECT 1 FROM {tailTableNoVerifyConfig.FullyQualifiedSourceName} DEST WITH (NOLOCK) ");
                sb.Append(Environment.NewLine);
                sb.Append("                  WHERE ");
                var keyColumnList = tailTableNoVerifyConfig.SourceKey.Split(',').ToList();
                var i = 0;
                foreach (var keyColumn in keyColumnList)
                {
                    i++;
                    sb.Append($@"DEST.[{keyColumn}] = SRC.[{keyColumn}]");
                    sb.Append(Environment.NewLine);
                    if (i < keyColumnList.Count) sb.Append("AND ");
                }
                sb.Append($@"                AND DEST.{tailTableNoVerifyConfig.DateColumn} = SRC.{tailTableNoVerifyConfig.DateColumn} ");
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

        private static DateTime GetMinSourceDate(SqlConnection cnDestination, TailTableNoVerifyConfig tailTableNoVerifyConfig)
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
                        SELECT ISNULL(MIN([{tailTableNoVerifyConfig.DateColumn}]), '1900-01-01')  
                        FROM {tailTableNoVerifyConfig.FullyQualifiedSourceName} WITH (NOLOCK) 
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

        private static DateTime GetMaxSourceDate(SqlConnection cnSource, TailTableNoVerifyConfig tailTableNoVerifyConfig)
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
                        SELECT ISNULL(MAX([{tailTableNoVerifyConfig.DateColumn}]), '1900-01-01')  
                        FROM {tailTableNoVerifyConfig.FullyQualifiedSourceName} WITH (NOLOCK) 
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

        private static long UpdatetailTableNoVerifyConfigHighwaterMarks(SqlConnection cnArchConfig, TailTableNoVerifyConfig tailTableNoVerifyConfig)
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
                        [HighwaterMarkDate] = '{tailTableNoVerifyConfig.HighwaterMarkDate}'
                        ,[HighwaterMarkID] = {tailTableNoVerifyConfig.HighwaterMarkID}
                        ,[SourceInitialRowCount] = {tailTableNoVerifyConfig.SourceInitialRowCount}
                        ,[SourceFinalRowCount] = {tailTableNoVerifyConfig.SourceFinalRowCount}
                        ,[SourceMinDate] = '{tailTableNoVerifyConfig.SourceMinDate}'
                        ,[SourceMaxDate] = '{tailTableNoVerifyConfig.SourceMaxDate}'
                        ,[RowsRemaining] = '{tailTableNoVerifyConfig.RowsRemaining}'
                        ,[RowsDeleted] = {tailTableNoVerifyConfig.RowsDeleted}
                        ,[BITimeStamp] = GETDATE()
                    FROM [dbo].[{_ConfigTableName}] xx
                    WHERE [ConfigID] = {tailTableNoVerifyConfig.ConfigID}
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

        private static long UpdateTailTableNoVerifyConfigDeletedRows(SqlConnection cnArchConfig, TailTableNoVerifyConfig tailTableNoVerifyConfig)
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
                        [RowsDeleted] = {tailTableNoVerifyConfig.RowsDeleted}
                        ,[DeleteSeconds] = {tailTableNoVerifyConfig.DeleteSeconds}
                        ,[BITimeStamp] = GETDATE()
                    FROM [dbo].[{_ConfigTableName}] xx
                    WHERE [ConfigID] = {tailTableNoVerifyConfig.ConfigID}
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

        private static long UpdateTailTableNoVerifyConfig(SqlConnection cnArchConfig, TailTableNoVerifyConfig tailTableNoVerifyConfig)
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
                            [IsRunning] = {Convert.ToInt32(tailTableNoVerifyConfig.IsRunning)}
                            ,[LastRunStatus] = '{tailTableNoVerifyConfig.LastRunStatus}'
                            ,[SourceInitialRowCount] = {tailTableNoVerifyConfig.SourceInitialRowCount}
                            ,[SourceFinalRowCount] = {tailTableNoVerifyConfig.SourceFinalRowCount}
                            ,[RowsRemaining] = {tailTableNoVerifyConfig.RowsRemaining}
                            ,[BITimeStamp] = GETDATE()
                        FROM [dbo].[{_ConfigTableName}] xx
                        WHERE [ConfigID] = {tailTableNoVerifyConfig.ConfigID}
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

        private static long UpdateTailTableNoVerifyConfigIsEnabled(SqlConnection cnArchConfig, TailTableNoVerifyConfig tailTableNoVerifyConfig, bool isEnabled)
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
                            ,[AutoConfigDisable] = {(tailTableNoVerifyConfig.AutoConfigDisable == true ? 1 : 0)}
                            ,[BITimeStamp] = GETDATE()
                        FROM [dbo].[{_ConfigTableName}] xx
                        WHERE [ConfigID] = {tailTableNoVerifyConfig.ConfigID}
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

        private static long UpdateTailTableNoVerifyConfigInitialRowCount(SqlConnection cnArchConfig, TailTableNoVerifyConfig tailTableNoVerifyConfig)
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
                        [SourceInitialRowCount] = CASE WHEN [SourceInitialRowCount] = 0 THEN {tailTableNoVerifyConfig.SourceInitialRowCount} ELSE [SourceInitialRowCount] END
                        ,[BITimeStamp] = GETDATE()
                    FROM [dbo].[{_ConfigTableName}] xx
                    WHERE [ConfigID] = {tailTableNoVerifyConfig.ConfigID}
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

        private static long UpdateTailTableNoVerifyConfigIsKeyUnique(SqlConnection cnArchConfig, TailTableNoVerifyConfig tailTableNoVerifyConfig)
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
                            [IsSourceKeyUnique] = '{tailTableNoVerifyConfig.IsSourceKeyUnique}'
                            ,[BITimeStamp] = GETDATE()
                        FROM [dbo].[{_ConfigTableName}] xx
                        WHERE [ConfigID] = {tailTableNoVerifyConfig.ConfigID}
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

        public static int LogProcessInsert(SqlConnection cnArchConfig, string processName, long batchID, TailTableNoVerifyConfig tailTableConfig)
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

        public static long LogProcessUpdate(SqlConnection cnArchConfig, TailTableNoVerifyConfig tailTableConfig)
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
                          ,[IDColumn] = '{tailTableConfig.IDColumn}'
                          ,[SourceKey] = '{tailTableConfig.SourceKey}'
                          ,[IsSourceKeyUnique] = {Convert.ToInt32(tailTableConfig.IsSourceKeyUnique)}
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

        public static long LogProcessUpdateDeleteSeconds(SqlConnection cnArchConfig, TailTableNoVerifyConfig tailTableConfig)
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

        public static long LogProcessUpdateDone(SqlConnection cnArchConfig, int processID, TailTableNoVerifyConfig tailTableConfig)
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
                            ,[RowsDeleted] = {tailTableConfig.RowsDeleted}
                            ,[RowsToDelete] = {tailTableConfig.MaxDeleteRowsPerBatch}
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

        public static long LogProcessUpdateInitialRowCount(SqlConnection cnArchConfig, int processID, TailTableNoVerifyConfig tailTableConfig)
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
                           ,[IsKeyColumnUnique] = '{tailTableConfig.IsSourceKeyUnique}'
                           ,[WhereClause] = '{tailTableConfig.WhereClause.Replace("'", "''")}'
                           ,[InitialRowCount] = {tailTableConfig.SourceInitialRowCount} 
                           ,[RowsToDelete] = {tailTableConfig.MaxDeleteRowsPerBatch}
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

        public static long LogProcessUpdateIsKeyColumnUnique(SqlConnection cnArchConfig, int processID, TailTableNoVerifyConfig tailTableConfig)
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
                           [IsKeyColumnUnique] = '{tailTableConfig.IsSourceKeyUnique}'
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

        public static TailTableNoVerifyConfigs GetTailTableNoVerifyConfigs(SqlConnection cnArchConfig, string groupName, string plantCode, string serverName, string databaseName, string schemaName, string tableName, bool useHighwaterMark, bool statsOnly)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Debug($"BEGIN: {DateTime.Now.ToLocalTime()}");
            var testMode = Convert.ToBoolean(ConfigurationManager.AppSettings["TestMode"]);
            var testModeNoDelete = Convert.ToBoolean(ConfigurationManager.AppSettings["TestModeNoDelete"]);
            var configs = new TailTableNoVerifyConfigs();
            var filters = new List<string>();
            var parameters = new List<SqlParameter>();
            try
            {
                if (string.IsNullOrEmpty(tableName))
                {
                    filters.Add("[IsTail] = 1 AND \r\n");
                    filters.Add("[IsVerify] = 0 AND \r\n");
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
                        filters.Add("[IsEnabled] = 1 AND \r\n");
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
                var cmd = GetTailTableNoVerifyConfigs(filters);
                foreach (var parameter in parameters) cmd.Parameters.Add(parameter);
                cmd.Connection = cnArchConfig;
                Logger.Debug(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        var config = new TailTableNoVerifyConfig();
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
                        config.IDColumn = reader.Get<string>("IDColumn");
                        config.SourceKey = reader.Get<string>("SourceKey").Replace(" ", "");
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
                        config.CreatedBy = reader.Get<string>("CreatedBy");
                        config.LastEditBy = reader.Get<string>("LastEditBy");
                        foreach (var keyColumn in config.SourceKey.Split(',').ToList())
                        {
                            config.SourceKeyColumns.Add(new KeyColumn(keyColumn));
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

        private static SqlCommand GetTailTableNoVerifyConfigs(List<string> filters)
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
                          ,[IDColumn]
                          ,[SourceKey]
                          ,[IsSourceKeyUnique]
                          ,[DateColumn]
                          ,[SelectStartDate]
                          ,[HighwaterMarkDate]
                          ,[HighwaterMarkID]
                          ,[OtherCondition]
                          ,[RetentionDays]
                          ,[RowsRemaining]
                          ,[RunFrequencyDays]
                          ,[NextRunDate]
                          ,[LastRunStatus]
                          ,[LastRunStartDate]
                          ,[LastRunEndDate]
                          ,[RowsDeleted]
                          ,[ProcessID]
                          ,[SourceInitialRowCount]
                          ,[SourceFinalRowCount]
                          ,[SourceMinDate]
                          ,[SourceMaxDate]
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
