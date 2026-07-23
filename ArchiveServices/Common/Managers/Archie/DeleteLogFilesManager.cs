using ArchiveServices.Common.Managers.Enterprise;
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
using System.IO;
using System.Linq;
using System.Text;

namespace ArchiveServices.Common.Managers.Archie
{
    public static class DeleteLogFilesManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static ProcessResponse Process(int batchID, string batchName, DeleteLogFileConfig deleteLogFileConfig, SqlConnection cnCorpInt, SqlConnection cnArchConfig)
        {
            deleteLogFileConfig.LastRunStartDate = DateTime.Now.ToLocalTime();
            var processName = $@"Delete {deleteLogFileConfig.LogType} Log Files: {deleteLogFileConfig.ServerQualifiedPathName}";
            Logger.Info($"{processName} BEGIN: {DateTime.Now.ToLocalTime()}");
            deleteLogFileConfig.BatchID = batchID;
            deleteLogFileConfig.ProcessID = ArchieLoggingManager.LogProcessInsert(cnArchConfig, processName, batchID, deleteLogFileConfig); ;
            var response = new ProcessResponse($@"BatchID [{batchID}] {batchName} ProcessID [{deleteLogFileConfig.ProcessID}] {processName}", deleteLogFileConfig, true);
            response.BatchID = batchID;
            response.ProcessID = deleteLogFileConfig.ProcessID;
            response.ArchiveDate = deleteLogFileConfig.LastRunStartDate;
            response.ConnectionStrings.Add(new ConnectionString(cnArchConfig, "cnCorpInt", cnCorpInt.ConnectionString, batchID, deleteLogFileConfig.ProcessID));
            response.ConnectionStrings.Add(new ConnectionString(cnArchConfig, "cnArchConfig", cnArchConfig.ConnectionString, batchID, deleteLogFileConfig.ProcessID));
            response.ConnectionStrings.Add(new ConnectionString("smtp", ConfigurationManager.AppSettings["SmtpHost"]));
            response.ServerName = deleteLogFileConfig.ServerName;
            response.Path = deleteLogFileConfig.Path;
            response.RetentionDays = deleteLogFileConfig.LogRetentionDays;
            try
            {
                var stepValidateServer = new ProcessStep($@"Validate Server Configuration {deleteLogFileConfig.ServerQualifiedPathName}");
                try
                {
                    deleteLogFileConfig.ValidationErrors = DeleteLogFilesConfigManager.ValidateConfig(stepValidateServer.StepID, deleteLogFileConfig, cnArchConfig);
                    stepValidateServer.RowsAffected = 1;
                }
                catch (Exception e)
                {
                    stepValidateServer.Errors.Add(new Error(e));
                }
                stepValidateServer.Done();
                response.ProcessSteps.Add(stepValidateServer);

                if (deleteLogFileConfig.ValidationErrors.Any())
                {
                    deleteLogFileConfig.LastRunStatus = "Error";
                    deleteLogFileConfig.LastRunEndDate = DateTime.Now.ToLocalTime();
                    ArchieLoggingManager.LogProcessUpdate(cnArchConfig, deleteLogFileConfig);
                    foreach (var validationError in deleteLogFileConfig.ValidationErrors)
                    {
                        response.Errors.Add(new Error(validationError.Message));
                    }
                }

                if (!response.Errors.Any())
                {
                    deleteLogFileConfig.LastRunEndDate = DateTime.Now.ToLocalTime();
                    deleteLogFileConfig.IsRunning = true;
                    DeleteLogFilesConfigManager.UpdateDeleteLogFileConfig(cnArchConfig, deleteLogFileConfig);
                    var stepFetchLogFiles = new ProcessStep($@"Fetch {deleteLogFileConfig.LogType} Log Files under {deleteLogFileConfig.ServerQualifiedPathName}");
                    IEnumerable<FileInfo> fileInfoLogFiles = new FileInfo[0];
                    try
                    {
                        DirectoryInfo dirInfoLogsRoot = new DirectoryInfo(deleteLogFileConfig.ServerQualifiedPathName);
                        fileInfoLogFiles = dirInfoLogsRoot.EnumerateFiles();
                        stepFetchLogFiles.RowsAffected = fileInfoLogFiles.Count();
                    }
                    catch (Exception e)
                    {
                        stepFetchLogFiles.Errors.Add(new Error(e));
                        Logger.Error($@"ERROR: {e.Message}: {e.StackTrace}");
                    }
                    stepFetchLogFiles.Done();
                    response.ProcessSteps.Add(stepFetchLogFiles);
                    if (fileInfoLogFiles.Any())
                    {
                        try
                        {
                            foreach (var fileInfoLogFile in fileInfoLogFiles)
                            {
                                var stepsDeleteFiles = new ProcessSteps();
                                try
                                {
                                    if (fileInfoLogFile.LastWriteTime < deleteLogFileConfig.LogRetentionDate && fileInfoLogFile.Name.ToLower().Contains($@".{deleteLogFileConfig.FileExt.ToLower()}"))
                                    //if (fileInfoLogFile.LastWriteTime < deleteLogFileConfig.LogRetentionDate && fileInfoLogFile.Extension.ToLower().Replace(".", "") == deleteLogFileConfig.FileExt.ToLower().Replace(".", ""))
                                    {
                                        Logger.Info($"Delete {fileInfoLogFile.FullName}");
                                        var stepDeleteLogFile = new ProcessStep($@"Delete {fileInfoLogFile.FullName}");
                                        try
                                        {
                                            Logger.Info($"File.Delete({fileInfoLogFile.FullName}){(deleteLogFileConfig.IsTestMode ? " - TestModeNoDelete" : "")}");
                                            if (!deleteLogFileConfig.IsTestMode) fileInfoLogFile.Delete();
                                            stepDeleteLogFile.RowsAffected = 1;
                                            response.RowsAffected += 1;
                                        }
                                        catch (Exception e)
                                        {
                                            stepDeleteLogFile.Errors.Add(new Error($"{fileInfoLogFile.FullName} {e.Message} {e.StackTrace}"));
                                            Logger.Error($@"ERROR: {e.Message}: {e.StackTrace}");
                                        }
                                        stepDeleteLogFile.Done();
                                        stepsDeleteFiles.Add(stepDeleteLogFile);
                                        response.AddStep(stepDeleteLogFile);
                                    }
                                }
                                catch (Exception e)
                                {
                                    Logger.Error($@"ERROR: {e.Message}: {e.StackTrace}");
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            response.Errors.Add(new Error(e));
                            Logger.Error($@"ERROR: {e.Message}: {e.StackTrace}");
                        }
                    }
                }
                else
                {
                    foreach (var validationError in deleteLogFileConfig.ValidationErrors)
                    {
                        response.Errors.Add(new Error(validationError.Message));
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
                deleteLogFileConfig.IsRunning = false;
                deleteLogFileConfig.LastRunStatus = response.Errors.Any() ? "Error" : "Success";
                deleteLogFileConfig.LastRunEndDate = DateTime.Now.ToLocalTime();
                DeleteLogFilesConfigManager.UpdateDeleteLogFileConfig(cnArchConfig, deleteLogFileConfig);
                Logger.Info($@"END: {Convert.ToDecimal((deleteLogFileConfig.LastRunEndDate - deleteLogFileConfig.LastRunStartDate).TotalSeconds)} SECONDS");
            }
            return response;
        }

        public static string GetBatchName(string actionName, string groupName, string serverName, string logType)
        {
            string batchName;
            if (!string.IsNullOrEmpty(groupName))
            {
                if (!string.IsNullOrEmpty(groupName))
                {
                    batchName = $@"{actionName} Plant: [{groupName}]";
                }
                else if (!string.IsNullOrEmpty(serverName))
                {
                    batchName = $@"{actionName} Group: [{serverName}]";
                }
                else
                {
                    batchName = $@"{actionName}";
                    Logger.Error($@"ERROR: {actionName}");
                    throw new Exception(batchName);
                }
            }
            else if (!string.IsNullOrEmpty(serverName))
            {
                batchName = $@"{actionName} Logs for Group: {groupName}";
            }
            else if (!string.IsNullOrEmpty(serverName))
            {
                batchName = $@"{actionName} Logs for Server: {serverName}";
            }
            else
            {
                batchName = $@"{actionName} All Logs";
            }
            Logger.Info(batchName);
            return batchName;
        }


        public static Servers GetPlantConfig(SqlConnection cnArchConfig, string plantName)
        {
            var servers = new Servers();
            var filters = new List<string>();
            var parameters = new List<SqlParameter>();
            try
            {
                filters.Add("[Plant] = @plant");
                parameters.Add(new SqlParameter("@plant", SqlDbType.VarChar));
                parameters.Last().Value = plantName;
                if (filters.Any())
                {
                    var cmd = GetPlantConfig(filters);
                    foreach (var parameter in parameters) cmd.Parameters.Add(parameter);
                    cmd.Connection = cnArchConfig;
                    //cmd.CommandTimeout = int.Parse(ConfigurationManager.AppSettings["BoxingQueryTimeout"]);
                    Logger.Trace(Logging.DbCmdString(cmd));
                    using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                    {
                        while (reader.Read())
                        {
                            var server = new Server();
                            server.ServerName = CastDbNull.To(reader["ServerName"], string.Empty).Trim();
                            server.NetBiosName = CastDbNull.To(reader["NetBiosName"], string.Empty).Trim();
                            server.WorkloadType = CastDbNull.To(reader["WorkloadType"], string.Empty).Trim();
                            server.ServerFunction = CastDbNull.To(reader["ServerFunction"], string.Empty).Trim();
                            server.DeploymentMode = CastDbNull.To(reader["DeploymentMode"], string.Empty).Trim();
                            server.ServerType = CastDbNull.To(reader["ServerType"], string.Empty).Trim();
                            server.IsActive = CastDbNull.To(reader["IsActive"], false);
                            server.IsEnabled = CastDbNull.To(reader["IsEnabled"], false);
                            server.IsSQL = CastDbNull.To(reader["IsSQL"], false);
                            server.DeleteLogFiles = CastDbNull.To(reader["DeleteLogFiles"], false);
                            server.LogFileRetentionDays = CastDbNull.To(reader["LogFileRetentionDays"], 0);
                            server.Plant = PlantManager.GetPlant(CastDbNull.To(reader["Plant"], string.Empty).Trim());
                            servers.Add(server);
                        }

                        Logger.Trace($"Rows: {servers.Count}");
                        reader.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnArchConfig.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new Exception($"{cnArchConfig.ConnectionString}  {e.Message}: {e.StackTrace}");
            }

            return servers;
        }

        private static SqlCommand GetPlantConfig(List<string> filters)
        {
            var filterString = new StringBuilder();
            foreach (var filter in filters) filterString.Append(filter);
            var cmd = new SqlCommand();
            try
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = $@"
                SELECT
                    [ID]
                    ,[Plant]
                    ,[ServerName]
                    ,[NetBiosName]
                    ,[WorkloadType]
                    ,[ServerFunction]
                    ,[DeploymentMode]
                    ,[ServerType]
                    ,[IsActive]
                    ,[IsEnabled]
                    ,[IsSQL]
                    ,[DeleteLogFiles]
                    ,[LogFileRetentionDays]
                    ,[BITimeStamp]
                    ,[PlantCode]                
                FROM
                    [dbo].[cfgServersLogDelete]
                WHERE
                     {filterString} 
                ";
            }
            catch (Exception e)
            {
                throw new Exception(e.ToString());
            }

            return cmd;
        }

        public static Server GetServerConfig(SqlConnection cnArchConfig, string serverName)
        {
            var server = new Server();
            var filters = new List<string>();
            var parameters = new List<SqlParameter>();
            try
            {
                filters.Add("[ServerName] = @serverName");
                parameters.Add(new SqlParameter("@serverName", SqlDbType.VarChar));
                parameters.Last().Value = serverName;
                if (filters.Any())
                {
                    var cmd = GetServerConfig(filters);
                    foreach (var parameter in parameters) cmd.Parameters.Add(parameter);
                    cmd.Connection = cnArchConfig;
                    //cmd.CommandTimeout = int.Parse(ConfigurationManager.AppSettings["BoxingQueryTimeout"]);
                    Logger.Trace(Logging.DbCmdString(cmd));
                    using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                    {
                        while (reader.Read())
                        {
                            server.ServerName = CastDbNull.To(reader["ServerName"], string.Empty).Trim();
                            server.NetBiosName = CastDbNull.To(reader["NetBiosName"], string.Empty).Trim();
                            server.WorkloadType = CastDbNull.To(reader["WorkloadType"], string.Empty).Trim();
                            server.ServerFunction = CastDbNull.To(reader["ServerFunction"], string.Empty).Trim();
                            server.DeploymentMode = CastDbNull.To(reader["DeploymentMode"], string.Empty).Trim();
                            server.ServerType = CastDbNull.To(reader["ServerType"], string.Empty).Trim();
                            server.IsActive = CastDbNull.To(reader["IsActive"], false);
                            server.IsEnabled = CastDbNull.To(reader["IsEnabled"], false);
                            server.IsSQL = CastDbNull.To(reader["IsSQL"], false);
                            server.DeleteLogFiles = CastDbNull.To(reader["DeleteLogFiles"], false);
                            server.LogFileRetentionDays = CastDbNull.To(reader["LogFileRetentionDays"], 0);
                            server.Plant = PlantManager.GetPlant(CastDbNull.To(reader["Plant"], string.Empty).Trim());
                        }

                        reader.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnArchConfig.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new Exception($"{cnArchConfig.ConnectionString}  {e.Message}: {e.StackTrace}");
            }

            return server;
        }

        private static SqlCommand GetServerConfig(List<string> filters)
        {
            var filterString = new StringBuilder();
            foreach (var filter in filters) filterString.Append(filter);
            var cmd = new SqlCommand();
            try
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = $@"
                SELECT
                    [ID]
                    ,[Plant]
                    ,[ServerName]
                    ,[NetBiosName]
                    ,[WorkloadType]
                    ,[ServerFunction]
                    ,[DeploymentMode]
                    ,[ServerType]
                    ,[IsActive]
                    ,[IsEnabled]
                    ,[IsSQL]
                    ,[DeleteLogFiles]
                    ,[LogFileRetentionDays]
                    ,[BITimeStamp]
                    ,[PlantCode]                
                FROM
                    [dbo].[cfgServersLogDelete]
                WHERE
                     {filterString} 
                ";
            }
            catch (Exception e)
            {
                throw new Exception(e.ToString());
            }

            return cmd;
        }

        public static ValidationErrors Validate(Server server, int requestedRetentionDays)
        {
            var messages = new ValidationErrors();
            var minRetentionDays = Convert.ToInt32(ConfigurationManager.AppSettings["MinLogRetentionDays"]);
            if (requestedRetentionDays < server.LogFileRetentionDays)
            {
                var err = new ValidationError($"Server {server.ServerName} Requested Retention Days {requestedRetentionDays} is less than server {server.ServerName} is configuration.", "Validation", "Fatal", true);
                messages.Add(err);
            }

            if (requestedRetentionDays < minRetentionDays)
            {
                var err = new ValidationError($"Server {server.ServerName} Requested Retention Days {requestedRetentionDays} is less than Minimum Retention Days.", "Validation", "Fatal", true);
                messages.Add(err);
            }

            if (!server.DeleteLogFiles)
            {
                var err = new ValidationError($"Server {server.ServerName} is not configured for DeleteLogFiles.", "Validation", "Fatal", true);
                messages.Add(err);
            }

            if (!server.IsActive)
            {
                var err = new ValidationError($"Server {server.ServerName} is Inactive.", "Validation", "Fatal", true);
                messages.Add(err);
            }

            if (!server.IsEnabled)
            {
                var err = new ValidationError($"Server {server.ServerName} is Disabled.", "Validation", "Fatal", true);
                messages.Add(err);
            }

            return messages;
        }


    }
}