using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MMGT.API.Data;
using MMGT.API.Helpers;
using MMGT.API.Services;
using MMGT.API.Services.Interfaces;
using MMGT.API.Middleware;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// register typed HttpClient for Infobip email service
builder.Services.AddHttpClient<IEmailService, InfobipEmailService>()
    .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromMinutes(10));

builder.Services.AddControllers();


builder.Services.AddEndpointsApiExplorer();

// Configure Swagger with JWT authentication
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "MMGT API",
        Version = "v1",
        Description = "Mortgage Management Portal API"
    });

    // Add JWT authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

// Add DbContext with connection pooling for performance
// Increased CommandTimeout to 300 seconds (5 minutes) to support large document merge operations
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlServerOptions => sqlServerOptions
            .EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null)
            .CommandTimeout(300))
);

// Add SMS Notification DbContext (SMSNotification database) with connection pooling
// Increased CommandTimeout to 300 seconds (5 minutes) to support large document merge operations
builder.Services.AddDbContext<SmsNotificationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("SmsNotificationConnection"),
        sqlServerOptions => sqlServerOptions
            .EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null)
            .CommandTimeout(300))
);

// Add memory caching for performance metrics
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 104857600; // 100 MB cache size limit
    options.CompactionPercentage = 0.25;
    options.ExpirationScanFrequency = TimeSpan.FromMinutes(5);
});

// Add JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
            ValidAudience = builder.Configuration["JwtSettings:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey is not configured")))
        };
    });

// Add Authorization
builder.Services.AddAuthorization();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins(
            "https://accessmortgage.access-armpensions.com",
            "https://accessmortgage.accesspensions.ng",
            "http://172.16.50.32:8097",
            "https://172.16.50.32:8097",
            "https://172.16.50.32:8098",
            "http://172.16.50.32:8098",
            "http://localhost:5173",
            "http://localhost:5174"
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
        .WithExposedHeaders("Content-Disposition"); // For file downloads
    });
});

// Register Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IPMIService, PMIService>();

// Register Performance and Escalation Services first (required dependencies)
builder.Services.AddScoped<IReviewerPerformanceService, ReviewerPerformanceService>();
builder.Services.AddScoped<IEscalationService, EscalationService>();

// Services that depend on Performance services
builder.Services.AddScoped<IApplicationService, ApplicationService>();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<ISmsService, SmsService>();

// New feature services
builder.Services.AddScoped<IDocumentTypeService, DocumentTypeService>();
builder.Services.AddScoped<IReviewerAssignmentService, ReviewerAssignmentService>();
builder.Services.AddScoped<ISystemConfigurationService, SystemConfigurationService>();
builder.Services.AddScoped<IDocumentReviewService, DocumentReviewService>();
builder.Services.AddScoped<IMigrationService, MigrationService>();

// Register Advanced Workload Management Services
builder.Services.AddScoped<IReviewerAvailabilityService, ReviewerAvailabilityService>();
builder.Services.AddScoped<IWorkloadSuggestionService, WorkloadSuggestionService>();
builder.Services.AddScoped<ICriteriaBasedReassignmentService, CriteriaBasedReassignmentService>();

// Add BackgroundService for periodic escalation checks
builder.Services.AddHostedService<EscalationCheckBackgroundService>();

// Add JWT Helper
builder.Services.AddScoped<JwtHelper>();

var app = builder.Build();

// Configure the HTTP request pipeline.

    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "MMGT API v1");
        c.RoutePrefix = string.Empty; // Set Swagger UI at root URL
    });

// Add global exception handling middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

// CORS must come BEFORE UseHttpsRedirection and UseAuthentication
app.UseCors("AllowReactApp");

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Seed database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await context.Database.MigrateAsync();
    await DbSeeder.SeedAsync(context);
}



app.Run();