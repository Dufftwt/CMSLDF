using System;
using System.Collections.Generic; // Keep if ErrorResponse uses List
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using CMSLDF; // Assuming VehiculeBasic is here
using Microsoft.Win32;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

// *** Reuse existing DTOs or define new ones if needed ***

// DTO for the POST request body (Including placeholder Image)
public class CreateVehicleRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("nom")]
    public string Nom { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("taille")]
    public string Taille { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("details")]
    public string Details { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("startingprice")]
    public string StartingPrice { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("image")]
    public string Image { get; set; } // Include the image property for the POST

    // Add other properties if required by your API for creation
    // Example:
    // [System.Text.Json.Serialization.JsonPropertyName("order")]
    // public int Order { get; set; } = 0; // Default order if needed
}

// Use the existing ErrorResponse DTO from ModifyVehicleLocationWindow.xaml.cs
// public class ErrorResponse { ... }

// *** End of DTO Definitions ***

namespace CMSLDF
{
    public partial class CreateVehicleLocationWindow : Window
    {
        // No _vehicleToModify needed here
        private const string PlaceholderImageUrl = "https://placehold.co/600x400/EEE/31343C?text=Image+Indisponible"; // Example placeholder

        public CreateVehicleLocationWindow()
        {
            InitializeComponent();
            // No data to load initially
            ImageTextBox.Text = ""; // Start with empty preview path
            UpdateImagePreview(null); // Clear preview
        }

        private async void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. Check Login Status First
            if (!AuthManager.IsLoggedIn)
            {
                MessageBox.Show("Vous n'êtes pas connectés, merci de relancer l'application pour vous connecter.", "Erreur d'authentification", MessageBoxButton.OK, MessageBoxImage.Warning);
                return; // Cannot create without being logged in
            }

            // 2. Create the data object to send
            var newVehicleData = new CreateVehicleRequest // Use the specific Create DTO
            {
                Nom = NomTextBox.Text,
                Taille = TailleTextBox.Text,
                Details = DetailsTextBox.Text,
                StartingPrice = StartingPriceTextBox.Text,
                Image = PlaceholderImageUrl // Use the placeholder URL for creation
                // Set other required properties if any
            };

