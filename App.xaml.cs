using CMSLDF;
using System.Windows;

namespace CMSLDF // Adjust namespace accordingly
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Create and show the LoginWindow modally
            LoginWindow loginWindow = new LoginWindow();
            bool? dialogResult = loginWindow.ShowDialog(); // ShowDialog waits until the window is closed

            // Check if the login was successful (DialogResult set to true in LoginWindow)
            if (dialogResult == true)
            {
                // Login successful, proceed to MainWindow
                MainWindow mainWindow = new MainWindow();
                // Set the main window for the application
                this.MainWindow = mainWindow;
                mainWindow.Show();
            }
            else
            {
                // Login failed or was cancelled, shutdown the application
                // Optional: Show a message before shutdown
                // MessageBox.Show("Login required to proceed. Application will exit.", "Login Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                Application.Current.Shutdown();
            }
        }
    }
}