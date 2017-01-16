using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;

namespace ConsoleWebDownload
{
    class Program
    {
        static void Main()
        {
            string path = "websites.csv";
            SqlDataAdapter statsAdapter = null;
            DataTable webStats = new DataTable();
            CreateDataTable(statsAdapter, webStats);
            ContentDownloader cd = new ContentDownloader();
            foreach (var data in Parse(path))
            {
                string[] result = cd.DownloadContent(data.Key, data.Value).Trim().Split(new char[] { ',' }, 4);
                Console.WriteLine($"{result[0]} : {result[1]} : {result[2]}");
                InsertData(statsAdapter, webStats, result);
            }
        }

        private static void InsertData(SqlDataAdapter adapter, DataTable table, string[] input)
        {
            using (SqlConnection connection = GetConnection())
            {
                using (SqlCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "INSERT INTO web_stats (site_name, loading_time, loading_status, content) " + "VALUES (@site, @load, @status, @content)";
                    adapter = new SqlDataAdapter(cmd);
                    SqlCommandBuilder commandBuilder = new SqlCommandBuilder(adapter);
                    cmd.Parameters.Add("@site", SqlDbType.NVarChar, 100, "site_name");
                    cmd.Parameters.Add("@load", SqlDbType.NVarChar, 100, "loading_time");
                    cmd.Parameters.Add("@status", SqlDbType.NVarChar, 30, "loading_status");
                    cmd.Parameters.Add("@content", SqlDbType.NText);

                    cmd.Parameters["@site"].Value = input[0];
                    cmd.Parameters["@load"].Value = input[1];
                    cmd.Parameters["@status"].Value = input[2];
                    cmd.Parameters["@content"].Value = input[3];

                    adapter.InsertCommand = cmd;
                    adapter.InsertCommand.ExecuteNonQuery();

                    int updateResult = adapter.Update(table);
                }
            }
        }

        private static DataTable CreateDataTable(SqlDataAdapter adapter, DataTable table)
        {
            using (SqlConnection connnection = GetConnection())
            {
                using (SqlCommand cmd = connnection.CreateCommand())
                {
                    try
                    {
                        cmd.CommandText = "SELECT * FROM dbo.web_stats";
                        adapter = new SqlDataAdapter(cmd);
                        int result = adapter.Fill(table);
                    } catch (InvalidOperationException ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            }
            return table;
        }

        private static SqlConnection GetConnection()
        {
            string connectionString = "Data Source=(localdb)\\mssqllocaldb;Initial Catalog=ContentAndStatistics;Integrated security=SSPI";
            SqlConnection conn = new SqlConnection(connectionString);
            try
            {
                conn.Open();
                return conn;
            }
            catch (System.Configuration.ConfigurationException ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }

        private static Dictionary<string, int> Parse(string path)
        {
            Dictionary<string, int> websites = new Dictionary<string, int>();
            try
            {
                string[] lines = File.ReadAllLines(path);
                foreach (string line in lines)
                {
                    var url = line.Trim().Split(',').ElementAt(0);
                    var poolingTime = int.Parse(line.Trim().Split(',').ElementAt(1));
                    websites.Add(url, poolingTime);
                }
                return websites;
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }
    }
}
