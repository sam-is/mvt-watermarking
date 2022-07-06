using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace mvt_Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TilesController : ControllerBase
    {
        [HttpGet("{z}/{x}/{y}")]
        public ActionResult GetTile(int z, int x, int y)
        {
            using var sqliteConnection = new SqliteConnection("Data Source = D:\\maximum_mbtiles.mbtiles");
            sqliteConnection.Open();

            using var sqlCommandGet = new SqliteCommand(@"SELECT tile_data FROM tiles WHERE zoom_level = $z AND tile_column = $x AND tile_row = $y", sqliteConnection);
            sqlCommandGet.Parameters.AddWithValue("$z", z);
            sqlCommandGet.Parameters.AddWithValue("$x", x);
            sqlCommandGet.Parameters.AddWithValue("$y", (1 << z) - y - 1);
            var tile = (byte[])sqlCommandGet.ExecuteScalar();

            Response.Headers.Add("Content-Encoding", "gzip");
            return File(tile, "application/vnd.mapbox-vector-tile");
        }
    }
}
