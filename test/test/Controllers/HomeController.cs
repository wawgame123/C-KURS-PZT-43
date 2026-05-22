using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using test.Data;
using test.Models;
using test.Models.ViewModels;
using test.Services.Interfaces;

namespace test.Controllers
{
    public class HomeController : Controller
    {
        private readonly IScheduleParserService _scheduleParserService;

        private readonly IWebHostEnvironment _environment;

        public HomeController(
            IScheduleParserService scheduleParserService,
            IWebHostEnvironment environment)
        {
            _scheduleParserService = scheduleParserService;

            _environment = environment;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var mode =
                Request.Cookies["mode"] ?? "student";

            var selectedTeacher =
                Request.Cookies["selectedTeacher"] ?? "";

            var selectedGroup =
                Request.Cookies["selectedGroup"] ?? "";

            var sourceType =
                Request.Cookies["sourceType"] ?? "file";

            var department =
                Request.Cookies["department"] ?? "VMSO";

            var groupCode =
                Request.Cookies["groupCode"] ?? "PZT";

            return await BuildView(
                mode,
                selectedTeacher,
                selectedGroup,
                sourceType,
                department,
                groupCode);
        }

        [HttpPost]
        public async Task<IActionResult> Index(
            ScheduleFilterViewModel filter)
        {
            Response.Cookies.Append(
                "mode",
                filter.Mode ?? "student");

            Response.Cookies.Append(
                "selectedTeacher",
                filter.SelectedTeacher ?? "");

            Response.Cookies.Append(
                "selectedGroup",
                filter.SelectedGroup ?? "");

            Response.Cookies.Append(
                "sourceType",
                filter.SourceType ?? "file");

            Response.Cookies.Append(
                "department",
                filter.Department ?? "VMSO");

            Response.Cookies.Append(
                "groupCode",
                filter.GroupCode ?? "PZT");

            SaveDepartmentGroupSelection(
                filter.Department ?? "VMSO",
                filter.SelectedGroup ?? "");

            return await BuildView(
                filter.Mode,
                filter.SelectedTeacher,
                filter.SelectedGroup,
                filter.SourceType,
                filter.Department,
                filter.GroupCode);
        }

