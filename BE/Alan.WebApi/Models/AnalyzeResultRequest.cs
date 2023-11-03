using Microsoft.AspNetCore.Http;

namespace Alan.WebApi.Models
{
    public class AnalyzeResultRequest
    {
        public IFormFile File { get; set; }
    }
}
