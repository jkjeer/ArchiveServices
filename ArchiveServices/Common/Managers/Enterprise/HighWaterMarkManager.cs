using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using ArchiveServices.Common.Types.Enterprise;
using ArchiveServices.Common.Types.Response;
//using ArchiveServices.Common.Types.ShopFloor;
using ArchiveServices.Common.Utilities;
using NLog;

namespace ArchiveServices.Common.Managers.Enterprise
{
    public class HighWaterMarkManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private static SqlCommand GetHighWaterMarks()
        {
            var cmd = new SqlCommand
            {
                CommandType = CommandType.Text,
                CommandText = @"
                    SELECT
                        MarkId,
                        MarkName ,
                        EnterpriseDateTime ,
                        LocalDateTime ,
                        LookbackSeconds ,
                        UniqueID ,
                        WrittenDate
                    FROM 
                        EBS.HighWaterMark
                "
            };
            return cmd;
        }

        public static HighWaterMarks GetHighWaterMarks(SqlConnection cn)
        {
            var marks = new HighWaterMarks();
            try
            {
                var cmd = GetHighWaterMarks();
                cmd.Connection = cn;
                Logger.Trace(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        var mark = new HighWaterMark();
                        mark.HighWaterMarkID = CastDbNull.To(reader["MarkId"], 0);
                        mark.HighWaterMarkName = CastDbNull.To(reader["MarkName"], string.Empty).Trim();
                        mark.EnterpriseDateTime = CastDbNull.To(reader["EnterpriseDateTime"], DateTime.MinValue);
                        mark.LocalDateTime = CastDbNull.To(reader["LocalDateTime"], DateTime.MinValue);
                        mark.LookbackSeconds = CastDbNull.To(reader["LookbackSeconds"], 0);
                        mark.UniqueID = CastDbNull.To<long>(reader["UniqueID"], 0);
                        mark.EnterpriseTimeSpan = (DateTime.Now - (mark.EnterpriseDateTime.AddSeconds(-CastDbNull.To(reader["LookbackSeconds"], 0))));
                        mark.LocalTimeSpan = (DateTime.Now - (mark.LocalDateTime.AddSeconds(-CastDbNull.To(reader["LookbackSeconds"], 0))));
                        mark.Exists = true;
                        marks.Add(mark);
                    }

                    reader.Close();
                }
                Logger.Trace($"Rows: {marks.Count}");

                if (!marks.Any())
                {
                    throw new Exception($"High Water Marks NOT found");
                }
            }
            catch (Exception e)
            {
                Logger.Error($@"ERROR: {cn.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new Exception($"{cn.ConnectionString}  {e.Message}");
            }
            return marks;
        }

        private static SqlCommand GetHighWaterMark()
        {
            var cmd = new SqlCommand
            {
                CommandType = CommandType.Text,
                CommandText = @"
                    SELECT
                        MarkId,
                        MarkName ,
                        EnterpriseDateTime ,
                        LocalDateTime ,
                        LookbackSeconds ,
                        UniqueID ,
                        WrittenDate
                    FROM 
                        EBS.HighWaterMark
                    WHERE 
                        MarkName = @MarkName 
                        AND PlantCode = @PlantCode
                "
            };
            cmd.Parameters.Add(new SqlParameter("@MarkName", SqlDbType.VarChar, 30));
            cmd.Parameters.Add(new SqlParameter("@PlantCode", SqlDbType.VarChar, 12));
            return cmd;
        }

