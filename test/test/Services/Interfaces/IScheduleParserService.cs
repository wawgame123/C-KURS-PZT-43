using test.Models;

namespace test.Services.Interfaces
{
    public interface IScheduleParserService
    {
        Task<List<ScheduleDay>> ParseExcelAsync(string filePath);

        Task<List<ScheduleDay>> ParseWebsiteAsync(
            string url,
            string mode,
            string selectedTeacher,
            string selectedGroup);

        Task<List<string>> BuildWebsiteMatrixDebugAsync(string url);

        Task<SourceCatalog> ParseWebsiteCatalogAsync(string url);
    }
}
