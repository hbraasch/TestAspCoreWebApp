using EasyMinutesServer.Models;
using Microsoft.EntityFrameworkCore;
using System.Configuration;
using System.Net.Sockets;
using System.Net;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers();
// var connectionstring = builder.Configuration.GetConnectionString("DefaultSqliteConnection");
// builder.Services.AddDbContext<DbaseContext>(options => { options.UseSqlite(connectionstring); options.EnableSensitiveDataLogging(true); });

string conn = "";
if (GetIpAddress().FirstOrDefault(o => o.Contains("192.168.0")) != null)
{
    // Running on local machine
    conn = builder.Configuration.GetConnectionString("LocalSqlServerConnection");
}
else
{
    // Running on remote server
    conn = builder.Configuration.GetConnectionString("RemoteSqlServerConnection");
}
builder.Services.AddDbContext<DbaseContext>(options => options.UseSqlServer(conn));

builder.Services.AddSingleton<IMailWorker, MailWorker>();
builder.Services.AddHostedService<BackgroundMailer>();
builder.Services.AddTransient<MinutesModel>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<DbaseContext>();
    await dbContext.Database.EnsureCreatedAsync();
    await dbContext.Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

app.Run();

static List<string> GetIpAddress()
{
    var addresses = new List<string>();
    var host = Dns.GetHostEntry(Dns.GetHostName());
    foreach (var ip in host.AddressList)
    {
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            addresses.Add(ip.ToString());
        }
    }
    return addresses;
}
