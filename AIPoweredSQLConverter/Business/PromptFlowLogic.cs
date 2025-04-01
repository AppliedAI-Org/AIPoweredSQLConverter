using AIPoweredSQLConverter.API;
using AIPoweredSQLConverter.Client.IntelligenceHub;
using AIPoweredSQLConverter.DAL;
using AIPoweredSQLConverter.DAL.Models;
using AIPoweredSQLConverter.Host.Config;
using Microsoft.EntityFrameworkCore;
using Stripe;
using System.Security.Cryptography;
using System.Text;

namespace AIPoweredSQLConverter.Business
{
    public class PromptFlowLogic : IPromptFlowLogic
    {
        private readonly IAIClientWrapper _aiClient;
        private readonly AppDbContext _dbContext;

        // Maximum number of requests allowed per day for non-paying users
        private const int _maxRequestsPerDay = 20;

        private const string _sqlDataConstructionProfile = "NLSequelDataConstructionHelper";
        private const string _sqlConversionProfile = "NLSequelConverter";
        private const string _sqlValidationProfile = "SqlValidator";

        private readonly string _meterName;
        private readonly string _stripeKey;

        public PromptFlowLogic(IAIClientWrapper aiClient, AppDbContext context, StripeSettings settings)
        {
            _aiClient = aiClient;
            _dbContext = context;

            _meterName = settings.MeterName;
            _stripeKey = settings.SecretKey;
        }

        public async Task<BackendResponse<UserData?>> GetUser(string username)
        {
            try
            {
                var user = await _dbContext.UserData.FirstOrDefaultAsync(u => u.Username == username);
                if (user == null)
                {
                    return BackendResponse<UserData?>.CreateFailureResponse("User not found.", System.Net.HttpStatusCode.NotFound);
                }
                return BackendResponse<UserData?>.CreateSuccessResponse(user, "User profile retrieved successfully.");
            }
            catch (Exception)
            {
                return BackendResponse<UserData?>.CreateFailureResponse($"Error retrieving user.");
            }
        }

        public async Task<BackendResponse<bool>> MarkUserAsPaying(string username, string customerId)
        {
            try
            {
                var user = await _dbContext.UserData.FirstOrDefaultAsync(u => u.Username == username);
                if (user == null) return BackendResponse<bool>.CreateFailureResponse("User not found.");
                user.IsPayingCustomer = true;
                user.StripeCustomerId = customerId;
                _dbContext.UserData.Update(user);
                await _dbContext.SaveChangesAsync();
                return BackendResponse<bool>.CreateSuccessResponse(true, "User updated to paying status.");
            }
            catch (Exception)
            {
                return BackendResponse<bool>.CreateFailureResponse($"Error updating user.");
            }
        }

        public async Task<BackendResponse<bool>> MarkUserAsNonPaying(string customerId)
        {
            try
            {
                var user = await _dbContext.UserData.FirstOrDefaultAsync(u => u.StripeCustomerId == customerId);
                if (user == null) return BackendResponse<bool>.CreateFailureResponse("User not found.");
                user.IsPayingCustomer = false;
                _dbContext.UserData.Update(user);
                await _dbContext.SaveChangesAsync();
                return BackendResponse<bool>.CreateSuccessResponse(true, "User updated to non-paying status.");
            }
            catch (Exception)
            {
                return BackendResponse<bool>.CreateFailureResponse($"Error updating user.");
            }
        }

        public async Task<BackendResponse<bool>> SaveNewUser(string sub)
        {
            try
            {
                var existingUser = await _dbContext.UserData.FirstOrDefaultAsync(u => u.Username == sub);
                if (existingUser != null)
                {
                    return BackendResponse<bool>.CreateSuccessResponse(true);
                }

                var newUser = new UserData
                {
                    Username = sub,
                    IsPayingCustomer = false,
                    RequestCount = 0,
                    LastRequestDate = null,
                    RowVersion = new byte[0]
                };

                await _dbContext.UserData.AddAsync(newUser);
                await _dbContext.SaveChangesAsync();

                return BackendResponse<bool>.CreateSuccessResponse(true, "User saved successfully.");
            }
            catch (Exception)
            {
                return BackendResponse<bool>.CreateFailureResponse($"Error saving user.");
            }
        }