        private async Task<IActionResult> BuildView(
            string mode,
            string selectedTeacher,
            string selectedGroup,
            string sourceType,
            string department,
            string groupCode)
        {
            selectedGroup =
                ResolveSelectedGroupForDepartment(
                    department,
                    selectedGroup);

            var effectiveSelectedGroup =
                selectedGroup;

            List<ScheduleDay> days =
                new List<ScheduleDay>();

            List<string> debugMatrixLines =
                new List<string>();

            var groupsCatalog =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            var teachersCatalog =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            if (mode == "teacher")
            {
                foreach (var departmentItem
                    in DepartmentData.Departments)
                {
                    foreach (var groupItem
                        in departmentItem.Value)
                    {
                        if (sourceType == "site")
                        {
                            var url =
                                $"http://ggpk.by/Raspisanie/Files/{groupItem}.html";

                            var catalog =
                                await _scheduleParserService
                                    .ParseWebsiteCatalogAsync(url);

                            foreach (var groupName in catalog.Groups)
                            {
                                groupsCatalog.Add(groupName);
                            }

                            foreach (var teacherName in catalog.Teachers)
                            {
                                AddNormalizedUnique(
                                    teachersCatalog,
                                    teacherName);
                            }

                            var parsed =
                                await _scheduleParserService
                                    .ParseWebsiteAsync(
                                        url,
                                        mode,
                                        selectedTeacher,
                                        selectedGroup);

                            MergeDays(days, parsed);
                        }
                        else
                        {
                            var filePath = Path.Combine(
                                _environment.ContentRootPath,
                                "excel",
                                departmentItem.Key,
                                groupItem,
                                "schedule.xlsx");

                            if (!System.IO.File.Exists(filePath))
                            {
                                continue;
                            }

                            var parsed =
                                await _scheduleParserService
                                    .ParseExcelAsync(filePath);

                            MergeDays(days, parsed);
                        }
                    }
                }
            }
            else
            {
                if (sourceType == "site")
                {
                    var selectedCodes =
                        DepartmentData.Departments.TryGetValue(
                            department,
                            out var departmentCodes)
                        ? departmentCodes
                        : new List<string> { groupCode };

                    foreach (var code in selectedCodes)
                    {
                        var url =
                            $"http://ggpk.by/Raspisanie/Files/{code}.html";

                        var catalog =
                            await _scheduleParserService
                                .ParseWebsiteCatalogAsync(url);

                        foreach (var groupName in catalog.Groups)
                        {
                            groupsCatalog.Add(groupName);
                        }

                        foreach (var teacherName in catalog.Teachers)
                        {
                            AddNormalizedUnique(
                                teachersCatalog,
                                teacherName);
                        }

                        if (code.Equals(
                            groupCode,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            if (!string.IsNullOrWhiteSpace(
                                effectiveSelectedGroup) &&
                                !groupsCatalog.Contains(
                                    effectiveSelectedGroup))
                            {
                                effectiveSelectedGroup = "";
                            }

                            debugMatrixLines =
                                await _scheduleParserService
                                    .BuildWebsiteMatrixDebugAsync(url);

                            var parsedDays =
                                await _scheduleParserService
                                    .ParseWebsiteAsync(
                                        url,
                                        mode,
                                        selectedTeacher,
                                        effectiveSelectedGroup);

                            MergeDays(days, parsedDays);
                        }
                    }
                }
                else
                {
                    var filePath = Path.Combine(
                        _environment.ContentRootPath,
                        "excel",
                        department,
                        groupCode,
                        "schedule.xlsx");

                    if (System.IO.File.Exists(filePath))
                    {
                        days =
                            await _scheduleParserService
                                .ParseExcelAsync(filePath);
                    }
                }
            }

            if (mode == "teacher" &&
                !string.IsNullOrWhiteSpace(selectedTeacher))
            {
                foreach (var day in days)
                {
                    day.Lessons = day.Lessons
                        .Where(x =>
                            !string.IsNullOrWhiteSpace(x.Teacher) &&
                            TeacherMatches(
                                x.Teacher,
                                selectedTeacher))
                        .OrderBy(x => x.SortTime)
                        .ThenBy(x => x.LessonNumber)
                        .ToList();
                }

                days = days
                    .Where(x => x.Lessons.Any())
                    .ToList();
            }

            if (mode == "student")
            {
                foreach (var day in days)
                {
                    day.Lessons = day.Lessons
                        .Where(x =>
                            string.IsNullOrWhiteSpace(effectiveSelectedGroup) ||
                            x.GroupName == effectiveSelectedGroup)
                        .OrderBy(x => x.SortTime)
                        .ThenBy(x => x.LessonNumber)
                        .ToList();
                }

                days = days
                    .Where(x => x.Lessons.Any())
                    .ToList();
            }

            if (groupsCatalog.Count == 0)
            {
                foreach (var groupName in days
                    .SelectMany(x => x.Lessons)
                    .Select(x => x.GroupName)
                    .Where(x => !string.IsNullOrWhiteSpace(x)))
                {
                    groupsCatalog.Add(groupName);
                }
            }

            if (teachersCatalog.Count == 0)
            {
                foreach (var teacherName in days
                    .SelectMany(x => x.Lessons)
                    .SelectMany(x => SplitTeachers(x.Teacher)))
                {
                    AddNormalizedUnique(
                        teachersCatalog,
                        teacherName);
                }
            }

            var groupsFromSource =
                groupsCatalog
                    .OrderByDescending(x => x)
                    .Select(x => new Group { Name = x })
                    .ToList();

            var teachersFromSource =
                teachersCatalog
                    .OrderBy(x => x)
                    .Select(x => new Teacher { FullName = x })
                    .ToList();

            if (!string.IsNullOrWhiteSpace(effectiveSelectedGroup) &&
                !groupsFromSource.Any(x =>
                    x.Name.Equals(
                        effectiveSelectedGroup,
                        StringComparison.OrdinalIgnoreCase)))
            {
                effectiveSelectedGroup = "";
            }

            if (!string.IsNullOrWhiteSpace(selectedTeacher) &&
                !teachersFromSource.Any(x =>
                    x.FullName.Equals(
                        selectedTeacher,
                        StringComparison.OrdinalIgnoreCase)))
            {
                teachersFromSource.Add(
                    new Teacher
                    {
                        FullName = selectedTeacher
                    });
            }

            var model =
                new HomeIndexViewModel
                {
                    Groups =
                        groupsFromSource
                            .OrderByDescending(x => x.Name)
                            .ToList(),

                    Teachers =
                        teachersFromSource
                            .OrderBy(x => x.FullName)
                            .ToList(),

                    ScheduleDays = days,

                    Departments =
                        DepartmentData.Departments,

                    Filter =
                        new ScheduleFilterViewModel
                        {
                            Mode = mode,

                            SelectedTeacher =
                                selectedTeacher,

                            SelectedGroup =
                                effectiveSelectedGroup,

                            SourceType =
                                sourceType,

                            Department =
                                department,

                            GroupCode =
                                groupCode
                        },

                    DebugMatrixLines =
                        debugMatrixLines
                };

            return View(model);
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
                .Select(x => NormalizeText(x))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private string NormalizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "";
            }

            return System.Text.RegularExpressions.Regex
                .Replace(text, @"\s+", " ")
                .Trim();
        }

