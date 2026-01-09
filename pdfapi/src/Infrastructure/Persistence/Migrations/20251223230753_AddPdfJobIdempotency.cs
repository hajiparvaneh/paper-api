using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaperAPI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPdfJobIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "idempotency_hash",
                table: "pdf_jobs",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "idempotency_key",
                table: "pdf_jobs",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "idempotency_key_expires_at",
                table: "pdf_jobs",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_pdf_jobs_UserId_IdempotencyKey",
                table: "pdf_jobs",
                columns: new[] { "UserId", "idempotency_key" },
                filter: "\"idempotency_key\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_pdf_jobs_UserId_IdempotencyKey",
                table: "pdf_jobs");

            migrationBuilder.DropColumn(
                name: "idempotency_hash",
                table: "pdf_jobs");

            migrationBuilder.DropColumn(
                name: "idempotency_key",
                table: "pdf_jobs");

            migrationBuilder.DropColumn(
                name: "idempotency_key_expires_at",
                table: "pdf_jobs");
        }
    }
}
