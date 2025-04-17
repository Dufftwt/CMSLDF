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
// using System.Net.Http.Headers; // No longer needed if using AuthManager's client directly
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

// *** Consider moving these DTOs to a shared Models/Api folder ***
// namespace YourApplicationNamespace.Models { ... }

// DTO for the PUT request body (excluding Image)
public class UpdateVehicleRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("nom")]
    public string Nom { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("taille")]
    public string Taille { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("details")]
    public string Details { get; set; }

    // No 'image' property here as requested
}

// DTO for potential Error Response from the server
public class ErrorResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("ok")]
    public string OkStatus { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("errors")]
    public List<string> Errors { get; set; }
}

// *** End of DTO Definitions ***

namespace CMSLDF
{
    public partial class ModifyVehicleLocationWindow : Window
    {
        private VehiculeBasic _vehicleToModify;
        // No separate HttpClient needed here, use AuthManager's

        public ModifyVehicleLocationWindow(VehiculeBasic vehicle)
        {
            InitializeComponent();
            _vehicleToModify = vehicle ?? throw new ArgumentNullException(nameof(vehicle), "Les données véhicules ne peuvent pas être null (vides)."); // Add null check
            if (string.IsNullOrEmpty(_vehicleToModify.Id))
            {
                throw new ArgumentException("Le véhicule doit avoir un identifiant valide.", nameof(vehicle));
            }
            LoadVehicleData();
        }

