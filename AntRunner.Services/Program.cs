using AntRunnerLib.Functions;
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true).AddEnvironmentVariables();
// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "AntRunner Services API", Version = "v1" });
    c.MapType<ScriptType>(() => new OpenApiSchema
    {
        Type = "string",
        Enum = Enum.GetNames(typeof(ScriptType))
            .Select(name => new OpenApiString(name) as IOpenApiAny)
            .ToList()
    });
});

builder.Services.AddDirectoryBrowser();

var app = builder.Build();

foreach (var kvp in builder.Configuration.AsEnumerable())
{
    if (!string.IsNullOrEmpty(kvp.Value) && Environment.GetEnvironmentVariable(kvp.Key) == null)
    {
        Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
    }
}

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "AntRunner Services API");
});
//}

//app.UseHttpsRedirection();

// Serve static files from the "shared" directory
app.UseDefaultFiles(new DefaultFilesOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "shared")),
    RequestPath = ""
});
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "shared")),
    RequestPath = ""
});
app.UseDirectoryBrowser(new DirectoryBrowserOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "shared")),
    RequestPath = ""
});

app.UseAuthorization();

app.MapControllers();

Console.WriteLine("Running...");

app.Run();