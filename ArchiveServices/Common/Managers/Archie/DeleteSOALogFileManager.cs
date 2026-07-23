using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using ArchiveServices.Common.Types.Archie;
using ArchiveServices.Common.Types.Enterprise;
using ArchiveServices.Common.Types.Response;
using NLog;
// ReSharper disable TooWideLocalVariableScope
// ReSharper disable StringLiteralTypo
// ReSharper disable InconsistentNaming

namespace ArchiveServices.Common.Managers.Archie
{
    public class DeleteSOALogFileManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static Types.Archie.ProcessResponse Process(SqlConnection cnArchConfig, string serverName, bool sendReport, string serviceName)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            var response = new Types.Archie.ProcessResponse($"Delete SOALog Files: {serverName} Retention Days: {retentionDays}", true);
            if (Convert.ToBoolean(ConfigurationManager.AppSettings["TestModeNoDelete"])) response.ProcessName = $"{response.ProcessName} - Test Mode No Delete";
            response.ServerName = serverName;
            response.RetentionDays = retentionDays;
            response.ConnectionStrings.Add(new ConnectionString("cnArchConfig", cnArchConfig.ConnectionString));
            response.ConnectionStrings.Add(new ConnectionString("smtp", ConfigurationManager.AppSettings["SmtpHost"]));
            var server = new Server();
            try
            {
                var filePathSOALog = $@"\\{serverName}\s$\scripts\FGServices\{serviceName}\logs\";
                var step1GetServerConfig = new ProcessStep($@"Step 1. Get Server Config {serverName}");
                try
                {
                    server = LogDeleteManager.GetServerConfig(cnArchConfig, serverName);
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
                    server.ValidationErrors = LogDeleteManager.Validate(server, retentionDays);
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
                    var step3FetchSOALogDateDirectories = new ProcessStep($@"Step 3. Fetch SOALog Date Directories <{filePathSOALog}>");
                    var dirNamesSOALogDates = new List<string>();
                    try
                    {
                        var retentionDateStr = $"{DateTime.Now.AddDays(-retentionDays):yyyyMMdd}";
                        DirectoryInfo dirInfoSOALogDates = new DirectoryInfo(filePathSOALog);
                        var dirsSOALogDates = dirInfoSOALogDates.EnumerateDirectories().Where(dir => string.Compare(dir.Name, retentionDateStr, StringComparison.Ordinal) <= 0);
                        dirNamesSOALogDates.AddRange(dirsSOALogDates.Select(dir => dir.Name));
                        step3FetchSOALogDateDirectories.RowsAffected = dirNamesSOALogDates.Count;
                    }
                    catch (Exception e)
                    {
                        step3FetchSOALogDateDirectories.Errors.Add(new Error(e));
                    }
                    step3FetchSOALogDateDirectories.Done();
                    response.ProcessSteps.Add(step3FetchSOALogDateDirectories);
                    if (dirNamesSOALogDates.Any())
                    {
                        var step4DeleteSOALogDateFiles = new ProcessStep($@"Step 4. Delete SOALog Date Files");
                        var stepsDelete = new ProcessSteps();
                        try
                        {
                            foreach (var dirNameDate in dirNamesSOALogDates)
                            {
                                var fullPathSOALogDate = $@"{filePathSOALog}{dirNameDate}\";
                                var step5DeleteSOALogDirectory = new ProcessStep($@"Delete SOALog Date Directory <{fullPathSOALogDate}>");
                                try
                                {
                                    DirectoryInfo di = new DirectoryInfo(fullPathSOALogDate);
                                    step5DeleteSOALogDirectory.Size = di.EnumerateFiles().Sum(file => file.Length) / 1024 / 1024; //mb
                                    step4DeleteSOALogDateFiles.Size += step5DeleteSOALogDirectory.Size;
                                    step5DeleteSOALogDirectory.RowsAffected = Directory.GetFiles(fullPathSOALogDate).Length;
                                    step4DeleteSOALogDateFiles.RowsAffected += step5DeleteSOALogDirectory.RowsAffected;
                                }
                                catch (Exception e)
                                {
                                    step5DeleteSOALogDirectory.Errors.Add(new Error(e));
                                }
                                Logger.Info($"Directory.Delete({fullPathSOALogDate}, true)");
                                if (!Convert.ToBoolean(ConfigurationManager.AppSettings["TestModeNoDelete"])) Directory.Delete(fullPathSOALogDate, true);
                                step5DeleteSOALogDirectory.Done();
                                stepsDelete.Add(step5DeleteSOALogDirectory);
                            }
                        }
                        catch (Exception e)
                        {
                            step4DeleteSOALogDateFiles.Errors.Add(new Error(e));
                        }
                        step4DeleteSOALogDateFiles.Done();
                        response.ProcessSteps.Add(step4DeleteSOALogDateFiles);
                        response.ProcessSteps.AddRange(stepsDelete);
                        response.Size = step4DeleteSOALogDateFiles.Size;
                        response.RowsAffected = step4DeleteSOALogDateFiles.RowsAffected;
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
