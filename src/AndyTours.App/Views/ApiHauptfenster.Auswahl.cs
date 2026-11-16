using System.Windows.Controls;
using AndyTours.App.ViewModels;

namespace AndyTours.App.Views;

public partial class ApiHauptfenster
{
    private void TourenDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is DataGrid grid)
            _viewModel.SetzeTourenauswahl(grid.SelectedItems.Count);
    }
}
