using test.Models;

namespace test.Models.ViewModels
{
    public class ClassroomsViewModel
    {
        public List<ScheduleDay> Days { get; set; } = new();

        public string SelectedDayTitle { get; set; } = "";

        public List<int> LessonNumbers { get; set; } = new();

        public int? SelectedLessonNumber { get; set; }

        public bool ShowAllLessons => SelectedLessonNumber == -1;

        public string Search { get; set; } = "";

        public List<ClassroomOccupancyViewModel> Classrooms { get; set; } = new();

        public int FreeCount => Classrooms.Count(x => !x.IsOccupied);

        public int OccupiedCount => Classrooms.Count(x => x.IsOccupied);

        public int FailedSourceCount { get; set; }
    }
}
