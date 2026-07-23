using System;
using System.Collections.Generic;
using System.Data;
using System.Runtime.Caching;
using BIServices.Common.Types.Enterprise;
using BIServices.Common.Utilities;
using IBM.Data.DB2.iSeries;

namespace BIServices.Common.Managers.Enterprise
{
    public class BusinessSystemSession
    {
        public iDB2Connection DbConnection;
        public Dictionary<string, iDB2Command> DbCommands;
        public Dictionary<string, object> Managers;
        public PlantMap PlantMap;
        public bool IsOpen;

        public void Init()
        {
            try
            {
                var dbConnectionString = ConnectionManager.GetDb2ConnectionString();
                DbConnection = ConnectionManager.GetDb2Connection(dbConnectionString);
                DbCommands = new Dictionary<string, iDB2Command>();

                var cache = MemoryCache.Default;
                if (cache.Contains("PlantMap"))
                {
                    PlantMap = (PlantMap)cache.Get("PlantMap");
                }
                else
                {
                    var map = new PlantMap();
                    var cacheItemPolicy = new CacheItemPolicy();
                    cacheItemPolicy.AbsoluteExpiration = DateTime.Now.AddHours(24);
                    cache.Add("PlantMap", map, cacheItemPolicy);
                    PlantMap = map;
                }
            }
            catch (Exception e)
            {
                throw new ApplicationException(e.ToString());
            }
        }

        public void Open()
        {
            try
            {
                DbConnection.Open();
                IsOpen = true;
            }
            catch (Exception e)
            {
                throw new ApplicationException(e.ToString());
            }

        }

        public void Close()
        {
            try
            {
                if (IsOpen)
                {
                    DbConnection.Close();
                    IsOpen = false;
                }
            }
            catch (Exception e)
            {
                throw new ApplicationException(e.ToString());
            }
        }

        public void Prepare()
        {
            foreach (iDB2Command command in DbCommands.Values)
            {
                command.Connection = DbConnection;
                command.Prepare();
            }
        }

        /**********************************************************************
        *  Current Time Stamp
        *********************************************************************/
        public iDB2Command GetCurrentTimestampStmt()
        {
            var cmd = new iDB2Command();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = @"
            SELECT
              CURRENT TIMESTAMP AS TS
            FROM
              SYSIBM.SYSDUMMY1";
            return cmd;
        }

        public DateTime GetCurrentTimestamp()
        {
            var dt = new DateTime();
            try
            {
                var cmd = GetCurrentTimestampStmt();
                cmd.Connection = this.DbConnection;
                IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleRow);
                while (reader.Read())
                {
                    try
                    {
                        dt = CastDbNull.To<DateTime>(reader["TS"], new DateTime(1900, 1, 1));
                    }
                    catch (Exception e)
                    {
                        throw new ApplicationException(e.ToString());
                    }
                }
                reader.Close();
            }
            catch (Exception err)
            {
                throw new ApplicationException(err.ToString());
            }
            return dt;
        }

    }
}
