using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Stock.Trading.Data;

namespace Stock.Trading.Migrations
{
    [DbContext(typeof(TradingDbContext))]
    [Migration("20170812145125_new")]
    partial class @new
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                .HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn)
                .HasAnnotation("ProductVersion", "1.1.2");

            modelBuilder.Entity("Stock.Trading.Data.Entities.OrderType", b =>
                {
                    b.Property<string>("Code")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Name")
                        .IsRequired();

                    b.HasKey("Code");

                    b.ToTable("OrderTypes");
                });

            modelBuilder.Entity("Stock.Trading.Entities.Ask", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("CurrencyPairId")
                        .IsRequired();

                    b.Property<decimal>("Fulfilled");

                    b.Property<DateTime>("OrderDateUtc");

                    b.Property<string>("OrderTypeCode")
                        .IsRequired();

                    b.Property<decimal>("Price");

                    b.Property<string>("UserId")
                        .IsRequired();

                    b.Property<decimal>("Volume");

                    b.HasKey("Id");

                    b.HasIndex("OrderTypeCode");

                    b.ToTable("Asks");
                });

            modelBuilder.Entity("Stock.Trading.Entities.Bid", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("CurrencyPairId")
                        .IsRequired();

                    b.Property<decimal>("Fulfilled");

                    b.Property<DateTime>("OrderDateUtc");

                    b.Property<string>("OrderTypeCode")
                        .IsRequired();

                    b.Property<decimal>("Price");

                    b.Property<string>("UserId")
                        .IsRequired();

                    b.Property<decimal>("Volume");

                    b.HasKey("Id");

                    b.HasIndex("OrderTypeCode");

                    b.ToTable("Bids");
                });

            modelBuilder.Entity("Stock.Trading.Models.Deal", b =>
                {
                    b.Property<Guid>("DealId")
                        .ValueGeneratedOnAdd();

                    b.Property<Guid>("AskId");

                    b.Property<Guid>("BidId");

                    b.Property<DateTime>("DealDateUtc");

                    b.Property<decimal>("Price");

                    b.Property<decimal>("Volume");

                    b.HasKey("DealId");

                    b.HasIndex("AskId");

                    b.HasIndex("BidId");

                    b.ToTable("Deals");
                });

            modelBuilder.Entity("Stock.Trading.Entities.Ask", b =>
                {
                    b.HasOne("Stock.Trading.Data.Entities.OrderType", "OrderType")
                        .WithMany()
                        .HasForeignKey("OrderTypeCode")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("Stock.Trading.Entities.Bid", b =>
                {
                    b.HasOne("Stock.Trading.Data.Entities.OrderType", "OrderType")
                        .WithMany()
                        .HasForeignKey("OrderTypeCode")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("Stock.Trading.Models.Deal", b =>
                {
                    b.HasOne("Stock.Trading.Entities.Ask", "Ask")
                        .WithMany("DealList")
                        .HasForeignKey("AskId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.HasOne("Stock.Trading.Entities.Bid", "Bid")
                        .WithMany("DealList")
                        .HasForeignKey("BidId")
                        .OnDelete(DeleteBehavior.Cascade);
                });
        }
    }
}
