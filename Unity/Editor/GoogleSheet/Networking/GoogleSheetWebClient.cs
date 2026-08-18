using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace PschLib.GoogleSheets
{
    internal sealed class GoogleSheetWebClient
    {
        private readonly string _webAppUrl;

        public GoogleSheetWebClient(string webAppUrl)
        {
            if (string.IsNullOrWhiteSpace(webAppUrl))
            {
                throw new ArgumentException("The web app URL is empty.", nameof(webAppUrl));
            }

            _webAppUrl = webAppUrl.Trim();
        }

        public async Task<GoogleSheetListResponse> GetSheetListAsync()
        {
            var json = await GetJsonAsync(BuildUrl("action=list"));
            return Deserialize<GoogleSheetListResponse>(json);
        }

        public async Task<GoogleSheetProjectListResponse> GetProjectsAsync()
        {
            var json = await GetJsonAsync(BuildUrl("action=projects"));
            return Deserialize<GoogleSheetProjectListResponse>(json);
        }

        public async Task<GoogleSheetProjectRegistrationResponse> RegisterProjectAsync(string key, string spreadsheetId)
        {
            var form = new WWWForm();
            form.AddField("action", "register");
            form.AddField("key", RequireValue(key, nameof(key)));
            form.AddField("spreadsheetId", RequireValue(spreadsheetId, nameof(spreadsheetId)));

            var json = await PostFormJsonAsync(_webAppUrl, form);
            return Deserialize<GoogleSheetProjectRegistrationResponse>(json);
        }

        public async Task<GoogleSheetListResponse> GetSheetListAsync(string spreadsheetId)
        {
            var id = Escape(spreadsheetId, nameof(spreadsheetId));
            var json = await GetJsonAsync(BuildUrl($"action=sheets&spreadsheetId={id}"));
            return Deserialize<GoogleSheetListResponse>(json);
        }

        public async Task<GoogleSheetDataResponse> GetSheetDataAsync(int sheetId)
        {
            var json = await GetJsonAsync(BuildUrl($"action=data&sheetId={sheetId}"));
            return Deserialize<GoogleSheetDataResponse>(json);
        }

        public async Task<GoogleSheetDataResponse> GetSheetDataAsync(string spreadsheetId, int sheetId)
        {
            var id = Escape(spreadsheetId, nameof(spreadsheetId));
            var json = await GetJsonAsync(BuildUrl($"action=data&spreadsheetId={id}&sheetId={sheetId}"));
            return Deserialize<GoogleSheetDataResponse>(json);
        }

        private string BuildUrl(string query)
        {
            var separator = _webAppUrl.Contains("?") ? "&" : "?";
            return $"{_webAppUrl}{separator}{query}";
        }

        private static string Escape(string value, string parameterName)
        {
            return UnityWebRequest.EscapeURL(RequireValue(value, parameterName));
        }

        private static string RequireValue(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("The request parameter is empty.", parameterName);
            }

            return value.Trim();
        }

        private static Task<string> GetJsonAsync(string url)
        {
            return SendJsonAsync(UnityWebRequest.Get(url));
        }

        private static Task<string> PostFormJsonAsync(string url, WWWForm form)
        {
            return SendJsonAsync(UnityWebRequest.Post(url, form));
        }

        private static Task<string> SendJsonAsync(UnityWebRequest request)
        {
            var completion = new TaskCompletionSource<string>();
            request.redirectLimit = 10;

            var operation = request.SendWebRequest();
            operation.completed += _ =>
            {
                try
                {
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        completion.TrySetException(new InvalidOperationException($"Google Sheet request failed: {request.error}"));
                        return;
                    }

                    var json = request.downloadHandler.text;

                    if (string.IsNullOrWhiteSpace(json))
                    {
                        completion.TrySetException(new InvalidOperationException("Google Sheet returned an empty response."));
                        return;
                    }

                    completion.TrySetResult(json);
                }
                finally
                {
                    request.Dispose();
                }
            };

            return completion.Task;
        }

        private static T Deserialize<T>(string json)
        {
            try
            {
                var response = JsonUtility.FromJson<T>(json);

                if (response == null)
                {
                    throw new InvalidOperationException("Google Sheet returned an invalid JSON response.");
                }

                return response;
            }
            catch (Exception exception) when (!(exception is InvalidOperationException))
            {
                throw new InvalidOperationException("Google Sheet response could not be parsed.", exception);
            }
        }
    }
}
