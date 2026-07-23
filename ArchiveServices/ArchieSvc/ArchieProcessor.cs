using ArchiveServices.Common.Managers.Archie;
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

namespace ArchiveServices.ArchieSvc
{
    public class ArchieProcessor
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static ProcessResponse GetLedgerTableCandidates(string groupName, string plantCode, string serverName, string databaseName, string schemaName, string tableName, bool useHighwaterMark, bool statsOnly, bool sendReport)
        {
            const string _ConfigTableName = "cfgTruncateLedgerTable";
            var startTime = DateTime.Now.ToLocalTime();
            var cnArchConfig = GetArchConfigSqlConnection();
            var cnCorpInt = GetCorpIntSqlConnection();
            var cnSource = new SqlConnection();
            string batchName = GetBatchName("GetLedgerTableCandidates", groupName, plantCode, serverName, databaseName, schemaName, tableName);
            var batchID = LogBatchInsert(cnArchConfig, batchName);
            Logger.Info($@"BatchID [{batchID}] {batchName} BEGIN: {startTime}");
            var response = new ProcessResponse(batchID, batchName, true);
            response.ConnectionStrings.Add(new ConnectionString("cnArchConfig", cnArchConfig.ConnectionString));
            response.ConnectionStrings.Add(new ConnectionString("cnCorpInt", cnCorpInt.ConnectionString));
            response.ConnectionStrings.Add(new ConnectionString("smtp", ConfigurationManager.AppSettings["SmtpHost"]));
            try
            {
                var configs = GetLedgerTableCandidatesManager.GetLedgerTableCandidateConfigs(cnArchConfig, groupName, plantCode, serverName, databaseName, schemaName, tableName, useHighwaterMark, statsOnly);
                if (configs.Any())
                {
                    foreach (var ledgerTableCandidateConfig in configs)
                    {
                        Logger.Info($@"Process {ledgerTableCandidateConfig.ServerQualifiedSourceName} BEGIN: {startTime}");
                        var processResponse = GetLedgerTableCandidatesManager.Process(batchID, batchName, ledgerTableCandidateConfig, cnArchConfig, cnSource, statsOnly);
                        processResponse.Close();
                        Notify(processResponse, cnCorpInt, "GetLedgerTableCandidatesResponseBody", $@"Get Ledger Table Candidates: {ledgerTableCandidateConfig.ServerQualifiedSourceDatabaseName}", sendReport);
                        response = processResponse;
                    }
                }
                else
                {
                    var truncateLedgerTableConfig = new TruncateLedgerTableConfig();
                    truncateLedgerTableConfig.LastRunStatus = "Error";
                    truncateLedgerTableConfig.LastRunStartDate = DateTime.Now.ToLocalTime();
                    truncateLedgerTableConfig.ConfigID = 0;
                    truncateLedgerTableConfig.SourceServerName = serverName;
                    truncateLedgerTableConfig.SourceDatabaseName = databaseName;
                    truncateLedgerTableConfig.SourceSchemaName = schemaName;
                    truncateLedgerTableConfig.SourceTableName = tableName;
                    truncateLedgerTableConfig.RowsAffected = 0;
                    var processName = $@"Truncate Ledger Table: {truncateLedgerTableConfig.ServerQualifiedSourceName}";
                    Logger.Info($"{processName} BEGIN: {DateTime.Now.ToLocalTime()}");
                    truncateLedgerTableConfig.ProcessID = LogProcessInsert(cnArchConfig, processName, batchID, truncateLedgerTableConfig);
                    var stepValidateTable = new ProcessStep(cnArchConfig, truncateLedgerTableConfig.ProcessID, $@"Validate configuration in [Archiver].[dbo].[{_ConfigTableName}] for {truncateLedgerTableConfig.ServerQualifiedSourceName}");
                    var validationError = new ValidationError($"{truncateLedgerTableConfig.ServerQualifiedSourceName} is not found in [Archiver].[dbo].[{_ConfigTableName}].", "Validation", "Fatal", true);
                    truncateLedgerTableConfig.ValidationErrors.Add(validationError);
                    truncateLedgerTableConfig.ValidationStatus = validationError.Message;
                    var errorID = LogErrorInsert(cnArchConfig, batchID, truncateLedgerTableConfig.ProcessID, 1, validationError.Message, "");
                    Logger.Error(validationError.Message);
                    stepValidateTable.RowsAffected = 1;
                    stepValidateTable.Done(cnArchConfig);
                    response.TruncateLedgerTableConfig = truncateLedgerTableConfig;
                    response.ProcessSteps.Add(stepValidateTable);
                    if (!response.Errors.Contains(new Error(validationError.Message))) response.Errors.Add(new Error(validationError.Message));
                    response.Close();
                    LogProcessUpdateNotFoundDone(cnArchConfig, truncateLedgerTableConfig);
                    Notify(response, cnCorpInt, "TruncateLedgerTableResponseBody", $@"{batchName}", true);
                }
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnSource.ConnectionString}  {e.Message}");
                if (!response.Errors.Contains(new Error($"ERROR: {cnSource.ConnectionString}  {e.Message}"))) response.Errors.Add(new Error($"ERROR: {cnSource.ConnectionString}  {e.Message}"));
            }
            finally
            {
                ArchieLoggingManager.LogBatchUpdateDone(cnArchConfig, batchID);
                cnSource.Close();
                cnArchConfig.Close();
                cnCorpInt.Close();
                Logger.Info($"{batchName} END: {Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds)} SECONDS");
            }
            return response;
        }

        public static ProcessResponse TruncateLedgerTable(string groupName, string plantCode, string serverName, string databaseName, string schemaName, string tableName, bool useHighwaterMark, bool statsOnly, bool sendReport)
        {
            const string _ConfigTableName = "cfgTruncateLedgerTable";
            var startTime = DateTime.Now.ToLocalTime();
            var cnArchConfig = GetArchConfigSqlConnection();
            var cnCorpInt = GetCorpIntSqlConnection();
            var cnSource = new SqlConnection();
            string batchName = GetBatchName("TailNoVerify", groupName, plantCode, serverName, databaseName, schemaName, tableName);
            var batchID = LogBatchInsert(cnArchConfig, batchName);
            Logger.Info($@"BatchID [{batchID}] {batchName} BEGIN: {startTime}");
            var response = new ProcessResponse(batchID, batchName, true);
            response.ConnectionStrings.Add(new ConnectionString("cnArchConfig", cnArchConfig.ConnectionString));
            response.ConnectionStrings.Add(new ConnectionString("cnCorpInt", cnCorpInt.ConnectionString));
            response.ConnectionStrings.Add(new ConnectionString("smtp", ConfigurationManager.AppSettings["SmtpHost"]));
            try
            {
                var configs = TruncateLedgerTableManager.GetTruncateLedgerTableConfigs(cnArchConfig, groupName, plantCode, serverName, databaseName, schemaName, tableName, useHighwaterMark, statsOnly);
                if (configs.Any())
                {
                    foreach (var truncateLedgerTableConfig in configs)
                    {
                        Logger.Info($@"Process {truncateLedgerTableConfig.ServerQualifiedSourceName} BEGIN: {startTime}");
                        var processResponse = TruncateLedgerTableManager.Process(batchID, batchName, truncateLedgerTableConfig, cnArchConfig, cnSource, statsOnly);
                        processResponse.Close();
                        Notify(processResponse, cnCorpInt, "TruncateLedgerTableResponseBody", $@"Truncate Ledger Table: {truncateLedgerTableConfig.ServerQualifiedSourceName}", sendReport);
                        response = processResponse;
                    }
                }
                else
                {
                    var truncateLedgerTableConfig = new TruncateLedgerTableConfig();
                    truncateLedgerTableConfig.LastRunStatus = "Error";
                    truncateLedgerTableConfig.LastRunStartDate = DateTime.Now.ToLocalTime();
                    truncateLedgerTableConfig.ConfigID = 0;
                    truncateLedgerTableConfig.SourceServerName = serverName;
                    truncateLedgerTableConfig.SourceDatabaseName = databaseName;
                    truncateLedgerTableConfig.SourceSchemaName = schemaName;
                    truncateLedgerTableConfig.SourceTableName = tableName;
                    truncateLedgerTableConfig.RowsAffected = 0;
                    var processName = $@"Truncate Ledger Table: {truncateLedgerTableConfig.ServerQualifiedSourceName}";
                    Logger.Info($"{processName} BEGIN: {DateTime.Now.ToLocalTime()}");
                    truncateLedgerTableConfig.ProcessID = LogProcessInsert(cnArchConfig, processName, batchID, truncateLedgerTableConfig);
                    var stepValidateTable = new ProcessStep(cnArchConfig, truncateLedgerTableConfig.ProcessID, $@"Validate configuration in [Archiver].[dbo].[{_ConfigTableName}] for {truncateLedgerTableConfig.ServerQualifiedSourceName}");
                    var validationError = new ValidationError($"{truncateLedgerTableConfig.ServerQualifiedSourceName} is not found in [Archiver].[dbo].[{_ConfigTableName}].", "Validation", "Fatal", true);
                    truncateLedgerTableConfig.ValidationErrors.Add(validationError);
                    truncateLedgerTableConfig.ValidationStatus = validationError.Message;
                    var errorID = LogErrorInsert(cnArchConfig, batchID, truncateLedgerTableConfig.ProcessID, 1, validationError.Message, "");
                    Logger.Error(validationError.Message);
                    stepValidateTable.RowsAffected = 1;
                    stepValidateTable.Done(cnArchConfig);
                    response.TruncateLedgerTableConfig = truncateLedgerTableConfig;
                    response.ProcessSteps.Add(stepValidateTable);
                    if (!response.Errors.Contains(new Error(validationError.Message))) response.Errors.Add(new Error(validationError.Message));
                    response.Close();
                    LogProcessUpdateNotFoundDone(cnArchConfig, truncateLedgerTableConfig);
                    Notify(response, cnCorpInt, "TruncateLedgerTableResponseBody", $@"{batchName}", true);
                }
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnSource.ConnectionString}  {e.Message}");
                if (!response.Errors.Contains(new Error($"ERROR: {cnSource.ConnectionString}  {e.Message}"))) response.Errors.Add(new Error($"ERROR: {cnSource.ConnectionString}  {e.Message}"));
            }
            finally
            {
                ArchieLoggingManager.LogBatchUpdateDone(cnArchConfig, batchID);
                cnSource.Close();
                cnArchConfig.Close();
                cnCorpInt.Close();
                Logger.Info($"{batchName} END: {Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds)} SECONDS");
            }
            return response;
        }

        public static ProcessResponse TailTableNoVerify(string groupName, string plantCode, string serverName, string databaseName, string schemaName, string tableName, bool useHighwaterMark, bool statsOnly, bool sendReport)
        {
            const string _ConfigTableName = "cfgTailTableNoVerify";
            var startTime = DateTime.Now.ToLocalTime();
            var cnArchConfig = GetArchConfigSqlConnection();
            var cnCorpInt = GetCorpIntSqlConnection();
            var cnSource = new SqlConnection();
            string batchName = GetBatchName("TailNoVerify", groupName, plantCode, serverName, databaseName, schemaName, tableName);
            var batchID = LogBatchInsert(cnArchConfig, batchName);
            Logger.Info($@"BatchID [{batchID}] {batchName} BEGIN: {startTime}");
            var response = new ProcessResponse(batchID, batchName, true);
            response.ConnectionStrings.Add(new ConnectionString("cnArchConfig", cnArchConfig.ConnectionString));
            response.ConnectionStrings.Add(new ConnectionString("smtp", ConfigurationManager.AppSettings["SmtpHost"]));
            try
            {
                var configs = TailTableNoVerifyManager.GetTailTableNoVerifyConfigs(cnArchConfig, groupName, plantCode, serverName, databaseName, schemaName, tableName, useHighwaterMark, statsOnly);
                if (configs.Any())
                {
                    foreach (var tailTableNoVerifyConfig in configs)
                    {
                        Logger.Info($@"Process {tailTableNoVerifyConfig.ServerQualifiedSourceName} BEGIN: {startTime}");
                        var processResponse = TailTableNoVerifyManager.Process(batchID, batchName, tailTableNoVerifyConfig, cnArchConfig, cnSource, statsOnly);
                        processResponse.Close();
                        Notify(processResponse, cnCorpInt, "TailTableNoVerifyResponseBody", $@"Tail Table No Verify: {tailTableNoVerifyConfig.ServerQualifiedSourceName}", sendReport);
                        response = processResponse;
                    }
                }
                else
                {
                    var tailTableNoVerifyConfig = new TailTableNoVerifyConfig();
                    tailTableNoVerifyConfig.LastRunStatus = "Error";
                    tailTableNoVerifyConfig.LastRunStartDate = DateTime.Now.ToLocalTime();
                    tailTableNoVerifyConfig.ConfigID = 0;
                    tailTableNoVerifyConfig.SourceServerName = serverName;
                    tailTableNoVerifyConfig.SourceDatabaseName = databaseName;
                    tailTableNoVerifyConfig.SourceSchemaName = schemaName;
                    tailTableNoVerifyConfig.SourceTableName = tableName;
                    tailTableNoVerifyConfig.RowsAffected = 0;
                    var processName = $@"Tail Table No Verify: {tailTableNoVerifyConfig.ServerQualifiedSourceName}";
                    Logger.Info($"{processName} BEGIN: {DateTime.Now.ToLocalTime()}");
                    tailTableNoVerifyConfig.ProcessID = LogProcessInsert(cnArchConfig, processName, batchID, tailTableNoVerifyConfig);
                    var stepValidateTable = new ProcessStep(cnArchConfig, tailTableNoVerifyConfig.ProcessID, $@"Validate configuration in [Archiver].[dbo].[{_ConfigTableName}] for {tailTableNoVerifyConfig.ServerQualifiedSourceName}");
                    var validationError = new ValidationError($"{tailTableNoVerifyConfig.ServerQualifiedSourceName} is not found in [Archiver].[dbo].[{_ConfigTableName}].", "Validation", "Fatal", true);
                    tailTableNoVerifyConfig.ValidationErrors.Add(validationError);
                    tailTableNoVerifyConfig.ValidationStatus = validationError.Message;
                    var errorID = LogErrorInsert(cnArchConfig, batchID, tailTableNoVerifyConfig.ProcessID, 1, validationError.Message, "");
                    Logger.Error(validationError.Message);
                    stepValidateTable.RowsAffected = 1;
                    stepValidateTable.Done(cnArchConfig);
                    response.TailTableNoVerifyConfig = tailTableNoVerifyConfig;
                    response.ProcessSteps.Add(stepValidateTable);
                    if (!response.Errors.Contains(new Error(validationError.Message))) response.Errors.Add(new Error(validationError.Message));
                    response.Close();
                    LogProcessUpdateNotFoundDone(cnArchConfig, tailTableNoVerifyConfig);
                    Notify(response, cnCorpInt, "TailTableNoVerifyResponseBody", $@"{batchName}", true);
                }
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnSource.ConnectionString}  {e.Message}");
                if (!response.Errors.Contains(new Error($"ERROR: {cnSource.ConnectionString}  {e.Message}"))) response.Errors.Add(new Error($"ERROR: {cnSource.ConnectionString}  {e.Message}"));
            }
            finally
            {
                ArchieLoggingManager.LogBatchUpdateDone(cnArchConfig, batchID);
                cnSource.Close();
                cnArchConfig.Close();
                cnCorpInt.Close();
                Logger.Info($"{batchName} END: {Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds)} SECONDS");
            }
            return response;
        }

        public static ProcessResponse TailTableWithVerify(string groupName, string plantCode, string serverName, string databaseName, string schemaName, string tableName, bool useHighwaterMark, bool statsOnly, bool sendReport)
        {
            const string _ConfigTableName = "cfgTailTableWithVerify";
            var startTime = DateTime.Now.ToLocalTime();
            var cnArchConfig = GetArchConfigSqlConnection();
            var cnCorpInt = GetCorpIntSqlConnection();
            var cnSource = new SqlConnection();
            var cnDestination = new SqlConnection();
            string batchName = GetBatchName("TailWithVerify", groupName, plantCode, serverName, databaseName, schemaName, tableName);
            var batchID = LogBatchInsert(cnArchConfig, batchName);
            Logger.Info($@"BatchID [{batchID}] {batchName} BEGIN: {startTime}");
            var response = new ProcessResponse(batchID, batchName, true);
            response.ConnectionStrings.Add(new ConnectionString("cnArchConfig", cnArchConfig.ConnectionString));
            response.ConnectionStrings.Add(new ConnectionString("cnCorpInt", cnCorpInt.ConnectionString));
            response.ConnectionStrings.Add(new ConnectionString("smtp", ConfigurationManager.AppSettings["SmtpHost"]));
            try
            {
                var configs = TailTableWithVerifyManager.GetTailTableWithVerifyConfigs(cnArchConfig, groupName, plantCode, serverName, databaseName, schemaName, tableName, useHighwaterMark, statsOnly);
                if (configs.Any())
                {
                    foreach (var tailTableWithVerifyConfig in configs)
                    {
                        Logger.Info($@"Process {tailTableWithVerifyConfig.ServerQualifiedSourceName} BEGIN: {startTime}");
                        var processResponse = TailTableWithVerifyManager.Process(batchID, batchName, tailTableWithVerifyConfig, cnArchConfig, cnSource, cnDestination, statsOnly);
                        processResponse.Close();
                        Notify(processResponse, cnCorpInt, "TailTableWithVerifyResponseBody", $@"Tail Table With Verify: {tailTableWithVerifyConfig.ServerQualifiedSourceName}", sendReport);
                        response = processResponse;
                    }
                }
                else
                {
                    var tailTableWithVerifyConfig = new TailTableWithVerifyConfig();
                    tailTableWithVerifyConfig.LastRunStatus = "Error";
                    tailTableWithVerifyConfig.LastRunStartDate = DateTime.Now.ToLocalTime();
                    tailTableWithVerifyConfig.ConfigID = 0;
                    tailTableWithVerifyConfig.SourceServerName = serverName;
                    tailTableWithVerifyConfig.SourceDatabaseName = databaseName;
                    tailTableWithVerifyConfig.SourceSchemaName = schemaName;
                    tailTableWithVerifyConfig.SourceTableName = tableName;
                    tailTableWithVerifyConfig.RowsAffected = 0;
                    var processName = $@"Tail Table With Verify: {tailTableWithVerifyConfig.ServerQualifiedSourceName}";
                    Logger.Info($"{processName} BEGIN: {DateTime.Now.ToLocalTime()}");
                    tailTableWithVerifyConfig.ProcessID = LogProcessInsert(cnArchConfig, processName, batchID, tailTableWithVerifyConfig);
                    var stepValidateTable = new ProcessStep(cnArchConfig, tailTableWithVerifyConfig.ProcessID, $@"Validate configuration in [Archiver].[dbo].[{_ConfigTableName}] for {tailTableWithVerifyConfig.ServerQualifiedSourceName}");
                    var validationError = new ValidationError($"{tailTableWithVerifyConfig.ServerQualifiedSourceName} is not found in [Archiver].[dbo].[{_ConfigTableName}].", "Validation", "Fatal", true);
                    tailTableWithVerifyConfig.ValidationErrors.Add(validationError);
                    tailTableWithVerifyConfig.ValidationStatus = validationError.Message;
                    var errorID = LogErrorInsert(cnArchConfig, batchID, tailTableWithVerifyConfig.ProcessID, 1, validationError.Message, "");
                    Logger.Error(validationError.Message);
                    stepValidateTable.RowsAffected = 1;
                    stepValidateTable.Done(cnArchConfig);
                    response.TailTableWithVerifyConfig = tailTableWithVerifyConfig;
                    response.ProcessSteps.Add(stepValidateTable);
                    if (!response.Errors.Contains(new Error(validationError.Message))) response.Errors.Add(new Error(validationError.Message));
                    response.Close();
                    LogProcessUpdateNotFoundDone(cnArchConfig, tailTableWithVerifyConfig);
                    Notify(response, cnCorpInt, "TailTableWithVerifyResponseBody", $@"{batchName}", true);
                }
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnSource.ConnectionString}  {e.Message}");
                if (!response.Errors.Contains(new Error($"ERROR: {cnSource.ConnectionString}  {e.Message}"))) response.Errors.Add(new Error($"ERROR: {cnSource.ConnectionString}  {e.Message}"));
            }
            finally
            {
                ArchieLoggingManager.LogBatchUpdateDone(cnArchConfig, batchID);
                cnSource.Close();
                cnDestination.Close();
                cnArchConfig.Close();
                cnCorpInt.Close();
                Logger.Info($"{batchName} END: {Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds)} SECONDS");
            }
            return response;
        }

        public static ProcessResponse ArchiveTable(string groupName, string plantCode, string serverName, string databaseName, string schemaName, string tableName, bool useHighwaterMark, bool statsOnly, bool sendReport)
        {
            var startTime = DateTime.Now.ToLocalTime();
            var cnArchConfig = GetArchConfigSqlConnection();
            var cnCorpInt = GetCorpIntSqlConnection();
            var cnSource = new SqlConnection();
            var cnDestination = new SqlConnection();
            string batchName = GetBatchName("Archive", groupName, plantCode, serverName, databaseName, schemaName, tableName);
            var batchID = LogBatchInsert(cnArchConfig, batchName);
            Logger.Info($@"BatchID [{batchID}] {batchName} BEGIN: {startTime}");
            var response = new ProcessResponse(batchID, batchName, true);
            response.ConnectionStrings.Add(new ConnectionString("cnArchConfig", cnArchConfig.ConnectionString));
            response.ConnectionStrings.Add(new ConnectionString("cnCorpInt", cnCorpInt.ConnectionString));
            response.ConnectionStrings.Add(new ConnectionString("smtp", ConfigurationManager.AppSettings["SmtpHost"]));
            try
            {
                var configs = ArchiveTableManager.GetArchiveTableConfigs(cnArchConfig, groupName, plantCode, serverName, databaseName, schemaName, tableName, useHighwaterMark, statsOnly);
                if (configs.Any())
                {
                    foreach (var archiveTableConfig in configs)
                    {
                        Logger.Info($@"Process {archiveTableConfig.ServerQualifiedSourceName} BEGIN: {startTime}");
                        var processResponse = ArchiveTableManager.Process(batchID, batchName, archiveTableConfig, cnCorpInt, cnArchConfig, cnSource, cnDestination, statsOnly);
                        processResponse.Close();
                        Notify(processResponse, cnCorpInt, "ArchiveTableResponseBody", $@"Archive Table: {archiveTableConfig.ServerQualifiedSourceName}", sendReport);
                        response = processResponse;
                    }
                }
                else
                {
                    var archiveTableConfig = new ArchiveTableConfig();
                    archiveTableConfig.LastRunStatus = "Error";
                    archiveTableConfig.LastRunStartDate = DateTime.Now.ToLocalTime();
                    archiveTableConfig.ConfigID = 0;
                    archiveTableConfig.SourceServerName = serverName;
                    archiveTableConfig.SourceDatabaseName = databaseName;
                    archiveTableConfig.SourceSchemaName = schemaName;
                    archiveTableConfig.SourceTableName = tableName;
                    archiveTableConfig.RowsAffected = 0;
                    var processName = $@"Archive Table: {archiveTableConfig.ServerQualifiedSourceName}";
                    Logger.Info($"{processName} BEGIN: {DateTime.Now.ToLocalTime()}");
                    archiveTableConfig.ProcessID = ArchieLoggingManager.LogProcessInsert(cnArchConfig, processName, batchID, archiveTableConfig);
                    var stepValidateTable = new ProcessStep(cnArchConfig, archiveTableConfig.ProcessID, $@"Validate configuration in [Archiver].[dbo].[cfgArchiveTable] for {archiveTableConfig.ServerQualifiedSourceName}");
                    var validationError = new ValidationError($"{archiveTableConfig.ServerQualifiedSourceName} is not found in [Archiver].[dbo].[cfgArchiveTable].", "Validation", "Fatal", true);
                    archiveTableConfig.ValidationErrors.Add(validationError);
                    archiveTableConfig.ValidationStatus = validationError.Message;
                    var errorID = LogErrorInsert(cnArchConfig, batchID, archiveTableConfig.ProcessID, 1, validationError.Message, "");
                    Logger.Error(validationError.Message);
                    stepValidateTable.RowsAffected = 1;
                    stepValidateTable.Done(cnArchConfig);
                    response.ArchiveTableConfig = archiveTableConfig;
                    response.ProcessSteps.Add(stepValidateTable);
                    if (!response.Errors.Contains(new Error(validationError.Message))) response.Errors.Add(new Error(validationError.Message));
                    response.Close();
                    ArchieLoggingManager.LogProcessUpdateNotFoundDone(cnArchConfig, archiveTableConfig.ProcessID, archiveTableConfig);
                    Notify(response, cnCorpInt, "ArchiveTableResponseBody", $@"{batchName}", true);
                }
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnSource.ConnectionString}  {e.Message}");
                if (!response.Errors.Contains(new Error($"ERROR: {cnSource.ConnectionString}  {e.Message}"))) response.Errors.Add(new Error($"ERROR: {cnSource.ConnectionString}  {e.Message}"));
            }
            finally
            {
                ArchieLoggingManager.LogBatchUpdateDone(cnArchConfig, batchID);
                cnSource.Close();
                cnDestination.Close();
                cnArchConfig.Close();
                cnCorpInt.Close();
                Logger.Info($"{batchName} END: {Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds)} SECONDS");
            }
            return response;
        }

        public static ProcessResponse ArchiveAndTailTable(string groupName, string plantCode, string serverName, string databaseName, string schemaName, string tableName, bool useHighwaterMark, bool statsOnly, bool sendReport)
        {
            var startTime = DateTime.Now.ToLocalTime();
            var cnArchConfig = GetArchConfigSqlConnection();
            var cnCorpInt = GetCorpIntSqlConnection();
            var cnSource = new SqlConnection();
            var cnDestination = new SqlConnection();
            string batchName = GetBatchName("Archive", groupName, plantCode, serverName, databaseName, schemaName, tableName);
            var batchID = LogBatchInsert(cnArchConfig, batchName);
            Logger.Info($@"BatchID [{batchID}] {batchName} BEGIN: {startTime}");
            var response = new ProcessResponse(batchID, batchName, true);
            response.ConnectionStrings.Add(new ConnectionString("cnArchConfig", cnArchConfig.ConnectionString));
            response.ConnectionStrings.Add(new ConnectionString("cnCorpInt", cnCorpInt.ConnectionString));
            response.ConnectionStrings.Add(new ConnectionString("smtp", ConfigurationManager.AppSettings["SmtpHost"]));
            try
            {
                var configs = ArchiveTableManager.GetArchiveTableConfigs(cnArchConfig, groupName, plantCode, serverName, databaseName, schemaName, tableName, useHighwaterMark, statsOnly);
                if (configs.Any())
                {
                    foreach (var archiveTableConfig in configs)
                    {
                        Logger.Info($@"Process {archiveTableConfig.ServerQualifiedSourceName} BEGIN: {startTime}");
                        var processResponse = ArchiveTableManager.Process(batchID, batchName, archiveTableConfig, cnCorpInt, cnArchConfig, cnSource, cnDestination, statsOnly);
                        processResponse.Close();
                        Notify(processResponse, cnCorpInt, "ArchiveTableResponseBody", $@"Archive Table: {archiveTableConfig.ServerQualifiedSourceName}", sendReport);
                        response = processResponse;
                    }
                }
                else
                {
                    var archiveTableConfig = new ArchiveTableConfig();
                    archiveTableConfig.LastRunStatus = "Error";
                    archiveTableConfig.LastRunStartDate = DateTime.Now.ToLocalTime();
                    archiveTableConfig.ConfigID = 0;
                    archiveTableConfig.SourceServerName = serverName;
                    archiveTableConfig.SourceDatabaseName = databaseName;
                    archiveTableConfig.SourceSchemaName = schemaName;
                    archiveTableConfig.SourceTableName = tableName;
                    archiveTableConfig.RowsAffected = 0;
                    var processName = $@"Archive Table: {archiveTableConfig.ServerQualifiedSourceName}";
                    Logger.Info($"{processName} BEGIN: {DateTime.Now.ToLocalTime()}");
                    archiveTableConfig.ProcessID = ArchieLoggingManager.LogProcessInsert(cnArchConfig, processName, batchID, archiveTableConfig);
                    var stepValidateTable = new ProcessStep(cnArchConfig, archiveTableConfig.ProcessID, $@"Validate configuration in [Archiver].[dbo].[cfgArchiveTable] for {archiveTableConfig.ServerQualifiedSourceName}");
                    var validationError = new ValidationError($"{archiveTableConfig.ServerQualifiedSourceName} is not found in [Archiver].[dbo].[cfgArchiveTable].", "Validation", "Fatal", true);
                    archiveTableConfig.ValidationErrors.Add(validationError);
                    archiveTableConfig.ValidationStatus = validationError.Message;
                    var errorID = LogErrorInsert(cnArchConfig, batchID, archiveTableConfig.ProcessID, 1, validationError.Message, "");
                    Logger.Error(validationError.Message);
                    stepValidateTable.RowsAffected = 1;
                    stepValidateTable.Done(cnArchConfig);
                    response.ArchiveTableConfig = archiveTableConfig;
                    response.ProcessSteps.Add(stepValidateTable);
                    if (!response.Errors.Contains(new Error(validationError.Message))) response.Errors.Add(new Error(validationError.Message));
                    response.Close();
                    ArchieLoggingManager.LogProcessUpdateNotFoundDone(cnArchConfig, archiveTableConfig.ProcessID, archiveTableConfig);
                    Notify(response, cnCorpInt, "ArchiveTableResponseBody", $@"{batchName}", true);
                }
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnSource.ConnectionString}  {e.Message}");
                if (!response.Errors.Contains(new Error($"ERROR: {cnSource.ConnectionString}  {e.Message}"))) response.Errors.Add(new Error($"ERROR: {cnSource.ConnectionString}  {e.Message}"));
            }
            finally
            {
                ArchieLoggingManager.LogBatchUpdateDone(cnArchConfig, batchID);
                cnSource.Close();
                cnDestination.Close();
                cnArchConfig.Close();
                cnCorpInt.Close();
                Logger.Info($"{batchName} END: {Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds)} SECONDS");
            }
            return response;
        }

        public static ProcessResponse DeleteLogFiles(string groupName, string serverName, string logType, string plantCode,  bool sendReport)
        {
            var startTime = DateTime.Now.ToLocalTime();
            var cnArchConfig = GetArchConfigSqlConnection();
            var cnCorpInt = GetCorpIntSqlConnection();
            string batchName = DeleteLogDirectoriesManager.GetBatchName($@"Delete {logType} Files", groupName, serverName, logType);
            var batchID = LogBatchInsert(cnArchConfig, batchName);
            Logger.Info($@"BatchID [{batchID}] {batchName} BEGIN: {startTime}");
            var response = new ProcessResponse(batchID, batchName, true);
            response.ConnectionStrings.Add(new ConnectionString("cnArchConfig", cnArchConfig.ConnectionString));
            response.ConnectionStrings.Add(new ConnectionString("cnCorpInt", cnCorpInt.ConnectionString));
            response.ConnectionStrings.Add(new ConnectionString("smtp", ConfigurationManager.AppSettings["SmtpHost"]));
            try
            {
                var configs = DeleteLogFilesConfigManager.GetDeleteLogFileConfigs(cnArchConfig, groupName, serverName, logType);
                if (configs.Any())
                {
                    foreach (var config in configs)
                    {
                        Logger.Info($@"Process {config.ServerName} BEGIN: {startTime}");
                        var processResponse = new ProcessResponse();
                        if (config.LogType.ToUpper() == "IIS")
                        {
                            processResponse = DeleteLogFilesManager.Process(batchID, batchName, config, cnCorpInt, cnArchConfig);
                            processResponse.Close();
                            Notify(processResponse, cnCorpInt, "DeleteLogFilesResponseBody", $@"Delete {config.LogType} Log Files on Server: {config.ServerName}", sendReport);
                        }
                        else if (config.LogType.ToUpper() == "INTSVC")
                        {
                            processResponse = DeleteIntSvcNoticesManager.Process(batchID, batchName, config, cnCorpInt, cnArchConfig, plantCode);
                            processResponse.Close();
                            Notify(processResponse, cnCorpInt, "DeleteIntSvcResponseFilesResponseBody", $@"Delete {config.LogType} Notices on Server: {config.ServerName}", sendReport);
                        }
                        else if (config.DeleteDirectories && (config.LogType.ToUpper() == "NLOG" || config.LogType.ToUpper() == "FORKLIFT"))
                        {
                            processResponse = DeleteLogDirectoriesManager.Process(batchID, batchName, config, cnCorpInt, cnArchConfig);
                            processResponse.Close();
                            Notify(processResponse, cnCorpInt, "DeleteLogDirectoriesResponseBody", $@"Delete {config.LogType} Log Directories on Server: {config.ServerName}", sendReport);
                        }
                        else if (config.LogType.ToUpper() == "FORZAEMAIL")
                        {
                            processResponse = DeleteLogFilesManager.Process(batchID, batchName, config, cnCorpInt, cnArchConfig);
                            processResponse.Close();
                            Notify(processResponse, cnCorpInt, "DeleteLogDirectoriesResponseBody", $@"Delete {config.LogType} Log Files on Server: {config.ServerName}", sendReport);
                        }
                        else 
                        {
                            var deleteLogFileConfig = new DeleteLogFileConfig();
                            deleteLogFileConfig.LastRunStatus = "Error";
                            deleteLogFileConfig.LastRunStartDate = DateTime.Now.ToLocalTime();
                            deleteLogFileConfig.ConfigID = 0;
                            deleteLogFileConfig.ServerName = serverName;
                            deleteLogFileConfig.LogType = logType;
                            var processName = $@"Delete {logType} Log Files on Server: {serverName}";
                            Logger.Info($"{processName} BEGIN: {DateTime.Now.ToLocalTime()}");
                            deleteLogFileConfig.ProcessID = ArchieLoggingManager.LogProcessInsert(cnArchConfig, processName, batchID, deleteLogFileConfig);
                            var stepValidateTable = new ProcessStep(cnArchConfig, deleteLogFileConfig.ProcessID, $@"Validate configuration in [Archiver].[dbo].[cfgDeleteLogFiles] for {deleteLogFileConfig.ServerName} LogType {deleteLogFileConfig.LogType}");
                            var validationError = new ValidationError($"{deleteLogFileConfig.ServerName} LogType {deleteLogFileConfig.LogType} is not handled in ArchiveServices", "Validation", "Fatal", true);
                            deleteLogFileConfig.ValidationErrors.Add(validationError);
                            deleteLogFileConfig.ValidationStatus = validationError.Message;
                            var errorID = LogErrorInsert(cnArchConfig, batchID, deleteLogFileConfig.ProcessID, 1, validationError.Message, "");
                            Logger.Error(validationError.Message);
                            stepValidateTable.RowsAffected = 1;
                            stepValidateTable.Done(cnArchConfig);
                            response.DeleteLogFileConfig = deleteLogFileConfig;
                            response.ProcessSteps.Add(stepValidateTable);
                            response.Errors.Add(new Error(validationError.Message));
                            response.Close();
                            ArchieLoggingManager.LogProcessUpdateNotFoundDone(cnArchConfig, deleteLogFileConfig.ProcessID, deleteLogFileConfig);

                            Notify(response, cnCorpInt, "DeleteLogFilesResponseBody", $@"{batchName}", true);
                        }
                        response = processResponse;
                    }
                }
                else 
                {
                    var deleteLogFileConfig = new DeleteLogFileConfig();
                    deleteLogFileConfig.LastRunStatus = "Error";
                    deleteLogFileConfig.LastRunStartDate = DateTime.Now.ToLocalTime();
                    deleteLogFileConfig.ConfigID = 0;
                    deleteLogFileConfig.ServerName = serverName;
                    deleteLogFileConfig.LogType = logType;
                    var processName = $@"Delete {logType} Log Files on Server: {serverName}";
                    Logger.Info($"{processName} BEGIN: {DateTime.Now.ToLocalTime()}");
                    deleteLogFileConfig.ProcessID = ArchieLoggingManager.LogProcessInsert(cnArchConfig, processName, batchID, deleteLogFileConfig);
                    var stepValidateTable = new ProcessStep(cnArchConfig, deleteLogFileConfig.ProcessID, $@"Validate configuration in [Archiver].[dbo].[cfgDeleteLogFiles] for {deleteLogFileConfig.ServerName} LogType {deleteLogFileConfig.LogType}");
                    var validationError = new ValidationError($"{deleteLogFileConfig.ServerName} LogType {deleteLogFileConfig.LogType} is not found in [Archiver].[dbo].[cfgDeleteLogFiles].", "Validation", "Fatal", true);
                    deleteLogFileConfig.ValidationErrors.Add(validationError);
                    deleteLogFileConfig.ValidationStatus = validationError.Message;
                    var errorID = LogErrorInsert(cnArchConfig, batchID, deleteLogFileConfig.ProcessID, 1, validationError.Message, "");
                    Logger.Error(validationError.Message);
                    stepValidateTable.RowsAffected = 1;
                    stepValidateTable.Done(cnArchConfig);
                    response.DeleteLogFileConfig = deleteLogFileConfig;
                    response.ProcessSteps.Add(stepValidateTable);
                    response.Errors.Add(new Error(validationError.Message));
                    response.Close();
                    ArchieLoggingManager.LogProcessUpdateNotFoundDone(cnArchConfig, deleteLogFileConfig.ProcessID, deleteLogFileConfig);

                    Notify(response, cnCorpInt, "DeleteLogFilesResponseBody", $@"{batchName}", true);
                }
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnArchConfig.ConnectionString}  {e.Message}");
                response.Errors.Add(new Error($"ERROR: {cnArchConfig.ConnectionString}  {e.Message}"));
            }
            finally
            {
                ArchieLoggingManager.LogBatchUpdateDone(cnArchConfig, batchID);
                cnArchConfig.Close();
                cnCorpInt.Close();
                Logger.Info($"Delete {logType} Files: {serverName} Retention Days: {0} END: {Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds)} SECONDS");
            }
            return response;
        }


        private static string GetBatchName(string actionName, string groupName, string plantCode, string serverName, string databaseName, string schemaName, string tableName)
        {
            string batchName = "";
            if (!string.IsNullOrEmpty(plantCode))
            {
                batchName = $@"Plant: [{plantCode}] ";
            }
            if (!string.IsNullOrEmpty(groupName))
            {
                batchName = $@"Group: [{groupName}] {batchName}";
            }

            //if (!string.IsNullOrEmpty(serverName))
            //{
            if (!string.IsNullOrEmpty(tableName))
            {
                if (!string.IsNullOrEmpty(serverName))
                    batchName = $@"{actionName} {batchName}Table: [{serverName}].[{databaseName}].[{schemaName}].[{tableName}]";
                else
                    batchName = $@"{actionName} {batchName}Table: [{databaseName}].[{schemaName}].[{tableName}]";
            }
            else if (!string.IsNullOrEmpty(schemaName))
            {
                if (!string.IsNullOrEmpty(serverName))
                    batchName = $@"{actionName} {batchName}Tables in Database Schema: [{serverName}].[{databaseName}].[{schemaName}].[*]";
                else
                    batchName = $@"{actionName} {batchName}Tables in Database Schema: [{databaseName}].[{schemaName}].[*]";
            }
            else if (!string.IsNullOrEmpty(databaseName))
            {
                if (!string.IsNullOrEmpty(serverName))
                    batchName = $@"{actionName} {batchName}Tables in Database: [{serverName}].[{databaseName}].[*].[*]";
                else
                    batchName = $@"{actionName} {batchName}Tables in Database: [{databaseName}].[*].[*]";
            }
            else if (!string.IsNullOrEmpty(serverName))
            {
                batchName = $@"{actionName} {batchName}Tables on Server: [{serverName}].[*].[*].[*]";
            }
            else
            {
                batchName = $@"{actionName} {batchName}";
            }
            //}
            //else
            //{
            //    batchName = $@"{actionName} All Tables";
            //}
            Logger.Info(batchName);
            return batchName;
        }

        public static void Notify(ProcessResponse response, SqlConnection cnCorpInt, string xsltName, string subject, bool sendReport)
        {
            var stepSendMail = new ProcessStep("Send Mail");
            try
            {
                Summarize(response);
                var serializedXml = response.Serialize();
                Logger.Info($"Serialized Process: {serializedXml}");
                var xslOutput = XsltManager.Transform(serializedXml, $@"ArchiveServices.Common.Xslt.{xsltName}.xslt", Encoding.UTF8);
                Logger.Info($"Report XML: {xslOutput}");
                if (response.HasErrors()) subject = $"Error - {subject}";
                //if (response.ArchiveTableConfig.StatsOnly) subject = $"{subject} - Stats Only";

                Mail.StoreResponse($@"{ConfigurationManager.AppSettings["ProcessResponseFilePath"]}\ARCHIE", subject, xslOutput);

                if (sendReport || response.HasErrors())
                {
                    var recipients = new List<string>();
                    recipients.AddRange(Mail.GetRecipients(cnCorpInt, "ARCHIE", "ADMIN"));
                    Mail.SendHtmlAsync(ConfigurationManager.AppSettings["SmtpHost"],
                        ConfigurationManager.AppSettings["MailFromAddress"],
                        recipients,
                        null,
                        subject,
                        xslOutput);
                }
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {e.Message}: {e.StackTrace}");
                stepSendMail.Errors.Add(new Error(e));
            }
            finally
            {
                stepSendMail.Done();
                response.ProcessSteps.Add(stepSendMail);
            }
        }

        public static void NotifyNotFound(ProcessResponse response, SqlConnection cnCorpInt, string xsltName, string subject)
        {
            var stepSendMail = new ProcessStep("Send Mail");
            try
            {
                Summarize(response);
                var serializedXml = response.Serialize();
                Logger.Info($"Serialized Process: {serializedXml}");
                var xslOutput = XsltManager.Transform(serializedXml, $@"ArchiveServices.Common.Xslt.{xsltName}.xslt", Encoding.UTF8);
                Logger.Info($"Report XML: {xslOutput}");
                if (response.HasErrors()) subject = $"Error - {subject}";

                Mail.StoreResponse($@"{ConfigurationManager.AppSettings["ProcessResponseFilePath"]}\ARCHIE", subject, xslOutput);

                var recipients = new List<string>();
                recipients.AddRange(Mail.GetRecipients(cnCorpInt, "ARCHIE", "ADMIN"));
                Mail.SendHtmlAsync(ConfigurationManager.AppSettings["SmtpHost"],
                    ConfigurationManager.AppSettings["MailFromAddress"],
                    recipients,
                    null,
                    subject,
                    xslOutput);
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {e.Message}: {e.StackTrace}");
                stepSendMail.Errors.Add(new Error(e));
            }
            finally
            {
                stepSendMail.Done();
                response.ProcessSteps.Add(stepSendMail);
            }
        }

        public static void Summarize(ProcessResponse response)
        {
            var stepSummarize = new ProcessStep("Summary Collections");
            try
            {
                var headerSummary = new
                {
                    response.TransferStatus,
                    HasErrors = response.HasErrors(),
                    response.TransferMessage,
                    response.StartDate,
                    StartDateString = response.StartDate.ToString("M/dd/yyyy hh:mm:ss tt", CultureInfo.CreateSpecificCulture("en-US")),
                    response.EndDate,
                    EndDateString = response.EndDate.ToString("M/dd/yyyy hh:mm:ss tt", CultureInfo.CreateSpecificCulture("en-US")),
                    ElapsedSeconds = (decimal)(response.EndDate - response.StartDate).TotalSeconds,
                    PlantCode = response.Plant == null ? "" : response.Plant.PlantCode,
                    response.InitialRowCount,
                    response.SourceRowCount,
                    response.TargetRowCount,
                    response.RowsToDelete,
                    response.RowsAffected,
                    response.FinalRowCount,
                    SFMinDateString = response.SFMinDate.ToString("M/dd/yyyy hh:mm:ss tt", CultureInfo.CreateSpecificCulture("en-US")),
                    ArchiveDateString = response.ArchiveDate.ToString("M/dd/yyyy hh:mm:ss tt", CultureInfo.CreateSpecificCulture("en-US")),
                    SourceMaxDate = response.SourceMaxDate.ToString("M/dd/yyyy hh:mm:ss tt", CultureInfo.CreateSpecificCulture("en-US")),
                    TargetMaxDate = response.TargetMaxDate.ToString("M/dd/yyyy hh:mm:ss tt", CultureInfo.CreateSpecificCulture("en-US")),
                    response.TestMode,
                    response.SelectCommand,
                    response.DeleteCommand,
                    response.RetentionDays,
                    response.SFDays,
                    response.Size,
                    response.BatchID,
                    response.ProcessID,
                    SourceMaxDateString = response.SourceMaxDate.ToString("M/dd/yyyy hh:mm:ss tt", CultureInfo.CreateSpecificCulture("en-US")),
                    TargetMaxDateString = response.TargetMaxDate.ToString("M/dd/yyyy hh:mm:ss tt", CultureInfo.CreateSpecificCulture("en-US")),
                    //ResponseErrors = response.Errors = null,
                };
                response.SerializedObjects.Add("SummaryHeader", headerSummary, null);
                stepSummarize.RowsAffected = 1;
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {e.Message}: {e.StackTrace}");
                stepSummarize.Errors.Add(new Error(e));
            }
            finally
            {
                stepSummarize.Done();
                response.ProcessSteps.Add(stepSummarize);
            }
        }

        private static SqlConnection GetArchConfigSqlConnection()
        {
            var cnArchConfig = new SqlConnection();
            try
            {
                cnArchConfig = ConnectionManager.GetSqlConnectionFromKey(@"ArchieConfigServer");
                cnArchConfig.Open();
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnArchConfig.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new ApplicationException($"ArchieConfigServer {cnArchConfig.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return cnArchConfig;
        }

        private static SqlConnection GetArchRepoMasterSqlConnection()
        {
            var cnArchRepoMaster = new SqlConnection();
            try
            {
                cnArchRepoMaster = ConnectionManager.GetSqlConnectionFromKey(@"ArchieRepositoryServer");
                cnArchRepoMaster.Open();
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnArchRepoMaster.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new ApplicationException($"ArchieRepositoryServer {cnArchRepoMaster.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return cnArchRepoMaster;
        }

        private static SqlConnection GetCorpIntSqlConnection()
        {
            var cnCorpInt = new SqlConnection();
            try
            {
                cnCorpInt = ConnectionManager.GetSqlConnectionFromKey(@"CORP.INTEGRATION");
                cnCorpInt.Open();
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnCorpInt.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new ApplicationException($"CORP.INTEGRATION {cnCorpInt.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return cnCorpInt;
        }

        public static long LogProcessUpdateNotFoundDone(SqlConnection cnArchConfig, TailTableNoVerifyConfig tailTableNoVerifyConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            long rowCount;
            try
            {
                DateTime dt1 = DateTime.Parse("01/01/1900");
                if (tailTableNoVerifyConfig.LastRunStartDate < dt1) tailTableNoVerifyConfig.LastRunStartDate = dt1;
                if (tailTableNoVerifyConfig.LastRunEndDate < dt1) tailTableNoVerifyConfig.LastRunEndDate = dt1;
                var cmd = new SqlCommand
                {
                    Connection = cnArchConfig,
                    CommandType = CommandType.Text,
                    CommandText = $@"
                        UPDATE [Archiver].[LOG].[Process] 
	                    SET 
                            [EndDate] = GETDATE()
                            ,[LastRunStatus] = '{tailTableNoVerifyConfig.LastRunStatus}'
                            ,[RowsMerged] = {tailTableNoVerifyConfig.RowsAffected} 
                            ,[LastRunStartDate] = '{tailTableNoVerifyConfig.LastRunStartDate}'        
                            ,[LastRunEndDate] = '{tailTableNoVerifyConfig.LastRunEndDate}'        
	                    WHERE [ProcessID] = {tailTableNoVerifyConfig.ProcessID}
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

        public static long LogProcessUpdateNotFoundDone(SqlConnection cnArchConfig, TailTableWithVerifyConfig tailTableWithVerifyConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            long rowCount;
            try
            {
                DateTime dt1 = DateTime.Parse("01/01/1900");
                if (tailTableWithVerifyConfig.LastRunStartDate < dt1) tailTableWithVerifyConfig.LastRunStartDate = dt1;
                if (tailTableWithVerifyConfig.LastRunEndDate < dt1) tailTableWithVerifyConfig.LastRunEndDate = dt1;
                var cmd = new SqlCommand
                {
                    Connection = cnArchConfig,
                    CommandType = CommandType.Text,
                    CommandText = $@"
                        UPDATE [Archiver].[LOG].[Process] 
	                    SET 
                            [EndDate] = GETDATE()
                            ,[LastRunStatus] = '{tailTableWithVerifyConfig.LastRunStatus}'
                            ,[RowsMerged] = {tailTableWithVerifyConfig.RowsAffected} 
                            ,[LastRunStartDate] = '{tailTableWithVerifyConfig.LastRunStartDate}'        
                            ,[LastRunEndDate] = '{tailTableWithVerifyConfig.LastRunEndDate}'        
	                    WHERE [ProcessID] = {tailTableWithVerifyConfig.ProcessID}
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

        public static long LogProcessUpdateNotFoundDone(SqlConnection cnArchConfig, TruncateLedgerTableConfig truncateLedgerTableConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            long rowCount;
            try
            {
                DateTime dt1 = DateTime.Parse("01/01/1900");
                if (truncateLedgerTableConfig.LastRunStartDate < dt1) truncateLedgerTableConfig.LastRunStartDate = dt1;
                if (truncateLedgerTableConfig.LastRunEndDate < dt1) truncateLedgerTableConfig.LastRunEndDate = dt1;
                var cmd = new SqlCommand
                {
                    Connection = cnArchConfig,
                    CommandType = CommandType.Text,
                    CommandText = $@"
                        UPDATE [Archiver].[LOG].[Process] 
	                    SET 
                            [EndDate] = GETDATE()
                            ,[LastRunStatus] = '{truncateLedgerTableConfig.LastRunStatus}'
                            ,[RowsMerged] = {truncateLedgerTableConfig.RowsAffected} 
                            ,[LastRunStartDate] = '{truncateLedgerTableConfig.LastRunStartDate}'        
                            ,[LastRunEndDate] = '{truncateLedgerTableConfig.LastRunEndDate}'        
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

        public static int LogProcessInsert(SqlConnection cnArchConfig, string processName, long batchID, TailTableWithVerifyConfig tailTableConfig)
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
                          ,[DestinationServerName]
                          ,[DestinationDatabaseName]
                          ,[DestinationSchemaName]
                          ,[DestinationTableName]
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
                           ,'{tailTableConfig.DestinationServerName}'
                           ,'{tailTableConfig.DestinationDatabaseName}'
                           ,'{tailTableConfig.DestinationSchemaName}'
                           ,'{tailTableConfig.DestinationTableName}'
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

        public static int LogBatchInsert(SqlConnection cnArchConfig, string batchName)
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
                        INSERT INTO [Archiver].[LOG].[Batch]
                        (
                            [BatchName]
                           ,[StartDate]
                        )
	                    VALUES 
	                    (
                            '{batchName}'
                           ,'{DateTime.Now}'
                        );
                        SELECT SCOPE_IDENTITY();
                    "
                };
                Logger.Info(Logging.DbCmdString(cmd));
                newID = Convert.ToInt32(cmd.ExecuteScalar());
                Logger.Info($"New BatchID: {newID}");
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

        public static long LogErrorInsert(SqlConnection cnArchConfig, long batchID, long processID, long stepID, string comment, string statement)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            long newID;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnArchConfig,
                    CommandType = CommandType.Text,
                    CommandText = $@"
                        INSERT INTO [Archiver].[LOG].[Error]
                        (
                            [BatchID]
                            ,[ProcessID]
                           ,[StepID]
                           ,[Comment]
                           ,[Statement]
                        )
	                    VALUES 
	                    (
                            {batchID}
                           ,{processID}
                           ,{stepID}
                           ,'{comment}'
                           ,'{statement}'
                        );
                        SELECT SCOPE_IDENTITY();
                    "
                };
                Logger.Info(Logging.DbCmdString(cmd));
                newID = Convert.ToInt32(cmd.ExecuteScalar());
                Logger.Info($"New ErrorID: {newID}");
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

    }
}