using Microsoft.Identity.Client;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            // Read secrets from environment variables
            string githubToken = Environment.GetEnvironmentVariable("GH_TOKEN");
            string clientId = Environment.GetEnvironmentVariable("CLIENT_ID");
            string clientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET");
            string tenantId = Environment.GetEnvironmentVariable("TENANT_ID");

            // Set up your GitHub repo details
            string repo = "domjv/gamechanger-flow"; // Replace with your repo details
            string filePath = "GameChanger.drawio";
            string branch = "main";
            
            // Step 1: Get file content from GitHub
            byte[] fileContent = await GetGitHubFileContent(repo, filePath, branch, githubToken);

            // Step 2: Authenticate with Microsoft Graph API and get access token
            string accessToken = await GetOnedriveAccessToken(clientId, clientSecret, tenantId);

            // Step 3: Upload file to OneDrive
            string uploadPath = "/personal/dominic_v_pleasantbiz_com/Documents/Flowcharts/GameChanger.drawio";
            await UploadFileToOnedrive(accessToken, fileContent, uploadPath);

            Console.WriteLine("File successfully uploaded to OneDrive.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    private static async Task<byte[]> GetGitHubFileContent(string repo, string filePath, string branch, string token)
    {
        string url = $"https://api.github.com/repos/{repo}/contents/{filePath}?ref={branch}";
        using (HttpClient client = new HttpClient())
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DotNetCoreApp", "1.0"));

            HttpResponseMessage response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"GitHub API returned an error: {response.StatusCode}");
            }

            string jsonResponse = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(jsonResponse);
            string encodedContent = jsonDoc.RootElement.GetProperty("content").GetString();
            return Convert.FromBase64String(encodedContent); // File is returned in Base64 format
        }
    }

    private static async Task<string> GetOnedriveAccessToken(string clientId, string clientSecret, string tenantId)
    {
        var app = ConfidentialClientApplicationBuilder.Create(clientId)
            .WithClientSecret(clientSecret)
            .WithAuthority(new Uri($"https://login.microsoftonline.com/{tenantId}"))
            .Build();

        string[] scopes = { "https://graph.microsoft.com/.default" };

        var authResult = await app.AcquireTokenForClient(scopes).ExecuteAsync();
        return authResult.AccessToken;
    }

    private static async Task UploadFileToOnedrive(string accessToken, byte[] fileContent, string uploadPath)
    {
        string userPrincipalName = "dominic.v@pleasantbiz.com";  // This should be your actual UPN
        string url = $"https://graph.microsoft.com/v1.0/users/{userPrincipalName}/drive/root:{uploadPath}:/content";

        using (HttpClient client = new HttpClient())
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            // client.DefaultRequestHeaders.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var content = new ByteArrayContent(fileContent);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            HttpResponseMessage response = await client.PutAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Error uploading file to OneDrive: {response.StatusCode} - {responseContent}");
            }
        }
    }
}
