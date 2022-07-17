using EasyMinutesServer.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers();
// var connectionstring = builder.Configuration.GetConnectionString("DefaultSqliteConnection");
// builder.Services.AddDbContext<DbaseContext>(options => { options.UseSqlite(connectionstring); options.EnableSensitiveDataLogging(true); });
builder.Services.AddDbContext<DbaseContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("LocalSqlServerConnection")));

builder.Services.AddSingleton<IMailWorker, MailWorker>();
builder.Services.AddHostedService<BackgroundMailer>();
builder.Services.AddTransient<MinutesModel>();

var app = builder.Build();

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
