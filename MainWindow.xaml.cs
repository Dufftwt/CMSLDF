using System;
using System.Windows;
using System.Windows.Controls; // Needed for TextBlock, RadioButton

namespace CMSLDF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            LoadInitialView();
        }

        private void LoadInitialView()
        {
            if (NavCamionsRadioButton.IsChecked == true)
            {
                NavigateTo(NavCamionsRadioButton);
            }
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
            // Prevent null reference exceptions if called before elements are loaded
            if (HeaderTitleTextBlock == null || MainContentArea == null)
            {
                return;
            }

            string newTitle = "Unknown";
            object newContent = null; // Use object type for flexibility

            if (target == NavCamionsRadioButton)
            {
                newTitle = "Nos camions";
                // Replace with actual UserControl: new CamionsView();
                newContent = new TextBlock
                {
                    Text = "Contenu - Nos camions",
                    FontSize = 24,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
            else if (target == NavDepotVenteRadioButton)
            {
                newTitle = "Dépôt-Vente";
                // Replace with actual UserControl: new DepotVenteView();
                newContent = new TextBlock
                {
                    Text = "Contenu - Dépôt-Vente",
                    FontSize = 24,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
            else if (target == NavInfosRadioButton)
            {
                newTitle = "Nos infos";
                // Replace with actual UserControl: new InfosView();
                newContent = new TextBlock
                {
                    Text = "Contenu - Nos infos",
                    FontSize = 24,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }

            // Update the UI elements
            HeaderTitleTextBlock.Text = newTitle;
            MainContentArea.Content = newContent;
        }
    }
}