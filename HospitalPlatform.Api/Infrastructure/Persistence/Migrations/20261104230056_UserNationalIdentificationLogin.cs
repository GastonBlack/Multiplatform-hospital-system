using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HospitalPlatform.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UserNationalIdentificationLogin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NationalIdentificationNumber",
                table: "Users",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Users" AS account
                SET "NationalIdentificationNumber" = patient."NationalIdentificationNumber"
                FROM "Patients" AS patient
                WHERE account."Id" = patient."UserId";

                -- Flush deferred profile checks before subsequent schema changes.
                SET CONSTRAINTS ALL IMMEDIATE;
                SET CONSTRAINTS ALL DEFERRED;
                """);

            migrationBuilder.DropIndex(
                name: "IX_Patients_NationalIdentificationNumber",
                table: "Patients");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Patients_NationalIdentificationNumber",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "NationalIdentificationNumber",
                table: "Patients");

            migrationBuilder.CreateIndex(
                name: "IX_Users_NationalIdentificationNumber",
                table: "Users",
                column: "NationalIdentificationNumber",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_NationalIdentificationNumber",
                table: "Users",
                sql: "\"NationalIdentificationNumber\" IS NULL OR \"NationalIdentificationNumber\" ~ '^[0-9]{8}$'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NationalIdentificationNumber",
                table: "Patients",
                type: "text",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Patients" AS patient
                SET "NationalIdentificationNumber" = account."NationalIdentificationNumber"
                FROM "Users" AS account
                WHERE patient."UserId" = account."Id";

                SET CONSTRAINTS ALL IMMEDIATE;
                SET CONSTRAINTS ALL DEFERRED;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "NationalIdentificationNumber",
                table: "Patients",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.DropIndex(
                name: "IX_Users_NationalIdentificationNumber",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_NationalIdentificationNumber",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NationalIdentificationNumber",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_NationalIdentificationNumber",
                table: "Patients",
                column: "NationalIdentificationNumber",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Patients_NationalIdentificationNumber",
                table: "Patients",
                sql: "\"NationalIdentificationNumber\" ~ '^[0-9]{8}$'");
        }
    }
}
