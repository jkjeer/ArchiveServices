using System;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Runtime.Caching;
using ArchiveServices.Common.Types.Archie;
using ArchiveServices.Common.Types.Response;
using ArchiveServices.Common.Types.Enterprise;
using ArchiveServices.Common.Utilities;
using ArchiveServices.Common.Managers.Archie;
using NLog;
using Plant = ArchiveServices.Common.Types.Enterprise.Plant;

namespace ArchiveServices.Common.Managers.Enterprise
{
    public class PlantManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static ValidationErrors ValidateConfig(int stepID, PlantServerConfig plantServerConfig, SqlConnection cnArchConfig, SqlConnection cnDestination)
        {
            var startTime = DateTime.Now.ToLocalTime();
            Logger.Info($"BEGIN: {startTime}");
            var validationErrors = new ValidationErrors();
            try
            {
                if (!plantServerConfig.IsActive)
                {
                    var validationError = new ValidationError($"{plantServerConfig.ServerName} is Inactive.", "Validation", "Fatal", true);
                    validationErrors.Add(validationError);
                    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                    plantServerConfig.ValidationStatus = validationError.Message;
                    Logger.Error(validationError.Message);
                }
                if (!plantServerConfig.IsEnabled)
                {
                    var validationError = new ValidationError($"{plantServerConfig.ServerName} is Disabled.", "Validation", "Fatal", true);
                    validationErrors.Add(validationError);
                    var errorID = ArchieLoggingManager.LogErrorInsert(cnArchConfig, stepID, validationError.Message, "");
                    plantServerConfig.ValidationStatus = validationError.Message;
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
                if (!validationErrors.Any()) plantServerConfig.ValidationStatus = "Valid";
                Logger.Info($"END: {Convert.ToDecimal((DateTime.Now.ToLocalTime() - startTime).TotalSeconds)} SECONDS");
            }
            return validationErrors;
        }


        /******************************************************************
        * Plant
        ******************************************************************/
        static public Plant GetPlant(string plantCode)
        {
            var plant = new Plant();
            plantCode = plantCode.ToUpper();
            try
            {
                var plants = GetPlants();
                plant = plants.Find(t => t.PlantNum == plantCode || string.Equals(t.PlantCode, plantCode, StringComparison.CurrentCultureIgnoreCase) || t.NumericValue.ToString() == plantCode);
                if (plant == null)
                {
                    throw new Exception($"Error: Plant {plantCode} not found.");
                }
            }
            catch (Exception e)
            {
                throw new Exception($"{e.Message}: {e.StackTrace}");
            }
            return plant;
        }

        public static Plants GetPlants()
        {
            ObjectCache cache = MemoryCache.Default;

            if (cache.Contains("Plants"))
                return (Plants)cache.Get("Plants");
            else
            {
                var cn = new SqlConnection();
                var plants = new Plants();
                try
                {
                    cn = ConnectionManager.GetSqlConnectionFromKey("CORP.INTEGRATION");
                    cn.Open();
                    plants = GetPlantsFromDb(cn);

                    // Store data in the cache
                    CacheItemPolicy cacheItemPolicy = new CacheItemPolicy();
                    //cacheItemPolicy.AbsoluteExpiration = DateTime.Now.AddHours(1.0);
                    cache.Add("Plants", plants, cacheItemPolicy);
                }
                catch (Exception e)
                {
                    Logger.Error($"ERROR:  {cn.ConnectionString} {e.Message}: {e.StackTrace}");
                    throw new Exception($"{e.Message}: {e.StackTrace}");
                }
                finally
                {
                    cn.Close();
                }
                return plants;
            }
        }

        /******************************************************************
        * Plant Attributes
        ******************************************************************/
        public static SqlCommand GetPlantsFromDb()

        {
            var cmd = new SqlCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = String.Format(@"
            SELECT 
                PLANT_NUMERIC_CD, 
                PLANT_ALPHA_CD, 
                PLANT_NAME, 
                PLANT_NO, 
                CUSTOMER_NO, 
                TIME_ZONE_CD, 
                TIME_ZONE_OFFSET,
                COMPANY_CD,
                SHORT_COMPANY_CD,
                ORDER_CALENDAR_CD,
                MC_RepairCenterPK
            FROM 
	            SVCBI.PLANT");
            return cmd;
        }

        public static Plants GetPlantsFromDb(SqlConnection cn)
        {
            var plants = new Plants();
            try
            {
                var cmd = GetPlantsFromDb();
                cmd.Connection = cn;
                Logger.Trace(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        var plant = new Plant();
                        plant.PlantNum = CastDbNull.To(reader["PLANT_NUMERIC_CD"], string.Empty).Trim();
                        plant.PlantCode = CastDbNull.To(reader["PLANT_ALPHA_CD"], string.Empty).Trim();
                        plant.Name = CastDbNull.To(reader["PLANT_NAME"], string.Empty).Trim();
                        plant.NumericValue = CastDbNull.To<int>(reader["PLANT_NO"], 0);
                        plant.AddressNum = CastDbNull.To<decimal>(reader["CUSTOMER_NO"], 0);
                        plant.TimeOffset = CastDbNull.To<int>(reader["TIME_ZONE_OFFSET"], 0);
                        plant.TimeZone = CastDbNull.To(reader["TIME_ZONE_CD"], string.Empty).Trim();
                        plant.CompanyNum = CastDbNull.To(reader["COMPANY_CD"], string.Empty).Trim();
                        plant.ShortCompanyNum = CastDbNull.To(reader["SHORT_COMPANY_CD"], string.Empty).Trim();
                        plant.OrderCalendarName = CastDbNull.To(reader["ORDER_CALENDAR_CD"], string.Empty).Trim();
                        plant.MC_RepairCenterPK = CastDbNull.To<int>(reader["MC_RepairCenterPK"], 0);
                        plants.Add(plant);
                    }

                    reader.Close();
                }
            }
            catch (Exception e)
            {
                Logger.Error("ERROR: " + cn.ConnectionString + " JobName: " + e.Message + ": " + e.StackTrace);
                throw new Exception(cn.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            return plants;
        }

    }
}
