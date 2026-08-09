using ERP.AI.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddTransient<ApiKeyProxyHeaderHandler>();
builder.Services.AddHttpClient("ErpApi", (serviceProvider, client) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    client.BaseAddress = new Uri(configuration["Api:BaseUrl"] ?? "http://localhost:5000");
}).AddHttpMessageHandler<ApiKeyProxyHeaderHandler>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();

app.MapControllers();

app.Run();

public partial class Program;
