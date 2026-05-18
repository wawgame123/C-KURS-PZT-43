using HtmlAgilityPack;
using OfficeOpenXml;
using test.Models;
using test.Services.Interfaces;

namespace test.Services
{
    public class ScheduleParserService : IScheduleParserService
    {
        public async Task<List<ScheduleDay>> ParseExcelAsync(string filePath)
        {
            ExcelPackage.License.SetNonCommercialPersonal("test");

            var result = new List<ScheduleDay>();

            var file = new FileInfo(filePath);

            using var package = new ExcelPackage(file);

            var worksheet = package.Workbook.Worksheets[0];

            ScheduleDay currentDay = null;

            var groups = new Dictionary<int, string>();

            for (int col = 3; col <= worksheet.Dimension.End.Column; col++)
            {
                var groupName = worksheet.Cells[1, col].Text.Trim();

                if (!string.IsNullOrWhiteSpace(groupName))
                {
                    groups[col] = groupName;
                }
            }

            for (int row = 1; row <= worksheet.Dimension.End.Row; row++)
            {
                for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
                {
                    var cellText = worksheet.Cells[row, col].Text.Trim();

                    if (cellText.Contains("Понедельник") ||
                        cellText.Contains("Вторник") ||
                        cellText.Contains("Среда") ||
                        cellText.Contains("Четверг") ||
                        cellText.Contains("Пятница") ||
                        cellText.Contains("Суббота"))
                    {
                        currentDay = new ScheduleDay
                        {
                            DayTitle = cellText,
                            Date = DateTime.Now
                        };

                        result.Add(currentDay);
                    }
                }

                if (currentDay == null)
                {
                    continue;
                }

                var lessonNumber = worksheet.Cells[row, 1].Text.Trim();
                var time = worksheet.Cells[row, 2].Text.Trim();

                if (string.IsNullOrWhiteSpace(time))
                {
                    continue;
                }

                foreach (var group in groups)
                {
                    var subject = worksheet.Cells[row, group.Key].Text.Trim();

                    if (string.IsNullOrWhiteSpace(subject))
                    {
                        continue;
                    }

                    var teacher = worksheet.Cells[row + 1, group.Key].Text.Trim();
                    var classroom = worksheet.Cells[row + 2, group.Key].Text.Trim();

                    currentDay.Lessons.Add(new ScheduleLesson
                    {
                        LessonNumber = int.TryParse(lessonNumber, out int number)
                            ? number
                            : 0,

                        Time = time,

                        Subject = subject,

                        Teacher = teacher,

                        Classroom = classroom,

                        GroupName = group.Value
                    });
                }
            }

            return result;
        }

        public async Task<List<ScheduleDay>> ParseWebsiteAsync(string url)
        {
            var web = new HtmlWeb();

            var document = await web.LoadFromWebAsync(url);

            var result = new List<ScheduleDay>();

            var tables = document.DocumentNode.SelectNodes("//table");

            if (tables == null)
            {
                return result;
            }

            foreach (var table in tables)
            {
                var rows = table.SelectNodes(".//tr");

                if (rows == null)
                {
                    continue;
                }

                ScheduleDay currentDay = null;

                foreach (var row in rows)
                {
                    var cells = row.SelectNodes("td|th");

                    if (cells == null)
                    {
                        continue;
                    }

                    foreach (var cell in cells)
                    {
                        var text = HtmlEntity.DeEntitize(cell.InnerText.Trim());

                        if (text.Contains("Понедельник") ||
                            text.Contains("Вторник") ||
                            text.Contains("Среда") ||
                            text.Contains("Четверг") ||
                            text.Contains("Пятница") ||
                            text.Contains("Суббота"))
                        {
                            currentDay = new ScheduleDay
                            {
                                DayTitle = text,
                                Date = DateTime.Now
                            };

                            result.Add(currentDay);
                        }
                    }

                    if (currentDay == null)
                    {
                        continue;
                    }

                    if (cells.Count < 3)
                    {
                        continue;
                    }

                    var time = HtmlEntity.DeEntitize(cells[1].InnerText.Trim());

                    for (int i = 2; i < cells.Count; i++)
                    {
                        var subject = HtmlEntity.DeEntitize(cells[i].InnerText.Trim());

                        if (string.IsNullOrWhiteSpace(subject))
                        {
                            continue;
                        }

                        currentDay.Lessons.Add(new ScheduleLesson
                        {
                            Time = time,
                            Subject = subject
                        });
                    }
                }
            }

            return result;
        }
    }
}