        public static HighWaterMark GetHighWaterMark(SqlConnection cn, Plant plant, string markName)
        {
            var mark = new HighWaterMark(markName, plant.PlantNum);
            try
            {
                var cmd = GetHighWaterMark();
                cmd.Parameters["@MarkName"].Value = markName;
                cmd.Parameters["@PlantCode"].Value = plant.PlantCode;
                cmd.Connection = cn;
                Logger.Trace(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleRow))
                {
                    while (reader.Read())
                    {
                        mark.HighWaterMarkID = CastDbNull.To(reader["MarkId"], 0);
                        mark.HighWaterMarkName = CastDbNull.To(reader["MarkName"], string.Empty).Trim();
                        mark.EnterpriseDateTime = CastDbNull.To(reader["EnterpriseDateTime"], DateTime.MinValue);
                        mark.LocalDateTime = CastDbNull.To(reader["LocalDateTime"], DateTime.MinValue);
                        mark.LookbackSeconds = CastDbNull.To(reader["LookbackSeconds"], 0);
                        mark.UniqueID = CastDbNull.To<long>(reader["UniqueID"], 0);
                        mark.EnterpriseTimeSpan = (DateTime.Now - (mark.EnterpriseDateTime.AddSeconds(-CastDbNull.To(reader["LookbackSeconds"], 0))));
                        mark.LocalTimeSpan = (DateTime.Now - (mark.LocalDateTime.AddSeconds(-CastDbNull.To(reader["LookbackSeconds"], 0))));
                        mark.Exists = true;
                    }

                    reader.Close();
                }

                if (!mark.Exists)
                {
                    //throw new Exception($"High Water Mark [{markName}] does NOT exist at Plant {plant.PlantNum} {plant.PlantCode}");
                    mark.HighWaterMarkID = 0;
                    mark.HighWaterMarkName = markName;
                    mark.EnterpriseDateTime = new DateTime(1900, 1, 1);
                    mark.LocalDateTime = new DateTime(1900, 1, 1);
                    mark.LookbackSeconds = 0;
                    mark.UniqueID = 0;
                    mark.EnterpriseTimeSpan = DateTime.Now - mark.EnterpriseDateTime.AddSeconds(0);
                    mark.LocalTimeSpan = DateTime.Now - mark.LocalDateTime.AddSeconds(0);
                    mark.Exists = true;
                }
            }
            catch (Exception e)
            {
                Logger.Error($@"ERROR: {cn.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new Exception($"{cn.ConnectionString}  {e.Message}");
            }
            return mark;
        }

        private static SqlCommand SaveHighWaterMarkDate()
        {
            var cmd = new SqlCommand
            {
                CommandType = CommandType.Text,
                CommandText = @"
                    UPDATE 
                        EBS.HighWaterMark
                    SET
                        EnterpriseDateTime = @EnterpriseDateTime,
                        LocalDateTime = @LocalDateTime,
                        WrittenDate = GETDATE()                     
                    WHERE 
                        MarkName = @MarkName 
                        AND PlantCode = @PlantCode
                "
            };
            cmd.Parameters.Add(new SqlParameter("@EnterpriseDateTime", SqlDbType.DateTime));
            cmd.Parameters.Add(new SqlParameter("@LocalDateTime", SqlDbType.DateTime));
            cmd.Parameters.Add(new SqlParameter("@MarkName", SqlDbType.VarChar, 30));
            cmd.Parameters.Add(new SqlParameter("@PlantCode", SqlDbType.VarChar, 12));
            return cmd;
        }

