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
using System.Linq;
using System.Text;

namespace ArchiveServices.Common.Managers.Archie
{
    public class GetLedgerTableCandidatesManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private const string _ConfigTableName = "cfgTruncateLedgerTable";

        public static ProcessResponse Process(long batchID, string batchName, LedgerTableCandidateConfig ledgerTableCandidateConfig, SqlConnection cnArchConfig, SqlConnection cnSource, bool statsOnly)
        {
            ledgerTableCandidateConfig.LastRunStartDate = DateTime.Now.ToLocalTime();
            ledgerTableCandidateConfig.LastRunEndDate = ledgerTableCandidateConfig.LastRunStartDate;
            var processName = $@"Get Ledger Table Candidates: {ledgerTableCandidateConfig.ServerQualifiedSourceDatabaseName}";
            //var ledgerTableCandidates = new LedgerTableCandidateConfigs();
            Logger.Debug($"{processName} BEGIN: {DateTime.Now.ToLocalTime()}");
            ledgerTableCandidateConfig.BatchID = batchID;
            ledgerTableCandidateConfig.ProcessID = LogProcessInsert(cnArchConfig, processName, batchID, ledgerTableCandidateConfig);
            ledgerTableCandidateConfig.StatsOnly = statsOnly;
            LogProcessUpdate(cnArchConfig, ledgerTableCandidateConfig);
            var response = new ProcessResponse($@"BatchID [{batchID}] {batchName} ProcessID [{ledgerTableCandidateConfig.ProcessID}] {processName}", ledgerTableCandidateConfig, true);
            response.BatchID = batchID;
            response.ProcessID = ledgerTableCandidateConfig.ProcessID;
            response.ArchiveDate = ledgerTableCandidateConfig.LastRunStartDate;
            response.ConnectionStrings.Add(new ConnectionString(cnArchConfig, "cnArchConfig", cnArchConfig.ConnectionString, ledgerTableCandidateConfig.BatchID, ledgerTableCandidateConfig.ProcessID));
            response.ConnectionStrings.Add(new ConnectionString("smtp", ConfigurationManager.AppSettings["SmtpHost"]));
            try
            {
                if (!response.Errors.Any())
                {
                    var stepGetSourceSqlConnection = new ProcessStep(cnArchConfig, ledgerTableCandidateConfig.ProcessID, $@"Get SQL connection for Source [{ledgerTableCandidateConfig.SourceServerName}[.[{ledgerTableCandidateConfig.SourceDatabaseName}]");
                    try
                    {
                        // Get Source SQL connection
                        cnSource = GetSourceSqlConnection(ledgerTableCandidateConfig.SourceServerName, ledgerTableCandidateConfig.SourceDatabaseName);
                        response.ConnectionStrings.Add(new ConnectionString(cnArchConfig, "cnSource", cnSource.ConnectionString, ledgerTableCandidateConfig.BatchID, ledgerTableCandidateConfig.ProcessID));
                        if (cnSource.State == ConnectionState.Open)
                        {
                            stepGetSourceSqlConnection.RowsAffected = 1;
                        }
                        else
                        {
                            var validationError = new ValidationError($@"ERROR: Source server [{ledgerTableCandidateConfig.SourceServerName}].[{ledgerTableCandidateConfig.SourceDatabaseName}] ConnectionState is not Open.", "Validation", "Fatal", true);
                            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepGetSourceSqlConnection.StepID, validationError.Message, "");
                            ledgerTableCandidateConfig.ValidationErrors.Add(validationError);
                            ledgerTableCandidateConfig.ValidationStatus = validationError.Message;
                            ledgerTableCandidateConfig.LastRunStatus = "Error";
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
                    var stepCheckSourceDatabaseExists = new ProcessStep(cnArchConfig, ledgerTableCandidateConfig.ProcessID, $@"Check that Source database [{ledgerTableCandidateConfig.SourceServerName}].[{ledgerTableCandidateConfig.SourceDatabaseName}] exists");
                    try
                    {
                        // See if Source database exists
                        if (CheckSourceDatabaseExists(cnSource, ledgerTableCandidateConfig))
                        {   
                            stepCheckSourceDatabaseExists.RowsAffected = 1;
                        }
                        else
                        {
                            var validationError = new ValidationError($@"ERROR: {cnSource.ConnectionString} Source database [{ledgerTableCandidateConfig.SourceServerName}].[{ledgerTableCandidateConfig.SourceDatabaseName}] not found.", "Validation", "Fatal", true);
                            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepCheckSourceDatabaseExists.StepID, validationError.Message, "");
                            ledgerTableCandidateConfig.ValidationErrors.Add(validationError);
                            ledgerTableCandidateConfig.ValidationStatus = validationError.Message;
                            ledgerTableCandidateConfig.LastRunStatus = "Error";
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

                    //var stepCheckSourceSchemaExists = new ProcessStep(cnArchConfig, ledgerTableCandidateConfig.ProcessID, $@"Check that Source schema [{ledgerTableCandidateConfig.SourceServerName}].[{ledgerTableCandidateConfig.SourceDatabaseName}].[{ledgerTableCandidateConfig.SourceSchemaName}] exists");
                    //try
                    //{
                    //    // See if Source schema exists
                    //    if (!CheckSourceSchemaExists(cnSource, ledgerTableCandidateConfig))
                    //    {
                    //        var validationError = new ValidationError($@"ERROR: {cnSource.ConnectionString} Source schema [{ledgerTableCandidateConfig.SourceServerName}].[{ledgerTableCandidateConfig.SourceDatabaseName}].[{ledgerTableCandidateConfig.SourceSchemaName}] not found.", "Validation", "Fatal", true);
                    //        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepCheckSourceSchemaExists.StepID, validationError.Message, "");
                    //        ledgerTableCandidateConfig.ValidationErrors.Add(validationError);
                    //        ledgerTableCandidateConfig.ValidationStatus = validationError.Message;
                    //        ledgerTableCandidateConfig.LastRunStatus = "Error";
                    //        stepCheckSourceSchemaExists.Errors.Add(new Error(validationError.Message));
                    //        if (!response.Errors.Contains(new Error(validationError.Message))) response.Errors.Add(new Error(validationError.Message));
                    //        Logger.Error(validationError.Message);
                    //    }
                    //}
                    //catch (Exception e)
                    //{
                    //    stepCheckSourceSchemaExists.Errors.Add(new Error(e));
                    //}
                    //stepCheckSourceSchemaExists.Done(cnArchConfig);
                    //response.ProcessSteps.Add(stepCheckSourceSchemaExists);

                    //--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

                    if (!response.Errors.Any() && ledgerTableCandidateConfig.StatsOnly == false)
                    {
                        if (!response.Errors.Any())
                        {
                            // Get a list of ledger tables from the source database.
                            var stepGetSourceLedgerTables = new ProcessStep(cnArchConfig, ledgerTableCandidateConfig.ProcessID, $@"Get Ledger tables from {ledgerTableCandidateConfig.ServerQualifiedSourceDatabaseName}");
                            var startTime = DateTime.Now.ToLocalTime();
                            var bulkResponse = new SqlBulkCopyResponse();
                            try
                            {
                                var reader = GetSourceData(ledgerTableCandidateConfig, cnSource, response.ServiceContext.UserName);
                                var stepBulkSchemaData = new ProcessStep("Bulk Copy");
                                try
                                {
                                    bulkResponse = BulkCopyManager.BulkReader("[Ledger].[cfgLedgerTableCandidates]", reader, cnArchConfig);
                                }
                                catch (Exception e)
                                {
                                    Logger.Error($"ERROR: {e.Message}: {e.StackTrace}");
                                    stepBulkSchemaData.Errors.Add(new Error(e));
                                }
                                finally
                                {
                                    stepBulkSchemaData.RowsAffected = (int)bulkResponse.RowsCopied;
                                    stepBulkSchemaData.SqlBulkCopyResponses.Add(bulkResponse);
                                    stepBulkSchemaData.Done();
                                    response.RowsAffected = stepBulkSchemaData.RowsAffected;
                                    response.AddStep(stepBulkSchemaData);
                                }
                                ledgerTableCandidateConfig.SelectedRows = stepBulkSchemaData.RowsAffected;
                                Logger.Info($@"ROWS: {ledgerTableCandidateConfig.SelectedRows}");
                                stepGetSourceLedgerTables.RowsAffected = ledgerTableCandidateConfig.SelectedRows;
                                Logger.Info($"END: {Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds)} SECONDS");
                            }
                            catch (Exception e)
                            {
                                Logger.Error($"ERROR: {e.Message}: {e.StackTrace}");
                                stepGetSourceLedgerTables.Errors.Add(new Error(e));
                                throw new ApplicationException($@"{cnSource.ConnectionString}  {e.Message}: {e.StackTrace}");
                            }
                            stepGetSourceLedgerTables.Done(cnArchConfig);
                            response.ProcessSteps.Add(stepGetSourceLedgerTables);
                        }
                    }

                    //--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

                    if (!response.Errors.Any())
                    {
                        var stepGatherFinalStatistics = new ProcessStep(cnArchConfig, ledgerTableCandidateConfig.ProcessID, $@"Gather final statistics for {ledgerTableCandidateConfig.ServerQualifiedSourceDatabaseName}");
                        try
                        {
                            ledgerTableCandidateConfig.RowsRemaining = CountNewRows(cnArchConfig, ledgerTableCandidateConfig);
                            //ledgerTableCandidateConfig.SourceFinalRowCount = GetRowCountSource(cnSource, ledgerTableCandidateConfig);
                            if (statsOnly == true) ledgerTableCandidateConfig.LastRunStatus = "Success";
                            var stepUpdateHighWaterMark = new ProcessStep(cnArchConfig, ledgerTableCandidateConfig.ProcessID, $@"Update HighWaterMark for {ledgerTableCandidateConfig.ServerQualifiedSourceDatabaseName}");
                            try
                            {
                                UpdateLedgerTableCandidateConfigHighwaterMarks(cnArchConfig, ledgerTableCandidateConfig);
                                stepUpdateHighWaterMark.RowsAffected = 1;
                            }
                            catch (Exception e)
                            {
                                stepUpdateHighWaterMark.Errors.Add(new Error(e));
                            }
                            stepUpdateHighWaterMark.Done(cnArchConfig);
                            response.ProcessSteps.Add(stepUpdateHighWaterMark);
                            response.RowsAffected = ledgerTableCandidateConfig.RowsAffected;
                            response.SourceRowCount = ledgerTableCandidateConfig.SourceRowCount;
                            response.SFMinDate = ledgerTableCandidateConfig.SourceMinDate;
                            response.SFDays = ledgerTableCandidateConfig.SourceDays;
                            response.SourceMaxDate = ledgerTableCandidateConfig.SourceMaxDate;
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
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            finally
            {
                ledgerTableCandidateConfig.IsRunning = false;
                ledgerTableCandidateConfig.LastRunEndDate = DateTime.Now;
                var stepUpdateledgerTableCandidateConfigDone = new ProcessStep(cnArchConfig, ledgerTableCandidateConfig.ProcessID, $@"Set [Archiver].[dbo].[{_ConfigTableName}] DONE for {ledgerTableCandidateConfig.ServerQualifiedSourceDatabaseName}");
                UpdateLedgerTableCandidateConfigIsRunning(cnArchConfig, ledgerTableCandidateConfig);
                LogProcessUpdate(cnArchConfig, ledgerTableCandidateConfig);
                stepUpdateledgerTableCandidateConfigDone.RowsAffected = UpdateLedgerTableCandidateConfig(cnArchConfig, ledgerTableCandidateConfig);
                stepUpdateledgerTableCandidateConfigDone.Done(cnArchConfig);
                Logger.Debug("END: " + Convert.ToDecimal((ledgerTableCandidateConfig.LastRunEndDate - ledgerTableCandidateConfig.LastRunStartDate).TotalSeconds) + " SECONDS");
            }
            return response;
        }

        private static IDataReader GetSourceData(LedgerTableCandidateConfig ledgerTableCandidateConfig, SqlConnection cnSource, string userName)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($@"BEGIN: {DateTime.Now.ToLocalTime()} ");
            try
            {
                var cmdStr = $@"
                                            select 
                                                '{ledgerTableCandidateConfig.GroupName}' as GroupName
                                                ,'{ledgerTableCandidateConfig.PlantCode}' as PlantCode
                                                ,'{ledgerTableCandidateConfig.SourceServerName}' as SourceServerName
                                                ,db_name() as SourceDatabaseName
                                                ,sch.name AS SourceSchemaName
                                                ,tbl.name AS SourceTableName
                                                ,isnull(col.name,'') as DateColumn
                                                ,part.rows as SourceInitialRowCount
                                                ,'{userName}' as CreatedBy
                                            from 
                                                {ledgerTableCandidateConfig.SourceDatabaseName}.sys.tables tbl
                                                INNER JOIN {ledgerTableCandidateConfig.SourceDatabaseName}.sys.schemas sch ON sch.schema_id = tbl.schema_id
                                                INNER JOIN {ledgerTableCandidateConfig.SourceDatabaseName}.sys.partitions part ON part.object_id = tbl.object_id
                                                LEFT OUTER JOIN {ledgerTableCandidateConfig.SourceDatabaseName}.sys.columns col ON col.object_id = tbl.object_id
                                                    and col.name in ('WrittenDate','DownloadDate','QueueDateTime','DownLoadTime')
                                            WHERE 
                                                part.index_id IN (0, 1) -- 0 for heaps, 1 for clustered indexes (i.e., the table itself)
                                                --and sch.name = '{ledgerTableCandidateConfig.SourceSchemaName}' 
                                                and tbl.name like '%Ledger%'
                                            ORDER BY 
                                                sch.name,
                                                tbl.name;
                                        ";
                var cmd = new SqlCommand
                {
                    Connection = cnSource,
                    CommandType = CommandType.Text,
                    CommandText = cmdStr,
                    CommandTimeout = 0
                };
                Logger.Info(Logging.DbCmdString(cmd));
                return cmd.ExecuteReader(CommandBehavior.SingleResult);
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {e.Message}: {e.StackTrace}");
                throw new Exception(e.Message);
            }
            finally
            {
                Logger.Info($@"END: {Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds)} SECONDS");
            }
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

        private static bool CheckSourceDatabaseExists(SqlConnection cnSource, LedgerTableCandidateConfig ledgerTableCandidateConfig)
        {
            bool retVal = false;
            try
            {
                string str = string.Format($@"SELECT 1 FROM sys.databases WHERE Name = '{ledgerTableCandidateConfig.SourceDatabaseName}'");
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

        private static bool CheckSourceSchemaExists(SqlConnection cnSource, LedgerTableCandidateConfig ledgerTableCandidateConfig)
        {
            bool retVal = false;
            try
            {
                string str = string.Format($@"SELECT 1 FROM sys.schemas WHERE name = '{ledgerTableCandidateConfig.SourceSchemaName}'");
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

        private static long UpdateLedgerTableCandidateConfigIsRunning(SqlConnection cnArchConfig, LedgerTableCandidateConfig ledgerTableCandidateConfig)
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
                            [IsRunning] = {Convert.ToInt32(ledgerTableCandidateConfig.IsRunning)}
                            ,[ProcessID] = {ledgerTableCandidateConfig.ProcessID}
                            ,[LastRunStatus] = '{ledgerTableCandidateConfig.LastRunStatus}'
                            ,[LastRunStartDate] = '{ledgerTableCandidateConfig.LastRunStartDate}'
                            ,[LastRunEndDate] = '{ledgerTableCandidateConfig.LastRunEndDate}'
                           ,[BITimeStamp] = GETDATE()
                        FROM [dbo].[{_ConfigTableName}] xx
                        WHERE [ConfigID] = {ledgerTableCandidateConfig.ConfigID}
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

        private static int CountNewRows(SqlConnection cnArchConfig, LedgerTableCandidateConfig ledgerTableCandidateConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Debug($"BEGIN: {DateTime.Now.ToLocalTime()}");
            var retVal = 0;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnArchConfig,
                    CommandType = CommandType.Text,
                    CommandTimeout = 0,
                    CommandText = $@"
                        SELECT COUNT(*)  
                        FROM {_ConfigTableName} WITH (NOLOCK) 
                        WHERE [CreatedDate] > getdate() - .1
                            AND [SourceServerName] = '{ledgerTableCandidateConfig.SourceServerName}'
                            AND [SourceDatabaseName] = '{ledgerTableCandidateConfig.SourceDatabaseName}'      
                            AND [SourceSchemaName] = '{ledgerTableCandidateConfig.SourceSchemaName}'
                            AND [LastRunStartDate] is null
                    "
                };
                Logger.Debug(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt16(cmd.ExecuteScalar());
                Logger.Debug("ROWS: " + retVal);
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cnArchConfig.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cnArchConfig.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            finally
            {
                Logger.Debug("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return retVal;
        }

        private static long UpdateLedgerTableCandidateConfigHighwaterMarks(SqlConnection cnArchConfig, LedgerTableCandidateConfig ledgerTableCandidateConfig)
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
                        [SourceInitialRowCount] = {ledgerTableCandidateConfig.SourceInitialRowCount}
                        ,[SourceFinalRowCount] = {ledgerTableCandidateConfig.SourceInitialRowCount}
                        ,[BITimeStamp] = GETDATE()
                    FROM [dbo].[{_ConfigTableName}] xx
                    WHERE [ConfigID] = {ledgerTableCandidateConfig.ConfigID}
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

        private static long UpdateLedgerTableCandidateConfig(SqlConnection cnArchConfig, LedgerTableCandidateConfig ledgerTableCandidateConfig)
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
                            [IsRunning] = {Convert.ToInt32(ledgerTableCandidateConfig.IsRunning)}
                            ,[LastRunStatus] = '{ledgerTableCandidateConfig.LastRunStatus}'
                            ,[SourceInitialRowCount] = {ledgerTableCandidateConfig.SourceInitialRowCount}
                            ,[SourceFinalRowCount] = {ledgerTableCandidateConfig.SourceInitialRowCount}
                            ,[BITimeStamp] = GETDATE()
                        FROM [dbo].[{_ConfigTableName}] xx
                        WHERE [ConfigID] = {ledgerTableCandidateConfig.ConfigID}
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

        private static long UpdateLedgerTableCandidateConfigIsEnabled(SqlConnection cnArchConfig, LedgerTableCandidateConfig ledgerTableCandidateConfig, bool isEnabled)
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
                            ,[AutoConfigDisable] = {(ledgerTableCandidateConfig.AutoConfigDisable == true ? 1 : 0)}
                            ,[BITimeStamp] = GETDATE()
                        FROM [dbo].[{_ConfigTableName}] xx
                        WHERE [ConfigID] = {ledgerTableCandidateConfig.ConfigID}
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

        private static long UpdateLedgerTableCandidateConfigInitialRowCount(SqlConnection cnArchConfig, LedgerTableCandidateConfig ledgerTableCandidateConfig)
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
                        [SourceInitialRowCount] = CASE WHEN [SourceInitialRowCount] = 0 THEN {ledgerTableCandidateConfig.SourceInitialRowCount} ELSE [SourceInitialRowCount] END
                        ,[SourceMinDate] = '{ledgerTableCandidateConfig.SourceMinDate}'
                        ,[SourceMaxDate] = '{ledgerTableCandidateConfig.SourceMaxDate}'
                        ,[BITimeStamp] = GETDATE()
                    FROM [dbo].[{_ConfigTableName}] xx
                    WHERE [ConfigID] = {ledgerTableCandidateConfig.ConfigID}
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

        public static int LogProcessInsert(SqlConnection cnArchConfig, string processName, long batchID, LedgerTableCandidateConfig ledgerTableCandidateConfig)
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
                           ,{ledgerTableCandidateConfig.ConfigID}
                           ,'{ledgerTableCandidateConfig.SourceServerName}'
                           ,'{ledgerTableCandidateConfig.SourceDatabaseName}'
                           ,'{ledgerTableCandidateConfig.SourceSchemaName}'
                           ,'{ledgerTableCandidateConfig.SourceTableName}'
                           ,'{ledgerTableCandidateConfig.TestModeNoDelete}'
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

        public static long LogProcessUpdate(SqlConnection cnArchConfig, LedgerTableCandidateConfig ledgerTableCandidateConfig)
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
                        SET [EndDate] = '{ledgerTableCandidateConfig.LastRunEndDate}'
                          ,[LastRunStatus] = '{ledgerTableCandidateConfig.LastRunStatus}'
                          ,[ConfigID] = {ledgerTableCandidateConfig.ConfigID}
                          ,[AutoConfigDisable] = {Convert.ToInt32(ledgerTableCandidateConfig.AutoConfigDisable)}
                          ,[IsTruncate] = {Convert.ToInt32(ledgerTableCandidateConfig.IsTruncate)}
                          ,[IsActive] = {Convert.ToInt32(ledgerTableCandidateConfig.IsActive)}
                          ,[IsEnabled] = {Convert.ToInt32(ledgerTableCandidateConfig.IsEnabled)}
                          ,[IsTestMode] = {Convert.ToInt32(ledgerTableCandidateConfig.IsTestMode)}
                          ,[TestModeNoDelete] = {Convert.ToInt32(ledgerTableCandidateConfig.TestModeNoDelete)}
                          ,[SourceServerName] = '{ledgerTableCandidateConfig.SourceServerName}'
                          ,[SourceDatabaseName] = '{ledgerTableCandidateConfig.SourceDatabaseName}'
                          ,[SourceSchemaName] = '{ledgerTableCandidateConfig.SourceSchemaName}'
                          ,[SourceTableName] = '{ledgerTableCandidateConfig.SourceTableName}'
                          ,[ValidationStatus] = '{ledgerTableCandidateConfig.ValidationStatus}'
                          ,[RowsRemaining] = {ledgerTableCandidateConfig.RowsRemaining}
                          ,[StatsOnly] = {Convert.ToInt32(ledgerTableCandidateConfig.StatsOnly)}
                          ,[SourceInitialRowCount] = {ledgerTableCandidateConfig.SourceInitialRowCount}
                          ,[SourceFinalRowCount] = {ledgerTableCandidateConfig.SourceInitialRowCount}
                        WHERE [ProcessID] = {ledgerTableCandidateConfig.ProcessID}
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

        //public static long LogProcessUpdateDeleteSeconds(SqlConnection cnArchConfig, LedgerTableCandidateConfig tailTableConfig)
        //{
        //    var startTime = DateTime.Now.ToLocalTime();
        //    Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
        //    long rowCount;
        //    try
        //    {
        //        var cmd = new SqlCommand
        //        {
        //            Connection = cnArchConfig,
        //            CommandType = CommandType.Text,
        //            CommandText = $@"
        //                UPDATE [Archiver].[LOG].[Process] 
	       //             SET 
        //                   [DeleteSeconds] = '{tailTableConfig.TruncateSeconds}'
        //                   ,[RowsDeleted] = '{tailTableConfig.RowsTruncated}'
	       //             WHERE [ProcessID] = {tailTableConfig.ProcessID}
        //            "
        //        };
        //        Logger.Info(Logging.DbCmdString(cmd));
        //        rowCount = cmd.ExecuteNonQuery();
        //        Logger.Info("ROWS: " + rowCount);
        //    }
        //    catch (Exception e)
        //    {
        //        Logger.Error("ERROR: " + cnArchConfig.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
        //        throw new ApplicationException(cnArchConfig.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
        //    }
        //    finally
        //    {
        //        Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
        //    }

        //    return rowCount;
        //}

        public static long LogProcessUpdateDone(SqlConnection cnArchConfig, int processID, LedgerTableCandidateConfig tailTableConfig)
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
                            ,[FinalRowCountCalculated] = {tailTableConfig.SourceInitialRowCount}
                            ,[FinalRowCount] = {tailTableConfig.SourceInitialRowCount}
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

        public static long LogProcessUpdateInitialRowCount(SqlConnection cnArchConfig, int processID, LedgerTableCandidateConfig tailTableConfig)
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

        public static LedgerTableCandidateConfigs GetLedgerTableCandidateConfigs(SqlConnection cnArchConfig, string groupName, string plantCode, string serverName, string databaseName, string schemaName, string tableName, bool useHighwaterMark, bool statsOnly)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Debug($"BEGIN: {DateTime.Now.ToLocalTime()}");
            var testMode = Convert.ToBoolean(ConfigurationManager.AppSettings["TestMode"]);
            var testModeNoDelete = Convert.ToBoolean(ConfigurationManager.AppSettings["TestModeNoDelete"]);
            var configs = new LedgerTableCandidateConfigs();
            var filters = new List<string>();
            var parameters = new List<SqlParameter>();
            try
            {
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
                var cmd = GetLedgerTableCandidateConfigs(filters);
                foreach (var parameter in parameters) cmd.Parameters.Add(parameter);
                cmd.Connection = cnArchConfig;
                Logger.Debug(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        var config = new LedgerTableCandidateConfig();
                        config.GroupName = reader.Get<string>("GroupName");
                        config.PlantCode = reader.Get<string>("PlantCode");
                        config.SourceServerName = reader.Get<string>("SourceServerName");
                        config.SourceDatabaseName = reader.Get<string>("SourceDatabaseName");
                        //config.SourceSchemaName = reader.Get<string>("SourceSchemaName");
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

        private static SqlCommand GetLedgerTableCandidateConfigs(List<string> filters)
        {
            var filterString = new StringBuilder();
            foreach (var filter in filters) filterString.Append(filter);
            var cmd = new SqlCommand();
            try
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = $@"
                    SELECT DISTINCT
                           [GroupName]
                          ,[PlantCode]
                          ,[SourceServerName]
                          ,[SourceDatabaseName]
                    FROM [dbo].[{_ConfigTableName}]
                    WHERE
                       {filterString} 
                    ORDER BY 
                         [GroupName]
                        ,[PlantCode]
                        ,[SourceServerName]
                        ,[SourceDatabaseName]
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
