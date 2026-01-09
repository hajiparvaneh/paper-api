using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using PaperAPI.Application.Access.Repositories;
using PaperAPI.Application.Billing.Dtos;
using PaperAPI.Application.Billing.Exceptions;
using PaperAPI.Application.Billing.Repositories;
using PaperAPI.Application.Billing.Requests;
using PaperAPI.Application.Billing.Services;
using PaperAPI.Application.Email;
using PaperAPI.Application.Identity.Repositories;
using PaperAPI.Domain.Billing;
using PaperAPI.Domain.Identity;
using PaperAPI.Infrastructure.Options;
using PaperAPI.Infrastructure.Services;
using Stripe;
using Stripe.Billing;
using Stripe.BillingPortal;
using Stripe.Checkout;
using BillingPlan = PaperAPI.Domain.Billing.Plan;
using BillingPortalSessionService = Stripe.BillingPortal.SessionService;
using CheckoutSession = Stripe.Checkout.Session;
using CheckoutSessionService = Stripe.Checkout.SessionService;
using StripeInvoice = Stripe.Invoice;
using StripeSubscription = Stripe.Subscription;
using Subscription = PaperAPI.Domain.Billing.Subscription;

namespace PaperAPI.Infrastructure.Billing;

public sealed class StripeBillingService : IBillingService
{
    private const int AnnualMonthsCharged = 10; // 10 months charged for 12 months (2 months free)
    private const int MaxInvoicesToReturn = 24;
    private const int OverageUnitSize = 1000;

    private static class StripeEventTypes
    {
        public const string CheckoutSessionCompleted = "checkout.session.completed";
        public const string CustomerSubscriptionCreated = "customer.subscription.created";
        public const string CustomerSubscriptionUpdated = "customer.subscription.updated";
        public const string CustomerSubscriptionDeleted = "customer.subscription.deleted";
        public const string InvoicePaid = "invoice.paid";
        public const string InvoicePaymentFailed = "invoice.payment_failed";
        public const string InvoiceUpcoming = "invoice.upcoming";
    }

    private readonly IPlanRepository _planRepository;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IUserRepository _userRepository;
    private readonly IStripeWebhookEventRepository _webhookRepository;
    private readonly IUsageRecordRepository _usageRecordRepository;
    private readonly IEmailService _emailService;
    private readonly StripeOptions _stripeOptions;
    private readonly ILogger<StripeBillingService> _logger;
    private readonly StripeClient _stripeClient;
    private readonly CheckoutSessionService _checkoutSessionService;
    private readonly BillingPortalSessionService _billingPortalSessionService;
    private readonly CustomerService _customerService;
    private readonly SubscriptionService _stripeSubscriptionService;
    private readonly InvoiceService _invoiceService;
    private readonly CustomerTaxIdService _customerTaxIdService;
    private readonly SubscriptionItemService _subscriptionItemService;
    private readonly PriceService _priceService;
    private readonly MeterService _meterService;
    private readonly MeterEventService _meterEventService;
    private readonly Dictionary<string, (string PlanCode, BillingInterval Interval)> _priceLookup;
    private readonly Dictionary<string, string> _overagePriceIds;
    private readonly ConcurrentDictionary<string, MeterDescriptor> _meterDescriptors;

