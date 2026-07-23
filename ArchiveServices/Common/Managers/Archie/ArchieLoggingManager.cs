using System;
using System.Data;
using Microsoft.Data.SqlClient;
using ArchiveServices.Common.Types.Archie;
using ArchiveServices.Common.Types.Enterprise;
using ArchiveServices.Common.Utilities;
using NLog;

namespace ArchiveServices.Common.Managers.Archie
{
    public class ArchieLoggingManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static long LogDetail(SqlConnection cnArchConfig, ArchieLogDetail archieLog)
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
                        INSERT INTO [Archiver].[dbo].[LoggingDetail]
                        (
                            [event_type]
                            ,[task_name]
                            ,[command]
                            ,[delete_rows]
                            ,[test_mode]
                            ,[written_date]
                            ,[Plant]
                            ,[ServerName]
                            ,[DatabaseName]
                            ,[SchemaName]
                            ,[TableName]
                        )
	                    VALUES 
	                    (
                            '{archieLog.EventType}'
                            ,'{archieLog.TaskName}'
                            ,'{archieLog.Command}'
                            ,{archieLog.DeleteRows}
                            ,{(archieLog.TestMode ? 1 : 0)}
                            ,'{archieLog.WrittenDate}'
                            ,'{archieLog.PlantCode}'
                            ,'{archieLog.ServerName}'
                            ,'{archieLog.DatabaseName}'
                            ,'{archieLog.SchemaName}'
                            ,'{archieLog.TableName}'
                        )"
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

        public static long LogActivity(SqlConnection cnArchConfig, ArchieLog archieLog)
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
                        INSERT INTO [Archiver].[dbo].[Logging]
                        (
                            [event_type]
                            ,[service_name]
                            ,[task_name]
                            ,[version]
                            ,[user_name]
                            ,[comments]
                            ,[command]
                            ,[date_column]
                            ,[retention_days]
                            ,[initial_rowcount]
                            ,[delete_rows]
                            ,[final_rowcount]
                            ,[test_mode]
                            ,[written_date]
                            ,[GroupName]
                            ,[Plant]
                            ,[ServerName]
                            ,[DatabaseName]
                            ,[SchemaName]
                            ,[TableName]
                        )
	                    VALUES 
	                    (
                            '{archieLog.EventType}'
                            ,'{archieLog.HostName}'
                            ,'{archieLog.TaskName}'
                            ,'{archieLog.Version}'
                            ,'{archieLog.UserName}'
                            ,'{archieLog.Comments.Replace("'", "''")}'
                            ,'{archieLog.Command.Replace("'", "''")}'
                            ,'{archieLog.DateColumn}'
                            ,{archieLog.RetentionDays}
                            ,{archieLog.InitialRowCount}
                            ,{archieLog.DeleteRows}
                            ,{archieLog.FinalRowCount}
                            ,{(archieLog.TestMode ? 1 : 0)}
                            ,'{archieLog.WrittenDate}'
                            ,'{archieLog.GroupName}'
                            ,'{archieLog.PlantCode}'
                            ,'{archieLog.ServerName}'
                            ,'{archieLog.DatabaseName}'
                            ,'{archieLog.SchemaName}'
                            ,'{archieLog.TableName}'
                        )"
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

        //public static int LogBatchInsert(SqlConnection cnArchConfig, string batchName)
        //{
        //    var startTime = DateTime.Now.ToLocalTime();
        //    Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
        //    int newID;
        //    try
        //    {
        //        var cmd = new SqlCommand
        //        {
        //            Connection = cnArchConfig,
        //            CommandType = CommandType.Text,
        //            CommandText = $@"
        //                INSERT INTO [Archiver].[LOG].[Batch]
        //                (
        //                    [BatchName]
        //                   ,[StartDate]
        //                )
	       //             VALUES 
	       //             (
        //                    '{batchName}'
        //                   ,'{DateTime.Now}'
        //                );
        //                SELECT SCOPE_IDENTITY();
        //            "
        //        };
        //        Logger.Info(Logging.DbCmdString(cmd));
        //        newID = Convert.ToInt32(cmd.ExecuteScalar());
        //        Logger.Info($"New BatchID: {newID}");
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
        //    return newID;
        //}

        public static long LogBatchUpdateDone(SqlConnection cnArchConfig, long batchID)
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
                        UPDATE [Archiver].[LOG].[Batch] 
	                    SET 
                            [EndDate] = GETDATE()
	                    WHERE [BatchID] = {batchID}
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

