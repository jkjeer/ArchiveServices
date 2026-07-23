using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Text;
using ArchiveServices.Common.Types.Archie;
using ArchiveServices.Common.Utilities;
using NLog;

namespace ArchiveServices.Common.Managers.Archie
{
    public class TruncateLedgerTableConfigManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static TruncateTableConfigs GetTruncateTableConfigs(SqlConnection cnArchConfig, string groupName, string plantCode, string serverName, string databaseName, string schemaName, string tableName, bool useHighwaterMark, bool statsOnly)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            var testMode = Convert.ToBoolean(ConfigurationManager.AppSettings["TestMode"]);
            var testModeNoDelete = Convert.ToBoolean(ConfigurationManager.AppSettings["TestModeNoDelete"]);
            var configs = new TruncateTableConfigs();
            var filters = new List<string>();
            var parameters = new List<SqlParameter>();
            try
            {
                filters.Add("[IsTruncate] = 1 AND \r\n");
                if (statsOnly)
                {
                }
                else if (testMode || testModeNoDelete)
                {
                    filters.Add("[IsTestMode] = 1 AND \r\n");
                }
                else
                {
                    filters.Add("[IsActive] = 1 AND \r\n");
                    filters.Add("[IsEnabled] = 1 AND \r\n");
                    filters.Add("[IsTestMode] = 0 AND \r\n");
                }

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
                    filters.Add("[SourceServerName] = @sourceServerName AND \r\n");
                    parameters.Add(new SqlParameter("@sourceServerName", SqlDbType.VarChar));
                    parameters.Last().Value = serverName;
                }
                if (!string.IsNullOrEmpty(databaseName))
                {
                    filters.Add("[SourceDatabaseName] = @sourceDatabaseName AND \r\n");
                    parameters.Add(new SqlParameter("@sourceDatabaseName", SqlDbType.VarChar));
                    parameters.Last().Value = databaseName;
                }
                if (!string.IsNullOrEmpty(schemaName))
                {
                    filters.Add("[SourceSchemaName] = @sourceSchemaName AND \r\n");
                    parameters.Add(new SqlParameter("@sourceSchemaName", SqlDbType.VarChar));
                    parameters.Last().Value = schemaName;
                }
                if (!string.IsNullOrEmpty(tableName))
                {
                    filters.Add("[SourceTableName] = @sourceTableName AND \r\n");
                    parameters.Add(new SqlParameter("@sourceTableName", SqlDbType.VarChar));
                    parameters.Last().Value = tableName;
                }
                filters[filters.Count - 1] = filters.Last().ToUpper().Replace(" AND \r\n", "");
                var cmd = GetTruncateTableConfig(filters);
                foreach (var parameter in parameters) cmd.Parameters.Add(parameter);
                cmd.Connection = cnArchConfig;
                Logger.Info(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        var config = new TruncateLedgerTableConfig();
                        config.ID = CastDbNull.To(reader["ConfigID"], 0);
                        config.IsActive = CastDbNull.To(reader["IsActive"], false);
                        config.IsEnabled = CastDbNull.To(reader["IsEnabled"], false);
                        config.IsTestMode = CastDbNull.To(reader["IsTestMode"], false);
                        config.IsRunning = CastDbNull.To(reader["IsRunning"], false);
                        config.TargetServerName = CastDbNull.To(reader["TargetServerName"], string.Empty).Trim();
                        config.TargetDatabaseName = CastDbNull.To(reader["TargetDatabaseName"], string.Empty).Trim();
                        config.TargetSchemaName = CastDbNull.To(reader["TargetSchemaName"], string.Empty).Trim();
                        config.TargetTableName = CastDbNull.To(reader["TargetTableName"], string.Empty).Trim();
                        config.TruncateFrequencyDays = CastDbNull.To(reader["TruncateFrequencyDays"], 0);
                        config.NextTruncateDate = CastDbNull.To(reader["NextTruncateDate"], DateTime.MinValue);
                        config.CreatedBy = reader.Get<string>("CreatedBy");
                        config.LastEditBy = reader.Get<string>("LastEditBy");

                        //var keyColumnList = config.SourceKey.Split(',').ToList();
                        //foreach (var keyColumn in keyColumnList)
                        //{
                        //    config.SourceKeyColumns.Add(new KeyColumn(keyColumn));
                        //}
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
                Logger.Info($"END: {Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds)} SECONDS");
            }
            return configs;
        }

        private static SqlCommand GetTruncateTableConfig(List<string> filters)
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
                        ,[IsArchive]
                        ,[IsTail]
                        ,[IsVerify]
                        ,[IsActive]
                        ,[IsEnabled]
                        ,[IsTestMode]
                        ,[IsHammerMode]
                        ,[IsRunning]
                        ,[GroupName]
                        ,[PlantCode]
                        ,[SourceServerName]
                        ,[SourceDatabaseName]
                        ,[SourceSchemaName]
                        ,[SourceTableName]
                        ,[DestinationServerName]
                        ,[DestinationDatabaseName]
                        ,[DestinationSchemaName]
                        ,[DestinationTableName]
                        ,[SourceKey]
                        ,[IsSourceKeyUnique]
                        ,[DestinationKey]
                        ,[IsDestinationKeyUnique]
                        ,[DateColumn]
                        ,[SelectStartDate]
                        ,[IDColumn]
                        ,[HighwaterMarkDate]
                        ,[HighwaterMarkID]
                        ,[OtherCondition]
                        ,[RunFrequencyDays]
                        ,[NextRunDate]
                        ,[CreatedBy]
                        ,[LastEditBy]
                    FROM [dbo].[cfgTablesMerge]
                    WHERE
                       {filterString} 
                    ORDER BY [GroupName]
                        ,[PlantCode]
                        ,[SourceServerName]
                        ,[SourceDatabaseName]
                        ,[SourceSchemaName]
                        ,[SourceTableName]
                ";
            }
            catch (Exception e)
            {
                throw new Exception(e.ToString());
            }
            return cmd;
        }

        public static KeyColumn GetKeyData(SqlConnection cn, string schemaName, string tableName, string columnName)
        {
            KeyColumn keyColumn = new KeyColumn(columnName);
            try
            {
                var cmd = new SqlCommand();
                cmd = GetKeyColumnMetadata();
                cmd.Parameters["@SchemaName"].Value = schemaName;
                cmd.Parameters["@TableName"].Value = tableName;
                cmd.Parameters["@ColumnName"].Value = columnName;
                cmd.Connection = cn;
                Logger.Info(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        keyColumn.DataType = CastDbNull.To<string>(reader["DataType"], string.Empty).Trim();
                        keyColumn.MaxLength = CastDbNull.To<Int16>(reader["MaxLength"], 0);
                        keyColumn.Precision = CastDbNull.To<Byte>(reader["Precision"], 0);
                        keyColumn.Scale = CastDbNull.To<Byte>(reader["Scale"], 0);
                    }
                    reader.Close();
                }
                //Logger.Info($@"columnType = {DataType}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {cn.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return keyColumn;
        }

        private static SqlCommand GetKeyColumnMetadata()
        {
            var cmd = new SqlCommand();
            try
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = $@"
                    SELECT '[' + c.name + '] [' + UPPER(t.name) + ']'
                           + CASE
                                 WHEN t.name IN ( 'bigint', 'bit', 'int', 'money', 'smallint', 'smallmoney', 'tinyint', 'float', 'real',
                                                  'date', 'datetime', 'datetime2', 'smalldatetime', 'datetimeoffset', 'time', 'image',
                                                  'uniqueidentifier', 'sysname', 'sql_variant', 'xml'
                                                ) THEN
                                     ''
                                 WHEN t.name IN ( 'varchar', 'char', 'text', 'binary', 'varbinary', 'nchar', 'nvarchar', 'ntext' ) THEN
                                     '(' + CASE WHEN c.max_length = -1 THEN 'MAX' else CONVERT(VARCHAR, c.max_length ) END + ')'
                                 WHEN t.name IN ( 'decimal', 'numeric' ) THEN
                                     '(' + CONVERT(VARCHAR, c.precision) + ', ' + CONVERT(VARCHAR, c.scale) + ')'
                                 ELSE
                                     '***'
                             END + ' NULL' + CASE
                                                 WHEN c.column_id = x.MaxColId THEN
                                                     ''
                                                 ELSE
                                                     ','
                                             END AS FieldDef,
                           c.column_id,
                           x.MaxColId,
                           c.max_length AS MaxLength,
                           c.precision AS Precision,
                           c.scale AS Scale,
                           t.name AS DataType,
                           t.schema_id,
                           t.max_length AS t_max_length,
                           t.precision AS t_precision,
                           t.scale AS t_scale
                    FROM sys.columns c
                        INNER JOIN sys.types t
                            ON t.system_type_id = c.system_type_id
							AND t.user_type_id = c.user_type_id
	                    INNER JOIN sys.tables tb 
		                    ON TB.OBJECT_ID = c.object_id
						INNER JOIN sys.schemas s ON s.schema_id = tb.schema_id
                        OUTER APPLY
                    (
                        SELECT MAX(column_id) AS MaxColId
                        FROM sys.columns c2
                        WHERE c2.object_id = c.object_id
                    ) x
                    WHERE s.name = @SchemaName
                        AND tb.name = @TableName
                        AND c.name = @ColumnName
                    ORDER BY c.column_id;
                ";
            }
            catch (Exception e)
            {
                throw new Exception(e.ToString());
            }
            cmd.Parameters.Add(new SqlParameter("@SchemaName", SqlDbType.VarChar));
            cmd.Parameters.Add(new SqlParameter("@TableName", SqlDbType.VarChar));
            cmd.Parameters.Add(new SqlParameter("@ColumnName", SqlDbType.VarChar));
            return cmd;
        }

        public static long UpdateTruncateTableConfigIsRunningTrue(SqlConnection cnArchConfig, TruncateLedgerTableConfig truncateTableConfig, out string statement)
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
                            [IsRunning] = 'TRUE'
                            ,[ProcessID] = {truncateTableConfig.ProcessID}
                            ,[BITimeStamp] = GETDATE()
                        FROM [dbo].[cfgTablesMerge] xx
                        WHERE [ConfigID] = {truncateTableConfig.ID}
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

        public static long UpdateTruncateTableConfigDone(SqlConnection cnArchConfig, TruncateLedgerTableConfig truncateTableConfig, out string statement)
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
                            ,[LastRunStatus] = '{truncateTableConfig.LastRunStatus}'
                            ,[TruncatedRows] = {truncateTableConfig.TruncatedRows}
                            ,[BITimeStamp] = GETDATE()
                        FROM [dbo].[cfgTruncateLedgerTable] xx
                        WHERE [ConfigID] = {truncateTableConfig.ID}
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

        public static long UpdateTruncateTableConfigIsEnabled(SqlConnection cnArchConfig, TruncateLedgerTableConfig truncateTableConfig, bool isEnabled)
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
                        FROM [dbo].[cfgTablesMerge] xx
                        WHERE [ConfigID] = {truncateTableConfig.ID}
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

        public static long UpdateTruncateTableConfigInitialRowCount(SqlConnection cnArchConfig, TruncateLedgerTableConfig truncateTableConfig)
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
                        [InitialRowCount] = CASE WHEN [InitialRowCount] = 0 THEN {truncateTableConfig.TruncatedRows} ELSE [InitialRowCount] END
                        ,[BITimeStamp] = GETDATE()
                    FROM [dbo].[cfgTablesMerge] xx
                    WHERE [ConfigID] = {truncateTableConfig.ID}
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

        //public static long UpdateTruncateTableConfigHighwaterMarks(SqlConnection cnArchConfig, TruncateTableConfig truncateTableConfig)
        //{
        //    var startTime = DateTime.Now.ToLocalTime();
        //    Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
        //    long rowCount;
        //    try
        //    {
        //        var cmd = new SqlCommand();
        //        cmd.CommandType = CommandType.Text;
        //        cmd.CommandText = $@"
        //            UPDATE xx
        //            SET 
        //                [HighwaterMarkDate] = '{truncateTableConfig.HighwaterMarkDate}'
        //                ,[HighwaterMarkID] = {truncateTableConfig.HighwaterMarkID}
        //                ,[FinalRowCount] = {truncateTableConfig.FinalRowCount}
        //                ,[SourceRowCount] = {truncateTableConfig.SourceRowCount}
        //                ,[SourceMinDate] = '{truncateTableConfig.SourceMinDate}'
        //                ,[SourceMaxDate] = '{truncateTableConfig.SourceMaxDate}'
        //                ,[DestinationRowCount] = {truncateTableConfig.DestinationRowCount}
        //                ,[DestinationMinDate] = '{truncateTableConfig.DestinationMinDate}'
        //                ,[DestinationMaxDate] = '{truncateTableConfig.DestinationMaxDate}'
        //                ,[MergeStartDate] = '{truncateTableConfig.LastRunStartDate}'        
        //                ,[MergeEndDate] = '{truncateTableConfig.LastRunEndDate}'        
        //                ,[BITimeStamp] = GETDATE()
        //            FROM [dbo].[cfgTablesMerge] xx
        //            WHERE [ConfigID] = {truncateTableConfig.ID}
        //        ";
        //        cmd.Connection = cnArchConfig;
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

    }
}