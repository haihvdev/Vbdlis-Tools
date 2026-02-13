using Haihv.Vbdlis.Tools.Api.Playwright.Models;

namespace Haihv.Vbdlis.Tools.Api.Playwright.Services;

public interface IVbdlisPlaywrightSearchService
{
    Task<VbdlisBatchSearchResponse> SearchAsync(VbdlisBatchSearchRequest request, CancellationToken cancellationToken);
}
