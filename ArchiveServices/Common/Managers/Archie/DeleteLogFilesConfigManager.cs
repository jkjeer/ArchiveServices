using ArchiveServices.Common.Types.Archie;
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
    public class DeleteLogFilesConfigManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static DeleteLogFileConfigs GetDeleteLogFileConfigs(SqlConnection cnArchConfig, string groupName, string serverName, string logType)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            var testMode = Convert.ToBoolean(ConfigurationManager.AppSettings["TestModeConfig"]);
            var testModeNoDelete = Convert.ToBoolean(ConfigurationManager.AppSettings["TestModeNoDelete"]);
            var configs = new DeleteLogFileConfigs();
            var filters = new List<string>();
            var parameters = new List<SqlParameter>();
            try
            {
                filters.Add("[IsActive] = 1 AND \r\n");
                filters.Add("[IsEnabled] = 1 AND \r\n");
                //filters.Add("[IsTestMode] = 0 AND \r\n");

                if (!string.IsNullOrEmpty(groupName))
                {
                    filters.Add("cd.[GroupName] = @groupName AND \r\n");
                    parameters.Add(new SqlParameter("@groupName", SqlDbType.VarChar));
                    parameters.Last().Value = groupName;
                }
                if (!string.IsNullOrEmpty(serverName))
                {
                    filters.Add("cd.[ServerName] = @serverName AND \r\n");
                    parameters.Add(new SqlParameter("@serverName", SqlDbType.VarChar));
                    parameters.Last().Value = serverName;
                }
                if (!string.IsNullOrEmpty(logType))
                {
                    filters.Add("cd.[LogType] = @logType AND \r\n");
                    parameters.Add(new SqlParameter("@logType", SqlDbType.VarChar));
                    parameters.Last().Value = logType;
                }
                filters[filters.Count - 1] = filters.Last().ToUpper().Replace(" AND \r\n", "");
                var cmd = GetDeleteLogFileConfigs(filters);
                foreach (var parameter in parameters) cmd.Parameters.Add(parameter);
                cmd.Connection = cnArchConfig;
                Logger.Info(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        var config = new DeleteLogFileConfig();
                        config.ConfigID = reader.Get<int>("ConfigID");
                        config.GroupName = reader.Get<string>("GroupName");
                        config.ServerName = reader.Get<string>("ServerName");
                        config.LogType = reader.Get<string>("LogType");
                        config.DeleteDirectories = reader.Get<bool>("DeleteDirectories");
                        
                        config.Path = reader.Get<string>("Path");
                        config.FileExt = reader.Get<string>("FileExt");
                        config.LogRetentionDays = reader.Get<int>("LogRetentionDays");
                        config.IsActive = reader.Get<bool>("IsActive");
                        config.IsEnabled = reader.Get<bool>("IsEnabled");
                        config.IsTestMode = reader.Get<bool>("IsTestMode");
                        config.IsRunning = reader.Get<bool>("IsRunning");
                        config.BatchID = reader.Get<int>("BatchID");
                        config.ProcessID = reader.Get<int>("ProcessID");
                        config.CreatedDate = reader.Get<DateTime>("CreatedDate");
                        config.CreatedBy = reader.Get<string>("CreatedBy");
                        config.LastEditBy = reader.Get<string>("LastEditBy");
                        config.LastRunStartDate = reader.Get<DateTime>("LastRunStartDate");
                        config.LastRunEndDate = reader.Get<DateTime>("LastRunEndDate");
                        config.LastRunStatus = reader.Get<string>("LastRunStatus");
                        if (testModeNoDelete || testMode) config.IsTestMode = true;
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
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return configs;
        }

        private static SqlCommand GetDeleteLogFileConfigs(List<string> filters)
        {
            var filterString = new StringBuilder();
            foreach (var filter in filters) filterString.Append(filter);
            var cmd = new SqlCommand();
            try
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = $@"
                    SELECT
                        cd.[ConfigID]
                        ,cd.[GroupName]
                        ,cd.[ServerName]
                        ,cd.[LogType]
                        ,cd.[Path]
                        ,cd.[FileExt]
                        ,cd.[LogRetentionDays]
                        ,lt.[DeleteDirectories]
                        ,cd.[IsActive]
                        ,cd.[IsEnabled]
                        ,cd.[IsTestMode]
                        ,cd.[IsRunning]
                        ,cd.[BatchID]
                        ,cd.[ProcessID]
                        ,cd.[CreatedDate]
                        ,cd.[CreatedBy]
                        ,cd.[LastEditBy]
                        ,cd.[BITimeStamp]              
                        ,cd.[LastRunStartDate]
                        ,cd.[LastRunEndDate]
                        ,cd.[LastRunStatus]
                    FROM [dbo].[cfgDeleteLogFiles] cd
                    LEFT OUTER JOIN [Archiver].[dbo].[LogType] lt
                        ON lt.[LogType] = cd.[LogType]
                    WHERE
                       {filterString} 
                    ORDER BY [ConfigID]
                ";
            }
            catch (Exception e)
            {
                throw new Exception(e.ToString());
            }
            return cmd;
        }

        public static ValidationErrors ValidateConfig(int stepID, DeleteLogFileConfig deleteLogFileConfig, SqlConnection cnArchConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {startTime}");
            var validationErrors = new ValidationErrors();
            try
            {
                var minLogRetentionDays = Convert.ToInt32(ConfigurationManager.AppSettings["MinLogRetentionDays"]);
                if (string.IsNullOrEmpty(deleteLogFileConfig.ServerName))
                {
                    var validationError = new ValidationError($"{deleteLogFileConfig.ServerName} {deleteLogFileConfig.LogType} {deleteLogFileConfig.Path} is not found in [Archiver].[dbo].[cfgDeleteLogFiles].", "Validation", "Fatal", true);
                    validationErrors.Add(validationError);
                    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                    deleteLogFileConfig.ValidationStatus = validationError.Message;
                    Logger.Error(validationError.Message);
                }
                else
                {
                    if (deleteLogFileConfig.IsRunning)
                    {
                        var validationError = new ValidationError($"Delete of {deleteLogFileConfig.ServerName} {deleteLogFileConfig.LogType} {deleteLogFileConfig.Path} is already Running.", "Validation", "Fatal", true);
                        validationErrors.Add(validationError);
                        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                        deleteLogFileConfig.ValidationStatus = validationError.Message;
                    }
                    if (!deleteLogFileConfig.IsActive)
                    {
                        var validationError = new ValidationError($"{deleteLogFileConfig.ServerName} {deleteLogFileConfig.LogType} {deleteLogFileConfig.Path} is Inactive.", "Validation", "Fatal", true);
                        validationErrors.Add(validationError);
                        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                        deleteLogFileConfig.ValidationStatus = validationError.Message;
                        Logger.Error(validationError.Message);
                    }
                    if (!deleteLogFileConfig.IsEnabled)
                    {
                        var validationError = new ValidationError($"{deleteLogFileConfig.ServerName} {deleteLogFileConfig.LogType} {deleteLogFileConfig.Path} is Disabled.", "Validation", "Fatal", true);
                        validationErrors.Add(validationError);
                        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                        deleteLogFileConfig.ValidationStatus = validationError.Message;
                        Logger.Error(validationError.Message);
                    }
                    if (deleteLogFileConfig.LogRetentionDays < minLogRetentionDays)
                    {
                        var validationError = new ValidationError($"{deleteLogFileConfig.ServerName} configured RetentionDays is less than the minimum ({minLogRetentionDays}).", "Validation", "Fatal", true);
                        validationErrors.Add(validationError);
                        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                        deleteLogFileConfig.ValidationStatus = validationError.Message;
                        Logger.Error(validationError.Message);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error($@"ERROR: {cnArchConfig.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new ApplicationException($@"{cnArchConfig.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            finally
            {
                if (!validationErrors.Any()) deleteLogFileConfig.ValidationStatus = "Valid";
                Logger.Info($"END: {Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds)} SECONDS");
            }
            return validationErrors;
        }

        public static long UpdateDeleteLogFileConfig(SqlConnection cnArchConfig, DeleteLogFileConfig deleteLogFileConfig)
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
                            [IsRunning] = {Convert.ToInt32(deleteLogFileConfig.IsRunning)}
                            ,[BatchID] = {deleteLogFileConfig.BatchID}
                            ,[ProcessID] = {deleteLogFileConfig.ProcessID}
                            ,[LastRunStatus] = '{deleteLogFileConfig.LastRunStatus}'
                            ,[LastRunStartDate] = '{deleteLogFileConfig.LastRunStartDate}'
                            ,[LastRunEndDate] = '{deleteLogFileConfig.LastRunEndDate}'
                            ,[BITimeStamp] = GETDATE()
                        FROM [dbo].[cfgDeleteLogFiles] xx
                        WHERE [ConfigID] = {deleteLogFileConfig.ConfigID}
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

    }
}
