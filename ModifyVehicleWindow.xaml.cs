using System; // Needed for Uri
using System.Collections.Generic;
using System.Diagnostics; // For Debug.WriteLine
using System.Windows;
using System.Windows.Controls; // Required for TextChanged event args
using System.Windows.Media.Imaging; // Required for BitmapImage
using CMSLDF;
using Microsoft.Win32; // Required for OpenFileDialog
using System.Net.Http;
using System.Net;
using System.Net.Http.Headers; // Required for Authorization header
using System.Text; // Required for Encoding
using System.Text.Json; // Required for JSON serialization/deserialization
using System.Threading.Tasks; // Required for async/await
using System.Diagnostics; // Keep for Debug.WriteLine if needed
// Make sure the namespace for VehiculeBasic is included if it's different
// using YourApplicationNamespace.Models;

namespace CMSLDF
{

    // For Login Request
    public class LoginRequest
    {
        // Use JsonPropertyName to match the exact case expected by the API
        [System.Text.Json.Serialization.JsonPropertyName("username")]
        public string Username { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("password")]
        public string Password { get; set; }
    }

    // For Login Response
    public class LoginResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("message")]
        public string Message { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("token")]
        public string Token { get; set; }
    }

    // For PUT Request (only fields to send)
    public class UpdateVehicleRequest
    {
        [System.Text.Json.Serialization.JsonPropertyName("nom")]
        public string Nom { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("taille")]
        public string Taille { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("details")]
        public string Details { get; set; }

        // IMPORTANT: No 'image' property here as requested
    }


    // For Error Response
    public class ErrorResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("ok")]
        public string OkStatus { get; set; } // Changed name slightly to avoid conflict with keywords

        [System.Text.Json.Serialization.JsonPropertyName("errors")]
        public List<string> Errors { get; set; }
    }

    public partial class ModifyVehicleWindow : Window
    {
        private VehiculeBasic _vehicleToModify;
        // Use a single HttpClient instance for the lifetime of the window for efficiency
        private static readonly HttpClient client = new HttpClient();

        public ModifyVehicleWindow(VehiculeBasic vehicle)
        {
            InitializeComponent();
            _vehicleToModify = vehicle;
            LoadVehicleData();
        }

        private void LoadVehicleData()
        {
            if (_vehicleToModify != null)
            {
                NomTextBox.Text = _vehicleToModify.Nom;
                TailleTextBox.Text = _vehicleToModify.Taille;
                DetailsTextBox.Text = _vehicleToModify.Details;
                ImageTextBox.Text = _vehicleToModify.Image;
            }
            UpdateImagePreview(ImageTextBox.Text);
        }

        // --- Make the Save Button Click Handler Async ---
        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. Update the local object first
            if (_vehicleToModify == null) return; // Should not happen, but good check

            _vehicleToModify.Nom = NomTextBox.Text;
            _vehicleToModify.Taille = TailleTextBox.Text;
            _vehicleToModify.Details = DetailsTextBox.Text;
            // _vehicleToModify.Image is updated locally by ImageTextBox, but we won't send it
            // _vehicleToModify.Order remains unchanged as per original requirement


            // Disable Save button to prevent double-clicks while processing
            SaveButton.IsEnabled = false;
            CancelButton.IsEnabled = false; // Maybe disable cancel too

            try
            {
                // 2. Get Authentication Token
                string token = await GetAuthTokenAsync("demo", "helloWorld");

                if (string.IsNullOrEmpty(token))
                {
                    // Error message shown in GetAuthTokenAsync
                    return; // Stop processing if login failed
                }

                // 3. Send Update Request
                bool success = await UpdateVehicleOnServerAsync(_vehicleToModify, token);

                if (success)
                {
                    MessageBox.Show($"Vehicle '{_vehicleToModify.Nom}' updated successfully on the server.",
                                    "Update Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                    this.DialogResult = true; // Signal success to the calling window
                    this.Close();
                }
                else
                {
                    // Error message should have been shown in UpdateVehicleOnServerAsync
                    // Keep the window open for the user to correct or cancel
                }
            }
            catch (Exception ex)
            {
                // Catch-all for unexpected errors (network issues, etc.)
                MessageBox.Show($"An unexpected error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Re-enable buttons regardless of outcome
                SaveButton.IsEnabled = true;
                CancelButton.IsEnabled = true;
            }
        }

        // --- Helper Method to Get Auth Token ---
        private async Task<string> GetAuthTokenAsync(string username, string password)
        {
            string loginUrl = "https://www.loueursdefrance.com/api/admin/login";
            string token = null;

            try
            {
                var loginData = new LoginRequest { Username = username, Password = password };
                string jsonPayload = JsonSerializer.Serialize(loginData);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.PostAsync(loginUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    var loginResponse = JsonSerializer.Deserialize<LoginResponse>(responseBody);
                    token = loginResponse?.Token;

                    if (string.IsNullOrEmpty(token))
                    {
                        MessageBox.Show("Login successful, but no token received from server.", "Login Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    Debug.WriteLine("Login successful, token obtained.");
                }
                else
                {
                    string errorBody = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"Login failed. Status: {response.StatusCode}\nDetails: {errorBody}",
                                    "Login Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (HttpRequestException httpEx)
            {
                MessageBox.Show($"Network error during login: {httpEx.Message}", "Login Network Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (JsonException jsonEx)
            {
                MessageBox.Show($"Error parsing login response: {jsonEx.Message}", "Login Parsing Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex) // Catch other unexpected errors
            {
                MessageBox.Show($"An unexpected error occurred during login: {ex.Message}", "Login Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return token; // Returns null if login failed or no token found
        }


        // --- Helper Method to Update Vehicle Data ---
        private async Task<bool> UpdateVehicleOnServerAsync(VehiculeBasic vehicle, string token)
        {
            string updateUrl = "https://www.loueursdefrance.com/api/admin/db/vehiculelocations";
            bool success = false;

            try
            {
                // Create the specific object to send (excluding Image)
                var updatePayload = new UpdateVehicleRequest
                {
                    Nom = vehicle.Nom,
                    Taille = vehicle.Taille,
                    Details = vehicle.Details
                };

                string jsonPayload = JsonSerializer.Serialize(updatePayload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                // Create request message to add Authorization header easily
                var request = new HttpRequestMessage(HttpMethod.Put, updateUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Content = content;

                HttpResponseMessage response = await client.SendAsync(request);

                if (response.StatusCode == HttpStatusCode.OK) // Check specifically for 200 OK
                {
                    Debug.WriteLine($"Vehicle '{vehicle.Nom}' updated successfully (Status Code: 200).");
                    success = true;
                }
                else
                {
                    // Attempt to read the error response body
                    string errorBody = await response.Content.ReadAsStringAsync();
                    string errorMessage = $"Failed to update vehicle. Status: {response.StatusCode}";

                    try
                    {
                        // Try parsing the specific error format
                        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(errorBody);
                        if (errorResponse?.Errors != null && errorResponse.Errors.Count > 0)
                        {
                            errorMessage += $"\nServer errors: {string.Join(", ", errorResponse.Errors)}";
                        }
                        else if (!string.IsNullOrWhiteSpace(errorBody))
                        {
                            errorMessage += $"\nServer response: {errorBody}";
                        }
                    }
                    catch (JsonException)
                    {
                        // If parsing the error structure fails, just show the raw body
                        if (!string.IsNullOrWhiteSpace(errorBody))
                        {
                            errorMessage += $"\nRaw server response: {errorBody}";
                        }
                    }

                    MessageBox.Show(errorMessage, "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    success = false;
                }
            }
            catch (HttpRequestException httpEx)
            {
                MessageBox.Show($"Network error during update: {httpEx.Message}", "Update Network Error", MessageBoxButton.OK, MessageBoxImage.Error);
                success = false;
            }
            catch (JsonException jsonEx)
            {
                MessageBox.Show($"Error processing update data or response: {jsonEx.Message}", "Update Processing Error", MessageBoxButton.OK, MessageBoxImage.Error);
                success = false;
            }
            catch (Exception ex) // Catch other unexpected errors
            {
                MessageBox.Show($"An unexpected error occurred during update: {ex.Message}", "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
                success = false;
            }

            return success;
        }


        // --- Existing Image Preview and Browse Methods ---
        private void BrowseImageButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Image Files (*.jpg; *.jpeg; *.png; *.gif; *.bmp)|*.jpg;*.jpeg;*.png;*.gif;*.bmp|All files (*.*)|*.*";
            openFileDialog.Title = "Select an Image File";
            bool? result = openFileDialog.ShowDialog();
            if (result == true)
            {
                ImageTextBox.Text = openFileDialog.FileName;
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
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                ImagePreview.Source = bitmap;
            }
            catch (Exception ex)
            {
                ImagePreview.Source = null;
                Debug.WriteLine($"Error loading image preview for '{imagePath}': {ex.Message}");
            }
        }
    }
}