using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq; // Needed for FirstOrDefault
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32; // For OpenFileDialog

namespace CMSLDF
{
    // *** DTO Definitions ***

    // DTO for the POST request body for DepotVente (including Images array)
    public class CreateVehicleVenteRequest
    {
        [JsonPropertyName("nom")]
        public string Nom { get; set; }

        [JsonPropertyName("km")]
        public string Km { get; set; }

        [JsonPropertyName("annee")]
        public string Annee { get; set; }

        [JsonPropertyName("prix")]
        public string Prix { get; set; }

        [JsonPropertyName("details")]
        public string Details { get; set; }

        [JsonPropertyName("short")]
        public string Short { get; set; }

        [JsonPropertyName("images")]
        public List<string> Images { get; set; } // Include the images property for the POST

        // Add other properties if required by your API for creation (e.g., hashId if client generates?)
        // Assuming hashId is server-generated
    }

    // Reuse the existing ErrorResponse DTO if applicable
    // Found in ModifyVehicleLocationWindow.xaml.cs or ModifyVehicleVenteWindow.xaml.cs
    // public class ErrorResponse { ... }

    // *** End of DTO Definitions ***

    public partial class CreateVehicleVenteWindow : Window
    {
        private const string PlaceholderImageUrl = "https://placehold.co/600x400/EEE/31343C?text=Image+Indisponible"; // Same placeholder

        public CreateVehicleVenteWindow()
        {
            InitializeComponent();
            ImageTextBox.Text = ""; // Start empty
            UpdateImagePreview(null); // Clear preview
        }

