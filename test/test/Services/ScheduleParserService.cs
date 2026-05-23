using HtmlAgilityPack;
using OfficeOpenXml;
using System.Text.RegularExpressions;
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

            var groups = new Dictionary<int, string>();

            for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
            {
                var text = worksheet.Cells[1, col]
                    .Text
                    .Trim();

                if (!string.IsNullOrWhiteSpace(text) &&
                    text.Contains("-"))
                {
                    groups[col] = text;
                }
            }

            ScheduleDay currentDay = null;

            for (int row = 1; row <= worksheet.Dimension.End.Row; row++)
            {
                string rowText = "";

                for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
                {
                    rowText += " " +
                        worksheet.Cells[row, col]
                        .Text
                        .Trim();
                }

                rowText = rowText.Trim();

                bool isDay =
                    rowText.Contains("Понедельник") ||
                    rowText.Contains("Вторник") ||
                    rowText.Contains("Среда") ||
                    rowText.Contains("Четверг") ||
                    rowText.Contains("Пятница") ||
                    rowText.Contains("Суббота");

                if (isDay)
                {
                    currentDay = new ScheduleDay
                    {
                        DayTitle = rowText,
                        Date = DateTime.Now
                    };

                    result.Add(currentDay);

                    continue;
                }

                if (currentDay == null)
                {
                    continue;
                }

                var lessonNumberText =
                    worksheet.Cells[row, 1]
                    .Text
                    .Trim();

                bool isLesson =
                    int.TryParse(
                        lessonNumberText,
                        out int lessonNumber);

                if (!isLesson)
                {
                    continue;
                }

                var time =
                    worksheet.Cells[row, 2]
                    .Text
                    .Trim();

                foreach (var group in groups)
                {
                    int col = group.Key;

                    var subject =
                        worksheet.Cells[row, col]
                        .Text
                        .Trim();

                    if (string.IsNullOrWhiteSpace(subject))
                    {
                        continue;
                    }

                    string teacher = "";

                    string classroom = "";

                    if (row + 1 <= worksheet.Dimension.End.Row)
                    {
                        teacher =
                            worksheet.Cells[row + 1, col]
                            .Text
                            .Trim();
                    }

                    if (row + 2 <= worksheet.Dimension.End.Row)
                    {
                        classroom =
                            worksheet.Cells[row + 2, col]
                            .Text
                            .Trim();
                    }

                    var detectedTime =
                        ExtractTime(subject);

                    if (detectedTime == null)
                    {
                        detectedTime =
                            ExtractTime(time);
                    }

                    currentDay.Lessons.Add(new ScheduleLesson
                    {
                        LessonNumber = lessonNumber,

                        Time = time,

                        Subject = subject,

                        Teacher = teacher,

                        Classroom = classroom,

                        GroupName = group.Value,

                        SortTime = detectedTime
                    });
                }
            }

            foreach (var day in result)
            {
                day.Lessons = day.Lessons
                    .OrderBy(x =>
                        x.SortTime ??
                        new TimeSpan(x.LessonNumber + 7, 0, 0))
                    .ThenBy(x => x.GroupName)
                    .ToList();
            }

            return result;
        }

        public async Task<List<ScheduleDay>> ParseWebsiteAsync(
            string url,
            string mode,
            string selectedTeacher,
            string selectedGroup)
        {
            var web = new HtmlWeb();

            var document = await web.LoadFromWebAsync(url);

            var result = new List<ScheduleDay>();

            var rows = document.DocumentNode
                .SelectNodes("//tr");
                

            if (rows == null)
            {
                return result;
            }

            var headers =
                new Dictionary<int, string>();

            var rowSpans =
                new Dictionary<(int Row, int Col), int>();

            ScheduleDay currentDay = null;

            for (int rowIndex = 0;
                 rowIndex < rows.Count;
                 rowIndex++)
            {
                var row = rows[rowIndex];

                var rowMap =
                    BuildRowMap(
                        row,
                        rowIndex,
                        rowSpans);

                if (rowMap.Count == 0)
                {
                    continue;
                }

                var maxColumn =
                    rowMap.Keys.Max();

                var rowTexts =
                    Enumerable
                        .Range(0, maxColumn + 1)
                        .Select(x =>
                            rowMap.TryGetValue(x, out var value)
                                ? value
                                : "")
                        .ToList();

                var normalizedRowTexts =
                    rowTexts
                        .Select(NormalizeCellText)
                        .ToList();

                var fullText =
                    string.Join(" ", normalizedRowTexts);

                bool isDay =
                    normalizedRowTexts.Any(IsDayCell);

                if (isDay)
                {
                    var dayCell =
                        normalizedRowTexts
                            .FirstOrDefault(IsDayCell) ?? fullText;

                    currentDay = new ScheduleDay
                    {
                        DayTitle = dayCell,
                        Date = DateTime.Now
                    };

                    result.Add(currentDay);

                    continue;
                }

                bool isHeader =
                    IsGroupHeaderRow(rowTexts);

                if (isHeader)
                {
                    headers.Clear();

                    for (int i = 2; i < rowTexts.Count; i++)
                    {
                        if (!string.IsNullOrWhiteSpace(rowTexts[i]))
                        {
                            headers[i] = rowTexts[i];
                        }
                    }

                    continue;
                }

                if (currentDay == null)
                {
                    continue;
                }

                if (rowTexts.Count < 2)
                {
                    continue;
                }

                var lessonNumberText =
                    rowTexts[0];

                bool isLesson =
                    int.TryParse(
                        lessonNumberText,
                        out int lessonNumber);

                if (!isLesson)
                {
                    continue;
                }

                if (rowIndex + 2 >= rows.Count)
                {
                    continue;
                }

                var teacherMap =
                    BuildRowMap(
                        rows[rowIndex + 1],
                        rowIndex + 1,
                        rowSpans);

                var classroomMap =
                    BuildRowMap(
                        rows[rowIndex + 2],
                        rowIndex + 2,
                        rowSpans);

                string time =
                    rowTexts.Count > 1
                    ? rowTexts[1]
                    : "";

                for (int col = 2;
                     col < rowTexts.Count;
                     col++)
                {
                    if (!headers.ContainsKey(col))
                    {
                        continue;
                    }

                    string subject =
                        rowTexts[col];

                    if (string.IsNullOrWhiteSpace(subject))
                    {
                        continue;
                    }

                    string teacher = "";

                    string classroom = "";

                    if (teacherMap.TryGetValue(
                        col,
                        out var teacherValue))
                    {
                        teacher = teacherValue;
                    }

                    if (classroomMap.TryGetValue(
                        col,
                        out var classroomValue))
                    {
                        classroom = classroomValue;
                    }

                    if (classroom.Contains("СМГ"))
                    {
                        classroom = "";
                    }

                    string groupName =
                        headers[col];

                    var detectedTime =
                        ExtractTime(subject);

                    if (detectedTime == null)
                    {
                        detectedTime =
                            ExtractTime(time);
                    }

                    currentDay.Lessons.Add(new ScheduleLesson
                    {
                        LessonNumber = lessonNumber,

                        Time = time,

                        Subject = subject,

                        Teacher = teacher,

                        Classroom = classroom,

                        GroupName = groupName,

                        SortTime = detectedTime
                    });
                }

                rowIndex += 2;
            }

            foreach (var day in result)
            {
                day.Lessons = day.Lessons
                    .OrderBy(x =>
                        x.SortTime ??
                        new TimeSpan(x.LessonNumber + 7, 0, 0))
                    .ThenBy(x => x.GroupName)
                    .ToList();
            }

            return result
                .Where(x => x.Lessons.Any())
                .ToList();
        }

        public async Task<List<string>> BuildWebsiteMatrixDebugAsync(
            string url)
        {
            var web = new HtmlWeb();

            var document = await web.LoadFromWebAsync(url);

            var rows = document.DocumentNode
                .SelectNodes("//tr");

            var lines = new List<string>();

            if (rows == null)
            {
                lines.Add("No rows found.");
                return lines;
            }

            var rowSpans =
                new Dictionary<(int Row, int Col), int>();

            for (int rowIndex = 0;
                 rowIndex < rows.Count;
                 rowIndex++)
            {
                var rowMap =
                    BuildRowMap(
                        rows[rowIndex],
                        rowIndex,
                        rowSpans);

                if (rowMap.Count == 0)
                {
                    lines.Add($"R{rowIndex:000}: <empty>");
                    continue;
                }

                var maxColumn =
                    rowMap.Keys.Max();

                var cells =
                    Enumerable.Range(0, maxColumn + 1)
                        .Select(col =>
                        {
                            var value =
                                rowMap.TryGetValue(
                                    col,
                                    out var text)
                                    ? text
                                    : "";

                            value = string.IsNullOrWhiteSpace(value)
                                ? "·"
                                : value.Replace("\n", " ");

                            return $"C{col:00}='{value}'";
                        });

                lines.Add(
                    $"R{rowIndex:000}: {string.Join(" | ", cells)}");
            }

            return lines;
        }

        public async Task<SourceCatalog> ParseWebsiteCatalogAsync(
            string url)
        {
            var web = new HtmlWeb();
            var document = await web.LoadFromWebAsync(url);

            var rows = document.DocumentNode
                .SelectNodes("//tr");

            var result = new SourceCatalog();

            if (rows == null)
            {
                return result;
            }

            var rowSpans =
                new Dictionary<(int Row, int Col), int>();

            var headers =
                new Dictionary<int, string>();

            var teacherSet =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int rowIndex = 0;
                 rowIndex < rows.Count;
                 rowIndex++)
            {
                var rowMap =
                    BuildRowMap(
                        rows[rowIndex],
                        rowIndex,
                        rowSpans);

                if (rowMap.Count == 0)
                {
                    continue;
                }

                var maxColumn = rowMap.Keys.Max();
                var rowTexts =
                    Enumerable.Range(0, maxColumn + 1)
                        .Select(col =>
                            rowMap.TryGetValue(col, out var value)
                                ? NormalizeCellText(value)
                                : "")
                        .ToList();

                if (IsGroupHeaderRow(rowTexts))
                {
                    headers.Clear();

                    for (int i = 2; i < rowTexts.Count; i++)
                    {
                        if (IsGroupCode(rowTexts[i]))
                        {
                            headers[i] = rowTexts[i];
                        }
                    }

                    continue;
                }

                if (rowTexts.Count < 1 ||
                    !int.TryParse(rowTexts[0], out _))
                {
                    continue;
                }

                if (rowIndex + 1 >= rows.Count)
                {
                    continue;
                }

                var teacherMap =
                    BuildRowMap(
                        rows[rowIndex + 1],
                        rowIndex + 1,
                        rowSpans);

                foreach (var header in headers)
                {
                    if (teacherMap.TryGetValue(
                        header.Key,
                        out var teacherCell))
                    {
                        foreach (var teacher in SplitTeachers(teacherCell))
                        {
                            if (IsTeacherName(teacher))
                            {
                                teacherSet.Add(teacher);
                            }
                        }
                    }
                }
            }

            result.Groups =
                headers.Values
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(x => x)
                    .ToList();

            result.Teachers =
                teacherSet
                    .OrderBy(x => x)
                    .ToList();

            return result;
        }

        private Dictionary<int, string> BuildRowMap(
            HtmlNode row,
            int rowIndex,
            Dictionary<(int Row, int Col), int> rowSpans)
        {
            var result =
                new Dictionary<int, string>();

            var cells =
                row.SelectNodes("td|th");

            if (cells == null)
            {
                return result;
            }

            int currentColumn = 0;

            foreach (var cell in cells)
            {
                while (rowSpans.TryGetValue(
                    (rowIndex, currentColumn),
                    out _))
                {
                    currentColumn++;
                }

                int colspan = 1;
                int rowspan = 1;

                if (cell.Attributes["colspan"] != null)
                {
                    int.TryParse(
                        cell.Attributes["colspan"].Value,
                        out colspan);
                }

                if (cell.Attributes["rowspan"] != null)
                {
                    int.TryParse(
                        cell.Attributes["rowspan"].Value,
                        out rowspan);
                }

                var text =
                    HtmlEntity
                    .DeEntitize(cell.InnerText)
                    .Replace("\r", "")
                    .Trim();

                for (int colOffset = 0;
                     colOffset < Math.Max(colspan, 1);
                     colOffset++)
                {
                    int colIndex =
                        currentColumn + colOffset;

                    result[colIndex] = text;

                    if (rowspan > 1)
                    {
                        for (int rowOffset = 1;
                             rowOffset < rowspan;
                             rowOffset++)
                        {
                            rowSpans[(rowIndex + rowOffset, colIndex)] =
                                rowspan - rowOffset;
                        }
                    }
                }

                currentColumn += colspan;
            }

            return result;
        }

        private TimeSpan? ExtractTime(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var match = Regex.Match(
                text,
                @"(\d{1,2})[.:](\d{2})");

            if (!match.Success)
            {
                return null;
            }

            int hour =
                int.Parse(match.Groups[1].Value);

            int minute =
                int.Parse(match.Groups[2].Value);

            return new TimeSpan(hour, minute, 0);
        }

        private bool IsGroupHeaderRow(List<string> rowTexts)
        {
            if (rowTexts.Count < 3)
            {
                return false;
            }

            if (int.TryParse(
                rowTexts[0],
                out _))
            {
                return false;
            }

            int groupLikeCells =
                rowTexts.Count(x =>
                    IsGroupCode(x));

            bool hasBellScheduleLabel =
                rowTexts.Any(x =>
                    x.Contains(
                        "Расписание",
                        StringComparison.OrdinalIgnoreCase));

            if (hasBellScheduleLabel)
            {
                return groupLikeCells >= 1;
            }

            return groupLikeCells >= 2;
        }

        private bool IsGroupCode(string text)
        {
            text =
                NormalizeCellText(text);

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            return Regex.IsMatch(
                text,
                @"^([А-ЯA-ZЁІЇЄҐ]{2,8}-\d{1,3}([-/]\d{1,3})?|[А-ЯA-ZЁІЇЄҐ]{2,8})$");
        }

        private bool IsDayCell(string text)
        {
            return text.Contains("Понедельник") ||
                   text.Contains("Вторник") ||
                   text.Contains("Среда") ||
                   text.Contains("Четверг") ||
                   text.Contains("Пятница") ||
                   text.Contains("Суббота");
        }

        private string NormalizeCellText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "";
            }

            return Regex.Replace(text, @"\s+", " ").Trim();
        }

        private List<string> SplitTeachers(string teacherCell)
        {
            if (string.IsNullOrWhiteSpace(teacherCell))
            {
                return new List<string>();
            }

            return teacherCell
                .Split(
                    '/',
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(x => NormalizeCellText(x))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private bool IsTeacherName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (value.Contains("СМГ", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return Regex.IsMatch(
                value,
                @"^[А-ЯЁІЇЄҐ][а-яёіїєґ\-']+\s+[А-ЯЁІЇЄҐ]\.\s*[А-ЯЁІЇЄҐ]\.?$");
        }
    }
}
