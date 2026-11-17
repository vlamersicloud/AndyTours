using System.Windows.Controls;
using AndyTours.App.ViewModels;

namespace AndyTours.App.Views;

public partial class ApiHauptfenster
{
    private void TourenDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        VerarbeiteManuelleTourenauswahl(sender, e);
    }
}
