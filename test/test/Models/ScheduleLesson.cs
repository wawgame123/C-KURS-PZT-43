namespace test.Models
{
    public class ScheduleLesson
    {
        public int LessonNumber { get; set; }

        public string Time { get; set; }

        public string Subject { get; set; }

        public string Teacher { get; set; }

        public string Classroom { get; set; }

        public string GroupName { get; set; }

        public TimeSpan? SortTime { get; set; }
    }
}