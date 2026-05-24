using System.ComponentModel.DataAnnotations;

namespace test.Models
{
    public class Teacher
    {
        public int Id { get; set; }

        [Required]
        public string FullName { get; set; }
    }
}
