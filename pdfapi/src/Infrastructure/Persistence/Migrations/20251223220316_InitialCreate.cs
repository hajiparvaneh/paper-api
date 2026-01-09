using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaperAPI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StripeInvoiceId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    AmountCents = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    InvoiceDate = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    PeriodStart = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    PeriodEnd = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    HostedInvoiceUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    InvoicePdfUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MonthlyPriceCents = table.Column<int>(type: "integer", nullable: false),
                    MaxPdfsPerMonth = table.Column<int>(type: "integer", nullable: false),
                    MaxRequestsPerMinute = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    PriorityWeight = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    LogRetentionDays = table.Column<int>(type: "integer", nullable: false, defaultValue: 7),
                    OveragePricePerThousandCents = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "stripe_webhook_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StripeEventId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Type = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stripe_webhook_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "usage_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApiKeyId = table.Column<Guid>(type: "uuid", nullable: true),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    RequestsCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    PdfCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    BytesGenerated = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usage_records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsEmailVerified = table.Column<bool>(type: "boolean", nullable: false),
                    PendingEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    EmailVerificationToken = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    EmailVerificationTokenExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastVerificationEmailSentAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    StripeCustomerId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BillingAddressLine1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BillingAddressLine2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BillingCity = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    BillingState = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    BillingPostalCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    BillingCountry = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    VatNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    TermsAcceptedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    PrivacyAcknowledgedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    DpaAcknowledgedAtUtc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    AcceptedFromIp = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    AcceptedUserAgent = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "api_keys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    KeyHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Prefix = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_keys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_api_keys_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    StripeSubscriptionId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Interval = table.Column<int>(type: "integer", nullable: false),
                    CurrentPeriodStart = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    CurrentPeriodEnd = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    StripeOverageSubscriptionItemId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    LastOveragePeriodEnd = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    LastOverageQuantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subscriptions_plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subscriptions_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pdf_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApiKeyId = table.Column<Guid>(type: "uuid", nullable: true),
                    Html = table.Column<string>(type: "text", nullable: false),
                    PageSize = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Orientation = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    MarginTop = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: true),
                    MarginRight = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: true),
                    MarginBottom = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: true),
                    MarginLeft = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: true),
                    PrintMediaType = table.Column<bool>(type: "boolean", nullable: true),
                    DisableSmartShrinking = table.Column<bool>(type: "boolean", nullable: true),
                    EnableJavascript = table.Column<bool>(type: "boolean", nullable: true),
                    DisableJavascript = table.Column<bool>(type: "boolean", nullable: true),
                    HeaderLeft = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HeaderCenter = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HeaderRight = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    FooterLeft = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    FooterCenter = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    FooterRight = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HeaderSpacing = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: true),
                    FooterSpacing = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: true),
                    HeaderHtml = table.Column<string>(type: "text", nullable: true),
                    FooterHtml = table.Column<string>(type: "text", nullable: true),
                    Dpi = table.Column<int>(type: "integer", nullable: true),
                    Zoom = table.Column<double>(type: "double precision", precision: 9, scale: 2, nullable: true),
                    ImageDpi = table.Column<int>(type: "integer", nullable: true),
                    ImageQuality = table.Column<int>(type: "integer", nullable: true),
                    LowQuality = table.Column<bool>(type: "boolean", nullable: true),
                    Images = table.Column<bool>(type: "boolean", nullable: true),
                    NoImages = table.Column<bool>(type: "boolean", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PriorityWeight = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    InputSizeBytes = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    OutputSizeBytes = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    DurationMs = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    RetentionDays = table.Column<int>(type: "integer", nullable: false, defaultValue: 7),
                    OutputPath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pdf_jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pdf_jobs_api_keys_ApiKeyId",
                        column: x => x.ApiKeyId,
                        principalTable: "api_keys",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_pdf_jobs_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_UserId_Name",
                table: "api_keys",
                columns: new[] { "UserId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_StripeInvoiceId",
                table: "payments",
                column: "StripeInvoiceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pdf_jobs_ApiKeyId",
                table: "pdf_jobs",
                column: "ApiKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_pdf_jobs_UserId_CreatedAt",
                table: "pdf_jobs",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_plans_Code",
                table: "plans",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stripe_webhook_events_StripeEventId",
                table: "stripe_webhook_events",
                column: "StripeEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_PlanId",
                table: "subscriptions",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_StripeSubscriptionId",
                table: "subscriptions",
                column: "StripeSubscriptionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_UserId",
                table: "subscriptions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_usage_records_UserId_ApiKeyId_Date",
                table: "usage_records",
                columns: new[] { "UserId", "ApiKeyId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_EmailVerificationToken",
                table: "users",
                column: "EmailVerificationToken");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "pdf_jobs");

            migrationBuilder.DropTable(
                name: "stripe_webhook_events");

            migrationBuilder.DropTable(
                name: "subscriptions");

            migrationBuilder.DropTable(
                name: "usage_records");

            migrationBuilder.DropTable(
                name: "api_keys");

            migrationBuilder.DropTable(
                name: "plans");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
