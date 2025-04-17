using CMSLDF;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace CMSLDF // Adjust namespace accordingly
{

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
    public static class AuthManager
    {
        private static readonly HttpClient client = new HttpClient();
        public static string? AuthToken { get; private set; }

        public static bool IsLoggedIn => !string.IsNullOrEmpty(AuthToken);

        // --- Login Logic (Moved here for centralization) ---
        public static async Task<string?> GetAuthTokenAsync(string username, string password)
        {
            string loginUrl = "https://www.loueursdefrance.com/api/admin/login";
            string? token = null;

            try
            {
                var loginData = new LoginRequest { Username = username, Password = password };
                string jsonPayload = JsonSerializer.Serialize(loginData);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                // Use the shared client instance
                HttpResponseMessage response = await client.PostAsync(loginUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    var loginResponse = JsonSerializer.Deserialize<LoginResponse>(responseBody);
                    token = loginResponse?.Token;

                    if (string.IsNullOrEmpty(token))
                    {
                        MessageBox.Show("L'authentification a semblement réussi, mais le serveur n'a pas renvoyé le token d'authentification, vous ne pourrez donc rien modifier.", "Login Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    else
                    {
                        // Store the token upon successful retrieval
                        AuthToken = token;
                        // Optionally configure the shared client's default headers
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AuthToken);
                        System.Diagnostics.Debug.WriteLine("Login successful, token obtained and stored.");
                    }
                }
                else
                {
                    string errorBody = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"L'authentification a échoué. Statut de la requête: {response.StatusCode}\nDetails: {errorBody}",
                                    "Authentification échouée", MessageBoxButton.OK, MessageBoxImage.Error);
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

            return token; // Returns the token string on success, null otherwise
        }

        // --- Method to get the configured HttpClient ---
        public static HttpClient GetHttpClient()
        {
            // Ensure the token is applied if the user is logged in
            // (redundant if set in GetAuthTokenAsync, but good practice)
            if (IsLoggedIn && client.DefaultRequestHeaders.Authorization == null)
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AuthToken);
            }
            else if (!IsLoggedIn)
            {
                client.DefaultRequestHeaders.Authorization = null; // Clear if not logged in
            }
            return client;
        }

        // --- Method to Logout (optional but good practice) ---
        public static void Logout()
        {
            AuthToken = null;
            client.DefaultRequestHeaders.Authorization = null;
            // Potentially add logic here to navigate back to login screen if needed
        }
    }
}