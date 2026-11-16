using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AndyTours.App.ViewModels;

public partial class StreckenkarteViewModel
{
    internal Func<IReadOnlyList<Coursezeile>, Task>? ApiCourseUebertragung { get; set; }
}