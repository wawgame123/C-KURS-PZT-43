using test.Models;

namespace test.Data
{
    public static class ClassroomData
    {
        public static IReadOnlyList<Classroom> Classrooms { get; } =
            BuildClassrooms();

        private static List<Classroom> BuildClassrooms()
        {
            var classrooms = new List<Classroom>();

            AddRange(
                classrooms,
                "building-2",
                "Второй корпус",
                1,
                19);

            AddRange(
                classrooms,
                "building-1",
                "Первый корпус",
                101,
                109);

            AddRange(
                classrooms,
                "building-1",
                "Первый корпус",
                201,
                207);

            AddRange(
                classrooms,
                "building-1",
                "Первый корпус",
                301,
                313);

            AddRange(
                classrooms,
                "dormitory",
                "Общежитие",
                1,
                3);

            AddRange(
                classrooms,
                "dormitory",
                "Общежитие",
                5,
                5);

            return classrooms;
        }

        private static void AddRange(
            List<Classroom> classrooms,
            string buildingCode,
            string buildingName,
            int start,
            int end)
        {
            for (var number = start; number <= end; number++)
            {
                classrooms.Add(new Classroom
                {
                    Key = $"{buildingCode}:{number}",
                    Number = number.ToString(),
                    BuildingCode = buildingCode,
                    BuildingName = buildingName
                });
            }
        }
    }
}
