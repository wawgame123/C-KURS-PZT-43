using test.Models;

namespace test.Models.ViewModels
{
    public class ClassroomOccupancyViewModel
    {
        public Classroom Classroom { get; set; } = new();

        public List<ScheduleLesson> Lessons { get; set; } = new();

        public List<ClassroomLessonStatusViewModel> LessonStatuses { get; set; } = new();

        public bool IsOccupied => Lessons.Count > 0;
    }
}