        public async Task<BackendResponse<bool>> UpsertUserSQLData(FrontEndRequest request)
        {
            try
            {
                var existingUser = _dbContext.UserData.FirstOrDefault(user => user.Username == request.Username);

                // Add or update existing user data
                if (existingUser != null)
                {
                    existingUser.UserSQLData = request.SqlData;
                    _dbContext.UserData.Update(existingUser);
                }
                else
                {
                    var newUser = new UserData { Username = request.Username, UserSQLData = request.SqlData };
                    await _dbContext.UserData.AddAsync(newUser);
                }

                // Save changes and return success response
                var changes = await _dbContext.SaveChangesAsync();
                if (changes > 0) return BackendResponse<bool>.CreateSuccessResponse(true, "User data updated successfully.");
                else return BackendResponse<bool>.CreateFailureResponse("No changes were made to the database.");
            }
            catch (Exception)
            {
                return BackendResponse<bool>.CreateFailureResponse($"An error occurred when updating user data.");
            }
        }

        public BackendResponse<string?> GetSQLData(string username)
        {
            try
            {
               var result = _dbContext.UserData.FirstOrDefault(user => user.Username == username);
                if (result != null && !string.IsNullOrEmpty(result.UserSQLData)) return BackendResponse<string?>.CreateSuccessResponse(result.UserSQLData);
                else return BackendResponse<string?>.CreateFailureResponse("No SQL data found for the given user.", System.Net.HttpStatusCode.NotFound);
            }
            catch (Exception)
            {
                return BackendResponse<string?>.CreateFailureResponse($"An error occurred when retrieving SQL data.");
            }
        }