        private void AddNormalizedUnique(
            HashSet<string> target,
            string value)
        {
            var normalized =
                NormalizeText(value);

            if (!string.IsNullOrWhiteSpace(normalized))
            {
                target.Add(normalized);
            }
        }

        private bool TeacherMatches(
            string teacherCell,
            string selectedTeacher)
        {
            var selected =
                NormalizeTeacherKey(selectedTeacher);

            if (string.IsNullOrWhiteSpace(selected))
            {
                return false;
            }

            return SplitTeachers(teacherCell)
                .Select(NormalizeTeacherKey)
                .Any(x => x == selected);
        }

        private string NormalizeTeacherKey(string teacher)
        {
            var normalized =
                NormalizeText(teacher)
                    .ToUpperInvariant()
                    .Replace(".", "")
                    .Replace(" ", "");

            return normalized;
        }

        private void SaveDepartmentGroupSelection(
            string department,
            string selectedGroup)
        {
            var map =
                ReadDepartmentGroupMap();

            if (string.IsNullOrWhiteSpace(selectedGroup))
            {
                map.Remove(department);
            }
            else
            {
                map[department] = selectedGroup;
            }

            Response.Cookies.Append(
                "departmentGroupMap",
                JsonSerializer.Serialize(map));
        }

        private string ResolveSelectedGroupForDepartment(
            string department,
            string selectedGroup)
        {
            if (!string.IsNullOrWhiteSpace(selectedGroup))
            {
                return selectedGroup;
            }

            var map =
                ReadDepartmentGroupMap();

            if (map.TryGetValue(
                department,
                out var storedGroup))
            {
                return storedGroup;
            }

            return "";
        }

        private Dictionary<string, string> ReadDepartmentGroupMap()
        {
            var json =
                Request.Cookies["departmentGroupMap"];

            if (string.IsNullOrWhiteSpace(json))
            {
                return new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                var map =
                    JsonSerializer.Deserialize<
                        Dictionary<string, string>>(json);

                return map ??
                    new Dictionary<string, string>(
                        StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);
            }
        }

        private void MergeDays(
            List<ScheduleDay> target,
            List<ScheduleDay> source)
        {
            foreach (var day in source)
            {
                var existingDay =
                    target.FirstOrDefault(x =>
                        x.DayTitle == day.DayTitle);

                if (existingDay == null)
                {
                    existingDay =
                        new ScheduleDay
                        {
                            DayTitle = day.DayTitle,
                            Date = day.Date
                        };

                    target.Add(existingDay);
                }

                existingDay.Lessons
                    .AddRange(day.Lessons);
            }
        }
    }
}
