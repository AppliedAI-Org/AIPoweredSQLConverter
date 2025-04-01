using Microsoft.OpenApi.Models;
using AIPoweredSQLConverter.API.Config;
using IntelligenceHub.Host.Config;
using AIPoweredSQLConverter.Common;
using AIPoweredSQLConverter.Host.Config;
using AIPoweredSQLConverter.Client.IntelligenceHub;
using AIPoweredSQLConverter.Business;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;
using System.Security.Claims;
using AIPoweredSQLConverter.Host.Auth;
using Microsoft.AspNetCore.Authorization;
using AIPoweredSQLConverter.DAL;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AIPoweredSQLConverter.Host
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            var connectionStrings = builder.Configuration.GetRequiredSection(nameof(ConnectionStrings)).Get<ConnectionStrings>();
            var authSettings = builder.Configuration.GetRequiredSection(nameof(AuthSettings)).Get<AuthSettings>();
            var intelligenceHubSettings = builder.Configuration.GetRequiredSection(nameof(IntelligenceHubAuthSettings)).Get<IntelligenceHubAuthSettings>();
            var stripeSettings = builder.Configuration.GetRequiredSection(nameof(StripeSettings)).Get<StripeSettings>();

            // Validate required settings.
            if (string.IsNullOrEmpty(connectionStrings?.DbConnectionString) || string.IsNullOrEmpty(connectionStrings?.AppInsightsConnectionString))
            {
                throw new ArgumentException("Connection strings cannot be null or empty.");
            }
            if (string.IsNullOrEmpty(authSettings?.BasicAuthUsername) || string.IsNullOrEmpty(authSettings?.BasicAuthPassword) ||
                string.IsNullOrEmpty(authSettings?.Audience) || string.IsNullOrEmpty(authSettings?.Domain))
            {
                throw new ArgumentException("Auth settings cannot be null or empty.");
            }
            if (string.IsNullOrEmpty(intelligenceHubSettings?.Endpoint) || string.IsNullOrEmpty(intelligenceHubSettings?.AdminClientId) ||
                string.IsNullOrEmpty(intelligenceHubSettings?.AdminClientSecret) || string.IsNullOrEmpty(intelligenceHubSettings?.DefaultClientId) ||
                string.IsNullOrEmpty(intelligenceHubSettings?.DefaultClientSecret) || string.IsNullOrEmpty(intelligenceHubSettings?.Audience))
            {
                throw new ArgumentException("Intelligence Hub Auth settings cannot be null or empty.");
            }
            if (string.IsNullOrEmpty(stripeSettings.SecretKey) || string.IsNullOrEmpty(stripeSettings.PublishableKey)
                || string.IsNullOrEmpty(stripeSettings.SignupWebhookKey) || string.IsNullOrEmpty(stripeSettings.CancelationWebhookKey)
                || string.IsNullOrEmpty(stripeSettings.Domain) || string.IsNullOrEmpty(stripeSettings.MeterName)
                || string.IsNullOrEmpty(stripeSettings.MeterPriceKey))
            {
                throw new ArgumentException("Stripe Settings cannot be null or empty.");
            }

            // Register configuration objects.
            builder.Services.AddSingleton(connectionStrings);
            builder.Services.AddSingleton(authSettings);
            builder.Services.AddSingleton(intelligenceHubSettings);
            builder.Services.AddSingleton(stripeSettings);

            builder.Services.AddHttpClient();
            builder.Services.AddSingleton<AIAuthClient>();
            builder.Services.AddSingleton<IAIAuthClient, AIAuthClient>();
            builder.Services.AddSingleton<IAIClientWrapper, AIClientWrapper>();
            builder.Services.AddScoped<IPromptFlowLogic, PromptFlowLogic>();
            builder.Services.AddScoped<IAuthLogic, AuthLogic>();
            builder.Services.AddScoped<IPublicApiLogic, PublicApiLogic>();

            #region Authentication
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = $"https://{authSettings.Domain}/";
                    options.Audience = authSettings.Audience;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        NameClaimType = ClaimTypes.NameIdentifier,
                    };
                });

            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("Auth0Policy", policy =>
                {
                    policy.AuthenticationSchemes.Add(JwtBearerDefaults.AuthenticationScheme);
                    policy.RequireAuthenticatedUser();
                });

                options.AddPolicy("ApiKeyPolicy", policy =>
                {
                    policy.RequireAssertion(context => context.User.Identity != null && context.User.Identity.AuthenticationType == "ApiKey");
                });
            });

            builder.Services.AddSingleton<IAuthorizationHandler, HasScopeHandler>();
            #endregion

            // Register EF Core DbContext
            builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionStrings.DbConnectionString));

            // Add controllers with JSON options.
            builder.Services.AddControllersWithViews().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

            // Register API key validator and HttpContextAccessor.
            builder.Services.AddScoped<IApiKeyValidator, ApiKeyValidator>();
            builder.Services.AddHttpContextAccessor();

            // Register Swagger generator.
            builder.Services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "Conversational AI Website", Version = "v1" });
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Enter 'Bearer' followed by a space and the JWT token."
                });

                options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
                {
                    Description = "API Key needed to access the endpoints. X-API-Key: {apiKey}",
                    Type = SecuritySchemeType.ApiKey,
                    Name = "X-API-Key",
                    In = ParameterLocation.Header,
                });

                // Add both security schemes as requirements
                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
                        Array.Empty<string>()
                    },
                    {
                        new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" } },
                        Array.Empty<string>()
                    }
                });
            });

            // Add Application Insights logging.
            builder.Services.AddApplicationInsightsTelemetry(options =>
            {
                options.ConnectionString = connectionStrings?.AppInsightsConnectionString;
            });
            builder.Services.AddLogging(options =>
            {
                options.AddApplicationInsights();
            });


            var app = builder.Build();

            // Enable CORS.
            app.UseCors("AllowSpecificOrigins");

            // Production settings.
            if (!app.Environment.IsDevelopment())
            {
                app.UseHsts();
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Your API V1");
                    c.RoutePrefix = "swagger";
                });
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();

            // Add API key middleware before authentication and authorization.
            app.UseMiddleware<ApiKeyMiddleware>();

            // Add authentication and authorization.
            app.UseAuthentication();
            app.UseAuthorization();


            // Map controllers and default route.
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller}/{action=Index}/{id?}");

            // Fallback route.
            app.MapFallbackToFile("index.html");

            app.Run();
        }
    }
}