        public static SqlDmlResponse SaveHighWaterMarkDate(SqlConnection cn, Plant plant, HighWaterMark mark, DateTime enterpriseDateTime)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Trace($"BEGIN: {DateTime.Now.ToLocalTime()}");
            var response = new SqlDmlResponse();
            try
            {
                if (enterpriseDateTime < mark.EnterpriseDateTime)
                {
                    Logger.Error($@"ERROR: High Water Mark Save Error ... {plant.PlantCode} {mark.HighWaterMarkName} Attempt to Reset Date to Older... Current {mark.EnterpriseDateTime} New {enterpriseDateTime}");
                    throw new Exception(
                        $"High Water Mark Save Error ... {plant.PlantCode} {mark.HighWaterMarkName} Attempt to Reset Date to Older... Current {mark.EnterpriseDateTime} New {enterpriseDateTime}");
                }
                //if (enterpriseDateTime > DateTime.Now.AddMinutes(1))
                //{
                //    Logger.Error($@"ERROR: High Water Mark Save Error ... {plant.PlantCode} {mark.HighWaterMarkName} Attempt to Reset Date to Future... Current {DateTime.Now} New {enterpriseDateTime}");
                //    throw new Exception(
                //        $"High Water Mark Save Error ... {plant.PlantCode} {mark.HighWaterMarkName} Attempt to Reset Date to Future... Current {DateTime.Now} New {enterpriseDateTime}");
                //}

                if (enterpriseDateTime > DateTime.Now) enterpriseDateTime = DateTime.Now;
                var localDateTime = enterpriseDateTime.AddHours(plant.TimeOffset);
                var cmd = SaveHighWaterMarkDate();
                cmd.Parameters["@EnterpriseDateTime"].Value = enterpriseDateTime;
                cmd.Parameters["@LocalDateTime"].Value = localDateTime;
                cmd.Parameters["@MarkName"].Value = mark.HighWaterMarkName;
                cmd.Parameters["@PlantCode"].Value = plant.PlantCode;
                cmd.Connection = cn;
                response.Init(cmd);
                Logger.Trace(Logging.DbCmdString(cmd));
                var rows = cmd.ExecuteNonQuery();
                response.RowsAffected = rows;
                Logger.Trace($@"ROWS: {rows}");
            }
            catch (Exception e)
            {
                response.Errors.Add(new Error($"{cn.ConnectionString}  {e.Message}"));
                Logger.Error($@"ERROR: {cn.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new Exception($"{cn.ConnectionString}  {e.Message}");
            }
            finally
            {
                Logger.Trace($"END: {Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds)} SECONDS");
            }
            return response;
        }

        public static HighWaterMark GetHighWaterMark(Plant plant, string markName)
        {
            var mark = new HighWaterMark(markName, plant.PlantNum);
            var cn = new SqlConnection();
            try
            {
                cn = ConnectionManager.GetSqlConnectionFromKey(plant.PlantCode + ".INTEGRATION");
                cn.Open();
                var cmd = GetHighWaterMark();
                cmd.Parameters["@MarkName"].Value = markName;
                cmd.Connection = cn;
                Logger.Trace(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleRow))
                {
                    while (reader.Read())
                    {
                        mark.HighWaterMarkID = CastDbNull.To(reader["MarkId"], 0);
                        mark.HighWaterMarkName = CastDbNull.To(reader["MarkName"], string.Empty).Trim();
                        mark.EnterpriseDateTime = CastDbNull.To(reader["EnterpriseDateTime"], DateTime.MinValue);
                        mark.LocalDateTime = CastDbNull.To(reader["LocalDateTime"], DateTime.MinValue);
                        mark.LookbackSeconds = CastDbNull.To(reader["LookbackSeconds"], 0);
                        mark.UniqueID = CastDbNull.To<long>(reader["UniqueID"], 0);
                        mark.EnterpriseTimeSpan = (DateTime.Now - (mark.EnterpriseDateTime.AddSeconds(-CastDbNull.To(reader["LookbackSeconds"], 0))));
                        mark.LocalTimeSpan = (DateTime.Now - (mark.LocalDateTime.AddSeconds(-CastDbNull.To(reader["LookbackSeconds"], 0))));
                        mark.Exists = true;
                    }

                    reader.Close();
                }

                if (!mark.Exists)
                {
                    throw new Exception($"High Water Mark [{markName}] does NOT exist");
                }
            }
            catch (Exception e)
            {
                Logger.Error($@"ERROR: {cn.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new Exception($"{cn.ConnectionString}  {e.Message}");
            }
            finally
            {
                cn.Close();
            }
            return mark;
        }

