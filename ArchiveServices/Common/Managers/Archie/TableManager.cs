using System;
using System.Configuration;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Text;
using ArchiveServices.Common.Types.Archie;
using ArchiveServices.Common.Types.Response;
using ArchiveServices.Common.Utilities;
using NLog;

namespace ArchiveServices.Common.Managers.Archie
{
    public class TableManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        //public static ValidationErrors ValidateConfig(int stepID, ArchiveTableConfig archiveTableConfig, SqlConnection cnArchConfig, SqlConnection cnSource, SqlConnection cnDestination)
        //{
        //    var startTime = DateTime.Now.ToLocalTime();
        //    Logger.Info($"BEGIN: {startTime}");
        //    var validationErrors = new ValidationErrors();
        //    try
        //    {
        //        //int maxMergeRows = Convert.ToInt32(ConfigurationManager.AppSettings["ArchieMaxMergeRows"]);
        //        if (archiveTableConfig.SourceTableName == null)
        //        {
        //            var validationError = new ValidationError($"{archiveTableConfig.ServerQualifiedDestinationName} is not configured for Archiving.", "Validation", "Fatal", true);
        //            validationErrors.Add(validationError);
        //            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
        //            archiveTableConfig.ValidationStatus = validationError.Message;
        //            Logger.Error(validationError.Message);
        //        }
        //        else if (archiveTableConfig.IsRunning)
        //        {
        //            var validationError = new ValidationError($"Archive of {archiveTableConfig.ServerQualifiedDestinationName} is already Running.", "Validation", "Fatal", true);
        //            validationErrors.Add(validationError);
        //            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
        //            archiveTableConfig.ValidationStatus = validationError.Message;
        //        }
        //        if (!archiveTableConfig.IsTestMode)
        //        {
        //            if (!archiveTableConfig.IsActive)
        //            {
        //                var validationError = new ValidationError($"{archiveTableConfig.ServerQualifiedDestinationName} is Inactive.", "Validation", "Fatal", true);
        //                validationErrors.Add(validationError);
        //                var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
        //                archiveTableConfig.ValidationStatus = validationError.Message;
        //                Logger.Error(validationError.Message);
        //            }
        //            if (!archiveTableConfig.IsEnabled)
        //            {
        //                var validationError = new ValidationError($"{archiveTableConfig.ServerQualifiedDestinationName} is Disabled.", "Validation", "Fatal", true);
        //                validationErrors.Add(validationError);
        //                var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
        //                archiveTableConfig.ValidationStatus = validationError.Message;
        //                Logger.Error(validationError.Message);
        //            }
        //        }
        //        if (archiveTableConfig.DestinationDatabaseName == "PlantArchive" && (archiveTableConfig.SourceServerName != archiveTableConfig.DestinationServerName))
        //        {
        //            var validationError = new ValidationError($"{archiveTableConfig.ServerQualifiedDestinationName} Source database must reside on the same server as the Destination.", "Validation", "Fatal", true);
        //            validationErrors.Add(validationError);
        //            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
        //            archiveTableConfig.ValidationStatus = validationError.Message;
        //            Logger.Error(validationError.Message);
        //        }
        //        if (string.IsNullOrEmpty(archiveTableConfig.SourceKey))
        //        {
        //            var validationError = new ValidationError($"{archiveTableConfig.ServerQualifiedDestinationName} is not configured for SourceKey.", "Validation", "Fatal", true);
        //            validationErrors.Add(validationError);
        //            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
        //            archiveTableConfig.ValidationStatus = validationError.Message;
        //            Logger.Error(validationError.Message);
        //        }
        //        if (string.IsNullOrEmpty(archiveTableConfig.DestinationKey))
        //        {
        //            var validationError = new ValidationError($"{archiveTableConfig.ServerQualifiedDestinationName} is not configured for DestinationKey.", "Validation", "Fatal", true);
        //            validationErrors.Add(validationError);
        //            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
        //            archiveTableConfig.ValidationStatus = validationError.Message;
        //            Logger.Error(validationError.Message);
        //        }
        //        if (string.IsNullOrEmpty(archiveTableConfig.DateColumn))
        //        {
        //            var validationError = new ValidationError($"{archiveTableConfig.ServerQualifiedDestinationName} is not configured for DateColumn.", "Validation", "Fatal", true);
        //            validationErrors.Add(validationError);
        //            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
        //            archiveTableConfig.ValidationStatus = validationError.Message;
        //            Logger.Error(validationError.Message);
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        Logger.Error($@"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
        //        throw new ApplicationException($@"{cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
        //    }
        //    finally
        //    {
        //        if (!validationErrors.Any()) archiveTableConfig.ValidationStatus = "Valid";
        //        Logger.Info($"END: {Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds)} SECONDS");
        //    }
        //    return validationErrors;
        //}

        public static ValidationErrors ValidateConfig(int stepID, ArchiveAndTailTableConfig ArchiveAndTailTableConfig, SqlConnection cnArchConfig, SqlConnection cnSource, SqlConnection cnDestination)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {startTime}");
            var validationErrors = new ValidationErrors();
            try
            {
                //int maxMergeRows = Convert.ToInt32(ConfigurationManager.AppSettings["ArchieMaxMergeRows"]);
                if (ArchiveAndTailTableConfig.SourceTableName == null)
                {
                    var validationError = new ValidationError($"{ArchiveAndTailTableConfig.ServerQualifiedSourceName} is not configured for Merging.", "Validation", "Fatal", true);
                    validationErrors.Add(validationError);
                    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                    ArchiveAndTailTableConfig.ValidationStatus = validationError.Message;
                    Logger.Error(validationError.Message);
                }
                else if (ArchiveAndTailTableConfig.IsRunning)
                {
                    var validationError = new ValidationError($"Merge of {ArchiveAndTailTableConfig.ServerQualifiedSourceName} is already Running.", "Validation", "Fatal", true);
                    validationErrors.Add(validationError);
                    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                    ArchiveAndTailTableConfig.ValidationStatus = validationError.Message;
                }
                if (!ArchiveAndTailTableConfig.IsTestMode)
                {
                    if (!ArchiveAndTailTableConfig.IsActive)
                    {
                        var validationError = new ValidationError($"{ArchiveAndTailTableConfig.ServerQualifiedSourceName} is Inactive.", "Validation", "Fatal", true);
                        validationErrors.Add(validationError);
                        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                        ArchiveAndTailTableConfig.ValidationStatus = validationError.Message;
                        Logger.Error(validationError.Message);
                    }
                    if (!ArchiveAndTailTableConfig.IsEnabled)
                    {
                        var validationError = new ValidationError($"{ArchiveAndTailTableConfig.ServerQualifiedSourceName} is Disabled.", "Validation", "Fatal", true);
                        validationErrors.Add(validationError);
                        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                        ArchiveAndTailTableConfig.ValidationStatus = validationError.Message;
                        Logger.Error(validationError.Message);
                    }
                }
                if (string.IsNullOrEmpty(ArchiveAndTailTableConfig.SourceKey))
                {
                    var validationError = new ValidationError($"{ArchiveAndTailTableConfig.ServerQualifiedSourceName} is not configured for SourceKey.", "Validation", "Fatal", true);
                    validationErrors.Add(validationError);
                    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                    ArchiveAndTailTableConfig.ValidationStatus = validationError.Message;
                    Logger.Error(validationError.Message);
                }
                if (string.IsNullOrEmpty(ArchiveAndTailTableConfig.DestinationKey))
                {
                    var validationError = new ValidationError($"{ArchiveAndTailTableConfig.ServerQualifiedSourceName} is not configured for DestinationKey.", "Validation", "Fatal", true);
                    validationErrors.Add(validationError);
                    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                    ArchiveAndTailTableConfig.ValidationStatus = validationError.Message;
                    Logger.Error(validationError.Message);
                }
                if (string.IsNullOrEmpty(ArchiveAndTailTableConfig.DateColumn))
                {
                    var validationError = new ValidationError($"{ArchiveAndTailTableConfig.ServerQualifiedSourceName} is not configured for DateColumn.", "Validation", "Fatal", true);
                    validationErrors.Add(validationError);
                    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                    ArchiveAndTailTableConfig.ValidationStatus = validationError.Message;
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
                if (!validationErrors.Any()) ArchiveAndTailTableConfig.ValidationStatus = "Valid";
                Logger.Info($"END: {Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds)} SECONDS");
            }
            return validationErrors;
        }

        public static ValidationErrors ValidateConfig(int stepID, OldArchiveTableConfig archiveTableConfig, SqlConnection cnArchConfig, SqlConnection cnArchRepoMaster, SqlConnection cnDestination)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {startTime}");
            var validationErrors = new ValidationErrors();
            try
            {
                var minRetentionDays = Convert.ToInt32(ConfigurationManager.AppSettings["ArchieMinRetentionDays"]);
                long maxDeleteRows = Convert.ToInt64(ConfigurationManager.AppSettings["ArchieMaxDeleteRows"]);
                if (archiveTableConfig.TableName == null)
                {
                    var validationError = new ValidationError($"{archiveTableConfig.ServerQualifiedName} is not configured for Archiving.", "Validation", "Fatal", true);
                    validationErrors.Add(validationError);
                    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                    archiveTableConfig.ValidationStatus = validationError.Message;
                    Logger.Error(validationError.Message);
                }
                if (archiveTableConfig.IsRunning)
                {
                    var validationError = new ValidationError($"Archive of {archiveTableConfig.ServerQualifiedName} is already Running.", "Validation", "Fatal", true);
                    validationErrors.Add(validationError);
                    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                    archiveTableConfig.ValidationStatus = validationError.Message;
                }
                if (!archiveTableConfig.IsTestMode)
                {
                    if (!archiveTableConfig.IsActive)
                    {
                        var validationError = new ValidationError($"{archiveTableConfig.ServerQualifiedName} is Inactive.", "Validation", "Fatal", true);
                        validationErrors.Add(validationError);
                        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                        archiveTableConfig.ValidationStatus = validationError.Message;
                        Logger.Error(validationError.Message);
                    }
                    if (!archiveTableConfig.IsEnabled)
                    {
                        var validationError = new ValidationError($"{archiveTableConfig.ServerQualifiedName} is Disabled.", "Validation", "Fatal", true);
                        validationErrors.Add(validationError);
                        var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                        archiveTableConfig.ValidationStatus = validationError.Message;
                        Logger.Error(validationError.Message);
                    }
                }
                if (string.IsNullOrEmpty(archiveTableConfig.KeyColumn))
                {
                    var validationError = new ValidationError($"{archiveTableConfig.ServerQualifiedName} is not configured for KeyColumn.", "Validation", "Fatal", true);
                    validationErrors.Add(validationError);
                    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                    archiveTableConfig.ValidationStatus = validationError.Message;
                    Logger.Error(validationError.Message);
                }
                if (string.IsNullOrEmpty(archiveTableConfig.DateColumn))
                {
                    var validationError = new ValidationError($"{archiveTableConfig.ServerQualifiedName} is not configured for DateColumn.", "Validation", "Fatal", true);
                    validationErrors.Add(validationError);
                    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                    archiveTableConfig.ValidationStatus = validationError.Message;
                    Logger.Error(validationError.Message);
                }
                if (archiveTableConfig.RetentionDays < minRetentionDays)
                {
                    var validationError = new ValidationError($"{archiveTableConfig.ServerQualifiedName} configured RetentionDays is less than the minimum ({minRetentionDays}).", "Validation", "Fatal", true);
                    validationErrors.Add(validationError);
                    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                    archiveTableConfig.ValidationStatus = validationError.Message;
                    Logger.Error(validationError.Message);
                }
                if (archiveTableConfig.MaxDeleteRowsPerBatch > maxDeleteRows)
                {
                    var validationError = new ValidationError($"{archiveTableConfig.MaxDeleteRowsPerBatch} configured MaxDeleteRowsPerBatch is greater than the maximum ({maxDeleteRows}).", "Validation", "Fatal", true);
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

        //public static ValidationErrors ValidateConfig(int stepID, TailTableNoVerifyConfig tailTableConfig, SqlConnection cnArchConfig, SqlConnection cnDestination)
        //{
        //    var startTime = DateTime.Now.ToLocalTime();
        //    Logger.Info($"BEGIN: {startTime}");
        //    var validationErrors = new ValidationErrors();
        //    try
        //    {
        //        var minRetentionDays = Convert.ToInt32(ConfigurationManager.AppSettings["ArchieMinRetentionDaysTail"]);
        //        long maxDeleteRows = Convert.ToInt64(ConfigurationManager.AppSettings["ArchieMaxDeleteRows"]);
        //        if (tailTableConfig.TargetTableName == null)
        //        {
        //            var validationError = new ValidationError($"{tailTableConfig.ServerQualifiedTargetName} is not configured for Tailing.", "Validation", "Fatal", true);
        //            validationErrors.Add(validationError);
        //            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
        //            tailTableConfig.ValidationStatus = validationError.Message;
        //            Logger.Error(validationError.Message);
        //        }
        //        else if (tailTableConfig.IsRunning)
        //        {
        //            var validationError = new ValidationError($"Tail {tailTableConfig.ServerQualifiedTargetName} is already Running.", "Validation", "Fatal", true);
        //            validationErrors.Add(validationError);
        //            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
        //            tailTableConfig.ValidationStatus = validationError.Message;
        //        }
        //        if (!tailTableConfig.IsTestMode)
        //        {
        //            if (!tailTableConfig.IsActive)
        //            {
        //                var validationError = new ValidationError($"{tailTableConfig.ServerQualifiedTargetName} is Inactive.", "Validation", "Fatal", true);
        //                validationErrors.Add(validationError);
        //                var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
        //                tailTableConfig.ValidationStatus = validationError.Message;
        //                Logger.Error(validationError.Message);
        //            }
        //            if (!tailTableConfig.IsEnabled)
        //            {
        //                var validationError = new ValidationError($"{tailTableConfig.ServerQualifiedTargetName} is Disabled.", "Validation", "Fatal", true);
        //                validationErrors.Add(validationError);
        //                var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
        //                tailTableConfig.ValidationStatus = validationError.Message;
        //                Logger.Error(validationError.Message);
        //            }
        //        }
        //        if (string.IsNullOrEmpty(tailTableConfig.SourceKey))
        //        {
        //            var validationError = new ValidationError($"{tailTableConfig.ServerQualifiedTargetName} is not configured for SourceKey.", "Validation", "Fatal", true);
        //            validationErrors.Add(validationError);
        //            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
        //            tailTableConfig.ValidationStatus = validationError.Message;
        //            Logger.Error(validationError.Message);
        //        }
        //        if (string.IsNullOrEmpty(tailTableConfig.DateColumn))
        //        {
        //            var validationError = new ValidationError($"{tailTableConfig.ServerQualifiedTargetName} is not configured for DateColumn.", "Validation", "Fatal", true);
        //            validationErrors.Add(validationError);
        //            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
        //            tailTableConfig.ValidationStatus = validationError.Message;
        //            Logger.Error(validationError.Message);
        //        }
        //        if (tailTableConfig.RetentionDays < minRetentionDays)
        //        {
        //            var validationError = new ValidationError($"{tailTableConfig.ServerQualifiedTargetName} configured RetentionDays is less than the minimum ({minRetentionDays}).", "Validation", "Fatal", true);
        //            validationErrors.Add(validationError);
        //            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
        //            tailTableConfig.ValidationStatus = validationError.Message;
        //            Logger.Error(validationError.Message);
        //        }
        //        if (tailTableConfig.MaxDeleteRowsPerBatch > maxDeleteRows)
        //        {
        //            var validationError = new ValidationError($"{tailTableConfig.MaxDeleteRowsPerBatch} configured MaxDeleteRowsPerBatch is greater than the maximum ({maxDeleteRows}).", "Validation", "Fatal", true);
        //            validationErrors.Add(validationError);
        //            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
        //            tailTableConfig.ValidationStatus = validationError.Message;
        //            Logger.Error(validationError.Message);
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        Logger.Error($@"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
        //        throw new ApplicationException($@"{cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
        //    }
        //    finally
        //    {
        //        if (!validationErrors.Any()) tailTableConfig.ValidationStatus = "Valid";
        //        Logger.Info($"END: {Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds)} SECONDS");
        //    }
        //    return validationErrors;
        //}

        //public static ValidationErrors ValidateConfig(int stepID, TruncateLedgerTableConfig truncateTableConfig, SqlConnection cnArchConfig, SqlConnection cnDestination)
        //{
        //    var startTime = DateTime.Now.ToLocalTime();
        //    Logger.Info($"BEGIN: {startTime}");
        //    var validationErrors = new ValidationErrors();
        //    try
        //    {
        //        var minRetentionDays = Convert.ToInt32(ConfigurationManager.AppSettings["ArchieMinRetentionDaysTail"]);
        //        long maxDeleteRows = Convert.ToInt64(ConfigurationManager.AppSettings["ArchieMaxDeleteRows"]);
        //        if (truncateTableConfig.SourceTableName == null)
        //        {
        //            var validationError = new ValidationError($"{truncateTableConfig.ServerQualifiedSourceName} is not configured for Truncating.", "Validation", "Fatal", true);
        //            validationErrors.Add(validationError);
        //            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
        //            truncateTableConfig.ValidationStatus = validationError.Message;
        //            Logger.Error(validationError.Message);
        //        }
        //        else if (truncateTableConfig.IsRunning)
        //        {
        //            var validationError = new ValidationError($"Tail {truncateTableConfig.ServerQualifiedSourceName} is already Running.", "Validation", "Fatal", true);
        //            validationErrors.Add(validationError);
        //            var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
        //            truncateTableConfig.ValidationStatus = validationError.Message;
        //        }
        //        if (!truncateTableConfig.IsTestMode)
        //        {
        //            if (!truncateTableConfig.IsActive)
        //            {
        //                var validationError = new ValidationError($"{truncateTableConfig.ServerQualifiedSourceName} is Inactive.", "Validation", "Fatal", true);
        //                validationErrors.Add(validationError);
        //                var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
        //                truncateTableConfig.ValidationStatus = validationError.Message;
        //                Logger.Error(validationError.Message);
        //            }
        //            if (!truncateTableConfig.IsEnabled)
        //            {
        //                var validationError = new ValidationError($"{truncateTableConfig.ServerQualifiedSourceName} is Disabled.", "Validation", "Fatal", true);
        //                validationErrors.Add(validationError);
        //                var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
        //                truncateTableConfig.ValidationStatus = validationError.Message;
        //                Logger.Error(validationError.Message);
        //            }
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        Logger.Error($@"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
        //        throw new ApplicationException($@"{cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
        //    }
        //    finally
        //    {
        //        if (!validationErrors.Any()) truncateTableConfig.ValidationStatus = "Valid";
        //        Logger.Info($"END: {Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds)} SECONDS");
        //    }
        //    return validationErrors;
        //}

        public static bool CheckSourceDatabaseExists(SqlConnection cnSource, ArchiveTableConfig mergeTableConfig)
        {
            bool retVal = false;
            try
            {
                string str = string.Format($@"SELECT 1 FROM sys.databases WHERE Name = '{mergeTableConfig.SourceDatabaseName}'");
                SqlCommand cmd = new SqlCommand(str, cnSource);
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnSource.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        public static bool CheckSourceDatabaseExists(SqlConnection cnSource, ArchiveAndTailTableConfig ArchiveAndTailTableConfig)
        {
            bool retVal = false;
            try
            {
                string str = string.Format($@"SELECT 1 FROM sys.databases WHERE Name = '{ArchiveAndTailTableConfig.SourceDatabaseName}'");
                SqlCommand cmd = new SqlCommand(str, cnSource);
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnSource.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        //public static bool CheckSourceDatabaseExists(SqlConnection cnSource, TailTableNoVerifyConfig tailTableConfig)
        //{
        //    bool retVal = false;
        //    try
        //    {
        //        string str = string.Format($@"SELECT 1 FROM sys.databases WHERE Name = '{tailTableConfig.TargetDatabaseName}'");
        //        SqlCommand cmd = new SqlCommand(str, cnSource);
        //        Logger.Info(Logging.DbCmdString(cmd));
        //        retVal = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        //        Logger.Info($@"retVal = {retVal}");
        //    }
        //    catch (Exception e)
        //    {
        //        Logger.Error($"ERROR: {cnSource.ConnectionString}  {e.Message}: {e.StackTrace}");
        //    }
        //    return retVal;
        //}

        public static bool CheckDestinationDatabaseExists(SqlConnection cnDestination, ArchiveTableConfig mergeTableConfig)
        {
            bool retVal = false;
            try
            {
                string str = string.Format($@"SELECT 1 FROM sys.databases WHERE Name = '{mergeTableConfig.DestinationDatabaseName}'");
                SqlCommand cmd = new SqlCommand(str, cnDestination);
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        public static bool CheckDestinationDatabaseExists(SqlConnection cnDestination, ArchiveAndTailTableConfig ArchiveAndTailTableConfig)
        {
            bool retVal = false;
            try
            {
                string str = string.Format($@"SELECT 1 FROM sys.databases WHERE Name = '{ArchiveAndTailTableConfig.DestinationDatabaseName}'");
                SqlCommand cmd = new SqlCommand(str, cnDestination);
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        public static bool CheckSourceDatabaseExists(SqlConnection cnSource, OldArchiveTableConfig archiveTableConfig)
        {
            bool retVal = false;
            try
            {
                string str = string.Format($@"SELECT 1 FROM sys.databases WHERE Name = '{archiveTableConfig.DatabaseName}'");
                SqlCommand cmd = new SqlCommand(str, cnSource);
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnSource.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        public static bool CheckSourceSchemaExists(SqlConnection cnSource, ArchiveTableConfig mergeTableConfig)
        {
            bool retVal = false;
            try
            {
                string str = string.Format($@"SELECT 1 FROM sys.schemas WHERE name = '{mergeTableConfig.SourceSchemaName}'");
                SqlCommand cmd = new SqlCommand(str, cnSource);
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnSource.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        public static bool CheckSourceSchemaExists(SqlConnection cnSource, ArchiveAndTailTableConfig ArchiveAndTailTableConfig)
        {
            bool retVal = false;
            try
            {
                string str = string.Format($@"SELECT 1 FROM sys.schemas WHERE name = '{ArchiveAndTailTableConfig.SourceSchemaName}'");
                SqlCommand cmd = new SqlCommand(str, cnSource);
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnSource.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        //public static bool CheckSourceSchemaExists(SqlConnection cnSource, TailTableNoVerifyConfig tailTableConfig)
        //{
        //    bool retVal = false;
        //    try
        //    {
        //        string str = string.Format($@"SELECT 1 FROM sys.schemas WHERE name = '{tailTableConfig.TargetSchemaName}'");
        //        SqlCommand cmd = new SqlCommand(str, cnSource);
        //        Logger.Info(Logging.DbCmdString(cmd));
        //        retVal = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        //        Logger.Info($@"retVal = {retVal}");
        //    }
        //    catch (Exception e)
        //    {
        //        Logger.Error($"ERROR: {cnSource.ConnectionString}  {e.Message}: {e.StackTrace}");
        //    }
        //    return retVal;
        //}

        public static bool CheckSourceSchemaExists(SqlConnection cnSource, OldArchiveTableConfig archiveTableConfig)
        {
            bool retVal = false;
            try
            {
                string str = string.Format($@"SELECT 1 FROM sys.schemas WHERE name = '{archiveTableConfig.SchemaName}'");
                SqlCommand cmd = new SqlCommand(str, cnSource);
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnSource.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        public static bool CheckDestinationSchemaExists(SqlConnection cnDestination, ArchiveTableConfig mergeTableConfig)
        {
            bool retVal = false;
            try
            {
                string str = string.Format($@"SELECT 1 FROM sys.schemas WHERE name = '{mergeTableConfig.DestinationSchemaName}'");
                SqlCommand cmd = new SqlCommand(str, cnDestination);
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        public static bool CheckDestinationSchemaExists(SqlConnection cnDestination, ArchiveAndTailTableConfig ArchiveAndTailTableConfig)
        {
            bool retVal = false;
            try
            {
                string str = string.Format($@"SELECT 1 FROM sys.schemas WHERE name = '{ArchiveAndTailTableConfig.DestinationSchemaName}'");
                SqlCommand cmd = new SqlCommand(str, cnDestination);
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        public static bool CreateDestinationSchema(SqlConnection cnDestination, ArchiveTableConfig mergeTableConfig, out string statement)
        {
            bool retVal = false;
            statement = $@"CREATE SCHEMA [{mergeTableConfig.DestinationSchemaName}]";
            try
            {
                SqlCommand cmd = new SqlCommand(statement, cnDestination);
                Logger.Info(Logging.DbCmdString(cmd));
                cmd.ExecuteNonQuery();
                retVal = true;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        public static bool CreateDestinationSchema(SqlConnection cnDestination, TailTableWithVerifyConfig tailTableConfig, out string statement)
        {
            bool retVal = false;
            statement = $@"CREATE SCHEMA [{tailTableConfig.DestinationSchemaName}]";
            try
            {
                SqlCommand cmd = new SqlCommand(statement, cnDestination);
                Logger.Info(Logging.DbCmdString(cmd));
                cmd.ExecuteNonQuery();
                retVal = true;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        public static bool CreateDestinationSchema(SqlConnection cnDestination, ArchiveAndTailTableConfig ArchiveAndTailTableConfig, out string statement)
        {
            bool retVal = false;
            statement = $@"CREATE SCHEMA [{ArchiveAndTailTableConfig.DestinationSchemaName}]";
            try
            {
                SqlCommand cmd = new SqlCommand(statement, cnDestination);
                Logger.Info(Logging.DbCmdString(cmd));
                cmd.ExecuteNonQuery();
                retVal = true;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        public static bool CheckDestinationTableExists(SqlConnection cnDestination, ArchiveTableConfig mergeTableConfig)
        {
            bool retVal = false;
            try
            {
                string str = string.Format($@"
                    SELECT 1 
                    FROM {mergeTableConfig.DestinationDatabaseName}.sys.tables t
                    INNER JOIN {mergeTableConfig.DestinationDatabaseName}.sys.schemas s ON s.schema_id = t.schema_id
                    WHERE s.name = '{mergeTableConfig.DestinationSchemaName}'
                    AND t.name = '{mergeTableConfig.DestinationTableName}'
                ");
                SqlCommand cmd = new SqlCommand(str, cnDestination);
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                Logger.Info(@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        public static bool CheckDestinationTableExists(SqlConnection cnDestination, ArchiveAndTailTableConfig ArchiveAndTailTableConfig)
        {
            bool retVal = false;
            try
            {
                string str = string.Format($@"
                    SELECT 1 
                    FROM {ArchiveAndTailTableConfig.DestinationDatabaseName}.sys.tables t
                    INNER JOIN {ArchiveAndTailTableConfig.DestinationDatabaseName}.sys.schemas s ON s.schema_id = t.schema_id
                    WHERE s.name = '{ArchiveAndTailTableConfig.DestinationSchemaName}'
                    AND t.name = '{ArchiveAndTailTableConfig.DestinationTableName}'
                ");
                SqlCommand cmd = new SqlCommand(str, cnDestination);
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                Logger.Info(@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        public static bool CheckSourceTableExists(SqlConnection cnSource, ArchiveTableConfig mergeTableConfig)
        {
            bool retVal = false;
            try
            {
                string str = string.Format($@"
                    SELECT 1 
                    FROM {mergeTableConfig.SourceDatabaseName}.sys.tables t
                    INNER JOIN {mergeTableConfig.SourceDatabaseName}.sys.schemas s ON s.schema_id = t.schema_id
                    WHERE s.name = '{mergeTableConfig.SourceSchemaName}'
                    AND t.name = '{mergeTableConfig.SourceTableName}'
                ");
                SqlCommand cmd = new SqlCommand(str, cnSource);
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                Logger.Info(@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnSource.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        public static bool CheckSourceTableExists(SqlConnection cnSource, ArchiveAndTailTableConfig ArchiveAndTailTableConfig)
        {
            bool retVal = false;
            try
            {
                string str = string.Format($@"
                    SELECT 1 
                    FROM {ArchiveAndTailTableConfig.SourceDatabaseName}.sys.tables t
                    INNER JOIN {ArchiveAndTailTableConfig.SourceDatabaseName}.sys.schemas s ON s.schema_id = t.schema_id
                    WHERE s.name = '{ArchiveAndTailTableConfig.SourceSchemaName}'
                    AND t.name = '{ArchiveAndTailTableConfig.SourceTableName}'
                ");
                SqlCommand cmd = new SqlCommand(str, cnSource);
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                Logger.Info(@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnSource.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        public static bool SyncDestinationTableStructureMissingColumns(SqlConnection cnSource, SqlConnection cnDestination, ArchiveTableConfig mergeTableConfig, out string statement)
        {
            bool retVal = false;
            statement = BuildSynchTableStringMissingColumns(cnSource, mergeTableConfig);
            try
            {
                SqlCommand cmd = new SqlCommand(statement, cnDestination);
                Logger.Info(Logging.DbCmdString(cmd));
                var x = cmd.ExecuteNonQuery();
                retVal = true;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        public static bool SyncDestinationTableStructureMissingColumns(SqlConnection cnSource, SqlConnection cnDestination, TailTableWithVerifyConfig tailTableConfig, out string statement)
        {
            bool retVal = false;
            statement = BuildSynchTableStringMissingColumns(cnSource, tailTableConfig);
            try
            {
                SqlCommand cmd = new SqlCommand(statement, cnDestination);
                Logger.Info(Logging.DbCmdString(cmd));
                var x = cmd.ExecuteNonQuery();
                retVal = true;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        public static bool SyncDestinationTableStructureMissingColumns(SqlConnection cnSource, SqlConnection cnDestination, ArchiveAndTailTableConfig ArchiveAndTailTableConfig, out string statement)
        {
            bool retVal = false;
            statement = BuildSynchTableStringMissingColumns(cnSource, ArchiveAndTailTableConfig);
            try
            {
                SqlCommand cmd = new SqlCommand(statement, cnDestination);
                Logger.Info(Logging.DbCmdString(cmd));
                var x = cmd.ExecuteNonQuery();
                retVal = true;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        private static String BuildSynchTableStringMissingColumns(SqlConnection cnSource, ArchiveTableConfig archiveTableConfig)
        {
            string str;
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($@"ALTER TABLE [{archiveTableConfig.DestinationSchemaName}].[{archiveTableConfig.DestinationTableName}] ADD ");
                sb.Append(Environment.NewLine);
                var cmd = new SqlCommand();
                cmd = GetSourceTableMissingColumns(archiveTableConfig);
                cmd.Connection = cnSource;
                Logger.Info(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        var fieldDef = CastDbNull.To(reader["FieldDef"], string.Empty).Trim();
                        sb.Append(fieldDef);
                        sb.Append(Environment.NewLine);
                    }
                    reader.Close();
                }
                str = sb.ToString();
                Logger.Info($@"statement = {str}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnSource.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new ApplicationException($@"{cnSource.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return str;
        }

        private static String BuildSynchTableStringMissingColumns(SqlConnection cnSource, TailTableWithVerifyConfig tailTableConfig)
        {
            string str;
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($@"ALTER TABLE [{tailTableConfig.DestinationSchemaName}].[{tailTableConfig.DestinationTableName}] ADD ");
                sb.Append(Environment.NewLine);
                var cmd = new SqlCommand();
                cmd = GetSourceTableMissingColumns(tailTableConfig);
                cmd.Connection = cnSource;
                Logger.Info(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        var fieldDef = CastDbNull.To(reader["FieldDef"], string.Empty).Trim();
                        sb.Append(fieldDef);
                        sb.Append(Environment.NewLine);
                    }
                    reader.Close();
                }
                str = sb.ToString();
                Logger.Info($@"statement = {str}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnSource.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new ApplicationException($@"{cnSource.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return str;
        }

        private static String BuildSynchTableStringMissingColumns(SqlConnection cnSource, ArchiveAndTailTableConfig archiveAndTailTableConfig)
        {
            string str;
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($@"ALTER TABLE [{archiveAndTailTableConfig.DestinationSchemaName}].[{archiveAndTailTableConfig.DestinationTableName}] ADD ");
                sb.Append(Environment.NewLine);
                var cmd = new SqlCommand();
                cmd = GetSourceTableMissingColumns(archiveAndTailTableConfig);
                cmd.Connection = cnSource;
                Logger.Info(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        var fieldDef = CastDbNull.To(reader["FieldDef"], string.Empty).Trim();
                        sb.Append(fieldDef);
                        sb.Append(Environment.NewLine);
                    }
                    reader.Close();
                }
                str = sb.ToString();
                Logger.Info($@"statement = {str}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {cnSource.ConnectionString}  {e.Message}: {e.StackTrace}");
                str = "";
            }
            return str;
        }

        public static bool SyncDestinationTableStructureDifferentColumns(SqlConnection cnSource, SqlConnection cnDestination, ArchiveTableConfig mergeTableConfig, out string statement)
        {
            bool retVal = false;
            statement = BuildSynchTableStringDifferentColumns(cnSource, mergeTableConfig);
            try
            {
                SqlCommand cmd = new SqlCommand(statement, cnDestination);
                Logger.Info(Logging.DbCmdString(cmd));
                var x = cmd.ExecuteNonQuery();
                retVal = true;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        public static bool SyncDestinationTableStructureDifferentColumns(SqlConnection cnSource, SqlConnection cnDestination, ArchiveAndTailTableConfig ArchiveAndTailTableConfig, out string statement)
        {
            bool retVal = false;
            statement = BuildSynchTableStringDifferentColumns(cnSource, ArchiveAndTailTableConfig);
            try
            {
                SqlCommand cmd = new SqlCommand(statement, cnDestination);
                Logger.Info(Logging.DbCmdString(cmd));
                var x = cmd.ExecuteNonQuery();
                retVal = true;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        private static String BuildSynchTableStringDifferentColumns(SqlConnection cnSource, ArchiveTableConfig archiveTableConfig)
        {
            string str;
            try
            {
                StringBuilder sb = new StringBuilder();
                var cmd = new SqlCommand();
                cmd = GetSourceTableDifferentColumns(archiveTableConfig);
                cmd.Connection = cnSource;
                Logger.Info(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        sb.Append($@"ALTER TABLE [{archiveTableConfig.DestinationSchemaName}].[{archiveTableConfig.DestinationTableName}] ");
                        sb.Append(Environment.NewLine);
                        var fieldDef = CastDbNull.To(reader["FieldDef"], string.Empty).Trim();
                        sb.Append($@"ALTER COLUMN {fieldDef}; ");
                        sb.Append(Environment.NewLine);
                    }
                    reader.Close();
                }
                str = sb.ToString();
                Logger.Info($@"str = {str}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {cnSource.ConnectionString}  {e.Message}: {e.StackTrace}");
                str = "";
            }
            return str;
        }

        private static String BuildSynchTableStringDifferentColumns(SqlConnection cnSource, ArchiveAndTailTableConfig ArchiveAndTailTableConfig)
        {
            string str;
            try
            {
                StringBuilder sb = new StringBuilder();
                var cmd = new SqlCommand();
                cmd = GetSourceTableDifferentColumns(ArchiveAndTailTableConfig);
                cmd.Connection = cnSource;
                Logger.Info(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        sb.Append($@"ALTER TABLE [{ArchiveAndTailTableConfig.DestinationSchemaName}].[{ArchiveAndTailTableConfig.DestinationTableName}] ");
                        sb.Append(Environment.NewLine);
                        var fieldDef = CastDbNull.To(reader["FieldDef"], string.Empty).Trim();
                        sb.Append($@"ALTER COLUMN {fieldDef}; ");
                        sb.Append(Environment.NewLine);
                    }
                    reader.Close();
                }
                str = sb.ToString();
                Logger.Info($@"str = {str}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {cnSource.ConnectionString}  {e.Message}: {e.StackTrace}");
                str = "";
            }
            return str;
        }

        public static bool CreateDestinationTable(SqlConnection cnSource, SqlConnection cnDestination, ArchiveTableConfig mergeTableConfig, out string statement)
        {
            bool retVal = false;
            statement = BuildCreateTableString(cnSource, mergeTableConfig);
            try
            {
                SqlCommand cmd = new SqlCommand(statement, cnDestination);
                Logger.Info(Logging.DbCmdString(cmd));
                var x = cmd.ExecuteNonQuery();
                retVal = true;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        public static bool CreateDestinationTable(SqlConnection cnSource, SqlConnection cnDestination, TailTableWithVerifyConfig tailTableConfig, out string statement)
        {
            bool retVal = false;
            statement = BuildCreateTableString(cnSource, tailTableConfig);
            try
            {
                SqlCommand cmd = new SqlCommand(statement, cnDestination);
                Logger.Info(Logging.DbCmdString(cmd));
                var x = cmd.ExecuteNonQuery();
                retVal = true;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        public static bool CreateDestinationTable(SqlConnection cnSource, SqlConnection cnDestination, ArchiveAndTailTableConfig ArchiveAndTailTableConfig, out string statement)
        {
            bool retVal = false;
            statement = BuildCreateTableString(cnSource, ArchiveAndTailTableConfig);
            try
            {
                SqlCommand cmd = new SqlCommand(statement, cnDestination);
                Logger.Info(Logging.DbCmdString(cmd));
                var x = cmd.ExecuteNonQuery();
                retVal = true;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        private static String BuildCreateTableString(SqlConnection cnSource, ArchiveTableConfig mergeTableConfig)
        {
            string str;
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($@"CREATE TABLE [{mergeTableConfig.DestinationSchemaName}].[{mergeTableConfig.DestinationTableName}] ");
                sb.Append(" ( ");
                var cmd = new SqlCommand();
                cmd = GetSourceTableMetadata();
                cmd.Parameters["@TableName"].Value = mergeTableConfig.DestinationTableName;
                cmd.Parameters["@SchemaName"].Value = mergeTableConfig.DestinationSchemaName;
                cmd.Connection = cnSource;
                Logger.Info(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        var fieldDef = CastDbNull.To(reader["FieldDef"], string.Empty).Trim();
                        sb.Append(fieldDef);
                    }
                    reader.Close();
                }
                sb.Append(")");
                str = sb.ToString();
                Logger.Info($@"str = {str}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {cnSource.ConnectionString}  {e.Message}: {e.StackTrace}");
                str = "";
            }
            return str;
        }

        private static String BuildCreateTableString(SqlConnection cnSource, TailTableWithVerifyConfig tailTableConfig)
        {
            string str;
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($@"CREATE TABLE [{tailTableConfig.DestinationSchemaName}].[{tailTableConfig.DestinationTableName}] ");
                sb.Append(" ( ");
                var cmd = new SqlCommand();
                cmd = GetSourceTableMetadata();
                cmd.Parameters["@TableName"].Value = tailTableConfig.DestinationTableName;
                cmd.Parameters["@SchemaName"].Value = tailTableConfig.DestinationSchemaName;
                cmd.Connection = cnSource;
                Logger.Info(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        var fieldDef = CastDbNull.To(reader["FieldDef"], string.Empty).Trim();
                        sb.Append(fieldDef);
                    }
                    reader.Close();
                }
                sb.Append(")");
                str = sb.ToString();
                Logger.Info($@"str = {str}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {cnSource.ConnectionString}  {e.Message}: {e.StackTrace}");
                str = "";
            }
            return str;
        }

        private static String BuildCreateTableString(SqlConnection cnSource, ArchiveAndTailTableConfig ArchiveAndTailTableConfig)
        {
            string str;
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($@"CREATE TABLE [{ArchiveAndTailTableConfig.DestinationSchemaName}].[{ArchiveAndTailTableConfig.DestinationTableName}] ");
                sb.Append(" ( ");
                var cmd = new SqlCommand();
                cmd = GetSourceTableMetadata();
                cmd.Parameters["@TableName"].Value = ArchiveAndTailTableConfig.DestinationTableName;
                cmd.Parameters["@SchemaName"].Value = ArchiveAndTailTableConfig.DestinationSchemaName;
                cmd.Connection = cnSource;
                Logger.Info(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        var fieldDef = CastDbNull.To(reader["FieldDef"], string.Empty).Trim();
                        sb.Append(fieldDef);
                    }
                    reader.Close();
                }
                sb.Append(")");
                str = sb.ToString();
                Logger.Info($@"str = {str}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {cnSource.ConnectionString}  {e.Message}: {e.StackTrace}");
                str = "";
            }
            return str;
        }

        private static SqlCommand GetSourceTableMetadata()
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
                           c.max_length,
                           c.precision,
                           c.scale,
                           t.name,
                           t.schema_id,
                           t.max_length,
                           t.precision,
                           t.scale
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
                    ORDER BY c.column_id;
                ";
            }
            catch (Exception e)
            {
                throw new Exception(e.ToString());
            }
            cmd.Parameters.Add(new SqlParameter("@SchemaName", SqlDbType.VarChar));
            cmd.Parameters.Add(new SqlParameter("@TableName", SqlDbType.VarChar));
            return cmd;
        }

        private static SqlCommand GetSourceTableMissingColumns(ArchiveTableConfig archiveTableConfig)
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
                           c.max_length,
                           c.precision,
                           c.scale,
                           t.name,
                           t.schema_id,
                           t.max_length,
                           t.precision,
                           t.scale
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
                    WHERE s.name = '{archiveTableConfig.SourceSchemaName}'
                        AND tb.name = '{archiveTableConfig.SourceTableName}'
						AND c.name IN (
										SELECT  src.[COLUMN_NAME]
										FROM    ( SELECT    *
													FROM      {archiveTableConfig.SourceDatabaseName}.information_schema.columns
													WHERE     [TABLE_SCHEMA] = '{archiveTableConfig.SourceSchemaName}'
															AND [TABLE_NAME] = '{archiveTableConfig.SourceTableName}'
												) src
												FULL JOIN ( SELECT  *
															FROM    {archiveTableConfig.DestinationDatabaseName}.information_schema.COLUMNS
															WHERE   [TABLE_SCHEMA] = '{archiveTableConfig.SourceSchemaName}'
																	AND [TABLE_NAME] = '{archiveTableConfig.SourceTableName}'
															) targ ON targ.[COLUMN_NAME] = src.[COLUMN_NAME]
										WHERE   targ.[DATA_TYPE] IS NULL
										)
                    ORDER BY c.column_id;
                ";
            }
            catch (Exception e)
            {
                throw new Exception(e.ToString());
            }
            return cmd;
        }

        private static SqlCommand GetSourceTableMissingColumns(TailTableWithVerifyConfig tailTableConfig)
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
                           c.max_length,
                           c.precision,
                           c.scale,
                           t.name,
                           t.schema_id,
                           t.max_length,
                           t.precision,
                           t.scale
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
                    WHERE s.name = '{tailTableConfig.SourceSchemaName}'
                        AND tb.name = '{tailTableConfig.SourceTableName}'
						AND c.name IN (
										SELECT  src.[COLUMN_NAME]
										FROM    ( SELECT    *
													FROM      {tailTableConfig.SourceDatabaseName}.information_schema.columns
													WHERE     [TABLE_SCHEMA] = '{tailTableConfig.SourceSchemaName}'
															AND [TABLE_NAME] = '{tailTableConfig.SourceTableName}'
												) src
												FULL JOIN ( SELECT  *
															FROM    {tailTableConfig.DestinationDatabaseName}.information_schema.COLUMNS
															WHERE   [TABLE_SCHEMA] = '{tailTableConfig.SourceSchemaName}'
																	AND [TABLE_NAME] = '{tailTableConfig.SourceTableName}'
															) targ ON targ.[COLUMN_NAME] = src.[COLUMN_NAME]
										WHERE   targ.[DATA_TYPE] IS NULL
										)
                    ORDER BY c.column_id;
                ";
            }
            catch (Exception e)
            {
                throw new Exception(e.ToString());
            }
            return cmd;
        }

        private static SqlCommand GetSourceTableMissingColumns(ArchiveAndTailTableConfig archiveTableConfig)
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
                           c.max_length,
                           c.precision,
                           c.scale,
                           t.name,
                           t.schema_id,
                           t.max_length,
                           t.precision,
                           t.scale
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
                    WHERE s.name = '{archiveTableConfig.SourceSchemaName}'
                        AND tb.name = '{archiveTableConfig.SourceTableName}'
						AND c.name IN (
										SELECT  src.[COLUMN_NAME]
										FROM    ( SELECT    *
													FROM      {archiveTableConfig.SourceDatabaseName}.information_schema.columns
													WHERE     [TABLE_SCHEMA] = '{archiveTableConfig.SourceSchemaName}'
															AND [TABLE_NAME] = '{archiveTableConfig.SourceTableName}'
												) src
												FULL JOIN ( SELECT  *
															FROM    {archiveTableConfig.DestinationDatabaseName}.information_schema.COLUMNS
															WHERE   [TABLE_SCHEMA] = '{archiveTableConfig.SourceSchemaName}'
																	AND [TABLE_NAME] = '{archiveTableConfig.SourceTableName}'
															) targ ON targ.[COLUMN_NAME] = src.[COLUMN_NAME]
										WHERE   targ.[DATA_TYPE] IS NULL
										)
                    ORDER BY c.column_id;
                ";
            }
            catch (Exception e)
            {
                throw new Exception(e.ToString());
            }
            return cmd;
        }


        //  private static SqlCommand GetSourceTableMissingColumns(ArchiveTableConfig archiveTableConfig)
        //  {
        //      var cmd = new SqlCommand();
        //      try
        //      {
        //          cmd.CommandType = CommandType.Text;
        //          cmd.CommandText = $@"
        //              SELECT '[' + c.name + '] [' + UPPER(t.name) + ']'
        //                     + CASE
        //                           WHEN t.name IN ( 'bigint', 'bit', 'int', 'money', 'smallint', 'smallmoney', 'tinyint', 'float', 'real',
        //                                            'date', 'datetime', 'datetime2', 'smalldatetime', 'datetimeoffset', 'time', 'image',
        //                                            'uniqueidentifier', 'sysname', 'sql_variant', 'xml'
        //                                          ) THEN
        //                               ''
        //                           WHEN t.name IN ( 'varchar', 'char', 'text', 'binary', 'varbinary', 'nchar', 'nvarchar', 'ntext' ) THEN
        //                               '(' + CASE WHEN c.max_length = -1 THEN 'MAX' else CONVERT(VARCHAR, c.max_length ) END + ')'
        //                           WHEN t.name IN ( 'decimal', 'numeric' ) THEN
        //                               '(' + CONVERT(VARCHAR, c.precision) + ', ' + CONVERT(VARCHAR, c.scale) + ')'
        //                           ELSE
        //                               '***'
        //                       END + ' NULL' + CASE
        //                                           WHEN c.column_id = x.MaxColId THEN
        //                                               ''
        //                                           ELSE
        //                                               ','
        //                                       END AS FieldDef,
        //                     c.column_id,
        //                     x.MaxColId,
        //                     c.max_length,
        //                     c.precision,
        //                     c.scale,
        //                     t.name,
        //                     t.schema_id,
        //                     t.max_length,
        //                     t.precision,
        //                     t.scale
        //              FROM sys.columns c
        //                  INNER JOIN sys.types t
        //                      ON t.system_type_id = c.system_type_id
        //	AND t.user_type_id = c.user_type_id
        //               INNER JOIN sys.tables tb 
        //                ON TB.OBJECT_ID = c.object_id
        //INNER JOIN sys.schemas s ON s.schema_id = tb.schema_id
        //                  OUTER APPLY
        //              (
        //                  SELECT MAX(column_id) AS MaxColId
        //                  FROM sys.columns c2
        //                  WHERE c2.object_id = c.object_id
        //              ) x
        //              WHERE s.name = '{archiveTableConfig.SourceSchemaName}'
        //                  AND tb.name = '{archiveTableConfig.SourceTableName}'
        //AND c.name IN (
        //				SELECT  src.[COLUMN_NAME]
        //				FROM    ( SELECT    *
        //							FROM      {archiveTableConfig.SourceDatabaseName}.information_schema.columns
        //							WHERE     [TABLE_SCHEMA] = '{archiveTableConfig.SourceSchemaName}'
        //									AND [TABLE_NAME] = '{archiveTableConfig.SourceTableName}'
        //						) src
        //						FULL JOIN ( SELECT  *
        //									FROM    {archiveTableConfig.DestinationDatabaseName}.information_schema.COLUMNS
        //									WHERE   [TABLE_SCHEMA] = '{archiveTableConfig.SourceSchemaName}'
        //											AND [TABLE_NAME] = '{archiveTableConfig.SourceTableName}'
        //									) targ ON targ.[COLUMN_NAME] = src.[COLUMN_NAME]
        //				WHERE   targ.[DATA_TYPE] IS NULL
        //				)
        //              ORDER BY c.column_id;
        //          ";
        //      }
        //      catch (Exception e)
        //      {
        //          throw new Exception(e.ToString());
        //      }
        //      return cmd;
        //  }

        //  private static SqlCommand GetSourceTableMissingColumns(TailTableWithVerifyConfig tailTableConfig)
        //  {
        //      var cmd = new SqlCommand();
        //      try
        //      {
        //          cmd.CommandType = CommandType.Text;
        //          cmd.CommandText = $@"
        //              SELECT '[' + c.name + '] [' + UPPER(t.name) + ']'
        //                     + CASE
        //                           WHEN t.name IN ( 'bigint', 'bit', 'int', 'money', 'smallint', 'smallmoney', 'tinyint', 'float', 'real',
        //                                            'date', 'datetime', 'datetime2', 'smalldatetime', 'datetimeoffset', 'time', 'image',
        //                                            'uniqueidentifier', 'sysname', 'sql_variant', 'xml'
        //                                          ) THEN
        //                               ''
        //                           WHEN t.name IN ( 'varchar', 'char', 'text', 'binary', 'varbinary', 'nchar', 'nvarchar', 'ntext' ) THEN
        //                               '(' + CASE WHEN c.max_length = -1 THEN 'MAX' else CONVERT(VARCHAR, c.max_length ) END + ')'
        //                           WHEN t.name IN ( 'decimal', 'numeric' ) THEN
        //                               '(' + CONVERT(VARCHAR, c.precision) + ', ' + CONVERT(VARCHAR, c.scale) + ')'
        //                           ELSE
        //                               '***'
        //                       END + ' NULL' + CASE
        //                                           WHEN c.column_id = x.MaxColId THEN
        //                                               ''
        //                                           ELSE
        //                                               ','
        //                                       END AS FieldDef,
        //                     c.column_id,
        //                     x.MaxColId,
        //                     c.max_length,
        //                     c.precision,
        //                     c.scale,
        //                     t.name,
        //                     t.schema_id,
        //                     t.max_length,
        //                     t.precision,
        //                     t.scale
        //              FROM sys.columns c
        //                  INNER JOIN sys.types t
        //                      ON t.system_type_id = c.system_type_id
        //	AND t.user_type_id = c.user_type_id
        //               INNER JOIN sys.tables tb 
        //                ON TB.OBJECT_ID = c.object_id
        //INNER JOIN sys.schemas s ON s.schema_id = tb.schema_id
        //                  OUTER APPLY
        //              (
        //                  SELECT MAX(column_id) AS MaxColId
        //                  FROM sys.columns c2
        //                  WHERE c2.object_id = c.object_id
        //              ) x
        //              WHERE s.name = '{tailTableConfig.SourceSchemaName}'
        //                  AND tb.name = '{tailTableConfig.SourceTableName}'
        //AND c.name IN (
        //				SELECT  src.[COLUMN_NAME]
        //				FROM    ( SELECT    *
        //							FROM      {tailTableConfig.SourceDatabaseName}.information_schema.columns
        //							WHERE     [TABLE_SCHEMA] = '{tailTableConfig.SourceSchemaName}'
        //									AND [TABLE_NAME] = '{tailTableConfig.SourceTableName}'
        //						) src
        //						FULL JOIN ( SELECT  *
        //									FROM    {tailTableConfig.DestinationDatabaseName}.information_schema.COLUMNS
        //									WHERE   [TABLE_SCHEMA] = '{tailTableConfig.SourceSchemaName}'
        //											AND [TABLE_NAME] = '{tailTableConfig.SourceTableName}'
        //									) targ ON targ.[COLUMN_NAME] = src.[COLUMN_NAME]
        //				WHERE   targ.[DATA_TYPE] IS NULL
        //				)
        //              ORDER BY c.column_id;
        //          ";
        //      }
        //      catch (Exception e)
        //      {
        //          throw new Exception(e.ToString());
        //      }
        //      return cmd;
        //  }

        private static SqlCommand GetSourceTableDifferentColumns(ArchiveTableConfig mergeTableConfig)
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
                                                     ''
                                             END AS FieldDef,
                           c.column_id,
                           x.MaxColId,
                           c.max_length,
                           c.precision,
                           c.scale,
                           t.name,
                           t.schema_id,
                           t.max_length,
                           t.precision,
                           t.scale
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
                    WHERE s.name = '{mergeTableConfig.SourceSchemaName}'
                        AND tb.name = '{mergeTableConfig.SourceTableName}'
						AND c.name IN (
										SELECT  src.[COLUMN_NAME]
										FROM    ( SELECT    *
													FROM      {mergeTableConfig.SourceDatabaseName}.information_schema.columns
													WHERE     [TABLE_SCHEMA] = '{mergeTableConfig.SourceSchemaName}'
															AND [TABLE_NAME] = '{mergeTableConfig.SourceTableName}'
												) src
												FULL JOIN ( SELECT  *
															FROM    {mergeTableConfig.DestinationDatabaseName}.information_schema.COLUMNS
															WHERE   [TABLE_SCHEMA] = '{mergeTableConfig.SourceSchemaName}'
																	AND [TABLE_NAME] = '{mergeTableConfig.SourceTableName}'
															) targ ON targ.[COLUMN_NAME] = src.[COLUMN_NAME]
                                        WHERE   ISNULL(targ.[DATA_TYPE], '') <> src.[DATA_TYPE]
					                    OR targ.[CHARACTER_MAXIMUM_LENGTH] < src.[CHARACTER_MAXIMUM_LENGTH]
					                    OR targ.[NUMERIC_PRECISION] < src.[NUMERIC_PRECISION]
					                    OR targ.[NUMERIC_SCALE] < src.[NUMERIC_SCALE]
										)
                    ORDER BY c.column_id;
                ";
            }
            catch (Exception e)
            {
                throw new Exception(e.ToString());
            }
            return cmd;
        }

        private static SqlCommand GetSourceTableDifferentColumns(ArchiveAndTailTableConfig ArchiveAndTailTableConfig)
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
                                                     ''
                                             END AS FieldDef,
                           c.column_id,
                           x.MaxColId,
                           c.max_length,
                           c.precision,
                           c.scale,
                           t.name,
                           t.schema_id,
                           t.max_length,
                           t.precision,
                           t.scale
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
                    WHERE s.name = '{ArchiveAndTailTableConfig.SourceSchemaName}'
                        AND tb.name = '{ArchiveAndTailTableConfig.SourceTableName}'
						AND c.name IN (
										SELECT  src.[COLUMN_NAME]
										FROM    ( SELECT    *
													FROM      {ArchiveAndTailTableConfig.SourceDatabaseName}.information_schema.columns
													WHERE     [TABLE_SCHEMA] = '{ArchiveAndTailTableConfig.SourceSchemaName}'
															AND [TABLE_NAME] = '{ArchiveAndTailTableConfig.SourceTableName}'
												) src
												FULL JOIN ( SELECT  *
															FROM    {ArchiveAndTailTableConfig.DestinationDatabaseName}.information_schema.COLUMNS
															WHERE   [TABLE_SCHEMA] = '{ArchiveAndTailTableConfig.SourceSchemaName}'
																	AND [TABLE_NAME] = '{ArchiveAndTailTableConfig.SourceTableName}'
															) targ ON targ.[COLUMN_NAME] = src.[COLUMN_NAME]
                                        WHERE   ISNULL(targ.[DATA_TYPE], '') <> src.[DATA_TYPE]
					                    OR targ.[CHARACTER_MAXIMUM_LENGTH] < src.[CHARACTER_MAXIMUM_LENGTH]
					                    OR targ.[NUMERIC_PRECISION] < src.[NUMERIC_PRECISION]
					                    OR targ.[NUMERIC_SCALE] < src.[NUMERIC_SCALE]
										)
                    ORDER BY c.column_id;
                ";
            }
            catch (Exception e)
            {
                throw new Exception(e.ToString());
            }
            return cmd;
        }

        //public static bool CheckDestinationTableUniqueIndex(SqlConnection cnDestination, ArchiveTableConfig mergeTableConfig)
        //{
        //    bool retVal = false;
        //    try
        //    {
        //        //var x = string.Join("','", mergeTableConfig.DestinationKeyColumns);
        //        var columnsIn = "";
        //        var keyColumnList = mergeTableConfig.DestinationKey.Split(',').ToList();
        //        foreach (var keyColumn in keyColumnList)
        //        {
        //            columnsIn += $@"'{keyColumn}',";
        //        }
        //        columnsIn = columnsIn.Remove(columnsIn.LastIndexOf(","));
        //        string cmdString = $@"
        //            SELECT COUNT(*)
        //            FROM
        //            (
        //                SELECT i.index_id,
        //                       i.object_id,
        //                       i.name AS IndexName,
        //                       OBJECT_SCHEMA_NAME(i.object_id) AS SchemaName,
        //                       OBJECT_NAME(i.object_id) AS TableName,
        //                       (
        //                           SELECT c.name
        //                           FROM sys.columns c
        //                           WHERE object_id = ic.object_id
        //                                 AND c.column_id = ic.column_id
        //                                 AND c.name IN ({columnsIn})
        //                       ) AS ColumnName,
        //                       ic.index_column_id,
        //                       ic.is_included_column
        //                FROM sys.index_columns ic
        //                    INNER JOIN sys.indexes i
        //                        ON i.object_id = ic.object_id
        //                           AND i.index_id = ic.index_id
        //                WHERE OBJECT_SCHEMA_NAME(i.object_id) = '{mergeTableConfig.DestinationSchemaName}'
        //                      AND OBJECT_NAME(i.object_id) = '{mergeTableConfig.DestinationTableName}'
        //                      AND i.is_unique = 1
        //                      AND ic.is_included_column = 0
        //            ) a
        //            WHERE ColumnName IS NOT NULL
        //        ";
        //        SqlCommand cmd = new SqlCommand(cmdString, cnDestination);
        //        Logger.Info(Logging.DbCmdString(cmd));
        //        var rowCount = Convert.ToInt32(cmd.ExecuteScalar());
        //        retVal = rowCount == keyColumnList.Count();
        //        Logger.Info($@"retVal = {retVal}");
        //    }
        //    catch (Exception e)
        //    {
        //        Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
        //    }
        //    return retVal;
        //}

        public static bool CheckDestinationTableUniqueIndex(SqlConnection cnDestination, ArchiveAndTailTableConfig ArchiveAndTailTableConfig)
        {
            bool retVal = false;
            try
            {
                //var x = string.Join("','", mergeTableConfig.DestinationKeyColumns);
                var columnsIn = "";
                var keyColumnList = ArchiveAndTailTableConfig.DestinationKey.Split(',').ToList();
                foreach (var keyColumn in keyColumnList)
                {
                    columnsIn += $@"'{keyColumn}',";
                }
                columnsIn = columnsIn.Remove(columnsIn.LastIndexOf(","));
                string cmdString = $@"
                    SELECT COUNT(*)
                    FROM
                    (
                        SELECT i.index_id,
                               i.object_id,
                               i.name AS IndexName,
                               OBJECT_SCHEMA_NAME(i.object_id) AS SchemaName,
                               OBJECT_NAME(i.object_id) AS TableName,
                               (
                                   SELECT c.name
                                   FROM sys.columns c
                                   WHERE object_id = ic.object_id
                                         AND c.column_id = ic.column_id
                                         AND c.name IN ({columnsIn})
                               ) AS ColumnName,
                               ic.index_column_id,
                               ic.is_included_column
                        FROM sys.index_columns ic
                            INNER JOIN sys.indexes i
                                ON i.object_id = ic.object_id
                                   AND i.index_id = ic.index_id
                        WHERE OBJECT_SCHEMA_NAME(i.object_id) = '{ArchiveAndTailTableConfig.DestinationSchemaName}'
                              AND OBJECT_NAME(i.object_id) = '{ArchiveAndTailTableConfig.DestinationTableName}'
                              AND i.is_unique = 1
                              AND ic.is_included_column = 0
                    ) a
                    WHERE ColumnName IS NOT NULL
                ";
                SqlCommand cmd = new SqlCommand(cmdString, cnDestination);
                Logger.Info(Logging.DbCmdString(cmd));
                var rowCount = Convert.ToInt32(cmd.ExecuteScalar());
                retVal = rowCount == keyColumnList.Count();
                Logger.Info($@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        public static bool DropDestinationTableIndex(SqlConnection cnDestination, ArchiveTableConfig mergeTableConfig)
        {
            bool retVal = false;
            try
            {
                var str = $@"DROP INDEX [IDX_Archie] ON [{mergeTableConfig.DestinationSchemaName}].[{mergeTableConfig.DestinationTableName}]";
                SqlCommand cmd = new SqlCommand(str, cnDestination);
                Logger.Info(Logging.DbCmdString(cmd));
                cmd.ExecuteNonQuery();
                retVal = true;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (System.Exception )
            {
                //Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        public static bool DropDestinationTableIndex(SqlConnection cnDestination, TailTableWithVerifyConfig tailTableConfig)
        {
            bool retVal = false;
            try
            {
                var str = $@"DROP INDEX [IDX_Archie] ON [{tailTableConfig.DestinationSchemaName}].[{tailTableConfig.DestinationTableName}]";
                SqlCommand cmd = new SqlCommand(str, cnDestination);
                Logger.Info(Logging.DbCmdString(cmd));
                cmd.ExecuteNonQuery();
                retVal = true;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (System.Exception)
            {
                //Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        public static bool DropDestinationTableIndex(SqlConnection cnDestination, ArchiveAndTailTableConfig ArchiveAndTailTableConfig)
        {
            bool retVal = false;
            try
            {
                var str = $@"DROP INDEX [IDX_Archie] ON [{ArchiveAndTailTableConfig.DestinationSchemaName}].[{ArchiveAndTailTableConfig.DestinationTableName}]";
                SqlCommand cmd = new SqlCommand(str, cnDestination);
                Logger.Info(Logging.DbCmdString(cmd));
                cmd.ExecuteNonQuery();
                retVal = true;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (System.Exception)
            {
                //Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        //public static bool CreateDestinationTableIndex(SqlConnection cnDestination, ArchiveTableConfig mergeTableConfig, out string statement)
        //{
        //    bool retVal = false;
        //    try
        //    {
        //        StringBuilder sb = new StringBuilder();
        //        sb.Append($@"CREATE UNIQUE NONCLUSTERED INDEX [IDX_Archie] ON [{mergeTableConfig.DestinationSchemaName}].[{mergeTableConfig.DestinationTableName}]");
        //        sb.Append(Environment.NewLine);
        //        sb.Append("(");
        //        var keyColumnList = mergeTableConfig.DestinationKey.Split(',').ToList();
        //        foreach (var keyColumn in keyColumnList)
        //        {
        //            sb.Append($@"[{keyColumn}] ASC,");
        //            sb.Append(Environment.NewLine);
        //        }
        //        statement = sb.ToString();
        //        statement = statement.Remove(statement.LastIndexOf(","));
        //        statement += $@")WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = OFF) ON [PRIMARY]";
        //        SqlCommand cmd = new SqlCommand(statement, cnDestination);
        //        Logger.Info(Logging.DbCmdString(cmd));
        //        cmd.ExecuteNonQuery();
        //        retVal = true;
        //        Logger.Info($@"retVal = {retVal}");
        //    }
        //    catch (System.Exception e)
        //    {
        //        Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
        //        statement = "";
        //    }
        //    return retVal;
        //}

        public static bool CreateDestinationTableIndex(SqlConnection cnDestination, TailTableWithVerifyConfig tailTableConfig, out string statement)
        {
            bool retVal = false;
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($@"CREATE UNIQUE NONCLUSTERED INDEX [IDX_Archie] ON [{tailTableConfig.DestinationSchemaName}].[{tailTableConfig.DestinationTableName}]");
                sb.Append(Environment.NewLine);
                sb.Append("(");
                var keyColumnList = tailTableConfig.DestinationKey.Split(',').ToList();
                foreach (var keyColumn in keyColumnList)
                {
                    sb.Append($@"[{keyColumn}] ASC,");
                    sb.Append(Environment.NewLine);
                }
                statement = sb.ToString();
                statement = statement.Remove(statement.LastIndexOf(","));
                statement += $@")WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = OFF) ON [PRIMARY]";
                SqlCommand cmd = new SqlCommand(statement, cnDestination);
                Logger.Info(Logging.DbCmdString(cmd));
                cmd.ExecuteNonQuery();
                retVal = true;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
                statement = "";
            }
            return retVal;
        }

        public static bool CreateDestinationTableIndex(SqlConnection cnDestination, ArchiveAndTailTableConfig ArchiveAndTailTableConfig, out string statement)
        {
            bool retVal = false;
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($@"CREATE UNIQUE NONCLUSTERED INDEX [IDX_Archie] ON [{ArchiveAndTailTableConfig.DestinationSchemaName}].[{ArchiveAndTailTableConfig.DestinationTableName}]");
                sb.Append(Environment.NewLine);
                sb.Append("(");
                var keyColumnList = ArchiveAndTailTableConfig.DestinationKey.Split(',').ToList();
                foreach (var keyColumn in keyColumnList)
                {
                    sb.Append($@"[{keyColumn}] ASC,");
                    sb.Append(Environment.NewLine);
                }
                statement = sb.ToString();
                statement = statement.Remove(statement.LastIndexOf(","));
                statement += $@")WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = OFF) ON [PRIMARY]";
                SqlCommand cmd = new SqlCommand(statement, cnDestination);
                Logger.Info(Logging.DbCmdString(cmd));
                cmd.ExecuteNonQuery();
                retVal = true;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
                statement = "";
            }
            return retVal;
        }

        //public static DateTime GetMinDestinationDate(SqlConnection cnDestination, ArchiveTableConfig mergeTableConfig)
        //{
        //    var startTime = DateTime.Now.ToLocalTime();
        //    Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
        //    DateTime retVal;
        //    try
        //    {
        //        var cmd = new SqlCommand
        //        {
        //            Connection = cnDestination,
        //            CommandType = CommandType.Text,
        //            CommandTimeout = 0,
        //            CommandText = $@"
        //                SELECT ISNULL(MIN([{mergeTableConfig.DateColumn}]), '1900-01-01')  
        //                FROM [{mergeTableConfig.DestinationSchemaName}].[{mergeTableConfig.DestinationTableName}] WITH (NOLOCK) 
        //            "
        //        };
        //        Logger.Info(Logging.DbCmdString(cmd));
        //        retVal = Convert.ToDateTime(cmd.ExecuteScalar());
        //        Logger.Info("ROWS: " + retVal);
        //    }
        //    catch (Exception e)
        //    {
        //        Logger.Error("ERROR: " + cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
        //        throw new ApplicationException(cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
        //    }
        //    finally
        //    {
        //        Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
        //    }
        //    return retVal;
        //}

        public static DateTime GetMinDestinationDate(SqlConnection cnDestination, ArchiveAndTailTableConfig ArchiveAndTailTableConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            DateTime retVal;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnDestination,
                    CommandType = CommandType.Text,
                    CommandTimeout = 0,
                    CommandText = $@"
                        SELECT ISNULL(MIN([{ArchiveAndTailTableConfig.DateColumn}]), '1900-01-01')  
                        FROM [{ArchiveAndTailTableConfig.DestinationSchemaName}].[{ArchiveAndTailTableConfig.DestinationTableName}] WITH (NOLOCK) 
                    "
                };
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToDateTime(cmd.ExecuteScalar());
                Logger.Info("ROWS: " + retVal);
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return retVal;
        }

        //public static DateTime GetMaxDestinationDate(SqlConnection cnDestination, ArchiveTableConfig mergeTableConfig)
        //{
        //    var startTime = DateTime.Now.ToLocalTime();
        //    Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
        //    DateTime retVal;
        //    try
        //    {
        //        var cmd = new SqlCommand
        //        {
        //            Connection = cnDestination,
        //            CommandType = CommandType.Text,
        //            CommandTimeout = 0,
        //            CommandText = $@"
        //                SELECT ISNULL(MAX([{mergeTableConfig.DateColumn}]), '1900-01-01')  
        //                FROM [{mergeTableConfig.DestinationSchemaName}].[{mergeTableConfig.DestinationTableName}] WITH (NOLOCK) 
        //            "
        //        };
        //        Logger.Info(Logging.DbCmdString(cmd));
        //        retVal = Convert.ToDateTime(cmd.ExecuteScalar());
        //        Logger.Info("ROWS: " + retVal);
        //    }
        //    catch (Exception e)
        //    {
        //        Logger.Error("ERROR: " + cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
        //        throw new ApplicationException(cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
        //    }
        //    finally
        //    {
        //        Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
        //    }
        //    return retVal;
        //}

        //public static DateTime GetHighWaterMarkDate(SqlConnection cnSource, ArchiveTableConfig archiveTableConfig)
        //{
        //    var startTime = DateTime.Now.ToLocalTime();
        //    Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
        //    DateTime retVal;
        //    try
        //    {
        //        var cmd = new SqlCommand
        //        {
        //            Connection = cnSource,
        //            CommandType = CommandType.Text,
        //            CommandTimeout = 0,
        //            CommandText = BuildHighWaterMarkDateString(archiveTableConfig)
        //        };
        //        Logger.Info(Logging.DbCmdString(cmd));
        //        retVal = Convert.ToDateTime(cmd.ExecuteScalar());
        //        Logger.Info("retVal: " + retVal);
        //    }
        //    catch (Exception e)
        //    {
        //        Logger.Error("ERROR: " + cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
        //        throw new ApplicationException(cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
        //    }
        //    finally
        //    {
        //        Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
        //    }
        //    return retVal;
        //}

        //private static String BuildHighWaterMarkDateString(ArchiveTableConfig archiveTableConfig)
        //{
        //    string str;
        //    try
        //    {
        //        StringBuilder sb = new StringBuilder();
        //        sb.Append($@"SELECT ISNULL(ISNULL(MIN([SRC].[{archiveTableConfig.DateColumn}]), (SELECT MAX([{archiveTableConfig.DateColumn}]) FROM [{archiveTableConfig.DestinationDatabaseName}].[{archiveTableConfig.DestinationSchemaName}].[{archiveTableConfig.DestinationTableName}] WITH (NOLOCK))), '1900-01-01') ");
        //        sb.Append(Environment.NewLine);
        //        sb.Append($@"FROM [{archiveTableConfig.SourceDatabaseName}].[{archiveTableConfig.SourceSchemaName}].[{archiveTableConfig.SourceTableName}] AS SRC WITH (NOLOCK) ");
        //        sb.Append(Environment.NewLine);
        //        sb.Append($@"WHERE NOT EXISTS (SELECT 1 FROM [{archiveTableConfig.DestinationDatabaseName}].[{archiveTableConfig.DestinationSchemaName}].[{archiveTableConfig.DestinationTableName}] DEST WITH (NOLOCK) ");
        //        sb.Append(Environment.NewLine);
        //        sb.Append("                  WHERE ");
        //        var keyColumnList = archiveTableConfig.DestinationKey.Split(',').ToList();
        //        var i = 0;
        //        foreach (var keyColumn in keyColumnList)
        //        {
        //            i++;
        //            sb.Append($@"DEST.[{keyColumn}] = SRC.[{keyColumn}]");
        //            sb.Append(Environment.NewLine);
        //            if (i < keyColumnList.Count) sb.Append("AND ");
        //        }
        //        sb.Append(") ");
        //        str = sb.ToString();
        //        //Logger.Info($@"str = {str}");
        //    }
        //    catch (System.Exception e)
        //    {
        //        Logger.Error($"ERROR: {e.Message}: {e.StackTrace}");
        //        str = "";
        //    }
        //    return str;
        //}

        public static DateTime GetHighWaterMarkDate(SqlConnection cnSource, ArchiveAndTailTableConfig archiveAndTailTableConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            DateTime retVal;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnSource,
                    CommandType = CommandType.Text,
                    CommandTimeout = 0,
                    CommandText = BuildHighWaterMarkDateString(archiveAndTailTableConfig)
                };
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToDateTime(cmd.ExecuteScalar());
                Logger.Info("retVal: " + retVal);
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return retVal;
        }

        private static String BuildHighWaterMarkDateString(ArchiveAndTailTableConfig archiveAndTailTableConfig)
        {
            string str;
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($@"SELECT ISNULL(ISNULL(MIN([SRC].[{archiveAndTailTableConfig.DateColumn}]), (SELECT MAX([{archiveAndTailTableConfig.DateColumn}]) FROM [{archiveAndTailTableConfig.DestinationDatabaseName}].[{archiveAndTailTableConfig.DestinationSchemaName}].[{archiveAndTailTableConfig.DestinationTableName}] WITH (NOLOCK))), '1900-01-01') ");
                sb.Append(Environment.NewLine);
                sb.Append($@"FROM [{archiveAndTailTableConfig.SourceDatabaseName}].[{archiveAndTailTableConfig.SourceSchemaName}].[{archiveAndTailTableConfig.SourceTableName}] AS SRC WITH (NOLOCK) ");
                sb.Append(Environment.NewLine);
                sb.Append($@"WHERE NOT EXISTS (SELECT 1 FROM [{archiveAndTailTableConfig.DestinationDatabaseName}].[{archiveAndTailTableConfig.DestinationSchemaName}].[{archiveAndTailTableConfig.DestinationTableName}] DEST WITH (NOLOCK) ");
                sb.Append(Environment.NewLine);
                sb.Append("                  WHERE ");
                var keyColumnList = archiveAndTailTableConfig.DestinationKey.Split(',').ToList();
                var i = 0;
                foreach (var keyColumn in keyColumnList)
                {
                    i++;
                    sb.Append($@"DEST.[{keyColumn}] = SRC.[{keyColumn}]");
                    sb.Append(Environment.NewLine);
                    if (i < keyColumnList.Count) sb.Append("AND ");
                }
                sb.Append(") ");
                str = sb.ToString();
                //Logger.Info($@"str = {str}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {e.Message}: {e.StackTrace}");
                str = "";
            }
            return str;
        }

        //public static long GetRowsRemaining(SqlConnection cnSource, ArchiveTableConfig archiveTableConfig)
        //{
        //    var startTime = DateTime.Now.ToLocalTime();
        //    Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
        //    long retVal;
        //    try
        //    {
        //        var cmd = new SqlCommand
        //        {
        //            Connection = cnSource,
        //            CommandType = CommandType.Text,
        //            CommandTimeout = 0,
        //            CommandText = BuildRowsRemainingString(archiveTableConfig)
        //        };
        //        Logger.Info(Logging.DbCmdString(cmd));
        //        retVal = Convert.ToInt64(cmd.ExecuteScalar());
        //        Logger.Info("retVal: " + retVal);
        //    }
        //    catch (Exception e)
        //    {
        //        Logger.Error("ERROR: " + cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
        //        throw new ApplicationException(cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
        //    }
        //    finally
        //    {
        //        Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
        //    }
        //    return retVal;
        //}

        //private static String BuildRowsRemainingString(ArchiveTableConfig archiveTableConfig)
        //{
        //    string str;
        //    try
        //    {
        //        StringBuilder sb = new StringBuilder();
        //        sb.Append($@"SELECT COUNT(*) ");
        //        sb.Append(Environment.NewLine);
        //        sb.Append($@"FROM [{archiveTableConfig.SourceDatabaseName}].[{archiveTableConfig.SourceSchemaName}].[{archiveTableConfig.SourceTableName}] AS SRC WITH (NOLOCK) ");
        //        sb.Append(Environment.NewLine);
        //        sb.Append($@"WHERE NOT EXISTS (SELECT 1 FROM [{archiveTableConfig.DestinationDatabaseName}].[{archiveTableConfig.DestinationSchemaName}].[{archiveTableConfig.DestinationTableName}] DEST WITH (NOLOCK) ");
        //        sb.Append(Environment.NewLine);
        //        sb.Append("                  WHERE ");
        //        var keyColumnList = archiveTableConfig.DestinationKey.Split(',').ToList();
        //        var i = 0;
        //        foreach (var keyColumn in keyColumnList)
        //        {
        //            i++;
        //            sb.Append($@"DEST.[{keyColumn}] = SRC.[{keyColumn}]");
        //            sb.Append(Environment.NewLine);
        //            if (i < keyColumnList.Count) sb.Append("AND ");
        //        }
        //        sb.Append($@"                AND DEST.{archiveTableConfig.DateColumn} = SRC.{archiveTableConfig.DateColumn} ");
        //        sb.Append(") ");
        //        str = sb.ToString();
        //        //Logger.Info($@"str = {str}");
        //    }
        //    catch (System.Exception e)
        //    {
        //        Logger.Error($"ERROR: {e.Message}: {e.StackTrace}");
        //        str = "";
        //    }
        //    return str;
        //}

        public static long GetRowsRemaining(SqlConnection cnSource, ArchiveAndTailTableConfig archiveAndTailTableConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            long retVal;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnSource,
                    CommandType = CommandType.Text,
                    CommandTimeout = 0,
                    CommandText = BuildRowsRemainingString(archiveAndTailTableConfig)
                };
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt64(cmd.ExecuteScalar());
                Logger.Info("retVal: " + retVal);
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return retVal;
        }

        private static String BuildRowsRemainingString(ArchiveAndTailTableConfig archiveAndTailTableConfig)
        {
            string str;
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($@"SELECT COUNT(*) ");
                sb.Append(Environment.NewLine);
                sb.Append($@"FROM [{archiveAndTailTableConfig.SourceDatabaseName}].[{archiveAndTailTableConfig.SourceSchemaName}].[{archiveAndTailTableConfig.SourceTableName}] AS SRC WITH (NOLOCK) ");
                sb.Append(Environment.NewLine);
                sb.Append($@"WHERE NOT EXISTS (SELECT 1 FROM [{archiveAndTailTableConfig.DestinationDatabaseName}].[{archiveAndTailTableConfig.DestinationSchemaName}].[{archiveAndTailTableConfig.DestinationTableName}] DEST WITH (NOLOCK) ");
                sb.Append(Environment.NewLine);
                sb.Append("                  WHERE ");
                var keyColumnList = archiveAndTailTableConfig.DestinationKey.Split(',').ToList();
                var i = 0;
                foreach (var keyColumn in keyColumnList)
                {
                    i++;
                    sb.Append($@"DEST.[{keyColumn}] = SRC.[{keyColumn}]");
                    sb.Append(Environment.NewLine);
                    if (i < keyColumnList.Count) sb.Append("AND ");
                }
                sb.Append($@"                AND DEST.{archiveAndTailTableConfig.DateColumn} = SRC.{archiveAndTailTableConfig.DateColumn} ");
                sb.Append(") ");
                str = sb.ToString();
                //Logger.Info($@"str = {str}");
            }
            catch (System.Exception e)
            {
                Logger.Error($"ERROR: {e.Message}: {e.StackTrace}");
                str = "";
            }
            return str;
        }

        public static DateTime GetMaxDestinationDate(SqlConnection cnDestination, ArchiveAndTailTableConfig ArchiveAndTailTableConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            DateTime retVal;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnDestination,
                    CommandType = CommandType.Text,
                    CommandTimeout = 0,
                    CommandText = $@"
                        SELECT ISNULL(MAX([{ArchiveAndTailTableConfig.DateColumn}]), '1900-01-01')  
                        FROM [{ArchiveAndTailTableConfig.DestinationSchemaName}].[{ArchiveAndTailTableConfig.DestinationTableName}] WITH (NOLOCK) 
                    "
                };
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToDateTime(cmd.ExecuteScalar());
                Logger.Info("ROWS: " + retVal);
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return retVal;
        }

        //public static DateTime GetMinSourceDate(SqlConnection cnSource, ArchiveTableConfig mergeTableConfig)
        //{
        //    var startTime = DateTime.Now.ToLocalTime();
        //    Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
        //    DateTime retVal;
        //    try
        //    {
        //        var cmd = new SqlCommand
        //        {
        //            Connection = cnSource,
        //            CommandType = CommandType.Text,
        //            CommandTimeout = 0,
        //            CommandText = $@"
        //                SELECT ISNULL(MIN([{mergeTableConfig.DateColumn}]), '1900-01-01')  
        //                FROM [{mergeTableConfig.SourceSchemaName}].[{mergeTableConfig.SourceTableName}] WITH (NOLOCK) 
        //            "
        //        };
        //        Logger.Info(Logging.DbCmdString(cmd));
        //        retVal = Convert.ToDateTime(cmd.ExecuteScalar());
        //        Logger.Info("ROWS: " + retVal);
        //    }
        //    catch (Exception e)
        //    {
        //        Logger.Error("ERROR: " + cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
        //        throw new ApplicationException(cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
        //    }
        //    finally
        //    {
        //        Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
        //    }
        //    return retVal;
        //}

        public static DateTime GetMinSourceDate(SqlConnection cnSource, ArchiveAndTailTableConfig ArchiveAndTailTableConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            DateTime retVal;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnSource,
                    CommandType = CommandType.Text,
                    CommandTimeout = 0,
                    CommandText = $@"
                        SELECT ISNULL(MIN([{ArchiveAndTailTableConfig.DateColumn}]), '1900-01-01')  
                        FROM [{ArchiveAndTailTableConfig.SourceSchemaName}].[{ArchiveAndTailTableConfig.SourceTableName}] WITH (NOLOCK) 
                    "
                };
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToDateTime(cmd.ExecuteScalar());
                Logger.Info("ROWS: " + retVal);
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return retVal;
        }

        //public static DateTime GetMaxSourceDate(SqlConnection cnSource, ArchiveTableConfig mergeTableConfig)
        //{
        //    var startTime = DateTime.Now.ToLocalTime();
        //    Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
        //    DateTime retVal;
        //    try
        //    {
        //        var cmd = new SqlCommand
        //        {
        //            Connection = cnSource,
        //            CommandType = CommandType.Text,
        //            CommandTimeout = 0,
        //            CommandText = $@"
        //                SELECT ISNULL(MAX([{mergeTableConfig.DateColumn}]), '1900-01-01')  
        //                FROM [{mergeTableConfig.SourceSchemaName}].[{mergeTableConfig.SourceTableName}] WITH (NOLOCK) 
        //            "
        //        };
        //        Logger.Info(Logging.DbCmdString(cmd));
        //        retVal = Convert.ToDateTime(cmd.ExecuteScalar());
        //        Logger.Info("ROWS: " + retVal);
        //    }
        //    catch (Exception e)
        //    {
        //        Logger.Error("ERROR: " + cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
        //        throw new ApplicationException(cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
        //    }
        //    finally
        //    {
        //        Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
        //    }
        //    return retVal;
        //}

        public static DateTime GetMaxSourceDate(SqlConnection cnSource, ArchiveAndTailTableConfig ArchiveAndTailTableConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            DateTime retVal;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnSource,
                    CommandType = CommandType.Text,
                    CommandTimeout = 0,
                    CommandText = $@"
                        SELECT ISNULL(MAX([{ArchiveAndTailTableConfig.DateColumn}]), '1900-01-01')  
                        FROM [{ArchiveAndTailTableConfig.SourceSchemaName}].[{ArchiveAndTailTableConfig.SourceTableName}] WITH (NOLOCK) 
                    "
                };
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToDateTime(cmd.ExecuteScalar());
                Logger.Info("ROWS: " + retVal);
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return retVal;
        }

        //public static Int64 GetMaxDestinationID(SqlConnection cnDestination, ArchiveTableConfig archiveTableConfig)
        //{
        //    var startTime = DateTime.Now.ToLocalTime();
        //    Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
        //    Int64 retVal = 0;
        //    try
        //    {
        //        if (!string.IsNullOrEmpty(archiveTableConfig.IDColumn))
        //        {
        //            var cmd = new SqlCommand
        //            {
        //                Connection = cnDestination,
        //                CommandType = CommandType.Text,
        //                CommandTimeout = 0,
        //                CommandText = $@"
        //                    SELECT ISNULL(MAX([{archiveTableConfig.IDColumn}]), 0)  
        //                    FROM [{archiveTableConfig.DestinationSchemaName}].[{archiveTableConfig.DestinationTableName}] WITH (NOLOCK) 
        //            "
        //            };
        //            Logger.Info(Logging.DbCmdString(cmd));
        //            retVal = Convert.ToInt64(cmd.ExecuteScalar());
        //        }
        //        Logger.Info("ROWS: " + retVal);
        //    }
        //    catch (Exception e)
        //    {
        //        Logger.Error("ERROR: " + cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
        //        throw new ApplicationException(cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
        //    }
        //    finally
        //    {
        //        Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
        //    }
        //    return retVal;
        //}

        public static Int64 GetMaxDestinationID(SqlConnection cnDestination, ArchiveAndTailTableConfig ArchiveAndTailTableConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            Int64 retVal = 0;
            try
            {
                if (!string.IsNullOrEmpty(ArchiveAndTailTableConfig.IDColumn))
                {
                    var cmd = new SqlCommand
                    {
                        Connection = cnDestination,
                        CommandType = CommandType.Text,
                        CommandTimeout = 0,
                        CommandText = $@"
                            SELECT ISNULL(MAX([{ArchiveAndTailTableConfig.IDColumn}]), 0)  
                            FROM [{ArchiveAndTailTableConfig.DestinationSchemaName}].[{ArchiveAndTailTableConfig.DestinationTableName}] WITH (NOLOCK) 
                    "
                    };
                    Logger.Info(Logging.DbCmdString(cmd));
                    retVal = Convert.ToInt64(cmd.ExecuteScalar());
                }
                Logger.Info("ROWS: " + retVal);
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return retVal;
        }

        public static bool CheckDestinationTableStructureMissingColumns(SqlConnection cnDestination, ArchiveTableConfig mergeTableConfig)
        {
            bool retVal = false;
            try
            {
                string str = $@"
                    SELECT  COUNT(*)
                    FROM    ( SELECT    *
                              FROM      {mergeTableConfig.SourceDatabaseName}.information_schema.columns
                              WHERE     [TABLE_SCHEMA] = '{mergeTableConfig.SourceSchemaName}'
                                        AND [TABLE_NAME] = '{mergeTableConfig.SourceTableName}'
                            ) src
                            FULL JOIN ( SELECT  *
                                        FROM    {mergeTableConfig.DestinationDatabaseName}.information_schema.COLUMNS
                                        WHERE   [TABLE_SCHEMA] = '{mergeTableConfig.DestinationSchemaName}'
                                                AND [TABLE_NAME] = '{mergeTableConfig.DestinationTableName}'
                                      ) targ ON targ.[COLUMN_NAME] = src.[COLUMN_NAME]
                    WHERE targ.[DATA_TYPE] IS NULL
                ";
                SqlCommand cmd = new SqlCommand(str, cnDestination);
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        public static bool CheckDestinationTableStructureMissingColumns(SqlConnection cnDestination, ArchiveAndTailTableConfig ArchiveAndTailTableConfig)
        {
            bool retVal = false;
            try
            {
                string str = $@"
                    SELECT  COUNT(*)
                    FROM    ( SELECT    *
                              FROM      {ArchiveAndTailTableConfig.SourceDatabaseName}.information_schema.columns
                              WHERE     [TABLE_SCHEMA] = '{ArchiveAndTailTableConfig.SourceSchemaName}'
                                        AND [TABLE_NAME] = '{ArchiveAndTailTableConfig.SourceTableName}'
                            ) src
                            FULL JOIN ( SELECT  *
                                        FROM    {ArchiveAndTailTableConfig.DestinationDatabaseName}.information_schema.COLUMNS
                                        WHERE   [TABLE_SCHEMA] = '{ArchiveAndTailTableConfig.DestinationSchemaName}'
                                                AND [TABLE_NAME] = '{ArchiveAndTailTableConfig.DestinationTableName}'
                                      ) targ ON targ.[COLUMN_NAME] = src.[COLUMN_NAME]
                    WHERE targ.[DATA_TYPE] IS NULL
                ";
                SqlCommand cmd = new SqlCommand(str, cnDestination);
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        public static bool CheckDestinationTableStructureDifferentColumns(SqlConnection cnSource, ArchiveTableConfig mergeTableConfig)
        {
            bool retVal = false;
            try
            {
                string str = string.Format($@"
                    SELECT  COUNT(*)
                    FROM    ( SELECT    *
                              FROM      {mergeTableConfig.SourceDatabaseName}.information_schema.columns
                              WHERE     [TABLE_SCHEMA] = '{mergeTableConfig.SourceSchemaName}'
                                        AND [TABLE_NAME] = '{mergeTableConfig.SourceTableName}'
                            ) src
                            FULL JOIN ( SELECT  *
                                        FROM    {mergeTableConfig.DestinationDatabaseName}.information_schema.COLUMNS
                                        WHERE   [TABLE_SCHEMA] = '{mergeTableConfig.DestinationSchemaName}'
                                                AND [TABLE_NAME] = '{mergeTableConfig.DestinationTableName}'
                                      ) targ ON targ.[COLUMN_NAME] = src.[COLUMN_NAME]
                    WHERE targ.[DATA_TYPE] <> src.[DATA_TYPE]
					OR targ.[CHARACTER_MAXIMUM_LENGTH] < src.[CHARACTER_MAXIMUM_LENGTH]
					OR targ.[NUMERIC_PRECISION] < src.[NUMERIC_PRECISION]
					OR targ.[NUMERIC_SCALE] < src.[NUMERIC_SCALE]
                ");
                SqlCommand cmd = new SqlCommand(str, cnSource);
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt32(cmd.ExecuteScalar()) == 0;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnSource.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        public static bool CheckDestinationTableStructureDifferentColumns(SqlConnection cnSource, ArchiveAndTailTableConfig ArchiveAndTailTableConfig)
        {
            bool retVal = false;
            try
            {
                string str = string.Format($@"
                    SELECT  COUNT(*)
                    FROM    ( SELECT    *
                              FROM      {ArchiveAndTailTableConfig.SourceDatabaseName}.information_schema.columns
                              WHERE     [TABLE_SCHEMA] = '{ArchiveAndTailTableConfig.SourceSchemaName}'
                                        AND [TABLE_NAME] = '{ArchiveAndTailTableConfig.SourceTableName}'
                            ) src
                            FULL JOIN ( SELECT  *
                                        FROM    {ArchiveAndTailTableConfig.DestinationDatabaseName}.information_schema.COLUMNS
                                        WHERE   [TABLE_SCHEMA] = '{ArchiveAndTailTableConfig.DestinationSchemaName}'
                                                AND [TABLE_NAME] = '{ArchiveAndTailTableConfig.DestinationTableName}'
                                      ) targ ON targ.[COLUMN_NAME] = src.[COLUMN_NAME]
                    WHERE targ.[DATA_TYPE] <> src.[DATA_TYPE]
					OR targ.[CHARACTER_MAXIMUM_LENGTH] < src.[CHARACTER_MAXIMUM_LENGTH]
					OR targ.[NUMERIC_PRECISION] < src.[NUMERIC_PRECISION]
					OR targ.[NUMERIC_SCALE] < src.[NUMERIC_SCALE]
                ");
                SqlCommand cmd = new SqlCommand(str, cnSource);
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt32(cmd.ExecuteScalar()) == 0;
                Logger.Info($@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnSource.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        public static bool CheckSourceTableExists(SqlConnection cnSource, OldArchiveTableConfig archiveTableConfig)
        {
            bool retVal = false;
            try
            {
                string str = string.Format($@"
                    SELECT 1 
                    FROM sys.tables t
                    INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
                    WHERE s.name = @SchemaName 
                    AND t.name = @TableName
                ");
                SqlCommand cmd = new SqlCommand(str, cnSource);
                cmd.Parameters.Add("@SchemaName", SqlDbType.VarChar).Value = archiveTableConfig.SchemaName;
                cmd.Parameters.Add("@TableName", SqlDbType.VarChar).Value = archiveTableConfig.TableName;
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                Logger.Info(@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnSource.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        public static bool CheckDestinationTableExists(SqlConnection cnDestination, PlantServerConfig plantServerConfig)
        {
            bool retVal = false;
            try
            {
                string str = string.Format($@"
                    SELECT 1 
                    FROM sys.tables t
                    INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
                    WHERE s.name = @SchemaName 
                    AND t.name = @TableName
                ");
                SqlCommand cmd = new SqlCommand(str, cnDestination);
                //cmd.Parameters.Add("@SchemaName", SqlDbType.VarChar).Value = plantServerConfig.SchemaName;
                //cmd.Parameters.Add("@TableName", SqlDbType.VarChar).Value = plantServerConfig.TableName;
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                Logger.Info(@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        public static bool CheckSourceTableExists(SqlConnection cnSource, PlantServerConfig plantServerConfig)
        {
            bool retVal = false;
            try
            {
                string str = string.Format($@"
                    SELECT 1 
                    FROM sys.tables t
                    INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
                    WHERE s.name = @SchemaName 
                    AND t.name = @TableName
                ");
                SqlCommand cmd = new SqlCommand(str, cnSource);
                //cmd.Parameters.Add("@SchemaName", SqlDbType.VarChar).Value = plantServerConfig.SchemaName;
                //cmd.Parameters.Add("@TableName", SqlDbType.VarChar).Value = plantServerConfig.TableName;
                Logger.Info(Logging.DbCmdString(cmd));
                retVal = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                Logger.Info(@"retVal = {retVal}");
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cnSource.ConnectionString}  {e.Message}: {e.StackTrace}");
            }
            return retVal;
        }

        //public static bool CheckSourceTableExists(SqlConnection cnSource, TailTableNoVerifyConfig tailTableConfig)
        //{
        //    bool retVal = false;
        //    try
        //    {
        //        string str = string.Format($@"
        //            SELECT 1 
        //            FROM sys.tables t
        //            INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
        //            WHERE s.name = @SchemaName 
        //            AND t.name = @TableName
        //        ");
        //        SqlCommand cmd = new SqlCommand(str, cnSource);
        //        cmd.Parameters.Add("@SchemaName", SqlDbType.VarChar).Value = tailTableConfig.TargetSchemaName;
        //        cmd.Parameters.Add("@TableName", SqlDbType.VarChar).Value = tailTableConfig.TargetTableName;
        //        Logger.Info(Logging.DbCmdString(cmd));
        //        retVal = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        //        Logger.Info(@"retVal = {retVal}");
        //    }
        //    catch (Exception e)
        //    {
        //        Logger.Error($"ERROR: {cnSource.ConnectionString}  {e.Message}: {e.StackTrace}");
        //    }
        //    return retVal;
        //}

        //public static bool CheckTargetTableExists(SqlConnection cnSource, TruncateLedgerTableConfig truncateTableConfig)
        //{
        //    bool retVal = false;
        //    try
        //    {
        //        string str = string.Format($@"
        //            SELECT 1 
        //            FROM sys.tables t
        //            INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
        //            WHERE s.name = @SchemaName 
        //            AND t.name = @TableName
        //        ");
        //        SqlCommand cmd = new SqlCommand(str, cnSource);
        //        cmd.Parameters.Add("@SchemaName", SqlDbType.VarChar).Value = truncateTableConfig.SourceSchemaName;
        //        cmd.Parameters.Add("@TableName", SqlDbType.VarChar).Value = truncateTableConfig.SourceTableName;
        //        Logger.Info(Logging.DbCmdString(cmd));
        //        retVal = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        //        Logger.Info(@"retVal = {retVal}");
        //    }
        //    catch (Exception e)
        //    {
        //        Logger.Error($"ERROR: {cnSource.ConnectionString}  {e.Message}: {e.StackTrace}");
        //    }
        //    return retVal;
        //}

        //public static long GetRowCount(SqlConnection cn, ArchiveAndTailTableConfig ArchiveAndTailTableConfig, string whereClause = null)
        //{
        //    var startTime = DateTime.Now.ToLocalTime();
        //    Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
        //    long rowCount;
        //    try
        //    {
        //        var cmd = new SqlCommand
        //        {
        //            Connection = cn,
        //            CommandType = CommandType.Text,
        //            CommandTimeout = 0,
        //            CommandText = $@"
        //                SELECT COUNT(*) 
        //                FROM {ArchiveAndTailTableConfig.FullyQualifiedSourceName} WITH (NOLOCK) 
        //                {whereClause}
        //            "
        //        };
        //        Logger.Info(Logging.DbCmdString(cmd));
        //        rowCount = Convert.ToInt64(cmd.ExecuteScalar());
        //        Logger.Info("ROWS: " + rowCount);
        //    }
        //    catch (Exception e)
        //    {
        //        Logger.Error("ERROR: " + cn.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
        //        throw new ApplicationException(cn.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
        //    }
        //    finally
        //    {
        //        Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
        //    }
        //    return rowCount;
        //}


        //public static long GetRowCountSource(SqlConnection cnSource, ArchiveTableConfig archiveTableConfig, string whereClause = null)
        //{
        //    var startTime = DateTime.Now.ToLocalTime();
        //    Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
        //    long rowCount;
        //    try
        //    {
        //        var cmd = new SqlCommand
        //        {
        //            Connection = cnSource,
        //            CommandType = CommandType.Text,
        //            CommandTimeout = 0,
        //            CommandText = $@"
        //                SELECT COUNT(*) 
        //                FROM {archiveTableConfig.FullyQualifiedSourceName} WITH (NOLOCK) 
        //                {whereClause}
        //            "
        //        };
        //        Logger.Info(Logging.DbCmdString(cmd));
        //        rowCount = Convert.ToInt64(cmd.ExecuteScalar());
        //        Logger.Info("ROWS: " + rowCount);
        //    }
        //    catch (Exception e)
        //    {
        //        Logger.Error("ERROR: " + cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
        //        throw new ApplicationException(cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
        //    }
        //    finally
        //    {
        //        Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
        //    }
        //    return rowCount;
        //}

        public static long GetRowCountSource(SqlConnection cnSource, ArchiveAndTailTableConfig archiveAndTailTableConfig, string whereClause = null)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            long rowCount;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnSource,
                    CommandType = CommandType.Text,
                    CommandTimeout = 0,
                    CommandText = $@"
                        SELECT COUNT(*) 
                        FROM {archiveAndTailTableConfig.FullyQualifiedSourceName} WITH (NOLOCK) 
                        {whereClause}
                    "
                };
                Logger.Info(Logging.DbCmdString(cmd));
                rowCount = Convert.ToInt64(cmd.ExecuteScalar());
                Logger.Info("ROWS: " + rowCount);
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return rowCount;
        }

        public static long GetRowCountDestination(SqlConnection cnDestination, ArchiveTableConfig archiveTableConfig, string whereClause = null)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            long rowCount;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnDestination,
                    CommandType = CommandType.Text,
                    CommandTimeout = 0,
                    CommandText = $@"
                        SELECT COUNT(*) 
                        FROM {archiveTableConfig.FullyQualifiedDestinationName} WITH (NOLOCK) 
                        {whereClause}
                    "
                };
                Logger.Info(Logging.DbCmdString(cmd));
                rowCount = Convert.ToInt64(cmd.ExecuteScalar());
                Logger.Info("ROWS: " + rowCount);
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return rowCount;
        }

        public static long GetRowCountDestination(SqlConnection cnDestination, ArchiveAndTailTableConfig ArchiveAndTailTableConfig, string whereClause = null)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            long rowCount;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnDestination,
                    CommandType = CommandType.Text,
                    CommandTimeout = 0,
                    CommandText = $@"
                        SELECT COUNT(*) 
                        FROM {ArchiveAndTailTableConfig.FullyQualifiedDestinationName} WITH (NOLOCK) 
                        {whereClause}
                    "
                };
                Logger.Info(Logging.DbCmdString(cmd));
                rowCount = Convert.ToInt64(cmd.ExecuteScalar());
                Logger.Info("ROWS: " + rowCount);
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return rowCount;
        }

        public static long GetRowCount(SqlConnection cnDestination, OldArchiveTableConfig archiveTableConfig, string whereClause = null)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            long rowCount;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnDestination,
                    CommandType = CommandType.Text,
                    CommandTimeout = 0,
                    CommandText = $@"
                        SELECT COUNT(*) 
                        FROM {archiveTableConfig.FullyQualifiedName} WITH (NOLOCK) 
                        {whereClause}
                    "
                };
                Logger.Info(Logging.DbCmdString(cmd));
                rowCount = Convert.ToInt64(cmd.ExecuteScalar());
                Logger.Info("ROWS: " + rowCount);
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return rowCount;
        }

        //public static long GetRowCount(SqlConnection cnDestination, TailTableNoVerifyConfig tailTableConfig, string whereClause = null)
        //{
        //    var startTime = DateTime.Now.ToLocalTime();
        //    Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
        //    long rowCount;
        //    try
        //    {
        //        var cmd = new SqlCommand
        //        {
        //            Connection = cnDestination,
        //            CommandType = CommandType.Text,
        //            CommandTimeout = 0,
        //            CommandText = $@"
        //                SELECT COUNT(*) 
        //                FROM {tailTableConfig.FullyQualifiedTargetName} WITH (NOLOCK) 
        //                {whereClause}
        //            "
        //        };
        //        Logger.Info(Logging.DbCmdString(cmd));
        //        rowCount = Convert.ToInt64(cmd.ExecuteScalar());
        //        Logger.Info("ROWS: " + rowCount);
        //    }
        //    catch (Exception e)
        //    {
        //        Logger.Error("ERROR: " + cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
        //        throw new ApplicationException(cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
        //    }
        //    finally
        //    {
        //        Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
        //    }
        //    return rowCount;
        //}

        //public static long GetRowCount(SqlConnection cnDestination, TruncateLedgerTableConfig truncateTableConfig)
        //{
        //    var startTime = DateTime.Now.ToLocalTime();
        //    Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
        //    long rowCount;
        //    try
        //    {
        //        var cmd = new SqlCommand
        //        {
        //            Connection = cnDestination,
        //            CommandType = CommandType.Text,
        //            CommandTimeout = 0,
        //            CommandText = $@"
        //                SELECT COUNT(*) 
        //                FROM {truncateTableConfig.FullyQualifiedSourceName} WITH (NOLOCK) 
        //            "
        //        };
        //        Logger.Info(Logging.DbCmdString(cmd));
        //        rowCount = Convert.ToInt64(cmd.ExecuteScalar());
        //        Logger.Info("ROWS: " + rowCount);
        //    }
        //    catch (Exception e)
        //    {
        //        Logger.Error("ERROR: " + cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
        //        throw new ApplicationException(cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
        //    }
        //    finally
        //    {
        //        Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
        //    }
        //    return rowCount;
        //}

        //public static DateTime GetMinDate(SqlConnection cnDestination, OldArchiveTableConfig archiveTableConfig)
        //{
        //    var startTime = DateTime.Now.ToLocalTime();
        //    Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
        //    DateTime retVal;
        //    try
        //    {
        //        var cmd = new SqlCommand
        //        {
        //            Connection = cnDestination,
        //            CommandType = CommandType.Text,
        //            CommandTimeout = 0,
        //            CommandText = $@"
        //                SELECT ISNULL(MIN({archiveTableConfig.DateColumn}), '1900-01-01')  
        //                FROM {archiveTableConfig.FullyQualifiedName} WITH (NOLOCK) 
        //            "
        //        };
        //        Logger.Info(Logging.DbCmdString(cmd));
        //        retVal = Convert.ToDateTime(cmd.ExecuteScalar());
        //        Logger.Info("ROWS: " + retVal);
        //    }
        //    catch (Exception e)
        //    {
        //        Logger.Error("ERROR: " + cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
        //        throw new ApplicationException(cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
        //    }
        //    finally
        //    {
        //        Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
        //    }
        //    return retVal;
        //}

        //public static DateTime GetMinDate(SqlConnection cnDestination, TailTableNoVerifyConfig tailTableConfig)
        //{
        //    var startTime = DateTime.Now.ToLocalTime();
        //    Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
        //    DateTime retVal;
        //    try
        //    {
        //        var cmd = new SqlCommand
        //        {
        //            Connection = cnDestination,
        //            CommandType = CommandType.Text,
        //            CommandTimeout = 0,
        //            CommandText = $@"
        //                SELECT ISNULL(MIN({tailTableConfig.DateColumn}), '1900-01-01')  
        //                FROM {tailTableConfig.FullyQualifiedTargetName} WITH (NOLOCK) 
        //            "
        //        };
        //        Logger.Info(Logging.DbCmdString(cmd));
        //        retVal = Convert.ToDateTime(cmd.ExecuteScalar());
        //        Logger.Info("ROWS: " + retVal);
        //    }
        //    catch (Exception e)
        //    {
        //        Logger.Error("ERROR: " + cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
        //        throw new ApplicationException(cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
        //    }
        //    finally
        //    {
        //        Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
        //    }
        //    return retVal;
        //}

        //public static DateTime GetMaxDate(SqlConnection cnDestination, OldArchiveTableConfig archiveTableConfig)
        //{
        //    var startTime = DateTime.Now.ToLocalTime();
        //    Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
        //    DateTime retVal;
        //    try
        //    {
        //        var cmd = new SqlCommand
        //        {
        //            Connection = cnDestination,
        //            CommandType = CommandType.Text,
        //            CommandTimeout = 0,
        //            CommandText = $@"
        //                SELECT ISNULL(MAX({archiveTableConfig.DateColumn}), '1900-01-01')  
        //                FROM {archiveTableConfig.FullyQualifiedName} WITH (NOLOCK) 
        //            "
        //        };
        //        Logger.Info(Logging.DbCmdString(cmd));
        //        retVal = Convert.ToDateTime(cmd.ExecuteScalar());
        //        Logger.Info("ROWS: " + retVal);
        //    }
        //    catch (Exception e)
        //    {
        //        Logger.Error("ERROR: " + cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
        //        throw new ApplicationException(cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
        //    }
        //    finally
        //    {
        //        Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
        //    }
        //    return retVal;
        //}

        //public static DateTime GetMaxDate(SqlConnection cnDestination, TailTableNoVerifyConfig tailTableConfig)
        //{
        //    var startTime = DateTime.Now.ToLocalTime();
        //    Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
        //    DateTime retVal;
        //    try
        //    {
        //        var cmd = new SqlCommand
        //        {
        //            Connection = cnDestination,
        //            CommandType = CommandType.Text,
        //            CommandTimeout = 0,
        //            CommandText = $@"
        //                SELECT ISNULL(MAX({tailTableConfig.DateColumn}), '1900-01-01')  
        //                FROM {tailTableConfig.FullyQualifiedTargetName} WITH (NOLOCK) 
        //            "
        //        };
        //        Logger.Info(Logging.DbCmdString(cmd));
        //        retVal = Convert.ToDateTime(cmd.ExecuteScalar());
        //        Logger.Info("ROWS: " + retVal);
        //    }
        //    catch (Exception e)
        //    {
        //        Logger.Error("ERROR: " + cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
        //        throw new ApplicationException(cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
        //    }
        //    finally
        //    {
        //        Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
        //    }
        //    return retVal;
        //}

        //public static int GetSFDays(SqlConnection cnDestination, OldArchiveTableConfig archiveTableConfig)
        //{
        //    var startTime = DateTime.Now.ToLocalTime();
        //    Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
        //    int retVal;
        //    try
        //    {
        //        var cmd = new SqlCommand
        //        {
        //            Connection = cnDestination,
        //            CommandType = CommandType.Text,
        //            CommandTimeout = 0,
        //            CommandText = $@"
        //                SELECT ISNULL(DATEDIFF(DAY, MIN({archiveTableConfig.DateColumn}), MAX({archiveTableConfig.DateColumn})), 0) 
        //                FROM {archiveTableConfig.FullyQualifiedName} WITH (NOLOCK) 
        //            "
        //        };
        //        Logger.Info(Logging.DbCmdString(cmd));
        //        retVal = Convert.ToInt32(cmd.ExecuteScalar());
        //        Logger.Info("ROWS: " + retVal);
        //    }
        //    catch (Exception e)
        //    {
        //        Logger.Error("ERROR: " + cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
        //        throw new ApplicationException(cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
        //    }
        //    finally
        //    {
        //        Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
        //    }
        //    return retVal;
        //}

        //public static int GetSFDays(SqlConnection cnDestination, TailTableNoVerifyConfig tailTableConfig)
        //{
        //    var startTime = DateTime.Now.ToLocalTime();
        //    Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
        //    int retVal;
        //    try
        //    {
        //        var cmd = new SqlCommand
        //        {
        //            Connection = cnDestination,
        //            CommandType = CommandType.Text,
        //            CommandTimeout = 0,
        //            CommandText = $@"
        //                SELECT ISNULL(DATEDIFF(DAY, MIN({tailTableConfig.DateColumn}), MAX({tailTableConfig.DateColumn})), 0) 
        //                FROM {tailTableConfig.FullyQualifiedTargetName} WITH (NOLOCK) 
        //            "
        //        };
        //        Logger.Info(Logging.DbCmdString(cmd));
        //        retVal = Convert.ToInt32(cmd.ExecuteScalar());
        //        Logger.Info("ROWS: " + retVal);
        //    }
        //    catch (Exception e)
        //    {
        //        Logger.Error("ERROR: " + cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
        //        throw new ApplicationException(cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
        //    }
        //    finally
        //    {
        //        Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
        //    }
        //    return retVal;
        //}

        public static bool CheckDatabaseExists(SqlConnection cnDestination, Table DestinationTable)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            var isExist = false;
            var rowCount = 0;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnDestination,
                    CommandType = CommandType.Text,
                    CommandText = $@"
                       SELECT [name]
                        FROM  sys.databases
                        WHERE [name] = '{DestinationTable.DatabaseName}' 
                    "
                };
                Logger.Trace(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        isExist = true;
                        rowCount += 1;
                    }

                    reader.Close();
                    Logger.Trace("ROWS: " + rowCount);
                }
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }

            return isExist;
        }

        public static bool CheckSchemaExists(SqlConnection cnDestination, Table DestinationTable)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            var isExist = false;
            var rowCount = 0;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnDestination,
                    CommandType = CommandType.Text,
                    CommandText = $@"
                       SELECT [TABLE_SCHEMA]
                        FROM  [{DestinationTable.DatabaseName}].[INFORMATION_SCHEMA].[TABLES]
                        WHERE [TABLE_CATALOG] = '{DestinationTable.DatabaseName}' AND
                              [TABLE_SCHEMA] = '{DestinationTable.SchemaName}' 
                    "
                };
                Logger.Trace(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        isExist = true;
                        rowCount += 1;
                    }

                    reader.Close();
                    Logger.Trace("ROWS: " + rowCount);
                }
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }

            return isExist;
        }

        public static bool CheckTableExists(SqlConnection cnDestination, Table DestinationTable)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            var isExist = false;
            var rowCount = 0;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnDestination,
                    CommandType = CommandType.Text,
                    CommandText = $@"
                       SELECT [TABLE_NAME]
                        FROM  [{DestinationTable.DatabaseName}].[INFORMATION_SCHEMA].[TABLES]
                        WHERE [TABLE_CATALOG] = '{DestinationTable.DatabaseName}' AND
                              [TABLE_SCHEMA] = '{DestinationTable.SchemaName}' AND 
                              [TABLE_NAME] = '{DestinationTable.TableName}' 
                    "
                };
                Logger.Trace(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        isExist = true;
                        rowCount += 1;
                    }

                    reader.Close();
                    Logger.Trace("ROWS: " + rowCount);
                }
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }

            return isExist;
        }

        public static long CountTableRows(SqlConnection cnDestination, Table DestinationTable)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            long rowCount;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnDestination,
                    CommandType = CommandType.Text,
                    CommandTimeout = 0,
                    CommandText = $@"
                        SELECT  COUNT(*)
                        FROM    [{DestinationTable.SchemaName}].[{DestinationTable.TableName}] WITH (NOLOCK) 
                    "
                };
                Logger.Trace(Logging.DbCmdString(cmd));
                rowCount = (long)cmd.ExecuteScalar();
                Logger.Trace($@"ROWS: {rowCount}");
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return rowCount;
        }

        public static bool IsSourceKeyUnique(SqlConnection cnSource, ArchiveTableConfig mergeTableConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            bool retVal = false;
            var rowCount = 0;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnSource,
                    CommandType = CommandType.Text,
                    CommandTimeout = 0,
                    CommandText = $@"
                        SELECT  [{mergeTableConfig.SourceKey.Replace(",", "], [")}], COUNT(*)
                        FROM    [{mergeTableConfig.SourceSchemaName}].[{mergeTableConfig.SourceTableName}] WITH (NOLOCK) 
                        GROUP BY [{mergeTableConfig.SourceKey.Replace(",", "], [")}]
                        HAVING COUNT(*) > 1
                    "
                };
                Logger.Trace(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        rowCount += 1;
                    }

                    reader.Close();
                    Logger.Trace("ROWS: " + rowCount);
                }
                if (rowCount == 0) retVal = true;
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return retVal;
        }

        public static bool IsSourceKeyUnique(SqlConnection cnSource, ArchiveAndTailTableConfig ArchiveAndTailTableConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            bool retVal = false;
            var rowCount = 0;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnSource,
                    CommandType = CommandType.Text,
                    CommandTimeout = 0,
                    CommandText = $@"
                        SELECT  [{ArchiveAndTailTableConfig.SourceKey.Replace(",", "], [")}], COUNT(*)
                        FROM    [{ArchiveAndTailTableConfig.SourceSchemaName}].[{ArchiveAndTailTableConfig.SourceTableName}] WITH (NOLOCK) 
                        GROUP BY [{ArchiveAndTailTableConfig.SourceKey.Replace(",", "], [")}]
                        HAVING COUNT(*) > 1
                    "
                };
                Logger.Trace(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        rowCount += 1;
                    }

                    reader.Close();
                    Logger.Trace("ROWS: " + rowCount);
                }
                if (rowCount == 0) retVal = true;
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return retVal;
        }

        //public static bool IsSourceKeyUnique(SqlConnection cnSource, TailTableNoVerifyConfig tailTableConfig)
        //{
        //    var startTime = DateTime.Now.ToLocalTime();
        //    Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
        //    bool retVal = false;
        //    var rowCount = 0;
        //    try
        //    {
        //        var cmd = new SqlCommand
        //        {
        //            Connection = cnSource,
        //            CommandType = CommandType.Text,
        //            CommandTimeout = 0,
        //            CommandText = $@"
        //                SELECT  [{tailTableConfig.SourceKey.Replace(",", "], [")}], COUNT(*)
        //                FROM    [{tailTableConfig.TargetSchemaName}].[{tailTableConfig.TargetTableName}] WITH (NOLOCK) 
        //                GROUP BY [{tailTableConfig.SourceKey.Replace(",", "], [")}]
        //                HAVING COUNT(*) > 1
        //            "
        //        };
        //        Logger.Trace(Logging.DbCmdString(cmd));
        //        using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
        //        {
        //            while (reader.Read())
        //            {
        //                rowCount += 1;
        //            }

        //            reader.Close();
        //            Logger.Trace("ROWS: " + rowCount);
        //        }
        //        if (rowCount == 0) retVal = true;
        //    }
        //    catch (Exception e)
        //    {
        //        Logger.Error("ERROR: " + cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
        //        throw new ApplicationException(cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
        //    }
        //    finally
        //    {
        //        Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
        //    }
        //    return retVal;
        //}

        public static bool IsDestinationKeyUnique(SqlConnection cnDestination, ArchiveTableConfig mergeTableConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            bool retVal = false;
            var rowCount = 0;
            try
            {
                if (cnDestination.State != ConnectionState.Open) cnDestination.Open();
                var cmd = new SqlCommand
                {
                    Connection = cnDestination,
                    CommandType = CommandType.Text,
                    CommandTimeout = 0,
                    CommandText = $@"
                        SELECT  [{mergeTableConfig.DestinationKey.Replace(",", "], [")}], COUNT(*)
                        FROM    [{mergeTableConfig.DestinationSchemaName}].[{mergeTableConfig.DestinationTableName}] WITH (NOLOCK) 
                        GROUP BY [{mergeTableConfig.DestinationKey.Replace(",", "], [")}]
                        HAVING COUNT(*) > 1
                    "
                };
                Logger.Trace(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        rowCount += 1;
                    }

                    reader.Close();
                    Logger.Trace("ROWS: " + rowCount);
                }
                if (rowCount == 0) retVal = true;
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return retVal;
        }

        public static bool IsDestinationKeyUnique(SqlConnection cnSource, ArchiveAndTailTableConfig ArchiveAndTailTableConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            bool retVal = false;
            var rowCount = 0;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnSource,
                    CommandType = CommandType.Text,
                    CommandTimeout = 0,
                    CommandText = $@"
                        SELECT  [{ArchiveAndTailTableConfig.DestinationKey.Replace(",", "], [")}], COUNT(*)
                        FROM    [{ArchiveAndTailTableConfig.SourceSchemaName}].[{ArchiveAndTailTableConfig.SourceTableName}] WITH (NOLOCK) 
                        GROUP BY [{ArchiveAndTailTableConfig.DestinationKey.Replace(",", "], [")}]
                        HAVING COUNT(*) > 1
                    "
                };
                Logger.Trace(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        rowCount += 1;
                    }

                    reader.Close();
                    Logger.Trace("ROWS: " + rowCount);
                }
                if (rowCount == 0) retVal = true;
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cnSource.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return retVal;
        }

        public static bool IsKeyColumnUnique(SqlConnection cnDestination, OldArchiveTableConfig archiveTableConfig)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
            bool retVal = false;
            var rowCount = 0;
            try
            {
                var cmd = new SqlCommand
                {
                    Connection = cnDestination,
                    CommandType = CommandType.Text,
                    CommandTimeout = 0,
                    CommandText = $@"
                        SELECT  [{archiveTableConfig.KeyColumn.Replace(",", "], [")}], COUNT(*)
                        FROM    [{archiveTableConfig.SchemaName}].[{archiveTableConfig.TableName}] WITH (NOLOCK) 
                        GROUP BY [{archiveTableConfig.KeyColumn.Replace(",", "], [")}]
                        HAVING COUNT(*) > 1
                    "
                };
                Logger.Trace(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        rowCount ++;
                    }

                    reader.Close();
                    Logger.Trace("ROWS: " + rowCount);
                }
                if (rowCount == 0) retVal = true;
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            finally
            {
                Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
            }
            return retVal;
        }

        //public static bool IsKeyColumnUnique(SqlConnection cnDestination, TailTableNoVerifyConfig tailTableConfig)
        //{
        //    var startTime = DateTime.Now.ToLocalTime();
        //    Logger.Info($"BEGIN: {DateTime.Now.ToLocalTime()}");
        //    bool retVal = false;
        //    var rowCount = 0;
        //    try
        //    {
        //        var cmd = new SqlCommand
        //        {
        //            Connection = cnDestination,
        //            CommandType = CommandType.Text,
        //            CommandTimeout = 0,
        //            CommandText = $@"
        //                SELECT  [{tailTableConfig.KeyColumn.Replace(",", "], [")}], COUNT(*)
        //                FROM    [{tailTableConfig.TargetSchemaName}].[{tailTableConfig.TargetTableName}] WITH (NOLOCK) 
        //                GROUP BY [{tailTableConfig.KeyColumn.Replace(",", "], [")}]
        //                HAVING COUNT(*) > 1
        //            "
        //        };
        //        Logger.Trace(Logging.DbCmdString(cmd));
        //        using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
        //        {
        //            while (reader.Read())
        //            {
        //                rowCount += 1;
        //            }

        //            reader.Close();
        //            Logger.Trace("ROWS: " + rowCount);
        //        }
        //        if (rowCount == 0) retVal = true;
        //    }
        //    catch (Exception e)
        //    {
        //        Logger.Error("ERROR: " + cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
        //        throw new ApplicationException(cnDestination.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
        //    }
        //    finally
        //    {
        //        Logger.Info("END: " + Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds) + " SECONDS");
        //    }
        //    return retVal;
        //}

        //public static bool CreateDestinationLinkedServer(SqlConnection cnDestination, SqlConnection cnArchRepoMaster, OldArchiveTableConfig archiveTableConfig)
        //{
        //    bool retVal = false;
        //    try
        //    {
        //        string str = $@"EXEC sp_addlinkedserver @server=N'{archiveTableConfig.ServerName}'";
        //        SqlCommand cmd = new SqlCommand(str, cnDestination);
        //        cmd.Parameters.Add("@SchemaName", SqlDbType.VarChar).Value = cnArchRepoMaster.DataSource;
        //        Logger.Info(Logging.DbCmdString(cmd));
        //        cmd.ExecuteNonQuery();
        //        retVal = true;
        //        Logger.Info(@"retVal = {retVal}");
        //    }
        //    catch (System.Exception e)
        //    {
        //        Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
        //    }
        //    return retVal;
        //}

    //    public static bool CheckDestinationLinkedServerExists(SqlConnection cnDestination, SqlConnection cnArchRepoMaster, OldArchiveTableConfig archiveTableConfig)
    //    {
    //        bool retVal = false;
    //        try
    //        {
    //            string str = string.Format($@"SELECT 1 FROM sys.servers WHERE Name = @RepoServerName");
    //            SqlCommand cmd = new SqlCommand(str, cnDestination);
    //            cmd.Parameters.Add("@RepoServerName", SqlDbType.VarChar).Value = cnArchRepoMaster.DataSource;
    //            Logger.Info(Logging.DbCmdString(cmd));
    //            retVal = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    //            Logger.Info(@"retVal = {retVal}");
    //        }
    //        catch (Exception e)
    //        {
    //            Logger.Error($"ERROR: {cnDestination.ConnectionString}  {e.Message}: {e.StackTrace}");
    //        }
    //        return retVal;
    //    }

    }
}
