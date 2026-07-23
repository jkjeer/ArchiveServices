using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Text;
using ArchiveServices.Common.Types.Archie;
using ArchiveServices.Common.Utilities;
using NLog;

namespace ArchiveServices.Common.Managers.Archie
{
    public class GetCandidateTableConfigManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private static SqlCommand GetPlantServerConfigs(List<string> filters)
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
                        ,[PlantCode]
                        ,[ServerName]
                        ,[DatabaseName]
                        ,[SchemaName]
                        ,[TableName]
                        ,[IsActive]
                        ,[IsEnabled]
                        ,[IsRunning]
                        ,[MaxDeleteRowsPerBatch]
                        ,[KeyColumn]
                        ,[DateColumn]
                        ,[RetentionDays]
                        ,[OtherCondition]
                        ,[CreatedBy]
                        ,[LastEditBy]
                    FROM [dbo].[cfgTablesArchive]
                    WHERE
                       {filterString} 
                    ORDER BY [GroupName]
                        ,[PlantCode]
                        ,[ServerName]
                        ,[DatabaseName]
                        ,[SchemaName]
                        ,[TableName]
                ";
            }
            catch (Exception e)
            {
                throw new Exception(e.ToString());
            }
            return cmd;
        }

        public static PlantServerConfigs GetPlantServerConfigs(SqlConnection cnArchConfig, string groupName, string plantCode, string serverName)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            var configs = new PlantServerConfigs();
            var filters = new List<string>();
            var parameters = new List<SqlParameter>();
            try
            {
                filters.Add("[IsActive] = @isActive AND \r\n");
                parameters.Add(new SqlParameter("@isActive", SqlDbType.Bit));
                parameters.Last().Value = 1;
                filters.Add("[IsEnabled] = @isEnabled AND \r\n");
                parameters.Add(new SqlParameter("@isEnabled", SqlDbType.Bit));
                parameters.Last().Value = 1;

                filters.Add("[NextRunDate] <= GETDATE() AND \r\n");

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
                    filters.Add("[ServerName] = @serverName AND \r\n");
                    parameters.Add(new SqlParameter("@serverName", SqlDbType.VarChar));
                    parameters.Last().Value = serverName;
                }
                filters[filters.Count - 1] = filters.Last().ToUpper().Replace(" AND \r\n", "");
                var cmd = GetPlantServerConfigs(filters);
                foreach (var parameter in parameters) cmd.Parameters.Add(parameter);
                cmd.Connection = cnArchConfig;
                Logger.Info(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        var config = new PlantServerConfig();
                        config.ID = CastDbNull.To(reader["ConfigID"], 0);
                        config.ServerName = CastDbNull.To(reader["ServerName"], string.Empty).Trim();
                        config.IsActive = CastDbNull.To(reader["IsActive"], false);
                        config.IsEnabled = CastDbNull.To(reader["IsEnabled"], false);
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

        //public static long UpdateArchiveTableConfigIsRunningTrue(SqlConnection cnArchConfig, OldArchiveTableConfig archiveTableConfig, out string statement)
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
        //                UPDATE xx
        //                SET 
        //                    [IsRunning] = 'TRUE'
        //                    ,[ProcessID] = {archiveTableConfig.ProcessID}
        //                    ,[BITimeStamp] = GETDATE()
        //                FROM [dbo].[cfgTablesArchive] xx
        //                WHERE [ConfigID] = {archiveTableConfig.ID}
        //            "
        //        };
        //        statement = cmd.CommandText;
        //        Logger.Info(Logging.DbCmdString(cmd));
        //        rowCount = Convert.ToInt64(cmd.ExecuteNonQuery());
        //        Logger.Info("ROWS: " + rowCount);
        //    }
        //    catch (Exception e)
        //    {
        //        Logger.Error($"ERROR: {cnArchConfig.ConnectionString}  {e.Message}: {e.StackTrace}");
        //        throw new Exception($"{cnArchConfig.ConnectionString}  {e.Message}: {e.StackTrace}");
        //    }
        //    finally
        //    {
        //        Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
        //    }
        //    return rowCount;
        //}

        public static long UpdateArchiveTableConfigIsKeyColumnUnique(SqlConnection cnArchConfig, OldArchiveTableConfig archiveTableConfig, out string statement)
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
                            [IsKeyColumnUnique] = '{archiveTableConfig.IsKeyColumnUnique}'
                            ,[BITimeStamp] = GETDATE()
                        FROM [dbo].[cfgTablesArchive] xx
                        WHERE [ConfigID] = {archiveTableConfig.ID}
                    "
                };
                statement = cmd.CommandText;
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

        public static long UpdateArchiveTableConfigDone(SqlConnection cnArchConfig, OldArchiveTableConfig archiveTableConfig, out string statement)
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
                            [IsRunning] = 'FALSE'
                            ,[TotalArchivedRows] = {archiveTableConfig.TotalMovedRows}
                            ,[SFRows] = {archiveTableConfig.FinalRowCountActual}
                            ,[BITimeStamp] = GETDATE()
                        FROM [dbo].[cfgTablesArchive] xx
                        WHERE [ConfigID] = {archiveTableConfig.ID}
                    "
                };
                statement = cmd.CommandText;
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

        public static long UpdateArchiveTableConfigIsEnabled(SqlConnection cnArchConfig, OldArchiveTableConfig archiveTableConfig, bool isEnabled)
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
                            [IsEnabled] = {Convert.ToInt32(isEnabled)}
                            ,[BITimeStamp] = GETDATE()
                        FROM [dbo].[cfgTablesArchive] xx
                        WHERE [ConfigID] = {archiveTableConfig.ID}
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

        public static long UpdateArchiveTableConfigInitialRowCount(SqlConnection cnArchConfig, OldArchiveTableConfig archiveTableConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            long rowCount;
            try
            {
                var cmd = new SqlCommand();
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = $@"
                    UPDATE xx
                    SET 
                        [InitialRowCount] = CASE WHEN [InitialRowCount] = 0 THEN {archiveTableConfig.InitialRowCount} ELSE [InitialRowCount] END
                        ,[BITimeStamp] = GETDATE()
                    FROM [dbo].[cfgTablesArchive] xx
                    WHERE [ConfigID] = {archiveTableConfig.ID}
                        AND ISNULL([InitialRowCount], 0) = 0
                ";
                cmd.Connection = cnArchConfig;
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