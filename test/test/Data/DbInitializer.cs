using System.Linq;
using test.Models;

namespace test.Data
{
    public static class DbInitializer
    {
        public static void Initialize(ApplicationDbContext context)
        {
            context.Database.EnsureCreated();

            if (context.Groups.Any())
            {
                return;
            }

            var groups = new Group[]
            {
                new Group { Name = "ПЗТ-40" },
                new Group { Name = "ПЗТ-41" },
                new Group { Name = "ПЗТ-42" },
                new Group { Name = "ПЗТ-43" },
                new Group { Name = "ПЗТ-44" },
                new Group { Name = "ПЗТ-45" }
            };

            context.Groups.AddRange(groups);
            context.SaveChanges();

            var teachers = new Teacher[]
            {
                new Teacher { FullName = "Орехво В.Д." },
                new Teacher { FullName = "Кизер О.И." },
                new Teacher { FullName = "Заш Е.В." },
                new Teacher { FullName = "Бабуль А.Г." },
                new Teacher { FullName = "Лукша И.А." }
            };

            context.Teachers.AddRange(teachers);
            context.SaveChanges();

            var curatedGroups = new CuratedGroup[]
            {
                new CuratedGroup
                {
                    TeacherId = teachers[0].Id,
                    GroupId = groups[0].Id
                },

                new CuratedGroup
                {
                    TeacherId = teachers[1].Id,
                    GroupId = groups[1].Id
                },

                new CuratedGroup
                {
                    TeacherId = teachers[2].Id,
                    GroupId = groups[2].Id
                }
            };

            context.CuratedGroups.AddRange(curatedGroups);
            context.SaveChanges();
        }
    }
}