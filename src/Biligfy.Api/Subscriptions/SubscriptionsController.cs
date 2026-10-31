using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Biligfy.Api.Subscriptions;

[ApiController]
[Authorize]
[Route("api/subscriptions")]
public sealed class SubscriptionsController(ISubscriptionService service) : ControllerBase
{
    private Guid UserId => Guid.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : Guid.Empty;

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<SubscriptionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<SubscriptionResponse>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] bool? isActive = null,
        CancellationToken ct = default)
    {
        if (page < 1) ModelState.AddModelError("page", "page en az 1 olmalıdır.");
        if (pageSize < 1 || pageSize > 100) ModelState.AddModelError("pageSize", "pageSize 1 ile 100 arasında olmalıdır.");
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        return Ok(await service.ListAsync(UserId, page, pageSize, isActive, ct));
    }

    [HttpGet("{id:guid}", Name = "GetSubscription")]
    [ProducesResponseType(typeof(SubscriptionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubscriptionResponse>> Get(Guid id, CancellationToken ct)
    {
        var result = await service.GetAsync(UserId, id, ct);
        return result is null ? NotFoundProblem() : Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(SubscriptionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SubscriptionResponse>> Create([FromBody] SubscriptionRequest request, CancellationToken ct)
    {
        try
        {
            var created = await service.CreateAsync(UserId, request, ct);
            return CreatedAtRoute("GetSubscription", new { id = created.Id }, created);
        }
        catch (SubscriptionLimitExceededException)
        {
            return Problem(
                title: "Abonelik limiti aşıldı",
                detail: "En fazla 100 abonelik ekleyebilirsiniz.",
                statusCode: StatusCodes.Status409Conflict);
        }
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(SubscriptionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubscriptionResponse>> Update(Guid id, [FromBody] SubscriptionRequest request, CancellationToken ct)
    {
        var result = await service.UpdateAsync(UserId, id, request, ct);
        return result is null ? NotFoundProblem() : Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        return await service.DeleteAsync(UserId, id, ct) ? NoContent() : NotFoundProblem();
    }

    private ObjectResult NotFoundProblem() => Problem(
        title: "Abonelik bulunamadı",
        detail: "Belirtilen kimlikte bir abonelik yok.",
        statusCode: StatusCodes.Status404NotFound);
}
