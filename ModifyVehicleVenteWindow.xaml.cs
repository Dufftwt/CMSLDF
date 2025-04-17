using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Text.Json.Serialization;
using Microsoft.Win32;
using System.Diagnostics;
using System.Net.Http;
using System.Net;
using System.Text.Json;

namespace CMSLDF
{
   

    // DTO for the PUT request body for DepotVente (excluding Images)
    public class UpdateVehicleVenteRequest
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

        // Note: 'images' is intentionally excluded based on the reference
        // Note: 'hashId' is also excluded as it's likely server-generated/managed
    }
    public partial class ModifyVehicleVenteWindow : Window
    {
        private DepotVenteBasic _depotVenteToModify; // Changed type

        // Constructor accepting DepotVenteBasic
        public ModifyVehicleVenteWindow(DepotVenteBasic depotVente)
        {
            InitializeComponent();
            _depotVenteToModify = depotVente ?? throw new ArgumentNullException(nameof(depotVente), "Les données du dépôt vente ne peuvent pas être null (vides).");
            if (string.IsNullOrEmpty(_depotVenteToModify.Id))
            {
                throw new ArgumentException("Le dépôt vente doit avoir un identifiant (_id) valide.", nameof(depotVente));
            }
            LoadVehicleData();
        }

        private void LoadVehicleData()
        {
            // Populate fields from the DepotVenteBasic object
            NomTextBox.Text = _depotVenteToModify.Nom;
            KmTextBox.Text = _depotVenteToModify.Km;
            AnneeTextBox.Text = _depotVenteToModify.Annee;
            PrixTextBox.Text = _depotVenteToModify.Prix;
            ShortTextBox.Text = _depotVenteToModify.Short;
            DetailsTextBox.Text = _depotVenteToModify.Details;

            // Load the first image path/URL for preview, if available
            string firstImagePath = _depotVenteToModify.Images?.FirstOrDefault();
            ImageTextBox.Text = firstImagePath ?? string.Empty; // Handle null/empty list

            UpdateImagePreview(ImageTextBox.Text);
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!AuthManager.IsLoggedIn)
            {
                MessageBox.Show("Vous n'êtes pas connecté, merci de relancer l'application pour vous connecter.", "Erreur d'authentification", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_depotVenteToModify == null) return;

            // Update the local object with UI values
            _depotVenteToModify.Nom = NomTextBox.Text;
            _depotVenteToModify.Km = KmTextBox.Text;
            _depotVenteToModify.Annee = AnneeTextBox.Text;
            _depotVenteToModify.Prix = PrixTextBox.Text;
            _depotVenteToModify.Short = ShortTextBox.Text;
            _depotVenteToModify.Details = DetailsTextBox.Text;

            // Update the first image path in the local object if the TextBox changed
            // Again, this change is NOT sent to the server by this specific function
            if (_depotVenteToModify.Images == null) _depotVenteToModify.Images = new List<string>();
            if (_depotVenteToModify.Images.Any())
            {
                _depotVenteToModify.Images[0] = ImageTextBox.Text;
            }
            else if (!string.IsNullOrWhiteSpace(ImageTextBox.Text))
            {
                // Add if list was empty but textbox has content
                _depotVenteToModify.Images.Add(ImageTextBox.Text);
            }


            // Basic validation
            if (string.IsNullOrWhiteSpace(_depotVenteToModify.Nom) ||
                string.IsNullOrWhiteSpace(_depotVenteToModify.Km) ||
                string.IsNullOrWhiteSpace(_depotVenteToModify.Annee) ||
                string.IsNullOrWhiteSpace(_depotVenteToModify.Prix) ||
                string.IsNullOrWhiteSpace(_depotVenteToModify.Short) ||
                string.IsNullOrWhiteSpace(_depotVenteToModify.Details))
            {
                MessageBox.Show("Tous les champs (Nom, Km, Année, Prix, Description courte, Détails) sont obligatoires.", "Erreur de validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SetLoadingState(true);

            try
            {
                // Send Update Request using the specific method for Ventes
                bool success = await UpdateVehicleVenteOnServerAsync(_depotVenteToModify);

                if (success)
                {
                    MessageBox.Show($"Le dépôt vente '{_depotVenteToModify.Nom}' a été mis à jour avec succès.",
                                    "Modification effectuée", MessageBoxButton.OK, MessageBoxImage.Information);
                    this.DialogResult = true; // Signal success
                    this.Close();
                }
                // else: Error shown in UpdateVehicleVenteOnServerAsync
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unexpected error during save process (Depot Vente): {ex.Message}");
                MessageBox.Show($"Une erreur non-prévue s'est produite: {ex.Message}", "Erreur Sauvegarde", MessageBoxButton.OK, MessageBoxImage.Error);
                this.DialogResult = false;
            }
            finally
            {
                SetLoadingState(false);
            }
        }

        // Renamed and adapted method for DepotVente
        private async Task<bool> UpdateVehicleVenteOnServerAsync(DepotVenteBasic depotVente)
        {
            // Correct URL for updating vehiculeventes, using _id
            string updateUrl = $"https://www.loueursdefrance.com/api/admin/db/vehiculeventes/{depotVente.Id}";
            Debug.WriteLine($"Attempting PUT request to: {updateUrl}");

            bool success = false;

            try
            {
                // Create the specific DTO for Ventes (excluding Images, hashId)
                var updatePayload = new UpdateVehicleVenteRequest
                {
                    Nom = depotVente.Nom,
                    Km = depotVente.Km,
                    Annee = depotVente.Annee,
                    Prix = depotVente.Prix,
                    Details = depotVente.Details,
                    Short = depotVente.Short
                };

                string jsonPayload = JsonSerializer.Serialize(updatePayload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                HttpClient authClient = AuthManager.GetHttpClient();
                HttpResponseMessage response = await authClient.PutAsync(updateUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"DepotVente '{depotVente.Nom}' (ID: {depotVente.Id}) updated successfully (Status: {response.StatusCode}).");
                    success = true;
                }
                else if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                {
                    Debug.WriteLine($"Authentication error during update (DepotVente): {response.StatusCode}");
                    MessageBox.Show("L'authentification a échoué. Merci de relancer l'application et de vous reconnecter.", "Erreur d'authentification", MessageBoxButton.OK, MessageBoxImage.Warning);
                    AuthManager.Logout();
                    this.Close(); // Close this window on auth error
                    if (Application.Current != null) Application.Current.Shutdown();
                    success = false;
                }
                else
                {
                    string errorBody = await response.Content.ReadAsStringAsync();
                    string errorMessage = $"Le dépôt vente n'a pas pu être mis à jour. Status: {response.StatusCode}";
                    Debug.WriteLine($"API error during update (DepotVente): {response.StatusCode} - Body: {errorBody}");

                    try
                    {
                        // Attempt to parse structured error (assuming same ErrorResponse DTO)
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
                        Debug.WriteLine($"Failed to parse error response JSON (DepotVente): {jsonEx.Message}. Raw body: {errorBody}");
                        if (!string.IsNullOrWhiteSpace(errorBody)) { errorMessage += $"\nRéponse serveur: {errorBody}"; }
                    }

                    MessageBox.Show(errorMessage, "Erreur de modification", MessageBoxButton.OK, MessageBoxImage.Error);
                    success = false;
                }
            }
            catch (HttpRequestException httpEx)
            {
                Debug.WriteLine($"Network error during update (DepotVente): {httpEx.Message}");
                MessageBox.Show($"Erreur réseau pendant la mise à jour. Vérifiez la connexion.\nErreur: {httpEx.Message}", "Erreur Réseau", MessageBoxButton.OK, MessageBoxImage.Error);
                success = false;
            }
            catch (JsonException jsonEx)
            {
                Debug.WriteLine($"JSON processing error during update (DepotVente): {jsonEx.Message}");
                MessageBox.Show($"Erreur pendant le traitement des données: {jsonEx.Message}", "Erreur Traitement", MessageBoxButton.OK, MessageBoxImage.Error);
                success = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unexpected error during update (DepotVente): {ex.Message}");
                MessageBox.Show($"Une erreur non-prévue s'est produite pendant la mise à jour: {ex.Message}", "Erreur Inattendue", MessageBoxButton.OK, MessageBoxImage.Error);
                success = false;
            }

            return success;
        }

        // --- UI Helper Methods ---

        private void SetLoadingState(bool isLoading)
        {
            SaveButton.IsEnabled = !isLoading;
            CancelButton.IsEnabled = !isLoading; // Assuming Cancel should always be available unless saving
            NomTextBox.IsEnabled = !isLoading;
            KmTextBox.IsEnabled = !isLoading;
            AnneeTextBox.IsEnabled = !isLoading;
            PrixTextBox.IsEnabled = !isLoading;
            ShortTextBox.IsEnabled = !isLoading;
            DetailsTextBox.IsEnabled = !isLoading;
            ImageTextBox.IsEnabled = !isLoading;
            BrowseImageButton.IsEnabled = !isLoading;
        }

        // --- Image Preview and Browse Methods (Adapted slightly) ---
        private void BrowseImageButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Image Files (*.jpg; *.jpeg; *.png; *.gif; *.bmp)|*.jpg;*.jpeg;*.png;*.gif;*.bmp|All files (*.*)|*.*",
                Title = "Choisir un fichier image"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                ImageTextBox.Text = openFileDialog.FileName;
                // No direct update to the server object here, only local state / preview
                // UpdateImagePreview(openFileDialog.FileName); // TextChanged handles this
            }
        }

        private void ImageTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateImagePreview(ImageTextBox.Text);
            // Note: This change isn't saved to the server via the Save button's logic.
            // If you wanted to update the first image *locally* when text changes:
            /*
            if (_depotVenteToModify != null) {
                if (_depotVenteToModify.Images == null) _depotVenteToModify.Images = new List<string>();
                if (_depotVenteToModify.Images.Any()) {
                   _depotVenteToModify.Images[0] = ImageTextBox.Text;
                } else if (!string.IsNullOrWhiteSpace(ImageTextBox.Text)) {
                   _depotVenteToModify.Images.Add(ImageTextBox.Text);
                }
            }
            */
        }

        // Reused Image Preview Logic (should work for file paths and basic URLs)
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
                bitmap.UriSource = uri; // Works for absolute file paths and web URLs directly
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bitmap.EndInit();
                ImagePreview.Source = bitmap;

                // Handle potential errors during loading (e.g., invalid URL, file not found)
                bitmap.DownloadFailed += (s, e) => { ImagePreview.Source = null; Debug.WriteLine($"Image download/load failed: {e.ErrorException?.Message}"); };
                bitmap.DecodeFailed += (s, e) => { ImagePreview.Source = null; Debug.WriteLine($"Image decode failed: {e.ErrorException?.Message}"); };
            }
            catch (Exception ex)
            {
                ImagePreview.Source = null;
                Debug.WriteLine($"Error creating/loading image preview URI for '{imagePath}': {ex.Message}");
            }
        }
    }
}