        public async Task<BackendResponse<string?>> GetSQLDataHelp(FrontEndRequest request)
        {
            try
            {
                using var transaction = await _dbContext.Database.BeginTransactionAsync();

                var userData = await _dbContext.UserData
                    .Where(user => user.Username == request.Username)
                    .FirstOrDefaultAsync();

                if (userData == null)
                {
                    return BackendResponse<string?>.CreateFailureResponse("User data not found.");
                }

                var today = DateTime.UtcNow.Date;
                if (userData.LastRequestDate != today)
                {
                    userData.RequestCount = 1;
                    userData.LastRequestDate = today;
                }
                else
                {
                    userData.RequestCount++;
                }

                if (!userData.IsPayingCustomer && userData.RequestCount > _maxRequestsPerDay)
                {
                    return BackendResponse<string?>.CreateFailureResponse(
                        "You have reached the maximum number of requests allowed per day.",
                        System.Net.HttpStatusCode.TooManyRequests);
                }

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                if (userData.IsPayingCustomer && userData.RequestCount > _maxRequestsPerDay)
                {
                    RecordUsageWithStripe(userData.StripeCustomerId, 1);
                }

                var completionContent = $"{request.Query}. For context, I provided my current table definitions below:\n\n{userData.UserSQLData}";

                var completionRequest = new CompletionRequest
                {
                    ProfileOptions = new Profile { Name = _sqlDataConstructionProfile },
                    Messages = new List<Message>
                    {
                        new Message
                        {
                            Content = completionContent,
                            Role = Role.User,
                            TimeStamp = DateTime.UtcNow
                        }
                    }
                };

                var completionResponse = await _aiClient.ChatAsync(completionRequest);
                var responseContent = completionResponse.Messages?.LastOrDefault()?.Content;

                if (!string.IsNullOrEmpty(responseContent))
                {
                    return BackendResponse<string?>.CreateSuccessResponse(responseContent);
                }
                else
                {
                    return BackendResponse<string?>.CreateFailureResponse("No response received from the AI client.");
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                return BackendResponse<string?>.CreateFailureResponse("A concurrency error occurred. Please retry the request.");
            }
            catch (Exception)
            {
                return BackendResponse<string?>.CreateFailureResponse($"An error occurred.");
            }
        }

        public async Task<BackendResponse<string?>> ConvertQueryToSQL(FrontEndRequest request)
        {
            try
            {
                using var transaction = await _dbContext.Database.BeginTransactionAsync();

                var userData = await _dbContext.UserData
                    .Where(user => user.Username == request.Username)
                    .FirstOrDefaultAsync();

                if (userData == null)
                {
                    return BackendResponse<string?>.CreateFailureResponse("User data not found.");
                }

                var today = DateTime.UtcNow.Date;
                if (userData.LastRequestDate != today)
                {
                    userData.RequestCount = 1;
                    userData.LastRequestDate = today;
                }
                else
                {
                    userData.RequestCount++;
                }

                if (!userData.IsPayingCustomer && userData.RequestCount > _maxRequestsPerDay)
                {
                    return BackendResponse<string?>.CreateFailureResponse(
                        "You have reached the maximum number of requests allowed per day.",
                        System.Net.HttpStatusCode.TooManyRequests);
                }

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                if (userData.IsPayingCustomer && userData.RequestCount > _maxRequestsPerDay)
                {
                    RecordUsageWithStripe(userData.StripeCustomerId, 1);
                }

                var completionContent = $"\n\nThe current time in UTC is {DateTime.UtcNow}. For context, I provided my current table definitions below:\n\n{userData.UserSQLData}";
                if (request.Messages.Any(x => x.Role == Role.User))
                {
                    request.Messages.Last(x => x.Role == Role.User).Content += completionContent;
                }
                else
                {
                    return BackendResponse<string?>.CreateFailureResponse("No user message found in the conversation.");
                }

                var completionRequest = new CompletionRequest
                {
                    ProfileOptions = new Profile { Name = _sqlConversionProfile },
                    Messages = request.Messages
                };

                var completionResponse = await _aiClient.ChatAsync(completionRequest);
                var responseContent = completionResponse.Messages?.LastOrDefault()?.Content;

                var validationRequest = new CompletionRequest()
                {
                    ProfileOptions = new Profile() { Name = _sqlValidationProfile },
                    Messages = new List<Message>() { new Message() { Content = responseContent, Role = Role.User } }
                };
                var validationResponse = await _aiClient.ChatAsync(validationRequest);
                var cleanedResponseContent = validationRequest.Messages?.LastOrDefault()?.Content;

                if (!string.IsNullOrEmpty(cleanedResponseContent))
                {
                    return BackendResponse<string?>.CreateSuccessResponse(cleanedResponseContent);
                }
                else
                {
                    return BackendResponse<string?>.CreateFailureResponse("No response received from the AI client.");
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                return BackendResponse<string?>.CreateFailureResponse("A concurrency error occurred. Please retry the request.");
            }
            catch (Exception)
            {
                return BackendResponse<string?>.CreateFailureResponse($"An error occurred.");
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

        public async Task<BackendResponse<string>> GenerateAndStoreApiKey(string username)
        {
            try
            {
                var user = await _dbContext.UserData.FirstOrDefaultAsync(u => u.Username == username);
                if (user == null)
                {
                    return BackendResponse<string>.CreateFailureResponse("User not found.", System.Net.HttpStatusCode.NotFound);
                }

                // Generate a new API key
                var apiKey = GenerateApiKey();

                // Encrypt the API key
                var encryptedApiKey = HashApiKey(apiKey);

                // Update user profile
                user.EncryptedApiKey = encryptedApiKey;
                user.ApiKeyGeneratedDate = DateTime.UtcNow;

                _dbContext.UserData.Update(user);
                await _dbContext.SaveChangesAsync();

                return BackendResponse<string>.CreateSuccessResponse(apiKey, "API key generated and stored successfully.");
            }
            catch (Exception)
            {
                return BackendResponse<string>.CreateFailureResponse($"Error generating API key.");
            }
        }

        private string GenerateApiKey()
        {
            var apiKeyBytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(apiKeyBytes);
        }

        private string HashApiKey(string apiKey)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(apiKey));
                return Convert.ToBase64String(hashedBytes);
            }
        }
    }
}
