var builder = WebApplication.CreateBuilder(args);

// 允许前端跨域访问（Vue 开发服务器 http://localhost:5173）
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowVue",
        policy => policy.AllowAnyOrigin()
                        .AllowAnyHeader()
                        .AllowAnyMethod());
});

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowVue");

app.MapControllers();

app.Run();
