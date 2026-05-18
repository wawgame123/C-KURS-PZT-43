using test.Models;

namespace test.Services.Interfaces
{
    public interface IScheduleParserService
    {
        Task<List<ScheduleDay>> ParseExcelAsync(string filePath);

        Task<List<ScheduleDay>> ParseWebsiteAsync(string url);
    }
}