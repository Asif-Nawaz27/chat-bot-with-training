using MiniGptChat;
using MiniGptChat.Api.Extensions;
using MiniGptChat.Api.Services;

const string WebClientCorsPolicy = "WebClient";

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

//Get configuration
builder.Services.GetConfiguration(builder.Configuration);

//Registered services
builder.Services.RegisterServices();

builder.Services.AddCorePolicy(builder.Configuration, WebClientCorsPolicy);


var app = builder.Build();

app.Services.setServicesScop();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Skipped in Development: the "http" launch profile (and MiniGptChat.Web's
// dev server) deliberately talk plain HTTP on localhost, and redirecting to
// HTTPS here would turn every fetch() call from the browser into a
// cross-origin redirect, which CORS blocks outright (this is what a "307
// Temporary Redirect" failure from the web app means).
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Routing must run before CORS so that CORS preflight (OPTIONS) requests are
// short-circuited by the CORS middleware itself rather than falling through
// to endpoint routing's own "405 Method Not Allowed" handling.
app.UseRouting();

app.UseCors(WebClientCorsPolicy);

app.UseAuthorization();

app.MapControllers();

app.Run();
