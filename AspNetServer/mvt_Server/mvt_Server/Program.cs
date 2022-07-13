using System.Net.Sockets;
using Microsoft.AspNetCore.HttpOverrides;
using Serilog;
using Serilog.Filters;
using Serilog.Formatting.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Linq;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors((options) =>
{
    options.AddPolicy("all", (policy) =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

builder.Host.UseSerilog((_, _, configuration) =>
{
    configuration.WriteTo.Logger(lc =>
            lc.Filter.ByExcluding(Matching.FromSource("mvt"))
                .WriteTo.File(new JsonFormatter(),
                    builder.Configuration["Logging:LogFiles:log"])) 
        .WriteTo.Logger(lc =>
            lc.Filter.ByIncludingOnly(Matching.FromSource("mvt"))
                .WriteTo.File(builder.Configuration["Logging:LogFiles:mvt"]));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseRouting();

app.UseCors("all");

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                       ForwardedHeaders.XForwardedProto
});

app.MapGet("api/tiles/{z}/{x}/{y}/", (int Z, int X, int Y, HttpContext httpContext, ILoggerFactory loggerFactory) =>
{
    var remoteAddress = httpContext.Connection.RemoteIpAddress;
    if (remoteAddress != null)
    {
        if (remoteAddress.AddressFamily == AddressFamily.InterNetworkV6)
        {
            remoteAddress = System.Net.Dns.GetHostEntry(remoteAddress).AddressList
                .First(x => x.AddressFamily == AddressFamily.InterNetwork);
        }
    }

    var log = loggerFactory.CreateLogger("mvt");
    log.LogCritical("IP:" + remoteAddress);
    log.LogCritical($"tiles: (z: {Z}, x: {X}, y: {Y})");

    var connectionStringBuilder = new SqliteConnectionStringBuilder { DataSource = builder.Configuration["ConnectionStrings:mvtConnectionString"] };
    using var sqliteConnection = new SqliteConnection(connectionStringBuilder.ConnectionString);
    sqliteConnection.Open();

    using var sqlCommandGet = new SqliteCommand(@"SELECT tile_data FROM tiles WHERE zoom_level = $z AND tile_column = $x AND tile_row = $y", sqliteConnection);
    sqlCommandGet.Parameters.AddWithValue("$z", Z);
    sqlCommandGet.Parameters.AddWithValue("$x", X);
    sqlCommandGet.Parameters.AddWithValue("$y", (1 << Z) - Y - 1);
    var tile = (byte[])sqlCommandGet.ExecuteScalar();
    if (tile == null) return Results.NoContent();
    httpContext.Response.Headers.Add("Content-Encoding", "gzip");
    return Results.File(tile, "application/vnd.mapbox-vector-tile");
});

app.Run();
