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

namespace ArchiveServices.Common.Managers.Archie
{
    public class DeleteSvcLogFileManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static Types.Archie.ProcessResponse Process(SqlConnection cnArchConfig, string serverName, bool sendReport)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            var response = new Types.Archie.ProcessResponse($"Delete SvcLog Files: {serverName} Retention Days: {retentionDays}", true);
            if (Convert.ToBoolean(ConfigurationManager.AppSettings["TestModeNoDelete"])) response.ProcessName = $"{response.ProcessName} - Test Mode No Delete";
            response.ServerName = serverName;
            response.RetentionDays = retentionDays;
            response.ConnectionStrings.Add(new ConnectionString("cnArchConfig", cnArchConfig.ConnectionString));
            response.ConnectionStrings.Add(new ConnectionString("smtp", ConfigurationManager.AppSettings["SmtpHost"]));
            var server = new Server();
            var filePathSvcLog = $@"\\{serverName}\c$\temp\";
            try
            {
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
                    var step3FetchSvcLogFiles = new ProcessStep($@"Step 3. Fetch SvcLog Files <{filePathSvcLog}>");
                    var fileNamesSvcLog = new List<string>();
                    try
                    {
                        var retentionDate = DateTime.Now.AddDays(-retentionDays);
                        DirectoryInfo dirInfoSvcLog = new DirectoryInfo(filePathSvcLog);
                        var filesSvcLog = dirInfoSvcLog.EnumerateFiles().Where(file => file.Extension == ".svclog" && file.LastWriteTime <= retentionDate).OrderBy(p => p.LastWriteTime);
                        fileNamesSvcLog.AddRange(filesSvcLog.Select(file => file.Name));
                        step3FetchSvcLogFiles.RowsAffected = fileNamesSvcLog.Count;
                    }
                    catch (Exception e)
                    {
                        step3FetchSvcLogFiles.Errors.Add(new Error(e));
                    }
                    step3FetchSvcLogFiles.Done();
                    response.ProcessSteps.Add(step3FetchSvcLogFiles);
                    if (fileNamesSvcLog.Any())
                    {
                        var step4DeleteSvcLogFiles = new ProcessStep($@"Step 4. Delete SvcLog Files ({serverName})");
                        var stepsDelete = new ProcessSteps();
                        try
                        {
                            foreach (var fileName in fileNamesSvcLog)
                            {
                                var fullPathSvcLog = $@"{filePathSvcLog}{fileName}";
                                if (File.Exists(fullPathSvcLog))
                                {
                                    try
                                    {
                                        FileInfo fi = new FileInfo(fullPathSvcLog);
                                        var stepDeleteSvcLogFile = new ProcessStep($@"Delete <{fullPathSvcLog}> {fi.LastWriteTime}");
                                        stepDeleteSvcLogFile.Size = fi.Length / 1024 / 1024; //mb
                                        step4DeleteSvcLogFiles.Size += stepDeleteSvcLogFile.Size;
                                        stepDeleteSvcLogFile.RowsAffected = 1;
                                        stepsDelete.Add(stepDeleteSvcLogFile);
                                        step4DeleteSvcLogFiles.RowsAffected += stepDeleteSvcLogFile.RowsAffected;
                                        response.RowsAffected += stepDeleteSvcLogFile.RowsAffected;
                                        Logger.Info($"File.Delete({fullPathSvcLog}, true)");
                                        if (!Convert.ToBoolean(ConfigurationManager.AppSettings["TestModeNoDelete"])) File.Delete(fullPathSvcLog);
                                        stepDeleteSvcLogFile.Done();
                                    }
                                    catch (Exception e)
                                    {
                                        step4DeleteSvcLogFiles.Errors.Add(new Error($"{fullPathSvcLog} {e.Message} {e.StackTrace}"));
                                        //InsertDocDeleteError(cnTCCSUPPORT, doc, $"{doc.Filter} document {doc.fsdDocID} PDF {doc.PDFPath} {e.Message}", deleteGUID);
                                    }
                                }
                                else
                                {
                                    step4DeleteSvcLogFiles.Errors.Add(new Error($"{fullPathSvcLog} not found.", false));
                                    step4DeleteSvcLogFiles.RowsAffected = fileNamesSvcLog .Count; //+= InsertDocDeleteError(cnTCCSUPPORT, doc, $"{doc.Filter} document {doc.fsdDocID} PDF not found.", deleteGUID);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            step4DeleteSvcLogFiles.Errors.Add(new Error(e));
                        }
                        step4DeleteSvcLogFiles.Done();
                        response.ProcessSteps.Add(step4DeleteSvcLogFiles);
                        response.ProcessSteps.AddRange(stepsDelete);
                        response.Size = step4DeleteSvcLogFiles.Size;
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
