using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace App
{
    class Program
    {
        static void Main(string[] args)
        {
            var worlds = Queries(100).Result;
            var fortunes = Fortunes().Result;
            var updates = Update(100).Result;
        }

        static MySqlConnection CreateConnection(string providerName)
        {
            ConnectionStringSettings connectionSettings = ConfigurationManager.ConnectionStrings[providerName];
            DbProviderFactory factory = DbProviderFactories.GetFactory(connectionSettings.ProviderName);
            DbConnection connection = factory.CreateConnection();
            connection.ConnectionString = connectionSettings.ConnectionString;
            return connection as MySqlConnection; 
        }

        static async Task<List<World>> Queries(int queries)
        {
            List<World> worlds = new List<World>(queries);

            MySqlConnection connection = CreateConnection("MySQL");
            await connection.OpenAsync();

            MySqlCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM World WHERE id = @ID";

            Random random = new Random();

            for (int i = 0; i < worlds.Capacity; i++)
            {
                int randomID = random.Next(0, 10000) + 1;

                DbParameter parameter = command.CreateParameter();
                parameter.ParameterName = "@ID";
                parameter.Value = randomID;

                command.Parameters.Clear();
                command.Parameters.Add(parameter);

                MySqlDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow);
                if (await reader.ReadAsync())
                {
                    World world = new World();
                    world.id = reader.GetInt32(0);
                    world.randomNumber = reader.GetInt32(1);

                    worlds.Add(world);
                }
                await reader.DisposeAsync();
            }

            await command.DisposeAsync();
            await connection.DisposeAsync();

            return worlds;
        }

        static async Task<List<Fortune>> Fortunes()
        {
            List<Fortune> fortunes = new List<Fortune>();

            MySqlConnection connection = CreateConnection("MySQL");
            await connection.OpenAsync();

            MySqlCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Fortune";

            MySqlDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
            while (await reader.ReadAsync())
            {
                Fortune fortune = new Fortune
                {
                    ID = reader.GetInt32(0),
                    Message = reader.GetString(1)
                };

                fortunes.Add(fortune);
            }

            await reader.DisposeAsync();
            await command.DisposeAsync();
            await connection.DisposeAsync();

            fortunes.Add(new Fortune { ID = 0, Message = "Additional fortune added at request time." });
            fortunes.Sort();

            return fortunes;
        }

        static async Task<List<World>> Update(int? queries)
        {
            List<World> worlds = new List<World>(Math.Max(1, Math.Min(500, queries ?? 1)));

            MySqlConnection connection = CreateConnection("MySQL");
            await connection.OpenAsync();

            MySqlCommand selectCommand = connection.CreateCommand();
            MySqlCommand updateCommand = connection.CreateCommand();
            selectCommand.CommandText = "SELECT * FROM World WHERE id = @ID";
            updateCommand.CommandText = "UPDATE World SET randomNumber = @Number WHERE id = @ID";

            Random random = new Random();

            for (int i = 0; i < worlds.Capacity; i++)
            {
                int randomID = random.Next(0, 10000) + 1;
                int randomNumber = random.Next(0, 10000) + 1;

                DbParameter idParameter = selectCommand.CreateParameter();
                idParameter.ParameterName = "@ID";
                idParameter.Value = randomID;

                selectCommand.Parameters.Clear();
                selectCommand.Parameters.Add(idParameter);

                World world = null;

                MySqlDataReader reader = await selectCommand.ExecuteReaderAsync(CommandBehavior.SingleRow);
                if (await reader.ReadAsync())
                {
                    world = new World
                    {
                        id = reader.GetInt32(0),
                        randomNumber = reader.GetInt32(1)
                    };
                }
                await reader.DisposeAsync();

                if (world == null)
                    continue;

                DbParameter numberParameter = updateCommand.CreateParameter();
                numberParameter.ParameterName = "@Number";
                numberParameter.Value = randomNumber;

                updateCommand.Parameters.Clear();
                updateCommand.Parameters.Add(idParameter);
                updateCommand.Parameters.Add(numberParameter);

                await updateCommand.ExecuteNonQueryAsync();

                world.randomNumber = randomNumber;
                worlds.Add(world);
            }

            await selectCommand.DisposeAsync();
            await updateCommand.DisposeAsync();
            await connection.DisposeAsync();

            return worlds;
        }
    }

    public class World
    {
        public int id { get; set; }
        public int randomNumber { get; set; }
    }

    public class Fortune : IComparable<Fortune>
    {
        public int ID { get; set; }
        public string Message { get; set; }

        public int CompareTo(Fortune other)
        {
            return Message.CompareTo(other.Message);
        }
    }
}
