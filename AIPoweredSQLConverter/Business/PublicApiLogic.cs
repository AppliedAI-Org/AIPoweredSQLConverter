using AIPoweredSQLConverter.API;
using AIPoweredSQLConverter.Client.IntelligenceHub;
using AIPoweredSQLConverter.ClientApp.src.components;
using AIPoweredSQLConverter.DAL;
using AIPoweredSQLConverter.DAL.Models;
using AIPoweredSQLConverter.Host.Auth;
using AIPoweredSQLConverter.Host.Config;
using Microsoft.EntityFrameworkCore;
using Stripe;
using System.Security.Cryptography;
using System.Text;

namespace AIPoweredSQLConverter.Business
{
    public class PublicApiLogic : IPublicApiLogic
    {
        private readonly IAIClientWrapper _aiClient;
        private readonly AppDbContext _dbContext;
        private readonly IApiKeyValidator _apiKeyValidator;
        private readonly IHttpContextAccessor _httpContextAccessor;

        private const int _maxRequestsPerDay = 20;
        private const string _sqlConversionProfile = "NLSequelConverter";
        private const string _sqlValidationProfile = "SqlValidator";

        private readonly string _meterName;
        private readonly string _stripeKey;

        public PublicApiLogic(
            IAIClientWrapper aiClient,
            AppDbContext context,
            StripeSettings settings,
            IApiKeyValidator apiKeyValidator,
            IHttpContextAccessor httpContextAccessor)
        {
            _aiClient = aiClient;
            _dbContext = context;
            _meterName = settings.MeterName;
            _stripeKey = settings.SecretKey;
            _apiKeyValidator = apiKeyValidator;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<BackendResponse<int>> GetRemainingFreeRequests(string apiKey)
        {
            var encryptedApiKey = HashApiKey(apiKey);
            var user = await _dbContext.UserData.FirstOrDefaultAsync(u => u.EncryptedApiKey == encryptedApiKey);
            if (user == null)
            {
                return BackendResponse<int>.CreateFailureResponse("Invalid API key.", System.Net.HttpStatusCode.Unauthorized);
            }
            return BackendResponse<int>.CreateSuccessResponse(_maxRequestsPerDay - user.RequestCount);
        }

        public async Task<BackendResponse<bool>> UpsertUserSQLData(ApiRequest request, string apiKey)
        {
            try
            {
                var encryptedApiKey = HashApiKey(apiKey);
                var user = await _dbContext.UserData.FirstOrDefaultAsync(u => u.EncryptedApiKey == encryptedApiKey);
                if (user == null)
                {
                    return BackendResponse<bool>.CreateFailureResponse("Invalid or expired API key.");
                }

                user.UserSQLData = request.SqlData;
                _dbContext.UserData.Update(user);
                var changes = await _dbContext.SaveChangesAsync();
                return changes > 0
                    ? BackendResponse<bool>.CreateSuccessResponse(true, "User data updated successfully.")
                    : BackendResponse<bool>.CreateFailureResponse("No changes were made to the database.");
            }
            catch (Exception)
            {
                return BackendResponse<bool>.CreateFailureResponse("An error occurred when updating user data.");
            }
        }

        public async Task<BackendResponse<string?>> GetSQLData(string apiKey)
        {
            try
            {
                var encryptedApiKey = HashApiKey(apiKey);
                var user = await _dbContext.UserData.FirstOrDefaultAsync(u => u.EncryptedApiKey == encryptedApiKey);
                if (user == null)
                {
                    return BackendResponse<string?>.CreateFailureResponse("Invalid API key.", System.Net.HttpStatusCode.Unauthorized);
                }

                return !string.IsNullOrEmpty(user.UserSQLData)
                    ? BackendResponse<string?>.CreateSuccessResponse(user.UserSQLData)
                    : BackendResponse<string?>.CreateFailureResponse("No SQL data found for the given user.", System.Net.HttpStatusCode.NotFound);
            }
            catch (Exception)
            {
                return BackendResponse<string?>.CreateFailureResponse("An error occurred when retrieving SQL data.");
            }
        }

        public async Task<BackendResponse<string?>> ConvertQueryToSQL(ApiRequest request, string apiKey)
        {
            try
            {
                using var transaction = await _dbContext.Database.BeginTransactionAsync();

                var encryptedApiKey = HashApiKey(apiKey);
                var user = await _dbContext.UserData.FirstOrDefaultAsync(u => u.EncryptedApiKey == encryptedApiKey);
                if (user == null)
                {
                    return BackendResponse<string?>.CreateFailureResponse("Invalid API key.");
                }

                var today = DateTime.UtcNow.Date;
                if (user.LastRequestDate != today)
                {
                    user.RequestCount = 1;
                    user.LastRequestDate = today;
                }
                else
                {
                    user.RequestCount++;
                }

                if (!user.IsPayingCustomer && user.RequestCount > _maxRequestsPerDay)
                {
                    return BackendResponse<string?>.CreateFailureResponse(
                        "You have reached the maximum number of requests allowed per day.",
                        System.Net.HttpStatusCode.TooManyRequests);
                }

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                if (user.IsPayingCustomer && user.RequestCount > _maxRequestsPerDay)
                {
                    RecordUsageWithStripe(user.StripeCustomerId, 1);
                }

                var completionContent = request.Query + $"\n\n. For context, the current time in UTC is {DateTime.UtcNow}, and I provided my current table definitions below:\n\n{user.UserSQLData}";

                var completionRequest = new CompletionRequest
                {
                    ProfileOptions = new Profile { Name = _sqlConversionProfile },
                    Messages = new List<Message>() { new Message() { Content = completionContent, Role = Role.User, TimeStamp = DateTime.UtcNow } }
                };

                var completionResponse = await _aiClient.ChatAsync(completionRequest);
                var responseContent = completionResponse.Messages?.LastOrDefault()?.Content;

                var cleanedResponseContent = responseContent?.Replace("```sql", "").Replace("```", "");

                return !string.IsNullOrEmpty(cleanedResponseContent)
                    ? BackendResponse<string?>.CreateSuccessResponse(cleanedResponseContent)
                    : BackendResponse<string?>.CreateFailureResponse("No response received from the AI client.");
            }
            catch (DbUpdateConcurrencyException)
            {
                return BackendResponse<string?>.CreateFailureResponse("A concurrency error occurred. Please retry the request.");
            }
            catch (Exception)
            {
                return BackendResponse<string?>.CreateFailureResponse("An error occurred.");
            }
        }

        private string HashApiKey(string apiKey)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(apiKey));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        private void RecordUsageWithStripe(string customerId, long quantity)
        {
            StripeConfiguration.ApiKey = _stripeKey;
            var options = new Stripe.Billing.MeterEventCreateOptions
            {
                EventName = _meterName,
                Payload = new Dictionary<string, string>
                {
                    { "value", $"{quantity}" },
                    { "stripe_customer_id", customerId },
                },
            };
            var service = new Stripe.Billing.MeterEventService();
            service.Create(options);
        }
    }
}
