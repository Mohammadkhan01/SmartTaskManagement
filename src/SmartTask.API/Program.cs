using SmartTask.DAL;
using SmartTask.BLL;
using SmartTask.API.Exceptions;
using SmartTask.API.Authentication;
using SmartTask.DAL.Identity;
using Microsoft.Extensions.Options;
using SmartTask.DAL.Storage;


var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<FileStorageOptions>(options =>
{
    options.RootPath = Path.Combine(
        builder.Environment.ContentRootPath,
        "App_Data",
        "TaskAttachments");
});
// Register services.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddDal(builder.Configuration);
builder.Services.AddBll();

builder.Services.AddJwtAuthentication(builder.Configuration);

var app = builder.Build();
await IdentityRoleSeeder.SeedAsync(app.Services);
// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }