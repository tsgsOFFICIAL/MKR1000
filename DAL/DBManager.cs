using ArduinoApiHTTP.Models;
using Npgsql;

namespace ArduinoApiHTTP.DAL
{
    public class DBManager
    {
        private NpgsqlConnection _connection;
        private readonly string _connectionString = "Host=127.0.0.1;Username=postgres;Password=Marc7075fvg89cdzZ!;Database=arduinodemo";

        public DBManager()
        {
            _connection = new NpgsqlConnection(_connectionString);
        }

        public List<Measurement> GetMeasurements()
        {
            List<Measurement> measurements = new List<Measurement>();

            _connection.Open();

            string sql = "SELECT * FROM demo";

            using (NpgsqlCommand cmd = new NpgsqlCommand(sql, _connection)) // "Using" automatically disposes of objects after use.
            {
                using (NpgsqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        measurements.Add(new Measurement() { Temperature = (double)reader["temperature"], Humidity = (double)reader["humidity"], Time = (DateTime)reader["time"] });
                    }

                    _connection.Close();
                    return measurements;
                }
            }
        }

        public bool AddTemp(Measurement measurement)
        {
            try
            {
                _connection.Open();

                string sql = $"INSERT INTO demo (temperature, humidity) VALUES ({measurement.Temperature.ToString().Replace(',', '.')}, {measurement.Humidity.ToString().Replace(',', '.')});";

                using (NpgsqlCommand command = new NpgsqlCommand(sql, _connection))
                {
                    command.ExecuteNonQueryAsync();
                }

                _connection.Close();

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool ClearTemps()
        {
            try
            {
                _connection.Open();

                string sql = "TRUNCATE TABLE demo RESTART IDENTITY;";

                using (NpgsqlCommand command = new NpgsqlCommand(sql, _connection))
                {
                    command.ExecuteNonQueryAsync();
                }

                _connection.Close();

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