    public StripeBillingService(
        IPlanRepository planRepository,
        ISubscriptionRepository subscriptionRepository,
        IPaymentRepository paymentRepository,
        IUserRepository userRepository,
        IStripeWebhookEventRepository webhookRepository,
        IUsageRecordRepository usageRecordRepository,
        IEmailService emailService,
        IOptions<StripeOptions> stripeOptions,
        ILogger<StripeBillingService> logger)
    {
        _planRepository = planRepository;
        _subscriptionRepository = subscriptionRepository;
        _paymentRepository = paymentRepository;
        _userRepository = userRepository;
        _webhookRepository = webhookRepository;
        _usageRecordRepository = usageRecordRepository;
        _emailService = emailService;
        _stripeOptions = stripeOptions.Value;
        _logger = logger;

        _stripeClient = new StripeClient(_stripeOptions.SecretKey);
        _checkoutSessionService = new CheckoutSessionService(_stripeClient);
        _billingPortalSessionService = new BillingPortalSessionService(_stripeClient);
        _customerService = new CustomerService(_stripeClient);
        _stripeSubscriptionService = new SubscriptionService(_stripeClient);
        _invoiceService = new InvoiceService(_stripeClient);
        _customerTaxIdService = new CustomerTaxIdService(_stripeClient);
        _subscriptionItemService = new SubscriptionItemService(_stripeClient);
        _priceService = new PriceService(_stripeClient);
        _meterService = new MeterService(_stripeClient);
        _meterEventService = new MeterEventService(_stripeClient);

        _priceLookup = new Dictionary<string, (string PlanCode, BillingInterval Interval)>(StringComparer.OrdinalIgnoreCase);
        _overagePriceIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _meterDescriptors = new ConcurrentDictionary<string, MeterDescriptor>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in _stripeOptions.PriceIds)
        {
            var planCode = kvp.Key;
            var priceOptions = kvp.Value ?? new StripePlanPriceOptions();

            if (!string.IsNullOrWhiteSpace(priceOptions.Monthly))
            {
                _priceLookup[priceOptions.Monthly] = (planCode, BillingInterval.Monthly);
            }

            if (!string.IsNullOrWhiteSpace(priceOptions.Annual))
            {
                _priceLookup[priceOptions.Annual] = (planCode, BillingInterval.Annual);
            }

            if (!string.IsNullOrWhiteSpace(priceOptions.Overage))
            {
                _overagePriceIds[planCode] = priceOptions.Overage;
            }
        }
    }

    public async Task<IReadOnlyList<PlanSummary>> GetActivePlansAsync(CancellationToken cancellationToken)
    {
        var plans = await _planRepository.GetActiveAsync(cancellationToken);
        return plans
            .Select(plan => new PlanSummary
            {
                Id = plan.Id,
                Name = plan.Name,
                Code = plan.Code,
                MonthlyPriceCents = plan.MonthlyPriceCents,
                AnnualPriceCents = plan.MonthlyPriceCents * AnnualMonthsCharged,
                MaxPdfsPerMonth = plan.MaxPdfsPerMonth,
                MaxRequestsPerMinute = plan.MaxRequestsPerMinute,
                PriorityWeight = plan.PriorityWeight,
                LogRetentionDays = plan.LogRetentionDays,
                OveragePricePerThousandCents = plan.OveragePricePerThousandCents
            })
            .ToArray();
    }

    public async Task<SubscriptionSummary> GetSubscriptionSummaryAsync(Guid userId, CancellationToken cancellationToken)
    {
        var subscription = await _subscriptionRepository.GetActiveByUserAsync(userId, cancellationToken);
        if (subscription?.Plan is null)
        {
            return new SubscriptionSummary();
        }

        var priceCents = subscription.Interval == BillingInterval.Annual
            ? subscription.Plan.MonthlyPriceCents * AnnualMonthsCharged
            : subscription.Plan.MonthlyPriceCents;

        return new SubscriptionSummary
        {
            PlanName = subscription.Plan.Name,
            Price = FormatPrice(priceCents),
            NextBillingDate = subscription.CurrentPeriodEnd,
            Status = MapSubscriptionStatus(subscription.Status),
            Interval = subscription.Interval
        };
    }

    public async Task<string> CreateCheckoutSessionAsync(Guid userId, string planCode, BillingInterval interval, CancellationToken cancellationToken)
    {
        var user = await GetActiveUserAsync(userId, cancellationToken);
        var plan = await GetPlanAsync(planCode, cancellationToken);
        var priceId = GetPriceIdForPlan(plan.Code, interval);
        try
        {
            var customerId = await EnsureCustomerAsync(user, cancellationToken);

            var lineItems = new List<SessionLineItemOptions>
            {
                new()
                {
                    Price = priceId,
                    Quantity = 1
                }
            };

            var overagePriceId = GetOveragePriceId(plan, require: plan.OveragePricePerThousandCents > 0);
            if (!string.IsNullOrWhiteSpace(overagePriceId))
            {
                lineItems.Add(new SessionLineItemOptions
                {
                    // Metered subscription items must omit an explicit quantity
                    Price = overagePriceId
                });
            }

            var options = new Stripe.Checkout.SessionCreateOptions
            {
                Mode = "subscription",
                Customer = customerId,
                SuccessUrl = _stripeOptions.SuccessUrl,
                CancelUrl = _stripeOptions.CancelUrl,
                LineItems = lineItems,
                SubscriptionData = new SessionSubscriptionDataOptions
                {
                    Metadata = new Dictionary<string, string>
                    {
                        ["plan_code"] = plan.Code,
                        ["user_id"] = user.Id.ToString()
                    }
                }
            };

            var session = await _checkoutSessionService.CreateAsync(options, cancellationToken: cancellationToken);
            if (string.IsNullOrWhiteSpace(session.Url))
            {
                _logger.LogError("Stripe checkout session did not return a redirect URL for user {UserId}", user.Id);
                throw new BillingException("stripe_error", "Unable to start Stripe checkout session.", HttpStatusCode.BadGateway);
            }

            return session.Url;
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe checkout session failed for user {UserId}", user.Id);
            var message = ResolveStripeErrorMessage(ex, "Unable to start Stripe checkout session. Please verify your billing details and try again.");
            var statusCode = MapStripeErrorStatus(ex);
            throw new BillingException("stripe_error", message, statusCode);
        }
    }

    public async Task<string> CreateCustomerPortalSessionAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await GetActiveUserAsync(userId, cancellationToken);
        try
        {
            var customerId = await EnsureCustomerAsync(user, cancellationToken);

            var options = new Stripe.BillingPortal.SessionCreateOptions
            {
                Customer = customerId,
                ReturnUrl = _stripeOptions.PortalReturnUrl
            };

            var session = await _billingPortalSessionService.CreateAsync(options, cancellationToken: cancellationToken);
            if (string.IsNullOrWhiteSpace(session.Url))
            {
                _logger.LogError("Stripe billing portal session missing URL for user {UserId}", user.Id);
                throw new BillingException("stripe_error", "Unable to start Stripe billing portal session.", HttpStatusCode.BadGateway);
            }

            return session.Url;
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe billing portal session failed for user {UserId}", user.Id);
            var message = ResolveStripeErrorMessage(ex, "Unable to open the billing portal. Please verify your billing details and try again.");
            var statusCode = MapStripeErrorStatus(ex);
            throw new BillingException("stripe_error", message, statusCode);
        }
    }

    public async Task<BillingProfileDto> GetBillingProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await GetActiveUserAsync(userId, cancellationToken);
        return MapBillingProfile(user);
    }

    public async Task UpdateBillingProfileAsync(Guid userId, UpdateBillingProfileRequest request, CancellationToken cancellationToken)
    {
        ValidateBillingProfile(request);

        var user = await GetActiveUserAsync(userId, cancellationToken);
        ApplyBillingProfile(user, request);
        user.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await SyncStripeCustomerDetailsAsync(user, cancellationToken);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe rejected billing profile update for user {UserId}", user.Id);
            var message = ResolveStripeErrorMessage(ex, "Unable to update billing profile. Please verify the provided details and try again.");
            var statusCode = MapStripeErrorStatus(ex);
            throw new BillingException("stripe_error", message, statusCode);
        }

        await _userRepository.UpdateAsync(user, cancellationToken);
    }

    public async Task<IReadOnlyList<InvoiceSummary>> GetInvoicesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await GetActiveUserAsync(userId, cancellationToken);
        var payments = await _paymentRepository.GetRecentByUserAsync(user.Id, MaxInvoicesToReturn, cancellationToken);

        var invoices = new List<InvoiceSummary>(payments.Count);
        foreach (var payment in payments)
        {
            if (RequiresInvoiceRefresh(payment))
            {
                await RefreshInvoiceDetailsAsync(payment, cancellationToken);
            }

            invoices.Add(MapInvoiceSummary(payment));
        }

        return invoices;
    }

    public async Task HandleWebhookAsync(string payload, string signatureHeader, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            throw new BillingException("missing_signature", "Stripe-Signature header is required.", HttpStatusCode.BadRequest);
        }

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(payload, signatureHeader, _stripeOptions.WebhookSecret);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe webhook signature validation failed.");
            throw new BillingException("invalid_signature", "Stripe signature validation failed.", HttpStatusCode.BadRequest);
        }

        if (await _webhookRepository.ExistsAsync(stripeEvent.Id, cancellationToken))
        {
            _logger.LogInformation("Stripe event {EventId} already processed.", stripeEvent.Id);
            return;
        }

        await RecordEventAsync(stripeEvent, payload, cancellationToken);
        await DispatchEventAsync(stripeEvent, cancellationToken);
    }

    private async Task<User> GetActiveUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
                   ?? throw new BillingException("user_not_found", "User not found.", HttpStatusCode.NotFound);

        if (user.IsDeleted)
        {
            throw new BillingException("account_disabled", "This account is no longer active.", HttpStatusCode.Forbidden);
        }

        return user;
    }

    private async Task<BillingPlan> GetPlanAsync(string planCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(planCode))
        {
            throw new BillingException("invalid_plan", "Plan code is required.", HttpStatusCode.BadRequest);
        }

        var plan = await _planRepository.GetByCodeAsync(planCode, cancellationToken)
                   ?? throw new BillingException("plan_not_found", "Plan not found.", HttpStatusCode.NotFound);

        if (!plan.IsActive)
        {
            throw new BillingException("plan_inactive", "Selected plan is no longer available.", HttpStatusCode.BadRequest);
        }

        return plan;
    }

    private string GetPriceIdForPlan(string planCode, BillingInterval interval)
    {
        if (_stripeOptions.PriceIds.TryGetValue(planCode, out var priceOptions))
        {
            var priceId = interval == BillingInterval.Annual
                ? priceOptions.Annual
                : priceOptions.Monthly;
            if (!string.IsNullOrWhiteSpace(priceId))
            {
                return priceId;
            }
        }

        _logger.LogError("Missing Stripe price mapping for plan {PlanCode} ({Interval})", planCode, interval);
        throw new BillingException("price_not_configured", "Stripe price mapping missing for this plan interval.", HttpStatusCode.InternalServerError);
    }

    private async Task<string> EnsureCustomerAsync(User user, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(user.StripeCustomerId))
        {
            return user.StripeCustomerId;
        }

        var createOptions = new CustomerCreateOptions
        {
            Email = user.Email,
            Metadata = new Dictionary<string, string>
            {
                ["user_id"] = user.Id.ToString()
            }
        };
        ApplyCustomerProfile(createOptions, user);

        var customer = await _customerService.CreateAsync(createOptions, cancellationToken: cancellationToken);
        user.StripeCustomerId = customer.Id;
        await _userRepository.UpdateAsync(user, cancellationToken);
        return customer.Id;
    }

    private async Task RecordEventAsync(Event stripeEvent, string payload, CancellationToken cancellationToken)
    {
        var webhookEvent = new StripeWebhookEvent
        {
            Id = Guid.NewGuid(),
            StripeEventId = stripeEvent.Id,
            Type = stripeEvent.Type,
            PayloadJson = payload,
            CreatedAt = DateTimeOffset.UtcNow,
            ProcessedAt = DateTimeOffset.UtcNow
        };

        await _webhookRepository.AddAsync(webhookEvent, cancellationToken);
    }

    private async Task DispatchEventAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        switch (stripeEvent.Type)
        {
            case StripeEventTypes.CheckoutSessionCompleted:
                if (ConvertEvent<CheckoutSession>(stripeEvent) is { } checkoutSession)
                {
                    await HandleCheckoutCompletedAsync(checkoutSession, cancellationToken);
                }
                break;
            case StripeEventTypes.CustomerSubscriptionCreated:
            case StripeEventTypes.CustomerSubscriptionUpdated:
            case StripeEventTypes.CustomerSubscriptionDeleted:
                if (ConvertEvent<StripeSubscription>(stripeEvent) is { } subscription)
                {
                    await HandleSubscriptionAsync(subscription, cancellationToken);
                }
                break;
            case StripeEventTypes.InvoicePaid:
            case StripeEventTypes.InvoicePaymentFailed:
                if (ConvertEvent<StripeInvoice>(stripeEvent) is { } invoice)
                {
                    await HandleInvoiceAsync(invoice, cancellationToken);
                }
                break;
            case StripeEventTypes.InvoiceUpcoming:
                await HandleUpcomingInvoiceAsync(stripeEvent, cancellationToken);
                break;
            default:
                _logger.LogDebug("Unhandled Stripe event type {EventType}", stripeEvent.Type);
                break;
        }
    }

    private async Task HandleCheckoutCompletedAsync(CheckoutSession session, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(session.CustomerId) || string.IsNullOrWhiteSpace(session.SubscriptionId))
        {
            _logger.LogWarning("Checkout session missing customer or subscription identifiers.");
            return;
        }

        var user = await _userRepository.GetByStripeCustomerIdAsync(session.CustomerId, cancellationToken);
        if (user is null)
        {
            _logger.LogWarning("No user found for Stripe customer {CustomerId}", session.CustomerId);
            return;
        }

        var subscription = await _stripeSubscriptionService.GetAsync(session.SubscriptionId, cancellationToken: cancellationToken);
        if (subscription is null)
        {
            _logger.LogWarning("Stripe subscription {SubscriptionId} not found.", session.SubscriptionId);
            return;
        }

        await SyncSubscriptionAsync(user, subscription, cancellationToken);
    }

    private async Task HandleSubscriptionAsync(StripeSubscription subscription, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(subscription.CustomerId))
        {
            _logger.LogWarning("Stripe subscription event missing customer identifier.");
            return;
        }

        var user = await _userRepository.GetByStripeCustomerIdAsync(subscription.CustomerId, cancellationToken);
        if (user is null)
        {
            _logger.LogWarning("No user found for Stripe customer {CustomerId}", subscription.CustomerId);
            return;
        }

        await SyncSubscriptionAsync(user, subscription, cancellationToken);
    }

    private async Task HandleInvoiceAsync(StripeInvoice invoice, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(invoice.CustomerId))
        {
            _logger.LogWarning("Stripe invoice event missing customer identifier.");
            return;
        }

        var user = await _userRepository.GetByStripeCustomerIdAsync(invoice.CustomerId, cancellationToken);
        if (user is null)
        {
            _logger.LogWarning("No user found for Stripe customer {CustomerId}", invoice.CustomerId);
            return;
        }

        await PersistPaymentAsync(user, invoice, cancellationToken);
    }

    private async Task HandleUpcomingInvoiceAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        var payload = stripeEvent.Data?.RawJObject;
        var customerId = payload?["customer"]?.Value<string>();
        var subscriptionId = payload?["subscription"]?.Value<string>();

        if (string.IsNullOrWhiteSpace(customerId) || string.IsNullOrWhiteSpace(subscriptionId))
        {
            _logger.LogWarning("Upcoming invoice missing customer or subscription identifiers.");
            return;
        }

        var user = await _userRepository.GetByStripeCustomerIdAsync(customerId, cancellationToken);
        if (user is null)
        {
            _logger.LogWarning("No user found for Stripe customer {CustomerId}", customerId);
            return;
        }

        var subscription = await _subscriptionRepository.GetByStripeIdAsync(subscriptionId, cancellationToken);
        if (subscription?.Plan is null)
        {
            _logger.LogWarning("Subscription {SubscriptionId} not tracked locally while handling upcoming invoice.", subscriptionId);
            return;
        }

        if (subscription.Plan.OveragePricePerThousandCents <= 0)
        {
            return;
        }

        var overageItemId = subscription.StripeOverageSubscriptionItemId;
        if (string.IsNullOrWhiteSpace(overageItemId))
        {
            var stripeSubscription = await _stripeSubscriptionService.GetAsync(subscription.StripeSubscriptionId, cancellationToken: cancellationToken);
            if (stripeSubscription is null)
            {
                _logger.LogWarning("Unable to fetch Stripe subscription {SubscriptionId} while preparing overage usage.", subscription.StripeSubscriptionId);
                return;
            }

            overageItemId = await EnsureOverageSubscriptionItemAsync(
                stripeSubscription,
                subscription.Plan,
                require: true,
                cancellationToken);

            subscription.StripeOverageSubscriptionItemId = overageItemId;
        }

        if (string.IsNullOrWhiteSpace(overageItemId))
        {
            _logger.LogWarning("Overage subscription item missing for subscription {SubscriptionId}", subscription.StripeSubscriptionId);
            return;
        }

        var used = await GetUsageForCurrentPeriodAsync(subscription, cancellationToken);
        var billableUnits = CalculateBillableUnits(subscription.Plan, used);

        if (AlreadyReportedOverage(subscription, billableUnits))
        {
            return;
        }

        await ReportUsageAsync(customerId, subscription, overageItemId, billableUnits, cancellationToken);
    }

    private async Task PersistPaymentAsync(User user, StripeInvoice invoice, CancellationToken cancellationToken)
    {
        var payment = await _paymentRepository.GetByStripeInvoiceIdAsync(invoice.Id, cancellationToken);
        var previousStatus = payment?.Status ?? PaymentStatus.Unknown;
        var paymentStatus = MapInvoiceStatus(invoice.Status);
        var amountCents = invoice.AmountPaid != 0 ? invoice.AmountPaid : invoice.AmountDue;
        var fallbackDate = ToDateTimeOffset(invoice.Created);
        var invoiceDate = invoice.StatusTransitions?.PaidAt.HasValue == true
            ? ToDateTimeOffset(invoice.StatusTransitions.PaidAt.Value)
            : fallbackDate;
        var shouldSendPaymentReceipt = paymentStatus == PaymentStatus.Paid && previousStatus != PaymentStatus.Paid;

        if (payment is null)
        {
            payment = new Payment
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                StripeInvoiceId = invoice.Id,
                AmountCents = amountCents,
                Currency = invoice.Currency ?? "usd",
                Status = paymentStatus,
                InvoiceDate = invoiceDate
            };
            ApplyInvoiceFields(payment, invoice);

            await _paymentRepository.AddAsync(payment, cancellationToken);
        }
        else
        {
            payment.AmountCents = amountCents;
            payment.Currency = invoice.Currency ?? payment.Currency;
            payment.Status = paymentStatus;
            payment.InvoiceDate = invoiceDate;
            ApplyInvoiceFields(payment, invoice);
            await _paymentRepository.UpdateAsync(payment, cancellationToken);
        }

        if (shouldSendPaymentReceipt)
        {
            await SendPaymentSuccessEmailAsync(user, payment, invoice, cancellationToken);
        }
    }

    private async Task SendPaymentSuccessEmailAsync(User user, Payment payment, StripeInvoice invoice, CancellationToken cancellationToken)
    {
        try
        {
            var (plan, interval) = await ResolvePlanFromInvoiceAsync(invoice, cancellationToken);
            var planName = plan?.Name ?? "PaperAPI plan";
            var intervalLabel = interval.HasValue ? FormatIntervalLabel(interval.Value) : "Subscription";
            var amountText = FormatAmount(payment.AmountCents, payment.Currency);
            var periodText = FormatPeriod(payment.PeriodStart, payment.PeriodEnd);
            var invoiceUrl = payment.HostedInvoiceUrl ?? payment.InvoicePdfUrl;

            var invoiceButton = !string.IsNullOrWhiteSpace(invoiceUrl)
                ? $@"
<p style=""text-align: center;"">
    <a href=""{EmailTemplateBuilder.EscapeHtml(invoiceUrl!)}"" class=""cta-button"">View invoice</a>
</p>"
                : string.Empty;

            var planDetailsSection = plan is not null
                ? BuildPlanDetailsSection(plan)
                : string.Empty;

            var emailContent = $@"
<h2>Payment successful</h2>
<p style=""color: #f8fafc;"">Thank you for your payment. Your subscription is now active.</p>
<div style=""padding: 16px; background-color: #0b1220; border: 1px solid rgba(248, 250, 252, 0.08); border-radius: 10px; margin: 24px 0;"">
    <h3 style=""margin-top: 0;"">Activated plan</h3>
    <ul style=""list-style: none; padding-left: 0; margin: 0;"">
        <li><strong>Plan:</strong> {EmailTemplateBuilder.EscapeHtml(planName)}</li>
        <li><strong>Billing:</strong> {EmailTemplateBuilder.EscapeHtml(intervalLabel)}</li>
        <li><strong>Amount paid:</strong> {EmailTemplateBuilder.EscapeHtml(amountText)}</li>
        <li><strong>Billing period:</strong> {EmailTemplateBuilder.EscapeHtml(periodText)}</li>
    </ul>
</div>
{planDetailsSection}
{invoiceButton}
<p style=""color: #94a3b8;"">Need help? Reply to this email and we will assist you.</p>
";

            var subject = $"Payment received - {planName}";
            await _emailService.SendEmailAsync(
                to: user.Email,
                subject: subject,
                body: emailContent,
                isHtml: true,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send payment success email for invoice {InvoiceId} to user {UserId}", invoice.Id, user.Id);
        }
    }

    private async Task<(BillingPlan? Plan, BillingInterval? Interval)> ResolvePlanFromInvoiceAsync(StripeInvoice invoice, CancellationToken cancellationToken)
    {
        var subscriptionId = invoice.Parent?.SubscriptionDetails?.SubscriptionId
                             ?? invoice.Lines?.Data?.FirstOrDefault()?.SubscriptionId;

        if (!string.IsNullOrWhiteSpace(subscriptionId))
        {
            var subscription = await _subscriptionRepository.GetByStripeIdAsync(subscriptionId, cancellationToken);
            if (subscription?.Plan is not null)
            {
                return (subscription.Plan, subscription.Interval);
            }
        }

        var priceId = invoice.Lines?.Data?
            .Select(line => line.Pricing?.PriceDetails?.Price)
            .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));

        if (!string.IsNullOrWhiteSpace(priceId) && _priceLookup.TryGetValue(priceId, out var mapping))
        {
            var plan = await _planRepository.GetByCodeAsync(mapping.PlanCode, cancellationToken);
            if (plan is not null)
            {
                return (plan, mapping.Interval);
            }
        }

        return (null, null);
    }

    private async Task SyncSubscriptionAsync(User user, StripeSubscription stripeSubscription, CancellationToken cancellationToken)
    {
        var priceId = stripeSubscription.Items?.Data?
            .FirstOrDefault(item => !string.Equals(item.Price?.Recurring?.UsageType, "metered", StringComparison.OrdinalIgnoreCase))
            ?.Price?.Id;
        if (string.IsNullOrWhiteSpace(priceId) || !_priceLookup.TryGetValue(priceId, out var mapping))
        {
            _logger.LogWarning("No plan mapping found for Stripe price {PriceId}", priceId);
            return;
        }

        var planCode = mapping.PlanCode;
        var interval = mapping.Interval;
        var plan = await _planRepository.GetByCodeAsync(planCode, cancellationToken);
        if (plan is null)
        {
            _logger.LogWarning("Plan {PlanCode} not found while syncing subscription.", planCode);
            return;
        }

        var subscription = await _subscriptionRepository.GetByStripeIdAsync(stripeSubscription.Id, cancellationToken);
        var status = MapSubscriptionStatus(stripeSubscription.Status);
        var statusEnum = MapSubscriptionStatusEnum(stripeSubscription.Status);
        var invoicePeriodStart = ToDateTimeOffset(stripeSubscription.LatestInvoice?.PeriodStart);
        var invoicePeriodEnd = ToDateTimeOffset(stripeSubscription.LatestInvoice?.PeriodEnd);
        var cycleAnchor = ToDateTimeOffset(stripeSubscription.BillingCycleAnchor);
        var trialEnd = ToDateTimeOffset(stripeSubscription.TrialEnd);
        var periodStart = invoicePeriodStart ?? cycleAnchor;
        var periodEnd = invoicePeriodEnd ?? trialEnd ?? cycleAnchor.AddMonths(1);

        var overageSubscriptionItemId = await EnsureOverageSubscriptionItemAsync(
            stripeSubscription,
            plan,
            require: plan.OveragePricePerThousandCents > 0,
            cancellationToken);

        if (subscription is null)
        {
            subscription = new Subscription
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                PlanId = plan.Id,
                StripeSubscriptionId = stripeSubscription.Id,
                Status = statusEnum,
                CurrentPeriodStart = periodStart,
                CurrentPeriodEnd = periodEnd,
                CreatedAt = DateTimeOffset.UtcNow,
                Plan = plan,
                Interval = interval,
                StripeOverageSubscriptionItemId = overageSubscriptionItemId
            };

            await _subscriptionRepository.AddAsync(subscription, cancellationToken);
        }
        else
        {
            subscription.PlanId = plan.Id;
            subscription.Plan = plan;
            subscription.Status = statusEnum;
            subscription.CurrentPeriodStart = periodStart;
            subscription.CurrentPeriodEnd = periodEnd;
            subscription.CancelledAt = statusEnum == SubscriptionStatus.Canceled ? periodEnd : null;
            subscription.Interval = interval;
            subscription.StripeOverageSubscriptionItemId = overageSubscriptionItemId;
            await _subscriptionRepository.UpdateAsync(subscription, cancellationToken);
        }

        _logger.LogInformation("Synchronized subscription {SubscriptionId} for user {UserId} with status {Status}", stripeSubscription.Id, user.Id, status);
    }

    private async Task<string?> EnsureOverageSubscriptionItemAsync(
        StripeSubscription stripeSubscription,
        BillingPlan plan,
        bool require,
        CancellationToken cancellationToken)
    {
        var desiredPriceId = GetOveragePriceId(plan, require);
        if (string.IsNullOrWhiteSpace(desiredPriceId))
        {
            return null;
        }

        var overageItem = stripeSubscription.Items?.Data?
            .FirstOrDefault(item => string.Equals(item.Price?.Recurring?.UsageType, "metered", StringComparison.OrdinalIgnoreCase));

        if (overageItem is null)
        {
            var createOptions = new SubscriptionItemCreateOptions
            {
                Subscription = stripeSubscription.Id,
                Price = desiredPriceId,
                Quantity = 1
            };

            overageItem = await _subscriptionItemService.CreateAsync(createOptions, cancellationToken: cancellationToken);
            return overageItem.Id;
        }

        if (!string.Equals(overageItem.Price?.Id, desiredPriceId, StringComparison.OrdinalIgnoreCase))
        {
            var updateOptions = new SubscriptionItemUpdateOptions
            {
                Price = desiredPriceId
            };

            overageItem = await _subscriptionItemService.UpdateAsync(overageItem.Id, updateOptions, cancellationToken: cancellationToken);
        }

        return overageItem.Id;
    }

    private async Task<int> GetUsageForCurrentPeriodAsync(Subscription subscription, CancellationToken cancellationToken)
    {
        if (subscription.CurrentPeriodStart == default || subscription.CurrentPeriodEnd == default)
        {
            _logger.LogWarning("Subscription {SubscriptionId} missing period bounds while calculating usage.", subscription.StripeSubscriptionId);
            return 0;
        }

        var periodStart = DateOnly.FromDateTime(subscription.CurrentPeriodStart.UtcDateTime);
        var periodEnd = DateOnly.FromDateTime(subscription.CurrentPeriodEnd.UtcDateTime);
        return await _usageRecordRepository.GetMonthlyPdfCountAsync(subscription.UserId, periodStart, periodEnd, cancellationToken);
    }

    private static int CalculateBillableUnits(BillingPlan plan, int usedCount)
    {
        if (plan.MaxPdfsPerMonth <= 0)
        {
            return 0;
        }

        var overageCount = Math.Max(0, usedCount - plan.MaxPdfsPerMonth);
        if (overageCount <= 0)
        {
            return 0;
        }

        return (int)Math.Ceiling(overageCount / (double)OverageUnitSize);
    }

    private static bool AlreadyReportedOverage(Subscription subscription, int billableUnits)
    {
        return subscription.LastOveragePeriodEnd.HasValue
            && subscription.LastOveragePeriodEnd.Value == subscription.CurrentPeriodEnd
            && subscription.LastOverageQuantity == billableUnits;
    }

    private async Task ReportUsageAsync(string stripeCustomerId, Subscription subscription, string overageItemId, int billableUnits, CancellationToken cancellationToken)
    {
        if (subscription.Plan is null)
        {
            _logger.LogWarning("Subscription {SubscriptionId} missing plan while reporting usage.", subscription.StripeSubscriptionId);
            return;
        }

        var meterDescriptor = await GetOverageMeterAsync(subscription.Plan, cancellationToken);
        if (meterDescriptor is null)
        {
            _logger.LogWarning("Unable to resolve meter for plan {PlanCode} while reporting usage.", subscription.Plan.Code);
            return;
        }

        var timestamp = ResolveUsageTimestamp(subscription);
        var payload = BuildMeterEventPayload(meterDescriptor, stripeCustomerId, subscription, overageItemId, billableUnits);

        var options = new MeterEventCreateOptions
        {
            EventName = meterDescriptor.EventName,
            Identifier = $"usage::{subscription.StripeSubscriptionId}::{timestamp.ToUnixTimeSeconds()}",
            Payload = payload,
            Timestamp = timestamp.UtcDateTime
        };

        try
        {
            await _meterEventService.CreateAsync(options, cancellationToken: cancellationToken);

            subscription.LastOveragePeriodEnd = subscription.CurrentPeriodEnd;
            subscription.LastOverageQuantity = billableUnits;
            subscription.StripeOverageSubscriptionItemId ??= overageItemId;
            await _subscriptionRepository.UpdateAsync(subscription, cancellationToken);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Failed to emit meter event {EventName} for subscription {SubscriptionId}. Overage item: {OverageItemId}, Units: {BillableUnits}",
                meterDescriptor.EventName, subscription.StripeSubscriptionId, overageItemId, billableUnits);
            throw;
        }
    }

    private static DateTimeOffset ResolveUsageTimestamp(Subscription subscription)
    {
        var timestamp = subscription.CurrentPeriodEnd.AddSeconds(-1);
        if (timestamp > DateTimeOffset.UtcNow)
        {
            timestamp = DateTimeOffset.UtcNow;
        }

        if (timestamp < subscription.CurrentPeriodStart)
        {
            timestamp = subscription.CurrentPeriodStart;
        }

        return timestamp;
    }

    private string? GetOveragePriceId(BillingPlan plan, bool require)
    {
        if (plan.OveragePricePerThousandCents <= 0)
        {
            return null;
        }

        if (_overagePriceIds.TryGetValue(plan.Code, out var priceId) && !string.IsNullOrWhiteSpace(priceId))
        {
            return priceId;
        }

        if (require)
        {
            _logger.LogError("Missing Stripe overage price mapping for plan {PlanCode}", plan.Code);
            throw new BillingException("price_not_configured", "Stripe overage price mapping missing for this plan.", HttpStatusCode.InternalServerError);
        }

        return null;
    }

    private async Task<MeterDescriptor?> GetOverageMeterAsync(BillingPlan plan, CancellationToken cancellationToken)
    {
        var priceId = GetOveragePriceId(plan, require: true);
        if (string.IsNullOrWhiteSpace(priceId))
        {
            return null;
        }

        if (_meterDescriptors.TryGetValue(priceId, out var descriptor))
        {
            return descriptor;
        }

        try
        {
            var price = await _priceService.GetAsync(priceId, cancellationToken: cancellationToken);
            var meterId = price?.Recurring?.Meter;
            if (string.IsNullOrWhiteSpace(meterId))
            {
                _logger.LogError("Stripe overage price {PriceId} is not backed by a meter.", priceId);
                return null;
            }

            var meter = await _meterService.GetAsync(meterId, cancellationToken: cancellationToken);
            if (meter is null)
            {
                _logger.LogError("Unable to load Stripe meter {MeterId} for price {PriceId}.", meterId, priceId);
                return null;
            }

            if (string.IsNullOrWhiteSpace(meter.EventName))
            {
                _logger.LogError("Stripe meter {MeterId} is missing an event name.", meterId);
                return null;
            }

            var valueKey = !string.IsNullOrWhiteSpace(meter.ValueSettings?.EventPayloadKey)
                ? meter.ValueSettings!.EventPayloadKey
                : "value";
            var customerKey = meter.CustomerMapping?.EventPayloadKey;
            var customerType = meter.CustomerMapping?.Type;

            descriptor = new MeterDescriptor(priceId, meterId, meter.EventName, valueKey, customerKey, customerType);
            _meterDescriptors[priceId] = descriptor;
            return descriptor;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Unable to resolve meter for price {PriceId}", priceId);
            throw;
        }
    }

    private Dictionary<string, string> BuildMeterEventPayload(
        MeterDescriptor descriptor,
        string stripeCustomerId,
        Subscription subscription,
        string overageItemId,
        int billableUnits)
    {
        var payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [descriptor.ValuePayloadKey] = billableUnits.ToString(CultureInfo.InvariantCulture),
            ["customer_id"] = stripeCustomerId,
            ["subscription_id"] = subscription.StripeSubscriptionId,
            ["subscription_item_id"] = overageItemId
        };

        if (!string.IsNullOrWhiteSpace(descriptor.CustomerPayloadKey))
        {
            var mappingValue = descriptor.CustomerMappingType?.ToLowerInvariant() switch
            {
                "subscription_id" => subscription.StripeSubscriptionId,
                "subscription_item_id" => overageItemId,
                "customer_id" => stripeCustomerId,
                _ => stripeCustomerId
            };

            if (!string.IsNullOrWhiteSpace(mappingValue))
            {
                payload[descriptor.CustomerPayloadKey!] = mappingValue;
            }
        }

        return payload;
    }

    private sealed record MeterDescriptor(
        string PriceId,
        string MeterId,
        string EventName,
        string ValuePayloadKey,
        string? CustomerPayloadKey,
        string? CustomerMappingType);

    private static string BuildPlanDetailsSection(BillingPlan plan)
    {
        return $@"
<h3>Plan details</h3>
<ul style=""padding-left: 20px; margin-top: 8px;"">
    <li>Monthly PDFs: {plan.MaxPdfsPerMonth.ToString("N0", CultureInfo.InvariantCulture)}</li>
    <li>Requests per minute: {plan.MaxRequestsPerMinute.ToString("N0", CultureInfo.InvariantCulture)}</li>
    <li>Log retention: {plan.LogRetentionDays} days</li>
</ul>";
    }

    private static string FormatIntervalLabel(BillingInterval interval)
    {
        return interval == BillingInterval.Annual ? "Annual" : "Monthly";
    }

    private static string FormatAmount(long amountCents, string? currency)
    {
        var amount = amountCents / 100m;
        var code = string.IsNullOrWhiteSpace(currency) ? "USD" : currency.ToUpperInvariant();
        return code switch
        {
            "EUR" => $"€{amount:0.##}",
            "USD" => $"${amount:0.##}",
            _ => $"{amount:0.##} {code}"
        };
    }

    private static string FormatPeriod(DateTimeOffset? start, DateTimeOffset? end)
    {
        if (start.HasValue && end.HasValue)
        {
            return $"{start.Value.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)} - {end.Value.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)}";
        }

        if (start.HasValue)
        {
            return $"{start.Value.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)} onward";
        }

        if (end.HasValue)
        {
            return $"Through {end.Value.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)}";
        }

        return "Current billing period";
    }

    private static BillingProfileDto MapBillingProfile(User user)
    {
        return new BillingProfileDto
        {
            CompanyName = user.CompanyName,
            AddressLine1 = user.BillingAddressLine1,
            AddressLine2 = user.BillingAddressLine2,
            City = user.BillingCity,
            State = user.BillingState,
            PostalCode = user.BillingPostalCode,
            Country = user.BillingCountry,
            VatNumber = user.VatNumber
        };
    }

    private static void ApplyBillingProfile(User user, UpdateBillingProfileRequest request)
    {
        user.CompanyName = Normalize(request.CompanyName);
        user.BillingAddressLine1 = Normalize(request.AddressLine1);
        user.BillingAddressLine2 = Normalize(request.AddressLine2);
        user.BillingCity = Normalize(request.City);
        user.BillingState = Normalize(request.State);
        user.BillingPostalCode = Normalize(request.PostalCode);
        user.BillingCountry = NormalizeCountry(request.Country);
        user.VatNumber = Normalize(request.VatNumber);
    }

    private static void ValidateBillingProfile(UpdateBillingProfileRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CompanyName))
        {
            throw new BillingException("invalid_billing_profile", "Company name is required.", HttpStatusCode.BadRequest);
        }

        if (string.IsNullOrWhiteSpace(request.AddressLine1))
        {
            throw new BillingException("invalid_billing_profile", "Billing address line 1 is required.", HttpStatusCode.BadRequest);
        }

        if (string.IsNullOrWhiteSpace(request.City))
        {
            throw new BillingException("invalid_billing_profile", "City is required.", HttpStatusCode.BadRequest);
        }

        if (string.IsNullOrWhiteSpace(request.PostalCode))
        {
            throw new BillingException("invalid_billing_profile", "Postal code is required.", HttpStatusCode.BadRequest);
        }

        if (string.IsNullOrWhiteSpace(request.Country) || request.Country.Trim().Length != 2)
        {
            throw new BillingException("invalid_billing_profile", "Country must be a 2-letter ISO code.", HttpStatusCode.BadRequest);
        }
    }

    private static string ResolveStripeErrorMessage(StripeException ex, string fallbackMessage)
    {
        if (!string.IsNullOrWhiteSpace(ex.StripeError?.Message))
        {
            return ex.StripeError.Message;
        }

        return string.IsNullOrWhiteSpace(ex.Message)
            ? fallbackMessage
            : ex.Message;
    }

    private static HttpStatusCode MapStripeErrorStatus(StripeException ex)
    {
        return string.Equals(ex.StripeError?.Type, "invalid_request_error", StringComparison.OrdinalIgnoreCase)
            ? HttpStatusCode.BadRequest
            : HttpStatusCode.BadGateway;
    }

    private async Task SyncStripeCustomerDetailsAsync(User user, CancellationToken cancellationToken)
    {
        var customerId = await EnsureCustomerAsync(user, cancellationToken);
        var updateOptions = new CustomerUpdateOptions
        {
            Email = user.Email
        };

        if (!string.IsNullOrWhiteSpace(user.CompanyName))
        {
            updateOptions.Name = user.CompanyName;
        }

        var address = BuildAddressOptions(user);
        if (address is not null)
        {
            updateOptions.Address = address;
        }

        await _customerService.UpdateAsync(customerId, updateOptions, cancellationToken: cancellationToken);
        await SyncVatNumberAsync(customerId, user.VatNumber, cancellationToken);
    }

    private static void ApplyCustomerProfile(CustomerCreateOptions options, User user)
    {
        if (!string.IsNullOrWhiteSpace(user.CompanyName))
        {
            options.Name = user.CompanyName;
        }

        var address = BuildAddressOptions(user);
        if (address is not null)
        {
            options.Address = address;
        }

        if (!string.IsNullOrWhiteSpace(user.VatNumber))
        {
            // Note: Currently assumes all VAT numbers are EU-based ("eu_vat").
            // Future enhancement: validate VAT format and determine appropriate tax ID type based on country code.
            options.TaxIdData = new List<CustomerTaxIdDataOptions>
            {
                new()
                {
                    Type = "eu_vat",
                    Value = user.VatNumber
                }
            };
        }
    }

    private static AddressOptions? BuildAddressOptions(User user)
    {
        if (string.IsNullOrWhiteSpace(user.BillingAddressLine1))
        {
            return null;
        }

        return new AddressOptions
        {
            Line1 = user.BillingAddressLine1,
            Line2 = user.BillingAddressLine2,
            City = user.BillingCity,
            State = user.BillingState,
            PostalCode = user.BillingPostalCode,
            Country = user.BillingCountry
        };
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeCountry(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    }

    private static bool RequiresInvoiceRefresh(Payment payment)
    {
        return string.IsNullOrWhiteSpace(payment.InvoicePdfUrl)
               || string.IsNullOrWhiteSpace(payment.HostedInvoiceUrl)
               || string.IsNullOrWhiteSpace(payment.Description);
    }

    private async Task RefreshInvoiceDetailsAsync(Payment payment, CancellationToken cancellationToken)
    {
        try
        {
            var invoice = await _invoiceService.GetAsync(payment.StripeInvoiceId, cancellationToken: cancellationToken);
            if (invoice is null)
            {
                return;
            }

            ApplyInvoiceFields(payment, invoice);
            await _paymentRepository.UpdateAsync(payment, cancellationToken);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Unable to refresh Stripe invoice {InvoiceId}", payment.StripeInvoiceId);
        }
    }

    private void ApplyInvoiceFields(Payment payment, StripeInvoice invoice)
    {
        payment.HostedInvoiceUrl = invoice.HostedInvoiceUrl;
        payment.InvoicePdfUrl = invoice.InvoicePdf;
        payment.Description = ResolveInvoiceDescription(invoice);
        payment.PeriodStart = ResolvePeriodStart(invoice);
        payment.PeriodEnd = ResolvePeriodEnd(invoice);
    }

    private static InvoiceSummary MapInvoiceSummary(Payment payment)
    {
        return new InvoiceSummary
        {
            PaymentId = payment.Id,
            StripeInvoiceId = payment.StripeInvoiceId,
            AmountCents = payment.AmountCents,
            Currency = payment.Currency,
            Status = payment.Status,
            InvoiceDate = payment.InvoiceDate,
            PeriodStart = payment.PeriodStart,
            PeriodEnd = payment.PeriodEnd,
            Description = payment.Description,
            HostedInvoiceUrl = payment.HostedInvoiceUrl,
            InvoicePdfUrl = payment.InvoicePdfUrl
        };
    }

    private static string? ResolveInvoiceDescription(StripeInvoice invoice)
    {
        if (!string.IsNullOrWhiteSpace(invoice.Description))
        {
            return invoice.Description;
        }

        return invoice.Lines?.Data?.FirstOrDefault()?.Description;
    }

    private static DateTimeOffset? ResolvePeriodStart(StripeInvoice invoice)
    {
        var line = invoice.Lines?.Data?.FirstOrDefault();
        return line?.Period?.Start != null ? ToDateTimeOffset(line.Period.Start) : null;
    }

    private static DateTimeOffset? ResolvePeriodEnd(StripeInvoice invoice)
    {
        var line = invoice.Lines?.Data?.FirstOrDefault();
        return line?.Period?.End != null ? ToDateTimeOffset(line.Period.End) : null;
    }

    private async Task SyncVatNumberAsync(string customerId, string? vatNumber, CancellationToken cancellationToken)
    {
        try
        {
            var existing = await _customerTaxIdService.ListAsync(customerId, new CustomerTaxIdListOptions { Limit = 25 }, cancellationToken: cancellationToken);
            var currentVat = existing?.Data?.FirstOrDefault(t => string.Equals(t.Type, "eu_vat", StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(vatNumber))
            {
                if (currentVat is not null)
                {
                    await _customerTaxIdService.DeleteAsync(customerId, currentVat.Id, cancellationToken: cancellationToken);
                }

                return;
            }

            if (currentVat is not null && string.Equals(currentVat.Value, vatNumber, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (currentVat is not null)
            {
                await _customerTaxIdService.DeleteAsync(customerId, currentVat.Id, cancellationToken: cancellationToken);
            }

            var createOptions = new CustomerTaxIdCreateOptions
            {
                Type = "eu_vat",
                Value = vatNumber
            };
            await _customerTaxIdService.CreateAsync(customerId, createOptions, cancellationToken: cancellationToken);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Unable to synchronize VAT number for Stripe customer {CustomerId}", customerId);
        }
    }

    private static T? ConvertEvent<T>(Event stripeEvent) where T : StripeEntity
    {
        if (stripeEvent.Data.Object is T typed)
        {
            return typed;
        }

        return stripeEvent.Data.RawJObject?.ToObject<T>();
    }

    private static string FormatPrice(int cents)
    {
        var euros = cents / 100m;
        return string.Format(CultureInfo.InvariantCulture, "€{0:0.##}", euros);
    }

    private static DateTimeOffset ToDateTimeOffset(DateTime value)
    {
        var normalized = DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return new DateTimeOffset(normalized);
    }

    private static DateTimeOffset? ToDateTimeOffset(DateTime? value)
    {
        return value.HasValue ? ToDateTimeOffset(value.Value) : null;
    }

    private static string MapSubscriptionStatus(SubscriptionStatus status)
    {
        return status switch
        {
            SubscriptionStatus.Active => "active",
            SubscriptionStatus.Trial => "trialing",
            SubscriptionStatus.PastDue => "past_due",
            SubscriptionStatus.Canceled => "canceled",
            _ => "unknown"
        };
    }

    private static string MapSubscriptionStatus(string? status)
    {
        return status?.ToLowerInvariant() switch
        {
            "trialing" => "trialing",
            "active" => "active",
            "past_due" => "past_due",
            "canceled" => "canceled",
            _ => "unknown"
        };
    }

    private static SubscriptionStatus MapSubscriptionStatusEnum(string? status)
    {
        return status?.ToLowerInvariant() switch
        {
            "active" => SubscriptionStatus.Active,
            "trialing" => SubscriptionStatus.Trial,
            "canceled" => SubscriptionStatus.Canceled,
            _ => SubscriptionStatus.PastDue
        };
    }

    private static PaymentStatus MapInvoiceStatus(string? status)
    {
        return status?.ToLowerInvariant() switch
        {
            "paid" => PaymentStatus.Paid,
            "void" => PaymentStatus.Void,
            "uncollectible" => PaymentStatus.Uncollectible,
            "open" => PaymentStatus.Open,
            _ => PaymentStatus.Unknown
        };
    }
}
