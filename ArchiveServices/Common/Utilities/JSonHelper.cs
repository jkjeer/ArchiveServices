using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using ETLServices.Common.Types.Archie;
using ETLServices.QuerySvc;

namespace ETLServices.Common.Utilities
{
    public static class JsonHelper
    {
        public static string ConvertObjectToJSon<T>(T obj)
        {
            DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(T));
            MemoryStream ms = new MemoryStream();
            ser.WriteObject(ms, obj);
            string jsonString = Encoding.UTF8.GetString(ms.ToArray());
            ms.Close();
            return jsonString;
        }

        public static T ConvertJSonToObject<T>(string jsonString)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
            MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(jsonString));
            T obj = (T)serializer.ReadObject(ms);
            return obj;
        }

        // Create a new AuthToken from a JSON serialization.
        public static AuthToken FromJson(string json)
        {
            // Make a stream to read from.
            MemoryStream stream = new MemoryStream();
            StreamWriter writer = new StreamWriter(stream);
            writer.Write(json);
            writer.Flush();
            stream.Position = 0;

            // Deserialize from the stream.
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(AuthToken));
            AuthToken token = (AuthToken)serializer.ReadObject(stream);

            // Return the result.
            return token;
        }
    }
}
