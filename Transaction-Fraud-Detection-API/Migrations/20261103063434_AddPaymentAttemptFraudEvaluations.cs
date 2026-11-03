using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Transaction_Fraud_Detection_API.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentAttemptFraudEvaluations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentAttemptFraudEvaluations",
                columns: table => new
                {
                    PaymentAttemptFraudEvaluationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentAttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    RiskScore = table.Column<int>(type: "integer", nullable: false),
                    Decision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EvaluatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAttemptFraudEvaluations", x => x.PaymentAttemptFraudEvaluationId);
                    table.ForeignKey(
                        name: "FK_PaymentAttemptFraudEvaluations_PaymentAttempts_PaymentAttem~",
                        column: x => x.PaymentAttemptId,
                        principalTable: "PaymentAttempts",
                        principalColumn: "PaymentAttemptId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentAttemptTriggeredRules",
                columns: table => new
                {
                    PaymentAttemptTriggeredRuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentAttemptFraudEvaluationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PointsAwarded = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAttemptTriggeredRules", x => x.PaymentAttemptTriggeredRuleId);
                    table.ForeignKey(
                        name: "FK_PaymentAttemptTriggeredRules_PaymentAttemptFraudEvaluations~",
                        column: x => x.PaymentAttemptFraudEvaluationId,
                        principalTable: "PaymentAttemptFraudEvaluations",
                        principalColumn: "PaymentAttemptFraudEvaluationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAttemptFraudEvaluations_PaymentAttemptId",
                table: "PaymentAttemptFraudEvaluations",
                column: "PaymentAttemptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAttemptTriggeredRules_PaymentAttemptFraudEvaluationId",
                table: "PaymentAttemptTriggeredRules",
                column: "PaymentAttemptFraudEvaluationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentAttemptTriggeredRules");

            migrationBuilder.DropTable(
                name: "PaymentAttemptFraudEvaluations");
        }
    }
}
