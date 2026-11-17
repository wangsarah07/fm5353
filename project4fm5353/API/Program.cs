var builder = WebApplication.CreateBuilder(args);

// allow requests from postman and a locally hosted user interface
builder.Services.AddCors(options =>
{
    options.AddPolicy("permissive", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// enable swagger for local api testing
app.UseSwagger();
app.UseSwaggerUI();

// HTTPS redirection is omitted for straightforward localhost testing
app.UseCors("permissive");
app.MapControllers();

app.Run();
