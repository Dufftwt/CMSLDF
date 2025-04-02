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
            // 'sender' is the actual Button that was clicked
            Button clickedButton = sender as Button;
            if (clickedButton != null)
            {
                // Retrieve the VehiculeBasic object we stored in the Tag property in XAML
                VehiculeBasic itemToModify = clickedButton.Tag as VehiculeBasic;

                if (itemToModify != null)
                {
                    // Now you know which truck needs modification!
                    Debug.WriteLine($"Modify clicked for: {itemToModify.Nom}");
                    MessageBox.Show($"Modify: {itemToModify.Nom}\nDetails: {itemToModify.Details}", "Modify Item");
                    // TODO: Add your actual modification logic here (e.g., open a new window)
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