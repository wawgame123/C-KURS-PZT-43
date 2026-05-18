using System;
using System.Collections.Generic;

namespace test.Models
{
    public class ScheduleDay
    {
        public DateTime Date { get; set; }

        public string DayTitle { get; set; }

        public List<ScheduleLesson> Lessons { get; set; } = new();
    }
}