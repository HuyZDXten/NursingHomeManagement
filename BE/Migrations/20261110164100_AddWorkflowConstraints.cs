using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NursingHomeManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_MedicationLog_Status",
                table: "MedicationLogs",
                sql: "[Status] BETWEEN 0 AND 3");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invoice_Status",
                table: "Invoices",
                sql: "[Status] BETWEEN 0 AND 4");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CareTask_Status",
                table: "CareTasks",
                sql: "[Status] BETWEEN 0 AND 3");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Bed_Status",
                table: "Beds",
                sql: "[Status] BETWEEN 0 AND 2");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MedicationLog_Status",
                table: "MedicationLogs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Invoice_Status",
                table: "Invoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CareTask_Status",
                table: "CareTasks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Bed_Status",
                table: "Beds");
        }
    }
}
