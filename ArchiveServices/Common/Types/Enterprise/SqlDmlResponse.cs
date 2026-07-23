using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Runtime.Serialization;
using IBM.Data.DB2.iSeries;

namespace BIServices.Common.Types.Enterprise
{
    [DataContract(Namespace = "")]
    public class SqlDmlResponse
    {
        [DataMember(Order = 10)]
        public string ConnectionString { get; set; }
        [DataMember(Order = 50)]
        public string Statement { get; set; }
        [DataMember(Order = 55)]
        public List<Parameter> Parameters { get; set; }
        [DataMember(Order = 60)]
        public int RowsAffected { get; set; }

        [DataMember(Order = 100)]
        public DateTime StartTime { get; set; }
        [DataMember(Order = 110)]
        public DateTime EndTime { get; set; }

        [DataMember(Order = 200)]
        public Boolean HasErrors { get; set; }
        [DataMember(Order = 210)]
        public string Message { get; set; }

        [DataMember(Order = 300)]
        public string HandlerClass { get; set; }


        public void Init(SqlCommand cmd)
        {
            ConnectionString = cmd.Connection.ConnectionString;
            Statement = cmd.CommandText;
            HandlerClass = cmd.GetType().ToString();

            Parameters = new List<Parameter>();
            foreach (SqlParameter iParameter in cmd.Parameters)
            {
                var parameter = new Parameter();
                parameter.Name = iParameter.ParameterName;
                parameter.Value = iParameter.Value.ToString();
                Parameters.Add(parameter);
            }
        }

        public void Init(iDB2Command cmd)
        {
            ConnectionString = cmd.Connection.ConnectionString;
            Statement = cmd.CommandText;
            HandlerClass = cmd.GetType().ToString();

            Parameters = new List<Parameter>();
            foreach (iDB2Parameter iParameter in cmd.Parameters)
            {
                var parameter = new Parameter();
                parameter.Name = iParameter.ParameterName;
                parameter.Value = iParameter.Value.ToString();
                Parameters.Add(parameter);
            }
        }

    }

    [DataContract(Namespace = "")]
    public class Parameter
    {
        [DataMember(Order = 10)]
        public string Name { get; set; }
        [DataMember(Order = 20)]
        public string Value { get; set; }
    }
}
