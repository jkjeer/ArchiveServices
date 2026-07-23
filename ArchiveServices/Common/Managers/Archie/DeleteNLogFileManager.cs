using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using Microsoft.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using ArchiveServices.Common.Types.Archie;
using ArchiveServices.Common.Types.Enterprise;
using ArchiveServices.Common.Types.Response;
using ArchiveServices.Common.Utilities;
using NLog;

namespace ArchiveServices.Common.Managers.Archie
{
    public class DeleteNLogFileManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static ProcessResponse Process(int batchID, string batchName, DeleteLogFileConfig deleteLogFileConfig, SqlConnection cnCorpInt, SqlConnection cnArchConfig)
        {
             var startTime = DateTime.Now.ToLocalTime();
            var processName = $@"Delete NLog Files: {deleteLogFileConfig.ServerName}";
            var processID = ArchieLoggingManager.LogProcessInsert(cnArchConfig, processName, batchID, deleteLogFileConfig);
            deleteLogFileConfig.ProcessID = processID;
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            var response = new ProcessResponse($@"BatchID [{batchID}] {batchName} ProcessID [{processID}] {processName}", deleteLogFileConfig, true);
            response.BatchID = batchID;
            response.ProcessID = processID;
            response.ArchiveDate = startTime;
            response.ConnectionStrings.Add(new ConnectionString(cnArchConfig, "cnCorpInt", cnCorpInt.ConnectionString, batchID, processID));
            response.ConnectionStrings.Add(new ConnectionString(cnArchConfig, "cnArchConfig", cnArchConfig.ConnectionString, batchID, processID));
            response.ConnectionStrings.Add(new ConnectionString("smtp", ConfigurationManager.AppSettings["SmtpHost"]));
            response.ServerName = deleteLogFileConfig.ServerName;
            response.RetentionDays = deleteLogFileConfig.LogRetentionDays;
            var server = new Server();
            //var filePathNLog = $@"\\{deleteLogFileConfig.ServerName}\c$\temp\logs\";
            var filePathNLog = $@"\\{deleteLogFileConfig.ServerName}\{deleteLogFileConfig.Path}\";
            try
            {
                var stepValidateServer = new ProcessStep($@"Validate Server Configuration {deleteLogFileConfig.ServerName}");
                try
                {
                    server.ValidationErrors = DeleteLogFilesConfigManager.ValidateConfig(stepValidateServer.StepID, deleteLogFileConfig, cnArchConfig);
                    stepValidateServer.RowsAffected = server.ValidationErrors.Count;
                    response.RowsAffected = stepValidateServer.RowsAffected;
                }
                catch (Exception e)
                {
                    stepValidateServer.Errors.Add(new Error(e));
                }
                stepValidateServer.Done();
                response.ProcessSteps.Add(stepValidateServer);

                if (!server.ValidationErrors.Any())
                {
                    var stepFetchNLogDateDirectories = new ProcessStep($@"Fetch NLog Date Directories <{filePathNLog}>");
                    var dirNamesNLogDates = new List<string>();
                    try
                    {
                        var retentionDateStr = $"{DateTime.Now.AddDays(-deleteLogFileConfig.LogRetentionDays):yyyy-MM-dd}"; 
                        DirectoryInfo dirInfoNLogDates = new DirectoryInfo(filePathNLog);
                        var dirsNLogDates = dirInfoNLogDates.EnumerateDirectories().Where(dir => string.Compare(dir.Name, retentionDateStr, StringComparison.Ordinal) <= 0);
                        dirNamesNLogDates.AddRange(dirsNLogDates.Select(dir => dir.Name));
                        stepFetchNLogDateDirectories.RowsAffected = dirNamesNLogDates.Count;
                    }
                    catch (Exception e)
                    {
                        stepFetchNLogDateDirectories.Errors.Add(new Error(e));
                    }
                    stepFetchNLogDateDirectories.Done();
                    response.ProcessSteps.Add(stepFetchNLogDateDirectories);
                    if (dirNamesNLogDates.Any())
                    {
                        var stepFetchNLogServiceDirectories = new ProcessStep($@"Delete NLog Service Directories <{filePathNLog}>");
                        var stepsDeleteDateDirectories = new ProcessSteps();
                        try
                        {
                            foreach (var dirNameDate in dirNamesNLogDates)
                            {
                                var fullPathNLogDate = $@"{filePathNLog}{dirNameDate}\";
                                try
                                {
                                    DirectoryInfo dirInfoNLogService = new DirectoryInfo(fullPathNLogDate);
                                    var dirsNLogService = dirInfoNLogService.EnumerateDirectories();
                                    var dirNamesNLogService = new List<string>();
                                    dirNamesNLogService.AddRange(dirsNLogService.Select(dir => dir.Name));
                                    if (dirNamesNLogService.Any())
                                    {
                                        stepFetchNLogServiceDirectories.RowsAffected += dirNamesNLogService.Count;
                                        var stepDeleteNLogDateServiceDirectories = new ProcessStep($@"Delete NLog Date Service Directories <{fullPathNLogDate}>");
                                        var stepsDelete = new ProcessSteps();
                                        try
                                        {
                                            foreach (var dirNameNLogService in dirNamesNLogService)
                                            {
                                                var fullPathNLogService = $@"{fullPathNLogDate}{dirNameNLogService}\";
                                                try
                                                {
                                                    var stepDeleteNLogServiceDirectory = new ProcessStep($@"Delete <{fullPathNLogService}>");
                                                    DirectoryInfo di = new DirectoryInfo(fullPathNLogService);
                                                    stepDeleteNLogServiceDirectory.Size = di.EnumerateFiles().Sum(file => file.Length) / 1024 / 1024; //mb
                                                    stepDeleteNLogDateServiceDirectories.Size += stepDeleteNLogServiceDirectory.Size;
                                                    stepFetchNLogServiceDirectories.Size += stepDeleteNLogServiceDirectory.Size;
                                                    stepDeleteNLogServiceDirectory.RowsAffected = Directory.GetFiles(fullPathNLogService).Length;
                                                    stepDeleteNLogServiceDirectory.Done();
                                                    stepsDelete.Add(stepDeleteNLogServiceDirectory);
                                                    stepDeleteNLogDateServiceDirectories.RowsAffected += stepDeleteNLogServiceDirectory.RowsAffected;
                                                    response.RowsAffected += stepDeleteNLogServiceDirectory.RowsAffected;
                                                }
                                                catch (Exception e)
                                                {
                                                    stepDeleteNLogDateServiceDirectories.Errors.Add(new Error($"{fullPathNLogService} {e.Message} {e.StackTrace}"));
                                                }
                                            }
                                        }
                                        catch (Exception e)
                                        {
                                            stepDeleteNLogDateServiceDirectories.Errors.Add(new Error(e));
                                        }
                                        if (!Convert.ToBoolean(ConfigurationManager.AppSettings["TestModeNoDelete"]))
                                        {
                                            Directory.Delete(fullPathNLogDate, true);
                                        }
                                        Logger.Info($"Directory.Delete({fullPathNLogDate}, true)");
                                        stepDeleteNLogDateServiceDirectories.Done();
                                        stepsDeleteDateDirectories.Add(stepDeleteNLogDateServiceDirectories);
                                        stepsDeleteDateDirectories.AddRange(stepsDelete);
                                    }
                                }
                                catch (Exception e)
                                {
                                    stepFetchNLogServiceDirectories.Errors.Add(new Error($"{fullPathNLogDate} {e.Message} {e.StackTrace}"));
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            stepFetchNLogServiceDirectories.Errors.Add(new Error(e));
                        }
                        stepFetchNLogServiceDirectories.Done();
                        response.ProcessSteps.Add(stepFetchNLogServiceDirectories);
                        response.ProcessSteps.AddRange(stepsDeleteDateDirectories);
                        response.Size = stepFetchNLogServiceDirectories.Size;
                    }
                }
                else
                {
                    foreach (var validationError in server.ValidationErrors)
                    {
                        response.Errors.Add(new Error(validationError.Message));
                    }
                    //targetTable.TruncateStatus = "E";
                    //targetTable.TruncateMessage = targetTable.ValidationStatus;
                }
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
            return response;
        }

        private static SqlCommand GetDeleteLogFileConfig(List<string> filters)
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
                        ,[GroupName]
                        ,[ServerName]
                        ,[LogType]
                        ,[Path]
                        ,[FileExt]
                        ,[LogRetentionDays]
                        ,[IsActive]
                        ,[IsEnabled]
,[IsTestMode]
                        ,[IsRunning]
,[BatchID]
                        ,[ProcessID]
                        ,[CreatedDate]
                        ,[CreatedBy]
                        ,[LastEditBy]
                        ,[BITimeStamp]              
,[LastRunDate]
,[LastRunStatus]
                    FROM [dbo].[cfgDeleteLogs]
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

        public static DeleteLogFileConfigs GetDeleteLogFileConfigs(SqlConnection cnArchConfig, string groupName, string serverName, string logType)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            var configs = new DeleteLogFileConfigs();
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
                if (!string.IsNullOrEmpty(serverName))
                {
                    filters.Add("[ServerName] = @serverName AND \r\n");
                    parameters.Add(new SqlParameter("@serverName", SqlDbType.VarChar));
                    parameters.Last().Value = serverName;
                }
                if (!string.IsNullOrEmpty(groupName))
                {
                    filters.Add("[LogType] = @logType AND \r\n");
                    parameters.Add(new SqlParameter("@logType", SqlDbType.VarChar));
                    parameters.Last().Value = logType;
                }
                filters.Add("[IsActive] = 'TRUE' AND \r\n");
                filters.Add("[IsEnabled] = 'TRUE' AND \r\n");
                filters[filters.Count - 1] = filters.Last().ToUpper().Replace(" AND \r\n", "");
                var cmd = GetDeleteLogFileConfig(filters);
                foreach (var parameter in parameters) cmd.Parameters.Add(parameter);
                cmd.Connection = cnArchConfig;
                Logger.Info(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        var config = new DeleteLogFileConfig();
                        config.ConfigID = CastDbNull.To(reader["ConfigID"], 0);
                        config.GroupName = CastDbNull.To(reader["GroupName"], string.Empty).Trim();
                        config.ServerName = CastDbNull.To(reader["ServerName"], string.Empty).Trim();
                        config.LogType = CastDbNull.To(reader["LogType"], string.Empty).Trim();
                        config.Path = CastDbNull.To(reader["Path"], string.Empty).Trim();
                        config.FileExt = CastDbNull.To(reader["FileExt"], string.Empty).Trim();
                        config.LogRetentionDays = CastDbNull.To(reader["LogRetentionDays"], 0);
                        config.IsActive = CastDbNull.To(reader["IsActive"], false);
                        config.IsEnabled = CastDbNull.To(reader["IsEnabled"], false);
                        config.IsTestMode = CastDbNull.To(reader["IsTestMode"], false);
                        config.IsRunning = CastDbNull.To(reader["IsRunning"], false);
                        config.BatchID = CastDbNull.To(reader["BatchID"], 0);
                        config.ProcessID = CastDbNull.To(reader["ProcessID"], 0);
                        config.CreatedDate = reader.Get<DateTime>("CreatedDate");
                        config.CreatedBy = reader.Get<string>("CreatedBy");
                        config.LastEditBy = reader.Get<string>("LastEditBy");
                        config.LastRunStartDate = reader.Get<DateTime>("LastRunDate");
                        config.LastRunStatus = reader.Get<string>("LastRunStatus");
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

    }
}
