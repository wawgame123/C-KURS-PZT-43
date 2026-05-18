using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using test.Data;
using test.Models.ViewModels;
using test.Services.Interfaces;

namespace test.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IScheduleParserService _scheduleParserService;
        private readonly IWebHostEnvironment _environment;

        public HomeController(
            ApplicationDbContext context,
            IScheduleParserService scheduleParserService,
            IWebHostEnvironment environment)
        {
            _context = context;
            _scheduleParserService = scheduleParserService;
            _environment = environment;
        }

        public async Task<IActionResult> Index(
     string type = "weekly",
     string mode = "student",
     string selectedTeacher = "",
     string selectedGroup = "")
        {
            var fileName = type == "weekly"
                ? "weekly.xlsx"
                : "general.xlsx";

            var filePath = Path.Combine(
                _environment.WebRootPath,
                "schedules",
                fileName);

            var days = await _scheduleParserService.ParseExcelAsync(filePath);

            if (mode == "teacher" &&
                !string.IsNullOrWhiteSpace(selectedTeacher))
            {
                foreach (var day in days)
                {
                    day.Lessons = day.Lessons
                        .Where(x =>
                            !string.IsNullOrWhiteSpace(x.Teacher) &&
                            x.Teacher.Contains(selectedTeacher))
                        .ToList();
                }

                days = days
                    .Where(x => x.Lessons.Any())
                    .ToList();
            }

            if (mode == "student" &&
                !string.IsNullOrWhiteSpace(selectedGroup))
            {
                foreach (var day in days)
                {
                    day.Lessons = day.Lessons
                        .Where(x =>
                            x.GroupName == selectedGroup)
                        .ToList();
                }

                days = days
                    .Where(x => x.Lessons.Any())
                    .ToList();
            }

            var model = new HomeIndexViewModel
            {
                Groups = await _context.Groups.ToListAsync(),

                Teachers = await _context.Teachers.ToListAsync(),

                ScheduleDays = days,

                Filter = new ScheduleFilterViewModel
                {
                    Mode = mode,
                    SelectedTeacher = selectedTeacher,
                    SelectedGroup = selectedGroup,
                    ScheduleType = type
                }
            };

            return View(model);
        }

        public async Task<IActionResult> ParseWebsite(string type)
        {
            var url = type == "weekly"
                ? "http://ggpk.by/Raspisanie/Files/PZT.html"
                : "http://ggpk.by/Raspisanie/Files/VMSO.html";

            var days = await _scheduleParserService.ParseWebsiteAsync(url);

            var model = new ScheduleViewModel
            {
                Days = days
            };

            return View("Schedule", model);
        }
    }
}