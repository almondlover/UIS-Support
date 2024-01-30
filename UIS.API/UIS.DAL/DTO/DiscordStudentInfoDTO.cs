using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UIS.DAL.DTO
{
    public class DiscordStudentInfoDTO
    {
        public string degree { get; set; }

        public int course { get; set; }

        public string faculty { get; set; }

        public string facultyNumber { get; set; }

        public string names { get; set; }

        public string specialty { get; set; }
    }
}
