using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

namespace WebService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TablesController : ControllerBase
    {
        private readonly string _dbPath;

        public TablesController()
        {
            _dbPath = Path.Combine(AppContext.BaseDirectory, "data.db");
        }

        [HttpGet]
        public IActionResult GetTables()
        {
            var tables = new List<string>();
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                tables.Add(reader.GetString(0));
            }
            return Ok(tables);
        }

        [HttpGet("{tableName}")]
        public IActionResult GetTableData(string tableName)
        {
            var rows = new List<Dictionary<string, object?>>();
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT * FROM \"{tableName}\" LIMIT 100;";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                rows.Add(row);
            }
            return Ok(rows);
        }
    }
}