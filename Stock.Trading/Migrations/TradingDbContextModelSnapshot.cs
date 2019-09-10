﻿// <auto-generated />
using System;
using MatchingEngine.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Stock.Trading.Migrations
{
    [DbContext(typeof(TradingDbContext))]
    partial class TradingDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn)
                .HasAnnotation("ProductVersion", "2.2.2-servicing-10034")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            modelBuilder.Entity("MatchingEngine.Models.Ask", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<decimal>("Amount");

                    b.Property<decimal>("Blocked");

                    b.Property<string>("CurrencyPairCode")
                        .IsRequired();

                    b.Property<DateTimeOffset>("DateCreated");

                    b.Property<int>("Exchange");

                    b.Property<bool>("FromInnerTradingBot");

                    b.Property<decimal>("Fulfilled");

                    b.Property<bool>("IsBid");

                    b.Property<bool>("IsCanceled");

                    b.Property<decimal>("Price");

                    b.Property<string>("UserId")
                        .IsRequired();

                    b.HasKey("Id");

                    b.ToTable("AsksV2");
                });

            modelBuilder.Entity("MatchingEngine.Models.Bid", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<decimal>("Amount");

                    b.Property<decimal>("Blocked");

                    b.Property<string>("CurrencyPairCode")
                        .IsRequired();

                    b.Property<DateTimeOffset>("DateCreated");

                    b.Property<int>("Exchange");

                    b.Property<bool>("FromInnerTradingBot");

                    b.Property<decimal>("Fulfilled");

                    b.Property<bool>("IsBid");

                    b.Property<bool>("IsCanceled");

                    b.Property<decimal>("Price");

                    b.Property<string>("UserId")
                        .IsRequired();

                    b.HasKey("Id");

                    b.ToTable("BidsV2");
                });

            modelBuilder.Entity("MatchingEngine.Models.Deal", b =>
                {
                    b.Property<Guid>("DealId")
                        .ValueGeneratedOnAdd();

                    b.Property<Guid>("AskId");

                    b.Property<Guid>("BidId");

                    b.Property<DateTimeOffset>("DateCreated");

                    b.Property<bool>("FromInnerTradingBot");

                    b.Property<decimal>("Price");

                    b.Property<decimal>("Volume");

                    b.HasKey("DealId");

                    b.HasIndex("AskId");

                    b.HasIndex("BidId");

                    b.ToTable("DealsV2");
                });

            modelBuilder.Entity("MatchingEngine.Models.Deal", b =>
                {
                    b.HasOne("MatchingEngine.Models.Ask", "Ask")
                        .WithMany("DealList")
                        .HasForeignKey("AskId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.HasOne("MatchingEngine.Models.Bid", "Bid")
                        .WithMany("DealList")
                        .HasForeignKey("BidId")
                        .OnDelete(DeleteBehavior.Cascade);
                });
#pragma warning restore 612, 618
        }
    }
}
