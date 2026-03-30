using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdvisorLeads.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBrokerCheckSroAndPdfFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AdvisorRegistrations_AdvisorId_StateCode_RegistrationCategory",
                table: "AdvisorRegistrations");

            migrationBuilder.AddColumn<string>(
                name: "BrokerCheckReportPdfUrl",
                table: "Advisors",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationType",
                table: "AdvisorRegistrations",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SroName",
                table: "AdvisorRegistrations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdvisorRegistrations_AdvisorId_RegistrationType_StateCode_RegistrationCategory",
                table: "AdvisorRegistrations",
                columns: new[] { "AdvisorId", "RegistrationType", "StateCode", "RegistrationCategory" });

            migrationBuilder.CreateIndex(
                name: "IX_AdvisorRegistrations_RegistrationType",
                table: "AdvisorRegistrations",
                column: "RegistrationType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AdvisorRegistrations_AdvisorId_RegistrationType_StateCode_RegistrationCategory",
                table: "AdvisorRegistrations");

            migrationBuilder.DropIndex(
                name: "IX_AdvisorRegistrations_RegistrationType",
                table: "AdvisorRegistrations");

            migrationBuilder.DropColumn(
                name: "BrokerCheckReportPdfUrl",
                table: "Advisors");

            migrationBuilder.DropColumn(
                name: "RegistrationType",
                table: "AdvisorRegistrations");

            migrationBuilder.DropColumn(
                name: "SroName",
                table: "AdvisorRegistrations");

            migrationBuilder.CreateIndex(
                name: "IX_AdvisorRegistrations_AdvisorId_StateCode_RegistrationCategory",
                table: "AdvisorRegistrations",
                columns: new[] { "AdvisorId", "StateCode", "RegistrationCategory" },
                unique: true);
        }
    }
}
