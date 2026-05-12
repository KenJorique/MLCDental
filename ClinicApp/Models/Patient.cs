using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SQLite;

namespace ClinicApp.Models;

public class Patient
{
    [PrimaryKey, AutoIncrement]
    public int PatientID { get; set; }

    public string FirstName { get; set; }

    public string LastName { get; set; }

    public string ContactNumber { get; set; }

    public string Address { get; set; }

    public string MedicalHistory { get; set; }

    public bool HasNoMedicalHistory { get; set; }
}