            // Basic validation
            if (string.IsNullOrWhiteSpace(newVehicleData.Nom) ||
                string.IsNullOrWhiteSpace(newVehicleData.Taille) ||
                string.IsNullOrWhiteSpace(newVehicleData.Details) ||
                string.IsNullOrWhiteSpace(newVehicleData.StartingPrice))
            {
                MessageBox.Show("Le nom, la taille, les détails et le prix de départ ne peuvent pas être vides.", "Erreur de validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SetLoadingState(true); // Disable controls

            try
            {
                // 3. Send Create Request using AuthManager
                bool success = await CreateVehicleOnServerAsync(newVehicleData);

                if (success)
                {
                    MessageBox.Show($"Le véhicule '{newVehicleData.Nom}' a été créé avec succès.",
                                    "Création Réussie", MessageBoxButton.OK, MessageBoxImage.Information);
                    this.DialogResult = true; // Signal success (true) to CamionsView
                    this.Close();
                }
                // else: Error message already shown in CreateVehicleOnServerAsync, keep window open
            }
            catch (Exception ex) // Catch-all for unexpected errors during the process
            {
                Debug.WriteLine($"Unexpected error during create process: {ex.Message}");
                MessageBox.Show($"Une erreur non-prévue s'est produite lors de la création: {ex.Message}", "Erreur de création", MessageBoxButton.OK, MessageBoxImage.Error);
                this.DialogResult = false;
            }
            finally
            {
                SetLoadingState(false); // Re-enable controls
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false; // Signal cancellation (false)
            this.Close();
        }


        // --- Helper Method to Create Vehicle Data on Server ---
        private async Task<bool> CreateVehicleOnServerAsync(CreateVehicleRequest vehicleData)
        {
            // Use the base URL for creating new items (POST)
            string createUrl = "https://www.loueursdefrance.com/api/admin/db/vehiculelocations";
            Debug.WriteLine($"Attempting POST request to: {createUrl}");

            bool success = false;

            try
            {
                string jsonPayload = JsonSerializer.Serialize(vehicleData);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                // Get the pre-configured HttpClient from AuthManager
                HttpClient authClient = AuthManager.GetHttpClient();

                // Send the POST request
                HttpResponseMessage response = await authClient.PostAsync(createUrl, content);

                // Check common success codes for POST (201 Created or 200 OK)
                if (response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"Vehicle '{vehicleData.Nom}' created successfully (Status: {response.StatusCode}).");
                    success = true;
                }
                else if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                {
                    Debug.WriteLine($"Authentication error during create: {response.StatusCode}");
                    MessageBox.Show("L'authentification a échoué. Merci de relancer l'application et de vous reconnecter.", "Erreur d'authentification", MessageBoxButton.OK, MessageBoxImage.Warning);
                    AuthManager.Logout();
                    this.Close(); // Close create window on auth error
                    Application.Current.Shutdown(); // Consider shutting down app
                    success = false;
                }
                else // Handle other errors (400, 500, etc.)
                {
                    string errorBody = await response.Content.ReadAsStringAsync();
                    string errorMessage = $"Le véhicule n'a pas pu être créé. Status: {response.StatusCode}";
                    Debug.WriteLine($"API error during create: {response.StatusCode} - Body: {errorBody}");

                    try // Try parsing specific error format
                    {
                        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(errorBody);
                        if (errorResponse?.Errors != null && errorResponse.Errors.Count > 0)
                        {
                            errorMessage += $"\nErreur serveur: {string.Join("; ", errorResponse.Errors)}";
                        }
                        else if (!string.IsNullOrWhiteSpace(errorBody))
                        {
                            errorMessage += $"\nRéponse du serveur: {errorBody}";
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        Debug.WriteLine($"Failed to parse error response JSON: {jsonEx.Message}. Raw body: {errorBody}");
                        if (!string.IsNullOrWhiteSpace(errorBody))
                        {
                            errorMessage += $"\nRéponse du serveur invalide: {errorBody}";
                        }
                    }

                    MessageBox.Show(errorMessage, "Erreur de création", MessageBoxButton.OK, MessageBoxImage.Error);
                    success = false;
                }
            }
            catch (HttpRequestException httpEx)
            {
                Debug.WriteLine($"Network error during create: {httpEx.Message}");
                MessageBox.Show($"Erreur réseau lors de la création. Vérifiez la connexion.\nError: {httpEx.Message}", "Erreur réseau Création", MessageBoxButton.OK, MessageBoxImage.Error);
                success = false;
            }
            catch (JsonException jsonEx) // Error during serialization or deserialization
            {
                Debug.WriteLine($"JSON processing error during create: {jsonEx.Message}");
                MessageBox.Show($"Erreur lors du traitement des données de création: {jsonEx.Message}", "Erreur Traitement Création", MessageBoxButton.OK, MessageBoxImage.Error);
                success = false;
            }
            catch (Exception ex) // Catch other unexpected errors
            {
                Debug.WriteLine($"Unexpected error during create: {ex.Message}");
                MessageBox.Show($"Une erreur non-prévue s'est produite lors de la création: {ex.Message}", "Erreur Création", MessageBoxButton.OK, MessageBoxImage.Error);
                success = false;
            }

            return success;
        }

        // --- UI Helper Methods ---

        private void SetLoadingState(bool isLoading)
        {
            CreateButton.IsEnabled = !isLoading;
            CancelButton.IsEnabled = !isLoading;
            NomTextBox.IsEnabled = !isLoading;
            TailleTextBox.IsEnabled = !isLoading;
            DetailsTextBox.IsEnabled = !isLoading;
            StartingPriceTextBox.IsEnabled = !isLoading;
            ImageTextBox.IsEnabled = !isLoading; // Keep enabled/disabled consistently
            BrowseImageButton.IsEnabled = !isLoading;
            // Optionally show a progress indicator
        }

        // --- Image Preview and Browse Methods (For Local Preview Only) ---
        // These methods are the same as in ModifyVehicleLocationWindow, they only affect the UI preview
        // and DO NOT affect the data sent to the server (which uses the placeholder).
        private void BrowseImageButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Image Files (*.jpg; *.jpeg; *.png; *.gif; *.bmp)|*.jpg;*.jpeg;*.png;*.gif;*.bmp|All files (*.*)|*.*",
                Title = "Select an Image File for Preview"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                ImageTextBox.Text = openFileDialog.FileName;
                // No need to update a local _vehicle object here
            }
        }

        private void ImageTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateImagePreview(ImageTextBox.Text);
            // No need to update a local _vehicle object here
        }

        private void UpdateImagePreview(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                ImagePreview.Source = null;
                return;
            }
            try
            {
                Uri uri = new Uri(imagePath, UriKind.RelativeOrAbsolute);

                if (!uri.IsAbsoluteUri || uri.IsFile)
                {
                    string absolutePath = System.IO.Path.GetFullPath(imagePath);
                    if (System.IO.File.Exists(absolutePath))
                    {
                        BitmapImage bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(absolutePath, UriKind.Absolute);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                        bitmap.EndInit();
                        ImagePreview.Source = bitmap;
                    }
                    else
                    {
                        ImagePreview.Source = null;
                        Debug.WriteLine($"Image file not found for preview: '{absolutePath}'");
                    }
                }
                else if (uri.IsAbsoluteUri && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    BitmapImage bitmap = new BitmapImage(uri);
                    ImagePreview.Source = bitmap;
                    Debug.WriteLine($"Attempting to load web image preview for: '{imagePath}'");
                }
                else
                {
                    ImagePreview.Source = null;
                    Debug.WriteLine($"Unsupported URI scheme for image preview: '{imagePath}'");
                }
            }
            catch (Exception ex)
            {
                ImagePreview.Source = null;
                Debug.WriteLine($"Error loading image preview for '{imagePath}': {ex.Message}");
            }
        }
    }
}