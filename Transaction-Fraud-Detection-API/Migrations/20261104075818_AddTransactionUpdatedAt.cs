using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Transaction_Fraud_Detection_API.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionUpdatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "Transactions",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Transactions");
        }
    }
}