        private static SqlCommand UpdateHighWaterMark()
        {
            //UPDATE 
            //    EBS.HighWaterMark
            //SET
            //    EnterpriseDateTime = @EnterpriseDateTime,
            //    LocalDateTime = @LocalDateTime,
            //    WrittenDate = GETDATE(),
            //    UniqueID = @UniqueID                        
            //WHERE 
            //    MarkName = @MarkName
            var cmd = new SqlCommand
            {
                CommandType = CommandType.Text,
                CommandText = @"
                    MERGE EBS.HighWaterMark AS PY
                      USING 
                        ( SELECT
                            @MarkName AS MarkName,
                            @EnterpriseDateTime AS EnterpriseDateTime,
		                    @LocalDateTime AS LocalDateTime,
		                    @UniqueID AS UniqueID
                       ) AS MK
                    ON 
	                    PY.MarkName = MK.MarkName
                      WHEN MATCHED 
                        THEN UPDATE
                         SET
                            PY.EnterpriseDateTime = MK.EnterpriseDateTime,
		                    PY.LocalDateTime = MK.LocalDateTime,
		                    PY.UniqueID = MK.UniqueID,
		                    PY.WrittenDate = GETDATE()
                      WHEN NOT MATCHED 
                        THEN INSERT
                          (
                            MarkName,
                            EnterpriseDateTime,
		                    LocalDateTime,
                            LookbackSeconds,
		                    UniqueID,
		                    WrittenDate
                          )
                         VALUES
                          ( 
                            MK.MarkName,
		                    MK.EnterpriseDateTime,
		                    MK.LocalDateTime,
		                    120,
		                    MK.UniqueID,
		                    Getdate()
                          );

                "
            };
            cmd.Parameters.Add(new SqlParameter("@MarkName", SqlDbType.VarChar, 30));
            cmd.Parameters.Add(new SqlParameter("@EnterpriseDateTime", SqlDbType.DateTime));
            cmd.Parameters.Add(new SqlParameter("@LocalDateTime", SqlDbType.DateTime));
            cmd.Parameters.Add(new SqlParameter("@UniqueID", SqlDbType.Int));
            return cmd;
        }

        private static SqlCommand InsertHighWaterMark()
        {
            var cmd = new SqlCommand
            {
                CommandType = CommandType.Text,
                CommandText = @"
                INSERT INTO EBS.HighWaterMark (
                    MarkName,
                    EnterpriseDateTime,
                    LocalDateTime,
                    LookbackSeconds,
                    UniqueID)
			    VALUES ( 
                    @MarkName,
					@EnterpriseDateTime,
					@LocalDateTime,
                    120,
                    @UniqueID)"
            };
            cmd.Parameters.Add(new SqlParameter("@MarkName", SqlDbType.VarChar, 30));
            cmd.Parameters.Add(new SqlParameter("@EnterpriseDateTime", SqlDbType.DateTime));
            cmd.Parameters.Add(new SqlParameter("@LocalDateTime", SqlDbType.DateTime));
            cmd.Parameters.Add(new SqlParameter("@UniqueID", SqlDbType.Int));
            return cmd;
        }

