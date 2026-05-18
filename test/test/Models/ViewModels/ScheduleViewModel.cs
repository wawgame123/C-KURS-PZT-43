using System.Collections.Generic;
using test.Models;

namespace test.Models.ViewModels
{
    public class ScheduleViewModel
    {
        public List<ScheduleDay> Days { get; set; } = new();
    }
}