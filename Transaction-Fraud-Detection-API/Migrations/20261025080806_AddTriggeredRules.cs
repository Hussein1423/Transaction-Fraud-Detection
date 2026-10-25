using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Transaction_Fraud_Detection_API.Migrations
{
    /// <inheritdoc />
    public partial class AddTriggeredRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TriggeredRules",
                columns: table => new
                {
                    TriggeredRuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    FraudEvaluationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PointsAwarded = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TriggeredRules", x => x.TriggeredRuleId);
                    table.ForeignKey(
                        name: "FK_TriggeredRules_FraudEvaluations_FraudEvaluationId",
                        column: x => x.FraudEvaluationId,
                        principalTable: "FraudEvaluations",
                        principalColumn: "FraudEvaluationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TriggeredRules_FraudEvaluationId",
                table: "TriggeredRules",
                column: "FraudEvaluationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TriggeredRules");
        }
    }
}
