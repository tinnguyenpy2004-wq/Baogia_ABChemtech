using BaoGia.Api.Models;
using BaoGia.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BaoGia.Api.Controllers;

[ApiController]
[Route("api/quotes")]
public sealed class QuotesController(QuoteJobStore store) : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyCollection<QuoteResponse>> List()
        => Ok(store.All().Select(ToResponse));

    [HttpGet("{id:guid}")]
    public ActionResult<QuoteResponse> Get(Guid id)
    {
        var job = store.Find(id);
        return job is null ? NotFound() : Ok(ToResponse(job));
    }

    [HttpGet("{id:guid}/download")]
    public IActionResult Download(Guid id)
    {
        var job = store.Find(id);
        if (job is null) return NotFound();
        if (job.Status != QuoteJobStatus.Completed || job.OutputFileName is null) return Conflict("Bao gia chua san sang.");
        return PhysicalFile(store.OutputPath(job.OutputFileName), "text/html", job.OutputFileName);
    }

    [HttpPost]
    public ActionResult<QuoteResponse> Create(CreateQuoteRequest request)
    {
        var job = store.Create(request);
        return CreatedAtAction(nameof(Get), new { id = job.Id }, ToResponse(job));
    }

    private static QuoteResponse ToResponse(QuoteJobRecord job)
    {
        var total = job.Request.Items.Sum(item => item.Quantity * item.UnitPrice);
        var productName = job.Request.Items.Count switch
        {
            0 => string.Empty,
            1 => job.Request.Items[0].Product,
            _ => string.Join(", ", job.Request.Items.Select(item => item.Product))
        };

        return new QuoteResponse(
            job.Id,
            job.QuoteNumber,
            job.Request.CustomerName,
            job.Status.ToString().ToUpperInvariant(),
            total,
            job.CreatedAt,
            job.ErrorMessage,
            job.Status == QuoteJobStatus.Completed ? $"/api/quotes/{job.Id}/download" : null,
            productName);
    }
}
