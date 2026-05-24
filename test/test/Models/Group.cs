using System.ComponentModel.DataAnnotations;

namespace test.Models
{
    public class Group
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }
    }
}
