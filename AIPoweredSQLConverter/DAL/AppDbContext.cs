﻿using Microsoft.EntityFrameworkCore;
using AIPoweredSQLConverter.DAL.Models;
using Polly;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace AIPoweredSQLConverter.DAL
{
    public class AppDbContext : DbContext
    {
        public DbSet<UserData> UserData { get; set; } = null!;
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserData>(entity =>
            {
                entity.HasKey(e => e.Username);
                entity.Property(e => e.EncryptedApiKey).IsRequired(false);
                entity.Property(e => e.ApiKeyGeneratedDate).IsRequired(false);
                entity.Property(e => e.UserSQLData).HasMaxLength(4000).IsRequired(false);
                entity.Property(e => e.IsPayingCustomer).IsRequired().HasDefaultValue(false);
                entity.Property(e => e.RequestCount).IsRequired().HasDefaultValue(0);
                entity.Property(e => e.LastRequestDate).IsRequired(false);
                entity.Property(e => e.RowVersion).IsRowVersion();
                entity.Property(e => e.StripeCustomerId).IsRequired(false);
            });
        }
    }
}
