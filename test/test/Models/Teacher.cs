using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace test.Models
{
    public class Teacher
    {
        public int Id { get; set; }

        [Required]
        public string FullName { get; set; }

        public ICollection<CuratedGroup> CuratedGroups { get; set; }
    }
}