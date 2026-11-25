using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TreeGraph.IdentityAdmin.Migrations;

/// <inheritdoc />
public partial class _20261122074108_InitialIdentityAdmin : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "admin_audit_log",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                actor_sub = table.Column<string>(type: "text", nullable: false),
                actor_name = table.Column<string>(type: "text", nullable: true),
                service_client_id = table.Column<string>(type: "text", nullable: true),
                action = table.Column<string>(type: "text", nullable: false),
                target_type = table.Column<string>(type: "text", nullable: true),
                target_id = table.Column<string>(type: "text", nullable: true),
                request_json = table.Column<string>(type: "jsonb", nullable: true),
                result = table.Column<string>(type: "text", nullable: false),
                keycloak_status = table.Column<int>(type: "integer", nullable: true),
                correlation_id = table.Column<string>(type: "text", nullable: true),
                error = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_admin_audit_log", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "realm_backup",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                realm = table.Column<string>(type: "text", nullable: false),
                file_name = table.Column<string>(type: "text", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                actor_sub = table.Column<string>(type: "text", nullable: false),
                action = table.Column<string>(type: "text", nullable: false),
                payload_protected = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_realm_backup", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_admin_audit_log_action",
            table: "admin_audit_log",
            column: "action");

        migrationBuilder.CreateIndex(
            name: "ix_admin_audit_log_actor_sub",
            table: "admin_audit_log",
            column: "actor_sub");

        migrationBuilder.CreateIndex(
            name: "ix_admin_audit_log_occurred_at",
            table: "admin_audit_log",
            column: "occurred_at");

        migrationBuilder.CreateIndex(
            name: "ix_admin_audit_log_result",
            table: "admin_audit_log",
            column: "result");

        migrationBuilder.CreateIndex(
            name: "ix_realm_backup_created_at",
            table: "realm_backup",
            column: "created_at");

        migrationBuilder.CreateIndex(
            name: "ix_realm_backup_realm",
            table: "realm_backup",
            column: "realm");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "admin_audit_log");

        migrationBuilder.DropTable(
            name: "realm_backup");
    }
}
