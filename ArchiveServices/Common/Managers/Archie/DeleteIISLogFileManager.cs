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
    public class DeleteIISLogFileManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static Types.Archie.ProcessResponse Process(int batchID, string batchName, DeleteLogFileConfig deleteLogFileConfig, SqlConnection cnCorpInt, SqlConnection cnArchConfig)
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
            var filePathIISLog = $@"\\{deleteLogFileConfig.ServerName}\c$\inetpub\logs\LogFiles\W3SVC1\";
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
                    var stepFetchIISLogFiles = new ProcessStep($@"Fetch IISLog Files <{filePathIISLog}>");
                    var fileNamesIISLog = new List<string>();
                    try
                    {
                        var retentionDateStr = $"{DateTime.Now.AddDays(-deleteLogFileConfig.LogRetentionDays):yyyy-MM-dd}";
                        var retentionDate = DateTime.Now.AddDays(-deleteLogFileConfig.LogRetentionDays);
                        DirectoryInfo dirInfoIISLog = new DirectoryInfo(filePathIISLog);
                        var filesIISLog = dirInfoIISLog.EnumerateFiles().Where(file => file.Extension == ".log" && file.LastWriteTime <= retentionDate).OrderBy(p => p.LastWriteTime);
                        fileNamesIISLog.AddRange(filesIISLog.Select(file => file.Name));
                        stepFetchIISLogFiles.RowsAffected = fileNamesIISLog.Count;
                    }
                    catch (Exception e)
                    {
                        stepFetchIISLogFiles.Errors.Add(new Error(e));
                    }
                    stepFetchIISLogFiles.Done();
                    response.ProcessSteps.Add(stepFetchIISLogFiles);

                    if (fileNamesIISLog.Any())
                    {
                        var stepDeleteIISLogFiles = new ProcessStep($@"Delete IIS Service Directories <{filePathIISLog}>");
                        var stepsDelete = new ProcessSteps();
                        try
                        {
                            foreach (var fileName in fileNamesIISLog)
                            {
                                var fullPathIISLog = $@"{filePathIISLog}{fileName}";
                                if (File.Exists(fullPathIISLog))
                                {
                                    try
                                    {
                                        FileInfo fi = new FileInfo(fullPathIISLog);
                                        var stepDeleteIISLogFile = new ProcessStep($@"Delete <{fullPathIISLog}> {fi.LastWriteTime}");
                                        stepDeleteIISLogFile.Size = fi.Length / 1024 / 1024; //mb
                                        stepDeleteIISLogFiles.Size += stepDeleteIISLogFile.Size;
                                        stepDeleteIISLogFile.RowsAffected = 1;
                                        stepsDelete.Add(stepDeleteIISLogFile);
                                        stepDeleteIISLogFiles.RowsAffected += stepDeleteIISLogFile.RowsAffected;
                                        response.RowsAffected += stepDeleteIISLogFile.RowsAffected;
                                        Logger.Info($"File.Delete({fullPathIISLog}, true)");
                                        if (!Convert.ToBoolean(ConfigurationManager.AppSettings["TestModeNoDelete"])) File.Delete(fullPathIISLog);
                                        stepDeleteIISLogFile.Done();
                                    }
                                    catch (Exception e)
                                    {
                                        stepDeleteIISLogFiles.Errors.Add(new Error($"{fullPathIISLog} {e.Message} {e.StackTrace}"));
                                    }
                                }
                                else
                                {
                                    stepDeleteIISLogFiles.Errors.Add(new Error($"{fullPathIISLog} not found.", false));
                                    stepDeleteIISLogFiles.RowsAffected = fileNamesIISLog.Count; //+= InsertDocDeleteError(cnTCCSUPPORT, doc, $"{doc.Filter} document {doc.fsdDocID} PDF not found.", deleteGUID);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            stepDeleteIISLogFiles.Errors.Add(new Error(e));
                        }
                        stepDeleteIISLogFiles.Done();
                        response.ProcessSteps.Add(stepDeleteIISLogFiles);
                        response.ProcessSteps.AddRange(stepsDelete);
                        response.Size = stepDeleteIISLogFiles.Size;
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
                        ,[IsActive]
                        ,[IsEnabled]
                        ,[IsRunning]
                        ,[LogRetentionDays]
                        ,[DeleteNLog]
                        ,[DeleteIISLog]
                        ,[DeleteVelocityADLog]
                        ,[DeleteScriptLog]
                        ,[CreatedDate]
                        ,[CreatedBy]
                        ,[LastEditBy]
                        ,[BITimeStamp]                    
                    FROM [dbo].[cfgServersLogDelete]
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

        public static DeleteLogFileConfigs GetDeleteLogFileConfigs(SqlConnection cnArchConfig, string groupName, string serverName)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            var configs = new DeleteLogFileConfigs();
            var filters = new List<string>();
            var parameters = new List<SqlParameter>();
            try
            {
                filters.Add("[IsActive] = 'TRUE' AND \r\n");

                filters.Add("[IsEnabled] = 'TRUE' AND \r\n");

                filters.Add("[DeleteIISLog] = 'TRUE' AND \r\n");

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
                        config.IsActive = CastDbNull.To(reader["IsActive"], false);
                        config.IsEnabled = CastDbNull.To(reader["IsEnabled"], false);
                        config.IsRunning = CastDbNull.To(reader["IsRunning"], false);
                        config.LogRetentionDays = CastDbNull.To(reader["LogRetentionDays"], 0);
                        config.CreatedBy = reader.Get<string>("CreatedBy");
                        config.LastEditBy = reader.Get<string>("LastEditBy");
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
