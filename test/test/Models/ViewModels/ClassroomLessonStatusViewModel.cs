using test.Models;

namespace test.Models.ViewModels
{
    public class ClassroomLessonStatusViewModel
    {
        public int LessonNumber { get; set; }

        public List<ScheduleLesson> Lessons { get; set; } = new();

        public bool IsOccupied => Lessons.Count > 0;
    }
}