        private void LoadVehicleData()
        {
            // Assumes _vehicleToModify is not null due to constructor check
            NomTextBox.Text = _vehicleToModify.Nom;
            TailleTextBox.Text = _vehicleToModify.Taille;
            DetailsTextBox.Text = _vehicleToModify.Details;
            ImageTextBox.Text = _vehicleToModify.Image; // Load image path for local preview

            UpdateImagePreview(ImageTextBox.Text);
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. Check Login Status First
            if (!AuthManager.IsLoggedIn)
            {
                MessageBox.Show("Vous n'êtes pas connectés, merci de relancer l'application pour vous connecter.", "Erreur d'authentification", MessageBoxButton.OK, MessageBoxImage.Warning);
                return; // Cannot save without being logged in
            }

            // 2. Update the local object with UI values
            if (_vehicleToModify == null) return; // Should not happen

            _vehicleToModify.Nom = NomTextBox.Text;
            _vehicleToModify.Taille = TailleTextBox.Text;
            _vehicleToModify.Details = DetailsTextBox.Text;
            // Update the local image path if changed, but it won't be sent to server
            _vehicleToModify.Image = ImageTextBox.Text;

            // Basic validation (optional, add more as needed)
            if (string.IsNullOrWhiteSpace(_vehicleToModify.Nom) ||
                string.IsNullOrWhiteSpace(_vehicleToModify.Taille) ||
                string.IsNullOrWhiteSpace(_vehicleToModify.Details)) 

            {
                MessageBox.Show("Le nom, la taille, ou les détails ne peuvent pas être vides.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }


            SetLoadingState(true); // Disable controls

            try
            {
                // 3. Send Update Request using AuthManager
                bool success = await UpdateVehicleOnServerAsync(_vehicleToModify);

                if (success)
                {
                    MessageBox.Show($"Le véhicule '{_vehicleToModify.Nom}' a été mis à jour avec succès.",
                                    "Modification effectuée", MessageBoxButton.OK, MessageBoxImage.Information);
                    this.DialogResult = true; // Signal success (true) to CamionsView
                    this.Close();
                }
                // else: Error message already shown in UpdateVehicleOnServerAsync, keep window open
            }
            catch (Exception ex) // Catch-all for unexpected errors during the process
            {
                Debug.WriteLine($"Unexpected error during save process: {ex.Message}");
                MessageBox.Show($"Une erreur non-prévue s'est produite: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                // Explicitly ensure DialogResult is not true on error
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


        // --- Helper Method to Update Vehicle Data on Server ---
        // No token parameter needed, gets client from AuthManager
        private async Task<bool> UpdateVehicleOnServerAsync(VehiculeBasic vehicle)
        {
            // Construct the CORRECT URL with the vehicle ID
            string updateUrl = $"https://www.loueursdefrance.com/api/admin/db/vehiculelocations/{vehicle.Id}";
            Debug.WriteLine($"Attempting PUT request to: {updateUrl}");

            bool success = false;

            try
            {
                // Create the specific DTO to send (excluding Image)
                var updatePayload = new UpdateVehicleRequest
                {
                    Nom = vehicle.Nom,
                    Taille = vehicle.Taille,
                    Details = vehicle.Details
                };

                string jsonPayload = JsonSerializer.Serialize(updatePayload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                // Get the pre-configured HttpClient from AuthManager
                HttpClient authClient = AuthManager.GetHttpClient();

                // Send the PUT request - AuthManager's client already has the token header
                HttpResponseMessage response = await authClient.PutAsync(updateUrl, content);

                if (response.IsSuccessStatusCode) // Check for any 2xx status code
                {
                    Debug.WriteLine($"Vehicle '{vehicle.Nom}' (ID: {vehicle.Id}) updated successfully (Status: {response.StatusCode}).");
                    success = true;
                }
                else if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                {
                    Debug.WriteLine($"Authentication error during update: {response.StatusCode}");
                    MessageBox.Show("L'authentification a échoué. Merci de relancer l'application et de vous reconnecter.", "Erreur d'authentification", MessageBoxButton.OK, MessageBoxImage.Warning);
                    // Recommend logging out and potentially closing app or redirecting
                    AuthManager.Logout();
                    // Optionally close this window or the whole app:
                    this.Close();
                    Application.Current.Shutdown();
                    success = false;
                }
                else
                {
                    // Attempt to read the error response body for more details
                    string errorBody = await response.Content.ReadAsStringAsync();
                    string errorMessage = $"Le véhicule n'a pas pu être mis à jour à cause d'une erreur serveur. Status: {response.StatusCode}";
                    Debug.WriteLine($"API error during update: {response.StatusCode} - Body: {errorBody}");

                    try
                    {
                        // Try parsing the specific error format if the server provides one
                        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(errorBody);
                        if (errorResponse?.Errors != null && errorResponse.Errors.Count > 0)
                        {
                            errorMessage += $"\nErreur serveur: {string.Join("; ", errorResponse.Errors)}";
                        }
                        else if (!string.IsNullOrWhiteSpace(errorBody))
                        {
                            // Fallback to showing raw body if parsing fails or no specific errors field
                            errorMessage += $"\nRéponse du serveur: {errorBody}";
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        Debug.WriteLine($"Failed to parse error response JSON: {jsonEx.Message}. Raw body: {errorBody}");
                        // If parsing the error structure fails, show the raw body if available
                        if (!string.IsNullOrWhiteSpace(errorBody))
                        {
                            errorMessage += $"\nRéponse du serveur invalide: {errorBody}";
                        }
                    }

                    MessageBox.Show(errorMessage, "Erreur de modification", MessageBoxButton.OK, MessageBoxImage.Error);
                    success = false;
                }
            }
            catch (HttpRequestException httpEx)
            {
                Debug.WriteLine($"Network error during update: {httpEx.Message}");
                MessageBox.Show($"Network error during update. Check connection.\nError: {httpEx.Message}", "Update Network Error", MessageBoxButton.OK, MessageBoxImage.Error);
                success = false;
            }
            catch (JsonException jsonEx) // Error during serialization or deserialization
            {
                Debug.WriteLine($"JSON processing error during update: {jsonEx.Message}");
                MessageBox.Show($"Error processing update data or response: {jsonEx.Message}", "Update Processing Error", MessageBoxButton.OK, MessageBoxImage.Error);
                success = false;
            }
            catch (Exception ex) // Catch other unexpected errors
            {
                Debug.WriteLine($"Unexpected error during update: {ex.Message}");
                MessageBox.Show($"An unexpected error occurred during update: {ex.Message}", "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
                success = false;
            }

            return success;
        }

        // --- UI Helper Methods ---

        private void SetLoadingState(bool isLoading)
        {
            SaveButton.IsEnabled = !isLoading;
            CancelButton.IsEnabled = !isLoading;
            NomTextBox.IsEnabled = !isLoading;
            TailleTextBox.IsEnabled = !isLoading;
            DetailsTextBox.IsEnabled = !isLoading;
            ImageTextBox.IsEnabled = !isLoading;
            BrowseImageButton.IsEnabled = !isLoading;
            // Optionally show a progress indicator
            // LoadingIndicator.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        }

        // --- Existing Image Preview and Browse Methods (Unchanged) ---
        private void BrowseImageButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Image Files (*.jpg; *.jpeg; *.png; *.gif; *.bmp)|*.jpg;*.jpeg;*.png;*.gif;*.bmp|All files (*.*)|*.*",
                Title = "Select an Image File"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                ImageTextBox.Text = openFileDialog.FileName;
                // Update the local object's image property immediately (but it won't be saved to server via PUT)
                if (_vehicleToModify != null)
                {
                    _vehicleToModify.Image = openFileDialog.FileName;
                }
            }
        }

        private void ImageTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateImagePreview(ImageTextBox.Text);
            // Update the local object's image property immediately (but it won't be saved to server via PUT)
            if (_vehicleToModify != null)
            {
                _vehicleToModify.Image = ImageTextBox.Text;
            }
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
                // Check if it's a local file path or a URL
                Uri uri = new Uri(imagePath, UriKind.RelativeOrAbsolute);

                // Only attempt to load local files for preview unless you specifically handle web URLs
                if (!uri.IsAbsoluteUri || uri.IsFile)
                {
                    // Ensure the path is absolute if it's a file path
                    string absolutePath = System.IO.Path.GetFullPath(imagePath);
                    if (System.IO.File.Exists(absolutePath))
                    {
                        BitmapImage bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(absolutePath, UriKind.Absolute);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad; // Load immediately
                        bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache; // Avoid caching issues
                        bitmap.EndInit();
                        ImagePreview.Source = bitmap;
                    }
                    else
                    {
                        ImagePreview.Source = null; // File doesn't exist
                        Debug.WriteLine($"Image file not found for preview: '{absolutePath}'");
                    }
                }
                else if (uri.IsAbsoluteUri && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    // Handle web URLs if needed (might require async loading or placeholder)
                    BitmapImage bitmap = new BitmapImage(uri); // Simple way, might block UI briefly
                    ImagePreview.Source = bitmap;
                    Debug.WriteLine($"Attempting to load web image preview for: '{imagePath}'");
                }
                else
                {
                    ImagePreview.Source = null; // Unsupported URI scheme
                    Debug.WriteLine($"Unsupported URI scheme for image preview: '{imagePath}'");
                }
            }
            catch (Exception ex)
            {
                ImagePreview.Source = null; // Clear preview on error
                Debug.WriteLine($"Error loading image preview for '{imagePath}': {ex.Message}");
                // Optionally show a placeholder error image
            }
        }
    }
}