using ArchiveServices.Common.Types.Archie;
using Microsoft.Data.SqlClient;
using NLog;
using System;
using System.Configuration;
using System.Data.OleDb;
using System.Net.NetworkInformation;
using System.Text;

namespace ArchiveServices.Common.Utilities
{
    public static class ConnectionManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static OleDbConnection GetOleDbConnection(string connectionString)
        {
            var builder = new OleDbConnectionStringBuilder(connectionString);
            IsPingable(builder["Network Address"].ToString());
            var oleDbCn = new OleDbConnection { ConnectionString = connectionString };
            try
            {
                oleDbCn.ConnectionString = connectionString;
                return oleDbCn;
            }
            catch (Exception)
            {
                var message = "ERROR using: [" + connectionString + "]";
                throw new Exception(message);
            }
        }

        public static SqlConnection GetSqlConnection(string connectionString)
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            if (!builder.MultiSubnetFailover) IsPingable(builder.DataSource);
            var sqlCn = new SqlConnection { ConnectionString = connectionString };
            try
            {
                sqlCn.ConnectionString = connectionString;
                return sqlCn;
            }
            catch (Exception)
            {
                var message = "ERROR using: [" + connectionString + "]";
                throw new Exception(message);
            }
        }

        public static SqlConnection GetSqlConnectionFromKey(string keyValue, bool ping = true)
        {
            var cn = new SqlConnection();
            var builder = new SqlConnectionStringBuilder();
            try
            {
                builder.ConnectionString = ConfigurationManager.ConnectionStrings[keyValue].ConnectionString;
                cn.ConnectionString = builder.ConnectionString;
            }
            catch (Exception e)
            {
                var errorMsg = "Connection String Key Lookup Failed for " + keyValue + Environment.NewLine + e.Message;
                throw new Exception(errorMsg);
            }
            try
            {
                if (!builder.MultiSubnetFailover && ping) IsPingable(builder.DataSource);
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
            cn.ConnectionString = builder.ConnectionString;
            return cn;
        }

        public static SqlConnection GetSqlConnectionFromParts(string serverName, string databaseName, bool ping = true)
        {
            var cn = new SqlConnection();
            var builder = new SqlConnectionStringBuilder();
            try
            {
                builder.ConnectionString = $@"Data Source={serverName};Initial Catalog={databaseName};Integrated Security=SSPI;MultipleActiveResultSets=true;TrustServerCertificate=True;Application Name=ArchiveServices;";
                var connectTimeout = Convert.ToInt32(ConfigurationManager.AppSettings["ArchieConnectTimeout"]);
                builder.ConnectTimeout = connectTimeout > 0 ? connectTimeout : 30; 
                cn.ConnectionString = builder.ConnectionString;
            }
            catch (Exception e)
            {
                var errorMsg = $@"Connection String Build Failed for {serverName}.{databaseName}" + Environment.NewLine + e.Message;
                throw new Exception(errorMsg);
            }
            try
            {
                if (!builder.MultiSubnetFailover && ping) IsPingable(builder.DataSource);
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {cn.ConnectionString}  {e.Message}: {e.StackTrace}");
                //throw new Exception(e.Message);
            }
            //cn.ConnectionString = builder.ConnectionString;
            return cn;
        }

        public static void IsPingable2(string hostName)
        {
            var pingSender = new Ping();
            var options = new PingOptions { DontFragment = true };
            const string data = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var buffer = Encoding.ASCII.GetBytes(data);
            int timeout = Convert.ToInt32(ConfigurationManager.AppSettings["PingTimeoutMs"]);
            try
            {
                var reply = pingSender.Send(hostName, timeout, buffer, options);
                Logger.Trace($"{hostName} {reply?.Status} {reply?.RoundtripTime}");
                if (reply?.Status != IPStatus.Success)
                {
                    Logger.Info($"{hostName} Timed Out");
                    throw new Exception($"{hostName} Timed Out");
                }
            }
            catch (Exception)
            {
                Logger.Info($"{hostName} PING Unsuccessful");
                throw new Exception($"{hostName} PING Unsuccessful");
            }
        }

        public static void IsPingable(string hostName)
        {
            var pingSender = new Ping();
            var options = new PingOptions { DontFragment = true };
            const string data = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var buffer = Encoding.ASCII.GetBytes(data);
            int timeout = Convert.ToInt32(ConfigurationManager.AppSettings["PingTimeoutMs"]);
            int retries = Convert.ToInt32(ConfigurationManager.AppSettings["PingRetries"]);
            try
            {
                for (int i = 1; i <= retries; i++)
                {
                    var reply = pingSender.Send(hostName, timeout, buffer, options);
                    Logger.Trace($"{hostName} {reply?.Status} {reply?.RoundtripTime}");
                    if (reply?.Status == IPStatus.Success)
                    {
                        return;
                    }
                }
                throw new Exception($"[{hostName}] PING Failure");
            }
            catch (Exception e)
            {
                Logger.Info($"[{hostName}] {e.Message}");
                throw new Exception(e.Message);
            }
        }

        public static string GetE1ConnectionString()
        {
            string connectionString;
            var connectionKey = "";
            try
            {
                var sb = new StringBuilder();
                sb.Append(ConfigurationManager.AppSettings["PrimaryEnvironment"]);
                sb.Append(".E1");
                connectionKey = sb.ToString();
                connectionString = ConfigurationManager.ConnectionStrings[connectionKey].ConnectionString;
            }
            catch (Exception)
            {
                var errorMsg = "Connection String Key not found for " + connectionKey;
                throw new Exception(errorMsg);
            }
            return connectionString;
        }

        public static string GetSFConnectionString(String plantCode, String databaseName)
        {
            string connectionString;
            var connectionKey = "";
            try
            {
                var sb = new StringBuilder();
                sb.Append(ConfigurationManager.AppSettings["PrimaryEnvironment"]);
                sb.Append(".");
                sb.Append(plantCode);
                sb.Append(".");
                sb.Append(databaseName);
                connectionKey = sb.ToString();
                connectionString = ConfigurationManager.ConnectionStrings[connectionKey].ConnectionString;
            }
            catch (Exception)
            {
                var errorMsg = "Connection String Key not found for " + connectionKey;
                throw new Exception(errorMsg);
            }
            return connectionString;
        }

        public static string GetDb2ConnectionStringPY(string environment = null)
        {
            string connectionString;
            var connectionKey = "";
            try
            {
                connectionKey = "PY.iSeries";
                connectionString = ConfigurationManager.ConnectionStrings[connectionKey].ConnectionString;
            }
            catch (Exception)
            {
                var errorMsg = "Connection String Key not found for " + connectionKey;
                throw new Exception(errorMsg);
            }
            return connectionString;
        }

        public static string GetDb2ConnectionString(string environment = null)
        {
            string connectionString;
            var connectionKey = "";
            try
            {
                var sb = new StringBuilder();
                sb.Append(string.IsNullOrEmpty(environment) ? ConfigurationManager.AppSettings["PrimaryEnvironment"] : environment);
                sb.Append(".iSeries");
                connectionKey = sb.ToString();
                connectionString = ConfigurationManager.ConnectionStrings[connectionKey].ConnectionString;
            }
            catch (Exception)
            {
                var errorMsg = "Connection String Key not found for " + connectionKey;
                throw new Exception(errorMsg);
            }
            return connectionString;
        }

        public static string GetConnectionString(string name)
        {
            try
            {
                return ConfigurationManager.ConnectionStrings[name].ConnectionString;
            }
            catch (Exception)
            {
                var errorMsg = "Connection String Key not found for " + name;
                throw new Exception(errorMsg);
            }
        }
    }
}
