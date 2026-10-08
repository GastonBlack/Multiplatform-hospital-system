using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HospitalPlatform.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "EmployeeNumberSequence");

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "text", nullable: false),
                    LastName = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    ProfileType = table.Column<string>(type: "text", nullable: false),
                    AccountStatus = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.UniqueConstraint("AK_Users_Id_ProfileType", x => new { x.Id, x.ProfileType });
                    table.CheckConstraint("CK_Users_AccountStatus", "\"AccountStatus\" IN ('PendingVerification', 'Active', 'Suspended', 'Deactivated')");
                    table.CheckConstraint("CK_Users_ProfileType", "\"ProfileType\" IN ('Patient', 'Doctor', 'Staff')");
                });

            migrationBuilder.CreateTable(
                name: "EmployeeNumbers",
                columns: table => new
                {
                    EmployeeNumber = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeNumbers", x => x.EmployeeNumber);
                    table.UniqueConstraint("AK_EmployeeNumbers_EmployeeNumber_UserId", x => new { x.EmployeeNumber, x.UserId });
                    table.ForeignKey(
                        name: "FK_EmployeeNumbers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Patients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileType = table.Column<string>(type: "text", nullable: false, defaultValue: "Patient"),
                    NationalIdentificationNumber = table.Column<string>(type: "text", nullable: false),
                    PhoneNumber = table.Column<string>(type: "text", nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    IdentityVerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IdentityVerifiedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Patients", x => x.Id);
                    table.CheckConstraint("CK_Patients_IdentityVerification", "(\"IdentityVerifiedAt\" IS NULL AND \"IdentityVerifiedByUserId\" IS NULL) OR (\"IdentityVerifiedAt\" IS NOT NULL AND \"IdentityVerifiedByUserId\" IS NOT NULL)");
                    table.CheckConstraint("CK_Patients_NationalIdentificationNumber", "\"NationalIdentificationNumber\" ~ '^[0-9]{8}$'");
                    table.CheckConstraint("CK_Patients_ProfileType", "\"ProfileType\" = 'Patient'");
                    table.ForeignKey(
                        name: "FK_Patients_Users_IdentityVerifiedByUserId",
                        column: x => x.IdentityVerifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Patients_Users_UserId_ProfileType",
                        columns: x => new { x.UserId, x.ProfileType },
                        principalTable: "Users",
                        principalColumns: new[] { "Id", "ProfileType" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Doctors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileType = table.Column<string>(type: "text", nullable: false, defaultValue: "Doctor"),
                    EmployeeNumber = table.Column<string>(type: "text", nullable: false),
                    MedicalLicenseNumber = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Doctors", x => x.Id);
                    table.CheckConstraint("CK_Doctors_ProfileType", "\"ProfileType\" = 'Doctor'");
                    table.ForeignKey(
                        name: "FK_Doctors_EmployeeNumbers_EmployeeNumber_UserId",
                        columns: x => new { x.EmployeeNumber, x.UserId },
                        principalTable: "EmployeeNumbers",
                        principalColumns: new[] { "EmployeeNumber", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Doctors_Users_UserId_ProfileType",
                        columns: x => new { x.UserId, x.ProfileType },
                        principalTable: "Users",
                        principalColumns: new[] { "Id", "ProfileType" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Staff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileType = table.Column<string>(type: "text", nullable: false, defaultValue: "Staff"),
                    EmployeeNumber = table.Column<string>(type: "text", nullable: false),
                    StaffRole = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Staff", x => x.Id);
                    table.CheckConstraint("CK_Staff_ProfileType", "\"ProfileType\" = 'Staff'");
                    table.CheckConstraint("CK_Staff_StaffRole", "\"StaffRole\" IN ('Receptionist', 'Administrator')");
                    table.ForeignKey(
                        name: "FK_Staff_EmployeeNumbers_EmployeeNumber_UserId",
                        columns: x => new { x.EmployeeNumber, x.UserId },
                        principalTable: "EmployeeNumbers",
                        principalColumns: new[] { "EmployeeNumber", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Staff_Users_UserId_ProfileType",
                        columns: x => new { x.UserId, x.ProfileType },
                        principalTable: "Users",
                        principalColumns: new[] { "Id", "ProfileType" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Doctors_EmployeeNumber",
                table: "Doctors",
                column: "EmployeeNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Doctors_EmployeeNumber_UserId",
                table: "Doctors",
                columns: new[] { "EmployeeNumber", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Doctors_MedicalLicenseNumber",
                table: "Doctors",
                column: "MedicalLicenseNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Doctors_UserId",
                table: "Doctors",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Doctors_UserId_ProfileType",
                table: "Doctors",
                columns: new[] { "UserId", "ProfileType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeNumbers_UserId",
                table: "EmployeeNumbers",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Patients_IdentityVerifiedByUserId",
                table: "Patients",
                column: "IdentityVerifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_NationalIdentificationNumber",
                table: "Patients",
                column: "NationalIdentificationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Patients_UserId",
                table: "Patients",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Patients_UserId_ProfileType",
                table: "Patients",
                columns: new[] { "UserId", "ProfileType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Staff_EmployeeNumber",
                table: "Staff",
                column: "EmployeeNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Staff_EmployeeNumber_UserId",
                table: "Staff",
                columns: new[] { "EmployeeNumber", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Staff_UserId",
                table: "Staff",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Staff_UserId_ProfileType",
                table: "Staff",
                columns: new[] { "UserId", "ProfileType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.Sql("""
                CREATE FUNCTION lock_profile_owners() RETURNS trigger
                LANGUAGE plpgsql AS $$
                DECLARE
                    old_owner uuid;
                    new_owner uuid;
                    owner_id uuid;
                BEGIN
                    IF TG_OP <> 'INSERT' THEN old_owner := OLD."UserId"; END IF;
                    IF TG_OP <> 'DELETE' THEN new_owner := NEW."UserId"; END IF;
                    FOR owner_id IN
                        SELECT DISTINCT value FROM unnest(ARRAY[old_owner, new_owner]) AS owners(value)
                        WHERE value IS NOT NULL ORDER BY value
                    LOOP
                        PERFORM 1 FROM "Users" WHERE "Id" = owner_id FOR UPDATE;
                    END LOOP;
                    IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
                    RETURN NEW;
                END;
                $$;

                CREATE FUNCTION check_user_profile() RETURNS trigger
                LANGUAGE plpgsql AS $$
                DECLARE
                    old_owner uuid;
                    new_owner uuid;
                    owner_id uuid;
                    profile_type text;
                    has_profile boolean;
                BEGIN
                    IF TG_TABLE_NAME = 'Users' THEN
                        IF TG_OP <> 'INSERT' THEN old_owner := OLD."Id"; END IF;
                        IF TG_OP <> 'DELETE' THEN new_owner := NEW."Id"; END IF;
                    ELSE
                        IF TG_OP <> 'INSERT' THEN old_owner := OLD."UserId"; END IF;
                        IF TG_OP <> 'DELETE' THEN new_owner := NEW."UserId"; END IF;
                    END IF;
                    FOR owner_id IN
                        SELECT DISTINCT value FROM unnest(ARRAY[old_owner, new_owner]) AS owners(value)
                        WHERE value IS NOT NULL ORDER BY value
                    LOOP
                        SELECT "ProfileType" INTO profile_type FROM "Users"
                        WHERE "Id" = owner_id FOR UPDATE;
                        IF NOT FOUND THEN CONTINUE; END IF;
                        -- Keys enforce at most one; this deferred check enforces existence.
                        SELECT CASE profile_type
                            WHEN 'Patient' THEN EXISTS (SELECT 1 FROM "Patients" WHERE "UserId" = owner_id)
                            WHEN 'Doctor' THEN EXISTS (SELECT 1 FROM "Doctors" WHERE "UserId" = owner_id)
                            WHEN 'Staff' THEN EXISTS (SELECT 1 FROM "Staff" WHERE "UserId" = owner_id)
                            ELSE false
                        END INTO has_profile;
                        IF NOT has_profile THEN
                            RAISE EXCEPTION 'User % must have exactly one matching profile', owner_id
                                USING ERRCODE = '23514', CONSTRAINT = 'CK_Users_ExactlyOneProfile';
                        END IF;
                    END LOOP;
                    RETURN NULL;
                END;
                $$;

                CREATE FUNCTION reject_account_truncate() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'TRUNCATE is not allowed on account and profile tables'
                        USING ERRCODE = '23514';
                END;
                $$;

                CREATE CONSTRAINT TRIGGER "CT_Users_ExactlyOneProfile"
                    AFTER INSERT OR UPDATE ON "Users"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION check_user_profile();

                CREATE CONSTRAINT TRIGGER "CT_Patients_ExactlyOneProfile"
                    AFTER INSERT OR UPDATE OR DELETE ON "Patients"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION check_user_profile();
                CREATE CONSTRAINT TRIGGER "CT_Doctors_ExactlyOneProfile"
                    AFTER INSERT OR UPDATE OR DELETE ON "Doctors"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION check_user_profile();
                CREATE CONSTRAINT TRIGGER "CT_Staff_ExactlyOneProfile"
                    AFTER INSERT OR UPDATE OR DELETE ON "Staff"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION check_user_profile();

                CREATE TRIGGER "TR_Patients_LockOwners" BEFORE INSERT OR UPDATE OR DELETE ON "Patients"
                    FOR EACH ROW EXECUTE FUNCTION lock_profile_owners();
                CREATE TRIGGER "TR_Doctors_LockOwners" BEFORE INSERT OR UPDATE OR DELETE ON "Doctors"
                    FOR EACH ROW EXECUTE FUNCTION lock_profile_owners();
                CREATE TRIGGER "TR_Staff_LockOwners" BEFORE INSERT OR UPDATE OR DELETE ON "Staff"
                    FOR EACH ROW EXECUTE FUNCTION lock_profile_owners();

                CREATE TRIGGER "TR_Users_NoTruncate" BEFORE TRUNCATE ON "Users"
                    FOR EACH STATEMENT EXECUTE FUNCTION reject_account_truncate();
                CREATE TRIGGER "TR_Patients_NoTruncate" BEFORE TRUNCATE ON "Patients"
                    FOR EACH STATEMENT EXECUTE FUNCTION reject_account_truncate();
                CREATE TRIGGER "TR_Doctors_NoTruncate" BEFORE TRUNCATE ON "Doctors"
                    FOR EACH STATEMENT EXECUTE FUNCTION reject_account_truncate();
                CREATE TRIGGER "TR_Staff_NoTruncate" BEFORE TRUNCATE ON "Staff"
                    FOR EACH STATEMENT EXECUTE FUNCTION reject_account_truncate();
                CREATE TRIGGER "TR_EmployeeNumbers_NoTruncate" BEFORE TRUNCATE ON "EmployeeNumbers"
                    FOR EACH STATEMENT EXECUTE FUNCTION reject_account_truncate();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Doctors");

            migrationBuilder.DropTable(
                name: "Patients");

            migrationBuilder.DropTable(
                name: "Staff");

            migrationBuilder.DropTable(
                name: "EmployeeNumbers");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropSequence(
                name: "EmployeeNumberSequence");

            migrationBuilder.Sql("""
                DROP FUNCTION check_user_profile();
                DROP FUNCTION lock_profile_owners();
                DROP FUNCTION reject_account_truncate();
                """);
        }
    }
}