        private async void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            if (!AuthManager.IsLoggedIn)
            {
                MessageBox.Show("Vous n'êtes pas connecté, merci de relancer l'application pour vous connecter.", "Erreur d'authentification", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Create the data object to send
            var newVehicleData = new CreateVehicleVenteRequest
            {
                Nom = NomTextBox.Text,
                Km = KmTextBox.Text,
                Annee = AnneeTextBox.Text,
                Prix = PrixTextBox.Text,
                Short = ShortTextBox.Text,
                Details = DetailsTextBox.Text,
                // Use the placeholder URL in a list for the Images property
                Images = new List<string> { PlaceholderImageUrl }
            };

            // Basic validation (matching Modify window)
            if (string.IsNullOrWhiteSpace(newVehicleData.Nom) ||
                string.IsNullOrWhiteSpace(newVehicleData.Km) ||
                string.IsNullOrWhiteSpace(newVehicleData.Annee) ||
                string.IsNullOrWhiteSpace(newVehicleData.Prix) ||
                string.IsNullOrWhiteSpace(newVehicleData.Short) ||
                string.IsNullOrWhiteSpace(newVehicleData.Details))
            {
                MessageBox.Show("Tous les champs (Nom, Km, Année, Prix, Description courte, Détails) sont obligatoires.", "Erreur de validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SetLoadingState(true);

            try
            {
                // Send Create Request using the specific method for Ventes
                bool success = await CreateVehicleVenteOnServerAsync(newVehicleData);

                if (success)
                {
                    MessageBox.Show($"Le dépôt vente '{newVehicleData.Nom}' a été créé avec succès.",
                                    "Création Réussie", MessageBoxButton.OK, MessageBoxImage.Information);
                    this.DialogResult = true; // Signal success
                    this.Close();
                }
                // else: Error message shown in CreateVehicleVenteOnServerAsync
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unexpected error during create process (Depot Vente): {ex.Message}");
                MessageBox.Show($"Une erreur non-prévue s'est produite lors de la création: {ex.Message}", "Erreur Création", MessageBoxButton.OK, MessageBoxImage.Error);
                this.DialogResult = false;
            }
            finally
            {
                SetLoadingState(false);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false; // Signal cancellation
            this.Close();
        }


        // --- Helper Method to Create DepotVente Data on Server ---
        private async Task<bool> CreateVehicleVenteOnServerAsync(CreateVehicleVenteRequest vehicleData)
        {
            // Correct URL for CREATING vehiculeventes (POST to collection endpoint)
            string createUrl = "https://www.loueursdefrance.com/api/admin/db/vehiculeventes";
            Debug.WriteLine($"Attempting POST request to: {createUrl}");

            bool success = false;

            try
            {
                string jsonPayload = JsonSerializer.Serialize(vehicleData);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                HttpClient authClient = AuthManager.GetHttpClient();
                HttpResponseMessage response = await authClient.PostAsync(createUrl, content);

                if (response.IsSuccessStatusCode) // Typically 201 Created or 200 OK
                {
                    Debug.WriteLine($"DepotVente '{vehicleData.Nom}' created successfully (Status: {response.StatusCode}).");
                    success = true;
                }
                else if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                {
                    Debug.WriteLine($"Authentication error during create (DepotVente): {response.StatusCode}");
                    MessageBox.Show("L'authentification a échoué. Merci de relancer l'application et de vous reconnecter.", "Erreur d'authentification", MessageBoxButton.OK, MessageBoxImage.Warning);
                    AuthManager.Logout();
                    this.Close(); // Close create window on auth error
                    if (Application.Current != null) Application.Current.Shutdown();
                    success = false;
                }
                else // Handle 400, 500, etc.
                {
                    string errorBody = await response.Content.ReadAsStringAsync();
                    string errorMessage = $"Le dépôt vente n'a pas pu être créé. Status: {response.StatusCode}";
                    Debug.WriteLine($"API error during create (DepotVente): {response.StatusCode} - Body: {errorBody}");

                    try // Try parsing specific error format
                    {
                        // Assuming ErrorResponse DTO is available and applicable
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
                        Debug.WriteLine($"Failed to parse error response JSON (DepotVente Create): {jsonEx.Message}. Raw body: {errorBody}");
                        if (!string.IsNullOrWhiteSpace(errorBody)) { errorMessage += $"\nRéponse serveur: {errorBody}"; }
                    }

                    MessageBox.Show(errorMessage, "Erreur de création", MessageBoxButton.OK, MessageBoxImage.Error);
                    success = false;
                }
            }
            catch (HttpRequestException httpEx)
            {
                Debug.WriteLine($"Network error during create (DepotVente): {httpEx.Message}");
                MessageBox.Show($"Erreur réseau lors de la création. Vérifiez la connexion.\nErreur: {httpEx.Message}", "Erreur Réseau Création", MessageBoxButton.OK, MessageBoxImage.Error);
                success = false;
            }
            catch (JsonException jsonEx)
            {
                Debug.WriteLine($"JSON processing error during create (DepotVente): {jsonEx.Message}");
                MessageBox.Show($"Erreur pendant le traitement des données de création: {jsonEx.Message}", "Erreur Traitement Création", MessageBoxButton.OK, MessageBoxImage.Error);
                success = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unexpected error during create (DepotVente): {ex.Message}");
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
            KmTextBox.IsEnabled = !isLoading;
            AnneeTextBox.IsEnabled = !isLoading;
            PrixTextBox.IsEnabled = !isLoading;
            ShortTextBox.IsEnabled = !isLoading;
            DetailsTextBox.IsEnabled = !isLoading;
            ImageTextBox.IsEnabled = !isLoading; // Keep consistent
            BrowseImageButton.IsEnabled = !isLoading;
        }

        // --- Image Preview and Browse Methods (For Local Preview Only) ---
        // These are identical to the ones in ModifyVehicleVenteWindow / CreateVehicleLocationWindow

        private void BrowseImageButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Image Files (*.jpg; *.jpeg; *.png; *.gif; *.bmp)|*.jpg;*.jpeg;*.png;*.gif;*.bmp|All files (*.*)|*.*",
                Title = "Choisir un fichier image (pour aperçu)"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                ImageTextBox.Text = openFileDialog.FileName;
                // TextChanged handler updates the preview
            }
        }

        private void ImageTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateImagePreview(ImageTextBox.Text);
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

                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = uri;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bitmap.EndInit();
                ImagePreview.Source = bitmap;

                bitmap.DownloadFailed += (s, ev) => { ImagePreview.Source = null; Debug.WriteLine($"Image download/load failed: {ev.ErrorException?.Message}"); };
                bitmap.DecodeFailed += (s, ev) => { ImagePreview.Source = null; Debug.WriteLine($"Image decode failed: {ev.ErrorException?.Message}"); };
            }
            catch (Exception ex)
            {
                ImagePreview.Source = null;
                Debug.WriteLine($"Error creating/loading image preview URI for '{imagePath}': {ex.Message}");
            }
        }
    }
}