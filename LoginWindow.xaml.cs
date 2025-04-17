using System;
using System.Windows;
using System.Threading.Tasks;
using CMSLDF;
using System.Windows.Controls;

namespace CMSLDF // Adjust namespace accordingly
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
            UsernameTextBox.Focus(); // Set initial focus
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameTextBox.Text;
            string password = PasswordBox.Password; // Get password from PasswordBox

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                StatusTextBlock.Text = "Le nom d'utilisateur et le mot de passe ne peuvent pas être vide.";
                return;
            }

            // Disable controls during login attempt
            SetLoadingState(true);
            StatusTextBlock.Text = "Connexion en cours..."; // Provide feedback

            try
            {
                // Call the static login method from AuthManager
                string? token = await AuthManager.GetAuthTokenAsync(username, password);

                if (!string.IsNullOrEmpty(token))
                {
                    // Login successful! AuthManager already stored the token.
                    // Set DialogResult to true to indicate success to the caller (App.xaml.cs)
                    this.DialogResult = true;
                    this.Close(); // Close the login window
                }
                else
                {
                    // Login failed, GetAuthTokenAsync already showed a MessageBox
                    StatusTextBlock.Text = "L'authentification a échoué. Réessayez ou vérifiez le statut du serveur.";
                }
            }
            catch (Exception ex)
            {
                // Catch potential exceptions from the async void pattern (though handled in AuthManager)
                StatusTextBlock.Text = $"Une erreur s'est produite: {ex.Message}";
                MessageBox.Show($"Une erreur s'est produite: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Re-enable controls regardless of success or failure (unless window closed)
                if (this.IsVisible) // Only re-enable if the window didn't close
                {
                    SetLoadingState(false);
                }
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false; // Indicate cancellation
            this.Close();
        }

        private void SetLoadingState(bool isLoading)
        {
            LoginButton.IsEnabled = !isLoading;
            CancelButton.IsEnabled = !isLoading;
            UsernameTextBox.IsEnabled = !isLoading;
            PasswordBox.IsEnabled = !isLoading;
            StatusTextBlock.Text = isLoading ? "Connexion en cours..." : ""; // Clear status on re-enable
        }
    }
}