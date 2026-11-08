using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Transaction_Fraud_Detection_API.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Transactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "Transactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_UserId_Timestamp",
                table: "Transactions",
                columns: new[] { "UserId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_UserId_Timestamp",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "Transactions");
        }
    }
}
