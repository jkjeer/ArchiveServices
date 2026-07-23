using System;
using System.Collections.Generic;
using System.Configuration;
using Microsoft.Data.SqlClient;
using System.IO;
using System.Linq;
using ArchiveServices.Common.Types.Archie;
using ArchiveServices.Common.Types.Enterprise;
using ArchiveServices.Common.Types.Response;
using NLog;

namespace ArchiveServices.Common.Managers.Archie
{
    public class DeletePSLogFileManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static Types.Archie.ProcessResponse Process(SqlConnection cnCorpInt, SqlConnection cnArchConfig, string serverName, bool sendReport)
        {
            var retentionDays = 0;
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            var response = new Types.Archie.ProcessResponse($"Delete PSLog Files: {serverName} Retention Days: {retentionDays}", true);
            if (Convert.ToBoolean(ConfigurationManager.AppSettings["TestModeNoDelete"])) response.ProcessName = $"{response.ProcessName} - Test Mode No Delete";
            response.ServerName = serverName;
            response.RetentionDays = retentionDays;
            response.ConnectionStrings.Add(new ConnectionString("cnArchConfig", cnArchConfig.ConnectionString));
            response.ConnectionStrings.Add(new ConnectionString("cnCorpInt", cnCorpInt.ConnectionString));
            response.ConnectionStrings.Add(new ConnectionString("smtp", ConfigurationManager.AppSettings["SmtpHost"]));
            var server = new Server();
            var filePathPSLog = $@"\\{serverName}\s$\scripts\logs\";
            try
            {
                var step1GetServerConfig = new ProcessStep($@"Step 1. Get Server Config {serverName}");
                try
                {
                    server = DeleteLogDirectoriesManager.GetServerConfig(cnArchConfig, serverName);
                    step1GetServerConfig.RowsAffected = 1;
                    response.RowsAffected = step1GetServerConfig.RowsAffected;
                }
                catch (Exception e)
                {
                    step1GetServerConfig.Errors.Add(new Error(e));
                }
                step1GetServerConfig.Done();
                response.ProcessSteps.Add(step1GetServerConfig);

                var step2ValidateServer = new ProcessStep($@"Step 2. Validate Server Configuration {serverName}");
                try
                {
                    server.ValidationErrors = DeleteLogDirectoriesManager.Validate(server, retentionDays);
                    step2ValidateServer.RowsAffected = server.ValidationErrors.Count;
                    response.RowsAffected = step2ValidateServer.RowsAffected;
                }
                catch (Exception e)
                {
                    step2ValidateServer.Errors.Add(new Error(e));
                }
                step2ValidateServer.Done();
                response.ProcessSteps.Add(step2ValidateServer);

                if (!server.ValidationErrors.Any())
                {
                    var step3FetchPSLogDateDirectories = new ProcessStep($@"Step 3. Fetch PSLog Date Directories <{filePathPSLog}>");
                    var dirNamesPSLogDates = new List<string>();
                    try
                    {
                        var retentionDateStr = $"{DateTime.Now.AddDays(-retentionDays):yyyyMMdd}";
                        DirectoryInfo dirInfoPSLogDates = new DirectoryInfo(filePathPSLog);
                        var dirsPSLogDates = dirInfoPSLogDates.EnumerateDirectories().Where(dir => string.Compare(dir.Name, retentionDateStr, StringComparison.Ordinal) <= 0);
                        dirNamesPSLogDates.AddRange(dirsPSLogDates.Select(dir => dir.Name));
                        step3FetchPSLogDateDirectories.RowsAffected = dirNamesPSLogDates.Count;
                    }
                    catch (Exception e)
                    {
                        step3FetchPSLogDateDirectories.Errors.Add(new Error(e));
                    }
                    step3FetchPSLogDateDirectories.Done();
                    response.ProcessSteps.Add(step3FetchPSLogDateDirectories);
                    if (dirNamesPSLogDates.Any())
                    {
                        var step4DeletePSLogDateFiles = new ProcessStep($@"Step 4. Delete PSLog Date Files");
                        var stepsDelete = new ProcessSteps();
                        try
                        {
                            foreach (var dirNameDate in dirNamesPSLogDates)
                            {
                                var fullPathPSLogDate = $@"{filePathPSLog}{dirNameDate}\";
                                var step5DeletePSLogDirectory = new ProcessStep($@"Delete PSLog Date Directory <{fullPathPSLogDate}>");
                                try
                                {
                                    DirectoryInfo di = new DirectoryInfo(fullPathPSLogDate);
                                    step5DeletePSLogDirectory.Size = di.EnumerateFiles().Sum(file => file.Length) / 1024 / 1024; //mb
                                    step4DeletePSLogDateFiles.Size += step5DeletePSLogDirectory.Size;
                                    step5DeletePSLogDirectory.RowsAffected = Directory.GetFiles(fullPathPSLogDate).Length;
                                    step4DeletePSLogDateFiles.RowsAffected += step5DeletePSLogDirectory.RowsAffected;
                                }
                                catch (Exception e)
                                {
                                    step5DeletePSLogDirectory.Errors.Add(new Error(e));
                                }
                                Logger.Info($"Directory.Delete({fullPathPSLogDate}, true)");
                                if (!Convert.ToBoolean(ConfigurationManager.AppSettings["TestModeNoDelete"])) Directory.Delete(fullPathPSLogDate, true);
                                step5DeletePSLogDirectory.Done();
                                stepsDelete.Add(step5DeletePSLogDirectory);
                            }
                        }
                        catch (Exception e)
                        {
                            step4DeletePSLogDateFiles.Errors.Add(new Error(e));
                        }
                        step4DeletePSLogDateFiles.Done();
                        response.ProcessSteps.Add(step4DeletePSLogDateFiles);
                        response.ProcessSteps.AddRange(stepsDelete);
                        response.Size = step4DeletePSLogDateFiles.Size;
                        response.RowsAffected = step4DeletePSLogDateFiles.RowsAffected;
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

    }
}
