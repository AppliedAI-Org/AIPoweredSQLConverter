﻿using System.ComponentModel.DataAnnotations;

namespace AIPoweredSQLConverter.DAL.Models
{
    public class UserData
    {
        [Key]
        public required string Username { get; set; }
        public string? EncryptedApiKey { get; set; }
        public DateTime? ApiKeyGeneratedDate { get; set; }
        [MaxLength(4000)]
        public string? UserSQLData { get; set; }
        public bool IsPayingCustomer { get; set; } = false;
        public int RequestCount { get; set; } = 0;
        public DateTime? LastRequestDate { get; set; }
        [Timestamp]
        public byte[] RowVersion { get; set; } = null!;
        public string? StripeCustomerId { get; set; }
    }
}
