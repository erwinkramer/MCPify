using MCPify.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogging();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Register the test tool just to see our mcp server is working
builder.Services.AddMcpifyTestTool();

builder.Services.AddMcpify(
    swaggerUrl: "https://petstore.swagger.io/v2/swagger.json",
    apiBaseUrl: "https://petstore.swagger.io/v2",
    options =>
    {
        options.ToolPrefix = "petstore_";
    });

var app = builder.Build();

app.Use(async (context, next) =>
{
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {context.Request.Method} {context.Request.Path}");
    await next();
});

app.Use(async (context, next) =>
{
    if (context.Request.Method == "POST" && context.Request.Path == "/sse")
    {
        Console.WriteLine("Patching Request: Rewriting POST /sse -> /messages");
        context.Request.Path = "/messages";
    }
    await next();
});

app.UseCors("AllowAll");

app.MapMcpifyEndpoint();
app.MapGet("/status", () => "MCPify Sample is Running");

app.Run();