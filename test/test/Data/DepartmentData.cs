namespace test.Data
{
    public static class DepartmentData
    {
        public static Dictionary<string, List<string>> Departments =
            new Dictionary<string, List<string>>
            {
                {
                    "MRSO",
                    new List<string>
                    {
                        "TAR",
                        "BSB"
                    }
                },

                {
                    "PGS",
                    new List<string>
                    {
                        "BDA",
                        "PGB"
                    }
                },

                {
                    "VMSO",
                    new List<string>
                    {
                        "PZT",
                        "AEP"
                    }
                },

                {
                    "PTOO",
                    new List<string>
                    {
                        "AGB"
                    }
                },

                {
                    "P_KURS",
                    new List<string>()
                }
            };

        public static Dictionary<string, string> DepartmentTitles =
            new Dictionary<string, string>
            {
                {
                    "MRSO",
                    "ОТДЕЛЕНИЕ МАШИНОСТРОЕНИЯ И ЭКОНОМИКИ"
                },

                {
                    "PGS",
                    "ОТДЕЛЕНИЕ СТРОИТЕЛЬНЫХ ТЕХНОЛОГИЙ"
                },

                {
                    "VMSO",
                    "ОТДЕЛЕНИЕ АВТОМАТИЗАЦИИ И ИНФОРМАТИЗАЦИИ"
                },

                {
                    "PTOO",
                    "ОТДЕЛЕНИЕ АРХИТЕКТУРЫ И ОБЩЕГО СРЕДНЕГО ОБРАЗОВАНИЯ"
                },

                {
                    "P_KURS",
                    "ПЕРВЫЙ КУРС"
                }
            };
    }
}