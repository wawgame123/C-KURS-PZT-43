using System.Collections.Generic;
using test.Models;

namespace test.Models.ViewModels
{
    public class HomeIndexViewModel
    {
        public List<Group> Groups { get; set; }

        public List<Teacher> Teachers { get; set; }

        public List<ScheduleDay> ScheduleDays { get; set; }

        public ScheduleFilterViewModel Filter { get; set; }
    }
}