        public static SqlDmlResponse UpdateHighWaterMarks(SqlConnection cn, HighWaterMarks marks)
        {
            var response = new SqlDmlResponse();
            try
            {
                foreach (var mark in marks)
                {
                    var cmd = UpdateHighWaterMark();
                    cmd.Parameters["@MarkName"].Value = mark.HighWaterMarkName;
                    cmd.Parameters["@EnterpriseDateTime"].Value = mark.EnterpriseDateTime;
                    cmd.Parameters["@LocalDateTime"].Value = mark.LocalDateTime;
                    cmd.Parameters["@UniqueID"].Value = mark.UniqueID;
                    cmd.Connection = cn;
                    Logger.Trace(Logging.DbCmdString(cmd));
                    var rows = cmd.ExecuteNonQuery();
                    response.RowsAffected = rows;
                    Logger.Trace($"Rows: {rows}");
                }
            }
            catch (Exception e)
            {
                response.Errors.Add(new Error($"{cn.ConnectionString}  {e.Message}"));
                Logger.Error($@"ERROR: {cn.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new Exception($"{cn.ConnectionString}  {e.Message}");
            }
            return response;
        }

        private static SqlCommand SaveHighWaterMarkId()
        {
            var cmd = new SqlCommand
            {
                CommandType = CommandType.Text,
                CommandText = @"
                    UPDATE 
                        EBS.HighWaterMark
                    SET
                        EnterpriseDateTime = @EnterpriseDateTime,
                        LocalDateTime = @LocalDateTime,
                        WrittenDate = GETDATE(),
                        UniqueID = @UniqueID                        
                    WHERE 
                        MarkName = @MarkName
                        AND PlantCode = @PlantCode
                "
            };
            cmd.Parameters.Add(new SqlParameter("@EnterpriseDateTime", SqlDbType.DateTime));
            cmd.Parameters.Add(new SqlParameter("@LocalDateTime", SqlDbType.DateTime));
            cmd.Parameters.Add(new SqlParameter("@UniqueID", SqlDbType.BigInt));
            cmd.Parameters.Add(new SqlParameter("@MarkName", SqlDbType.VarChar, 30));
            cmd.Parameters.Add(new SqlParameter("@PlantCode", SqlDbType.VarChar, 12));
            return cmd;
        }

        public static SqlDmlResponse SaveHighWaterMarkId(SqlConnection cn, Plant plant, HighWaterMark mark, DateTime enterpriseDateTime, long uniqueId)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Trace($"BEGIN: {DateTime.Now.ToLocalTime()}");
            var response = new SqlDmlResponse();
            try
            {

                if (mark.EnterpriseDateTime > enterpriseDateTime)
                {
                    Logger.Error($@"ERROR: High Water Mark Save Error ... {plant.PlantCode} {mark.HighWaterMarkName} Attempt to Reset Date to Older... Current {mark.EnterpriseDateTime} New {enterpriseDateTime}");
                    throw new Exception(
                        $"High Water Mark Save Error ... {plant.PlantCode} {mark.HighWaterMarkName} Attempt to Reset Date to Older... Current {mark.EnterpriseDateTime} New {enterpriseDateTime}");
                }

                //if (enterpriseDateTime > DateTime.Now)
                //{
                //    Logger.Error($@"ERROR: High Water Mark Save Error ... {plant.PlantCode} {mark.HighWaterMarkName} Attempt to Reset Date to Future... Current {DateTime.Now} New {enterpriseDateTime}");
                //    throw new Exception(
                //        $"High Water Mark Save Error ... {plant.PlantCode} {mark.HighWaterMarkName} Attempt to Reset Date to Future... Current {DateTime.Now} New {enterpriseDateTime}");
                //}

                if (mark.UniqueID > uniqueId)
                {
                    Logger.Error($@"ERROR: High Water Mark Save Error ... {plant.PlantCode} {mark.HighWaterMarkName} Attempt to Reset Id ... Current {mark.UniqueID} New {uniqueId}");
                    throw new Exception(
                        $"High Water Mark Save Error ... {plant.PlantCode} {mark.HighWaterMarkName} Attempt to Reset Id ... Current {mark.UniqueID} New {uniqueId}");
                }

                if (uniqueId == 0)
                {
                    Logger.Error($@"ERROR: High Water Mark Save Error ... {plant.PlantCode} {mark.HighWaterMarkName} Attempt to Reset Id to 0");
                    throw new Exception(
                        $"High Water Mark Save Error ... {plant.PlantCode} {mark.HighWaterMarkName} Attempt to Reset Id to 0");
                }

                if (enterpriseDateTime > DateTime.Now) enterpriseDateTime = DateTime.Now;
                var localDateTime = enterpriseDateTime.AddHours(plant.TimeOffset);
                var cmd = SaveHighWaterMarkId();
                cmd.Parameters["@EnterpriseDateTime"].Value = enterpriseDateTime;
                cmd.Parameters["@LocalDateTime"].Value = localDateTime;
                cmd.Parameters["@UniqueID"].Value = uniqueId;
                cmd.Parameters["@MarkName"].Value = mark.HighWaterMarkName;
                cmd.Parameters["@PlantCode"].Value = plant.PlantCode;
                cmd.Connection = cn;
                response.Init(cmd);
                Logger.Trace(Logging.DbCmdString(cmd));
                var rows = cmd.ExecuteNonQuery();
                response.RowsAffected = rows;
            }
            catch (Exception e)
            {
                response.Errors.Add(new Error($"{cn.ConnectionString}  {e.Message}"));
                Logger.Error($@"ERROR: {cn.ConnectionString}  {e.Message}: {e.StackTrace}");
                throw new Exception($"{cn.ConnectionString}  {e.Message}");
            }
            finally
            {
                Logger.Trace($"END: {Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds)} SECONDS");
            }
            return response;
        }
    }
}
