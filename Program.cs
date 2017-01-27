using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleWebDownload
{
    class Program
    {
        static void Main()
        {
            string path = "websites.csv";
            SqlDataAdapter statsAdapter = null;
            DataTable webStats = new DataTable();
            DataTable errTable = new DataTable();
            CreateDataTable(statsAdapter, webStats);
            CreateErrorsTable(statsAdapter, errTable);
            var entries = Parse(path);
            var numberOfTasks = int.Parse(File.ReadAllLines(path).First().Trim().Split(',').ElementAt(1));
            var numberOfCreatedTasks = 0;
            List<Task<ContentDownloader>> tasks = new List<Task<ContentDownloader>>();
            for (int i = 0; i < entries.Length; i++)
            {
                string[] values = entries[i].Trim().Split(',');
                if(numberOfCreatedTasks < numberOfTasks)
                {
                    CreateTaskWhenSlotsAvailable(tasks, values);
                    numberOfCreatedTasks++;
                }
                else
                {
                    CreateTaskWhenNoSlotsAvailable(tasks, values);
                }
            }
            Task.WaitAll(tasks.ToArray());
            foreach (var task in tasks)
            {
                InsertData(statsAdapter, webStats, new string[]
                    {
                        task.Result.Uri,
                        task.Result.DownloadTime.ToString(),
                        task.Result.Status,
                        task.Result.Content,
                        task.Result.FirstByteDownload.ToString(),
                        task.Result.AdditionalResourcesDownloadTime.ToString()
                    });
                SaveErrors(statsAdapter, errTable, task.Result.Errors);
            }
        }

        private static void CreateTaskWhenNoSlotsAvailable(List<Task<ContentDownloader>> tasks, string[] values)
        {
            var t = Task<ContentDownloader>.Run(async () =>
            {
                await Task.WhenAny(tasks);
                return new ContentDownloader(values[0], int.Parse(values[1]));
            });
            tasks.Add(t);
        }

        private static void CreateTaskWhenSlotsAvailable(List<Task<ContentDownloader>> tasks, string[] values)
        {
            var t = Task<ContentDownloader>.Run(() =>
            {
                return new ContentDownloader(values[0], int.Parse(values[1]));
            });
            tasks.Add(t);
        }

        private static void InsertData(SqlDataAdapter adapter, DataTable table, string[] input)
        {
            using (SqlConnection connection = GetConnection())
            {
                using (SqlCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "INSERT INTO web_stats (site_name, loading_time, loading_status, content, first_byte, additional_resources) "
                        + "VALUES (@site, @load, @status, @content, @fbite, @additional_content)";
                    adapter = new SqlDataAdapter(cmd);
                    SqlCommandBuilder commandBuilder = new SqlCommandBuilder(adapter);
                    cmd.Parameters.Add("@site", SqlDbType.NVarChar, 100, "site_name");
                    cmd.Parameters.Add("@load", SqlDbType.NVarChar, 100, "loading_time");
                    cmd.Parameters.Add("@status", SqlDbType.NVarChar, 30, "loading_status");
                    cmd.Parameters.Add("@content", SqlDbType.NText);
                    cmd.Parameters.Add("@fbite", SqlDbType.NVarChar, 20);
                    cmd.Parameters.Add("@additional_content", SqlDbType.NVarChar, 30);
                    
                    cmd.Parameters["@site"].Value = input[0];
                    cmd.Parameters["@load"].Value = input[1];
                    cmd.Parameters["@status"].Value = input[2];
                    cmd.Parameters["@content"].Value = input[3];
                    cmd.Parameters["@fbite"].Value = input[4];
                    cmd.Parameters["@additional_content"].Value = input[5];

                    adapter.InsertCommand = cmd;
                    adapter.InsertCommand.ExecuteNonQuery();

                    int updateResult = adapter.Update(table);
                }
            }
        }

        private static void SaveErrors(SqlDataAdapter adapter, DataTable table, List<Tuple<string, string>> errors)
        {
            using (SqlConnection connection = GetConnection())
            {
                using (SqlCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "INSERT INTO exceptions (url, error) " + "VALUES (@url, @error)";
                    adapter = new SqlDataAdapter(cmd);
                    SqlCommandBuilder commandBuilder = new SqlCommandBuilder(adapter);
                    cmd.Parameters.Add("@url", SqlDbType.NVarChar, 200);
                    cmd.Parameters.Add("@error", SqlDbType.NText);
                    adapter.InsertCommand = cmd;

                    foreach (var item in errors)
                    {
                        cmd.Parameters["@url"].Value = item.Item1;
                        cmd.Parameters["@error"].Value = item.Item2;
                        adapter.InsertCommand.ExecuteNonQuery();
                        int updateResult = adapter.Update(table);
                    }
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

        private static DataTable CreateErrorsTable(SqlDataAdapter adapter, DataTable table)
        {
            using (SqlConnection connection = GetConnection())
            {
                using (SqlCommand cmd = connection.CreateCommand())
                {
                    try
                    {
                        cmd.CommandText = "SELECT * FROM dbo.exceptions";
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

        private static string[] Parse(string path)
        {
            try
            {
                string[] lines = File.ReadAllLines(path).Skip(1).ToArray();      
                return lines;
            }
            catch (FileNotFoundException ex)
            {
                return null;
            }
        }
    }
}
