using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text; // Required for StringContent if needed
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace CMSLDF
{
    // --- VehiculeBasic and ApiResponseWrapper classes (keep as is) ---
    public class VehiculeBasic
    {
        [System.Text.Json.Serialization.JsonPropertyName("_id")]
        public string Id { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("nom")]
        public string Nom { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("taille")]
        public string Taille { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("details")]
        public string Details { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("image")]
        public string Image { get; set; }
    }
    public class ApiResponseWrapper
    {
        [System.Text.Json.Serialization.JsonPropertyName("data")]
        public ObservableCollection<VehiculeBasic> Data { get; set; }
    }
    // --- End of Model Definitions ---

    public partial class CamionsView : UserControl
    {
        private const string ApiBaseUrl = "https://loueursdefrance.com/api/admin"; // Base URL for easier reuse
        private const string VehiculesApiUrl = $"{ApiBaseUrl}/db/vehiculelocations";
        private const string SeedApiUrl = $"{ApiBaseUrl}/seedlocations"; // Specific URL for seeding

        public ObservableCollection<VehiculeBasic> Vehicules { get; } = new ObservableCollection<VehiculeBasic>();

        public CamionsView()
        {
            InitializeComponent();
            VehiculesItemsControl.ItemsSource = Vehicules;
            this.Loaded += CamionsView_Loaded;
        }

        private async void CamionsView_Loaded(object sender, RoutedEventArgs e)
        {
            await FetchAndDisplayDataAsync();
        }

        private async Task FetchAndDisplayDataAsync()
        {
            if (!AuthManager.IsLoggedIn)
            {
                // Don't show message here if called internally after seeding/delete/modify
                // Maybe disable UI elements instead if initial load fails
                if (!this.IsLoaded) // Only show message on initial load failure
                {
                    MessageBox.Show("You are not logged in. Please restart and log in.", "Authentication Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    this.IsEnabled = false;
                }
                return;
            }

            Debug.WriteLine($"Attempting to fetch data from {VehiculesApiUrl}...");
            // Consider adding a visual loading indicator
            // LoadingIndicator.Visibility = Visibility.Visible;
            this.IsEnabled = false; // Disable UI during fetch
            Vehicules.Clear();

            try
            {
                HttpClient client = AuthManager.GetHttpClient();
                HttpResponseMessage response = await client.GetAsync(VehiculesApiUrl); // Use constant

                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine("Data fetched successfully.");
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var wrappedData = JsonSerializer.Deserialize<ApiResponseWrapper>(jsonResponse, options);
                    if (wrappedData?.Data != null)
                    {
                        foreach (var vehicule in wrappedData.Data)
                        {
                            Vehicules.Add(vehicule);
                        }
                    }
                    Debug.WriteLine($"Displaying {Vehicules.Count} vehicules.");
                }
                else if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                {
                    HandleAuthenticationError("fetch data");
                }
                else
                {
                    await HandleApiError(response, "load data");
                }
            }
            catch (HttpRequestException httpEx) { HandleNetworkError(httpEx, "loading data"); }
            catch (JsonException jsonEx) { HandleJsonError(jsonEx, "reading data"); }
            catch (Exception ex) { HandleUnexpectedError(ex, "loading data"); }
            finally
            {
                // LoadingIndicator.Visibility = Visibility.Collapsed;
                this.IsEnabled = true; // Re-enable UI
            }
        }


        // --- NEW CREATE BUTTON HANDLER ---
        private async void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Create Vehicle button clicked.");

            // 1. Check Login Status
            if (!EnsureLoggedIn("créer un véhicule")) return;

            // 2. Open the Create Window
            CreateVehicleLocationWindow createWindow = new CreateVehicleLocationWindow(); // Create instance of the new window
            createWindow.Owner = Window.GetWindow(this); // Set owner for modal behavior
            bool? result = createWindow.ShowDialog(); // Show as modal dialog

            // 3. Refresh list if creation was successful
            if (result == true)
            {
                Debug.WriteLine("Creation successful. Refreshing list.");
                await FetchAndDisplayDataAsync(); // Refresh the list to show the new item
            }
            else
            {
                Debug.WriteLine("Creation cancelled or failed.");
            }
        }


        // --- Button Click Handlers ---

        private async void ModifyButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: VehiculeBasic itemToModify })
            {
                Debug.WriteLine($"Modify clicked for: {itemToModify.Nom} (ID: {itemToModify.Id})");

                // Check login *before* opening window
                if (!EnsureLoggedIn("modifier le véhicule")) return;

                ModifyVehicleLocationWindow modifyWindow = new ModifyVehicleLocationWindow(itemToModify);
                modifyWindow.Owner = Window.GetWindow(this);
                bool? result = modifyWindow.ShowDialog();

                if (result == true)
                {
                    Debug.WriteLine($"Modification successful for {itemToModify.Nom}. Refreshing list.");
                    // Refresh the list to see changes saved on the server
                    await FetchAndDisplayDataAsync();
                }
                else
                {
                    Debug.WriteLine($"Modification cancelled or failed for {itemToModify.Nom}.");
                }
            }
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: VehiculeBasic itemToDelete })
            {
                Debug.WriteLine($"Delete clicked for: {itemToDelete.Nom} (ID: {itemToDelete.Id})");

                // Check login *before* showing confirmation
                if (!EnsureLoggedIn("supprimer le véhicule")) return;

                MessageBoxResult confirmResult = MessageBox.Show(
                    $"Êtes-vous sûr de vouloir supprimer '{itemToDelete.Nom}'?",
                    "Confirmer la suppression", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (confirmResult == MessageBoxResult.Yes)
                {
                    bool deletedOnServer = await DeleteVehiculeOnServerAsync(itemToDelete.Id);
                    if (deletedOnServer)
                    {
                        // Remove from the ObservableCollection - UI updates automatically
                        Vehicules.Remove(itemToDelete);
                        Debug.WriteLine($"Deleted {itemToDelete.Nom}. List count: {Vehicules.Count}");
                        MessageBox.Show($"'{itemToDelete.Nom}' a été supprimé avec succès.", "Supression", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    // else: Error message shown in DeleteVehiculeOnServerAsync
                }
            }
        }

        // **** NEW SEED BUTTON HANDLER ****
        private async void SeedButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Seed Database button clicked.");

            // --- FIX START ---
            // Declare the button variable once, outside the try block
            Button seedButton = sender as Button;
            // --- FIX END ---


            // 1. Check Login Status
            if (!EnsureLoggedIn("seeder la base de données")) return;

            // 2. Confirmation (Recommended for potentially impactful actions)
            MessageBoxResult confirmResult = MessageBox.Show(
                "Êtes-vous sûrs de vouloir seeder la base de données? Cela supprimera tous les véhicules changés et les remplacera par les véhicules par défaut.",
                "Confirmer le seeding",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmResult != MessageBoxResult.Yes)
            {
                Debug.WriteLine("Seeding cancelled by user.");
                return;
            }

            // 3. Disable button and show loading state
            // --- FIX START ---
            // Use the already declared variable
            if (seedButton != null) seedButton.IsEnabled = false;
            // --- FIX END ---
            // Consider adding a visual indicator here too
            Debug.WriteLine($"Attempting GET request to {SeedApiUrl}...");


            try
            {
                HttpClient client = AuthManager.GetHttpClient();

                // Use POST for actions that change server state.
                HttpResponseMessage response = await client.GetAsync(SeedApiUrl); // Send null content

                if (response.IsSuccessStatusCode)
                {
                    Debug.WriteLine("Database seeded successfully.");
                    MessageBox.Show("Base de données seedée avec succès, veuillez attendre 5 secondes pour que la base de données se mette à jour!", "Seed terminé", MessageBoxButton.OK, MessageBoxImage.Information);

                    // --- Add the delay here ---
                    Debug.WriteLine("Waiting 6 seconds before refresh...");
                    await Task.Delay(6000); // Waits for 5000 milliseconds (5 seconds)
                    Debug.WriteLine("6 seconds elapsed, refreshing data...");
                    // --- End of delay ---

                    // Refresh the list to show any newly seeded data
                    await FetchAndDisplayDataAsync();
                }
                else if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                {
                    HandleAuthenticationError("seed database");
                }
                else
                {
                    await HandleApiError(response, "seed database");
                }
            }
            catch (HttpRequestException httpEx) { HandleNetworkError(httpEx, "seeding database"); }
            catch (Exception ex) { HandleUnexpectedError(ex, "seeding database"); }
            finally
            {
                if (seedButton != null) seedButton.IsEnabled = true;
            }
        }
        // **** END OF NEW HANDLER ****


        // --- API Helper Methods ---

        private async Task<bool> DeleteVehiculeOnServerAsync(string vehiculeId)
        {
            string deleteUrl = $"{VehiculesApiUrl}/{vehiculeId}"; // Use constant + ID
            Debug.WriteLine($"Attempting DELETE request to: {deleteUrl}");

            // Login check happens in DeleteButton_Click before calling this

            // Consider adding loading state specific to the delete button if needed
            bool success = false;

            try
            {
                HttpClient client = AuthManager.GetHttpClient();
                HttpResponseMessage response = await client.DeleteAsync(deleteUrl);

                if (response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"Successfully deleted vehicle ID {vehiculeId} on server.");
                    success = true;
                }
                else if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                {
                    HandleAuthenticationError("delete vehicle");
                }
                else
                {
                    await HandleApiError(response, "delete vehicle");
                }
            }
            catch (HttpRequestException httpEx) { HandleNetworkError(httpEx, "deleting vehicle"); }
            catch (Exception ex) { HandleUnexpectedError(ex, "deleting vehicle"); }

            return success; // Return success status
        }

        // --- Consolidated Error Handling Helpers ---

        private bool EnsureLoggedIn(string actionDescription)
        {
            if (!AuthManager.IsLoggedIn)
            {
                MessageBox.Show($"Vous devez être connecté pour {actionDescription}.", "Authentification requise", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        private void HandleAuthenticationError(string failedAction)
        {
            Debug.WriteLine($"Authentication error while trying to {failedAction}.");
            MessageBox.Show("Authentification échouée ou session expirée. Reconnectez-vous en relançant l'application.", "Erreur d'autorisation", MessageBoxButton.OK, MessageBoxImage.Warning);
            AuthManager.Logout();
            // Decide whether to shut down or just disable UI/redirect
            Application.Current.Shutdown();
        }

        private async Task HandleApiError(HttpResponseMessage response, string failedAction)
        {
            string errorBody = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"API error while trying to {failedAction}: {response.StatusCode} - {errorBody}");
            MessageBox.Show($"Failed to {failedAction}. Status: {response.StatusCode}\nDetails: {errorBody}", "API Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void HandleNetworkError(HttpRequestException httpEx, string failedAction)
        {
            Debug.WriteLine($"Network error while trying to {failedAction}: {httpEx.Message}");
            MessageBox.Show($"Failed to {failedAction}. Check network connection.\nError: {httpEx.Message}", "Network Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void HandleJsonError(JsonException jsonEx, string failedAction)
        {
            Debug.WriteLine($"JSON parsing error while trying to {failedAction}: {jsonEx.Message}");
            MessageBox.Show($"Failed to read data format from server while trying to {failedAction}.\nError: {jsonEx.Message}", "Data Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void HandleUnexpectedError(Exception ex, string failedAction)
        {
            Debug.WriteLine($"Unexpected error while trying to {failedAction}: {ex.Message}");
            MessageBox.Show($"An unexpected error occurred while trying to {failedAction}.\nError: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}