using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ReverseGantt.Data;
using ReverseGantt.Interfaces;
using ReverseGantt.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(opt =>
{
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default"));
});

builder.Services.AddScoped<DependencyRulesService>();
builder.Services.AddScoped<IExecutorService, ExecutorService>();
builder.Services.AddSingleton<PasswordService>();

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "ReverseGantt";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "ReverseGantt";
var jwtKey = builder.Configuration["Jwt:Key"] ?? "REPLACE_WITH_LONG_RANDOM_SECRET_AT_LEAST_32_CHARS";

builder.Services.AddSingleton(new JwtTokenService(jwtIssuer, jwtAudience, jwtKey));

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromSeconds(15)
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(o =>
{
    o.AddDefaultPolicy(p =>
        p.AllowAnyOrigin()
         .AllowAnyHeader()
         .AllowAnyMethod()
    );
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
