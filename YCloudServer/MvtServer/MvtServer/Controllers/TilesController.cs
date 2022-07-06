using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

namespace MvtServer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TilesController : ControllerBase
    {
        [HttpGet("{z}/{x}/{y}")]
        public ActionResult Get (int z, int x, int y)
        {
            using var sqliteConnection = new SqliteConnection("Data Source = C:\\practice\\tiles_mbtiles.mbtiles");
            sqliteConnection.Open();

            using var command = new SqliteCommand("SELECT tile_data FROM tiles WHERE zoom_level = $z AND tile_column = $x AND tile_row = $y", sqliteConnection);

            command.Parameters.AddWithValue("$z", z);
            command.Parameters.AddWithValue("$x", x);
            command.Parameters.AddWithValue("$y", (1<<z)-y-1);

            var tile = (byte[])command.ExecuteScalar();
            if (tile == null) return NoContent();
            Response.Headers.Add("Content-Encoding", "gzip");
            return File(tile, "application/vnd.mapbox-vector-tile");
        }
    }
}
