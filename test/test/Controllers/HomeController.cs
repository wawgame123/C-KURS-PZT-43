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

        public HomeController(
            IScheduleParserService scheduleParserService)
        {
            _scheduleParserService = scheduleParserService;
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

            var department =
                Request.Cookies["department"] ?? "VMSO";

            var groupCode =
                Request.Cookies["groupCode"] ?? "PZT";

            return await BuildView(
                mode,
                selectedTeacher,
                selectedGroup,
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
                filter.Department,
                filter.GroupCode);
        }

        private async Task<IActionResult> BuildView(
            string mode,
            string selectedTeacher,
            string selectedGroup,
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
                    foreach (var scheduleCode
                        in GetDepartmentScheduleCodes(
                            departmentItem.Key,
                            departmentItem.Value))
                    {
                        var url =
                            $"http://ggpk.by/Raspisanie/Files/{scheduleCode}.html";

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
                }
            }
            else
            {
                var selectedCodes =
                    DepartmentData.Departments.TryGetValue(
                        department,
                        out var departmentCodes)
                    ? GetDepartmentScheduleCodes(
                        department,
                        departmentCodes)
                    : new List<string>
                    {
                        ResolveScheduleCode(
                            department,
                            groupCode)
                    };

                var selectedScheduleCode =
                    ResolveScheduleCode(
                        department,
                        groupCode);

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
                        selectedScheduleCode,
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

            if (mode == "teacher" &&
                !string.IsNullOrWhiteSpace(selectedTeacher))
            {
                var selectedTeacherSubjectKeys =
                    BuildTeacherSubjectKeys(
                        days,
                        selectedTeacher);

                foreach (var day in days)
                {
                    var filteredLessons =
                        new List<ScheduleLesson>();

                    foreach (var lesson in day.Lessons)
                    {
                        var teacherLesson =
                            CreateTeacherSpecificLesson(
                                lesson,
                                selectedTeacher,
                                selectedTeacherSubjectKeys);

                        if (teacherLesson != null)
                        {
                            filteredLessons.Add(teacherLesson);
                        }
                    }

                    day.Lessons = filteredLessons
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

        private List<string> GetDepartmentScheduleCodes(
            string department,
            IEnumerable<string> configuredCodes)
        {
            var codes =
                configuredCodes
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();

            if (codes.Count > 0)
            {
                return codes;
            }

            return new List<string>
            {
                department
            };
        }

        private string ResolveScheduleCode(
            string department,
            string groupCode)
        {
            return string.IsNullOrWhiteSpace(groupCode)
                ? department
                : groupCode;
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
            return GetTeacherPartIndex(
                teacherCell,
                selectedTeacher) >= 0;
        }

        private ScheduleLesson? CreateTeacherSpecificLesson(
            ScheduleLesson lesson,
            string selectedTeacher,
            HashSet<string> selectedTeacherSubjectKeys)
        {
            if (string.IsNullOrWhiteSpace(lesson.Teacher))
            {
                return null;
            }

            var teacherParts =
                SplitCellParts(lesson.Teacher);

            var teacherIndex =
                GetTeacherPartIndex(
                    lesson.Teacher,
                    selectedTeacher);

            if (teacherIndex < 0 ||
                teacherIndex >= teacherParts.Count)
            {
                return null;
            }

            var subjectSelection =
                PickParallelPart(
                    lesson.Subject,
                    teacherIndex,
                    teacherParts.Count,
                    selectedTeacherSubjectKeys);

            var classroomSelection =
                PickParallelPart(
                    lesson.Classroom,
                    teacherIndex,
                    teacherParts.Count,
                    null);

            var selectedTime =
                PickLessonTime(
                    lesson.Time,
                    subjectSelection.Value,
                    subjectSelection.PartIndex,
                    subjectSelection.WasSplit);

            return new ScheduleLesson
            {
                LessonNumber =
                    lesson.LessonNumber,

                Time =
                    selectedTime,

                Subject =
                    subjectSelection.Value,

                Teacher =
                    teacherParts[teacherIndex],

                Classroom =
                    classroomSelection.Value,

                GroupName =
                    lesson.GroupName,

                SortTime =
                    ExtractSortTime(selectedTime) ??
                    lesson.SortTime
            };
        }

        private int GetTeacherPartIndex(
            string teacherCell,
            string selectedTeacher)
        {
            var selected =
                NormalizeTeacherKey(selectedTeacher);

            if (string.IsNullOrWhiteSpace(selected))
            {
                return -1;
            }

            var teacherParts =
                SplitCellParts(teacherCell);

            for (int i = 0; i < teacherParts.Count; i++)
            {
                if (NormalizeTeacherKey(teacherParts[i]) == selected)
                {
                    return i;
                }
            }

            return -1;
        }

        private (
            string Value,
            int PartIndex,
            bool WasSplit) PickParallelPart(
            string value,
            int partIndex,
            int expectedPartCount,
            HashSet<string>? preferredPartKeys)
        {
            var parts =
                SplitCellParts(value);

            if (parts.Count == expectedPartCount &&
                partIndex < parts.Count)
            {
                return (
                    parts[partIndex],
                    partIndex,
                    true);
            }

            if (preferredPartKeys != null &&
                parts.Count > 1)
            {
                for (int i = 0; i < parts.Count; i++)
                {
                    if (preferredPartKeys.Contains(
                        NormalizeSubjectKey(parts[i])))
                    {
                        return (
                            parts[i],
                            i,
                            true);
                    }
                }
            }

            if (preferredPartKeys != null &&
                expectedPartCount > 1 &&
                partIndex < parts.Count)
            {
                return (
                    parts[partIndex],
                    partIndex,
                    true);
            }

            if (preferredPartKeys != null &&
                expectedPartCount == 1 &&
                partIndex == 0 &&
                parts.Count > 1)
            {
                return (
                    parts[^1],
                    parts.Count - 1,
                    true);
            }

            return (
                NormalizeText(value),
                -1,
                false);
        }

        private string PickLessonTime(
            string time,
            string subject,
            int subjectPartIndex,
            bool subjectWasSplit)
        {
            var normalizedTime =
                NormalizeText(time);

            var intervals =
                System.Text.RegularExpressions.Regex
                    .Matches(
                        normalizedTime,
                        @"\d{1,2}[.:]\d{2}\s*-\s*\d{1,2}[.:]\d{2}")
                    .Select(x => NormalizeText(x.Value))
                    .ToList();

            if (intervals.Count == 0)
            {
                return normalizedTime;
            }

            if (subjectWasSplit &&
                IsOneHourSubject(subject) &&
                subjectPartIndex >= 0 &&
                subjectPartIndex < intervals.Count)
            {
                return intervals[subjectPartIndex];
            }

            var hourNumber =
                ExtractSubjectHourNumber(subject);

            if (hourNumber.HasValue &&
                hourNumber.Value > 1 &&
                hourNumber.Value <= intervals.Count)
            {
                return intervals[hourNumber.Value - 1];
            }

            return normalizedTime;
        }

        private bool IsOneHourSubject(string subject)
        {
            return System.Text.RegularExpressions.Regex
                .IsMatch(
                    NormalizeText(subject),
                    "(^|\\s)1\\s*\\u0447\\u0430\\u0441",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        private int? ExtractSubjectHourNumber(string subject)
        {
            var match =
                System.Text.RegularExpressions.Regex
                    .Match(
                        NormalizeText(subject),
                        "(^|\\s)(\\d+)\\s*\\u0447\\u0430\\u0441",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (!match.Success ||
                !int.TryParse(
                    match.Groups[2].Value,
                    out var hourNumber))
            {
                return null;
            }

            return hourNumber;
        }

        private TimeSpan? ExtractSortTime(string time)
        {
            var match =
                System.Text.RegularExpressions.Regex
                    .Match(
                        NormalizeText(time),
                        @"(\d{1,2})[.:](\d{2})");

            if (!match.Success ||
                !int.TryParse(
                    match.Groups[1].Value,
                    out var hours) ||
                !int.TryParse(
                    match.Groups[2].Value,
                    out var minutes))
            {
                return null;
            }

            return new TimeSpan(
                hours,
                minutes,
                0);
        }

        private HashSet<string> BuildTeacherSubjectKeys(
            List<ScheduleDay> days,
            string selectedTeacher)
        {
            var result =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (var lesson in days.SelectMany(x => x.Lessons))
            {
                if (!TeacherMatches(
                    lesson.Teacher,
                    selectedTeacher))
                {
                    continue;
                }

                var subjectParts =
                    SplitCellParts(lesson.Subject);

                var teacherParts =
                    SplitCellParts(lesson.Teacher);

                var teacherIndex =
                    GetTeacherPartIndex(
                        lesson.Teacher,
                        selectedTeacher);

                if (teacherIndex < 0)
                {
                    continue;
                }

                if (subjectParts.Count == teacherParts.Count &&
                    teacherIndex < subjectParts.Count)
                {
                    result.Add(
                        NormalizeSubjectKey(subjectParts[teacherIndex]));
                }
                else if (subjectParts.Count == 1)
                {
                    result.Add(
                        NormalizeSubjectKey(subjectParts[0]));
                }
            }

            return result;
        }

        private string NormalizeSubjectKey(string subject)
        {
            return NormalizeText(subject)
                .ToUpperInvariant();
        }

        private List<string> SplitCellParts(string value)
        {
            var normalized =
                NormalizeText(value);

            if (string.IsNullOrWhiteSpace(normalized))
            {
                return new List<string>();
            }

            return System.Text.RegularExpressions.Regex
                .Split(
                    normalized,
                    "(?<=\\s)[/\\u2215\\u2044\\uFF0F\\\\]|" +
                    "[/\\u2215\\u2044\\uFF0F\\\\](?=\\s)|" +
                    "[/\\u2215\\u2044\\uFF0F\\\\](?=\\s*\\p{Lu})|" +
                    "(?<=\\d)[/\\u2215\\u2044\\uFF0F\\\\](?=\\d)|" +
                    "(?<=\\b\\u0447\\u0430\\u0441)\\s+(?=\\p{Lu})")
                .Select(x => NormalizeText(x))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
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
