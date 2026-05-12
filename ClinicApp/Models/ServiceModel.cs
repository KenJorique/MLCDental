using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SQLite;

namespace ClinicApp.Models;

public class ServiceModel
{
    [PrimaryKey, AutoIncrement]
    public int ServiceID { get; set; }

    public string ServiceName { get; set; }

    public double Price { get; set; }

    public string Description { get; set; }
}