        public static int LogProcessInsert(SqlConnection cnArchConfig, string processName, long batchID, ArchiveTableConfig archiveTableConfig)
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
                          ,[SourceKey]
                          ,[DestinationServerName]
                          ,[DestinationDatabaseName]
                          ,[DestinationSchemaName]
                          ,[DestinationTableName]
                        )
	                    VALUES 
	                    (
                            '{processName}'
                           ,{batchID}
                           ,'{archiveTableConfig.LastRunStartDate}'
                           ,{archiveTableConfig.ConfigID}
                           ,'{archiveTableConfig.SourceServerName}'
                           ,'{archiveTableConfig.SourceDatabaseName}'
                           ,'{archiveTableConfig.SourceSchemaName}'
                           ,'{archiveTableConfig.SourceTableName}'
                           ,'{archiveTableConfig.SourceKey}'
                           ,'{archiveTableConfig.DestinationServerName}'
                           ,'{archiveTableConfig.DestinationDatabaseName}'
                           ,'{archiveTableConfig.DestinationSchemaName}'
                           ,'{archiveTableConfig.DestinationTableName}'
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

        public static long LogProcessUpdate(SqlConnection cnArchConfig, ArchiveTableConfig archiveTableConfig)
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
                        SET [EndDate] = '{archiveTableConfig.LastRunEndDate}'
                          ,[LastRunStatus] = '{archiveTableConfig.LastRunStatus}'
                          ,[ConfigID] = {archiveTableConfig.ConfigID}
                          ,[AutoConfigDisable] = {Convert.ToInt32(archiveTableConfig.AutoConfigDisable)}
                          ,[IsArchive] = {Convert.ToInt32(archiveTableConfig.IsArchive)}
                          ,[IsTail] = {Convert.ToInt32(archiveTableConfig.IsTail)}
                          ,[IsVerify] = {Convert.ToInt32(archiveTableConfig.IsVerify)}
                          ,[IsActive] = {Convert.ToInt32(archiveTableConfig.IsActive)}
                          ,[IsEnabled] = {Convert.ToInt32(archiveTableConfig.IsEnabled)}
                          ,[IsTestMode] = {Convert.ToInt32(archiveTableConfig.IsTestMode)}
                          ,[IsHammerMode] = {Convert.ToInt32(archiveTableConfig.IsHammerMode)}
                          ,[TestModeNoDelete] = {Convert.ToInt32(archiveTableConfig.TestModeNoDelete)}
                          ,[MaxRowsToMove] = {archiveTableConfig.MaxRowsToMove}
                          ,[SourceServerName] = '{archiveTableConfig.SourceServerName}'
                          ,[SourceDatabaseName] = '{archiveTableConfig.SourceDatabaseName}'
                          ,[SourceSchemaName] = '{archiveTableConfig.SourceSchemaName}'
                          ,[SourceTableName] = '{archiveTableConfig.SourceTableName}'
                          ,[DestinationServerName] = '{archiveTableConfig.DestinationServerName}'
                          ,[DestinationDatabaseName] = '{archiveTableConfig.DestinationDatabaseName}'
                          ,[DestinationSchemaName] = '{archiveTableConfig.DestinationSchemaName}'
                          ,[DestinationTableName] = '{archiveTableConfig.DestinationTableName}'
                          ,[IDColumn] = '{archiveTableConfig.IDColumn}'
                          ,[SourceKey] = '{archiveTableConfig.SourceKey}'
                          ,[IsSourceKeyUnique] = {Convert.ToInt32(archiveTableConfig.IsSourceKeyUnique)}
                          ,[DestinationKey] = '{archiveTableConfig.DestinationKey}'
                          ,[IsDestinationKeyUnique] = {Convert.ToInt32(archiveTableConfig.IsDestinationKeyUnique)}
                          ,[DateColumn] = '{archiveTableConfig.DateColumn}'
                          ,[SelectStartDate] = '{archiveTableConfig.SelectStartDate}'
                          ,[HighwaterMarkDate] = '{archiveTableConfig.HighwaterMarkDate}'
                          ,[HighwaterMarkID] = {archiveTableConfig.HighwaterMarkID}
                          ,[OtherCondition] = '{archiveTableConfig.OtherCondition.Replace("'", "''")}'
                          ,[ExcludeColumn] = '{archiveTableConfig.ExcludeColumn.Replace("'", "''")}'
                          ,[ValidationStatus] = '{archiveTableConfig.ValidationStatus}'
                          ,[RowsRemaining] = {archiveTableConfig.RowsRemaining}
                          ,[StatsOnly] = {Convert.ToInt32(archiveTableConfig.StatsOnly)}
                            ,[SourceInitialRowCount] = {archiveTableConfig.SourceInitialRowCount}
                            ,[SourceFinalRowCount] = {archiveTableConfig.SourceFinalRowCount}
                            ,[DestinationInitialRowCount] = {archiveTableConfig.DestinationInitialRowCount}
                            , [DestinationFinalRowCount] = {archiveTableConfig.DestinationFinalRowCount}
                        WHERE [ProcessID] = {archiveTableConfig.ProcessID}
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

        public static long LogProcessUpdate(SqlConnection cnArchConfig, ArchiveAndTailTableConfig archiveAndTailTableConfig)
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
                        SET [EndDate] = '{archiveAndTailTableConfig.LastRunEndDate}'
                          ,[LastRunStatus] = '{archiveAndTailTableConfig.LastRunStatus}'
                          ,[ConfigID] = {archiveAndTailTableConfig.ConfigID}
                          ,[AutoConfigDisable] = {Convert.ToInt32(archiveAndTailTableConfig.AutoConfigDisable)}
                          ,[IsArchive] = {Convert.ToInt32(archiveAndTailTableConfig.IsArchive)}
                          ,[IsTail] = {Convert.ToInt32(archiveAndTailTableConfig.IsTail)}
                          ,[IsVerify] = {Convert.ToInt32(archiveAndTailTableConfig.IsVerify)}
                          ,[IsActive] = {Convert.ToInt32(archiveAndTailTableConfig.IsActive)}
                          ,[IsEnabled] = {Convert.ToInt32(archiveAndTailTableConfig.IsEnabled)}
                          ,[IsTestMode] = {Convert.ToInt32(archiveAndTailTableConfig.IsTestMode)}
                          ,[IsHammerMode] = {Convert.ToInt32(archiveAndTailTableConfig.IsHammerMode)}
                          ,[TestModeNoDelete] = {Convert.ToInt32(archiveAndTailTableConfig.TestModeNoDelete)}
                          ,[MaxRowsToMove] = {archiveAndTailTableConfig.MaxRowsToMove}
                          ,[SourceServerName] = '{archiveAndTailTableConfig.SourceServerName}'
                          ,[SourceDatabaseName] = '{archiveAndTailTableConfig.SourceDatabaseName}'
                          ,[SourceSchemaName] = '{archiveAndTailTableConfig.SourceSchemaName}'
                          ,[SourceTableName] = '{archiveAndTailTableConfig.SourceTableName}'
                          ,[DestinationServerName] = '{archiveAndTailTableConfig.DestinationServerName}'
                          ,[DestinationDatabaseName] = '{archiveAndTailTableConfig.DestinationDatabaseName}'
                          ,[DestinationSchemaName] = '{archiveAndTailTableConfig.DestinationSchemaName}'
                          ,[DestinationTableName] = '{archiveAndTailTableConfig.DestinationTableName}'
                          ,[IDColumn] = '{archiveAndTailTableConfig.IDColumn}'
                          ,[SourceKey] = '{archiveAndTailTableConfig.SourceKey}'
                          ,[IsSourceKeyUnique] = {Convert.ToInt32(archiveAndTailTableConfig.IsSourceKeyUnique)}
                          ,[DestinationKey] = '{archiveAndTailTableConfig.DestinationKey}'
                          ,[IsDestinationKeyUnique] = {Convert.ToInt32(archiveAndTailTableConfig.IsDestinationKeyUnique)}
                          ,[DateColumn] = '{archiveAndTailTableConfig.DateColumn}'
                          ,[SelectStartDate] = '{archiveAndTailTableConfig.SelectStartDate}'
                          ,[HighwaterMarkDate] = '{archiveAndTailTableConfig.HighwaterMarkDate}'
                          ,[HighwaterMarkID] = {archiveAndTailTableConfig.HighwaterMarkID}
                          ,[OtherCondition] = '{archiveAndTailTableConfig.OtherCondition.Replace("'", "''")}'
                          ,[ExcludeColumn] = '{archiveAndTailTableConfig.ExcludeColumn.Replace("'", "''")}'
                          ,[ValidationStatus] = '{archiveAndTailTableConfig.ValidationStatus}'
                          ,[RowsRemaining] = {archiveAndTailTableConfig.RowsRemaining}
                          ,[StatsOnly] = {Convert.ToInt32(archiveAndTailTableConfig.StatsOnly)}
                            ,[SourceInitialRowCount] = {archiveAndTailTableConfig.SourceInitialRowCount}
                            ,[SourceFinalRowCount] = {archiveAndTailTableConfig.SourceFinalRowCount}
                            ,[DestinationInitialRowCount] = {archiveAndTailTableConfig.DestinationInitialRowCount}
                            , [DestinationFinalRowCount] = {archiveAndTailTableConfig.DestinationFinalRowCount}
                        WHERE [ProcessID] = {archiveAndTailTableConfig.ProcessID}
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

        public static long LogProcessUpdate(SqlConnection cnArchConfig, DeleteLogFileConfig deleteLogFileConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            deleteLogFileConfig.LastRunEndDate = DateTime.Now.ToLocalTime();
            long rowCount;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnArchConfig,
                    CommandType = CommandType.Text,
                    CommandText = $@"
                        UPDATE [Archiver].[LOG].[Process]
                        SET [EndDate] = '{deleteLogFileConfig.LastRunEndDate}'
                          ,[LastRunStatus] = '{deleteLogFileConfig.LastRunStatus}'
                          ,[ConfigID] = {deleteLogFileConfig.ConfigID}
                          ,[IsActive] = {Convert.ToInt32(deleteLogFileConfig.IsActive)}
                          ,[IsEnabled] = {Convert.ToInt32(deleteLogFileConfig.IsEnabled)}
                          ,[SourceServerName] = '{deleteLogFileConfig.ServerName}'
                          ,[ValidationStatus] = '{deleteLogFileConfig.ValidationStatus}'
                        WHERE [ProcessID] = {deleteLogFileConfig.ProcessID}
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

        public static int LogProcessInsert(SqlConnection cnArchConfig, string processName, long batchID, ArchiveAndTailTableConfig archiveAndTailTableConfig)
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
                          ,[SourceKey]
                            ,[DateColumn]
                            ,[SelectStartDate]
                            ,[OtherCondition]
                          ,[DestinationServerName]
                          ,[DestinationDatabaseName]
                          ,[DestinationSchemaName]
                          ,[DestinationTableName]
                        )
	                    VALUES 
	                    (
                            '{processName}'
                           ,{batchID}
                           ,'{DateTime.Now}'
                           ,{archiveAndTailTableConfig.ConfigID}
                           ,'{archiveAndTailTableConfig.SourceServerName}'
                           ,'{archiveAndTailTableConfig.SourceDatabaseName}'
                           ,'{archiveAndTailTableConfig.SourceSchemaName}'
                           ,'{archiveAndTailTableConfig.SourceTableName}'
                           ,'{archiveAndTailTableConfig.SourceKey}'
                           ,'{archiveAndTailTableConfig.DateColumn}'
                           ,'{archiveAndTailTableConfig.SelectStartDate}'
                           ,'{archiveAndTailTableConfig.OtherCondition.Replace("'", "''")}'
                           ,'{archiveAndTailTableConfig.DestinationServerName}'
                           ,'{archiveAndTailTableConfig.DestinationDatabaseName}'
                           ,'{archiveAndTailTableConfig.DestinationSchemaName}'
                           ,'{archiveAndTailTableConfig.DestinationTableName}'
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

        public static int LogProcessInsert(SqlConnection cnArchConfig, string processName, long batchID, TruncateLedgerTableConfig truncateTableConfig)
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
                           ,[ServerName]
                           ,[DatabaseName]
                           ,[SchemaName]
                           ,[TableName]
                           ,[TestModeNoDelete]
                        )
	                    VALUES 
	                    (
                            '{processName}'
                           ,{batchID}
                           ,'{DateTime.Now}'
                           ,{truncateTableConfig.ConfigID}
                           ,'{truncateTableConfig.SourceServerName}'
                           ,'{truncateTableConfig.SourceDatabaseName}'
                           ,'{truncateTableConfig.SourceSchemaName}'
                           ,'{truncateTableConfig.SourceTableName}'
                           ,'{truncateTableConfig.TestModeNoDelete}'
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

        public static int LogProcessInsert(SqlConnection cnArchConfig, string processName, long batchID, DeleteLogFileConfig deleteLogFileConfig)
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
                           ,[RetentionDays]
                        )
	                    VALUES 
	                    (
                            '{processName}'
                           ,{batchID}
                           ,'{DateTime.Now}'
                           ,{deleteLogFileConfig.ConfigID}
                           ,'{deleteLogFileConfig.ServerName}'
                           ,{deleteLogFileConfig.LogRetentionDays}
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
                Logger.Error($@"ERROR: {cnArchConfig.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new ApplicationException($@"{cnArchConfig.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }

            return newID;
        }

        public static int LogProcessInsert(SqlConnection cnArchConfig, string processName, long batchID, PlantServerConfig plantServerConfig)
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
                           ,[ServerName]
                        )
	                    VALUES 
	                    (
                            '{processName}'
                           ,{batchID}
                           ,'{DateTime.Now}'
                           ,{plantServerConfig.ID}
                           ,'{plantServerConfig.ServerName}'
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

        public static long LogProcessUpdateIsKeyColumnUnique(SqlConnection cnArchConfig, int processID, ArchiveTableConfig mergeTableConfig)
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
                           [IsKeyColumnUnique] = '{mergeTableConfig.IsSourceKeyUnique}'
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

        public static long LogProcessUpdateIsKeyColumnUnique(SqlConnection cnArchConfig, int processID, ArchiveAndTailTableConfig ArchiveAndTailTableConfig)
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
                           [IsKeyColumnUnique] = '{ArchiveAndTailTableConfig.IsSourceKeyUnique}'
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

        public static long LogProcessUpdateInitialRowCount(SqlConnection cnArchConfig, int processID, ArchiveTableConfig mergeTableConfig)
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
                            [ValidationStatus] = '{mergeTableConfig.ValidationStatus}'
                           ,[IsKeyColumnUnique] = '{mergeTableConfig.IsSourceKeyUnique}'
                           ,[WhereClause] = '{mergeTableConfig.WhereClause.Replace("'", "''")}'
                           ,[InitialRowCount] = {mergeTableConfig.DestinationInitialRowCount} 
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

        public static long LogProcessUpdateInitialRowCount(SqlConnection cnArchConfig, int processID, ArchiveAndTailTableConfig ArchiveAndTailTableConfig)
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
                            [ValidationStatus] = '{ArchiveAndTailTableConfig.ValidationStatus}'
                           ,[IsKeyColumnUnique] = '{ArchiveAndTailTableConfig.IsSourceKeyUnique}'
                           ,[WhereClause] = '{ArchiveAndTailTableConfig.WhereClause.Replace("'", "''")}'
                           ,[InitialRowCount] = {ArchiveAndTailTableConfig.DestinationInitialRowCount} 
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

        public static long LogProcessUpdateInitialRowCount(SqlConnection cnArchConfig, int processID, TruncateLedgerTableConfig truncateTableConfig)
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
                            [ValidationStatus] = '{truncateTableConfig.ValidationStatus}'
                           ,[InitialRowCount] = {truncateTableConfig.RowsTruncated} 
                           ,[RowsToDelete] = {truncateTableConfig.RowsTruncated}
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

        public static long LogProcessUpdateDone(SqlConnection cnArchConfig, int processID, ArchiveTableConfig archiveTableConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            long rowCount;
            try
            {
                DateTime dt1 = DateTime.Parse("01/01/1900");
                if (archiveTableConfig.SourceMinDate < dt1) archiveTableConfig.SourceMinDate = dt1;
                if (archiveTableConfig.SourceMaxDate < dt1) archiveTableConfig.SourceMaxDate = dt1;
                if (archiveTableConfig.DestinationMinDate < dt1) archiveTableConfig.DestinationMinDate = dt1;
                if (archiveTableConfig.DestinationMaxDate < dt1) archiveTableConfig.DestinationMaxDate = dt1;
                if (archiveTableConfig.LastRunStartDate < dt1) archiveTableConfig.LastRunStartDate = dt1;
                if (archiveTableConfig.LastRunEndDate < dt1) archiveTableConfig.LastRunEndDate = dt1;
                var cmd = new SqlCommand
                {
                    Connection = cnArchConfig,
                    CommandType = CommandType.Text,
                    CommandText = $@"
                        UPDATE [Archiver].[LOG].[Process] 
	                    SET 
                            [EndDate] = GETDATE()
                            ,[LastRunStatus] = '{archiveTableConfig.LastRunStatus}'
                            ,[RowsMerged] = {archiveTableConfig.RowsAffected} 
                            ,[InitialRowCount] = {archiveTableConfig.DestinationInitialRowCount}
                            ,[FinalRowCount] = {archiveTableConfig.DestinationFinalRowCount}
                            ,[SourceInitialRowCount] = {archiveTableConfig.SourceInitialRowCount}
                            ,[SourceFinalRowCount] = {archiveTableConfig.SourceFinalRowCount}
                            ,[SourceMinDate] = '{archiveTableConfig.SourceMinDate}'
                            ,[SourceMaxDate] = '{archiveTableConfig.SourceMaxDate}'
                            ,[DestinationInitialRowCount] = {archiveTableConfig.DestinationInitialRowCount}
                            ,[DestinationFinalRowCount] = {archiveTableConfig.DestinationFinalRowCount}
                            ,[DestinationMinDate] = '{archiveTableConfig.DestinationMinDate}'
                            ,[DestinationMaxDate] = '{archiveTableConfig.DestinationMaxDate}'
                            ,[LastRunStartDate] = '{archiveTableConfig.LastRunStartDate}'        
                            ,[LastRunEndDate] = '{archiveTableConfig.LastRunEndDate}'        
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

        public static long LogProcessUpdateNotFoundDone(SqlConnection cnArchConfig, int processID, ArchiveTableConfig archiveTableConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            long rowCount;
            try
            {
                DateTime dt1 = DateTime.Parse("01/01/1900");
                if (archiveTableConfig.LastRunStartDate < dt1) archiveTableConfig.LastRunStartDate = dt1;
                if (archiveTableConfig.LastRunEndDate < dt1) archiveTableConfig.LastRunEndDate = dt1;
                var cmd = new SqlCommand
                {
                    Connection = cnArchConfig,
                    CommandType = CommandType.Text,
                    CommandText = $@"
                        UPDATE [Archiver].[LOG].[Process] 
	                    SET 
                            [EndDate] = GETDATE()
                            ,[LastRunStatus] = '{archiveTableConfig.LastRunStatus}'
                            ,[RowsMerged] = {archiveTableConfig.RowsAffected} 
                            ,[LastRunStartDate] = '{archiveTableConfig.LastRunStartDate}'        
                            ,[LastRunEndDate] = '{archiveTableConfig.LastRunEndDate}'        
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

        public static long LogProcessUpdateNotFoundDone(SqlConnection cnArchConfig, int processID, DeleteLogFileConfig deleteLogFileConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            long rowCount;
            try
            {
                DateTime dt1 = DateTime.Parse("01/01/1900");
                if (deleteLogFileConfig.LastRunStartDate < dt1) deleteLogFileConfig.LastRunStartDate = dt1;
                if (deleteLogFileConfig.LastRunEndDate < dt1) deleteLogFileConfig.LastRunEndDate = dt1;
                var cmd = new SqlCommand
                {
                    Connection = cnArchConfig,
                    CommandType = CommandType.Text,
                    CommandText = $@"
                        UPDATE [Archiver].[LOG].[Process] 
	                    SET 
                            [EndDate] = GETDATE()
                            ,[LastRunStatus] = '{deleteLogFileConfig.LastRunStatus}'
                            ,[RowsMerged] = {deleteLogFileConfig.RowsAffected} 
                            ,[LastRunStartDate] = '{deleteLogFileConfig.LastRunStartDate}'        
                            ,[LastRunEndDate] = '{deleteLogFileConfig.LastRunEndDate}'        
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

        public static long LogProcessUpdateDone(SqlConnection cnArchConfig, int processID, ArchiveAndTailTableConfig ArchiveAndTailTableConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            long rowCount;
            try
            {
                DateTime dt1 = DateTime.Parse("01/01/1900");
                if (ArchiveAndTailTableConfig.SourceMinDate < dt1) ArchiveAndTailTableConfig.SourceMinDate = dt1;
                if (ArchiveAndTailTableConfig.SourceMaxDate < dt1) ArchiveAndTailTableConfig.SourceMaxDate = dt1;
                if (ArchiveAndTailTableConfig.DestinationMinDate < dt1) ArchiveAndTailTableConfig.DestinationMinDate = dt1;
                if (ArchiveAndTailTableConfig.DestinationMaxDate < dt1) ArchiveAndTailTableConfig.DestinationMaxDate = dt1;
                if (ArchiveAndTailTableConfig.LastRunStartDate < dt1) ArchiveAndTailTableConfig.LastRunStartDate = dt1;
                if (ArchiveAndTailTableConfig.LastRunEndDate < dt1) ArchiveAndTailTableConfig.LastRunEndDate = dt1;
                var cmd = new SqlCommand
                {
                    Connection = cnArchConfig,
                    CommandType = CommandType.Text,
                    CommandText = $@"
                        UPDATE [Archiver].[LOG].[Process] 
	                    SET 
                            [EndDate] = GETDATE()
                            ,[LastRunStatus] = '{ArchiveAndTailTableConfig.LastRunStatus}'
                            ,[RowsMerged] = {ArchiveAndTailTableConfig.RowsAffected} 
                            ,[InitialRowCount] = {ArchiveAndTailTableConfig.SourceInitialRowCount}
                            ,[FinalRowCount] = {ArchiveAndTailTableConfig.SourceFinalRowCount}
                            ,[SourceRowCount] = {ArchiveAndTailTableConfig.SourceRowCount}
                            ,[SourceMinDate] = '{ArchiveAndTailTableConfig.SourceMinDate}'
                            ,[SourceMaxDate] = '{ArchiveAndTailTableConfig.SourceMaxDate}'
                            ,[TargetRowCount] = {ArchiveAndTailTableConfig.DestinationFinalRowCount}
                            ,[TargetMinDate] = '{ArchiveAndTailTableConfig.DestinationMinDate}'
                            ,[TargetMaxDate] = '{ArchiveAndTailTableConfig.DestinationMaxDate}'
                            ,[MergeStartDate] = '{ArchiveAndTailTableConfig.LastRunStartDate}'        
                            ,[MergeEndDate] = '{ArchiveAndTailTableConfig.LastRunEndDate}'        
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

        //public static long LogProcessUpdateDone(SqlConnection cnArchConfig, int processID, OldArchiveTableConfig archiveTableConfig)
        //{
        //    var startTime = DateTime.Now.ToLocalTime();
        //    Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
        //    long rowCount;
        //    try
        //    {
        //        DateTime dt1 = DateTime.Parse("01/01/1900");
        //        if (archiveTableConfig.SFMinDate < dt1) archiveTableConfig.SFMinDate = dt1;
        //        if (archiveTableConfig.SFMaxDate < dt1) archiveTableConfig.SFMaxDate = dt1;
        //        var cmd = new SqlCommand
        //        {
        //            Connection = cnArchConfig,
        //            CommandType = CommandType.Text,
        //            CommandText = $@"
        //                UPDATE [Archiver].[LOG].[Process] 
	       //             SET 
        //                    [EndDate] = GETDATE()
        //                    ,[LastRunStatus] = '{archiveTableConfig.LastRunStatus}'
        //                    ,[RowsArchived] = {archiveTableConfig.MovedRows}
        //                    ,[RowsDeleted] = {archiveTableConfig.DeletedRows}
        //                    ,[RowsToDelete] = {archiveTableConfig.RowsToDelete}
        //                    ,[FinalRowCountCalculated] = {archiveTableConfig.FinalRowCountCalculated}
        //                    ,[FinalRowCount] = {archiveTableConfig.FinalRowCountActual}
        //                    ,[TotalArchivedRows] = {archiveTableConfig.TotalMovedRows}
        //                    ,[SFMinDate] = '{archiveTableConfig.SFMinDate}'
        //                    ,[SFMaxDate] = '{archiveTableConfig.SFMaxDate}'
        //                    ,[ArchiveStartTime] = '{archiveTableConfig.ArchiveStartTime}'        
	       //             WHERE [ProcessID] = {processID}
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

        public static long LogProcessUpdateDone(SqlConnection cnArchConfig, int processID, PlantServerConfig plantServerConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            long rowCount = 0;
            try
            {
                DateTime dt1 = DateTime.Parse("01/01/1900");
                //if (archiveTableConfig.SFMinDate < dt1) archiveTableConfig.SFMinDate = dt1;
                //if (archiveTableConfig.SFMaxDate < dt1) archiveTableConfig.SFMaxDate = dt1;
                //var cmd = new SqlCommand
                //{
                //    Connection = cnArchConfig,
                //    CommandType = CommandType.Text,
                //    CommandText = $@"
                //        UPDATE [Archiver].[LOG].[Process] 
	               //     SET 
                //            [EndDate] = GETDATE()
                //            ,[LastRunStatus] = '{plantServerConfig.LastRunStatus}'
                //            ,[RowsArchived] = {plantServerConfig.MovedRows}
                //            ,[RowsDeleted] = {archiveTableConfig.DeletedRows}
                //            ,[RowsToDelete] = {archiveTableConfig.RowsToDelete}
                //            ,[FinalRowCountCalculated] = {archiveTableConfig.FinalRowCountCalculated}
                //            ,[FinalRowCount] = {archiveTableConfig.FinalRowCountActual}
                //            ,[TotalArchivedRows] = {archiveTableConfig.TotalMovedRows}
                //            ,[SFMinDate] = '{archiveTableConfig.SFMinDate}'
                //            ,[SFMaxDate] = '{archiveTableConfig.SFMaxDate}'
                //            ,[ArchiveStartTime] = '{archiveTableConfig.ArchiveStartTime}'        
	               //     WHERE [ProcessID] = {processID}
                //    "
                //};
                //Logger.Info(Logging.DbCmdString(cmd));
                //rowCount = cmd.ExecuteNonQuery();
                //Logger.Info("ROWS: " + rowCount);
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

        public static long LogProcessUpdateDone(SqlConnection cnArchConfig, int processID, TruncateLedgerTableConfig truncateTableConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            long rowCount;
            try
            {
                DateTime dt1 = DateTime.Parse("01/01/1900");
                //if (truncateTableConfig.SFMinDate < dt1) truncateTableConfig.SFMinDate = dt1;
                //if (truncateTableConfig.SFMaxDate < dt1) truncateTableConfig.SFMaxDate = dt1;
                var cmd = new SqlCommand
                {
                    Connection = cnArchConfig,
                    CommandType = CommandType.Text,
                    CommandText = $@"
                        UPDATE [Archiver].[LOG].[Process] 
	                    SET 
                            [EndDate] = GETDATE()
                            ,[LastRunStatus] = '{truncateTableConfig.LastRunStatus}'
                            ,[RowsDeleted] = {truncateTableConfig.RowsTruncated}
                            ,[FinalRowCountCalculated] = 0
                            ,[FinalRowCount] = 0
                           ,[ArchiveStartTime] = '{truncateTableConfig.LastRunStartDate}'        
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

        public static int LogStepInsert(SqlConnection cnArchConfig, string stepName, int processID)
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
                        INSERT INTO [Archiver].[LOG].[Step]
                        (
                            [StepName]
                           ,[ProcessID]
                           ,[StartDate]
                        )
	                    VALUES 
	                    (
                            '{stepName}'
                           ,{processID}
                           ,'{DateTime.Now}'
                        );
                        SELECT SCOPE_IDENTITY();
                    "
                };
                Logger.Info(Logging.DbCmdString(cmd));
                newID = Convert.ToInt32(cmd.ExecuteScalar());
                Logger.Info($"New StepID: {newID}");
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

        public static long LogStepUpdateDone(SqlConnection cnArchConfig, ProcessStep step)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            long rowCount;
            try
            {
                for (int i = 0; i < 10; i++)
                {
                    step.Statement = step.Statement.Replace("    ", " ").Replace("   ", " ").Replace("  ", " ");
                }
                var cmd = new SqlCommand
                {
                    Connection = cnArchConfig,
                    CommandType = CommandType.Text,
                    CommandText = $@"
                        UPDATE [Archiver].[LOG].[Step] 
	                    SET [RowsAffected] = {step.RowsAffected}
                           ,[Statement] = '{step.Statement.Replace("'", "''")}'
                           ,[EndDate] = GETDATE()
	                    WHERE [StepID] = {step.StepID}
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

        public static int LogConnectionStringInsert(SqlConnection cnArchConfig, long batchID, int processID, string connectionStringType, string connectionString)
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
                        INSERT INTO [Archiver].[LOG].[ConnectionString]
                        (
                            [BatchID]
                           ,[ProcessID]
                           ,[ConnectionStringType]
                           ,[ConnectionString]
                        )
	                    VALUES 
	                    (
                           {batchID}
                          ,{processID}
                          ,'{connectionStringType}'
                          ,'{connectionString}'
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

        //public static long LogErrorInsert(SqlConnection cnArchConfig, long batchID, long processID, long stepID, string comment, string statement)
        //{
        //    var startTime = DateTime.Now.ToLocalTime();
        //    Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
        //    long newID;
        //    try
        //    {
        //        var cmd = new SqlCommand
        //        {
        //            Connection = cnArchConfig,
        //            CommandType = CommandType.Text,
        //            CommandText = $@"
        //                INSERT INTO [Archiver].[LOG].[Error]
        //                (
        //                    [BatchID]
        //                    ,[ProcessID]
        //                   ,[StepID]
        //                   ,[Comment]
        //                   ,[Statement]
        //                )
	       //             VALUES 
	       //             (
        //                    {batchID}
        //                   ,{processID}
        //                   ,{stepID}
        //                   ,'{comment}'
        //                   ,'{statement}'
        //                );
        //                SELECT SCOPE_IDENTITY();
        //            "
        //        };
        //        Logger.Info(Logging.DbCmdString(cmd));
        //        newID = Convert.ToInt32(cmd.ExecuteScalar());
        //        Logger.Info($"New ErrorID: {newID}");
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
        //    return newID;
        //}

        public static long LogErrorInsert(SqlConnection cnArchConfig, long processID, long stepID, string comment, string statement)
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
                            [ProcessID]
                           ,[StepID]
                           ,[Comment]
                           ,[Statement]
                        )
	                    VALUES 
	                    (
                           {processID}
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

        public static long LogErrorInsert(SqlConnection cnArchConfig, long stepID, string comment, string statement)
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
                           [StepID]
                           ,[Comment]
                           ,[Statement]
                        )
	                    VALUES 
	                    (
                           {stepID}
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