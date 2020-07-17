using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Receiver.Utilities
{
    class Session
    {
        public static int checkSession(string hostName)
        {
            string connetionString;
            SqlConnection connection;
            SqlCommand command;
            string sql;
            SqlDataReader dataReader;
            int status = 0;

            if (Config.conString != null)
            {
                connetionString = Config.conString;
                sql = @"SELECT TOP (1) [sessionid]
                  ,[processid]
                  ,[statusid]
                  ,[BPASession].[lastupdated]
                  ,[laststage]
	              ,[BPAResource].FQDN
                    FROM [dbo].[BPASession] 
                    inner join [BPAResource] on starterresourceid  = [BPAResource].[resourceid]
                    where FQDN = '" + hostName + @"'
                    order by [BPASession].[lastupdated] DESC";

                connection = new SqlConnection(connetionString);
                try
                {
                    connection.Open();
                    command = new SqlCommand(sql, connection);
                    dataReader = command.ExecuteReader();
                    while (dataReader.Read())
                    {
                        status = Int32.Parse(dataReader.GetValue(2).ToString());
                    }
                    command.Dispose();
                    connection.Close();
                }
                catch (Exception ex)
                {
                    LogFile.WriteToFile("Error sql : " + ex.ToString());
                }
            }

            return status;
        }
        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }
        public static string Base64Decode(string base64EncodedData)
        {
            var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
            return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
        }
    }
}
