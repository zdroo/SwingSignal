using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SwingSignal.Api.Extensions;
using SwingSignal.Application.Abstractions.Billing;
using SwingSignal.Application.Common;
using SwingSignal.Contracts.Billing;

namespace SwingSignal.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BillingController : ControllerBase
{
    private readonly IBillingService _billing;
    private readonly ProFeatures _pro;

    public BillingController(IBillingService billing, ProFeatures pro)
    {
        _billing = billing;
        _pro = pro;
    }

    // Purchasing only exists once Pro is publicly live — while dark, these
    // endpoints don't exist as far as the outside world can tell
    private void RequireProLive()
    {
        if (!_pro.Enabled)
            throw new NotFoundException("Pro subscriptions are not available yet.");
    }

    [HttpPost("checkout")]
    [Authorize]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Checkout([FromBody] CheckoutRequest request, CancellationToken ct)
    {
        RequireProLive();
        var url = await _billing.CreateCheckoutUrlAsync(User.RequireUserId(), request.Period, ct);
        return Ok(new BillingUrlDto(url));
    }

    [HttpPost("portal")]
    [Authorize]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Portal(CancellationToken ct)
    {
        RequireProLive();
        var url = await _billing.CreatePortalUrlAsync(User.RequireUserId(), ct);
        return Ok(new BillingUrlDto(url));
    }

    // Stripe calls this — authentication is the signature header, active
    // regardless of the dark-launch flag so a launch-day flip can't drop events
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(ct);
        var signature = Request.Headers["Stripe-Signature"].ToString();

        await _billing.HandleWebhookAsync(payload, signature, ct);
        return Ok();
    }
}
