using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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

namespace CMSLDF
{
    public class DepotVenteBasic
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } // Used for Modify/Delete API calls

        [JsonPropertyName("hashId")]
        public string HashId { get; set; } // Public slug identifier

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
        public string Short { get; set; } // Short description

        [JsonPropertyName("images")]
        public List<string> Images { get; set; } // List of image URLs/paths
    }

    public class ApiResponseWrapper<T>
    {
        [JsonPropertyName("data")]
        public ObservableCollection<T> Data { get; set; }
    }
    public partial class DepotVenteView : UserControl
    {
        private const string ApiBaseUrl = "https://loueursdefrance.com/api/admin";
        // **** MODIFICATION START ****
        private const string DepotVentesApiUrl = $"{ApiBaseUrl}/db/vehiculeventes"; // Changed endpoint
        // ASSUMPTION: Adjust the seed endpoint if necessary
        private const string SeedVentesApiUrl = $"{ApiBaseUrl}/seedventes"; // Changed endpoint
        // **** MODIFICATION END ****

        // **** MODIFICATION START ****
        // Use the specific model and the generic wrapper (or a specific wrapper)
        public ObservableCollection<DepotVenteBasic> DepotVentes { get; } = new ObservableCollection<DepotVenteBasic>();
        // **** MODIFICATION END ****

        public DepotVenteView()
        {
            InitializeComponent();
            // **** MODIFICATION START ****
            DepotVentesItemsControl.ItemsSource = DepotVentes; // Bind to the correct collection and ItemsControl
            this.Loaded += DepotVenteView_Loaded; // Changed event handler name
            // **** MODIFICATION END ****
        }

        // **** MODIFICATION START ****
        private async void DepotVenteView_Loaded(object sender, RoutedEventArgs e) // Changed method name
        // **** MODIFICATION END ****
        {
            await FetchAndDisplayDataAsync();
        }

        private async Task FetchAndDisplayDataAsync()
        {
            if (!AuthManager.IsLoggedIn)
            {
                if (!this.IsLoaded)
                {
                    MessageBox.Show("You are not logged in. Please restart and log in.", "Authentication Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    this.IsEnabled = false;
                }
                return;
            }

            // **** MODIFICATION START ****
            Debug.WriteLine($"Attempting to fetch data from {DepotVentesApiUrl}..."); // Use correct URL
            this.IsEnabled = false;
            DepotVentes.Clear(); // Use correct collection
            // **** MODIFICATION END ****

            try
            {
                HttpClient client = AuthManager.GetHttpClient();
                // **** MODIFICATION START ****
                HttpResponseMessage response = await client.GetAsync(DepotVentesApiUrl); // Use correct URL

                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine("Data fetched successfully (Depot Ventes)."); // Updated log
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    // Use generic wrapper or specific wrapper for DepotVente
                    var wrappedData = JsonSerializer.Deserialize<ApiResponseWrapper<DepotVenteBasic>>(jsonResponse, options);
                    // Or: var wrappedData = JsonSerializer.Deserialize<DepotVenteApiResponseWrapper>(jsonResponse, options);

                    if (wrappedData?.Data != null)
                    {
                        foreach (var depotVente in wrappedData.Data) // Loop through correct type
                        {
                            DepotVentes.Add(depotVente); // Add to correct collection
                        }
                    }
                    Debug.WriteLine($"Displaying {DepotVentes.Count} depot ventes."); // Updated log
                    // **** MODIFICATION END ****
                }
                else if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                {
                    HandleAuthenticationError("fetch depot ventes data"); // Updated log context
                }
                else
                {
                    await HandleApiError(response, "load depot ventes data"); // Updated log context
                }
            }
            catch (HttpRequestException httpEx) { HandleNetworkError(httpEx, "loading depot ventes data"); } // Updated log context
            catch (JsonException jsonEx) { HandleJsonError(jsonEx, "reading depot ventes data"); } // Updated log context
            catch (Exception ex) { HandleUnexpectedError(ex, "loading depot ventes data"); } // Updated log context
            finally
            {
                this.IsEnabled = true;
            }
        }

        // --- Button Click Handlers ---

        private async void ModifyButton_Click(object sender, RoutedEventArgs e)
        {
            // **** MODIFICATION START ****
            // Check for the correct data type
            if (sender is Button { Tag: DepotVenteBasic itemToModify })
            {
                Debug.WriteLine($"Modify clicked for DepotVente: {itemToModify.Nom} (ID: {itemToModify.Id})"); // Updated log

                if (!EnsureLoggedIn("modifier le dépôt vente")) return; // Updated context message

                ModifyVehicleVenteWindow modifyWindow = new ModifyVehicleVenteWindow(itemToModify);
                modifyWindow.Owner = Window.GetWindow(this);
                bool? result = modifyWindow.ShowDialog();

                if (result == true)
                {
                    Debug.WriteLine($"Modification presumed successful for {itemToModify.Nom}. Refreshing list."); // Updated log
                    await FetchAndDisplayDataAsync();
                }
                else
                {
                    Debug.WriteLine($"Modification cancelled or failed for {itemToModify.Nom}."); // Updated log
                }
            }
            // **** MODIFICATION END ****
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            // **** MODIFICATION START ****
            // Check for the correct data type
            if (sender is Button { Tag: DepotVenteBasic itemToDelete })
            {
                Debug.WriteLine($"Delete clicked for DepotVente: {itemToDelete.Nom} (ID: {itemToDelete.Id})"); // Updated log

                if (!EnsureLoggedIn("supprimer le dépôt vente")) return; // Updated context message

                MessageBoxResult confirmResult = MessageBox.Show(
                    $"Êtes-vous sûr de vouloir supprimer le dépôt vente '{itemToDelete.Nom}'?", // Updated confirmation text
                    "Confirmer la suppression", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (confirmResult == MessageBoxResult.Yes)
                {
                    // Call the specific delete method for DepotVente
                    bool deletedOnServer = await DeleteDepotVenteOnServerAsync(itemToDelete.Id);
                    if (deletedOnServer)
                    {
                        // Remove from the correct ObservableCollection
                        DepotVentes.Remove(itemToDelete);
                        Debug.WriteLine($"Deleted {itemToDelete.Nom} (DepotVente). List count: {DepotVentes.Count}"); // Updated log
                        MessageBox.Show($"Dépôt vente '{itemToDelete.Nom}' supprimé avec succès.", "Supprimé", MessageBoxButton.OK, MessageBoxImage.Information); // Updated success message
                    }
                    // else: Error message shown in DeleteDepotVenteOnServerAsync
                }
            }
            // **** MODIFICATION END ****
        }

        // --- NEW CREATE BUTTON HANDLER ---
        private async void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Create Depot Vente button clicked.");

            // 1. Check Login Status
            if (!EnsureLoggedIn("créer un dépôt vente")) return; // Use specific context

            // 2. Open the Create Window (New window class name)
            CreateVehicleVenteWindow createWindow = new CreateVehicleVenteWindow();
            createWindow.Owner = Window.GetWindow(this);
            bool? result = createWindow.ShowDialog();

            // 3. Refresh list if creation was successful
            if (result == true)
            {
                Debug.WriteLine("Depot Vente creation successful. Refreshing list.");
                await FetchAndDisplayDataAsync(); // Refresh the list
            }
            else
            {
                Debug.WriteLine("Depot Vente creation cancelled or failed.");
            }
        }

        private async void SeedButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Seed Database (Ventes) button clicked."); // Updated log

            Button seedButton = sender as Button;

            if (!EnsureLoggedIn("seeder la base de données des ventes")) return; // Updated context message

            MessageBoxResult confirmResult = MessageBox.Show(
                "Êtes-vous sûrs de vouloir seeder la base de données des VENTES? Cela peut remplacer les données existantes par les valeurs par défaut.", // Updated confirmation text
                "Confirmer le seeding (Ventes)", // Updated title
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmResult != MessageBoxResult.Yes)
            {
                Debug.WriteLine("Seeding (Ventes) cancelled by user."); // Updated log
                return;
            }

            if (seedButton != null) seedButton.IsEnabled = false;
            // **** MODIFICATION START ****
            Debug.WriteLine($"Attempting GET request to {SeedVentesApiUrl}..."); // Use correct seed URL
            // **** MODIFICATION END ****

            try
            {
                HttpClient client = AuthManager.GetHttpClient();
                // **** MODIFICATION START ****
                // Assuming seed endpoint for ventes is also GET, adjust if it's POST
                HttpResponseMessage response = await client.GetAsync(SeedVentesApiUrl); // Use correct seed URL
                if (response.IsSuccessStatusCode)
                {
                    Debug.WriteLine("Database (Ventes) seeded successfully."); // Updated log
                    MessageBox.Show("Base de données (Ventes) seedée avec succès, veuillez attendre 6 secondes pour la mise à jour!", "Seed terminé", MessageBoxButton.OK, MessageBoxImage.Information); // Updated message

                    Debug.WriteLine("Waiting 6 seconds before refresh...");
                    await Task.Delay(6000);
                    Debug.WriteLine("6 seconds elapsed, refreshing data...");

                    await FetchAndDisplayDataAsync(); // Refresh the list
                }
                else if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                {
                    HandleAuthenticationError("seed ventes database"); // Updated context
                }
                else
                {
                    await HandleApiError(response, "seed ventes database"); // Updated context
                }
                // **** MODIFICATION END ****
            }
            catch (HttpRequestException httpEx) { HandleNetworkError(httpEx, "seeding ventes database"); } // Updated context
            catch (Exception ex) { HandleUnexpectedError(ex, "seeding ventes database"); } // Updated context
            finally
            {
                if (seedButton != null) seedButton.IsEnabled = true;
            }
        }


        // --- API Helper Methods ---

        // **** MODIFICATION START ****
        // Renamed and adapted for DepotVente
        private async Task<bool> DeleteDepotVenteOnServerAsync(string depotVenteId)
        {
            string deleteUrl = $"{DepotVentesApiUrl}/{depotVenteId}"; // Use correct API URL + ID
            Debug.WriteLine($"Attempting DELETE request to: {deleteUrl}");

            bool success = false;

            try
            {
                HttpClient client = AuthManager.GetHttpClient();
                HttpResponseMessage response = await client.DeleteAsync(deleteUrl);

                if (response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"Successfully deleted depot vente ID {depotVenteId} on server."); // Updated log
                    success = true;
                }
                else if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                {
                    HandleAuthenticationError("delete depot vente"); // Updated context
                }
                else
                {
                    await HandleApiError(response, "delete depot vente"); // Updated context
                }
            }
            catch (HttpRequestException httpEx) { HandleNetworkError(httpEx, "deleting depot vente"); } // Updated context
            catch (Exception ex) { HandleUnexpectedError(ex, "deleting depot vente"); } // Updated context

            return success;
        }
        // **** MODIFICATION END ****

        // --- Consolidated Error Handling Helpers ---
        // (Update text inside messages if needed for clarity)

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
            if (Application.Current != null) // Check if Application exists
            {
                Application.Current.Shutdown();
            }
        }

        private async Task HandleApiError(HttpResponseMessage response, string failedAction)
        {
            string errorBody = "N/A";
            try
            {
                errorBody = await response.Content.ReadAsStringAsync();
            }
            catch { } // Ignore exceptions reading body if response is bad

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
            Debug.WriteLine($"Unexpected error while trying to {failedAction}: {ex}"); // Log full exception for details
            MessageBox.Show($"An unexpected error occurred while trying to {failedAction}.\nError: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
