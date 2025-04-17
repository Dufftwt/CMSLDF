using System;
using System.Windows;
using System.Windows.Controls;

namespace CMSLDF
{
    public partial class MainWindow : Window
    {
        // Keep references to potentially reuse views (optional optimization)
        private CamionsView _camionsView;
        private DepotVenteView _depotVenteView;
        //private InfosView _infosView;

        public MainWindow()
        {
            InitializeComponent();
            // Optional: Pre-load the initial view if needed
            this.Closed += MainWindow_Closed;
            LoadInitialView();
        }

        // Event handler for the Window's Closed event
        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            // Explicitly shut down the application when the main window is closed.
            Application.Current.Shutdown();
        }

        private void LoadInitialView()
        {
            NavigateTo(NavCamionsRadioButton);
        }

        private void NavigationRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton checkedRadioButton)
            {
                NavigateTo(checkedRadioButton);
            }
        }

        private void NavigateTo(RadioButton target)
        {
            if (HeaderTitleTextBlock == null || MainContentArea == null)
            {
                return;
            }

            string newTitle = "Unknown";
            object newContent = null; // Content type is now UserControl (or object)

            if (target == NavCamionsRadioButton)
            {
                newTitle = "Nos camions";
                // *** MODIFIED: Instantiate or reuse CamionsView ***
                if (_camionsView == null) // Create only if it doesn't exist
                    _camionsView = new CamionsView();
                newContent = _camionsView;
            }
            else if (target == NavDepotVenteRadioButton)
            {
                newTitle = "Dépôt-Vente";
                // *** MODIFIED: Instantiate or reuse DepotVenteView ***
                if (_depotVenteView == null)
                    _depotVenteView = new DepotVenteView();
                newContent = _depotVenteView;
            }
            /*else if (target == NavInfosRadioButton)
            {
                newTitle = "Nos infos";
                // *** MODIFIED: Instantiate or reuse InfosView ***
                if (_infosView == null)
                    _infosView = new InfosView();
                newContent = _infosView;
            }*/

            // Update the UI elements
            HeaderTitleTextBlock.Text = newTitle;
            MainContentArea.Content = newContent; // Set the ContentControl's content
        }
    }
}