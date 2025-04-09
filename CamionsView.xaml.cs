using System;
using System.Collections.Generic; // For List<T>
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace CMSLDF
{
    public class VehiculeBasic
    {
        public string Nom { get; set; }
        public string Taille { get; set; }
        public string Details { get; set; }
        public string Image { get; set; } // The URL as a string
    }

    public class ApiResponseWrapper
    {
        public List<VehiculeBasic> Data { get; set; }
    }

    public partial class CamionsView : UserControl
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private const string ApiUrl = "https://loueursdefrance.com/api/db/vehiculelocations";

        // We'll store the fetched data in a simple List for now
        private List<VehiculeBasic> AllVehicules = new List<VehiculeBasic>();
        private ApiResponseWrapper WrappedVehicles = new ApiResponseWrapper();

        public CamionsView()
        {
            InitializeComponent();
            // Add the Loaded event handler - this runs when the control is ready
            this.Loaded += CamionsView_Loaded;
        }

        // This method runs when the UserControl is loaded and shown on screen
        private async void CamionsView_Loaded(object sender, RoutedEventArgs e)
        {
            await FetchAndDisplayDataAsync();
        }

        // Method to get data from the internet and show it
        private async Task FetchAndDisplayDataAsync()
        {
            Debug.WriteLine("Attempting to fetch data...");
            try
            {
                // 1. Fetch data as string
                string jsonResponse = await httpClient.GetStringAsync(ApiUrl);
                Debug.WriteLine("Data fetched successfully.");

                // 2. Convert JSON string into a List of our VehiculeBasic objects
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                WrappedVehicles = JsonSerializer.Deserialize<ApiResponseWrapper>(jsonResponse, options);
                AllVehicules = WrappedVehicles.Data;

                // 3. Tell the ItemsControl in the XAML to display this list
                //    The ItemsControl will automatically use the ItemTemplate we defined
                //    for each item in the 'AllVehicules' list.
                VehiculesItemsControl.ItemsSource = AllVehicules;

                Debug.WriteLine($"Displaying {AllVehicules?.Count ?? 0} vehicules.");

            }
            catch (HttpRequestException httpEx)
            {
                Debug.WriteLine($"Network error: {httpEx.Message}");
                MessageBox.Show($"Failed to load data. Check network connection.\nError: {httpEx.Message}", "Network Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (JsonException jsonEx)
            {
                Debug.WriteLine($"JSON parsing error: {jsonEx.Message}");
                MessageBox.Show($"Failed to read data format from server.\nError: {jsonEx.Message}", "Data Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unexpected error: {ex.Message}");
                MessageBox.Show($"An unexpected error occurred.\nError: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // --- Button Click Handlers ---

        private void ModifyButton_Click(object sender, RoutedEventArgs e)
        {
            Button clickedButton = sender as Button;
            if (clickedButton != null)
            {
                // Retrieve the VehiculeBasic object from the Tag property
                VehiculeBasic itemToModify = clickedButton.Tag as VehiculeBasic;

                if (itemToModify != null)
                {
                    Debug.WriteLine($"Modify clicked for: {itemToModify.Nom}");

                    // --- Modification Window Logic ---
                    // 1. Créer une instance de la nouvelle fenêtre
                    ModifyVehicleWindow modifyWindow = new ModifyVehicleWindow(itemToModify);

                    // mettre notre fenêtre ici le owner
                    modifyWindow.Owner = Window.GetWindow(this); // 'this' refers to CamionsView UserControl/Window

                    // faire en sorte que modifyWindow soit un dialog, ce qui devrait pauser l'execution derriere
                    bool? result = modifyWindow.ShowDialog();

                    // 4. (Optional but Recommended) Check the result after the window closes.
                    //    We set DialogResult = true in ModifyVehicleWindow's SaveButton_Click.
                    if (result == true)
                    {
                        // The user clicked "Save" in the modify window.
                        Debug.WriteLine($"Changes saved for {itemToModify.Nom}.");

                        MessageBox.Show($"'{itemToModify.Nom}' a été modifé localement.\n", "Modification Complete");

                        // Reload la page depuis le cache
                        VehiculesItemsControl.ItemsSource = null;
                        VehiculesItemsControl.ItemsSource = AllVehicules; 
                    }
                    else
                    {
                        Debug.WriteLine($"Modification annulée pour {itemToModify.Nom}.");
                    }
                    // --- End Modification Window Logic ---
                }
                else
                {
                    Debug.WriteLine("ModifyButton clicked, but Tag was not a VehiculeBasic object.");
                }
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            Button clickedButton = sender as Button;
            if (clickedButton != null)
            {
                // Get the item associated with this button
                VehiculeBasic itemToDelete = clickedButton.Tag as VehiculeBasic;

                if (itemToDelete != null)
                {
                    Debug.WriteLine($"Delete clicked for: {itemToDelete.Nom}");

                    // Ask for confirmation
                    MessageBoxResult result = MessageBox.Show(
                        $"Are you sure you want to delete '{itemToDelete.Nom}'?",
                        "Confirm Delete",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        // 1. Remove the item from our data list
                        AllVehicules.Remove(itemToDelete);

                        // 2. IMPORTANT: Refresh the ItemsControl's view of the data
                        //    Since we used a simple List, the UI doesn't update automatically.
                        //    We need to tell it to reload the (now smaller) list.
                        //    Setting ItemsSource to null and then back forces a refresh.
                        VehiculesItemsControl.ItemsSource = null; // Clear the binding temporarily
                        VehiculesItemsControl.ItemsSource = AllVehicules; // Re-assign the updated list

                        Debug.WriteLine($"Deleted {itemToDelete.Nom}. List count: {AllVehicules.Count}");

                        // TODO: Add code here to call your API to delete the item on the server
                    }
                }
            }
        }
    }
}