using ArchiveServices.Common.Types.Archie;
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

namespace ArchiveServices.Common.Managers.Archie
{
    public class ArchiveTableConfigManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static ArchiveTableConfigs GetArchiveTableConfigs(SqlConnection cnArchConfig, string groupName, string plantCode, string serverName, string databaseName, string schemaName, string tableName, bool useHighwaterMark, bool statsOnly)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            var testMode = Convert.ToBoolean(ConfigurationManager.AppSettings["TestMode"]);
            var testModeNoDelete = Convert.ToBoolean(ConfigurationManager.AppSettings["TestModeNoDelete"]);
            var configs = new ArchiveTableConfigs();
            var filters = new List<string>();
            var parameters = new List<SqlParameter>();
            try
            {
                if (string.IsNullOrEmpty(tableName))
                {
                    filters.Add("[IsArchive] = 1 AND \r\n");
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
                        //filters.Add("[IsEnabled] = 1 AND \r\n");
                        filters.Add("[IsTestMode] = 0 AND \r\n");
                    }
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
                filters[filters.Count - 1] = filters.Last().Replace(" AND \r\n", "");
                var cmd = GetArchiveTableConfigs(filters);
                foreach (var parameter in parameters) cmd.Parameters.Add(parameter);
                cmd.Connection = cnArchConfig;
                Logger.Info(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        var config = new ArchiveTableConfig();
                        config.ConfigID = reader.Get<int>("ConfigID");
                        config.IsArchive = reader.Get<bool>("IsArchive");
                        config.IsTail = reader.Get<bool>("IsTail");
                        config.IsVerify = reader.Get<bool>("IsVerify");
                        config.IsActive = reader.Get<bool>("IsActive");
                        config.IsEnabled = reader.Get<bool>("IsEnabled");
                        config.IsTestMode = reader.Get<bool>("IsTestMode");
                        config.IsHammerMode = reader.Get<bool>("IsHammerMode");
                        config.IsRunning = reader.Get<bool>("IsRunning");
                        config.SourceServerName = reader.Get<string>("SourceServerName");
                        config.SourceDatabaseName = reader.Get<string>("SourceDatabaseName");
                        config.SourceSchemaName = reader.Get<string>("SourceSchemaName");
                        config.SourceTableName = reader.Get<string>("SourceTableName");
                        config.DestinationServerName = reader.Get<string>("DestinationServerName");
                        config.DestinationDatabaseName = reader.Get<string>("DestinationDatabaseName");
                        config.DestinationSchemaName = reader.Get<string>("DestinationSchemaName");
                        config.DestinationTableName = reader.Get<string>("DestinationTableName");
                        config.IDColumn = reader.Get<string>("IDColumn");
                        config.SourceKey = reader.Get<string>("SourceKey").Replace(" ", "");
                        config.DestinationKey = reader.Get<string>("DestinationKey").Replace(" ", "");
                        config.DateColumn = reader.Get<string>("DateColumn");
                        config.UseHighwaterMark = useHighwaterMark;
                        config.SelectStartDate = reader.Get<DateTime>("SelectStartDate");
                        config.HighwaterMarkDate = reader.Get<DateTime>("HighwaterMarkDate");
                        config.OrigHighwaterMarkDate = reader.Get<DateTime>("HighwaterMarkDate");
                        config.OrigHighwaterMarkDateString = config.HighwaterMarkDate.ToString("M/dd/yyyy hh:mm:ss tt", CultureInfo.CreateSpecificCulture("en-US"));
                        config.HighwaterMarkID = reader.Get<long>("HighwaterMarkID");
                        config.OtherCondition = reader.Get<string>("OtherCondition");
                        config.ExcludeColumn = reader.Get<string>("ExcludeColumn");
                        config.RunFrequencyDays = reader.Get<int>("RunFrequencyDays");
                        config.NextRunDate = reader.Get<DateTime>("NextRunDate");
                        config.MaxRowsToMove = reader.Get<int>("MaxRowsToMove");
                        config.CreatedBy = reader.Get<string>("CreatedBy");
                        config.LastEditBy = reader.Get<string>("LastEditBy");
                        foreach (var keyColumn in config.SourceKey.Split(',').ToList())
                        {
                            config.SourceKeyColumns.Add(new KeyColumn(keyColumn));
                        }
                        foreach (var keyColumn in config.DestinationKey.Split(',').ToList())
                        {
                            config.DestinationKeyColumns.Add(new KeyColumn(keyColumn));
                        }
                        if (!string.IsNullOrEmpty(config.ExcludeColumn))
                        {
                            foreach (var keyColumn in config.ExcludeColumn.Split(',').ToList())
                            {
                                config.ExcludeColumns.Add(new KeyColumn(keyColumn));
                            }
                        }
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

        public static ValidationErrors ValidateConfig(int stepID, ArchiveTableConfig archiveTableConfig, SqlConnection cnArchConfig, SqlConnection cnSource, SqlConnection cnDestination)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {startTime}");
            var validationErrors = new ValidationErrors();
            try
            {
                //int maxMergeRows = Convert.ToInt32(ConfigurationManager.AppSettings["ArchieMaxMergeRows"]);

                if (string.IsNullOrEmpty(archiveTableConfig.SourceTableName))
                {
                    var validationError = new ValidationError($"{archiveTableConfig.ServerQualifiedDestinationName} is not found in [Archiver].[dbo].[cfgArchiveTable].", "Validation", "Fatal", true);
                    validationErrors.Add(validationError);
                    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                    archiveTableConfig.ValidationStatus = validationError.Message;
                    Logger.Error(validationError.Message);
                }
                else
                {
                    if (archiveTableConfig.IsRunning)
                    {
                        var validationError = new ValidationError($"Archive of {archiveTableConfig.ServerQualifiedDestinationName} is already Running.", "Validation", "Fatal", true);
                        validationErrors.Add(validationError);
                        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                        archiveTableConfig.ValidationStatus = validationError.Message;
                    }
                    if (archiveTableConfig.IsArchive == false)
                    {
                        var validationError = new ValidationError($"{archiveTableConfig.ServerQualifiedDestinationName} is not configured for Archiving.", "Validation", "Fatal", true);
                        validationErrors.Add(validationError);
                        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                        archiveTableConfig.ValidationStatus = validationError.Message;
                        Logger.Error(validationError.Message);
                    }
                    if (archiveTableConfig.IsActive == false)
                    {
                        var validationError = new ValidationError($"{archiveTableConfig.ServerQualifiedDestinationName} is not Active.", "Validation", "Fatal", true);
                        validationErrors.Add(validationError);
                        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                        archiveTableConfig.ValidationStatus = validationError.Message;
                        Logger.Error(validationError.Message);
                    }
                    if (archiveTableConfig.IsEnabled == false && archiveTableConfig.StatsOnly == false)
                    {
                        var validationError = new ValidationError($"{archiveTableConfig.ServerQualifiedDestinationName} is not Enabled.", "Validation", "Fatal", true);
                        validationErrors.Add(validationError);
                        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                        archiveTableConfig.ValidationStatus = validationError.Message;
                        Logger.Error(validationError.Message);
                    }
                    if (archiveTableConfig.TestModeConfig == true && archiveTableConfig.IsTestMode == false && archiveTableConfig.StatsOnly == false)
                    {
                        var validationError = new ValidationError($"{archiveTableConfig.ServerQualifiedDestinationName} is not configured for Test Mode but web.config is in Test Mode.", "Validation", "Fatal", true);
                        validationErrors.Add(validationError);
                        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                        archiveTableConfig.ValidationStatus = validationError.Message;
                        Logger.Error(validationError.Message);
                    }
                }
                //if (!archiveTableConfig.IsTestMode)
                //{
                //    if (!archiveTableConfig.IsActive)
                //    {
                //        var validationError = new ValidationError($"{archiveTableConfig.ServerQualifiedDestinationName} is Inactive.", "Validation", "Fatal", true);
                //        validationErrors.Add(validationError);
                //        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                //        archiveTableConfig.ValidationStatus = validationError.Message;
                //        Logger.Error(validationError.Message);
                //    }
                //    if (!archiveTableConfig.IsEnabled)
                //    {
                //        var validationError = new ValidationError($"{archiveTableConfig.ServerQualifiedDestinationName} is Disabled.", "Validation", "Fatal", true);
                //        validationErrors.Add(validationError);
                //        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                //        archiveTableConfig.ValidationStatus = validationError.Message;
                //        Logger.Error(validationError.Message);
                //    }
                //}
                //if (archiveTableConfig.DestinationDatabaseName == "PlantArchive" && (archiveTableConfig.SourceServerName != archiveTableConfig.DestinationServerName))
                //{
                //    var validationError = new ValidationError($"{archiveTableConfig.ServerQualifiedDestinationName} Source database must reside on the same server as the Destination.", "Validation", "Fatal", true);
                //    validationErrors.Add(validationError);
                //    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                //    archiveTableConfig.ValidationStatus = validationError.Message;
                //    Logger.Error(validationError.Message);
                //}
                if (string.IsNullOrEmpty(archiveTableConfig.SourceKey))
                {
                    var validationError = new ValidationError($"{archiveTableConfig.ServerQualifiedDestinationName} is not configured for SourceKey.", "Validation", "Fatal", true);
                    validationErrors.Add(validationError);
                    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                    archiveTableConfig.ValidationStatus = validationError.Message;
                    Logger.Error(validationError.Message);
                }
                if (string.IsNullOrEmpty(archiveTableConfig.DestinationKey))
                {
                    var validationError = new ValidationError($"{archiveTableConfig.ServerQualifiedDestinationName} is not configured for DestinationKey.", "Validation", "Fatal", true);
                    validationErrors.Add(validationError);
                    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                    archiveTableConfig.ValidationStatus = validationError.Message;
                    Logger.Error(validationError.Message);
                }
                if (string.IsNullOrEmpty(archiveTableConfig.DateColumn))
                {
                    var validationError = new ValidationError($"{archiveTableConfig.ServerQualifiedDestinationName} is not configured for DateColumn.", "Validation", "Fatal", true);
                    validationErrors.Add(validationError);
                    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                    archiveTableConfig.ValidationStatus = validationError.Message;
                    Logger.Error(validationError.Message);
                }
                if (archiveTableConfig.IDColumn.Contains(","))
                {
                    var validationError = new ValidationError($"IDColumn must be a single value for {archiveTableConfig.ServerQualifiedDestinationName}.", "Validation", "Fatal", true);
                    validationErrors.Add(validationError);
                    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                    archiveTableConfig.ValidationStatus = validationError.Message;
                    Logger.Error(validationError.Message);
                }
            }
            catch (Exception e)
            {
                Logger.Error($@"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new ApplicationException($@"{cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            finally
            {
                if (!validationErrors.Any()) archiveTableConfig.ValidationStatus = "Valid";
                Logger.Info($"END: {Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds)} SECONDS");
            }
            return validationErrors;
        }


        private static SqlCommand GetArchiveTableConfigs(List<string> filters)
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
                        ,[ExcludeColumn]
                        ,[RunFrequencyDays]
                        ,[NextRunDate]
                          ,[MaxRowsToMove]
                          ,[SourceInitialRowCount]
                          ,[DestinationInitialRowCount]
                        ,[CreatedBy]
                        ,[LastEditBy]
                    FROM [dbo].[cfgArchiveTable]
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

        public static long UpdateArchiveTableConfigIsRunning(SqlConnection cnArchConfig, ArchiveTableConfig archiveTableConfig, bool isRunning)
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
                            [IsRunning] = {Convert.ToInt32(isRunning)}
                            ,[ProcessID] = {archiveTableConfig.ProcessID}
                            ,[LastRunStatus] = '{archiveTableConfig.LastRunStatus}'
                            ,[LastRunStartDate] = '{archiveTableConfig.LastRunStartDate}'
                            ,[LastRunEndDate] = '{archiveTableConfig.LastRunEndDate}'
                            ,[IsSourceKeyUnique] = '{archiveTableConfig.IsSourceKeyUnique}'
                            ,[IsDestinationKeyUnique] = '{archiveTableConfig.IsDestinationKeyUnique}'
                            ,[BITimeStamp] = GETDATE()
                        FROM [dbo].[cfgArchiveTable] xx
                        WHERE [ConfigID] = {archiveTableConfig.ConfigID}
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

        public static long UpdateArchiveTableConfigIsKeyColumnUnique(SqlConnection cnArchConfig, ArchiveTableConfig archiveTableConfig)
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
                            [IsSourceKeyUnique] = '{archiveTableConfig.IsSourceKeyUnique}'
                            ,[IsDestinationKeyUnique] = '{archiveTableConfig.IsDestinationKeyUnique}'
                            ,[BITimeStamp] = GETDATE()
                        FROM [dbo].[cfgArchiveTable] xx
                        WHERE [ConfigID] = {archiveTableConfig.ConfigID}
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

        public static long UpdateArchiveTableConfig(SqlConnection cnArchConfig, ArchiveTableConfig archiveTableConfig)
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
                            [IsRunning] = {Convert.ToInt32(archiveTableConfig.IsRunning)}
                            ,[LastRunStatus] = '{archiveTableConfig.LastRunStatus}'
                            ,[SourceInitialRowCount] = {archiveTableConfig.SourceInitialRowCount}
                            ,[SourceFinalRowCount] = {archiveTableConfig.SourceFinalRowCount}
                            ,[DestinationInitialRowCount] = {archiveTableConfig.DestinationInitialRowCount}
                            ,[DestinationFinalRowCount] = {archiveTableConfig.DestinationFinalRowCount}
                            ,[RowsRemaining] = {archiveTableConfig.RowsRemaining}
                            ,[BITimeStamp] = GETDATE()
                        FROM [dbo].[cfgArchiveTable] xx
                        WHERE [ConfigID] = {archiveTableConfig.ConfigID}
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

        public static long UpdateArchiveTableConfigDone(SqlConnection cnArchConfig, ArchiveTableConfig archiveTableConfig, out string statement)
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
                            ,[LastRunStatus] = '{archiveTableConfig.LastRunStatus}'
                            ,[SourceRowCount] = {archiveTableConfig.SourceRowCount}
                            ,[DestinationFinalRowCount] = {archiveTableConfig.DestinationFinalRowCount}
                            ,[BITimeStamp] = GETDATE()
                        FROM [dbo].[cfgArchiveTable] xx
                        WHERE [ConfigID] = {archiveTableConfig.ConfigID}
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

        public static long UpdateArchiveTableConfigIsEnabled(SqlConnection cnArchConfig, ArchiveTableConfig archiveTableConfig, bool isEnabled)
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
                            ,[AutoConfigDisable] = {(archiveTableConfig.AutoConfigDisable == true ? 1 : 0)}
                            ,[BITimeStamp] = GETDATE()
                        FROM [dbo].[cfgArchiveTable] xx
                        WHERE [ConfigID] = {archiveTableConfig.ConfigID}
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

        public static long UpdateArchiveTableConfigInitialRowCount(SqlConnection cnArchConfig, ArchiveTableConfig archiveTableConfig)
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
                        [DestinationInitialRowCount] = CASE WHEN [DestinationInitialRowCount] = 0 THEN {archiveTableConfig.DestinationInitialRowCount} ELSE [DestinationInitialRowCount] END
                        ,[BITimeStamp] = GETDATE()
                    FROM [dbo].[cfgArchiveTable] xx
                    WHERE [ConfigID] = {archiveTableConfig.ConfigID}
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

        public static long UpdateArchiveTableConfigHighwaterMarks(SqlConnection cnArchConfig, ArchiveTableConfig archiveTableConfig)
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
                        [HighwaterMarkDate] = '{archiveTableConfig.HighwaterMarkDate}'
                        ,[HighwaterMarkID] = {archiveTableConfig.HighwaterMarkID}
                        ,[SourceInitialRowCount] = {archiveTableConfig.SourceInitialRowCount}
                        ,[SourceFinalRowCount] = {archiveTableConfig.SourceFinalRowCount}
                        ,[SourceMinDate] = '{archiveTableConfig.SourceMinDate}'
                        ,[SourceMaxDate] = '{archiveTableConfig.SourceMaxDate}'
                        ,[DestinationInitialRowCount] = {archiveTableConfig.DestinationInitialRowCount}
                        ,[DestinationFinalRowCount] = {archiveTableConfig.DestinationFinalRowCount}
                        ,[DestinationMinDate] = '{archiveTableConfig.DestinationMinDate}'
                        ,[DestinationMaxDate] = '{archiveTableConfig.DestinationMaxDate}'
                        ,[RowsRemaining] = '{archiveTableConfig.RowsRemaining}'
                        ,[RowsMoved] = {archiveTableConfig.RowsAffected}
                        ,[BITimeStamp] = GETDATE()
                    FROM [dbo].[cfgArchiveTable] xx
                    WHERE [ConfigID] = {archiveTableConfig.ConfigID}
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